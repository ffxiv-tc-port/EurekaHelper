using ECommons;
using ECommons.SplatoonAPI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;

namespace EurekaHelper.System
{
    // Draws Eureka NM aggro-detection ranges (aural/visual/magic/blood) in-game via Splatoon's
    // public IPC (ECommons.SplatoonAPI), the same mechanism PalacePal uses to draw trap markers -
    // no manual Splatoon layout import needed.
    //
    // Data gap: per-NM aggro type/radius isn't known yet (see AggroRanges.json). This class only
    // wires up the IPC plumbing and reacts to whatever's in that file; it ships empty/example-only
    // until someone fills in verified values.
    public class SplatoonManager : IDisposable
    {
        private const string LayerName = "EurekaHelper.AggroRanges";

        // IMPORTANT: this is a TC (Traditional Chinese) client build, so IBattleNpc.Name.TextValue
        // from the live object table is the CHINESE display name, not the English one used
        // elsewhere in this codebase (EurekaFate.BossName/SpawnedBy, Loc.cs's EurekaNamesZhTw,
        // etc. - all English-keyed). Splatoon's refActorName has to match the actual live display
        // name too, or it silently binds to nothing. So this classification dictionary is keyed
        // by the CHINESE name specifically - matching against English substrings here (as an
        // earlier version of this file did) would never fire on this client.
        //
        // Sourced from community-compiled Eureka aggro-type notes (user-provided, cross-zone
        // aural/blood/magic breakdown with level ranges and NM-trigger context) rather than an
        // official source, since none publishes a full per-mob table. Radius is intentionally
        // left at each type's default from AggroTypeDefaults (0 for Magic/Blood - no known
        // baseline distance, drawn as nothing until measured via the Debug tab; Aural/Visual do
        // have a default and draw immediately).
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

            // 魔法偵測型/元精系 (Magic) - only spawn in specific weather, aggro on any nearby
            // spell/magic-category action (including healing, Logos actions). Core counter:
            // don't cast anything near them.
            ["常風元精"] = AggroType.Magic,       // Anemos, Showers/Gales
            ["冰島元精"] = AggroType.Magic,       // Pagos, Thunder/Blizzards
            ["湧火元精"] = AggroType.Magic,       // Pyros, Heat Waves/Fair Skies
            ["豐水元精"] = AggroType.Magic,       // Hydatos, Thunderstorms/Squall
        };

        private readonly string _configPath;
        private readonly string _seenMonstersPath;
        private Dictionary<string, List<AggroRangeConfig>> _aggroRanges = new();
        private HashSet<string> _seenMonsters = new();
        private bool _splatoonReady = false;

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

        private void OnSplatoonConnect()
        {
            _splatoonReady = true;
            DrawForCurrentZone();
        }

