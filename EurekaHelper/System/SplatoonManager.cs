using ECommons;
using ECommons.SplatoonAPI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using EurekaHelper.XIV;
using EurekaHelper.XIV.Zones;

namespace EurekaHelper.System
{
    // Draws Eureka mob aggro-detection ranges (aural/visual/magic/blood) in-game via Splatoon's public IPC
    // (ECommons.SplatoonAPI), the same mechanism PalacePal uses to draw trap markers - no manual Splatoon layout
    // import needed. Per-mob type/radius lives in the player's AggroRanges.json, seeded from AggroRanges.seed.json
    // (or from KnownAggroNames when the seed is missing) and editable via the Debug tab.
    public class SplatoonManager : IDisposable
    {
        private const string LayerName = "EurekaHelper.AggroRanges";

        // IMPORTANT: on the TC client IBattleNpc.Name.TextValue is the CHINESE display name, not the English one used
        // elsewhere in this codebase (EurekaFate.BossName/SpawnedBy etc. are English-keyed), and Splatoon's refActorName has
        // to match the live display name or it silently binds to nothing - so this dictionary is keyed by the CHINESE name.
        // Types come from community-compiled aggro notes, not an official source. Radius is left at each type's default
        // from AggroTypeDefaults (0 for Magic/Blood = nothing drawn until measured via the Debug tab).
        private static readonly Dictionary<string, AggroType> KnownAggroNames = new()
        {
            // 聽覺型 (Aural) - blind to sight, aggros on movement/running past. Core counter:
            // walk (not run) past them.
            ["舊日之影"] = AggroType.Aural,      // Anemos, Lv18-22 (Shadow Wraith)
            ["虛無冰雪龍"] = AggroType.Aural,     // Pagos, Lv40 (Frozen Void Dragon) - the famous "sleeping dragon"
            ["恒冰阿納拉"] = AggroType.Aural,     // Pagos, Lv29-31 (Pagos Anala)
            ["湧火鷹蜂"] = AggroType.Aural,       // Pyros, Lv50 (Pyros Hawk)
            ["瓦爾蜘蛛"] = AggroType.Aural,       // Pyros, Lv45-47 (Val Tarantula)
            ["豐水睡龍"] = AggroType.Aural,       // Hydatos, Lv65 (Hydatos Void Dragon)
            ["豐水魔界花"] = AggroType.Aural,     // Hydatos, Lv58-60 (Hydatos Morbol)

            // Sourced from a JP community Splatoon-layout blog post (actual in-game verified
            // detection ranges, not a guess) - see System/AggroRanges.seed.json for the precise
            // per-mob radius that came with these (this dict only carries the type).
            ["常風畢托所"] = AggroType.Aural,     // Anemos (Anemos Pitho)
            ["瓦爾螳螂"] = AggroType.Aural,       // Pyros (Val Mantis)
            ["瓦爾鼴鼠"] = AggroType.Aural,       // Hydatos (Val Mole)
            ["豐水榴彈怪"] = AggroType.Aural,     // Hydatos (Hydatos Squib)

            // 血量偵測型/夜間不死系 (Blood) - only spawn 18:00-06:00, infinite-aggro anyone
            // below ~30% HP regardless of facing/distance. Core counter: don't run around at
            // night below 30% HP.
            ["瓦爾幽靈"] = AggroType.Blood,       // Anemos (Val Specter), triggers Lv19 NM Lamashtu
            ["化石暴龍"] = AggroType.Blood,       // Anemos (Fossil Dragon), triggers Lv20 NM Fafnir
            ["恒冰屍骸"] = AggroType.Blood,       // Pagos (Pagos Corpse)
            ["墓地守衛"] = AggroType.Blood,       // Pagos (Gravekeeper), triggers Lv33 NM Ker
            ["湧火白狼"] = AggroType.Blood,       // Pyros (Pyros Wolf)
            ["湧火浮靈"] = AggroType.Blood,       // Pyros (Pyros Bhoot), triggers Lv35 NM Leucosia
            ["豐水巫妖"] = AggroType.Blood,       // Hydatos (Hydatos Lich)
            ["實驗體"] = AggroType.Blood,         // Hydatos (Experimental Tomestones)

            // Name-pattern based (屍人兵/幽靈/無魂/腐屍/殭屍 = corpse/soulless/carrion naming,
            // matching the confirmed Blood-type mobs above) rather than individually source-
            // verified - lower confidence than the JP-sourced Aural entries above, but still
            // higher confidence than leaving them at the Visual default.
            ["速射屍人兵"] = AggroType.Blood,
            ["毒手屍人兵"] = AggroType.Blood,
            ["強腕屍人兵"] = AggroType.Blood,
            ["湧火幽靈"] = AggroType.Blood,
            ["豐水幽靈"] = AggroType.Blood,
            ["暗影幽靈"] = AggroType.Blood,
            ["無魂搜尋者"] = AggroType.Blood,
            ["無魂代理人"] = AggroType.Blood,
            ["武士腐屍"] = AggroType.Blood,
            ["殭屍布羅賓雅克"] = AggroType.Blood,
            ["食腐獅鷲"] = AggroType.Blood,
            ["瓦爾爛泥食腐獸"] = AggroType.Blood,

            // 魔法偵測型/元精系 (Magic) - only spawn in specific weather, aggro on any nearby
            // spell/magic-category action (including healing, Logos actions). Core counter:
            // don't cast anything near them.
            ["常風元精"] = AggroType.Magic,       // Anemos, Showers/Gales
            ["冰島元精"] = AggroType.Magic,       // Pagos, Thunder/Blizzards
            ["湧火元精"] = AggroType.Magic,       // Pyros, Heat Waves/Fair Skies
            ["豐水元精"] = AggroType.Magic,       // Hydatos, Thunderstorms/Squall
            ["死亡元精"] = AggroType.Magic,       // Hydatos (Death Sprite) - 元精 naming pattern, JP-sourced radius
        };

