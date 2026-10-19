using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ECommons;
using ECommons.SplatoonAPI;
using Lumina.Excel.Sheets;

namespace EurekaHelper.System
{
    // 幸福兔 FATE 後取得的「幸運胡蘿蔔」，每次使用會在聊天視窗印出一則方位＋距離等級的提示
    // （例："財寶好像是在西北方向稍遠的地方！"）。這個 class 監聽聊天訊息、記錄「提示當下的
    // 玩家座標＋方位＋距離等級」，並用多筆提示做方位交會（bearing-only triangulation）估算
    // 寶藏位置，透過 Splatoon 疊層畫出範圍圈，隨提示增加逐步縮小。
    public class TreasureHuntManager : IDisposable
    {
        private const string LayerName = "EurekaHelper.TreasureHunt";
        private const string HistoryMarkerLayerName = LayerName + ".HistoryMarker";

        // How close the bearing-triangulated EstimatedPosition needs to land to a past
        // TreasureFoundRecord (same territory) before we trust the historical spot over the
        // triangulation and snap to it instead.
        private const float HistoricalPredictionMatchRadius = 30f;

        // How close the player physically needs to walk to a past TreasureFoundRecord (same
        // territory) before a "dig here" marker is drawn at that historical spot - only while a
        // hunt is active (see OnFrameworkUpdate).
        private const float HistoricalProximityRadius = 100f;

        // 8 方位 -> 從正北順時針量測的角度（度）。遊戲聊天文字用的是中文全形方位詞。
        private static readonly Dictionary<string, float> DirectionAngles = new()
        {
            ["正北"] = 0f,
            ["東北"] = 45f,
            ["正東"] = 90f,
            ["東南"] = 135f,
            ["正南"] = 180f,
            ["西南"] = 225f,
            ["正西"] = 270f,
            ["西北"] = 315f,
        };

        // 距離帶在 PotTreasureCandidates；不認得的距離文字比對只看方向，這組只給估計位置與扇形長度用。
        private static readonly (float Min, float Max) UnknownTierFallback = (25f, 200f);

        // 「很遠」沒有上限：畫扇形、算 EstimatedPosition／EstimatedRadius 時夾在這裡，免得丟出無限長的線段。
        private const float FarTierRenderCap = 500f;

        private static readonly Regex HintRegex =
            new(@"財寶好像是在(?<dir>正東|正南|正西|正北|東北|東南|西南|西北)方向(?<tier>[^的]+?)的地方", RegexOptions.Compiled);

        // 挖到寶藏時的系統訊息，例如"發現了財寶！！"。找到的當下座標最準確 - 拿來跟這一輪收集
        // 到的提示鏈一起存進歷史，之後可以回頭校正距離帶的碼數區間準不準。
        private static readonly Regex FoundRegex = new(@"發現了財寶", RegexOptions.Compiled);

        // 「似乎能夠告知第二處財寶所在地！」：下一輪提示指向偏遠的第二處財寶點，不在魔法甕那 60 點裡。
        private static readonly Regex SecondTreasureRegex = new(@"第二處財寶", RegexOptions.Compiled);

        public IReadOnlyList<TreasureHint> Hints => _hints;
        public IReadOnlyList<TreasureFoundRecord> History => EurekaHelper.Config.TreasureHuntHistory;
        public Vector2? EstimatedPosition { get; private set; }
        public float EstimatedRadius { get; private set; }

        // 這次推算到底有多不準，給 UI 直接顯示在列上用。
        // null = **上不封頂**：最新一次提示是「很遠」，而「很遠」的碼數區間沒有上限，
        // 所以真正的不確定範圍是未知的。這種時候 UI 必須顯示
        // 「?」而不是拿 FarTierRenderCap 那個「只是為了不要畫出無限長線段」的數字充數 ——
        // 那會讓一個純粹的繪圖上限看起來像一個測量結果。
        public float? EstimateUncertainty { get; private set; }

        // True when EstimatedPosition came from a past TreasureFoundRecord (strong fan-match or
        // proximity snap) rather than the raw bearing triangulation - lets the UI flag that the
        // suggested spot is a confirmed historical dig site, not just a geometric guess.
        public bool IsUsingHistoricalPosition { get; private set; }

        // 給 UI 顯示連線狀態用：Splatoon 疊層圓圈需要遊戲裡真的裝了「Splatoon」這個 Dalamud
        // 外掛且已連上 ECommons IPC，才會實際畫出來 - 沒裝的話這裡會一直是 false，但地圖旗標
        // （SetMapFlag）不受影響，仍會照常更新。
        // NOTE: read straight from Splatoon.IsConnected(), not a flag set via Splatoon.SetOnConnect(): that callback is a
        // single static slot and SplatoonManager owns it (a second registration silently overwrites the first).
        public bool IsSplatoonReady => Splatoon.IsConnected();

        // 魔法甕候選點：只在 PotTreasureCandidates.HasTable 的地區有值，全部在聊天事件裡更新。
        // FlagCandidate 為 null 而 IsCandidateFallback 為真 ＝ 有提示但沒有候選符合，旗標退回估計位置。
        public bool HasCandidateTable => PotTreasureCandidates.HasTable(DalamudApi.ClientState.TerritoryType);
        public IReadOnlyList<PotCandidateState> MatchingCandidates => _matchingCandidates;
        public int CandidatePoolSize { get; private set; }
        public PotCandidatePool CandidatePool { get; private set; }
        public ushort? PreferredPotFateId { get; private set; }
        public PotCandidate? FlagCandidate { get; private set; }
        public bool IsFlagCandidateConfirmed { get; private set; }
        public bool IsCandidateFallback { get; private set; }

        private const float CandidateCircleRadius = 3f;

        private readonly List<TreasureHint> _hints = new();
        private TreasureFoundRecord _nearbyHistoricalRecord;
        private List<PotCandidateState> _matchingCandidates = new();
        private bool _secondTreasurePending;

        // Func<bool>：true ＝ 新月島魔法甕尋寶這一輪進行中，而且本插件會自己送走向旗標。名字與型別是對外契約，不改。
        public const string PotHuntActiveIpcName = "EurekaHelper.OccultPotHuntActive";

        private readonly PotHuntActivity _potHunt = new();
        private readonly ICallGateProvider<bool> _potHuntActiveIpc;

        // 跨執行緒讀的唯一欄位：PotHuntActivity.ActiveUntilMs 的快照，只在框架執行緒寫。
        private long _potHuntActiveUntil;

        // Mappy 的點綁地區：換區或登出就移除，不像提示鏈與 Splatoon 扇形那樣留著。
        private readonly MappyPotOverlay _mappyOverlay;
        private bool _mappyFailureReported;

