using ECommons;
using ECommons.SplatoonAPI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;

namespace EurekaHelper.System
{
    // 優雷卡（含豐水之地的兵武塔 BA）裡「看不見的東西」兩態標示：隱藏陷阱與傳送門。
    //
    // 資料模型抄的是 PalacePal 的做法（Pal.Client/Database/ClientLocation.cs 的 Seen 旗標
    // ＋ Pal.Client/Floors/FloorService.cs 的翻面邏輯）：同一個位置有兩種狀態 —
    //   * 可能有（Possible）：這個座標在**以前**的探索裡出現過，但現在看不到。
    //   * 已確認（Confirmed）：這個物件**此刻**就在 ObjectTable 裡。
    // PalacePal 匯入的點位一律 Seen=false（可能有），實際踩到／看到才翻成 true（已確認）。
    // 這裡照抄語意，但**不抄它的資料後端** — PalacePal 有自己的群眾外包伺服器，我們沒有，
    // 也不打算有。本類別的資料**只來自玩家自己的觀察**，存在本機。
    //
    // 顯示風格對齊 NecroLens（util/ESPUtils.cs）：有外框、不疊顏色。兩態用**同一個色相**的
    // 不同透明度＋線粗表現，不是換顏色 — 疊在一起時不會多出第三種顏色造成視覺困擾。
    //
    // 🔴 為什麼不內建任何 DataId 表：
    // PalacePal 能寫死 2007182 那組 DataId，是因為深層迷宮的陷阱在國際服被反覆驗證過。優雷卡
    // ／兵武塔沒有等價的公開資料，而且台服的資料表一律要假設與國際服不同。實查 7.20 台服 EXD
    // dump 的結果是：隱藏物件在 EObjName 裡**名字是空的**（PalacePal 那組 DataId 查出來全部
    // 空字串，而 2007543=「埋藏的寶藏」、2007357/2007358=「寶箱」查得到），所以光靠離線表也
    // 認不出哪個 DataId 是陷阱。猜一組寫死進來只會得到「靜默錯誤」。
    // 因此改成**先發現、再分類**：本類別被動記錄在優雷卡看到的 EventObj（DataId＋名字），
    // 玩家在設定頁把某個 DataId 標成陷阱／傳送門之後，才開始累積那個 DataId 的座標並繪製。
    // 出廠是空的 — 沒有任何捏造的座標。
    public class HazardManager : IDisposable
    {
        private const string LayerName = "EurekaHelper.Hazards";

        // 兵武塔（Baldesion Arsenal）的子地圖。台服 7.20 EXD dump 實查（Map.csv 的
        // TerritoryType 欄位）：豐水之地（TerritoryType 827）底下的 Map 只有 515（野外本體）
        // 與這 6 張塔內圖。
        //
        // ⚠️ ECommons GameHelpers/Content.cs 判 BA 用的是「MapID is >= 520 and <= 527」，但
        // 台服資料實查顯示 522/523 屬於 TerritoryType 826（樂欲之所甌博訥修道院）而不是豐水
        // 之地 — 那個連續區間是過寬的。這裡改用實查到的集合，並且外層仍然先卡 TerritoryType，
        // 兩道防線都對得上才算在塔內。
        private static readonly Dictionary<uint, string> BaFloorNames = new()
        {
            [520] = "總部塔入口",
            [521] = "兵武塔底層",
            [524] = "兵武塔中層",
            [525] = "兵武塔靈極層",
            [526] = "兵武塔星極層",
            [527] = "兵武塔頂層",
        };

        public static bool IsBaldesionArsenalMap(ushort territoryId, uint mapId) =>
            territoryId == 827 && BaFloorNames.ContainsKey(mapId);

        public static string GetMapLabel(uint mapId) =>
            BaFloorNames.TryGetValue(mapId, out var name) ? name : string.Empty;