        private readonly string _configPath;
        private readonly string _seenMonstersPath;
        // 鍵一律是 MonsterNameKey.Normalize 過的寫法：載入時換掉 7.25 前的舊寫法，查詢與新增也先正規化。
        private Dictionary<string, List<AggroRangeConfig>> _aggroRanges = new();
        private HashSet<string> _seenMonsters = new();

        // "變異怪物" (mutant field mobs, unrelated to the aggro ranges above): under certain weather/time-of-day a normal mob
        // has no status until it triggers, then gets exactly one of "環境適應" (Environmental Adaptation) or "突然變異"
        // (Sudden Mutation). _adaptedNames/_mutatedNames are detected from the live status list. The Splatoon circle is
        // drawn BEFORE that - while the mob is statusless but the current window CAN trigger it (see
        // MutantMonster.WeatherWindows) - and stops once either status appears. Matched by name, so same-named mobs highlight together.
        private const string MutationLayerName = "EurekaHelper.MutationStatus";
        private const string AdaptedStatusName = "環境適應";
        private const string MutatedStatusName = "突然變異";
        private HashSet<string> _adaptedNames = new();
        private HashSet<string> _mutatedNames = new();
        private static readonly TimeSpan MutationRedrawInterval = TimeSpan.FromSeconds(1);
        private DateTime _nextMutationRedraw = DateTime.MinValue;

        public SplatoonManager()
        {
            _configPath = Path.Combine(DalamudApi.PluginInterface.GetPluginConfigDirectory(), "AggroRanges.json");
            _seenMonstersPath = Path.Combine(DalamudApi.PluginInterface.GetPluginConfigDirectory(), "SeenMonsters.json");
            EnsureConfigFileExists();
            LoadConfig();
            LoadSeenMonsters();

            ECommonsMain.Init(DalamudApi.PluginInterface, EurekaHelper.Plugin, Module.SplatoonAPI);
            Splatoon.SetOnConnect(OnSplatoonConnect);

            DalamudApi.ClientState.TerritoryChanged += OnTerritoryChanged;
            if (Utils.IsPlayerInEurekaZone(DalamudApi.ClientState.TerritoryType))
                DalamudApi.Framework.Update += OnFrameworkUpdate;
        }

        // NOTE: Splatoon.SetOnConnect is a single static callback slot (ECommons.SplatoonAPI.Splatoon.OnConnect), not a
        // multicast event - only ONE registration survives. This class owns the slot (it needs the "just (re)connected"
        // edge to trigger an immediate redraw); TreasureHuntManager must read Splatoon.IsConnected() directly instead.
        private void OnSplatoonConnect() => DrawForCurrentZone();

        private void OnTerritoryChanged(ushort territoryId)
        {
            Splatoon.RemoveDynamicElements(LayerName);
            Splatoon.RemoveDynamicElements(MutationLayerName);
            _adaptedNames = new();
            _mutatedNames = new();

            if (Utils.IsPlayerInEurekaZone(territoryId))
            {
                DalamudApi.Framework.Update += OnFrameworkUpdate;
                DrawForCurrentZone();
            }
            else
            {
                DalamudApi.Framework.Update -= OnFrameworkUpdate;
            }
        }