        public MappyOverlayStatus MappyStatus => _mappyOverlay.Client.Status;

        // Func<(int Sequence, Vector3 Destination, bool HasHeight)>：最近一次「自動走向」送給 vnavmesh 的目的地（見 PotNavTargetPublisher）。
        // BOCCHI 用它認出 vnavmesh 正在走的是本插件送的路、套它的仇恨迴避。名字與型別是對外契約，要改形狀就換新名字。
        public const string PotNavTargetIpcName = "EurekaHelper.OccultPotNavTarget";

        private readonly PotNavTargetPublisher _navTarget = new();
        private readonly ICallGateProvider<(int, Vector3, bool)> _navTargetIpc;

        // vnavmesh 端點，名字與型別逐字照 vnavmesh IPCProvider：Func<Vector3 dest, bool fly, bool>。
        private const string VnavMoveToIpcName = "vnavmesh.SimpleMove.PathfindAndMoveTo";
        private readonly ICallGateSubscriber<Vector3, bool, bool> _vnavMoveTo;

        // 先取消還在算的尋路、再清掉已經在走的路徑點（只送 Path.Stop 攔不住背景算完才開走的那一條）。
        private readonly ICallGateSubscriber<object> _vnavPathfindCancelAll;
        private readonly ICallGateSubscriber<object> _vnavPathStop;

        // 按清除時認「這條路是不是本插件送的」用，型別逐字照 vnavmesh IPCProvider。
        private readonly ICallGateSubscriber<bool> _vnavMoveInProgress;
        private readonly ICallGateSubscriber<bool> _vnavPathRunning;
        private readonly ICallGateSubscriber<List<Vector3>> _vnavPathWaypoints;

        // 艦隊急停：Action<string>，參數是呼叫端署名；與 BOCCHI.StopAll／HoardFarm.StopAll 同形。名字與型別是對外契約。
        public const string StopAllIpcName = "EurekaHelper.StopAll";
        private readonly ICallGateProvider<string, object> _stopAllIpc;

        // 由 IPC（任何執行緒）寫入、框架執行緒下一幀取走；null＝沒有待處理的急停。
        private string _pendingStop;

        private static readonly VirtualKey[] MoveKeyCandidates =
        [
            VirtualKey.W, VirtualKey.A, VirtualKey.S, VirtualKey.D, VirtualKey.Q, VirtualKey.E,
            VirtualKey.UP, VirtualKey.DOWN, VirtualKey.LEFT, VirtualKey.RIGHT,
        ];

        // 手把左搖桿的值域約 ±99，超過這個值才算在推。
        private const int StickThreshold = 30;
        private const long DeferLogIntervalMs = 5_000;

        // 以下只在框架執行緒讀寫。
        private readonly HandsOnLatch _handsOn = new();
        private readonly AutoMoveDeferral _moveDeferral = new();
        private VirtualKey[] _moveKeys;
        private bool _mouseKeysValid;
        private bool _handsOnBroken;
        private long _nextDeferLogAt;

        public TreasureHuntManager()
        {
            _mappyOverlay = new MappyPotOverlay(
                new MappyOverlayClient(DalamudApi.PluginInterface, static message => DalamudApi.Log.Information(message)),
                static message => DalamudApi.Log.Information(message));

            DalamudApi.Framework.Update += OnFrameworkUpdate;
            DalamudApi.ClientState.TerritoryChanged += OnTerritoryChanged;
            DalamudApi.ClientState.Logout += OnLogout;
            // ECommonsMain.Init/Dispose 是可重複呼叫的（內部有初始化計數），SplatoonManager
            // 也會各自呼叫一次；兩者互不影響。這裡刻意不在 Dispose() 呼叫 ECommonsMain.Dispose()，
            // 避免在 SplatoonManager 已停用（EnableSplatoonAggroRanges=false）時把另一個仍在用
            // 的 Splatoon 連線關掉、或造成重複釋放。
            ECommonsMain.Init(DalamudApi.PluginInterface, EurekaHelper.Plugin, Module.SplatoonAPI);

            DalamudApi.ChatGui.ChatMessage += OnChatMessage;

            _potHuntActiveIpc = DalamudApi.PluginInterface.GetIpcProvider<bool>(PotHuntActiveIpcName);
            _potHuntActiveIpc.RegisterFunc(IsPotHuntActiveForIpc);

            _navTargetIpc = DalamudApi.PluginInterface.GetIpcProvider<(int, Vector3, bool)>(PotNavTargetIpcName);
            _navTargetIpc.RegisterFunc(_navTarget.Read);

            _vnavMoveTo = DalamudApi.PluginInterface.GetIpcSubscriber<Vector3, bool, bool>(VnavMoveToIpcName);
            _vnavPathfindCancelAll = DalamudApi.PluginInterface.GetIpcSubscriber<object>("vnavmesh.Nav.PathfindCancelAll");
            _vnavPathStop = DalamudApi.PluginInterface.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
            _vnavMoveInProgress = DalamudApi.PluginInterface.GetIpcSubscriber<bool>("vnavmesh.SimpleMove.PathfindInProgress");
            _vnavPathRunning = DalamudApi.PluginInterface.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning");
            _vnavPathWaypoints = DalamudApi.PluginInterface.GetIpcSubscriber<List<Vector3>>("vnavmesh.Path.ListWaypoints");

            _stopAllIpc = DalamudApi.PluginInterface.GetIpcProvider<string, object>(StopAllIpcName);
            _stopAllIpc.RegisterAction(RequestStopAll);
        }

        // IPC 端點，任何執行緒：框架執行緒上當場停，其他執行緒只記下來、下一幀在 OnFrameworkUpdate 停。不擲例外給呼叫端。
        private void RequestStopAll(string source)
        {
            var label = string.IsNullOrWhiteSpace(source) ? "?" : source.Trim();
            try
            {
                if (DalamudApi.Framework.IsInFrameworkUpdateThread)
                {
                    StopAutomation(label);
                    return;
                }
            }
            catch (Exception ex)
            {
                DalamudApi.Log.Error(ex, $"[TreasureHuntManager] 急停（{label}）執行失敗");
                return;
            }

            Interlocked.Exchange(ref _pendingStop, label);
        }

