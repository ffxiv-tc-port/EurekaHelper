using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
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

        // 距離等級 -> 概略碼數區間（min, max）。原始數值取自玩家社群經驗分級，但比對
        // TreasureHuntHistory 跟實際挖到座標的距離後發現不準，動過三次：
        // 1. 第一版全部「打對折」下修 - 近距離（很近/不遠）修對了方向，但遠距離修過頭。
        // 2. 之後新增一筆乾淨的單目標連續10次提示資料（距離單調遞減：491.9→...→225.0 時是
        //    「很遠」、171.2→101.2 是「稍遠」、40.3 是「不遠」、9.0 是「很近」），顯示遠距離
        //    等級的真實碼數遠比對折後的數字大很多 - 距離等級不是線性縮放，遠距離區間本來就該
        //    寬很多。這版近距離沿用對折後的數字（有跨紀錄驗證），遠距離改用這筆乾淨資料校正。
        // 3. 累積到 28 筆完整紀錄後發現「很遠」的 500 碼上限還是抓太窄 - 實際樣本跨 199~1293
        //    碼，而且是隨玩家靠近單調遞減到 500 以下才轉「稍遠」，代表 500 只是碰巧沒收集到更遠
        //    的資料，不是真正的上限。改成無上限（用 float.MaxValue），交會/歷史比對（MatchesHint）
        //    不再誤判超過 500 碼的「很遠」提示為不符合。畫面呈現用的長度/半徑另外夾在
        //    FarTierRenderCap，避免對 Splatoon 疊層丟出無限長的線段。
        // 「就在這附近」拿掉了 - 從沒收集到樣本驗證過這個字串／區間，抓錯字或亂猜區間風險比留著
        // 高。沒收錄的話會落到 UnknownTierFallback，跟其他未知提示文字一樣處理。
        private static readonly Dictionary<string, (float Min, float Max)> DistanceTiers = new()
        {
            ["很遠"] = (200f, float.MaxValue),
            ["稍遠"] = (100f, 200f),
            ["不遠"] = (40f, 100f),
            ["很近"] = (10f, 40f),
        };

        private static readonly (float Min, float Max) UnknownTierFallback = (40f, 200f);

        // 「很遠」現在 Max 是 float.MaxValue（無上限，見上方 DistanceTiers 註解），僅用於
        // MatchesHint 的距離比對。畫扇形線段/更新 EstimatedPosition、EstimatedRadius 時若直接
        // 拿 MaxDistance 來用會得到無限長的線或 NaN 中點，所以另外夾這個視覺呈現用的碼數上限。
        private const float FarTierRenderCap = 500f;

        private static readonly Regex HintRegex =
            new(@"財寶好像是在(?<dir>正東|正南|正西|正北|東北|東南|西南|西北)方向(?<tier>[^的]+?)的地方", RegexOptions.Compiled);

        // 挖到寶藏時的系統訊息，例如"發現了財寶！！"。找到的當下座標最準確 - 拿來跟這一輪收集
        // 到的提示鏈一起存進歷史，之後可以回頭校正 DistanceTiers 的碼數區間準不準。
        private static readonly Regex FoundRegex = new(@"發現了財寶", RegexOptions.Compiled);

        public IReadOnlyList<TreasureHint> Hints => _hints;
        public IReadOnlyList<TreasureFoundRecord> History => EurekaHelper.Config.TreasureHuntHistory;
        public Vector2? EstimatedPosition { get; private set; }
        public float EstimatedRadius { get; private set; }

        // True when EstimatedPosition came from a past TreasureFoundRecord (strong fan-match or
        // proximity snap) rather than the raw bearing triangulation - lets the UI flag that the
        // suggested spot is a confirmed historical dig site, not just a geometric guess.
        public bool IsUsingHistoricalPosition { get; private set; }

        // 給 UI 顯示連線狀態用：Splatoon 疊層圓圈需要遊戲裡真的裝了「Splatoon」這個 Dalamud
        // 外掛且已連上 ECommons IPC，才會實際畫出來 - 沒裝的話這裡會一直是 false，但地圖旗標
        // （SetMapFlag）不受影響，仍會照常更新。
        //
        // NOTE: read straight from Splatoon.IsConnected() rather than a locally-tracked "ready"
        // flag set via Splatoon.SetOnConnect(). That callback is a single static slot
        // (ECommons.SplatoonAPI.Splatoon.OnConnect), not a multicast event - SplatoonManager also
        // calls SetOnConnect from its own constructor (when EnableSplatoonAggroRanges is on) and,
        // being constructed after this class, permanently overwrites whichever callback registered
        // first. That silently broke this tab's "connected" status forever (retry included, since
        // retry only re-runs Splatoon's own connect check - it doesn't restore this class's
        // callback), even though Splatoon.Instance was actually set correctly the whole time.
        public bool IsSplatoonReady => Splatoon.IsConnected();

        private readonly List<TreasureHint> _hints = new();
        private TreasureFoundRecord _nearbyHistoricalRecord;

        public TreasureHuntManager()
        {
            DalamudApi.Framework.Update += OnFrameworkUpdate;
            // ECommonsMain.Init/Dispose 是可重複呼叫的（內部有初始化計數），SplatoonManager
            // 也會各自呼叫一次；兩者互不影響。這裡刻意不在 Dispose() 呼叫 ECommonsMain.Dispose()，
            // 避免在 SplatoonManager 已停用（EnableSplatoonAggroRanges=false）時把另一個仍在用
            // 的 Splatoon 連線關掉、或造成重複釋放。
            ECommonsMain.Init(DalamudApi.PluginInterface, EurekaHelper.Plugin, Module.SplatoonAPI);

            DalamudApi.ChatGui.ChatMessage += OnChatMessage;
        }

        // Continuously checks whether the player has walked within HistoricalProximityRadius of
        // a past TreasureFoundRecord and keeps a solid green "dig here" marker drawn at that spot
        // - cleared again once they walk away. Only runs while a hunt is actually active (has at
        // least one collected hint) - outside of that this is just noise, not something worth
        // drawing over every historical dig site the player happens to walk past.
        private void OnFrameworkUpdate(IFramework framework)
        {
            if (_hints.Count == 0)
            {
                if (_nearbyHistoricalRecord != null)
                {
                    _nearbyHistoricalRecord = null;
                    Splatoon.RemoveDynamicElements(HistoryMarkerLayerName);
                }
                return;
            }

            var player = DalamudApi.ClientState.LocalPlayer;
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

        private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            var text = message.TextValue;

            if (FoundRegex.IsMatch(text))
            {
                OnTreasureFound();
                return;
            }

            var match = HintRegex.Match(text);
            if (!match.Success)
                return;

            var direction = match.Groups["dir"].Value;
            var tier = match.Groups["tier"].Value.Trim();

            if (!DirectionAngles.TryGetValue(direction, out var angleDeg))
                return;

            var player = DalamudApi.ClientState.LocalPlayer;
            if (player == null)
                return;

            if (!DistanceTiers.TryGetValue(tier, out var tierRange))
            {
                DalamudApi.Log.Warning($"[TreasureHuntManager] Unrecognized distance tier text \"{tier}\" - using fallback range.");
                tierRange = UnknownTierFallback;
            }

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

            Draw();
        }

        // 挖到寶藏時，把「找到當下的玩家座標」連同這一輪收集到的完整提示鏈存進歷史紀錄
        // （Configuration.TreasureHuntHistory，跨 session 持久化），供之後回頭比對每個距離等級
        // 的碼數區間準不準。找到後這一輪尋寶結束，順便清空目前的提示鏈跟畫面標記。
        private void OnTreasureFound()
        {
            var player = DalamudApi.ClientState.LocalPlayer;
            if (player == null || _hints.Count == 0)
            {
                Clear();
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

            Clear();
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

        // Whether a world point falls within a single hint's fan - same direction ± half-angle
        // and same distance-tier range that Draw() uses to render that hint's two edge lines.
        private static bool MatchesHint(Vector2 point, TreasureHint hint)
        {
            var origin2D = new Vector2(hint.Origin.X, hint.Origin.Z);
            var offset = point - origin2D;
            var distance = offset.Length();
            if (distance < hint.MinDistance || distance > hint.MaxDistance)
                return false;

            var angle = MathF.Atan2(offset.X, -offset.Y) * 180f / MathF.PI;
            if (angle < 0)
                angle += 360f;

            var diff = MathF.Abs(angle - hint.AngleDegrees);
            if (diff > 180f)
                diff = 360f - diff;

            return diff <= FanHalfAngleDegrees;
        }

        // 原本畫一個以三角交會估算點為中心的圓圈，但交會點常常不穩定（提示方位太接近時會跑到
        // 很奇怪的地方）、範圍圈又大到看不出實際指向，玩家回報「太遠看不出來、到了也沒有縮小到
        // 有用的範圍」。改成從「該次提示當下」座標出發、朝提示方位延伸的扇形（兩條邊線），長度
        // 跟著該次提示的距離等級縮短 - 直接呈現「往哪個方向走、大概還要多遠」。每次按胡蘿蔔都會
        // 疊加畫一個新的扇形，不會把前幾次的扇形擦掉，這樣可以直接在畫面上看到整個尋寶過程的
        // 方位是怎麼逐漸縮小的。
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

            if (!Splatoon.IsConnected())
                return;

            Splatoon.RemoveDynamicElements(LayerName);

            // ECommons.SplatoonAPI.Element.SetRefCoord/SetOffCoord(Vector3) - the library's own
            // helpers for fixed-coordinate elements - map refX=v.X,refY=v.Z,refZ=v.Y and the same
            // for off* (decompiled from ECommons.dll). Using two Line elements (not a Cone) here
            // deliberately sidesteps Splatoon's cone angle/rotation reference convention, which
            // isn't documented anywhere and would need live in-game trial and error to get right -
            // two lines from verified XYZ points carries no such risk.
            var elements = new List<Element>(_hints.Count * 2);
            foreach (var hint in _hints)
            {
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
                    color = 0x800000FF,
                    thicc = 3f,
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
                    color = 0x800000FF,
                    thicc = 3f,
                    Enabled = true,
                });
            }

            try
            {
                Splatoon.AddDynamicElements(LayerName, elements.ToArray(), -2); // -2 = 不自動過期，生命週期由本類別管理
            }
            catch (Exception ex)
            {
                DalamudApi.Log.Error(ex, "[TreasureHuntManager] AddDynamicElements failed");
            }

            SetMapFlag(EstimatedPosition.Value);
        }

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
                Utils.SendMessage("/vnav moveflag");
        }

        // 唯二會清掉目前這輪提示鏈跟畫面上扇形的地方：玩家自己按「清除」（UI 呼叫這個方法），
        // 或 OnTreasureFound 挖到寶藏時。刻意不會因為換區、閒置太久等情況自動清掉 - 扇形範圍
        // 通常要花好幾分鐘才走得到，自動清掉只會讓畫面上的參考線消失卻沒有新的可看。
        public void Clear()
        {
            _hints.Clear();
            EstimatedPosition = null;
            EstimatedRadius = 0f;
            IsUsingHistoricalPosition = false;
            _nearbyHistoricalRecord = null;
            Splatoon.RemoveDynamicElements(LayerName);
            Splatoon.RemoveDynamicElements(HistoryMarkerLayerName);
        }

        public void Dispose()
        {
            DalamudApi.ChatGui.ChatMessage -= OnChatMessage;
            DalamudApi.Framework.Update -= OnFrameworkUpdate;
            Splatoon.RemoveDynamicElements(LayerName);
            Splatoon.RemoveDynamicElements(HistoryMarkerLayerName);
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
    // Configuration.TreasureHuntHistory 裡，供之後回頭比對 TreasureHuntManager.DistanceTiers
    // 的碼數區間是否準確（例如：提示說「很近」時實際距離最終挖到的座標有多遠）。
    public class TreasureFoundRecord
    {
        public DateTime Timestamp { get; set; }
        public ushort TerritoryId { get; set; }
        public Vector3 FoundPosition { get; set; }
        public List<TreasureHint> Hints { get; set; } = new();
    }
}
