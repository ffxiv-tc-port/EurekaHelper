using Dalamud.Game.Gui.Dtr;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Memory;
using Dalamud.Utility.Signatures;
using System;
using System.Threading.Tasks;

namespace EurekaHelper.System
{
    public class ZoneManager
    {
        private delegate nint InitZoneDelegate(nint a1, int a2, nint a3);
        private readonly IDtrBarEntry _dtrBarEntry;

        // Last known server ID seen for each zone (index 1-4 = Anemos/Pagos/Pyros/Hydatos, 0
        // unused), session-scoped only. Used purely to detect "this is a different running
        // instance than last time I was here" - not persisted, since a fresh plugin load has no
        // prior instance to compare against anyway.
        private readonly ushort[] _lastServerIdPerZone = new ushort[5];

        // Exposed publicly so the UI can show "which server ID am I actually on right now" (e.g.
        // next to the tracker's viewer count) without duplicating the InitZoneDetour hook.
        public static int CurrentZoneIndex { get; private set; }
        public static ushort CurrentServerId { get; private set; }

        public ZoneManager()
        {
            DalamudApi.GameInteropProvider.InitializeFromAttributes(this);
            InitZoneHook?.Enable();

            var dtrBarTitle = "Eureka Helper";
            try
            {
                _dtrBarEntry = DalamudApi.DtrBar.Get(dtrBarTitle);
            }
            catch (ArgumentException ex)
            {
                for (var i = 0; i < 5; i++)
                {
                    DalamudApi.Log.Error(ex, $"Failed to acquire DtrBarEntry {dtrBarTitle}, trying {dtrBarTitle}{i}");
                    try
                    {
                        _dtrBarEntry = DalamudApi.DtrBar.Get($"{dtrBarTitle}{i}");
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }

                    break;
                }
            }

            if (_dtrBarEntry != null)
                _dtrBarEntry.OnClick = () => EurekaHelper.Plugin.PluginWindow.IsOpen ^= true;
        }

        [Signature("E8 ?? ?? ?? ?? 45 33 C0 48 8D ?? ?? 8B ?? E8 ?? ?? ?? ?? 48 8D ??", DetourName = nameof(InitZoneDetour))]
        private readonly Hook<InitZoneDelegate> InitZoneHook = null!;

        private nint InitZoneDetour(nint a1, int a2, nint a3)
        {
            try
            {
                ushort serverId = MemoryHelper.Read<ushort>(a3);
                ushort zoneId = MemoryHelper.Read<ushort>(a3 + 2);
                var zoneName = Utils.GetZoneName(zoneId);

                if (zoneName != null)
                {
                    if (EurekaHelper.Config.DisplayServerId)
                        EurekaHelper.PrintMessage(Loc.Format("{0} Server ID: {1}", zoneName, serverId));

                    if (EurekaHelper.Config.DisplayServerIdInServerInfo)
                    {
                        if (_dtrBarEntry != null)
                        {
                            _dtrBarEntry.Text = Loc.Format("Server ID: {0}", serverId);
                            _dtrBarEntry.Shown = true;
                        }
                    }

                    HandleZoneEntry(Utils.GetIndexOfZone(zoneId), serverId);
                }
                else
                {
                    if (_dtrBarEntry != null)
                    {
                        _dtrBarEntry.Text = "";
                        _dtrBarEntry.Shown = false;
                    }

                    CurrentZoneIndex = 0;
                    CurrentServerId = 0;
                }
            }
            catch (Exception ex)
            {
                DalamudApi.Log.Error(Loc.Format("Something went wrong. Please contact the author.\n{0}", ex.Message));
            }

            return InitZoneHook.Original(a1, a2, a3);
        }

        // Leaving a zone no longer touches its tracker connection at all - you can zone in and
        // out of the same instance repeatedly without losing it. On entering a zone, only
        // rebuild the connection if the server ID differs from last time we saw this zone,
        // meaning it's actually a different running instance now (so the existing tracker no
        // longer reflects reality). If this zone has no recorded server ID yet (first time this
        // session), just adopt the current one as the baseline rather than treating it as a
        // mismatch.
        private void HandleZoneEntry(int zoneIndex, ushort serverId)
        {
            CurrentZoneIndex = zoneIndex;
            CurrentServerId = serverId;

            if (zoneIndex is < 1 or > 4)
                return;

            var lastServerId = _lastServerIdPerZone[zoneIndex];
            _lastServerIdPerZone[zoneIndex] = serverId;

            if (lastServerId != 0 && lastServerId != serverId)
                RebuildTrackerConnection(zoneIndex);
        }

        // Closes and rejoins a zone's tracker using the same code/password it already had -
        // used both automatically (server ID mismatch on zone entry) and manually (a "rebuild"
        // button in the Tracker tab, for when the connection just seems stuck/stale). No-op if
        // that zone isn't currently connected to anything.
        public static void RebuildTrackerConnection(int zoneIndex)
        {
            var connection = EurekaHelper.Plugin.PluginWindow.GetConnection(zoneIndex);
            if (!connection.IsConnected())
                return;

            var code = connection.GetTrackerId();
            var password = connection.CanModify() ? connection.GetTrackerPassword() : string.Empty;
            _ = Task.Run(async () =>
            {
                await connection.Close();
                var rejoined = await EurekaConnectionManager.JoinTracker(code, password);
                EurekaHelper.Plugin.PluginWindow.SetConnection(zoneIndex, rejoined);
            });
        }

        public void Dispose()
        {
            InitZoneHook?.Dispose();
            _dtrBarEntry?.Remove();
        }
    }
}