        // 急停只停自動化：待補送的自動走向、這一輪的「尋寶進行中」、本插件送出的 vnavmesh 導航。
        // 提示鏈與畫面標記留著、設定不動；之後的新提示照「自動走向旗標」設定行事。只在框架執行緒呼叫。
        private void StopAutomation(string reason)
        {
            var hadPendingMove = _moveDeferral.Pending;
            var wasActive = _potHunt.IsActive;
            var sentMove = _navTarget.Read().Sequence != 0;
            _moveDeferral.Reset();
            EndPotHunt(PotHuntEndReason.EmergencyStop);
            _navTarget.Clear();
            var navNote = sentMove ? StopVnavmesh() : "本插件沒有送出中的自動走向，未動 vnavmesh";
            DalamudApi.Log.Information($"[TreasureHuntManager] 收到急停（{reason}）：待補送的自動走向 {(hadPendingMove ? "已取消" : "沒有")}；魔法甕尋寶進行中 {(wasActive ? "已結束" : "原本就沒有")}；{navNote}。提示與畫面標記保留，設定不變。");
        }

        private string StopVnavmesh()
        {
            try
            {
                _vnavPathfindCancelAll.InvokeAction();
                _vnavPathStop.InvokeAction();
                return "已送 vnavmesh Nav.PathfindCancelAll ＋ Path.Stop";
            }
            catch (IpcNotReadyError)
            {
                return "vnavmesh 沒有載入，未停導航";
            }
            catch (Exception ex)
            {
                return $"停 vnavmesh 失敗（{ex.GetType().Name}：{ex.Message}）";
            }
        }

        // 只停本插件這一輪送出的導航：別的插件或使用者換過的路（終點對不上）不碰。只在框架執行緒呼叫。
        private string StopOwnVnavmesh(int sequence, Vector3 destination)
        {
            try
            {
                var computing = _vnavMoveInProgress.InvokeFunc();
                var running = _vnavPathRunning.InvokeFunc();
                Vector3? pathEnd = null;
                if (running && _vnavPathWaypoints.InvokeFunc() is { Count: > 0 } waypoints)
                    pathEnd = waypoints[^1];
                var (cancel, stop) = PotNavigation.DecideOwnStop(sequence, destination, computing, running, pathEnd);
                if (cancel)
                    _vnavPathfindCancelAll.InvokeAction();
                if (stop)
                    _vnavPathStop.InvokeAction();
                var endText = pathEnd is { } e ? $"終點 ({e.X:0.0}, {e.Z:0.0})" : "讀不到終點";
                return $"vnavmesh 算路中={computing}、在走={running}（{endText}；本插件第 {sequence} 次送的目的地 ({destination.X:0.0}, {destination.Z:0.0})）→ "
                    + $"{(cancel ? "已送 Nav.PathfindCancelAll" : "不取消尋路")}、{(stop ? "已送 Path.Stop" : running ? "在走的不是本插件的路、不停" : "沒有在走的路")}";
            }
            catch (IpcNotReadyError)
            {
                return "vnavmesh 沒有載入，未停導航";
            }
            catch (Exception ex)
            {
                return $"停 vnavmesh 失敗（{ex.GetType().Name}：{ex.Message}）";
            }
        }

        // IPC 端點，跑在呼叫端的執行緒：只讀快照與時鐘，不鎖、不轉派、不碰遊戲狀態。
        // 截止時間在這裡判，框架更新就算停了也不會一直回 true。
        private bool IsPotHuntActiveForIpc() =>
            PotHuntActivity.IsActiveAt(Volatile.Read(ref _potHuntActiveUntil), Environment.TickCount64);

        private void PublishPotHunt() => Volatile.Write(ref _potHuntActiveUntil, _potHunt.ActiveUntilMs);

        private void RefreshPotHunt()
        {
            if (!_potHunt.HasRound)
                return;

            var reason = _potHunt.Tick(Environment.TickCount64, DalamudApi.ClientState.TerritoryType,
                EurekaHelper.Config.TreasureHuntAutoMoveFlag, DalamudApi.ClientState.IsLoggedIn);
            if (reason is { } ended)
                LogPotHuntEnd(ended);
            PublishPotHunt();
        }

        private void NotePotHuntHint()
        {
            RefreshPotHunt();
            var territoryId = DalamudApi.ClientState.TerritoryType;
            var result = _potHunt.OnHint(Environment.TickCount64, territoryId, EurekaHelper.Config.TreasureHuntAutoMoveFlag);
            PublishPotHunt();
            if (result == PotHuntHintResult.Started)
                DalamudApi.Log.Information($"[TreasureHuntManager] 魔法甕尋寶進行中：開始（IPC {PotHuntActiveIpcName}＝true），地區 {territoryId}；{PotHuntActivity.IdleTimeoutMs / 1000} 秒沒有新提示或整輪滿 {PotHuntActivity.MaxDurationMs / 1000} 秒自動結束");
        }

        private void EndPotHunt(PotHuntEndReason reason)
        {
            if (_potHunt.End())
                LogPotHuntEnd(reason);
            PublishPotHunt();
        }

        private void LogPotHuntEnd(PotHuntEndReason reason)
        {
            var seconds = (Environment.TickCount64 - _potHunt.StartedAtMs) / 1000.0;
            DalamudApi.Log.Information($"[TreasureHuntManager] 魔法甕尋寶進行中：結束（IPC {PotHuntActiveIpcName}＝false），原因：{PotHuntEndReasonText(reason)}；歷時 {seconds:0.0} 秒、{_potHunt.HintCount} 則提示");
        }

        private static string PotHuntEndReasonText(PotHuntEndReason reason) => reason switch
        {
            PotHuntEndReason.Found => "挖到財寶",
            PotHuntEndReason.Cleared => "按了清除",
            PotHuntEndReason.TerritoryChanged => "換區或離開新月島",
            PotHuntEndReason.Logout => "登出",
            PotHuntEndReason.AutoMoveOff => "「自動走向旗標」已關閉",
            PotHuntEndReason.Idle => $"{PotHuntActivity.IdleTimeoutMs / 1000} 秒沒有新提示",
            PotHuntEndReason.MaxDuration => $"整輪滿 {PotHuntActivity.MaxDurationMs / 1000} 秒（之後的提示不再算進行中，直到 {PotHuntActivity.IdleTimeoutMs / 1000} 秒沒有新提示、挖到、清除、換區或登出）",
            PotHuntEndReason.Unload => "插件卸載",
            PotHuntEndReason.EmergencyStop => "急停",
            _ => reason.ToString(),
        };

        private void OnLogout(int type, int code)
        {
            _moveDeferral.Reset();
            _navTarget.Clear();
            EndPotHunt(PotHuntEndReason.Logout);
            _mappyOverlay.ClearCandidates();
        }

