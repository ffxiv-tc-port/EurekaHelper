using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Memory;
using Dalamud.Utility.Signatures;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EurekaHelper.System
{
    // This isn't used :(
    public class ZoneManager
    {
        private delegate nint InitZoneDelegate(nint a1, int a2, nint a3);

        private readonly IDtrBarEntry _dtrBarEntry;

        // Last known server ID seen for each zone (index 1-4 = Anemos/Pagos/Pyros/Hydatos, 0 unused). Seeded from
        // Configuration.LastServerIdPerZone on construction and kept in sync on every zone entry (see HandleZoneEntry):
        // InitZoneDetour only fires on an actual zone-entry event, not when the plugin restarts mid-instance. Used to
        // detect "a different running instance than last time", and so the UI can keep showing a zone's last-known
        // server ID in its tracker header after you've left that zone.
        private static readonly ushort[] LastServerIdPerZone = new ushort[5];

        public static ushort GetLastServerId(int zoneIndex) => LastServerIdPerZone[zoneIndex];

        // 只 Cancel 不 Dispose：卸載後才結束的重新加入工作仍要讀得到 Token。
        private readonly CancellationTokenSource _disposeCts = new();

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
            // 伺服器 ID 顯示與「進區自動開追蹤器」的唯一觸發點都是這個 detour：特徵碼對不上（欄位為 null）時
            // 兩者一起停用，其餘功能照常。
            if (InitZoneHook == null)
                DalamudApi.Log.Information("[ZoneManager] 進區 hook 的特徵碼在這個遊戲版本對不上：伺服器 ID 顯示與「進區自動開追蹤器」停用，其餘功能照常。");
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
                _dtrBarEntry.OnClick = _ => EurekaHelper.Plugin.PluginWindow.IsOpen ^= true;

            // If the plugin (re)loaded while already standing inside an Eureka instance, seed the "current" fields (and the
            // DTR bar) too: InitZoneDetour won't fire again until the next zone transition, and the server ID is already
            // known from the persisted config. Must run after _dtrBarEntry is acquired above, or the DTR update here would
            // silently no-op against a still-null entry.
            var currentTerritory = DalamudApi.ClientState.TerritoryType;
            if (Utils.IsPlayerInEurekaZone(currentTerritory))
            {
                var zoneIndex = Utils.GetIndexOfZone(currentTerritory);
                CurrentZoneIndex = zoneIndex;
                CurrentServerId = LastServerIdPerZone[zoneIndex];

                if (EurekaHelper.Config.DisplayServerIdInServerInfo)
                {
                    if (CurrentServerId != 0)
                        SetDtrServerId(CurrentServerId, Utils.GetZoneName(currentTerritory));
                    else
                        SetDtrOutsideEureka();   // 在優雷卡但還沒拿到 ID（例如剛重載外掛）
                }
            }
            else if (EurekaHelper.Config.DisplayServerIdInServerInfo)
            {
                // 在優雷卡之外載入外掛時也要把這一格初始化，否則它會停在預設的空白狀態。
                SetDtrOutsideEureka();
            }
        }

        /// <summary>
        /// 資訊列只放圖示，伺服器 ID 移到滑鼠說明——那一格空間很擠，而且 ID 是偶爾才需要看的資訊。
        /// ElementalLevel（元素等級）是優雷卡專屬圖示，一眼就認得出是哪個外掛。
        /// </summary>
        private void SetDtrServerId(ushort serverId, string zoneName)
        {
            if (_dtrBarEntry == null)
                return;

            _dtrBarEntry.Text = new SeString(new IconPayload(BitmapFontIcon.ElementalLevel));
            _dtrBarEntry.Tooltip = new SeString(new TextPayload(
                $"{(string.IsNullOrEmpty(zoneName) ? "優雷卡" : zoneName)}\n伺服器 ID：{serverId}\n\n左鍵／右鍵：開啟／關閉 Eureka Helper 視窗（追蹤器與設定都在裡面）\n\nEureka Helper"));
            _dtrBarEntry.Shown = true;
        }

        /// <summary>
        /// 不在優雷卡時也保留這一格：它同時是「點擊開啟追蹤器」的按鈕，
        /// 在外面查 NM 時間一樣用得到，只是沒有伺服器 ID 可顯示。
        /// </summary>
        private void SetDtrOutsideEureka()
        {
            if (_dtrBarEntry == null)
                return;

            _dtrBarEntry.Text = new SeString(new IconPayload(BitmapFontIcon.ElementalLevel));
            _dtrBarEntry.Tooltip = new SeString(new TextPayload(
                "目前不在優雷卡（沒有伺服器 ID）\n\n左鍵／右鍵：開啟／關閉 Eureka Helper 視窗（追蹤器與設定都在裡面）\n\nEureka Helper"));
            _dtrBarEntry.Shown = true;
        }

        // 對不上時欄位留 null、只停用這個 hook，不能讓整個插件載入失敗（本專案沒開 nullable，
        // Fallibility 必須明寫，Auto 會判成 Infallible 而擲例外）。特徵碼推導見 commit 訊息。
        [Signature("E8 ?? ?? ?? ?? 45 33 C0 48 8D ?? ?? 41 8B ?? E8 ?? ?? ?? ?? 48 8D ??", DetourName = nameof(InitZoneDetour), Fallibility = Fallibility.Fallible)]
        private readonly Hook<InitZoneDelegate> InitZoneHook = null;

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
                        SetDtrServerId(serverId, zoneName);

                    HandleZoneEntry(Utils.GetIndexOfZone(zoneId), serverId);
                }
                else
                {
                    // 離開優雷卡：保留圖示，說明改為「沒有伺服器 ID」（不隱藏整格）。
                    if (EurekaHelper.Config.DisplayServerIdInServerInfo)
                        SetDtrOutsideEureka();
                    else if (_dtrBarEntry != null)
                        _dtrBarEntry.Shown = false;

                    CurrentZoneIndex = 0;
                    CurrentServerId = 0;
                }
            }
            catch (Exception ex)
            {
                DalamudApi.Log.Error(Loc.Format("Something went wrong. Please contact the author.\n{0}", ex.Message));
            }

            // ⚠️ 一定要把控制權交回遊戲（呼叫 Original），不可以直接 return：
            // 直接 return 的話 detour 會「取代」而不是「攔截」遊戲的進區初始化：
            // 0x70 bytes 的封包 payload 不會被搬進全域物件、進區旗標不會設、三個子呼叫全部不執行，
            // 結果是一進優雷卡就壞掉。回傳值本身無害（呼叫端丟棄 RAX），
            // 傷害純粹來自跳過副作用。
            return InitZoneHook.OriginalDisposeSafe(a1, a2, a3);
        }

        // Leaving a zone never touches its tracker connection. On entering a zone, the connection is rebuilt only if the
        // server ID differs from last time we saw this zone (a different running instance) - the old tracker's NM timers no
        // longer apply, so it is left (or replaced by a new one only with AutoCreateTracker on). If this zone has no recorded server ID yet
        // THIS SESSION, Configuration.TrackerMemory (last tracker per zone + its server ID, see PluginWindow.SetConnection)
        // is checked: a matching server ID means it is safe to silently rejoin; otherwise the current ID becomes the baseline.
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
                if (lastServerId == serverId)
                    return;

                // 換實例：舊追蹤器的 NM 時間不再適用。只有開了自動建立才在 ffxiv-eureka.com 建新的
                // （CreateTracker 會先關掉舊連線）；沒開就只離開舊的並提示，不替使用者在第三方網站建追蹤器。
                if (EurekaHelper.Config.AutoCreateTracker)
                {
                    _ = Task.Run(async () => await EurekaHelper.Plugin.PluginWindow.CreateTracker(zoneIndex));
                    return;
                }

                var oldConnection = EurekaHelper.Plugin.PluginWindow.GetConnection(zoneIndex);
                if (oldConnection.IsConnected())
                {
                    DalamudApi.Log.Information($"[ZoneManager] 換到不同的副本（伺服器 ID {lastServerId} → {serverId}），自動建立追蹤器關著：離開區域 {zoneIndex} 的舊追蹤器 {oldConnection.GetTrackerId()}，不建新的");
                    EurekaHelper.PrintMessage(Loc.Format("Entered a different instance (server ID {0}). Left this zone's previous tracker because its NM times no longer apply. To share times again, create a new tracker with the \"+\" button on the Tracker tab, or turn on \"Auto Create Tracker\".", serverId));
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await oldConnection.Close();
                        }
                        catch (Exception ex)
                        {
                            DalamudApi.Log.Warning($"[ZoneManager] 離開舊追蹤器失敗：{ex.Message}");
                        }
                    });
                }
                return;
            }

            // Auto Create Tracker is triggered here, not from FateManager.OnTerritoryChanged: that event has no guaranteed
            // ordering against this hook (InitZoneDetour), and if it fired first ZoneManager.GetLastServerId(zoneIndex) would
            // still be stale/zero, so the auto-created tracker would be persisted into Configuration.TrackerMemory with the WRONG
            // server ID and break the "rejoin on reload" match. Here serverId is the one just read for this exact entry.
            if (!TryRejoinTracker(zoneIndex, serverId) && EurekaHelper.Config.AutoCreateTracker)
            {
                var connection = EurekaHelper.Plugin.PluginWindow.GetConnection(zoneIndex);
                if (!connection.IsConnected())
                    _ = Task.Run(async () => await EurekaHelper.Plugin.PluginWindow.CreateTracker(zoneIndex, true));
            }
        }

        // Silently rejoins the zone's last-known tracker (from Configuration.TrackerMemory) if we're not already connected
        // and the remembered server ID still matches. Shared by HandleZoneEntry and TryRejoinCurrentZoneTracker (plugin
        // reloaded while standing inside the instance). Returns true if a matching memory entry was found and a rejoin
        // was kicked off (even if that async rejoin later fails) - callers use this to decide whether to fall back to
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

            var code = memory.Code;
            _ = Task.Run(async () =>
            {
                try
                {
                    var rejoined = await EurekaConnectionManager.JoinTracker(memory.Code, memory.Password);
                    EurekaHelper.Plugin.PluginWindow.SetConnection(zoneIndex, rejoined);
                    DalamudApi.Log.Debug($"[ZoneManager] TryRejoinTracker: zone {zoneIndex} rejoined tracker {memory.Code}, connected={rejoined?.IsConnected()}");
                    if (rejoined != null && rejoined.IsInvalid())
                        QueueForgetGoneTracker(zoneIndex, memory, code, rejoined);
                }
                catch (Exception ex)
                {
                    DalamudApi.Log.Error(ex, $"[ZoneManager] TryRejoinTracker: zone {zoneIndex} failed to rejoin tracker {memory.Code}");
                }
            });

            return true;
        }

        // 執行緒池呼叫；改設定與存檔排到框架執行緒（與 UI 端的 Config.Save() 同一條）。插件卸載後不清。
        private void QueueForgetGoneTracker(int zoneIndex, TrackerMemoryEntry memory, string code, EurekaConnectionManager rejoined)
        {
            var token = _disposeCts.Token;
            if (token.IsCancellationRequested || DalamudApi.Framework.IsFrameworkUnloading)
                return;

            _ = DalamudApi.Framework.Run(() =>
            {
                if (token.IsCancellationRequested || DalamudApi.Framework.IsFrameworkUnloading)
                    return;

                try
                {
                    ForgetGoneTracker(EurekaHelper.Config, zoneIndex, memory, code, rejoined);
                }
                catch (Exception ex)
                {
                    DalamudApi.Log.Warning($"[ZoneManager] 清除第 {zoneIndex} 區記住的追蹤器 {code} 失敗：{ex.Message}");
                }
            }, token);
        }

        // TrackerMemory 只在框架執行緒上改（與其他存檔同一條）。執行時該區放的必須仍是這個連線、而且還連著才寫，
        // 否則代表之後又換過或斷了，這一筆就作廢。
        internal void QueueRememberTracker(int zoneIndex, EurekaConnectionManager connection, TrackerMemoryEntry entry)
        {
            var token = _disposeCts.Token;
            if (token.IsCancellationRequested || DalamudApi.Framework.IsFrameworkUnloading)
                return;

            _ = DalamudApi.Framework.Run(() =>
            {
                if (token.IsCancellationRequested || DalamudApi.Framework.IsFrameworkUnloading)
                    return;

                try
                {
                    if (!ReferenceEquals(Windows.PluginWindow.PeekConnection(zoneIndex), connection) || !connection.IsConnected())
                    {
                        DalamudApi.Log.Debug($"[ZoneManager] 第 {zoneIndex} 區連線已換掉或中斷，不記住追蹤器 {entry.Code}");
                        return;
                    }

                    EurekaHelper.Config.TrackerMemory[zoneIndex] = entry;
                    EurekaHelper.Config.Save();
                    DalamudApi.Log.Debug($"[ZoneManager] saved TrackerMemory for zone {zoneIndex}, code={entry.Code}, serverId={entry.ServerId}");
                }
                catch (Exception ex)
                {
                    DalamudApi.Log.Warning($"[ZoneManager] 記住第 {zoneIndex} 區的追蹤器 {entry.Code} 失敗：{ex.Message}");
                }
            }, token);
        }

        // 只在伺服器明確回「追蹤器不存在」（IsInvalid）時移除；連不上、逾時、其他錯誤都不算。
        // 該區記憶必須仍是當初拿去重新加入的那一筆（同物件、同代碼），被換成別的就不動。回傳是否移除。
        internal static bool ForgetGoneTracker(Configuration config, int zoneIndex, TrackerMemoryEntry memory, string code, EurekaConnectionManager rejoined)
        {
            if (rejoined == null || !rejoined.IsInvalid())
                return false;

            if (!config.TrackerMemory.TryGetValue(zoneIndex, out var current) || !ReferenceEquals(current, memory) || current.Code != code)
                return false;

            config.TrackerMemory.Remove(zoneIndex);
            config.Save();
            DalamudApi.Log.Information($"[ZoneManager] 第 {zoneIndex} 區記住的追蹤器 {code} 在網站上已不存在，已清除；下次載入不再自動加入");
            return true;
        }

        // InitZoneDetour (and therefore HandleZoneEntry) only fires on an actual zone-entry event, and only for the zone
        // the player is standing in. Call this once PluginWindow exists (see the EurekaHelper constructor - this class
        // can't call into PluginWindow any earlier) to retry the silent rejoin for ALL FOUR zones, each against its own
        // last-known server ID (seeded from Configuration.LastServerIdPerZone in the constructor above), so every zone's
        // tracker connection survives a reload, not just the current one.
        public void TryRejoinCurrentZoneTracker()
        {
            for (var zoneIndex = 1; zoneIndex <= 4; zoneIndex++)
            {
                var serverId = LastServerIdPerZone[zoneIndex];
                if (serverId == 0)
                {
                    DalamudApi.Log.Debug($"[ZoneManager] TryRejoinCurrentZoneTracker: zone {zoneIndex} has no known server ID, skipping");
                    continue;
                }

                TryRejoinTracker(zoneIndex, serverId);
            }
        }

        // Closes and rejoins a zone's tracker using the same code/password it already had - used by the manual "rebuild"
        // button in the Tracker tab, for when the connection seems stuck/stale but the tracker itself is still the right
        // one. (A server ID mismatch on zone entry is handled in HandleZoneEntry instead.)
        // No-op if that zone isn't currently connected to anything.
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
            _disposeCts.Cancel();
            InitZoneHook?.Dispose();
            _dtrBarEntry?.Remove();
        }
    }
}