        // 同一個座標視為「同一個點」的容忍距離。PalacePal 用 PalaceMath.IsNearlySamePosition
        // 做等價比較；這裡用平面距離即可 — 陷阱／傳送門不會在垂直方向重疊。
        private const float SamePositionRadius = 2.5f;

        // 掃描與重繪的節流。ObjectTable 全表掃描每幀跑沒有必要（陷阱不會動），而且優雷卡的
        // 物件數不少。
        private static readonly TimeSpan ScanInterval = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan RedrawInterval = TimeSpan.FromSeconds(2);
        private DateTime _nextScan = DateTime.MinValue;
        private DateTime _nextRedraw = DateTime.MinValue;

        private readonly string _dataPath;
        private HazardData _data = new();

        // 這一刻真的看得到的點（已確認）。key 是 DataId，value 是座標清單 — 每次掃描重建，
        // 不跨幀保存任何原生指標。
        private List<(uint DataId, Vector3 Position)> _visibleNow = new();

        public HazardManager()
        {
            _dataPath = Path.Combine(DalamudApi.PluginInterface.GetPluginConfigDirectory(), "Hazards.json");
            Load();

            ECommonsMain.Init(DalamudApi.PluginInterface, EurekaHelper.Plugin, Module.SplatoonAPI);

            DalamudApi.ClientState.TerritoryChanged += OnTerritoryChanged;
            DalamudApi.ClientState.MapIdChanged += OnMapIdChanged;
            DalamudApi.Framework.Update += OnFrameworkUpdate;
        }

        private void OnTerritoryChanged(ushort territoryId)
        {
            _visibleNow = new();
            Splatoon.RemoveDynamicElements(LayerName);
            _nextScan = DateTime.MinValue;
        }

        // 進出兵武塔不會換 TerritoryType（塔在豐水之地裡面，見 BaFloorNames 的註解），只會換
        // MapId — 所以樓層之間移動必須靠這個事件清掉上一層的標記，光聽 TerritoryChanged 會把
        // 上一層的點一路帶著跑。
        private void OnMapIdChanged(uint mapId)
        {
            _visibleNow = new();
            Splatoon.RemoveDynamicElements(LayerName);
            _nextScan = DateTime.MinValue;
        }

        private void OnFrameworkUpdate(IFramework framework)
        {
            if (DateTime.UtcNow < _nextScan)
                return;
            _nextScan = DateTime.UtcNow + ScanInterval;

            var territoryId = DalamudApi.ClientState.TerritoryType;
            if (!Utils.IsPlayerInEurekaZone(territoryId))
            {
                if (_visibleNow.Count > 0)
                {
                    _visibleNow = new();
                    Splatoon.RemoveDynamicElements(LayerName);
                }
                return;
            }

            var mapId = DalamudApi.ClientState.MapId;

            // 🔴 這個迴圈是唯一碰 ObjectTable 的地方，而且**只在同一幀內把值型別抄出來**
            // （DataId 是 uint、Position 是 Vector3 的複本）。不保留 IGameObject、不保留
            // Address — 那兩個都是建構當下凍結的，跨幀再用就是 AVE，而 AVE 在 .NET Core 是
            // corrupted-state exception，try/catch 攔不到。
            var visible = new List<(uint DataId, Vector3 Position)>();
            var catalogueChanged = false;
            var sightingsChanged = false;

            foreach (var obj in DalamudApi.ObjectTable)
            {
                if (obj.ObjectKind != ObjectKind.EventObj)
                    continue;

                // ⚠️ 用 BaseId 不是 DataId。這裡拿到的值是**身分比對**用的（要對 Classification
                // 字典與已記錄的 Sightings 做比對，而且會被寫進存檔跨 session 使用），不是拿去
                // 查 Excel 表。Dalamud 這版 IGameObject.DataId 已標記為過時（Renamed to
                // BaseId），兩者目前都回傳 Struct->BaseId、值完全相同，所以改名不影響行為。
                var dataId = obj.BaseId;
                if (dataId == 0)
                    continue;

                var position = obj.Position;
                var name = obj.Name.TextValue ?? string.Empty;

                // 目錄（catalogue）記錄「在優雷卡看過哪些 EventObj」，一個 DataId 一筆，用來
                // 讓玩家在 UI 上分類。這是有界的（不隨座標增長）。
                if (RecordInCatalogue(dataId, name, territoryId, mapId))
                    catalogueChanged = true;

                var kind = _data.Classification.GetValueOrDefault(dataId, HazardKind.Unclassified);
                if (kind is not (HazardKind.Trap or HazardKind.Portal))
                    continue;

                visible.Add((dataId, position));

                // 已分類的才累積座標 — 這是 PalacePal 式的持久點位，下次進來會以「可能有」
                // 畫出來。
                if (RecordSighting(territoryId, mapId, dataId, position))
                    sightingsChanged = true;
            }

            var changed = !SameVisibleSet(visible, _visibleNow);
            _visibleNow = visible;

            if (catalogueChanged || sightingsChanged)
                Save();

            if (changed || DateTime.UtcNow >= _nextRedraw)
            {
                Splatoon.RemoveDynamicElements(LayerName);
                Draw();
                _nextRedraw = DateTime.UtcNow + RedrawInterval;
            }
        }