        // Passively records every battle mob name encountered in the zone (so coverage grows over a play session without
        // manually locking each one) and auto-registers an aggro-range entry for it (see AutoClassify).
        // Diagnostic counters, shown in the Debug tab's Seen Monsters section, so a stuck "0 discovered" can be pinned
        // to an exact pipeline stage from a screenshot.
        private int _lastScanTotalObjects;
        private int _lastScanBattleNpcs;
        private int _lastScanEnemyKind;
        private int _lastScanAliveEnemies;

        public (int TotalObjects, int BattleNpcs, int EnemyKind, int AliveEnemies) GetLastScanCounts() =>
            (_lastScanTotalObjects, _lastScanBattleNpcs, _lastScanEnemyKind, _lastScanAliveEnemies);

        private static readonly TimeSpan RedrawInterval = TimeSpan.FromSeconds(2);
        private DateTime _nextForcedRedraw = DateTime.MinValue;

        private void OnFrameworkUpdate(IFramework framework)
        {
            var newNames = false;
            var newlyClassified = false;

            var totalObjects = 0;
            var battleNpcs = 0;
            var enemyKind = 0;
            var aliveEnemies = 0;

            var currentAdapted = new HashSet<string>();
            var currentMutated = new HashSet<string>();

            foreach (var obj in DalamudApi.ObjectTable)
            {
                totalObjects++;

                if (obj is not IBattleNpc battleNpc)
                    continue;
                battleNpcs++;

                // NOTE: filter on the raw SubKind byte, not the Dalamud BattleNpcKind enum wrapper or OwnerId: both of those
                // filtered out 100% of BattleNpc in testing on this API level. Raw values: 5 = Enemy, 2 = Pet (fairy/egi/automaton),
                // 3 = Chocobo companion, 9 = Buddy/trust, 11 = Helper - this excludes summoned pets without rejecting real monsters.
                if (battleNpc.SubKind != 5)
                    continue;
                enemyKind++;

                // Skip dead bodies (corpse still exists briefly before despawning). Deliberately NOT also skipping !IsTargetable:
                // it is flaky (LoS-dependent) for regular idle mobs and ended up filtering out everything. The Splatoon Element
                // itself sets onlyTargetable=true (see DrawForCurrentZone), which covers "stop drawing once dead".
                if (battleNpc.IsDead)
                    continue;
                aliveEnemies++;

                var name = MonsterNameKey.Normalize(battleNpc.Name.TextValue);
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                // NOTE: deliberately no "mob is 3+ levels below the player" filter: Eureka mobs are leveled on the Elemental Level
                // track, so comparing IBattleNpc.Level against LocalPlayer.Level (job level) filters out nearly every Eureka mob.

                // Logging "seen" and having an AggroRanges entry are separate concerns - deliberately NOT gating classification on
                // _seenMonsters.Add(): a name already in the persisted SeenMonsters.json whose entry is missing (older build, or
                // deleted via the Debug tab) would otherwise never get one again. AutoClassify no-ops cheaply via
                // _aggroRanges.ContainsKey, so calling it for every visible monster every tick is safe and self-heals that case.
                if (_seenMonsters.Add(name))
                    newNames = true;

                if (AutoClassify(name))
                    newlyClassified = true;

                if (BackfillNpcId(name, battleNpc.NameId))
                    newlyClassified = true;

                foreach (var status in battleNpc.StatusList)
                {
                    var statusName = status.GameData.Value.Name.ExtractText();
                    if (statusName == AdaptedStatusName)
                        currentAdapted.Add(name);
                    else if (statusName == MutatedStatusName)
                        currentMutated.Add(name);
                }
            }

            _lastScanTotalObjects = totalObjects;
            _lastScanBattleNpcs = battleNpcs;
            _lastScanEnemyKind = enemyKind;
            _lastScanAliveEnemies = aliveEnemies;

            if (newNames)
                SaveSeenMonsters();

            // Batch the redraw: entering a zone can surface many new monsters within the same
            // tick, and calling Splatoon.RemoveDynamicElements + AddDynamicElements once per
            // monster (rather than once per tick) raced against itself - a later Remove could
            // land before an earlier Add's elements were actually registered, silently dropping
            // some of them (reported as "some aggro ranges just don't show up").
            if (newlyClassified)
            {
                SaveConfig();
                Splatoon.RemoveDynamicElements(LayerName);
                DrawForCurrentZone();
                _nextForcedRedraw = DateTime.UtcNow + RedrawInterval;
            }
            else if (DateTime.UtcNow >= _nextForcedRedraw)
            {
                // Splatoon's onlyTargetable filter is only re-evaluated against the current dynamic element set, not against the
                // live object table every render frame - a dead monster's circle would otherwise keep showing until something else
                // triggers a redraw. Force a periodic remove+re-add so dead monsters' ranges clear within ~2 seconds.
                Splatoon.RemoveDynamicElements(LayerName);
                DrawForCurrentZone();
                _nextForcedRedraw = DateTime.UtcNow + RedrawInterval;
            }

            // Mutation status can appear/disappear far more often than aggro-range classification
            // does, so it gets its own shorter redraw cadence/layer instead of piggybacking on
            // RedrawInterval above.
            if (!currentAdapted.SetEquals(_adaptedNames) || !currentMutated.SetEquals(_mutatedNames) ||
                DateTime.UtcNow >= _nextMutationRedraw)
            {
                _adaptedNames = currentAdapted;
                _mutatedNames = currentMutated;
                Splatoon.RemoveDynamicElements(MutationLayerName);
                DrawMutationStatus();
                _nextMutationRedraw = DateTime.UtcNow + MutationRedrawInterval;
            }
        }

