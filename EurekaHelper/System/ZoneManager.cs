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

        // Which zone tracker (1=Anemos, 2=Pagos, 3=Pyros, 4=Hydatos, 0=none) is currently the
        // "active" one for auto-remember/reconnect purposes, and the server ID it was joined on.
        private int _activeZoneIndex;
        private ushort _activeServerId;

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

                    HandleTrackerAutoReconnect(Utils.GetIndexOfZone(zoneId), serverId);
                }
                else
                {
                    if (_dtrBarEntry != null)
                    {
                        _dtrBarEntry.Text = "";
                        _dtrBarEntry.Shown = false;
                    }

                    HandleTrackerAutoReconnect(0, 0);
                }
            }
            catch (Exception ex)
            {
                DalamudApi.Log.Error(Loc.Format("Something went wrong. Please contact the author.\n{0}", ex.Message));
            }

            return InitZoneHook.Original(a1, a2, a3);
        }

        // Since you can only be in one map at a time, there's no need to stay connected to more
        // than one zone's tracker simultaneously. On leaving a zone, remember what tracker was
        // connected there (and the server ID it was on) so returning to the same zone on the
        // same server ID can silently rejoin it instead of leaving the tab disconnected.
        private void HandleTrackerAutoReconnect(int newZoneIndex, ushort newServerId)
        {
            if (_activeZoneIndex == newZoneIndex)
                return;

            if (_activeZoneIndex is >= 1 and <= 4)
            {
                var oldConnection = EurekaHelper.Plugin.PluginWindow.GetConnection(_activeZoneIndex);
                if (oldConnection.IsConnected())
                {
                    EurekaHelper.Config.TrackerMemory[_activeZoneIndex] = new TrackerMemoryEntry
                    {
                        Code = oldConnection.GetTrackerId(),
                        Password = oldConnection.CanModify() ? oldConnection.GetTrackerPassword() : string.Empty,
                        ServerId = _activeServerId,
                    };
                    EurekaHelper.Config.Save();
                    _ = Task.Run(oldConnection.Close);
                }
            }

            _activeZoneIndex = newZoneIndex;
            _activeServerId = newServerId;

            if (newZoneIndex is >= 1 and <= 4 &&
                EurekaHelper.Config.TrackerMemory.TryGetValue(newZoneIndex, out var memory) &&
                memory.ServerId == newServerId &&
                !string.IsNullOrWhiteSpace(memory.Code))
            {
                _ = Task.Run(async () =>
                {
                    var connection = await EurekaConnectionManager.JoinTracker(memory.Code, memory.Password);
                    EurekaHelper.Plugin.PluginWindow.SetConnection(newZoneIndex, connection);
                });
            }
        }

        public void Dispose()
        {
            InitZoneHook?.Dispose();
            _dtrBarEntry?.Remove();
        }
    }
}