        private static bool SameVisibleSet(
            List<(uint DataId, Vector3 Position)> a,
            List<(uint DataId, Vector3 Position)> b)
        {
            if (a.Count != b.Count)
                return false;

            for (var i = 0; i < a.Count; i++)
            {
                if (a[i].DataId != b[i].DataId)
                    return false;
                if (Vector3.Distance(a[i].Position, b[i].Position) > 0.1f)
                    return false;
            }

            return true;
        }

        private bool RecordInCatalogue(uint dataId, string name, ushort territoryId, uint mapId)
        {
            if (_data.Catalogue.TryGetValue(dataId, out var entry))
            {
                // 名字第一次抓到空字串（隱藏物件常常是空的）之後又抓到非空，補上去。
                if (string.IsNullOrEmpty(entry.Name) && !string.IsNullOrEmpty(name))
                {
                    entry.Name = name;
                    return true;
                }
                return false;
            }

            _data.Catalogue[dataId] = new HazardCatalogueEntry
            {
                Name = name,
                FirstSeenTerritoryId = territoryId,
                FirstSeenMapId = mapId,
                FirstSeen = DateTime.Now,
            };

            DalamudApi.Log.Information(
                $"[HazardManager] 在優雷卡發現未分類的 EventObj：DataId={dataId} 名稱=\"{(string.IsNullOrEmpty(name) ? "(無名稱)" : name)}\" territory={territoryId} map={mapId}");
            return true;
        }

        private bool RecordSighting(ushort territoryId, uint mapId, uint dataId, Vector3 position)
        {
            foreach (var existing in _data.Sightings)
            {
                if (existing.TerritoryId != territoryId || existing.MapId != mapId || existing.DataId != dataId)
                    continue;

                if (Vector3.Distance(existing.Position, position) > SamePositionRadius)
                    continue;

                existing.LastSeen = DateTime.Now;
                existing.TimesSeen++;
                return false; // 已經有這個點了，只更新統計，不算「新資料」（避免每次掃描都存檔）
            }

            _data.Sightings.Add(new HazardSighting
            {
                TerritoryId = territoryId,
                MapId = mapId,
                DataId = dataId,
                Position = position,
                FirstSeen = DateTime.Now,
                LastSeen = DateTime.Now,
                TimesSeen = 1,
            });

            DalamudApi.Log.Information(
                $"[HazardManager] 記錄新點位：DataId={dataId} territory={territoryId} map={mapId} 座標=({position.X:0.0}, {position.Y:0.0}, {position.Z:0.0})");
            return true;
        }