        // Draws a small solid circle (15% alpha) on any seeded mutant monster whose weather/time-of-day window is active
        // AND that hasn't triggered yet (no 環境適應/突然變異 status) - yellow if predicted to end up "突然變異", green if
        // "環境適應" (see MutantMonster.PredictedOutcome). Only Pagos/Pyros/Hydatos have weather-window data seeded.
        // Same name-matching limitation as the aggro ranges: a same-named mob that already mutated may keep showing this
        // circle until Splatoon's own onlyTargetable/redraw cycle catches up - accepted rather than fought.
        private void DrawMutationStatus()
        {
            if (!Splatoon.IsConnected())
                return;

            var (monsters, currentWeather) = DalamudApi.ClientState.TerritoryType switch
            {
                763 => (EurekaPagosMutants.Monsters, EurekaPagos.GetCurrentWeatherInfoStatic().Weather),
                795 => (EurekaPyrosMutants.Monsters, EurekaPyros.GetCurrentWeatherInfoStatic().Weather),
                827 => (EurekaHydatosMutants.Monsters, EurekaHydatos.GetCurrentWeatherInfoStatic().Weather),
                _ => (null, EurekaWeather.None),
            };
            if (monsters == null)
                return;

            var isNight = EorzeaTime.Now.IsNight;

            var playerPos = DalamudApi.ObjectTable.LocalPlayer?.Position;
            var elements = new List<Element>();

            foreach (var monster in monsters)
            {
                if (_adaptedNames.Contains(monster.Name) || _mutatedNames.Contains(monster.Name))
                    continue; // already triggered - nothing left to flag

                if (!monster.IsEligibleNow(currentWeather, isNight))
                    continue;

                var circle = new Element(ElementType.CircleRelativeToActorPosition)
                {
                    refActorType = RefActorType.IGameObjectWithSpecifiedAttribute,
                    refActorName = monster.Name,
                    radius = 1f,
                    color = monster.PredictedOutcome == MutationOutcome.Mutated
                        ? 0x2600FFFFu  // yellow, 15% alpha
                        : 0x2600FF00u, // green, 15% alpha
                    Filled = true,
                    thicc = 2f,
                    onlyTargetable = true,
                };
                ApplyDistanceLimit(circle, playerPos);
                elements.Add(circle);
            }

            try
            {
                Splatoon.AddDynamicElements(MutationLayerName, elements.ToArray(), -2);
            }
            catch (Exception ex)
            {
                DalamudApi.Log.Error(ex, $"[SplatoonManager] AddDynamicElements failed for {elements.Count} mutation-status element(s)");
            }
        }