        private void OnTerritoryChanged(ushort territoryId)
        {
            Splatoon.RemoveDynamicElements(LayerName);

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

        // Passively records every battle NM/mob name encountered in the zone (so you can see
        // coverage grow over a play session without manually locking each one), and
        // auto-registers an aggro-type entry for names matching a known pattern (see
        // MagicNamePatterns/BloodNamePatterns) - still radius 0 (undrawn) until measured via the
        // Debug tab, same as the hand-seeded entries.
        // Diagnostic counters, shown in the Debug tab's Seen Monsters section, so a stuck "0
        // discovered" can be pinned to an exact pipeline stage from a screenshot instead of
        // guessing blind (this got bounced on several wrong guesses already - level scale
        // mismatch, IsTargetable flakiness - each only discoverable after the fact).
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

            foreach (var obj in DalamudApi.ObjectTable)
            {
                totalObjects++;

                if (obj is not IBattleNpc battleNpc)
                    continue;
                battleNpcs++;

                // NOTE: BOTH BattleNpcKind != Enemy AND OwnerId != 0 were tried and each, in turn,
                // filtered out 100% of BattleNpc in testing (18/18, then 41/41) - the Dalamud-mapped
                // BattleNpcKind enum wrapper appears broken on this build's older API level. The raw
                // SubKind byte (same underlying game value, just read without the enum wrapper) does
                // work: 5 = Enemy, 2 = Pet (fairy/egi/automaton), 3 = Chocobo companion, 9 = Buddy/trust,
                // 11 = Helper. Filtering on the raw byte instead of the wrapper is what actually excludes
                // summoned pets without also rejecting real monsters.
                if (battleNpc.SubKind != 5)
                    continue;
                enemyKind++;

                // Skip dead bodies (corpse still exists briefly before despawning). NOTE: an
                // earlier version of this also skipped !IsTargetable to catch transient nameless
                // skill-effect actors, but IsTargetable can be flaky (LoS-dependent) for regular
                // idle mobs too and ended up filtering out everything - removed. The Splatoon
                // Element itself still sets onlyTargetable=true (see DrawForCurrentZone), which
                // covers "stop drawing once dead" without needing this scan-time check too.
                if (battleNpc.IsDead)
                    continue;
                aliveEnemies++;

                var name = battleNpc.Name.TextValue;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                // NOTE: previously filtered mobs 3+ levels below the player here, but Eureka
                // mobs are leveled on the separate Elemental Level track, not the character's
                // actual class/job level - comparing IBattleNpc.Level against
                // LocalPlayer.Level compared two unrelated scales and filtered out nearly every
                // Eureka mob (job level 90+ vs. Elemental level ~20-65). Removed until there's a
                // reliable way to read the player's Elemental Level for a correct comparison.

                // Logging "seen" and actually having an AggroRanges entry are separate concerns -
                // deliberately NOT gating classification on _seenMonsters.Add() here. A name
                // recorded as seen under an older build (before Visual got auto-registered, or if
                // an entry was later deleted via the Debug tab) would otherwise be permanently
                // stuck with no entry forever, since HashSet.Add() only returns true once per
                // name for the life of the persisted SeenMonsters.json. AutoClassify already
                // no-ops cheaply via _aggroRanges.ContainsKey, so calling it unconditionally for
                // every visible monster every tick is safe and self-heals that case.
                if (_seenMonsters.Add(name))
                    newNames = true;

                if (AutoClassify(name))
                    newlyClassified = true;
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
                // Splatoon's onlyTargetable filter is only re-evaluated against the current
                // dynamic element set, not against the live object table on every render frame -
                // a dead monster's circle otherwise keeps showing until something else happens to
                // trigger a redraw (e.g. a brand-new monster gets classified). Force a periodic
                // remove+re-add so dead monsters' aggro ranges clear out within ~2 seconds instead
                // of lingering indefinitely.
                Splatoon.RemoveDynamicElements(LayerName);
                DrawForCurrentZone();
                _nextForcedRedraw = DateTime.UtcNow + RedrawInterval;
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

        public IReadOnlyCollection<string> GetSeenMonsters() => _seenMonsters;

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

        // Skip drawing entirely once the player is farther than MaxDrawDistance from the
        // aggro-range's own reference actor - there's no point rendering ranges around monsters
        // nowhere near the player, and it keeps distant zone-wide clutter off screen.
        //
        // NOTE: Splatoon compares DistanceSourceX/Y/Z against gameObject.GetPositionXZY(), which
        // swaps Y (height) and Z (depth) from Dalamud's normal Position layout (confirmed by
        // decompiling Splatoon.dll - Utility.GetPositionXZY literally swaps them). Feeding it raw
        // Position.X/Y/Z here compared height against depth and vice versa, so nearly every
        // monster measured as "too far" the moment there was any elevation difference - which
        // hid everything. Swapping Y/Z here to match fixes it.
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

        public void DrawForCurrentZone()
        {
            if (!_splatoonReady || !Splatoon.IsConnected())
                return;

            if (!Utils.IsPlayerInEurekaZone(DalamudApi.ClientState.TerritoryType))
                return;

            var playerPos = DalamudApi.ClientState.LocalPlayer?.Position;

            var elements = new List<Element>();
            foreach (var (bossName, ranges) in _aggroRanges)
            {
                foreach (var range in ranges)
                {
                    if (range.Radius <= 0f)
                        continue; // radius not measured yet - see AggroRanges.json / Debug tab

                    if (range.Shape == AggroShape.Cone)
                    {
                        // NOTE: Splatoon's ImGui-Legacy renderer hardcodes Cone elements to always
                        // draw filled, ignoring Element.Filled entirely (confirmed in Splatoon's
                        // own source, Splatoon/RenderEngines/ImGuiLegacy/ImGuiLegacyRenderer.cs
                        // AddCone: `new DisplayObjectCone(..., e.color, true)` - "true" is
                        // hardcoded, not e.Filled). Rather than fight that, embrace it: draw the
                        // cone filled but with a low-alpha color (see AggroTypeDefaults - Visual's
                        // default color has ~30% alpha) so it reads as a light directional tint
                        // instead of an opaque wedge.
                        var cone = new Element(ElementType.ConeRelativeToObjectPosition)
                        {
                            refActorType = RefActorType.IGameObjectWithSpecifiedAttribute,
                            refActorName = bossName,
                            radius = range.Radius,
                            coneAngleMin = -range.ConeHalfAngleDegrees,
                            coneAngleMax = range.ConeHalfAngleDegrees,
                            includeRotation = true,
                            color = range.Color,
                            Filled = true,
                            thicc = range.Thickness,
                            onlyTargetable = true, // stop drawing once the actor dies/despawns
                        };
                        ApplyDistanceLimit(cone, playerPos);
                        elements.Add(cone);
                    }
                    else
                    {
                        var circle = new Element(ElementType.CircleRelativeToActorPosition)
                        {
                            refActorType = RefActorType.IGameObjectWithSpecifiedAttribute,
                            refActorName = bossName,
                            radius = range.Radius,
                            color = range.Color,
                            Filled = false, // Circle elements DO respect Filled correctly - outline only
                            thicc = range.Thickness,
                            onlyTargetable = true, // stop drawing once the actor dies/despawns
                        };
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
            _aggroRanges.TryGetValue(bossName, out var list) ? list : Array.Empty<AggroRangeConfig>();

        // Used by the Debug tab: lock a target in-game, tune shape/radius/color while watching
        // the live Splatoon overlay, then commit it here once it looks right.
        public void AddEntry(string bossName, AggroRangeConfig config)
        {
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

        private void EnsureConfigFileExists()
        {
            if (File.Exists(_configPath))
                return;

            // Seed data. Every Eureka mob defaults to Visual (sight) aggro if it's not listed
            // here at all - that's the common case and not worth an entry per mob. Everything in
            // KnownAggroNames is a documented exception, keyed by the actual TC Chinese display
            // name (see that dictionary's comments for sourcing/reasoning per entry).
            var seed = new Dictionary<string, List<AggroRangeConfig>>
            {
                ["EXAMPLE - 沙巴頓仙人掌怪"] = new()
                {
                    new AggroRangeConfig { Type = AggroType.Aural, Shape = AggroShape.Circle, Radius = 10f, Color = 0x4000FFFFu },
                    new AggroRangeConfig { Type = AggroType.Visual, Shape = AggroShape.Cone, Radius = 15f, ConeHalfAngleDegrees = 45, Color = 0x0D0000FFu },
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
            Splatoon.RemoveDynamicElements(LayerName);
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
    }

    public static class AggroTypeDefaults
    {
        // Visual = 15y, 90 degree forward cone (45 either side of facing) - and Aural = 10y
        // circle - are the user-specified defaults for those two types. Magic/Blood have no
        // known default radius (no public source gives one), so they stay at 0 - drawn as
        // nothing until measured via the Debug tab. All of these remain freely editable before
        // committing an entry.
        public static (AggroShape Shape, uint Color, float Radius, int ConeHalfAngleDegrees) Get(AggroType type) => type switch
        {
            AggroType.Aural => (AggroShape.Circle, 0x4000FFFFu, 10f, 60),
            AggroType.Visual => (AggroShape.Cone, 0x0D0000FFu, 15f, 45), // ~5% alpha - cones always draw filled (Splatoon quirk), keep it nearly invisible
            AggroType.Magic => (AggroShape.Circle, 0x40FF7E27u, 0f, 60),
            AggroType.Blood => (AggroShape.Circle, 0x40B000FFu, 0f, 60),
            _ => (AggroShape.Circle, 0x40FFFFFFu, 0f, 60),
        };
    }
}
