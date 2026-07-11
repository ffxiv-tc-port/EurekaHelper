using Dalamud.Interface.Windowing;
using System;
using ImGuiNET;
using System.Numerics;
using Dalamud.Interface;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Logging;
using Dalamud.Interface.Components;
using Dalamud.Game.Text;
using Dalamud.Interface.Colors;
using EurekaHelper.System;
using EurekaHelper.XIV;

namespace EurekaHelper.Windows
{
    public class PluginWindow : Window, IDisposable
    {
        private readonly EurekaHelper Plugin = null!;

        public PluginWindow(EurekaHelper plugin) : base(Loc.Text("Eureka Helper"))
        {
            Plugin = plugin;
            SizeConstraints = new WindowSizeConstraints
                { MinimumSize = new Vector2(566, 520), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };
        }

        // Index 1-4 = Anemos/Pagos/Pyros/Hydatos (see Utils.GetIndexOfZone). Each zone keeps its
        // own independent tracker connection/UI state - you're never expected to be connected to
        // more than one at a time (can't be in two maps at once), but switching zone tabs (or
        // ZoneManager auto-reconnecting on zone entry) shouldn't lose what was set up for the
        // others.
        private static readonly EurekaConnectionManager[] Connections = new EurekaConnectionManager[5];
        private int SelectedTrackerZoneIndex = 1;

        private EurekaConnectionManager Connection
        {
            get => Connections[SelectedTrackerZoneIndex] ??= new EurekaConnectionManager();
            set => Connections[SelectedTrackerZoneIndex] = value;
        }

        public EurekaConnectionManager GetConnection(int zoneIndex) => Connections[zoneIndex] ??= new EurekaConnectionManager();

        public void SetConnection(int zoneIndex, EurekaConnectionManager connection) => Connections[zoneIndex] = connection;

        public void Dispose()
        {
        }

        public override void Draw()
        {
            if (ImGui.BeginTabBar("EHelperTab"))
            {
                if (ImGui.BeginTabItem(Loc.Text("Tracker")))
                {
                    DrawTrackerTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem(Loc.Text("Elementals")))
                {
                    DrawElementalTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem(Loc.Text("Configuration")))
                {
                    DrawSettingsTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem(Loc.Text("Instance")))
                {
                    DrawInstanceTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem(Loc.Text("About")))
                {
                    DrawAboutTab();
                    ImGui.EndTabItem();
                }

                if (EurekaHelper.Config.EnableSplatoonAggroRanges && ImGui.BeginTabItem(Loc.Text("Debug")))
                {
                    DrawDebugTab();
                    ImGui.EndTabItem();
                }
            }
        }

        // Index 1-4 = Anemos/Pagos/Pyros/Hydatos, mirroring Connections - these are just the
        // input-box text for the Code/Password fields, kept per-zone so typing/joining in one
        // zone tab doesn't overwrite what's shown in another.
        private readonly string[] TrackerCodeInputs = new string[5];
        private readonly string[] TrackerPasswordInputs = new string[5];

        public string TrackerCode
        {
            get => TrackerCodeInputs[SelectedTrackerZoneIndex] ??= string.Empty;
            set => TrackerCodeInputs[SelectedTrackerZoneIndex] = value;
        }

        public string TrackerPassword
        {
            get => TrackerPasswordInputs[SelectedTrackerZoneIndex] ??= string.Empty;
            set => TrackerPasswordInputs[SelectedTrackerZoneIndex] = value;
        }

        private AggroType DebugAggroType = AggroType.Aural;
        private AggroShape DebugAggroShape = AggroShape.Circle;
        private float DebugRadius = 10f;
        private int DebugConeHalfAngle = 60;
        private Vector4 DebugColor = new(1f, 1f, 0f, 1f);
        private float DebugThickness = 2f;
        private string DebugBossNameOverride = string.Empty;

        public void DrawTrackerTab()
        {
            if (ImGui.BeginTabBar("EHelperTrackerZoneTab"))
            {
                for (var zoneIndex = 1; zoneIndex <= Constants.EurekaZones.Length; zoneIndex++)
                {
                    var zoneName = Loc.Text(Utils.GetZoneName(Constants.EurekaZones[zoneIndex - 1]));
                    if (ImGui.BeginTabItem(zoneName))
                    {
                        SelectedTrackerZoneIndex = zoneIndex;
                        DrawTrackerZoneTab();
                        ImGui.EndTabItem();
                    }
                }

                ImGui.EndTabBar();
            }
        }

        public async void DrawTrackerZoneTab()
        {
            ImGui.AlignTextToFramePadding();
            ImGui.Text(Loc.Text("Settings:"));
            ImGui.SameLine();

            ImGui.SameLine();

            if (!Connection.IsConnected())
            {
                // Each tab is already scoped to a single zone, so there's no need to pick which
                // zone's tracker to create - just create one for whichever tab is currently open.
                var zoneIndex = SelectedTrackerZoneIndex;
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus))
                    _ = Task.Run(async () => { await CreateTracker(zoneIndex); });
                Utils.SetTooltip(Loc.Text("Create a new tracker"));
            }