        // 顯示風格對齊 NecroLens：**只畫外框，不填色**（NecroLens 的 DrawCircle 也是
        // PathStroke 不是 PathFillConvex），兩態靠同色相的透明度與線粗區分，不換色相 —
        // 兩個點靠在一起時不會疊出第三種顏色。
        //
        // 全部用 CircleAtFixedCoordinates（固定座標）而不是綁定 actor：陷阱／傳送門不會移動，
        // 固定座標讓 Splatoon 完全不需要為我們解參考任何 actor，順帶讓「已確認」與「可能有」
        // 走同一條繪製路徑。
        private void Draw()
        {
            if (!Splatoon.IsConnected())
                return;

            var territoryId = DalamudApi.ClientState.TerritoryType;
            if (!Utils.IsPlayerInEurekaZone(territoryId))
                return;

            var mapId = DalamudApi.ClientState.MapId;
            var elements = new List<Element>();

            foreach (var sighting in _data.Sightings)
            {
                if (sighting.TerritoryId != territoryId || sighting.MapId != mapId)
                    continue;

                var kind = _data.Classification.GetValueOrDefault(sighting.DataId, HazardKind.Unclassified);
                if (kind is not (HazardKind.Trap or HazardKind.Portal))
                    continue;

                var confirmed = _visibleNow.Any(v =>
                    v.DataId == sighting.DataId &&
                    Vector3.Distance(v.Position, sighting.Position) <= SamePositionRadius);

                elements.Add(new Element(ElementType.CircleAtFixedCoordinates)
                {
                    refX = sighting.Position.X,
                    refY = sighting.Position.Z,
                    refZ = sighting.Position.Y,
                    radius = kind == HazardKind.Trap ? 1.7f : 2.2f,
                    color = HazardColors.Get(kind, confirmed),
                    Filled = false,
                    thicc = confirmed ? 3f : 1.5f,
                    Enabled = true,
                });
            }

            if (elements.Count == 0)
                return;

            try
            {
                Splatoon.AddDynamicElements(LayerName, elements.ToArray(), -2);
            }
            catch (Exception ex)
            {
                DalamudApi.Log.Error(ex, $"[HazardManager] AddDynamicElements 失敗（{elements.Count} 個元素）");
            }
        }

        #region UI 用的查詢與編輯

        public bool IsSplatoonReady => Splatoon.IsConnected();

        public string GetDataPath() => _dataPath;

        public IReadOnlyDictionary<uint, HazardCatalogueEntry> GetCatalogue() => _data.Catalogue;

        public HazardKind GetClassification(uint dataId) =>
            _data.Classification.GetValueOrDefault(dataId, HazardKind.Unclassified);

        public void SetClassification(uint dataId, HazardKind kind)
        {
            if (kind == HazardKind.Unclassified)
                _data.Classification.Remove(dataId);
            else
                _data.Classification[dataId] = kind;

            Save();
            Splatoon.RemoveDynamicElements(LayerName);
            _nextScan = DateTime.MinValue; // 立刻重掃，讓剛分類的 DataId 馬上開始累積座標
        }

        public IReadOnlyList<HazardSighting> GetSightings() => _data.Sightings;

        // 目前這張圖上「已確認」的點數／「可能有」的點數 — 給 UI 列上直接顯示用。
        public (int Confirmed, int Possible) GetCurrentMapCounts()
        {
            var territoryId = DalamudApi.ClientState.TerritoryType;
            var mapId = DalamudApi.ClientState.MapId;

            var confirmed = 0;
            var possible = 0;

            foreach (var sighting in _data.Sightings)
            {
                if (sighting.TerritoryId != territoryId || sighting.MapId != mapId)
                    continue;

                var kind = _data.Classification.GetValueOrDefault(sighting.DataId, HazardKind.Unclassified);
                if (kind is not (HazardKind.Trap or HazardKind.Portal))
                    continue;

                if (_visibleNow.Any(v => v.DataId == sighting.DataId &&
                                         Vector3.Distance(v.Position, sighting.Position) <= SamePositionRadius))
                    confirmed++;
                else
                    possible++;
            }

            return (confirmed, possible);
        }