        // 出錯不能擋到同一幀的其他處理（同一插件的 Framework.Update 共用一個 try/catch），只記第一次。
        private void TickMappyOverlay()
        {
            try
            {
                _mappyOverlay.Tick(Environment.TickCount64, EurekaHelper.Config.TreasureHuntShowOnMappy);
            }
            catch (Exception ex)
            {
                if (_mappyFailureReported)
                    return;
                _mappyFailureReported = true;
                DalamudApi.Log.Warning(ex, "[TreasureHuntManager] 在 Mappy 顯示尋寶候選點時出錯；之後的錯誤不再記");
            }
        }

        // Continuously checks whether the player has walked within HistoricalProximityRadius of
        // a past TreasureFoundRecord and keeps a solid green "dig here" marker drawn at that spot
        // - cleared again once they walk away. Only runs while a hunt is actually active (has at
        // least one collected hint) - outside of that this is just noise, not something worth
        // drawing over every historical dig site the player happens to walk past.
        private void OnFrameworkUpdate(IFramework framework)
        {
            var externalStop = Interlocked.Exchange(ref _pendingStop, null);
            if (externalStop != null)
                StopAutomation(externalStop);

            RefreshPotHunt();
            TickMappyOverlay();
            TickAutoMove();

            if (_hints.Count == 0)
            {
                if (_nearbyHistoricalRecord != null)
                {
                    _nearbyHistoricalRecord = null;
                    Splatoon.RemoveDynamicElements(HistoryMarkerLayerName);
                }
                return;
            }

            var player = DalamudApi.ObjectTable.LocalPlayer;
            if (player == null)
                return;

            var territoryId = DalamudApi.ClientState.TerritoryType;
            var playerPos2D = new Vector2(player.Position.X, player.Position.Z);

            TreasureFoundRecord nearest = null;
            var nearestDistance = float.MaxValue;
            foreach (var record in EurekaHelper.Config.TreasureHuntHistory)
            {
                if (record.TerritoryId != territoryId)
                    continue;

                var histPos2D = new Vector2(record.FoundPosition.X, record.FoundPosition.Z);
                var distance = Vector2.Distance(playerPos2D, histPos2D);
                if (distance <= HistoricalProximityRadius && distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = record;
                }
            }

            if (ReferenceEquals(nearest, _nearbyHistoricalRecord))
                return;

            _nearbyHistoricalRecord = nearest;

            if (!Splatoon.IsConnected())
                return;

            Splatoon.RemoveDynamicElements(HistoryMarkerLayerName);

            if (nearest == null)
                return;

            var marker = new Element(ElementType.CircleAtFixedCoordinates)
            {
                refX = nearest.FoundPosition.X,
                refY = nearest.FoundPosition.Z,
                refZ = nearest.FoundPosition.Y,
                radius = 1f,
                color = 0xFF00FF00u, // solid green
                Filled = true,
                thicc = 2f,
                Enabled = true,
            };

            try
            {
                Splatoon.AddDynamicElements(HistoryMarkerLayerName, new[] { marker }, -2);
            }
            catch (Exception ex)
            {
                DalamudApi.Log.Error(ex, "[TreasureHuntManager] AddDynamicElements (history marker) failed");
            }
        }

        // 離開地區後指引財寶的效果就沒了，殘留的「第二處財寶」不能帶到下一次進來。
        private void OnTerritoryChanged(ushort territoryId)
        {
            _secondTreasurePending = false;
            _moveDeferral.Reset();
            _navTarget.Clear();
            EndPotHunt(PotHuntEndReason.TerritoryChanged);
            _mappyOverlay.ClearCandidates();
        }

        private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            var text = message.TextValue;

            if (FoundRegex.IsMatch(text))
            {
                _secondTreasurePending = false;
                OnTreasureFound();
                return;
            }

            if (SecondTreasureRegex.IsMatch(text))
            {
                if (PotTreasureCandidates.HasTable(DalamudApi.ClientState.TerritoryType))
                {
                    _secondTreasurePending = true;
                    DalamudApi.Log.Information("[TreasureHuntManager] 收到「第二處財寶」訊息：下一輪提示改用第二處財寶的候選點");
                }
                return;
            }

            var match = HintRegex.Match(text);
            if (!match.Success)
                return;

            var direction = match.Groups["dir"].Value;
            var tier = match.Groups["tier"].Value.Trim();

            if (!DirectionAngles.TryGetValue(direction, out var angleDeg))
                return;

            var player = DalamudApi.ObjectTable.LocalPlayer;
            if (player == null)
                return;

            if (!PotTreasureCandidates.TryGetTierBand(tier, out var tierRange))
            {
                DalamudApi.Log.Warning($"[TreasureHuntManager] Unrecognized distance tier text \"{tier}\" - using fallback range.");
                tierRange = UnknownTierFallback;
            }

            if (_hints.Count == 0)
                BeginCandidateHunt(new Vector2(player.Position.X, player.Position.Z));

            _hints.Add(new TreasureHint
            {
                Timestamp = DateTime.Now,
                Origin = player.Position,
                DirectionText = direction,
                TierText = tier,
                AngleDegrees = angleDeg,
                MinDistance = tierRange.Min,
                MaxDistance = tierRange.Max,
            });

            // 先發布進行中再 Draw：Draw 會送走向旗標，對方下一幀就要看得到 true。
            NotePotHuntHint();
            Draw();
        }

        // 挖到寶藏時，把「找到當下的玩家座標」連同這一輪收集到的完整提示鏈存進歷史紀錄
        // （Configuration.TreasureHuntHistory，跨 session 持久化），供之後回頭比對每個距離等級
        // 的碼數區間準不準。找到後這一輪尋寶結束，順便清空目前的提示鏈跟畫面標記。
        private void OnTreasureFound()
        {
            EndPotHunt(PotHuntEndReason.Found);

            var player = DalamudApi.ObjectTable.LocalPlayer;
            if (player == null || _hints.Count == 0)
            {
                ClearRound();
                return;
            }

            EurekaHelper.Config.TreasureHuntHistory.Add(new TreasureFoundRecord
            {
                Timestamp = DateTime.Now,
                TerritoryId = DalamudApi.ClientState.TerritoryType,
                FoundPosition = player.Position,
                Hints = new List<TreasureHint>(_hints),
            });
            EurekaHelper.Config.Save();

            ClearRound();
        }

        public void ClearHistory()
        {
            EurekaHelper.Config.TreasureHuntHistory.Clear();
            EurekaHelper.Config.Save();
        }

        public void DeleteHistoryRecord(TreasureFoundRecord record)
        {
            EurekaHelper.Config.TreasureHuntHistory.Remove(record);
            EurekaHelper.Config.Save();
        }

