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
        // unused). Seeded from Configuration.LastServerIdPerZone on construction and kept in sync
        // with it on every zone entry (see HandleZoneEntry), so a plugin reload while already
        // standing inside an instance doesn't lose it - InitZoneDetour only fires on an actual
        // zone-entry event and won't refire just because the plugin restarted mid-instance. Used
        // both to detect "this is a different running instance than last time I was here", and so
        // the UI can keep showing a zone's last-known server ID in its tracker header even after
        // you've left that zone, instead of it disappearing.
        private static readonly ushort[] LastServerIdPerZone = new ushort[5];

        public static ushort GetLastServerId(int zoneIndex) => LastServerIdPerZone[zoneIndex];

        // Exposed publicly so the UI can show "which server ID am I actually on right now" (e.g.
        // next to the tracker's viewer count) without duplicating the InitZoneDetour hook.
        public static int CurrentZoneIndex { get; private set; }
        public static ushort CurrentServerId { get; private set; }

        public ZoneManager()
        {
            for (var zoneIndex = 1; zoneIndex <= 4; zoneIndex++)
            {
                if (EurekaHelper.Config.LastServerIdPerZone.TryGetValue(zoneIndex, out var serverId))
                    LastServerIdPerZone[zoneIndex] = serverId;
            }

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

            // If the plugin (re)loaded while already standing inside an Eureka instance, seed the
            // "current" fields (and the DTR bar) too - InitZoneDetour won't fire again until the
            // next actual zone transition, so without this the UI/DTR bar would show nothing until
            // then even though we already know the server ID from the persisted config. Must run
            // after _dtrBarEntry is acquired above, or the DTR update here would silently no-op
            // against a still-null entry.
            var currentTerritory = DalamudApi.ClientState.TerritoryType;
            if (Utils.IsPlayerInEurekaZone(currentTerritory))
            {
                var zoneIndex = Utils.GetIndexOfZone(currentTerritory);
                CurrentZoneIndex = zoneIndex;
                CurrentServerId = LastServerIdPerZone[zoneIndex];

                if (CurrentServerId != 0 && EurekaHelper.Config.DisplayServerIdInServerInfo && _dtrBarEntry != null)
                {
                    _dtrBarEntry.Text = Loc.Format("Server ID: {0}", CurrentServerId);
                    _dtrBarEntry.Shown = true;
                }
            }
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
        // longer reflects reality) - and rebuilding here means a brand-new tracker, not
        // rejoining the old one, since a different server ID means different NM spawns/timers
        // that the old tracker's data no longer applies to.
        //
        // If this zone has no recorded server ID yet THIS SESSION, check whether a plugin
        // restart wiped an in-progress connection: Configuration.TrackerMemory persists the last
        // tracker joined per zone (see PluginWindow.SetConnection) along with the server ID it
        // was on, so a matching server ID here means it's safe to silently rejoin instead of
        // leaving the tab empty. No match (or first time ever) just adopts the current server ID
        // as the baseline.
        private void HandleZoneEntry(int zoneIndex, ushort serverId)
        {
            CurrentZoneIndex = zoneIndex;
            CurrentServerId = serverId;

            if (zoneIndex is < 1 or > 4)
                return;

            var lastServerId = LastServerIdPerZone[zoneIndex];
            LastServerIdPerZone[zoneIndex] = serverId;
            EurekaHelper.Config.LastServerIdPerZone[zoneIndex] = serverId;
            EurekaHelper.Config.Save();

            if (lastServerId != 0)
            {
                if (lastServerId != serverId)
                    _ = Task.Run(async () => await EurekaHelper.Plugin.PluginWindow.CreateTracker(zoneIndex));
                return;
            }

            // Auto Create Tracker used to be triggered from FateManager.OnTerritoryChanged, a
            // separate event from this hook (InitZoneDetour) with no guaranteed ordering against
            // it - if that fired first, EurekaConnectionManager.SetConnection's
            // ZoneManager.GetLastServerId(zoneIndex) read a stale/zero server ID (this zone's
            // LastServerIdPerZone entry hadn't been updated yet), so the auto-created tracker got
            // persisted into Configuration.TrackerMemory with the WRONG server ID - silently
            // breaking the "rejoin on reload" match below on every subsequent zone entry. Doing it
            // here instead guarantees serverId is the one that was just read for this exact entry.
            if (!TryRejoinTracker(zoneIndex, serverId) && EurekaHelper.Config.AutoCreateTracker)
            {
                var connection = EurekaHelper.Plugin.PluginWindow.GetConnection(zoneIndex);
                if (!connection.IsConnected())
                    _ = Task.Run(async () => await EurekaHelper.Plugin.PluginWindow.CreateTracker(zoneIndex, true));
            }
        }

        // Silently rejoins the zone's last-known tracker (from Configuration.TrackerMemory) if
        // we're not already connected and the remembered server ID still matches. Shared by
        // HandleZoneEntry (normal zone-entry path) and TryRejoinCurrentZoneTracker (plugin
        // reloaded while already standing inside the instance - see that method).
        // Returns true if a matching memory entry was found and a rejoin was kicked off (even if
        // that async rejoin later fails) - callers use this to decide whether to fall back to
        // auto-creating a brand-new tracker instead.
        private bool TryRejoinTracker(int zoneIndex, ushort serverId)
        {
            var connection = EurekaHelper.Plugin.PluginWindow.GetConnection(zoneIndex);
            if (connection.IsConnected())
            {
                DalamudApi.Log.Debug($"[ZoneManager] TryRejoinTracker: zone {zoneIndex} already connected, skipping");
                return true;
            }

            if (!EurekaHelper.Config.TrackerMemory.TryGetValue(zoneIndex, out var memory))
            {
                DalamudApi.Log.Debug($"[ZoneManager] TryRejoinTracker: no TrackerMemory entry for zone {zoneIndex}");
                return false;
            }

            if (memory.ServerId != serverId)
            {
                DalamudApi.Log.Debug($"[ZoneManager] TryRejoinTracker: zone {zoneIndex} TrackerMemory server ID {memory.ServerId} != current {serverId}, not rejoining (different instance)");
                return false;
            }

            if (string.IsNullOrWhiteSpace(memory.Code))
            {
                DalamudApi.Log.Debug($"[ZoneManager] TryRejoinTracker: zone {zoneIndex} TrackerMemory has no code");
                return false;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var rejoined = await EurekaConnectionManager.JoinTracker(memory.Code, memory.Password);
                    EurekaHelper.Plugin.PluginWindow.SetConnection(zoneIndex, rejoined);
                    DalamudApi.Log.Debug($"[ZoneManager] TryRejoinTracker: zone {zoneIndex} rejoined tracker {memory.Code}, connected={rejoined?.IsConnected()}");
                }
                catch (Exception ex)
                {
                    DalamudApi.Log.Error(ex, $"[ZoneManager] TryRejoinTracker: zone {zoneIndex} failed to rejoin tracker {memory.Code}");
                }
            });

            return true;
        }

        // InitZoneDetour (and therefore HandleZoneEntry, which normally drives tracker
        // rejoining) only fires on an actual zone-entry event. If the plugin is loaded/reloaded
        // while already standing inside an Eureka instance, that event never refires, so the
        // tracker would otherwise just stay disconnected until the player physically zones out
        // and back in. Call this once PluginWindow exists (see EurekaHelper.cs constructor,
        // after PluginWindow is created - this class can't call into PluginWindow any earlier)
        // to retry the same silent-rejoin logic using the server ID already seeded from
        // Configuration.LastServerIdPerZone in the constructor above.
        public void TryRejoinCurrentZoneTracker()
        {
            if (CurrentZoneIndex is < 1 or > 4 || CurrentServerId == 0)
            {
                DalamudApi.Log.Debug($"[ZoneManager] TryRejoinCurrentZoneTracker: nothing to do (CurrentZoneIndex={CurrentZoneIndex}, CurrentServerId={CurrentServerId})");
                return;
            }

            TryRejoinTracker(CurrentZoneIndex, CurrentServerId);
        }

        // Closes and rejoins a zone's tracker using the same code/password it already had - used
        // by the manual "rebuild" button in the Tracker tab, for when the connection just seems
        // stuck/stale but the underlying tracker itself is still the right one. (A server ID
        // mismatch on zone entry is handled separately, in HandleZoneEntry, by creating a whole
        // new tracker instead - see that method's comment for why.) No-op if that zone isn't
        // currently connected to anything.
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