        // Returns true if a new entry was added (caller batches the Splatoon redraw itself -
        // see OnFrameworkUpdate - rather than each call triggering its own Remove+Add cycle).
        private bool AutoClassify(string name)
        {
            if (_aggroRanges.ContainsKey(name))
                return false;

            // Anything not in KnownAggroNames falls back to Visual, the documented default aggro
            // type for most Eureka mobs - and unlike Magic/Blood (no known default radius),
            // Visual/Aural DO have user-specified baseline ranges, so this is drawn immediately
            // rather than sitting at radius 0 until measured.
            var type = KnownAggroNames.GetValueOrDefault(name, AggroType.Visual);

            var (shape, color, radius, coneHalfAngle) = AggroTypeDefaults.Get(type);
            _aggroRanges[name] = new List<AggroRangeConfig>
            {
                new() { Type = type, Shape = shape, Radius = radius, ConeHalfAngleDegrees = coneHalfAngle, Color = color },
            };
            DalamudApi.Log.Information($"[SplatoonManager] Auto-registered \"{name}\" as {type} aggro ({(type == AggroType.Visual ? "default" : "name pattern match")}{(radius <= 0f ? ", radius still needs measuring" : "")})");
            return true;
        }

        // Passively fills in each aggro-range entry's NPC Name ID the first time that name is seen alive. Once collected,
        // DrawForCurrentZone switches that entry from substring name-matching (which false-positives whenever one mob's
        // name is contained in another's, e.g. "達菲妮" vs "豐水達菲妮") to an exact ID match.
        // Returns true if a config was updated (caller batches the Splatoon redraw the same way AutoClassify does).
        private bool BackfillNpcId(string name, uint nameId)
        {
            if (nameId == 0 || !_aggroRanges.TryGetValue(name, out var configs))
                return false;

            var changed = false;
            foreach (var config in configs)
            {
                if (config.NpcId != 0)
                    continue;

                config.NpcId = nameId;
                changed = true;
            }

            if (changed)
                DalamudApi.Log.Information($"[SplatoonManager] Collected NPC ID {nameId} for \"{name}\"");

            return changed;
        }

        public IReadOnlyCollection<string> GetSeenMonsters() => _seenMonsters;

        // Live-detected status: which mob names are currently showing "環境適應" (about to
        // mutate) or "突然變異" (already mutated), refreshed every OnFrameworkUpdate tick
        // regardless of whether Splatoon is connected (only the drawing step needs that).
        public IReadOnlyCollection<string> GetAdaptedNames() => _adaptedNames;
        public IReadOnlyCollection<string> GetMutatedNames() => _mutatedNames;

        private void LoadSeenMonsters()
        {
            try
            {
                if (!File.Exists(_seenMonstersPath))
                {
                    _seenMonsters = new();
                    return;
                }

                var json = File.ReadAllText(_seenMonstersPath);
                _seenMonsters = JsonConvert.DeserializeObject<HashSet<string>>(json) ?? new();
                var renamed = MonsterNameKey.NormalizeNames(ref _seenMonsters);
                if (renamed > 0)
                    DalamudApi.Log.Information($"[SplatoonManager] SeenMonsters.json 有 {renamed} 個怪物名是 7.25 前的舊中間點寫法，已改用新寫法比對");
            }
            catch (Exception ex)
            {
                DalamudApi.Log.Error(ex, $"Failed to load seen-monsters list from {_seenMonstersPath}");
                _seenMonsters = new();
            }
        }

        private void SaveSeenMonsters() =>
            File.WriteAllText(_seenMonstersPath, JsonConvert.SerializeObject(_seenMonsters, Formatting.Indented));

        // Redraws every configured NM's aggro ranges for the zone we're currently in. Safe to
        // call repeatedly (e.g. after reloading the config file); Splatoon replaces elements
        // under the same layer name rather than stacking duplicates.
        private const float MaxDrawDistance = 60f;

        // 「這一型有威脅但範圍還沒量出來」的標記圈大小。刻意取一個明顯偏小、不可能被誤認成
        // 真實仇恨範圍的值 —— 它表達的是「未知」，不是一個估計值。
        private const float UnmeasuredMarkerRadius = 2f;

        // Skip drawing once the player is farther than MaxDrawDistance from the range's own reference actor.
        // NOTE: Splatoon compares DistanceSourceX/Y/Z against gameObject.GetPositionXZY(), which swaps Y (height) and Z
        // (depth) from Dalamud's normal Position layout, so Y/Z are swapped here to match - raw Position.X/Y/Z would
        // compare height against depth and hide everything as soon as there is any elevation difference.
        private static void ApplyDistanceLimit(Element element, global::System.Numerics.Vector3? playerPos)
        {
            if (playerPos is not { } pos)
                return;

            element.LimitDistance = true;
            element.DistanceSourceX = pos.X;
            element.DistanceSourceY = pos.Z;
            element.DistanceSourceZ = pos.Y;
            element.DistanceMax = MaxDrawDistance;
        }