        // Manual correction for a record whose FoundPosition was recorded wrong (e.g. the player
        // moved a bit before the "發現了財寶" message fired) - overwrites it with wherever the
        // player is currently standing.
        public void RepositionHistoryRecord(TreasureFoundRecord record, Vector3 newPosition)
        {
            record.FoundPosition = newPosition;
            EurekaHelper.Config.Save();
        }

        // 8 方位角（0°=正北，順時針）換算成水平方向單位向量。FFXIV 世界座標中正北對應 -Z、
        // 正東對應 +X（與地圖畫面「上方為北」的慣例一致）— 這個假設尚待實機驗證，若範圍圈方向
        // 明顯錯誤，優先檢查這裡。
        private static Vector2 DirectionToVector(float angleDegrees)
        {
            var rad = angleDegrees * MathF.PI / 180f;
            return new Vector2(MathF.Sin(rad), -MathF.Cos(rad));
        }

        // 扇形涵蓋的半角 - 8 方位系統裡每個方位詞代表 45° 的扇區，所以真實方位可能落在提示方位
        // 兩側各 22.5° 內，剛好對應這個扇區的解析度。
        private const float FanHalfAngleDegrees = 22.5f;

        // 與魔法甕候選點同一套比對：不帶寬限時，挖到點常被自己那一輪的提示刷掉。
        private static bool MatchesHint(Vector2 point, TreasureHint hint) =>
            PotTreasureCandidates.Matches(point, ToPotHint(hint));

        private static PotHint ToPotHint(TreasureHint hint) =>
            new(new Vector2(hint.Origin.X, hint.Origin.Z), hint.AngleDegrees, hint.TierText);

        // 從「該次提示當下」座標出發、朝提示方位延伸的扇形（兩條邊線），長度跟著該次提示的距離等級縮短 -
        // 直接呈現「往哪個方向走、大概還要多遠」。刻意不畫以三角交會估算點為中心的圓圈：交會點在提示方位太接近時
        // 很不穩定，範圍圈又大到看不出實際指向。每次按胡蘿蔔都會疊加畫一個新的扇形，不會把前幾次的扇形擦掉，
        // 這樣可以直接在畫面上看到整個尋寶過程的方位是怎麼逐漸縮小的。
        private void Draw()
        {
            if (_hints.Count == 0)
                return;

            var latest = _hints[^1];

            // 順便更新 EstimatedPosition/EstimatedRadius，供 UI 分頁跟地圖旗標使用 - 沿最新一次
            // 提示的方位角走到距離等級的下限（不取中點）。玩家會拿著胡蘿蔔一路收集提示，隨著
            // 距離等級縮小標記點自然會跟著往寶藏逼近；取中點反而會讓標記點在提示還很粗略時就跳到
            // 一個沒有實際意義的位置，取下限則保證「至少要走到這裡才可能是」，跟著提示鏈逐步逼近。
            var latestOrigin2D = new Vector2(latest.Origin.X, latest.Origin.Z);
            var latestDir = DirectionToVector(latest.AngleDegrees);
            var latestRenderMax = MathF.Min(latest.MaxDistance, FarTierRenderCap);
            EstimatedPosition = latestOrigin2D + latestDir * latest.MinDistance;
            EstimatedRadius = latestRenderMax - latest.MinDistance;

            // float.MaxValue 代表這個距離等級沒有上限（目前只有「很遠」）。
            EstimateUncertainty = float.IsPositiveInfinity(latest.MaxDistance) || latest.MaxDistance >= float.MaxValue
                ? null
                : latest.MaxDistance - latest.MinDistance;

            // A past TreasureFoundRecord (same territory) that falls within EVERY collected
            // hint's fan (direction ± half-angle, distance within that hint's tier range) is a
            // much stronger signal than the raw bearing math - it's an actual confirmed spot that
            // still fits everything we've been told this round, so prioritize suggesting it over
            // the geometric estimate.
            var territoryId = DalamudApi.ClientState.TerritoryType;
            TreasureFoundRecord matchedRecord = null;
            foreach (var record in EurekaHelper.Config.TreasureHuntHistory)
            {
                if (record.TerritoryId != territoryId)
                    continue;

                var histPos2D = new Vector2(record.FoundPosition.X, record.FoundPosition.Z);
                if (_hints.All(h => MatchesHint(histPos2D, h)))
                {
                    matchedRecord = record;
                    break;
                }
            }

            IsUsingHistoricalPosition = false;

            if (matchedRecord != null)
            {
                EstimatedPosition = new Vector2(matchedRecord.FoundPosition.X, matchedRecord.FoundPosition.Z);
                EstimatedRadius = 1f;
                // 歷史挖到點是實測座標，不確定性接近 0（不是「未知」）。
                EstimateUncertainty = 1f;
                IsUsingHistoricalPosition = true;
            }
            else
            {
                // Weaker fallback: nothing matched every hint's fan, but if the triangulated
                // estimate itself lands close to somewhere treasure was found before, still trust
                // that historical spot over the bearing math and snap to it.
                foreach (var record in EurekaHelper.Config.TreasureHuntHistory)
                {
                    if (record.TerritoryId != territoryId)
                        continue;

                    var histPos2D = new Vector2(record.FoundPosition.X, record.FoundPosition.Z);
                    if (Vector2.Distance(EstimatedPosition.Value, histPos2D) > HistoricalPredictionMatchRadius)
                        continue;

                    EstimatedPosition = histPos2D;
                    EstimatedRadius = 1f;
                    IsUsingHistoricalPosition = true;
                    break;
                }
            }

            UpdateCandidates(territoryId);
            _mappyOverlay.SetCandidates(territoryId, _matchingCandidates, FlagCandidate);
            DrawOverlay();

            // 旗標與自動移動不靠 Splatoon：沒連線時只少了疊層。
            SetMapFlag(FlagCandidate?.Position2D ?? EstimatedPosition.Value);
        }