        public void DeleteSighting(HazardSighting sighting)
        {
            _data.Sightings.Remove(sighting);
            Save();
            Splatoon.RemoveDynamicElements(LayerName);
            Draw();
        }

        public void ClearSightings()
        {
            _data.Sightings.Clear();
            Save();
            Splatoon.RemoveDynamicElements(LayerName);
        }

        public void ClearCatalogue()
        {
            _data.Catalogue.Clear();
            Save();
        }

        #endregion

        private void Load()
        {
            try
            {
                if (!File.Exists(_dataPath))
                {
                    _data = new HazardData();
                    return;
                }

                var json = File.ReadAllText(_dataPath);
                _data = JsonConvert.DeserializeObject<HazardData>(json) ?? new HazardData();
            }
            catch (Exception ex)
            {
                DalamudApi.Log.Error(ex, $"[HazardManager] 讀取 {_dataPath} 失敗，改用空資料");
                _data = new HazardData();
            }
        }

        private void Save()
        {
            try
            {
                File.WriteAllText(_dataPath, JsonConvert.SerializeObject(_data, Formatting.Indented));
            }
            catch (Exception ex)
            {
                DalamudApi.Log.Error(ex, $"[HazardManager] 寫入 {_dataPath} 失敗");
            }
        }

        public void Dispose()
        {
            DalamudApi.ClientState.TerritoryChanged -= OnTerritoryChanged;
            DalamudApi.ClientState.MapIdChanged -= OnMapIdChanged;
            DalamudApi.Framework.Update -= OnFrameworkUpdate;
            Splatoon.RemoveDynamicElements(LayerName);
        }
    }

    // ⚠️ 這個列舉刻意保留 Unclassified = 0，讓 default(HazardKind) 落在有效值上
    // （「還沒分類」正是未知狀態該有的預設）。
    public enum HazardKind
    {
        Unclassified = 0,
        Trap = 1,
        Portal = 2,
        Ignored = 3,
    }

    public static class HazardColors
    {
        // Splatoon 的 color 是 ImGui 的 0xAABBGGRR（見 SplatoonManager 既有註解裡的
        // 0x2600FFFFu = 黃色）。
        //
        // 兩態**同色相、只改 alpha** — 這是 NecroLens「不疊顏色」與 PalacePal「可能／已確認」
        // 兩態的交集：疊在一起也只會是同一個顏色變深，不會出現第三種顏色。
        private const uint TrapRgb = 0x285AFFu;   // 橘紅（RGB FF5A28）
        private const uint PortalRgb = 0xFFC030u; // 青藍（RGB 30C0FF）

        public static uint Get(HazardKind kind, bool confirmed)
        {
            var rgb = kind == HazardKind.Portal ? PortalRgb : TrapRgb;
            var alpha = confirmed ? 0xE0u : 0x60u;
            return (alpha << 24) | rgb;
        }
    }

    public class HazardData
    {
        // DataId -> 在優雷卡看過的 EventObj 基本資料（分類用的候選清單）
        public Dictionary<uint, HazardCatalogueEntry> Catalogue { get; set; } = new();

        // DataId -> 玩家指定的分類。沒有出廠值 — 見 HazardManager 類別註解。
        public Dictionary<uint, HazardKind> Classification { get; set; } = new();

        // 已分類為陷阱／傳送門的 DataId 的累積座標
        public List<HazardSighting> Sightings { get; set; } = new();
    }

    public class HazardCatalogueEntry
    {
        public string Name { get; set; } = string.Empty;
        public ushort FirstSeenTerritoryId { get; set; }
        public uint FirstSeenMapId { get; set; }
        public DateTime FirstSeen { get; set; }
    }

    public class HazardSighting
    {
        public ushort TerritoryId { get; set; }
        public uint MapId { get; set; }
        public uint DataId { get; set; }
        public Vector3 Position { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public int TimesSeen { get; set; }
    }
}