            else if (Connection.IsConnected())
            {
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Link))
                    Utils.CopyToClipboard(
                        $"{Utils.CombineUrl(Constants.EurekaTrackerLink, Connection.GetTrackerId())}");
                Utils.SetTooltip(Loc.Text("Copy tracker link to clipboard"));

                if (Connection.CanModify())
                {
                    ImGui.SameLine();

                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Key))
                        Utils.CopyToClipboard(Loc.Format("Password: {0}", Connection.GetTrackerPassword()));
                    Utils.SetTooltip(Loc.Text("Copy tracker password to clipboard"));

                    ImGui.SameLine();

                    if (Connection.IsPublic())
                    {
                        if (ImGuiComponents.IconButton(FontAwesomeIcon.Lock))
                            await Connection.SetTrackerVisiblity();

                        Utils.SetTooltip(Loc.Text("Set tracker to private"));
                    }
                    else
                    {
                        if (ImGuiComponents.IconButton(FontAwesomeIcon.LockOpen))
                        {

                            if (Plugin.CurrentDatacenterId == 0)
                                EurekaHelper.PrintMessage(
                                    Loc.Text("This datacenter is not supported currently. Please submit an issue if you think this is incorrect."));
                            else
                                await Connection.SetTrackerVisiblity(Plugin.CurrentDatacenterId);
                        }

                        Utils.SetTooltip(Loc.Text("Set tracker to public"));
                    }
                }

                ImGui.SameLine();

                if (ImGuiComponents.IconButton(FontAwesomeIcon.Globe))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = $"{Constants.EurekaTrackerLink}{Connection.GetTrackerId()}",
                        UseShellExecute = true
                    });
                }

                Utils.SetTooltip(Loc.Text("Opens the tracker in a browser"));

                ImGui.SameLine();

                if (ImGuiComponents.IconButton(FontAwesomeIcon.FileExport))
                {
                    var zoneIndex = SelectedTrackerZoneIndex;
                    var oldTrackerId = Connection.GetTrackerId();
                    _ = Task.Run(async () => { await ExportTracker(zoneIndex, oldTrackerId); });
                }
                Utils.SetTooltip(Loc.Text("Exports the current tracker to a new one"));

                ImGui.SameLine();

                if (ImGuiComponents.IconButton(FontAwesomeIcon.Sync))
                    ZoneManager.RebuildTrackerConnection(SelectedTrackerZoneIndex);
                Utils.SetTooltip(Loc.Text("Rebuild tracker connection"));

                ImGui.SameLine();

                if (ImGuiComponents.IconButton(FontAwesomeIcon.SignOutAlt))
                {
                    _ = Task.Run(async () => { await Connection.Close(); });
                }

                Utils.SetTooltip(Loc.Text("Leave the current tracker"));

                ImGui.SameLine();

                ImGuiComponents.IconButton(FontAwesomeIcon.CloudSun);
                if (ImGui.IsItemHovered())
                {
                    ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1f);
                    ImGui.PushStyleColor(ImGuiCol.Border, ImGui.GetColorU32(ImGuiCol.TabActive));

                    ImGui.BeginTooltip();

                    float spacing = ImGui.GetStyle().ItemInnerSpacing.X;
                    ImGui.Text(Loc.Text("E.T:"));
                    ImGui.SameLine(0.0f, spacing);
                    ImGui.TextColored(new Vector4(0.0f, 1.0f, 0.0f, 1.0f), $"{EorzeaTime.Now.EorzeaDateTime:HH:mm}");
                    ImGui.SameLine(0.0f, spacing);

                    if (EorzeaTime.Now.EorzeaDateTime.Hour < 6 || EorzeaTime.Now.EorzeaDateTime.Hour >= 19)
                    {
                        ImGui.Text(Loc.Text("(Night)"));
                        ImGui.Text(Loc.Format("Day in {0}", EorzeaTime.Now.TimeUntilDay().ToString("mm'm 'ss's'")));
                    }
                    else
                    {
                        ImGui.Text(Loc.Text("(Day)"));
                        ImGui.Text(Loc.Format("Night in {0}", EorzeaTime.Now.TimeUntilNight().ToString("mm'm 'ss's'")));
                    }

                    ImGui.Dummy(new Vector2(0.0f, 10.0f));

                    ImGui.Text(Loc.Text("Weather:"));
                    ImGui.SameLine(0.0f, spacing);
                    ImGui.TextColored(new Vector4(0.0f, 1.0f, 0.0f, 1.0f),
                        $"{Connection.GetTracker().GetCurrentWeatherInfo().Weather.ToFriendlyString()}");
                    ImGui.Text(Loc.Format("Ends in {0}", Connection.GetTracker().GetCurrentWeatherInfo().Timeleft.ToString("mm'm 'ss's'")));

                    ImGui.Dummy(new Vector2(0.0f, 10.0f));

                    ImGui.Text(Loc.Text("Weather Forecast:"));
                    var weatherForecast = Connection.GetTracker().GetAllNextWeatherTime();
                    foreach (var (Weather, Time) in weatherForecast)
                    {
                        ImGui.TextColored(PurpleColorText, Weather.ToFriendlyString());
                        ImGui.SameLine(0.0f, ImGui.GetStyle().ItemInnerSpacing.X);
                        ImGui.Text(Loc.Format("in: {0}", Time.ToString(Time.Hours > 0 ? "hh'h 'mm'm 'ss's'" : "mm'm 'ss's'")));
                    }

                    ImGui.EndTooltip();

                    ImGui.PopStyleVar();
                    ImGui.PopStyleColor();
                }

                ImGui.SameLine();

                ImGuiComponents.IconButton(FontAwesomeIcon.InfoCircle);
                if (ImGui.IsItemHovered())
                {
                    ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1f);
                    ImGui.PushStyleColor(ImGuiCol.Border, ImGui.GetColorU32(ImGuiCol.TabActive));

                    ImGui.BeginTooltip();

                    ImGui.TextColored(GreenColorText, Loc.Text("Green"));
                    ImGui.SameLine(0.0f, ImGui.GetStyle().ItemInnerSpacing.X);
                    ImGui.Text(Loc.Text("=> Ready to be spawned"));
                    ImGui.TextColored(RedColorText, Loc.Text("Red"));
                    ImGui.SameLine(0.0f, ImGui.GetStyle().ItemInnerSpacing.X);
                    ImGui.Text(Loc.Text("=> Has been popped and is on a respawn timer"));
                    ImGui.TextColored(OrangeColorText, Loc.Text("Orange"));
                    ImGui.SameLine(0.0f, ImGui.GetStyle().ItemInnerSpacing.X);
                    ImGui.Text(Loc.Text("=> One of the requirements is not met to spawn/prep the NM"));

                    ImGui.EndTooltip();

                    ImGui.PopStyleVar();
                    ImGui.PopStyleColor();
                }

                var idViewersText = ZoneManager.CurrentZoneIndex == SelectedTrackerZoneIndex && ZoneManager.CurrentServerId != 0
                    ? Loc.Format("ID: {0}\t\tServer ID: {1}\t\tViewers: {2}", Connection.GetTrackerId(), ZoneManager.CurrentServerId, Connection.GetViewers())
                    : Loc.Format("ID: {0}\t\tViewers: {1}", Connection.GetTrackerId(), Connection.GetViewers());
                ImGui.SameLine(ImGui.GetContentRegionAvail().X - ImGui
                    .CalcTextSize(idViewersText).X);
                ImGui.AlignTextToFramePadding();
                ImGui.Text(idViewersText);
            }

            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(5.0f, 5.0f));
            if (ImGui.BeginTable("TrackerConnectionSettings", 3,
                    ImGuiTableFlags.Borders | ImGuiTableFlags.NoBordersInBody))
            {
                ImGui.TableSetupColumn(Loc.Text("Code"), ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn(Loc.Text("Password"), ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn(Loc.Text("Button"));

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.Text(Loc.Text("Code:"));
                ImGui.SameLine();
                ImGui.SetNextItemWidth(110f);
                var trackerCode = TrackerCode;
                ImGui.InputTextWithHint("##TrackerCode", Loc.Text("Enter 6 digit code"), ref trackerCode, 6);
                TrackerCode = trackerCode;

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.Text(Loc.Text("Password:"));
                ImGui.SameLine();
                ImGui.SetNextItemWidth(200f);
                var trackerPassword = TrackerPassword;
                ImGui.InputTextWithHint("##TrackerPassword", Loc.Text("Enter tracker password"), ref trackerPassword, 100);
                TrackerPassword = trackerPassword;
                Utils.SetTooltip(
                    Loc.Text("Don't input if you just want to join a tracker.\nIf you have the password, enter the correct password or you'll need to press \"Set\" again."));

                ImGui.TableNextColumn();
                if (ImGui.Button(Loc.Text("Set"), new Vector2(ImGui.GetContentRegionAvail().X, 0.0f)))
                {
                    if (!string.IsNullOrWhiteSpace(TrackerCode))
                    {
                        // Capture which zone tab this was clicked from now (synchronously) rather
                        // than reading SelectedTrackerZoneIndex again once the await completes -
                        // the user may have switched tabs by then, which would otherwise silently
                        // apply the result to the wrong zone's connection.
                        var zoneIndex = SelectedTrackerZoneIndex;
                        var conn = Connections[zoneIndex] ??= new EurekaConnectionManager();
                        var enteredCode = TrackerCode;
                        var enteredPassword = TrackerPassword;
                        _ = Task.Run(async () =>
                        {
                            if (conn.GetTrackerId() == enteredCode)
                            {
                                if (conn.IsConnected() && !conn.CanModify() &&
                                    !string.IsNullOrWhiteSpace(enteredPassword))
                                    await conn.SetPassword(enteredPassword);
                            }
                            else
                            {
                                if (conn.IsConnected())
                                    await conn.Close();

                                Connections[zoneIndex] = await EurekaConnectionManager.JoinTracker(enteredCode, enteredPassword);
                            }
                        });
                    }
                }

                Utils.SetTooltip(Loc.Text("Joins a tracker with the specified ID and password"));

                ImGui.EndTable();
            }

            ImGui.PopStyleVar();

            ImGui.Spacing();
            DrawTrackerTable();
        }

        public async Task CreateTracker(int zoneId, bool printMessage = false)
        {
            (string trackerId, string password) = await EurekaConnectionManager.CreateTracker(zoneId);

            if (string.IsNullOrWhiteSpace(trackerId) && string.IsNullOrWhiteSpace(password))
            {
                DalamudApi.Log.Error("TrackerId and Password not returned from API for some reason.");
                return;
            }

            var existing = Connections[zoneId];
            if (existing != null && existing.IsConnected())
                await existing.Close();

            TrackerCodeInputs[zoneId] = trackerId;
            TrackerPasswordInputs[zoneId] = password;
            Connections[zoneId] = await EurekaConnectionManager.JoinTracker(trackerId, password);

            if (printMessage)
                EurekaHelper.PrintMessage(
                    Loc.Format("Successfully created a tracker: {0}", Utils.CombineUrl(Constants.EurekaTrackerLink, trackerId)));
        }

        public async Task ExportTracker(int zoneIndex, string oldTrackerId, bool printMessage = false)
        {
            (string trackerId, string password) = await EurekaConnectionManager.ExportTracker(oldTrackerId);

            if (string.IsNullOrWhiteSpace(trackerId) && string.IsNullOrWhiteSpace(password))
            {
                DalamudApi.Log.Error("TrackerId and Password not returned from API for some reason.");
                return;
            }

            var existing = Connections[zoneIndex];
            if (existing != null && existing.IsConnected())
                await existing.Close();

            TrackerCodeInputs[zoneIndex] = trackerId;
            TrackerPasswordInputs[zoneIndex] = password;
            Connections[zoneIndex] = await EurekaConnectionManager.JoinTracker(trackerId, password);

            if (printMessage)
                EurekaHelper.PrintMessage(
                    Loc.Format("Successfully exported the previous tracker: {0}", Utils.CombineUrl(Constants.EurekaTrackerLink, trackerId)));
        }

        public void DrawTrackerTable()
        {
            ImGui.PushStyleColor(ImGuiCol.Border, ImGui.GetColorU32(ImGuiCol.TabActive));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0.0f, 0.0f));
            ImGui.BeginChild("EurekaTracker",
                new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y), true);
            ImGui.PopStyleColor();
            ImGui.PopStyleVar();

            if (Connection.IsConnected())
            {
                var numColumns = 6;
                if (ImGui.BeginTable("TrackerTable", numColumns,
                        ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.BordersV |
                        ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.ScrollY | ImGuiTableFlags.NoSavedSettings |
                        ImGuiTableFlags.Sortable | ImGuiTableFlags.SortTristate))
                {
                    var levelTableColumnFlags = ImGuiTableColumnFlags.WidthFixed;
                    if (!EurekaHelper.Config.ShowLevelInTrackerTable)
                        levelTableColumnFlags |= ImGuiTableColumnFlags.Disabled;

                    var resetAllText = Loc.Text("Reset All");
                    ImGui.TableSetupColumn(Loc.Text("Lv"), levelTableColumnFlags);
                    ImGui.TableSetupColumn(Loc.Text("NM"), ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn(Loc.Text("Spawned By"), ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn(Loc.Text("Popped At"));
                    ImGui.TableSetupColumn(Loc.Text("Respawn In"));
                    ImGui.TableSetupColumn(resetAllText,
                        ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.NoSort);
                    ImGui.TableSetupScrollFreeze(0, 1);

                    ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
                    for (int column = 0; column < numColumns; column++)
                    {
                        ImGui.TableSetColumnIndex(column);
                        string columnName = ImGui.TableGetColumnName(column);
                        if (columnName != resetAllText)
                        {
                            ImGui.TableHeader(columnName);
                            continue;
                        }

                        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0.0f, 0.0f));
                        if (Connection.CanModify())
                        {
                            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.89f, 0.5f, 0.5f, 1.0f));
                            if (ImGui.Button(resetAllText, new Vector2(ImGui.GetContentRegionAvail().X, 0.0f)))
                                ImGui.OpenPopup("Confirm");
                            ImGui.PopStyleColor();
                        }
                        else
                        {
                            ImGui.BeginDisabled();
                            ImGui.Button(resetAllText, new Vector2(ImGui.GetContentRegionAvail().X, 0.0f));
                            ImGui.EndDisabled();
                        }

                        ImGui.PopStyleVar();
                    }

                    ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1f);
                    ImGui.PushStyleColor(ImGuiCol.Border, ImGui.GetColorU32(ImGuiCol.TabActive));
                    if (ImGui.BeginPopup("Confirm"))
                    {
                        if (ImGui.SmallButton(Loc.Text("Confirm?")))
                        {
                            _ = Task.Run(async () => { await Connection.ResetAll(); });
                            ImGui.CloseCurrentPopup();
                        }

                        ImGui.EndPopup();
                    }

                    ImGui.PopStyleVar();
                    ImGui.PopStyleColor();

                    DrawTracker();

                    ImGui.EndTable();
                }
            }
            else
            {
                if (Connection.IsInvalid())
                    Utils.CenterText(Loc.Text("Invalid Tracker"));
                else
                    Utils.CenterText(Loc.Text("Not connected to a tracker"));
            }

            ImGui.EndChild();
        }

        static readonly Vector4 RedColor = new(0.89f, 0.5f, 0.5f, 1f);
        static readonly Vector4 BlueColor = new(0.26f, 0.44f, 0.64f, 1f);

        static readonly Vector4 GreenColorText = new(0.33f, 0.76f, 0.67f, 1f);
        static readonly Vector4 RedColorText = new(0.82f, 0.49f, 0.49f, 1f);
        static readonly Vector4 OrangeColorText = new(0.9f, 0.52f, 0f, 1f);
        static readonly Vector4 PurpleColorText = new Vector4(1.0f, 0.4f, 1.0f, 1.0f);

        private string TimeAgoHours = "0";
        private string TimeAgoMinutes = "0";
        private bool IsEditing = false;

        private void DrawTracker()
        {
            var zoneFates = Connection.GetTracker()?.GetFates().Where(x => x.IncludeInTracker).ToList();
            if (zoneFates is null)
                return;

            var minRowHeight = ImGui.GetContentRegionAvail().Y / zoneFates.Count;
            var spacing = ImGui.GetStyle().ItemInnerSpacing.X;

            var sortSpecs = ImGui.TableGetSortSpecs();
            if (sortSpecs.SpecsDirty)
            {
                var specsCount = sortSpecs.SpecsCount;
                if (specsCount > 0)
                {
                    switch (sortSpecs.Specs.ColumnIndex, sortSpecs.Specs.SortDirection)
                    {
                        case (0, ImGuiSortDirection.Ascending):
                            zoneFates = zoneFates.OrderBy(x => x.FateLevel).ToList();
                            break;
                        case (0, ImGuiSortDirection.Descending):
                            zoneFates = zoneFates.OrderByDescending(x => x.FateLevel).ToList();
                            break;
                        case (1, ImGuiSortDirection.Ascending):
                            zoneFates = zoneFates.OrderBy(x => x.BossName).ToList();
                            break;
                        case (1, ImGuiSortDirection.Descending):
                            zoneFates = zoneFates.OrderByDescending(x => x.BossName).ToList();
                            break;
                        case (2, ImGuiSortDirection.Ascending):
                            zoneFates = zoneFates.OrderBy(x => x.SpawnedBy).ToList();
                            break;
                        case (2, ImGuiSortDirection.Descending):
                            zoneFates = zoneFates.OrderByDescending(x => x.SpawnedBy).ToList();
                            break;
                        case (3, ImGuiSortDirection.Ascending):
                            zoneFates = zoneFates.OrderBy(x => x.IsPopped()).ThenBy(x => x.GetRespawnTimeleft())
                                .ThenBy(x => x.FateLevel).ToList();
                            break;
                        case (3, ImGuiSortDirection.Descending):
                            zoneFates = zoneFates.OrderBy(x => !x.IsPopped())
                                .ThenByDescending(x => x.GetRespawnTimeleft()).ThenBy(x => x.FateLevel).ToList();
                            break;
                        case (4, ImGuiSortDirection.Ascending):
                            zoneFates = zoneFates
                                .OrderBy(x =>
                                    x.GetRespawnRequirements(Connection.GetTracker())
                                        .OrderBy(y => !y.Action.Equals("Respawn"))
                                        .ThenByDescending(y => y.Time)
                                        .FirstOrDefault().Time)
                                .ToList();
                            break;
                        case (4, ImGuiSortDirection.Descending):
                            zoneFates = zoneFates
                                .OrderByDescending(x =>
                                    x.GetRespawnRequirements(Connection.GetTracker())
                                        .OrderBy(y => !y.Action.Equals("Respawn"))
                                        .ThenByDescending(y => y.Time)
                                        .FirstOrDefault().Time)
                                .ToList();
                            break;
                    }
                }
            }

            foreach (var fate in zoneFates)
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, minRowHeight);

                // Fate Level
                ImGui.TableSetColumnIndex(0);
                ImGui.Text(fate.FateLevel.ToString());

                // NM Boss Name
                ImGui.TableNextColumn();
                Loc.TryEurekaName(fate.BossName, out var bossNameText);
                ImGui.Text(bossNameText);
                if (ImGui.IsItemHovered())
                {
                    ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1f);
                    ImGui.PushStyleColor(ImGuiCol.Border, ImGui.GetColorU32(ImGuiCol.TabActive));

                    ImGui.BeginTooltip();
                    ImGui.Text(Loc.Format("FATE Name: {0}", fate.FateName));
                    ImGui.Text(Loc.Format("FATE Level: {0}", fate.FateLevel));
                    ImGui.Text(Loc.Text("Element:"));
                    ImGui.SameLine(0.0f, spacing);
                    ImGui.TextColored(new Vector4(0.68f, 0.88f, 0.12f, 1.0f), fate.BossElement.ToFriendlyString());
                    if (fate.SpawnRequiredWeather != EurekaWeather.None)
                    {
                        ImGui.Text(Loc.Text("Weather Required:"));
                        ImGui.SameLine(0.0f, spacing);
                        ImGui.TextColored(PurpleColorText, fate.SpawnRequiredWeather.ToFriendlyString());
                    }

                    ImGui.EndTooltip();

                    ImGui.PopStyleVar();
                    ImGui.PopStyleColor();
                }

                if (ImGui.IsItemClicked())
                    Utils.SetFlagMarker(fate, openMap: true);

                // Spawned By
                ImGui.TableNextColumn();
                Loc.TryEurekaName(fate.SpawnedBy, out var spawnedByText);
                ImGui.Text(spawnedByText);
                if (ImGui.IsItemHovered())
                {
                    ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1f);
                    ImGui.PushStyleColor(ImGuiCol.Border, ImGui.GetColorU32(ImGuiCol.TabActive));

                    ImGui.BeginTooltip();
                    ImGui.Text(Loc.Text("Element:"));
                    ImGui.SameLine(0.0f, spacing);
                    ImGui.TextColored(new Vector4(0.68f, 0.88f, 0.12f, 1.0f), fate.SpawnByElement.ToFriendlyString());

                    if (fate.SpawnByRequiredNight)
                        ImGui.Text(Loc.Text("Night Required"));

                    if (fate.SpawnByRequiredWeather != EurekaWeather.None)
                    {
                        ImGui.Text(Loc.Text("Weather Required:"));
                        ImGui.SameLine(0.0f, spacing);
                        ImGui.TextColored(PurpleColorText, fate.SpawnByRequiredWeather.ToFriendlyString());
                    }

                    ImGui.EndTooltip();

                    ImGui.PopStyleVar();
                    ImGui.PopStyleColor();
                }

                if (ImGui.IsItemClicked())
                    Utils.SetFlagMarker(fate.TerritoryId, fate.MapId,
                        new Vector2(fate.SpawnByPosition.X, fate.SpawnByPosition.Y), openMap: true, drawCircle: true);

                // Popped At
                ImGui.TableNextColumn();
                if (fate.IsPopped())
                {
                    Utils.RightAlignTextInColumn(fate.GetPoppedTime().ToString("HH:mm"), RedColorText);
                    if (Connection.CanModify())
                    {
                        if (ImGui.IsItemClicked())
                        {
                            IsEditing = false;
                            ImGui.OpenPopup($"EditPopTime##{fate.TrackerId}");
                        }
                    }

                    ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1f);
                    ImGui.PushStyleColor(ImGuiCol.Border, ImGui.GetColorU32(ImGuiCol.TabActive));
                    Utils.SetTooltip(Loc.Format("Popped on {0} local time", fate.GetPoppedTime()));
                    ImGui.PopStyleVar();
                    ImGui.PopStyleColor();

                    ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1f);
                    ImGui.PushStyleColor(ImGuiCol.Border, ImGui.GetColorU32(ImGuiCol.TabActive));
                    if (ImGui.BeginPopup($"EditPopTime##{fate.TrackerId}"))
                    {
                        if (!IsEditing)
                        {
                            var timeDiff = DateTime.Now - fate.GetPoppedTime();
                            TimeAgoHours = timeDiff.Hours.ToString();
                            TimeAgoMinutes = timeDiff.Minutes.ToString();

                            IsEditing = true;
                        }

                        unsafe
                        {
                            ImGui.Text(Loc.Text("- TIME AGO -"));
                            Loc.TryEurekaName(fate.BossName, out var popupBossName);
                            ImGui.Text(Loc.Format("NM: {0}", popupBossName));

                            var width = ImGui.CalcTextSize("TIME").X;
                            ImGui.SetNextItemWidth(width);
                            ImGui.InputText($"hr##{fate.TrackerId}", ref TimeAgoHours, 1,
                                ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.CallbackCharFilter,
                                IntegerCheck);
                            ImGui.SameLine();
                            ImGui.SetNextItemWidth(width);
                            ImGui.InputText($"min##{fate.TrackerId}", ref TimeAgoMinutes, 2,
                                ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.CallbackCharFilter,
                                IntegerCheck);
                        }

                        if (string.IsNullOrWhiteSpace(TimeAgoHours))
                            TimeAgoHours = "0";
                        if (string.IsNullOrWhiteSpace(TimeAgoMinutes))
                            TimeAgoMinutes = "0";

                        var ts = new TimeSpan(int.Parse(TimeAgoHours), int.Parse(TimeAgoMinutes), 0);
                        ImGui.Text(
                            Loc.Format("{0} {1} {2} {3} ago", ts.Hours, Loc.Text(ts.Hours > 1 ? "hours" : "hour"), ts.Minutes, Loc.Text(ts.Minutes > 1 ? "minutes" : "minute")));
                        if (ImGui.Button($"{Loc.Text("Set")}##{fate.TrackerId}", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
                        {
                            var editedPopTime = DateTime.Now - ts;
                            _ = Task.Run(async () =>
                            {
                                await Connection.SetPopTime((ushort)fate.TrackerId,
                                    new DateTimeOffset(editedPopTime).ToUnixTimeMilliseconds());
                            });
                            ImGui.CloseCurrentPopup();
                        }

                        ImGui.EndPopup();
                    }

                    ImGui.PopStyleVar();
                    ImGui.PopStyleColor();
                }

                // Respawn In:
                ImGui.TableNextColumn();
                var respawnRequirementsUnformatted = fate.GetRespawnRequirements(Connection.GetTracker());

                var respawnRequirements = respawnRequirementsUnformatted.Select(requirement =>
                    (requirement.Action,
                        Time: requirement.Time.ToString(
                            requirement.Time.Hours > 0 ? "hh'h 'mm'm 'ss's'" : "mm'm 'ss's'"))).ToArray();

                if (respawnRequirements.Length == 0)
                {
                    var readyWindowRemaining = fate.GetReadyWindowRemaining(Connection.GetTracker());
                    if (readyWindowRemaining.HasValue)
                    {
                        var remainingText = readyWindowRemaining.Value.ToString(
                            readyWindowRemaining.Value.Hours > 0 ? "hh'h 'mm'm 'ss's'" : "mm'm 'ss's'");
                        Utils.RightAlignTextInColumn($"{Loc.Text("Ready")} ({remainingText})", GreenColorText);
                    }
                    else
                    {
                        Utils.RightAlignTextInColumn(Loc.Text("Ready"), GreenColorText);
                    }
                }
                else
                {
                    Vector4 colorText;
                    if (respawnRequirements[0].Action.Equals("Respawn"))
                    {
                        if (respawnRequirements[0].Action.Equals("Respawn") && respawnRequirements.Length > 1)
                        {
                            colorText = PurpleColorText;
                        }
                        else
                        {
                            colorText = RedColorText;
                        }
                    }
                    else
                    {
                        colorText = OrangeColorText;
                    }

                    Utils.RightAlignTextInColumn(respawnRequirements[0].Time, colorText);
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1f);
                        ImGui.PushStyleColor(ImGuiCol.Border, ImGui.GetColorU32(ImGuiCol.TabActive));

                        ImGui.BeginTooltip();
                        foreach (var (action, time) in respawnRequirements)
                        {
                            if (action == "Respawn")
                            {
                                ImGui.Text(Loc.Format("{0} in: {1}", Loc.Text(action), time));
                            }
                            else if (action == "Night")
                            {
                                ImGui.Text(Loc.Format("{0} in: {1}", Loc.Text(action), time));
                            }
                            else
                            {
                                ImGui.TextColored(GreenColorText, action);
                                ImGui.SameLine(0.0f, ImGui.GetStyle().ItemInnerSpacing.X);
                                ImGui.Text(Loc.Format("in: {0}", time));
                            }
                        }

                        if (colorText == PurpleColorText)
                        {
                            ImGui.TextColored(PurpleColorText, Loc.Text("Note:"));
                            ImGui.SameLine(0.0f, ImGui.GetStyle().ItemInnerSpacing.X);
                            ImGui.Text(Loc.Text("Respawn time may be wrong due to other conditions"));
                        }

                        ImGui.EndTooltip();

                        ImGui.PopStyleVar();
                        ImGui.PopStyleColor();
                    }
                }

                // Reset All
                ImGui.TableNextColumn();
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0.0f, 0.0f));
                if (fate.IsPopped())
                {
                    if (Connection.CanModify())
                    {
                        ImGui.PushStyleColor(ImGuiCol.Button, RedColor);
                        if (ImGui.Button($"{Loc.Text("RESET")}##{fate.TrackerId}", new Vector2(ImGui.GetColumnWidth(), 0.0f)))
                        {
                            _ = Task.Run(async () => { await Connection.Reset((ushort)fate.TrackerId); });
                        }

                        ImGui.PopStyleColor();
                    }
                    else
                    {
                        ImGui.BeginDisabled();
                        ImGui.Button(Loc.Text("RESET"), new Vector2(ImGui.GetColumnWidth(), 0.0f));
                        ImGui.EndDisabled();
                    }
                }
                else
                {
                    if (Connection.CanModify())
                    {
                        ImGui.PushStyleColor(ImGuiCol.Button, BlueColor);
                        if (ImGui.Button($"{Loc.Text("POP")}##{fate.TrackerId}", new Vector2(ImGui.GetColumnWidth(), 0.0f)))
                        {
                            _ = Task.Run(async () =>
                            {
                                await Connection.SetPopTime((ushort)fate.TrackerId,
                                    DateTimeOffset.Now.ToUnixTimeMilliseconds());
                            });
                        }

                        ImGui.PopStyleColor();
                    }
                    else
                    {
                        ImGui.BeginDisabled();
                        ImGui.Button(Loc.Text("POP"), new Vector2(ImGui.GetColumnWidth(), 0.0f));
                        ImGui.EndDisabled();
                    }
                }

                ImGui.PopStyleVar();
            }
        }

        public static uint DefaultIcon = 60474;
        public static void ResetDefaultIcon() => DefaultIcon = 60474;

        public void DrawElementalTab()
        {
            ImGui.Columns(2);

            var save = false;

            save |= ImGui.Checkbox(Loc.Text("Display Elemental"), ref EurekaHelper.Config.DisplayElemental);
            Utils.SetTooltip(Loc.Text("Displays in chat whenever an Elemental appears near the player"));
            ImGui.NextColumn();

            save |= ImGui.Checkbox(Loc.Text("Display Elemental Toast"), ref EurekaHelper.Config.DisplayElementalToast);
            Utils.SetTooltip(Loc.Text("Displays a toast whenever an Elemental appears near the player"));
            ImGui.NextColumn();

            save |= ImGui.Checkbox(Loc.Text("Crowdsource Locations"), ref EurekaHelper.Config.ElementalCrowdsource);
            Utils.SetTooltip(Loc.Text("Assist to crowdsource for Elemental locations"));
            ImGui.NextColumn();

            ImGui.SetNextItemWidth(140f);
            var enumNames = Loc.EnumNames<PayloadOptions>();
            var enumValues = Enum.GetValues<PayloadOptions>();
            var enumCurrent = Array.IndexOf(enumValues, EurekaHelper.Config.ElementalPayloadOptions);
            if (ImGui.Combo(Loc.Text("Payload Options"), ref enumCurrent, enumNames, enumNames.Length))
            {
                EurekaHelper.Config.ElementalPayloadOptions = enumValues[enumCurrent];
                save = true;
            }

            Utils.SetTooltip(
                Loc.Text("Sets what the clickable payload does.\nThis also affects the Shout/Copy column in the table.\n" +
                "For example: Setting it to \'ShoutToChat\' will send the Elemental to current chat when you click the button."));
            ImGui.NextColumn();

            save |= ImGui.Checkbox(Loc.Text("Auto Mark Elementals"), ref EurekaHelper.Config.ElementalAutoMark);
            Utils.SetTooltip(Loc.Text("Auto mark Elementals (only new Elementals) on map as you find them.\n" +
                             "Due to some limitations, the map will always open when you find an Elemental with this configuration enabled."));
            ImGui.NextColumn();

            save |= ImGui.Checkbox(Loc.Text("Always Clear Elementals"), ref EurekaHelper.Config.ElementalAlwaysClear);
            Utils.SetTooltip(Loc.Text("Always clear the Elemental list whenever you join a Eureka zone"));
            ImGui.NextColumn();

            ImGui.Columns(1);

            if (save)
                EurekaHelper.Config.Save();

            ImGui.Separator();

            if (ImGui.Button(Loc.Text("Add Known Elemental Map Markers")))
            {
                var territoryType = DalamudApi.ClientState.TerritoryType;
                if (Utils.IsPlayerInEurekaZone(territoryType))
                {
                    var knownLocations = ElementalManager.GetKnownLocations(territoryType);
                    foreach (var location in knownLocations)
                    {
                        // 61502 - X
                        // 63922 - MOOGLE
                        Utils.AddMapMarker(territoryType, location, 63922, true);
                    }
                }
                else
                {
                    EurekaHelper.PrintMessage(Loc.Text("You must be in one of the Eureka zone to use this."));
                }
            }

            Utils.SetTooltip(Loc.Text("Adds a marker to known Elemental positions on the current map and minimap.\n" +
                             "Help contribute to the known locations by providing the developer the necessary information"));
            ImGui.SameLine();

            if (ImGui.Button(Loc.Text("Clear All Elementals")))
            {
                Plugin.ElementalManager.Elementals.Clear();
                ResetDefaultIcon();
            }

            ImGui.SameLine();

            if (ImGui.Button(Loc.Text("Clear All Map Markers")))
            {
                Utils.ClearMapMarker();
                ResetDefaultIcon();
            }

            ImGui.PushStyleColor(ImGuiCol.Border, ImGui.GetColorU32(ImGuiCol.TabActive));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0.0f, 0.0f));
            ImGui.BeginChild("ElementalsChild",
                new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y), true);
            ImGui.PopStyleColor();
            ImGui.PopStyleVar();

            if (ImGui.BeginTable("ElementalsTable", 6,
                    ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.BordersV |
                    ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.ScrollY | ImGuiTableFlags.NoSavedSettings))
            {
                ImGui.TableSetupColumn(Loc.Text("Elemental"));
                ImGui.TableSetupColumn(Loc.Text("Location"));
                ImGui.TableSetupColumn(Loc.Text("Last Seen"));
                ImGui.TableSetupColumn(Loc.Text("S/C"), ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn(Loc.Text("Mark"), ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn(Loc.Text("Delete"), ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableHeadersRow();

                for (int i = Plugin.ElementalManager.Elementals.Count - 1; i >= 0; i--)
                {
                    var elemental = Plugin.ElementalManager.Elementals[i];

                    ImGui.TableNextColumn();
                    ImGui.Text(elemental.Name);

                    ImGui.TableNextColumn();
                    ImGui.Text($"X: {elemental.Position.X:0.0}, Y: {elemental.Position.Y:0.0}");
                    if (ImGui.IsItemClicked())
                        Utils.SetFlagMarker(elemental.TerritoryId, elemental.MapId, elemental.Position, openMap: true);

                    ImGui.TableNextColumn();
                    var dateTime = EorzeaTime.Zero.AddSeconds(elemental.LastSeen).ToLocalTime();
                    ImGui.Text(dateTime.ToString());

                    ImGui.TableNextColumn();
                    if (ImGui.Button(
                            $"{(EurekaHelper.Config.ElementalPayloadOptions == PayloadOptions.CopyToClipboard ? $"C##{elemental.ObjectId}" : $"S##{elemental.ObjectId}")}"))
                    {
                        Utils.SetFlagMarker(elemental.TerritoryId, elemental.MapId, elemental.Position);
                        switch (EurekaHelper.Config.ElementalPayloadOptions)
                        {
                            case PayloadOptions.CopyToClipboard:
                                Utils.CopyToClipboard($"{elemental.Name} <flag>");
                                break;

                            default:
                            case PayloadOptions.ShoutToChat:
                                Utils.SendMessage($"/sh {elemental.Name} <flag>");
                                break;
                        }
                    }

                    ImGui.TableNextColumn();
                    if (ImGuiComponents.IconButton($"Elemental{elemental.ObjectId}", FontAwesomeIcon.MapMarked))
                    {
                        Utils.AddMapMarker(elemental.TerritoryId, elemental.RawPosition, DefaultIcon, true);
                        DefaultIcon++;

                        if (DefaultIcon > 60476)
                            ResetDefaultIcon();
                    }

                    ImGui.TableNextColumn();
                    if (ImGuiComponents.IconButton($"Elemental{elemental.ObjectId}", FontAwesomeIcon.Trash))
                        Plugin.ElementalManager.Elementals.RemoveAt(i);
                }

                ImGui.EndTable();
            }

            ImGui.EndChild();
        }

        static string CustomMessages = string.Join("\n", EurekaHelper.Config.CustomMessages);

        public static void DrawSettingsTab()
        {
            ImGui.Columns(2, null, true);

            var save = false;
            var useChatSoundEffect = EurekaHelper.Config.GlobalUseChatSoundEffect;

            save |= ImGui.Checkbox(Loc.Text("Display NM Pop"), ref EurekaHelper.Config.DisplayFatePop);
            Utils.SetTooltip(Loc.Text("Displays the NM that popped in chat"));
            ImGui.NextColumn();

            save |= ImGui.Checkbox(Loc.Text("Enable NM pop sound"), ref EurekaHelper.Config.PlayPopSound);
            Utils.SetTooltip(Loc.Text("A sound que will be played whenever an NM pops."));
            ImGui.NextColumn();

            save |= ImGui.Checkbox(Loc.Text("Display fate progress"), ref EurekaHelper.Config.DisplayFateProgress);
            Utils.SetTooltip(Loc.Text("Prints the NM progress in chat"));
            ImGui.NextColumn();

            save |= ImGui.Checkbox(Loc.Text("Enable bunny fates"), ref EurekaHelper.Config.DisplayBunnyFates);
            Utils.SetTooltip(Loc.Text("Enable display for bunny fates"));
            ImGui.NextColumn();

            save |= ImGui.Checkbox(Loc.Text("Display Toast"), ref EurekaHelper.Config.DisplayToastPop);
            Utils.SetTooltip(Loc.Text("Displays a toast whenever an NM pops"));
            ImGui.NextColumn();

            save |= ImGui.Checkbox(Loc.Text("Auto pop fate"), ref EurekaHelper.Config.AutoPopFate);
            Utils.SetTooltip(Loc.Text("Attempts to auto pop fate when connected to a tracker (if you have the password)"));
            ImGui.NextColumn();

            ImGui.SetNextItemWidth(140f);
            if (useChatSoundEffect)
            {
                var soundEffect = EurekaHelper.Config.NMChatSoundEffect;
                if (Utils.EnumSelector(Loc.Text("NM Sound Effect"), Loc.Text("Chat Sound Effect to be played when an NM pops."),
                        ref soundEffect))
                {
                    save = true;
                    EurekaHelper.Config.NMChatSoundEffect = soundEffect;
                    SoundManager.PlayNMSoundEffect();
                }
            }
            else
            {
                var nmSoundEffect = EurekaHelper.Config.NMSoundEffect;
                if (Utils.EnumSelector(Loc.Text("NM Sound Effect"), Loc.Text("Sound Effect to be played when an NM pops."),
                        ref nmSoundEffect))
                {
                    save = true;
                    EurekaHelper.Config.NMSoundEffect = nmSoundEffect;
                    SoundManager.PlayNMSoundEffect();
                }
            }

            ImGui.NextColumn();

            ImGui.SetNextItemWidth(140f);
            if (useChatSoundEffect)
            {
                var soundEffect = EurekaHelper.Config.BunnyChatSoundEffect;
                if (Utils.EnumSelector(Loc.Text("Bunny Sound Effect"), Loc.Text("Sound Effect to be played when bunny spawns."),
                        ref soundEffect))
                {
                    save = true;
                    EurekaHelper.Config.BunnyChatSoundEffect = soundEffect;
                    SoundManager.PlayBunnySoundEffect();
                }
            }
            else
            {
                var soundEffect = EurekaHelper.Config.BunnySoundEffect;
                if (Utils.EnumSelector(Loc.Text("Bunny Sound Effect"), Loc.Text("Sound Effect to be played when bunny spawns."),
                        ref soundEffect))
                {
                    save = true;
                    EurekaHelper.Config.BunnySoundEffect = soundEffect;
                    SoundManager.PlayBunnySoundEffect();
                }
            }

            ImGui.NextColumn();

            ImGui.SetNextItemWidth(140f);
            var payloadOption = EurekaHelper.Config.PayloadOptions;
            if (Utils.EnumSelector(Loc.Text("Payload Options"), Loc.Text("Sets what the clickable payload does.\n" +
                                                      "For example: Setting it to \'ShoutToChat\' will shout the pop when you click the button in chat."),
                    ref payloadOption))
            {
                save = true;
                EurekaHelper.Config.PayloadOptions = payloadOption;
            }

            ImGui.NextColumn();

            ImGui.SetNextItemWidth(140f);
            var xivChatType = EurekaHelper.Config.ChatChannel;
            if (Utils.EnumSelector(Loc.Text("Chat Channels"),
                    Loc.Text("Set the channel which the plugin messages will display. Default: Echo"),
                    ref xivChatType))
            {
                save = true;
                EurekaHelper.Config.ChatChannel = xivChatType;
            }

            ImGui.NextColumn();

            save |= ImGui.Checkbox(Loc.Text("Randomize Map Coords"), ref EurekaHelper.Config.RandomizeMapCoords);
            Utils.SetTooltip(Loc.Text("Randomizes map coords to range of +- 0.5 (recommended to enable)"));
            ImGui.NextColumn();

            save |= ImGui.Checkbox(Loc.Text("Auto Create Tracker"), ref EurekaHelper.Config.AutoCreateTracker);
            Utils.SetTooltip(Loc.Text("Auto creates tracker when joining an instance and prints the tracker link to chat"));
            ImGui.NextColumn();

            save |= ImGui.Checkbox(Loc.Text("Auto Pop fate within range"), ref EurekaHelper.Config.AutoPopFateWithinRange);
            Utils.SetTooltip(Loc.Text("Requires \"Auto pop fate\" to be enabled.\n\n" +
                             "NM fates has an estimated respawn time of 2 hours\n" +
                             "This option will pop fates if it has a cooldown of less than 5 minutes instead of waiting for the normal 2 hour duration"));
            ImGui.NextColumn();

            save |= ImGui.Checkbox(Loc.Text("Show Level On Tracker"), ref EurekaHelper.Config.ShowLevelInTrackerTable);
            Utils.SetTooltip(Loc.Text("Will show the level of a given NM in the tracker table."));
            ImGui.NextColumn();

            save |= ImGui.Checkbox(Loc.Text("Use Chat Sound Effects"), ref EurekaHelper.Config.GlobalUseChatSoundEffect);
            Utils.SetTooltip(
                Loc.Text("This option can be enabled to use the chat sound effects instead of the other sound effects.\nThis option is active for ALL sound effects and will not overwrite your previous selections, however you will need to reset the sound effects."));
            ImGui.NextColumn();

            if (ImGui.Button(EurekaHelper.Plugin.RelicWindow.IsOpen ? Loc.Text("Close Relic Window") : Loc.Text("Open Relic Window")))
                EurekaHelper.Plugin.RelicWindow.IsOpen ^= true;
            ImGui.NextColumn();

            save |= ImGui.Checkbox(Loc.Text("Auto Open Relic Window In Eureka"), ref EurekaHelper.Config.AutoOpenRelicWindowInEureka);
            Utils.SetTooltip(Loc.Text("Automatically opens the Relic window when you enter a Eureka zone, and closes it when you leave."));
            ImGui.NextColumn();

            var enableSplatoon = EurekaHelper.Config.EnableSplatoonAggroRanges;
            if (ImGui.Checkbox(Loc.Text("Show NM Aggro Ranges (Splatoon)"), ref enableSplatoon))
            {
                EurekaHelper.Config.EnableSplatoonAggroRanges = enableSplatoon;
                save = true;

                if (enableSplatoon)
                    EurekaHelper.Plugin.SplatoonManager = new();
                else
                    EurekaHelper.Plugin.SplatoonManager?.Dispose();
            }
            Utils.SetTooltip(Loc.Text("Requires the Splatoon plugin. Draws each NM's aural/visual/magic/blood aggro range as a circle/cone.\nEXPERIMENTAL: aggro range data is unverified/incomplete - see AggroRanges.json in the plugin config folder."));
            ImGui.NextColumn();

            ImGui.Columns(1);

            if (EurekaHelper.Config.EnableSplatoonAggroRanges && EurekaHelper.Plugin.SplatoonManager != null)
            {
                ImGui.Text(Loc.Text("Aggro range data file:"));
                ImGui.SameLine();
                ImGui.TextWrapped(EurekaHelper.Plugin.SplatoonManager.GetConfigPath());
                if (ImGui.Button(Loc.Text("Reload aggro range data")))
                    EurekaHelper.Plugin.SplatoonManager.ReloadConfig();
                Utils.SetTooltip(Loc.Text("Re-reads AggroRanges.json after you've edited it, without needing to restart the plugin."));
            }

            if (ImGui.CollapsingHeader(Loc.Text("Custom Messages")))
            {
                ImGui.TextWrapped(Loc.Text("** HOW TO USE **" +
                                  "\nType the messages you want in each line, to enter the next line press \"Enter\"\n"));
                ImGui.TextWrapped(Loc.Text("** AVAILABLE FORMATTINGS **"));
                ImGui.BulletText(Loc.Text("%%bossName%% - Replaced with fate boss name"));
                ImGui.BulletText(Loc.Text("%%bossShortName%% - Replaced with fate boss short name"));
                ImGui.BulletText(Loc.Text("%%fateName%% - Replaced with fate name"));
                ImGui.BulletText(Loc.Text("%%flag%% - Replaced with <flag>"));
                ImGui.Spacing();
                ImGui.InputTextMultiline("###CustomShoutMessages", ref CustomMessages, 9999, new Vector2(-1, -1));
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    if (!string.IsNullOrWhiteSpace(CustomMessages))
                    {
                        EurekaHelper.Config.CustomMessages = CustomMessages.Split("\n")
                            .Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                        save = true;
                    }
                    else
                    {
                        CustomMessages = "/shout %bossName% POP. %flag%";
                        EurekaHelper.Config.CustomMessages = new() { CustomMessages };
                        save = true;
                    }
                }
            }

            if (save)
                EurekaHelper.Config.Save();
        }

        public static void DrawInstanceTab()
        {
            var save = false;

            ImGui.Columns(2);

            save |= ImGui.Checkbox(Loc.Text("Display Server Id in chat"), ref EurekaHelper.Config.DisplayServerId);
            ImGui.NextColumn();

            save |= ImGui.Checkbox(Loc.Text("Display Server Id in \"server info\" bar"),
                ref EurekaHelper.Config.DisplayServerIdInServerInfo);

            ImGui.Columns(1);

            ImGui.Separator();

            ImGui.TextColored(RedColorText, Loc.Text("** DISCLAIMER, READ THIS **"));
            ImGui.TextWrapped(
                Loc.Text("This option will display the current server ID of the instance in chat each time you instance into a Eureka zone. " +
                "This might help you identify unique instances. However, there are a few things you should note." +
                "\n\nFirst of all, this method is definitely not the best way to uniquely identify Eureka zones." +
                "\n\nSecondly, according to sources and self-testing, the server ID may get reused for the new instance after the old instance gets locked." +
                "\n\nExamples:\nIf you enter a Pagos zone with server ID (60) and you rejoin to another Pagos zone with server ID (61), it would have meant that you've just joined another instance." +
                "\nIf a zone in Pyros with server ID (59) gets locked, on very rare occasions, the new Pyros instance might get the same server ID (59) as well." +
                "\n\nThirdly, from what I know and have read (but have been unable to test), these server IDs are unique to people in the same world as you. " +
                "This means that another person in another world will get a different server ID than what you have." +
                "\n\nAfter reading all this information, I hope that you will use it only for your own good. And I will not be entertaining any feedback mentioning that the server ID is \"incorrect\"."));

            if (save)
                EurekaHelper.Config.Save();
        }

        public static void DrawAboutTab()
        {
            ImGui.TextColored(new Vector4(0.0f, 1.0f, 0.0f, 1.0f), Loc.Text("About:"));
            ImGui.Indent();
            ImGui.TextWrapped(Loc.Text("Hi there!" +
                              "\nThis is my first FFXIV plugin, alot of the ideas are shamelessly taken from other plugins." +
                              "\n\nWelcome to Eureka Helper, a tool to help you on your Eureka Adventures. It offers a small variety of QoL changes and a built-in Eureka Tracker." +
                              "\nFor those interested in money making NMs (e.g Cassie, Skoll), you can type /arisu (command name from ABBA discord) for their next weather time window!"));
            ImGui.Unindent();
            ImGui.Dummy(new Vector2(0.0f, 10.0f));

            ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), Loc.Text("Information:"));
            ImGui.Indent();
            var userUrl = "https://github.com/KangasZ";
            ImGui.Text(Loc.Text("GitHub:"));
            ImGui.SameLine();
            Utils.TextURL("GitHub", $"{userUrl}/EurekaHelper", ImGui.GetColorU32(ImGuiCol.Text));
            //ImGui.Text("Last commit:"); ImGui.SameLine(); ImGui.Text(Utils.GetGitSha());
            ImGui.Text(Loc.Text("Version:"));
            ImGui.SameLine();
            ImGui.Text(Utils.GetVersion());
            ImGui.Unindent();
            ImGui.Dummy(new Vector2(0.0f, 10.0f));

            ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.06f, 1.0f), Loc.Text("Contact:"));
            ImGui.Indent();
            //ImGui.Text("Discord:"); ImGui.SameLine(); ImGui.Text("@snorux");
            ImGui.Text(Loc.Text("Issues / Feedbacks:"));
            ImGui.SameLine();
            Utils.TextURL("GitHub", $"{userUrl}/EurekaHelper/issues", ImGui.GetColorU32(ImGuiCol.Text));
            ImGui.Unindent();
            ImGui.Dummy(new Vector2(0.0f, 10.0f));

            ImGui.TextColored(ImGuiColors.ParsedPurple, Loc.Text("Commands"));
            ImGui.Indent();
            ImGui.Text(Loc.Text("/eurekahelper | /eh | /ehelper -> Opens / Closes the configuration window"));
            ImGui.Text(Loc.Text("/etrackers -> Attempts to get a tracker for the current instance in the same datacenter."));
            ImGui.Text(Loc.Text("/erelic -> Opens / Closes the Eureka Relic helper window"));
            ImGui.Text(Loc.Text("/ealarms -> Opens / Closes the Eureka Alarms window"));
            ImGui.Text(Loc.Text("/arisu -> Display next weather for Crab, Cassie & Skoll"));
            ImGui.Unindent();
            ImGui.Dummy(new Vector2(0.0f, 10.0f));

            ImGui.TextColored(new Vector4(1.0f, 0.0f, 0.5f, 1.0f), Loc.Text("Credits:"));
            ImGui.Indent();
            ImGui.Text(Loc.Text("FFXIV Dev community"));
            ImGui.Text(Loc.Text("electr0sheep for EurekaTrackerAutoPopper"));
            ImGui.Text(Loc.Text("Bedo9041 for EurekaPlugin"));
            ImGui.Text(Loc.Text("KangasZ for EurekaHelper contributions"));
        }

        private void DrawDebugTab()
        {
            ImGui.TextWrapped(Loc.Text("Lock a target in-game, tune the shape/radius/color below, and it'll draw live via Splatoon (if connected) so you can compare against the actual aggro range. Hit \"Add to AggroRanges.json\" once it matches."));
            ImGui.Separator();

            var splatoonManager = EurekaHelper.Plugin.SplatoonManager;
            if (splatoonManager == null)
            {
                Utils.CenterText(Loc.Text("Splatoon isn't connected."));
                return;
            }

            if (ImGui.CollapsingHeader(Loc.Text("Seen Monsters")))
            {
                ImGui.TextWrapped(Loc.Text("Every mob name encountered in Eureka this session. Names matching a known pattern (e.g. \"Sprite\" -> Magic, undead names -> Blood) get auto-registered with radius 0 - lock them and measure when you get the chance. Everything else defaults to Visual and isn't tracked here individually."));

                var seenMonsters = splatoonManager.GetSeenMonsters().OrderBy(x => x).ToList();
                ImGui.Text(Loc.Format("{0} unique names seen.", seenMonsters.Count));

                var (totalObjects, battleNpcs, enemyKind, aliveEnemies) = splatoonManager.GetLastScanCounts();
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f),
                    Loc.Format("Diagnostic (last scan): {0} objects -> {1} BattleNpc -> {2} Enemy-kind (not pet/summon) -> {3} alive.", totalObjects, battleNpcs, enemyKind, aliveEnemies));

                if (ImGui.BeginTable("SeenMonstersTable", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY, new Vector2(0, 200)))
                {
                    ImGui.TableSetupColumn(Loc.Text("Name"));
                    ImGui.TableSetupColumn(Loc.Text("Registered As"), ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableHeadersRow();

                    foreach (var name in seenMonsters)
                    {
                        ImGui.TableNextColumn();
                        ImGui.Text(name);

                        ImGui.TableNextColumn();
                        var seenEntries = splatoonManager.GetEntriesFor(name);
                        ImGui.Text(seenEntries.Count > 0 ? string.Join(", ", seenEntries.Select(e => Loc.Enum(e.Type))) : Loc.Text("Visual (default)"));
                    }

                    ImGui.EndTable();
                }
            }

            ImGui.Separator();

            var target = DalamudApi.TargetManager.Target;
            if (target == null)
            {
                Utils.CenterText(Loc.Text("No target locked."));
                return;
            }

            ImGui.Text(Loc.Text("Name:"));
            ImGui.SameLine();
            ImGui.Text(target.Name.TextValue);

            ImGui.Text(Loc.Text("Data ID:"));
            ImGui.SameLine();
            ImGui.Text(target.DataId.ToString());

            ImGui.Text(Loc.Text("Kind:"));
            ImGui.SameLine();
            ImGui.Text(target.ObjectKind.ToString());

            var distance = DalamudApi.ClientState.LocalPlayer != null
                ? Vector3.Distance(DalamudApi.ClientState.LocalPlayer.Position, target.Position).ToString("0.0")
                : "?";
            ImGui.Text(Loc.Text("Distance:"));
            ImGui.SameLine();
            ImGui.Text(distance);

            ImGui.Separator();

            ImGui.SetNextItemWidth(150f);
            var aggroTypeNames = Loc.EnumNames<AggroType>();
            var aggroTypeIndex = (int)DebugAggroType;
            if (ImGui.Combo("##DebugAggroType", ref aggroTypeIndex, aggroTypeNames, aggroTypeNames.Length))
            {
                DebugAggroType = (AggroType)aggroTypeIndex;
                var (shape, color, radius, coneHalfAngle) = AggroTypeDefaults.Get(DebugAggroType);
                DebugAggroShape = shape;
                DebugColor = ImGui.ColorConvertU32ToFloat4(color);
                DebugRadius = radius;
                DebugConeHalfAngle = coneHalfAngle;
            }
            Utils.SetTooltip(Loc.Text("Aggro type (just a label + starting shape/color - freely editable below)"));

            ImGui.SameLine();
            ImGui.SetNextItemWidth(100f);
            var shapeNames = Loc.EnumNames<AggroShape>();
            var shapeIndex = (int)DebugAggroShape;
            if (ImGui.Combo("##DebugShape", ref shapeIndex, shapeNames, shapeNames.Length))
                DebugAggroShape = (AggroShape)shapeIndex;

            ImGui.SetNextItemWidth(150f);
            ImGui.DragFloat(Loc.Text("Radius"), ref DebugRadius, 0.5f, 0f, 60f);

            if (DebugAggroShape == AggroShape.Cone)
            {
                ImGui.SetNextItemWidth(150f);
                ImGui.DragInt(Loc.Text("Cone Half-Angle"), ref DebugConeHalfAngle, 1f, 1, 180);
                ImGui.TextColored(new Vector4(0.9f, 0.6f, 0.2f, 1f),
                    Loc.Text("Note: Splatoon always draws cones filled (a quirk on its end, not something we can toggle off), so keep the color's alpha low or it'll be an eyesore."));
            }

            ImGui.SetNextItemWidth(150f);
            ImGui.DragFloat(Loc.Text("Outline Thickness"), ref DebugThickness, 0.1f, 0.5f, 10f);

            ImGui.ColorEdit4(Loc.Text("Color"), ref DebugColor);

            ImGui.SetNextItemWidth(200f);
            ImGui.InputTextWithHint("##DebugBossNameOverride", Loc.Text("Override name (optional)"), ref DebugBossNameOverride, 64);
            Utils.SetTooltip(Loc.Text("Leave empty to key the entry by the locked target's exact name."));

            var bossName = string.IsNullOrWhiteSpace(DebugBossNameOverride) ? target.Name.TextValue : DebugBossNameOverride;

            if (ImGui.Button(Loc.Text("Add to AggroRanges.json")))
            {
                splatoonManager.AddEntry(bossName, new AggroRangeConfig
                {
                    Type = DebugAggroType,
                    Shape = DebugAggroShape,
                    Radius = DebugRadius,
                    ConeHalfAngleDegrees = DebugConeHalfAngle,
                    Color = ImGui.ColorConvertFloat4ToU32(DebugColor),
                    Thickness = DebugThickness,
                });
            }

            ImGui.Separator();
            ImGui.Text(Loc.Format("Existing entries for \"{0}\":", bossName));

            var existing = splatoonManager.GetEntriesFor(bossName);
            for (var i = 0; i < existing.Count; i++)
            {
                var entry = existing[i];
                ImGui.Text($"{Loc.Enum(entry.Type)} - {Loc.Enum(entry.Shape)} - {entry.Radius}y");
                ImGui.SameLine();
                if (ImGui.SmallButton($"{Loc.Text("Delete")}##DebugEntry{i}"))
                    splatoonManager.RemoveEntry(bossName, i);
            }
        }

        // Resolves to whichever zone's connection matches the player's current territory (falling
        // back to slot 1 if not currently in a Eureka zone, e.g. during plugin shutdown) - used by
        // callers like FateManager that care about "the tracker for wherever I am right now"
        // rather than a specific zone tab.
        public static EurekaConnectionManager GetConnection()
        {
            var zoneIndex = Utils.GetIndexOfZone(DalamudApi.ClientState.TerritoryType);
            if (zoneIndex is < 1 or > 4)
                zoneIndex = 1;

            return Connections[zoneIndex] ??= new EurekaConnectionManager();
        }

        public static void DisposeAllConnections()
        {
            foreach (var connection in Connections)
                connection?.Dispose();
        }

        private unsafe int IntegerCheck(ImGuiInputTextCallbackData* data)
        {
            char c = Convert.ToChar(data->EventChar);

            if (c >= '0' && c <= '9')
                return 0;

            return 1;
        }
    }
}