        private void DrawOverlay()
        {
            if (!Splatoon.IsConnected())
                return;

            Splatoon.RemoveDynamicElements(LayerName);

            // ECommons.SplatoonAPI.Element.SetRefCoord/SetOffCoord(Vector3) - the library's own helpers for fixed-coordinate
            // elements - map refX=v.X,refY=v.Z,refZ=v.Y and the same for off*. Two Line elements (not a Cone) deliberately
            // sidestep Splatoon's cone angle/rotation reference convention, which isn't documented and would need live
            // trial and error to get right - two lines from known XYZ points carry no such risk.
            var elements = new List<Element>(_hints.Count * 2);
            for (var hintIndex = 0; hintIndex < _hints.Count; hintIndex++)
            {
                var hint = _hints[hintIndex];

                // 每按一次胡蘿蔔就多疊一個扇形；全部同色同線粗的話，疊到三四次之後看不出「哪一條是最新的」——
                // 最新那次提示才是現在該走的方向。所以**同一個色相只調透明度／線粗**（不新增顏色，避免疊色
                // 造成視覺困擾）：最新一次最亮最粗，越舊越淡越細，但仍保留得看得見，因為整串
                // 的收斂過程本身就是有用的資訊。
                var isLatest = hintIndex == _hints.Count - 1;
                var age = _hints.Count - 1 - hintIndex;
                var alpha = isLatest ? 0xC0u : (uint)Math.Max(0x28, 0x80 - (age * 0x18));
                var color = (alpha << 24) | 0x0000FFu; // 0xAABBGGRR - 紅色，只有 alpha 在變
                var thickness = isLatest ? 4f : 2f;

                var origin2D = new Vector2(hint.Origin.X, hint.Origin.Z);
                var leftDir = DirectionToVector(hint.AngleDegrees - FanHalfAngleDegrees);
                var rightDir = DirectionToVector(hint.AngleDegrees + FanHalfAngleDegrees);
                var length = MathF.Max(MathF.Min(hint.MaxDistance, FarTierRenderCap), 3f);
                var leftEdge = origin2D + leftDir * length;
                var rightEdge = origin2D + rightDir * length;

                elements.Add(new Element(ElementType.LineBetweenTwoFixedCoordinates)
                {
                    refX = origin2D.X,
                    refY = origin2D.Y,
                    refZ = hint.Origin.Y,
                    offX = leftEdge.X,
                    offY = leftEdge.Y,
                    offZ = hint.Origin.Y,
                    color = color,
                    thicc = thickness,
                    Enabled = true,
                });
                elements.Add(new Element(ElementType.LineBetweenTwoFixedCoordinates)
                {
                    refX = origin2D.X,
                    refY = origin2D.Y,
                    refZ = hint.Origin.Y,
                    offX = rightEdge.X,
                    offY = rightEdge.Y,
                    offZ = hint.Origin.Y,
                    color = color,
                    thicc = thickness,
                    Enabled = true,
                });
            }

            AddCandidateElements(elements);

            try
            {
                Splatoon.AddDynamicElements(LayerName, elements.ToArray(), -2); // -2 = 不自動過期，生命週期由本類別管理
            }
            catch (Exception ex)
            {
                DalamudApi.Log.Error(ex, "[TreasureHuntManager] AddDynamicElements failed");
            }
        }

        // 每輪第一則提示時決定這一輪用哪一組候選點，整輪不變。
        private void BeginCandidateHunt(Vector2 firstOrigin)
        {
            var territoryId = DalamudApi.ClientState.TerritoryType;
            PreferredPotFateId = PotTreasureCandidates.InferFateId(territoryId, firstOrigin);

            // 在魔法甕 F.A.T.E. 旁開始的一定是剛打完那場的第一處財寶，殘留的第二處旗標不算數。
            CandidatePool = _secondTreasurePending && PreferredPotFateId == null
                ? PotCandidatePool.SecondChance
                : PotCandidatePool.Primary;
            _secondTreasurePending = false;

            if (PotTreasureCandidates.HasTable(territoryId))
                DalamudApi.Log.Information($"[TreasureHuntManager] 新一輪魔法甕尋寶：候選組={CandidatePool} 優先={PreferredPotFateId?.ToString() ?? "無法判斷"}");
        }

        // 每則提示後重算：候選點必須符合這一輪「全部」提示，沒有任何一點符合就退回估計位置。
        private void UpdateCandidates(ushort territoryId)
        {
            _matchingCandidates = new List<PotCandidateState>();
            FlagCandidate = null;
            IsFlagCandidateConfirmed = false;
            IsCandidateFallback = false;

            var pool = PotTreasureCandidates.GetPool(territoryId, CandidatePool);
            CandidatePoolSize = pool.Count;
            if (pool.Count == 0 || _hints.Count == 0)
                return;

            var hints = new List<PotHint>(_hints.Count);
            foreach (var hint in _hints)
                hints.Add(ToPotHint(hint));

            var matching = PotTreasureCandidates.Filter(pool, hints);
            if (matching.Count == 0)
            {
                IsCandidateFallback = true;
                DalamudApi.Log.Information($"[TreasureHuntManager] 魔法甕候選：{pool.Count} 點都不符合這一輪 {hints.Count} 則提示，旗標退回估計位置");
                return;
            }

            var foundHere = new List<Vector2>();
            foreach (var record in EurekaHelper.Config.TreasureHuntHistory)
            {
                if (record.TerritoryId == territoryId)
                    foundHere.Add(new Vector2(record.FoundPosition.X, record.FoundPosition.Z));
            }

            foreach (var candidate in matching)
            {
                var confirmed = matching.Count == 1 || PotTreasureCandidates.IsConfirmedByHistory(candidate, foundHere);
                _matchingCandidates.Add(new PotCandidateState(candidate, confirmed));
            }

            var latest = _hints[^1];
            FlagCandidate = PotTreasureCandidates.PickTarget(matching, new Vector2(latest.Origin.X, latest.Origin.Z), PreferredPotFateId);
            if (FlagCandidate is { } target)
            {
                IsFlagCandidateConfirmed = _matchingCandidates.Exists(s => s.Candidate == target && s.Confirmed);
                DalamudApi.Log.Information($"[TreasureHuntManager] 魔法甕候選：第 {hints.Count} 則提示後符合 {matching.Count}/{pool.Count}，旗標→{target.FateId}#{target.Number} 已確認={IsFlagCandidateConfirmed}");
            }
        }

        // 照 NecroLens：只畫外框不填色；兩態同色相，可能有＝淡、已確認＝實。旗標目標多一條連到玩家
        // 的線指出方向（Splatoon tether）。
        private void AddCandidateElements(List<Element> elements)
        {
            foreach (var state in _matchingCandidates)
            {
                var position = state.Candidate.Position;
                var isTarget = FlagCandidate is { } target && target == state.Candidate;
                elements.Add(new Element(ElementType.CircleAtFixedCoordinates)
                {
                    refX = position.X,
                    refY = position.Z,
                    refZ = position.Y,
                    radius = CandidateCircleRadius,
                    color = PotCandidateColor(state.Confirmed),
                    Filled = false,
                    thicc = state.Confirmed ? 3f : 1.5f,
                    tether = isTarget,
                    Enabled = true,
                });
            }
        }