        // Prefers an exact NPC Name ID match (collected passively - see BackfillNpcId) over Splatoon's substring
        // name-matching, which false-positives whenever one mob's display name is contained inside another's (e.g. "達菲妮"
        // inside "豐水達菲妮"). refActorComparisonAnd + an empty refActorName tells Splatoon to skip the name check
        // entirely and match on refActorNPCID alone.
        private static void ApplyActorMatch(Element element, string bossName, AggroRangeConfig range)
        {
            element.refActorType = RefActorType.IGameObjectWithSpecifiedAttribute;

            if (range.NpcId != 0)
            {
                element.refActorComparisonAnd = true;
                element.refActorNPCID = range.NpcId;
            }
            else
            {
                element.refActorName = bossName;
            }
        }

        public void DrawForCurrentZone()
        {
            if (!Splatoon.IsConnected())
                return;

            if (!Utils.IsPlayerInEurekaZone(DalamudApi.ClientState.TerritoryType))
                return;

            var playerPos = DalamudApi.ObjectTable.LocalPlayer?.Position;

            var elements = new List<Element>();
            foreach (var (bossName, ranges) in _aggroRanges)
            {
                foreach (var range in ranges)
                {
                    if (range.Radius <= 0f)
                    {
                        // 🔴 半徑 0 在 Splatoon 上等於「什麼都不畫」，而畫面上「沒有圈」跟「這隻沒有威脅」長得一模一樣 ——
                        // Magic/Blood 兩型出廠就是 0（沒有公開來源給得出基準距離），所以預設狀態下這兩型是**靜默消失**的。
                        // 所以提供一個「已知類型、範圍未知」的小標記圈：固定 UnmeasuredMarkerRadius 大小，不假裝那就是真的仇恨範圍。
                        // 預設關閉（沿用不畫的行為）。
                        if (!EurekaHelper.Config.ShowUnmeasuredAggroMarkers)
                            continue;

                        var marker = new Element(ElementType.CircleRelativeToActorPosition)
                        {
                            radius = UnmeasuredMarkerRadius,
                            color = range.Color,
                            Filled = false,
                            thicc = 1.5f,
                            onlyTargetable = true,
                        };
                        ApplyActorMatch(marker, bossName, range);
                        ApplyDistanceLimit(marker, playerPos);
                        elements.Add(marker);
                        continue;
                    }

                    if (range.Shape == AggroShape.Cone)
                    {
                        // NOTE: Splatoon's ImGui-Legacy renderer hardcodes Cone elements to always draw filled, ignoring Element.Filled
                        // (ImGuiLegacyRenderer.AddCone passes a literal true). Rather than fight that, draw the cone filled but with a
                        // low-alpha color (see AggroTypeDefaults for Visual's default) so it reads as a light directional tint instead
                        // of an opaque wedge.
                        var cone = new Element(ElementType.ConeRelativeToObjectPosition)
                        {
                            radius = range.Radius,
                            coneAngleMin = -range.ConeHalfAngleDegrees,
                            coneAngleMax = range.ConeHalfAngleDegrees,
                            includeRotation = true,
                            color = range.Color,
                            Filled = true,
                            thicc = range.Thickness,
                            onlyTargetable = true, // stop drawing once the actor dies/despawns
                        };
                        ApplyActorMatch(cone, bossName, range);
                        ApplyDistanceLimit(cone, playerPos);
                        elements.Add(cone);
                    }
                    else
                    {
                        var circle = new Element(ElementType.CircleRelativeToActorPosition)
                        {
                            radius = range.Radius,
                            color = range.Color,
                            Filled = false, // Circle elements DO respect Filled correctly - outline only
                            thicc = range.Thickness,
                            onlyTargetable = true, // stop drawing once the actor dies/despawns
                        };
                        ApplyActorMatch(circle, bossName, range);
                        ApplyDistanceLimit(circle, playerPos);
                        elements.Add(circle);
                    }
                }
            }

            if (elements.Count == 0)
                return;

            try
            {
                Splatoon.AddDynamicElements(LayerName, elements.ToArray(), -2); // -2 = no auto-expiry, we manage lifetime via territory change
            }
            catch (Exception ex)
            {
                DalamudApi.Log.Error(ex, $"[SplatoonManager] AddDynamicElements failed for {elements.Count} element(s)");
            }
        }

        public void ReloadConfig()
        {
            LoadConfig();
            Splatoon.RemoveDynamicElements(LayerName);
            DrawForCurrentZone();
        }

        public string GetConfigPath() => _configPath;

        public IReadOnlyDictionary<string, List<AggroRangeConfig>> GetAllEntries() => _aggroRanges;

