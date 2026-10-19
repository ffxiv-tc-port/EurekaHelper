
using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using EurekaHelper.System;
using EurekaHelper.XIV;
using EurekaHelper.XIV.Zones;

namespace EurekaHelper.Windows
{
    internal class AlarmWindow : Window, IDisposable
    {
        private readonly EurekaHelper Plugin = null!;

        public AlarmWindow(EurekaHelper plugin) : base(Loc.Text("Eureka Helper - Alarms"))
        {
            Plugin = plugin;
            SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(360, 350), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };
        }

        public void Dispose() { }

        private readonly float LabelSize = 100f;

        // Default values
        private bool IsInMenu = false;
        private string AlarmName = string.Empty;
        private AlarmType AlarmType = AlarmType.Weather;
        private TimeType TimeType = TimeType.Day;
        private EurekaWeather WeatherType = EurekaWeather.FairSkies;
        private BaseSoundEffect SoundEffect = BaseSoundEffect.SoundEffect45;
        private ChatSoundEffect ChatSoundEffect = ChatSoundEffect.ChatSoundEffect10;
        private ushort AlarmZone = 732;
        private int MinutesBefore = 5;

        public override void Draw()
        {
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus))
            {
                IsInMenu = false;
                ImGui.OpenPopup("Add Alarm");
            }
            Utils.SetTooltip(Loc.Text("Add an alarm"));

            ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1f);
            ImGui.PushStyleColor(ImGuiCol.Border, ImGui.GetColorU32(ImGuiCol.TabActive));
            if (ImGui.BeginPopup("Add Alarm"))
            {
                if (!IsInMenu)
                {
                    // reset to default values
                    AlarmName = string.Empty;
                    AlarmType = AlarmType.Weather;
                    TimeType = TimeType.Day;
                    WeatherType = EurekaWeather.FairSkies;
                    SoundEffect = BaseSoundEffect.SoundEffect45;
                    ChatSoundEffect = ChatSoundEffect.ChatSoundEffect10;
                    AlarmZone = 732;
                    MinutesBefore = 5;

                    IsInMenu = true;
                }

                ImGui.Text(Loc.Text("Add Alarm"));
                ImGui.SetNextItemWidth(LabelSize);
                ImGui.LabelText("##NameLabel", Loc.Text("Name:")); 
                ImGui.SameLine(); 
                ImGui.SetNextItemWidth(150f);
                ImGui.InputTextWithHint("##Name", Loc.Text("Name of alarm"), ref AlarmName, 15);

                ImGui.SetNextItemWidth(LabelSize);
                ImGui.LabelText("##TypeLabel", Loc.Text("Type:")); 
                ImGui.SameLine(); 
                ImGui.SetNextItemWidth(150f);
                var currentAlarmType = Array.IndexOf(Enum.GetValues<AlarmType>(), AlarmType);
                if (ImGui.Combo("##AlarmTypeCombo", ref currentAlarmType, Loc.EnumNames<AlarmType>(), Enum.GetNames<AlarmType>().Length))
                    AlarmType = Enum.GetValues<AlarmType>()[currentAlarmType];

                if (AlarmType == AlarmType.Weather)
                {
                    ImGui.SetNextItemWidth(LabelSize);
                    ImGui.LabelText("##ZoneLabel", Loc.Text("Zone:")); 
                    ImGui.SameLine(); 
                    ImGui.SetNextItemWidth(150f);
                    var allZones = Constants.EurekaZones.Select(Utils.GetZoneName).ToArray();
                    var currentZone = Array.IndexOf(allZones, Utils.GetZoneName(AlarmZone));
                    if (ImGui.Combo("##AlarmZoneCombo", ref currentZone, allZones, 4))
                        AlarmZone = Constants.EurekaZones[currentZone];

                    ImGui.SetNextItemWidth(LabelSize);
                    ImGui.LabelText("##WeatherLabel", Loc.Text("Weather:")); 
                    ImGui.SameLine(); 
                    ImGui.SetNextItemWidth(150f);

                    // each zone has a different set of weathers:
                    var selectedZoneWeathers = AlarmZone switch
                    {
                        732 => EurekaAnemos.GetZoneWeathers().ToArray(),
                        763 => EurekaPagos.GetZoneWeathers().ToArray(),
                        795 => EurekaPyros.GetZoneWeathers().ToArray(),
                        827 => EurekaHydatos.GetZoneWeathers().ToArray(),
                        _ => throw new NotImplementedException(),
                    };

                    var currentWeather = Array.IndexOf(selectedZoneWeathers, WeatherType);
                    if (currentWeather == -1)
                    {
                        WeatherType = EurekaWeather.FairSkies;
                        currentWeather = Array.IndexOf(selectedZoneWeathers, EurekaWeather.FairSkies);
                    }

                    if (ImGui.Combo("##WeatherCombo", ref currentWeather, selectedZoneWeathers.Select(x => x.ToFriendlyString()).ToArray(), selectedZoneWeathers.Length))
                        WeatherType = selectedZoneWeathers[currentWeather];
                }
                else
                {
                    ImGui.SetNextItemWidth(LabelSize);
                    ImGui.LabelText("##TimeLabel", Loc.Text("Time:")); 
                    ImGui.SameLine(); 
                    ImGui.SetNextItemWidth(150f);
                    var currentTimeType = Array.IndexOf(Enum.GetValues<TimeType>(), TimeType);
                    if (ImGui.Combo("##TimeTypeCombo", ref currentTimeType, Loc.EnumNames<TimeType>(), Enum.GetNames<TimeType>().Length))
                        TimeType = Enum.GetValues<TimeType>()[currentTimeType];
                }

                ImGui.SetNextItemWidth(LabelSize);
                ImGui.LabelText("##SoundLabel", Loc.Text("Sound Effect:")); 
                ImGui.SameLine(); 
                ImGui.SetNextItemWidth(150f);
                var useChatSoundEffect = EurekaHelper.Config.GlobalUseChatSoundEffect;
                if (useChatSoundEffect)
                {
                    if (Utils.EnumSelector("##SoundChat", null, ref ChatSoundEffect))
                    {
                        SoundManager.PlaySoundEffect(ChatSoundEffect);
                    }
                }
                else
                {
                    if (Utils.EnumSelector("##Sound", null, ref SoundEffect))
                    {
                        SoundManager.PlaySoundEffect(SoundEffect);
                    }
                }

                ImGui.SetNextItemWidth(LabelSize);
                ImGui.LabelText("##TimeLabel", Loc.Text("Minutes Before:"));
                ImGui.SameLine();
                ImGui.SetNextItemWidth(150f);
                ImGui.SliderInt("##TimeSlider", ref MinutesBefore, 1, 20, "%d", ImGuiSliderFlags.NoInput);

                if (ImGui.Button(Loc.Text("Add"), new Vector2(ImGui.GetContentRegionAvail().X, 0.0f))) 
                {
                    Plugin.AlarmManager.AddAlarm(CreateAlarm());
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            ImGui.PopStyleVar();
            ImGui.PopStyleColor();

            ImGui.SameLine();
            if (ImGui.Button(Loc.Text("Delete All")))
                Plugin.AlarmManager.DeleteAlarm(null, true); 
            
            ImGuiComponents.HelpMarker(Loc.Text("Wow, you can now edit alarms."));

            ImGui.Separator();
            ImGui.Text(Loc.Text("Your Alarms"));

            ImGui.PushStyleColor(ImGuiCol.Border, ImGui.GetColorU32(ImGuiCol.TabActive));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0.0f, 0.0f));
            ImGui.BeginChild("AlarmDisplay", ImGui.GetContentRegionAvail(), true);
            ImGui.PopStyleColor();
            ImGui.PopStyleVar();

            DrawAlarmTable();

            ImGui.EndChild();
        }

        private void DrawAlarmTable()
        {
            if (ImGui.BeginTable("AlarmTable", 3, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.BordersV | ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.ScrollY | ImGuiTableFlags.NoSavedSettings))
            {
                ImGui.TableSetupColumn(Loc.Text("Alarm Name"));
                ImGui.TableSetupColumn(Loc.Text("Timeleft"));
                ImGui.TableSetupColumn(Loc.Text("Alarm Configurations / Edit"), ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableHeadersRow();

                for (int i = EurekaHelper.Config.Alarms.Count - 1; i >= 0; i--)
                {
                    var alarm = EurekaHelper.Config.Alarms[i];

                    ImGui.TableNextColumn();
                    ImGui.Text(alarm.Name);
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1f);
                        ImGui.PushStyleColor(ImGuiCol.Border, ImGui.GetColorU32(ImGuiCol.TabActive));
                        ImGui.BeginTooltip();

                        ImGui.TextColored(ImGuiColors.DalamudOrange, Loc.Text("Alarm Information"));
                        ImGui.Text(Loc.Text("Name:"));
                        ImGui.SameLine();
                        ImGui.Text(alarm.Name);

                        ImGui.Text(Loc.Text("Type:"));
                        ImGui.SameLine();
                        ImGui.Text(Loc.Enum(alarm.Type));

                        if (alarm.Type == AlarmType.Weather)
                        {
                            ImGui.Text(Loc.Text("Zone:"));
                            ImGui.SameLine();
                            ImGui.Text(Utils.GetZoneName(alarm.ZoneId));

                            ImGui.Text(Loc.Text("Weather:"));
                            ImGui.SameLine();
                            ImGui.Text(alarm.Weather.ToFriendlyString());
                        }
                        else
                        {
                            ImGui.Text(Loc.Text("Time:"));
                            ImGui.SameLine();
                            ImGui.Text(Loc.Enum(alarm.TimeType));
                        }

                        ImGui.Text(Loc.Text("Sound Effect:"));
                        ImGui.SameLine();
                        ImGui.Text(alarm.ChatSoundEffect.ToString());

                        ImGui.Text(Loc.Text("Minutes Before:"));
                        ImGui.SameLine();
                        ImGui.Text(alarm.MinutesOffset.ToString());

                        ImGui.EndTooltip();
                        ImGui.PopStyleVar();
                        ImGui.PopStyleColor();
                    }


                    ImGui.TableNextColumn();
                    var (start, end) = AlarmManager.GetUptime(alarm);
                    var now = DateTime.Now.AddMinutes(alarm.MinutesOffset);
                    if (start > now)
                    {
                        var diff = start - now;
                        Utils.RightAlignTextInColumn($"{(diff.ToString(diff.Hours > 0 ? "hh'h 'mm'm 'ss's'" : "mm'm 'ss's'"))}");
                    }
                    else
                    {
                        Utils.RightAlignTextInColumn(Loc.Text("Triggered"), ImGuiColors.ParsedGreen);
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1f);
                            ImGui.PushStyleColor(ImGuiCol.Border, ImGui.GetColorU32(ImGuiCol.TabActive));
                            ImGui.BeginTooltip();

                            ImGui.TextColored(ImGuiColors.DalamudOrange, Loc.Text("Uptime"));

                            ImGui.Text(Loc.Text("Start:"));
                            ImGui.SameLine();
                            ImGui.Text($"{start:d MMM yyyy hh:mm tt}");

                            ImGui.Text(Loc.Text("End:"));
                            ImGui.SameLine();
                            ImGui.Text($"{end:d MMM yyyy hh:mm tt}");

                            ImGui.EndTooltip();
                            ImGui.PopStyleVar();
                            ImGui.PopStyleColor();
                        }
                    }

                    ImGui.TableNextColumn();
                    var enabled = alarm.Enabled;
                    var printMessage = alarm.PrintMessage;
                    var showToast = alarm.ShowToast;

                    if (ImGui.Checkbox($"##Toggle{alarm.ID}", ref enabled))
                        Plugin.AlarmManager.ToggleAlarm(alarm);
                    Utils.SetTooltip(Loc.Text("Toggles the alarm to be enabled/disabled"));
                    ImGui.SameLine();

                    if (ImGui.Checkbox($"##Print{alarm.ID}", ref printMessage))
                        Plugin.AlarmManager.SetAlarmPrintMessage(alarm, printMessage);
                    Utils.SetTooltip(Loc.Text("Prints a message whenever the alarm is triggered"));
                    ImGui.SameLine();

                    if (ImGui.Checkbox($"##Toast{alarm.ID}", ref showToast))
                        Plugin.AlarmManager.SetAlarmShowToast(alarm, showToast);
                    Utils.SetTooltip(Loc.Text("Display a toast whenever the alarm is triggered"));
                    ImGui.SameLine();

                    if (ImGuiComponents.IconButton($"##Edit{alarm.ID}", FontAwesomeIcon.Edit))
                    {
                        IsInMenu = false;
                        ImGui.OpenPopup($"Edit Alarm {alarm.ID}");
                    }
                    Utils.SetTooltip(Loc.Text("Edit the current alarm"));
                    ImGui.SameLine();

                    if (ImGuiComponents.IconButton($"##Delete{alarm.ID}", FontAwesomeIcon.Trash))
                        Plugin.AlarmManager.DeleteAlarm(alarm);
                    Utils.SetTooltip(Loc.Text("Delete the current alarm"));

                    ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1f);
                    ImGui.PushStyleColor(ImGuiCol.Border, ImGui.GetColorU32(ImGuiCol.TabActive));
                    if (ImGui.BeginPopup($"Edit Alarm {alarm.ID}"))
                    {
                        if (!IsInMenu)
                        {
                            // set values to existing alarm
                            AlarmName = alarm.Name;
                            AlarmType = alarm.Type;
                            TimeType = alarm.TimeType;
                            WeatherType = alarm.Weather;
                            ChatSoundEffect = alarm.ChatSoundEffect;
                            AlarmZone = alarm.ZoneId != 0 ? alarm.ZoneId : (ushort)732;
                            MinutesBefore = alarm.MinutesOffset;

                            IsInMenu = true;
                        }

                        ImGui.Text(Loc.Text("Edit Alarm"));
                        ImGui.SetNextItemWidth(LabelSize);
                        ImGui.LabelText("##NameLabel", Loc.Text("Name:"));
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(150f);
                        ImGui.InputTextWithHint("##Name", Loc.Text("Name of alarm"), ref AlarmName, 15);

                        ImGui.SetNextItemWidth(LabelSize);
                        ImGui.LabelText("##TypeLabel", Loc.Text("Type:"));
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(150f);
                        var currentAlarmType = Array.IndexOf(Enum.GetValues<AlarmType>(), AlarmType);
                        if (ImGui.Combo("##AlarmTypeCombo", ref currentAlarmType, Loc.EnumNames<AlarmType>(), Enum.GetNames<AlarmType>().Length))
                            AlarmType = Enum.GetValues<AlarmType>()[currentAlarmType];

                        if (AlarmType == AlarmType.Weather)
                        {
                            ImGui.SetNextItemWidth(LabelSize);
                            ImGui.LabelText("##ZoneLabel", Loc.Text("Zone:"));
                            ImGui.SameLine();
                            ImGui.SetNextItemWidth(150f);
                            var allZones = Constants.EurekaZones.Select(Utils.GetZoneName).ToArray();
                            var currentZone = Array.IndexOf(allZones, Utils.GetZoneName(AlarmZone));
                            if (ImGui.Combo("##AlarmZoneCombo", ref currentZone, allZones, 4))
                                AlarmZone = Constants.EurekaZones[currentZone];

                            ImGui.SetNextItemWidth(LabelSize);
                            ImGui.LabelText("##WeatherLabel", Loc.Text("Weather:"));
                            ImGui.SameLine();
                            ImGui.SetNextItemWidth(150f);

                            // each zone has a different set of weathers:
                            var selectedZoneWeathers = AlarmZone switch
                            {
                                732 => EurekaAnemos.GetZoneWeathers().ToArray(),
                                763 => EurekaPagos.GetZoneWeathers().ToArray(),
                                795 => EurekaPyros.GetZoneWeathers().ToArray(),
                                827 => EurekaHydatos.GetZoneWeathers().ToArray(),
                                _ => throw new NotImplementedException(),
                            };

                            var currentWeather = Array.IndexOf(selectedZoneWeathers, WeatherType);
                            if (currentWeather == -1)
                            {
                                WeatherType = EurekaWeather.FairSkies;
                                currentWeather = Array.IndexOf(selectedZoneWeathers, EurekaWeather.FairSkies);
                            }

                            if (ImGui.Combo("##WeatherCombo", ref currentWeather, selectedZoneWeathers.Select(x => x.ToFriendlyString()).ToArray(), selectedZoneWeathers.Length))
                                WeatherType = selectedZoneWeathers[currentWeather];
                        }
                        else
                        {
                            ImGui.SetNextItemWidth(LabelSize);
                            ImGui.LabelText("##TimeLabel", Loc.Text("Time:"));
                            ImGui.SameLine();
                            ImGui.SetNextItemWidth(150f);
                            var currentTimeType = Array.IndexOf(Enum.GetValues<TimeType>(), TimeType);
                            if (ImGui.Combo("##TimeTypeCombo", ref currentTimeType, Enum.GetNames<TimeType>(), Enum.GetNames<TimeType>().Length))
                                TimeType = Enum.GetValues<TimeType>()[currentTimeType];
                        }

                        ImGui.SetNextItemWidth(LabelSize);
                        ImGui.LabelText("##SoundLabel", Loc.Text("Sound Effect:"));
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(150f);
                        var useChatSoundEffect = EurekaHelper.Config.GlobalUseChatSoundEffect;
                        if (useChatSoundEffect)
                        {
                            if (Utils.EnumSelector("##SoundChat", null, ref ChatSoundEffect))
                            {
                                SoundManager.PlaySoundEffect(ChatSoundEffect);
                            }
                        }
                        else
                        {
                            if (Utils.EnumSelector("##Sound", null, ref SoundEffect))
                            {
                                SoundManager.PlaySoundEffect(SoundEffect);
                            }
                        }

                        ImGui.SetNextItemWidth(LabelSize);
                        ImGui.LabelText("##TimeLabel", Loc.Text("Minutes Before:"));
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(150f);
                        ImGui.SliderInt("##TimeSlider", ref MinutesBefore, 1, 20, "%d", ImGuiSliderFlags.NoInput);

                        if (ImGui.Button(Loc.Text("Update"), new Vector2(ImGui.GetContentRegionAvail().X, 0.0f)))
                        {
                            UpdateAlarm(alarm);
                            ImGui.CloseCurrentPopup();
                        }

                        ImGui.EndPopup();
                    }

                    ImGui.PopStyleVar();
                    ImGui.PopStyleColor();
                }

                ImGui.EndTable();
            }
        }

        private EurekaAlarm CreateAlarm() => new()
        {
            Name = !string.IsNullOrEmpty(AlarmName) ? AlarmName : "Unnamed",
            Type = AlarmType,
            ZoneId = AlarmType == AlarmType.Weather ? AlarmZone : (ushort)0,
            Weather = AlarmType == AlarmType.Weather ? WeatherType : 0,
            TimeType = AlarmType == AlarmType.Time ? TimeType : 0,
            SoundEffect = SoundEffect,
            ChatSoundEffect = ChatSoundEffect,
            MinutesOffset = MinutesBefore
        };

        private void UpdateAlarm(EurekaAlarm alarm)
        {
            var alarmName = !string.IsNullOrEmpty(AlarmName) ? AlarmName : "Unamed";
            var alarmType = AlarmType;
            var alarmZoneId = AlarmType == AlarmType.Weather ? AlarmZone : (ushort)0;
            var alarmWeather = AlarmType == AlarmType.Weather ? WeatherType : 0;
            var alarmTimeType = AlarmType == AlarmType.Time ? TimeType : 0;
            var alarmSoundEffect = ChatSoundEffect;
            var alarmMinutesOffset = MinutesBefore;

            alarm.Name = alarmName;
            alarm.ChatSoundEffect = alarmSoundEffect;
            EurekaHelper.Config.Save();

            // Check if values are updated
            if (alarmType == alarm.Type &&
                alarmTimeType == alarm.TimeType &&
                alarmWeather == alarm.Weather &&
                alarmZoneId == alarm.ZoneId &&
                alarmMinutesOffset == alarm.MinutesOffset)
                return;

            alarm.Type = alarmType;
            alarm.ZoneId = alarmZoneId;
            alarm.Weather = alarmWeather;
            alarm.TimeType = alarmTimeType;
            alarm.MinutesOffset = alarmMinutesOffset;

            Plugin.AlarmManager.UpdateAlarm(alarm);
        }
    }
}