        // 0xAABBGGRR，金黃色（RGB FFD700）；alpha 與 HazardColors 的兩態一致。
        private static uint PotCandidateColor(bool confirmed) => ((confirmed ? 0xE0u : 0x60u) << 24) | 0x00D7FFu;

        // 每算出新的推算位置就自動更新遊戲地圖上的旗標，不用玩家手動點擊 - 這樣打開地圖就能
        // 直接看到目前的推算位置，跟 Splatoon 範圍圈同步更新。
        private void SetMapFlag(Vector2 worldPosXZ)
        {
            var territoryId = DalamudApi.ClientState.TerritoryType;
            var territoryType = DalamudApi.DataManager.GetExcelSheet<TerritoryType>()!.GetRowOrDefault(territoryId);
            if (territoryType == null)
                return;

            var mapPos = MapUtil.WorldToMap(worldPosXZ, territoryType.Value.Map.Value);
            Utils.SetFlagMarker(territoryId, (ushort)territoryType.Value.Map.RowId, mapPos);

            // vnavmesh 外掛的指令，讓角色自動走向剛設置好的地圖旗標。玩家沒裝 vnavmesh 的話這行
            // 指令會被遊戲當成無效指令擋掉，不會發生任何事。
            if (EurekaHelper.Config.TreasureHuntAutoMoveFlag)
                RequestAutoMove(worldPosXZ);
        }

        // 使用者在操作時不送：vnavmesh 會在下一幀被他的輸入取消，連送只是跟他搶。停手後由 TickAutoMove 補送。
        private void RequestAutoMove(Vector2 flagTarget)
        {
            var now = Environment.TickCount64;
            if (_moveDeferral.OnRequest(ReadHandsOn(now)))
            {
                SendAutoMove(flagTarget, $"自動（第 {_hints.Count} 則尋寶提示後，目標={DescribeFlagTarget()}）");
                return;
            }

            if (now < _nextDeferLogAt)
                return;
            _nextDeferLogAt = now + DeferLogIntervalMs;
            DalamudApi.Log.Information($"[TreasureHuntManager] 使用者正在操作（按著移動鍵／滑鼠左右鍵／左搖桿，或放開未滿 {HandsOnLatch.LatchMs / 1000} 秒）：第 {_hints.Count} 則尋寶提示先不送自動走向，停手後送當下最新的目的地（這行最多每 {DeferLogIntervalMs / 1000} 秒一次）");
        }

        // 每幀：記下使用者有沒有在操作；有待送的自動走向而且已停手，就送當下最新的目的地。
        private void TickAutoMove()
        {
            if (!EurekaHelper.Config.TreasureHuntAutoMoveFlag)
            {
                _moveDeferral.Reset();
                return;
            }

            var handsOn = ReadHandsOn(Environment.TickCount64);
            if (!_moveDeferral.Tick(handsOn) || _hints.Count == 0 || EstimatedPosition is not { } estimate)
                return;

            SendAutoMove(FlagCandidate?.Position2D ?? estimate, $"自動（使用者停手後補送第 {_hints.Count} 則尋寶提示的目的地，目標={DescribeFlagTarget()}）");
        }

        // 只讀 Dalamud 已解析的按鍵／手把狀態、當幀讀。讀不到就當沒按並且之後不再讀＝與沒有這個判斷時相同。
        // 自訂按鍵綁定讀不到：那時只剩 vnavmesh 自己的輸入取消。
        private bool ReadHandsOn(long now)
        {
            if (!_handsOnBroken)
            {
                try
                {
                    _handsOn.Note(MoveInputPressed(), now);
                }
                catch (Exception ex)
                {
                    _handsOnBroken = true;
                    DalamudApi.Log.Information($"[TreasureHuntManager] 讀不到按鍵／手把狀態（{ex.GetType().Name}：{ex.Message}）：之後自動走向不再等使用者停手");
                }
            }

            return _handsOn.Active(now);
        }

        private bool MoveInputPressed()
        {
            var state = DalamudApi.KeyState;
            if (_moveKeys == null)
            {
                var valid = new List<VirtualKey>(MoveKeyCandidates.Length);
                foreach (var key in MoveKeyCandidates)
                {
                    if (state.IsVirtualKeyValid(key))
                        valid.Add(key);
                }

                _moveKeys = valid.ToArray();
                _mouseKeysValid = state.IsVirtualKeyValid(VirtualKey.LBUTTON) && state.IsVirtualKeyValid(VirtualKey.RBUTTON);
            }

            foreach (var key in _moveKeys)
            {
                if (state[key])
                    return true;
            }

            if (_mouseKeysValid && state[VirtualKey.LBUTTON] && state[VirtualKey.RBUTTON])
                return true;

            var stick = DalamudApi.GamepadState.LeftStick;
            return Math.Abs(stick.X) >= StickThreshold || Math.Abs(stick.Y) >= StickThreshold;
        }

        // 旗標目標對得上這一輪的候選點就走它的 3D 座標：地圖旗標只有平面，vnavmesh 自己定的高度可能落在走不到的樓層。
        // 對不上或 IPC 失敗照舊送 /vnav moveflag。用 IPC 不用 "/vnav moveto"：失敗時拿得到例外、才能退回旗標。
        private void SendAutoMove(Vector2 flagTarget, string source)
        {
            string flatReason;
            if (PotNavigation.TryResolveCandidate(DalamudApi.ClientState.TerritoryType, CandidatePool, flagTarget, out var candidate, out var distance))
            {
                var label = $"{candidate.FateId}#{candidate.Number}";
                var p = candidate.Position;
                if (TryVnavMoveTo(p, out var failure))
                {
                    var sequence = _navTarget.Publish(p, true);
                    DalamudApi.Log.Information($"[TreasureHuntManager] 送出 vnavmesh IPC SimpleMove.PathfindAndMoveTo：來源={source}；目的地=3D 候選點 {label} 世界 ({p.X:0.0}, {p.Y:0.0}, {p.Z:0.0})，離旗標目標 {distance:0.0} 米；第 {sequence} 次自動走向");
                    return;
                }

                flatReason = $"候選點 {label} 對得上（離旗標目標 {distance:0.0} 米），但 vnavmesh IPC 失敗：{failure}";
            }
            else if (distance < float.MaxValue)
            {
                flatReason = $"最近的候選點 {candidate.FateId}#{candidate.Number} 離旗標目標 {distance:0.0} 米，超過 {PotNavigation.CandidateSnapRadius:0} 米";
            }
            else
            {
                flatReason = "這裡沒有候選點表";
            }

            var flatSequence = _navTarget.Publish(new Vector3(flagTarget.X, 0f, flagTarget.Y), false);
            SendMoveFlag(source, $"{flatReason}；第 {flatSequence} 次自動走向");
        }