        public IReadOnlyList<AggroRangeConfig> GetEntriesFor(string bossName) =>
            _aggroRanges.TryGetValue(MonsterNameKey.Normalize(bossName), out var list) ? list : Array.Empty<AggroRangeConfig>();

        // Used by the Debug tab: lock a target in-game, tune shape/radius/color while watching
        // the live Splatoon overlay, then commit it here once it looks right.
        public void AddEntry(string bossName, AggroRangeConfig config)
        {
            bossName = MonsterNameKey.Normalize(bossName);
            if (!_aggroRanges.TryGetValue(bossName, out var list))
            {
                list = new List<AggroRangeConfig>();
                _aggroRanges[bossName] = list;
            }

            list.Add(config);
            SaveConfig();
            Splatoon.RemoveDynamicElements(LayerName);
            DrawForCurrentZone();
        }

        public void RemoveEntry(string bossName, int index)
        {
            bossName = MonsterNameKey.Normalize(bossName);
            if (!_aggroRanges.TryGetValue(bossName, out var list) || index < 0 || index >= list.Count)
                return;

            list.RemoveAt(index);
            if (list.Count == 0)
                _aggroRanges.Remove(bossName);

            SaveConfig();
            Splatoon.RemoveDynamicElements(LayerName);
            DrawForCurrentZone();
        }

        private void SaveConfig() =>
            File.WriteAllText(_configPath, JsonConvert.SerializeObject(_aggroRanges, Formatting.Indented));

        // Ships next to EurekaHelper.dll (see the csproj's <None Update="System\AggroRanges.seed.json">
        // CopyToOutputDirectory entry) and is tracked in git, unlike the per-player runtime config
        // this seeds - see that file's own header comment for how to re-sync it after curating more
        // entries in-game via the Debug tab.
        private const string SeedFileName = "AggroRanges.seed.json";

        private void EnsureConfigFileExists()
        {
            if (File.Exists(_configPath))
                return;

            if (TrySeedFromBundledFile())
                return;

            SeedFromKnownAggroNamesOnly();
        }

        // Preferred path: copy the git-tracked, community-researched seed file shipped alongside
        // the DLL. Falls back to the KnownAggroNames-only seed (see below) if it's missing or
        // fails to parse, so a broken/missing seed file never blocks the plugin from working.
        private bool TrySeedFromBundledFile()
        {
            try
            {
                var assemblyDir = Path.GetDirectoryName(DalamudApi.PluginInterface.AssemblyLocation.FullName);
                if (assemblyDir == null)
                    return false;

                var bundledPath = Path.Combine(assemblyDir, "System", SeedFileName);
                if (!File.Exists(bundledPath))
                    return false;

                var json = File.ReadAllText(bundledPath);
                // Round-trip through the real model instead of a raw file copy, so a malformed
                // seed file fails loudly here (falls back to KnownAggroNames) rather than writing
                // garbage into the player's actual config.
                var parsed = JsonConvert.DeserializeObject<Dictionary<string, List<AggroRangeConfig>>>(json);
                if (parsed == null || parsed.Count == 0)
                    return false;

                File.WriteAllText(_configPath, JsonConvert.SerializeObject(parsed, Formatting.Indented));
                return true;
            }
            catch (Exception ex)
            {
                DalamudApi.Log.Error(ex, $"[SplatoonManager] Failed to seed from bundled {SeedFileName}, falling back to KnownAggroNames-only seed");
                return false;
            }
        }

        // Original fallback seed: every Eureka mob defaults to Visual (sight) aggro if it's not
        // listed here at all - that's the common case and not worth an entry per mob. Everything
        // in KnownAggroNames is a documented exception, keyed by the actual TC Chinese display
        // name (see that dictionary's comments for sourcing/reasoning per entry).
        private void SeedFromKnownAggroNamesOnly()
        {
            var seed = new Dictionary<string, List<AggroRangeConfig>>
            {
                ["EXAMPLE - 沙巴頓仙人掌怪"] = new()
                {
                    new AggroRangeConfig { Type = AggroType.Aural, Shape = AggroShape.Circle, Radius = 10f, Color = 0x4000FFFFu },
                    new AggroRangeConfig { Type = AggroType.Visual, Shape = AggroShape.Cone, Radius = 11f, ConeHalfAngleDegrees = 45, Color = 0x0D0000FFu },
                },
            };

            foreach (var (name, type) in KnownAggroNames)
            {
                var (shape, color, radius, coneHalfAngle) = AggroTypeDefaults.Get(type);
                seed[name] = new List<AggroRangeConfig>
                {
                    new() { Type = type, Shape = shape, Radius = radius, ConeHalfAngleDegrees = coneHalfAngle, Color = color },
                };
            }

            File.WriteAllText(_configPath, JsonConvert.SerializeObject(seed, Formatting.Indented));
        }