        // 不飛，與 /vnav moveflag 相同。
        private bool TryVnavMoveTo(Vector3 destination, out string failure)
        {
            try
            {
                if (_vnavMoveTo.InvokeFunc(destination, false))
                {
                    failure = null;
                    return true;
                }

                failure = "vnavmesh 回 false";
            }
            catch (IpcNotReadyError)
            {
                failure = "vnavmesh 沒有載入（IpcNotReadyError）";
            }
            catch (Exception ex)
            {
                failure = $"{ex.GetType().Name}：{ex.Message}";
            }

            return false;
        }

        private string DescribeFlagTarget()
        {
            if (FlagCandidate is { } target)
                return $"魔法甕候選點 {target.FateId}#{target.Number}（{(IsFlagCandidateConfirmed ? "已確認" : "可能")}）";
            if (IsUsingHistoricalPosition)
                return "以前挖到過的位置";
            return IsCandidateFallback ? "估計位置（沒有候選點符合）" : "估計位置";
        }

        // 本插件送 "/vnav moveflag" 一律走這裡，每次送出記一行 Information（來源＋目的地），
        // log 才分得出是誰讓角色動起來。只在框架執行緒呼叫（聊天事件、UI 按鈕）。
        public static void SendMoveFlag(string source, string flatReason = null)
        {
            var navNote = flatReason == null ? string.Empty : $"；導航=平面旗標（{flatReason}）";
            DalamudApi.Log.Information($"[TreasureHuntManager] 送出 /vnav moveflag：來源={source}；目的地={DescribeGameFlag()}{navNote}");
            Utils.SendMessage("/vnav moveflag");
        }

        // vnavmesh 走向的是遊戲的地圖旗標（AgentMap 第一個旗標），所以記它，不記我們以為的目標。
        private static unsafe string DescribeGameFlag()
        {
            var map = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMap.Instance();
            if (map == null || map->FlagMarkerCount == 0)
                return "目前沒有地圖旗標（vnavmesh 不會移動）";

            var marker = map->FlagMapMarkers[0];
            var world = new Vector2(marker.XFloat, marker.YFloat);
            var mapText = string.Empty;
            if (DalamudApi.DataManager.GetExcelSheet<Map>()!.GetRowOrDefault(marker.MapId) is { } mapRow)
            {
                var mapPos = MapUtil.WorldToMap(world, mapRow);
                mapText = $"地圖 X: {mapPos.X:0.0}, Y: {mapPos.Y:0.0}，";
            }

            return $"{mapText}世界 ({world.X:0.0}, {world.Y:0.0})，地區 {marker.TerritoryId}";
        }

        // 玩家按「清除」（UI，框架執行緒）：先停本插件這一輪送出的 vnavmesh 導航，再清提示鏈。
        public void Clear()
        {
            var sent = _navTarget.Read();
            if (sent.Sequence != 0)
                DalamudApi.Log.Information($"[TreasureHuntManager] 按了清除：{StopOwnVnavmesh(sent.Sequence, sent.Destination)}");
            ClearRound();
        }

        // 唯二會清掉目前這輪提示鏈跟畫面上扇形的地方：玩家自己按「清除」，或 OnTreasureFound 挖到寶藏時。
        // 刻意不會因為換區、閒置太久等情況自動清掉 - 扇形範圍通常要花好幾分鐘才走得到，
        // 自動清掉只會讓畫面上的參考線消失卻沒有新的可看。
        private void ClearRound()
        {
            EndPotHunt(PotHuntEndReason.Cleared);
            _moveDeferral.Reset();
            _navTarget.Clear();
            _hints.Clear();
            EstimatedPosition = null;
            EstimatedRadius = 0f;
            EstimateUncertainty = null;
            IsUsingHistoricalPosition = false;
            _nearbyHistoricalRecord = null;
            _matchingCandidates = new();
            CandidatePoolSize = 0;
            CandidatePool = PotCandidatePool.Primary;
            PreferredPotFateId = null;
            FlagCandidate = null;
            IsFlagCandidateConfirmed = false;
            IsCandidateFallback = false;
            _mappyOverlay.ClearCandidates();
            Splatoon.RemoveDynamicElements(LayerName);
            Splatoon.RemoveDynamicElements(HistoryMarkerLayerName);
        }

        public void Dispose()
        {
            _stopAllIpc.UnregisterAction();
            DalamudApi.ChatGui.ChatMessage -= OnChatMessage;
            DalamudApi.ClientState.TerritoryChanged -= OnTerritoryChanged;
            DalamudApi.ClientState.Logout -= OnLogout;
            DalamudApi.Framework.Update -= OnFrameworkUpdate;
            _mappyOverlay.Shutdown(Environment.TickCount64);
            EndPotHunt(PotHuntEndReason.Unload);
            _potHuntActiveIpc.UnregisterFunc();
            _navTargetIpc.UnregisterFunc();
            _navTarget.Clear();
            SplatoonCleanup.RemoveElements(LayerName);
            SplatoonCleanup.RemoveElements(HistoryMarkerLayerName);
        }

        // Manual retry for when Splatoon connection failed to establish on plugin load (e.g. the
        // "Splatoon.Loaded" IPC broadcast happened before EurekaHelper subscribed, since Splatoon
        // was already running). Re-running ECommonsMain.Init immediately re-checks whether
        // Splatoon is currently loaded and, if so, connects right away instead of waiting for
        // Splatoon's own next load/reload broadcast.
        public void RetryConnection()
        {
            ECommonsMain.Init(DalamudApi.PluginInterface, EurekaHelper.Plugin, Module.SplatoonAPI);
        }
    }

    public class TreasureHint
    {
        public DateTime Timestamp { get; set; }
        public Vector3 Origin { get; set; }
        public string DirectionText { get; set; } = string.Empty;
        public string TierText { get; set; } = string.Empty;
        public float AngleDegrees { get; set; }
        public float MinDistance { get; set; }
        public float MaxDistance { get; set; }
    }

    // 一次完整尋寶的結果：實際挖到寶藏的座標，加上這一輪收集到的完整提示鏈。持久化在
    // Configuration.TreasureHuntHistory 裡，供之後回頭比對距離帶（PotTreasureCandidates）
    // 的碼數區間是否準確（例如：提示說「很近」時實際距離最終挖到的座標有多遠）。
    public class TreasureFoundRecord
    {
        public DateTime Timestamp { get; set; }
        public ushort TerritoryId { get; set; }
        public Vector3 FoundPosition { get; set; }
        public List<TreasureHint> Hints { get; set; } = new();
    }
}