        private void LoadConfig()
        {
            try
            {
                var json = File.ReadAllText(_configPath);
                _aggroRanges = JsonConvert.DeserializeObject<Dictionary<string, List<AggroRangeConfig>>>(json) ?? new();
                _aggroRanges.Remove("EXAMPLE - 沙巴頓仙人掌怪");
                var (renamed, merged) = MonsterNameKey.NormalizeKeys(ref _aggroRanges);
                if (renamed > 0)
                    DalamudApi.Log.Information($"[SplatoonManager] AggroRanges.json 有 {renamed} 個怪物名是 7.25 前的舊中間點寫法，已改用新寫法比對（其中 {merged} 個與新寫法的同名條目合併）");
            }
            catch (Exception ex)
            {
                DalamudApi.Log.Error(ex, $"Failed to load Splatoon aggro range config from {_configPath}");
                _aggroRanges = new();
            }
        }

        public void Dispose()
        {
            DalamudApi.ClientState.TerritoryChanged -= OnTerritoryChanged;
            DalamudApi.Framework.Update -= OnFrameworkUpdate;
            SplatoonCleanup.RemoveElements(LayerName);
            SplatoonCleanup.RemoveElements(MutationLayerName);
            ECommonsMain.Dispose();
        }
    }

    public enum AggroShape
    {
        Circle,
        Cone,
    }

    public enum AggroType
    {
        Aural,
        Visual,
        Magic,
        Blood,
        Other,
    }

    public class AggroRangeConfig
    {
        public AggroType Type { get; set; } = AggroType.Aural;
        public AggroShape Shape { get; set; } = AggroShape.Circle;
        public float Radius { get; set; }
        public int ConeHalfAngleDegrees { get; set; } = 60;
        public uint Color { get; set; } = 0xFFFFFFFF;
        public float Thickness { get; set; } = 2f;

        // NPC Name ID (IBattleNpc.NameId / GetNameId()) - passively backfilled the first time this
        // name is seen alive in SplatoonManager.OnFrameworkUpdate, no manual work needed. 0 means
        // "not collected yet". Once set, drawing switches from substring name-matching (which
        // false-positives whenever one mob's name is a substring of another's, e.g. "達菲妮" vs
        // "豐水達菲妮") to an exact ID match - see SplatoonManager.DrawForCurrentZone.
        public uint NpcId { get; set; }
    }

    public static class AggroTypeDefaults
    {
        // Visual = 11y, 90 degree forward cone (45 either side of facing) - and Aural = 10y
        // circle - are the user-specified defaults for those two types. Magic/Blood have no
        // known default radius (no public source gives one), so they stay at 0 - drawn as
        // nothing until measured via the Debug tab. All of these remain freely editable before
        // committing an entry.
        public static (AggroShape Shape, uint Color, float Radius, int ConeHalfAngleDegrees) Get(AggroType type) => type switch
        {
            AggroType.Aural => (AggroShape.Circle, 0x4000FFFFu, 10f, 60),
            AggroType.Visual => (AggroShape.Cone, 0x0D0000FFu, 11f, 45), // ~5% alpha - cones always draw filled (Splatoon quirk), keep it nearly invisible
            AggroType.Magic => (AggroShape.Circle, 0x40FF7E27u, 0f, 60),
            AggroType.Blood => (AggroShape.Circle, 0x40B000FFu, 0f, 60),
            _ => (AggroShape.Circle, 0x40FFFFFFu, 0f, 60),
        };
    }

    internal static class SplatoonCleanup
    {
        // 卸載期 Splatoon 常比我們先走完，元素已隨它一起消失、沒有東西要清，
        // 而 ECommons 的包裝會對每一次失敗的呼叫寫一行 Warning。
        // 只在「框架正在卸載且確實已斷線」時降級，其餘時機照舊警告。
        public static void RemoveElements(string layerName)
        {
            if (DalamudApi.Framework.IsFrameworkUnloading && !Splatoon.IsConnected())
            {
                DalamudApi.Log.Verbose($"Splatoon 已卸載，略過圖層 {layerName} 的清理");
                return;
            }

            Splatoon.RemoveDynamicElements(layerName);
        }
    }
}
