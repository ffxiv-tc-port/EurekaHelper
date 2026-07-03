using ECommons;
using ECommons.SplatoonAPI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        private readonly string _configPath;
        private Dictionary<string, List<AggroRangeConfig>> _aggroRanges = new();
        private bool _splatoonReady = false;

        public SplatoonManager()
        {
            _configPath = Path.Combine(DalamudApi.PluginInterface.GetPluginConfigDirectory(), "AggroRanges.json");
            EnsureConfigFileExists();
            LoadConfig();

            ECommonsMain.Init(DalamudApi.PluginInterface, EurekaHelper.Plugin, Module.SplatoonAPI);
            Splatoon.SetOnConnect(OnSplatoonConnect);

            DalamudApi.ClientState.TerritoryChanged += OnTerritoryChanged;
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
                DrawForCurrentZone();
        }

        // Redraws every configured NM's aggro ranges for the zone we're currently in. Safe to
        // call repeatedly (e.g. after reloading the config file); Splatoon replaces elements
        // under the same layer name rather than stacking duplicates.
        public void DrawForCurrentZone()
        {
            if (!_splatoonReady || !Splatoon.IsConnected())
                return;

            if (!Utils.IsPlayerInEurekaZone(DalamudApi.ClientState.TerritoryType))
                return;

            var elements = new List<Element>();
            foreach (var (bossName, ranges) in _aggroRanges)
            {
                foreach (var range in ranges)
                {
                    if (range.Radius <= 0f)
                        continue; // radius not measured yet - see AggroRanges.json / Debug tab

                    // NOTE: Splatoon's ImGui-Legacy renderer hardcodes Cone elements to always
                    // draw filled, ignoring Element.Filled entirely (confirmed in Splatoon's own
                    // source, Splatoon/RenderEngines/ImGuiLegacy/ImGuiLegacyRenderer.cs AddCone:
                    // `new DisplayObjectCone(..., e.color, true)` - "true" is hardcoded, not
                    // e.Filled). Circle elements DO respect Filled correctly. Until that's fixed
                    // upstream (or the user's on the DirectX11 render engine, unverified), always
                    // draw a circle outline instead of a cone/wedge - loses directionality for
                    // "Visual"-type entries but reliably never renders as a solid fill.
                    elements.Add(new Element(ElementType.CircleRelativeToActorPosition)
                    {
                        refActorType = RefActorType.IGameObjectWithSpecifiedAttribute,
                        refActorName = bossName,
                        radius = range.Radius,
                        color = range.Color,
                        Filled = false, // outline only, never a filled disc
                        thicc = range.Thickness,
                    });
                }
            }

            if (elements.Count > 0)
                Splatoon.AddDynamicElements(LayerName, elements.ToArray(), -2); // -2 = no auto-expiry, we manage lifetime via territory change
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
            // here at all - that's the common case and not worth an entry per mob. The handful
            // of named spawner mobs below are the well-documented exceptions to that default
            // (see community wiki consensus: "Sprites" that only spawn in specific weather use
            // Magic aggro; Ashkin/undead-named mobs that only spawn at night use Blood/low-HP
            // aggro), cross-checked against this repo's own EurekaFate data (SpawnByRequiredWeather
            // / SpawnByRequiredNight flags in XIV/Zones/*.cs already flag exactly these mobs).
            // Radius is intentionally 0 (drawn as nothing, see DrawForCurrentZone) - no public
            // source gives per-mob aggro *distance*, only aggro *type*. Use the Debug tab to lock
            // one in-game and measure/tune the real radius, which'll overwrite the 0 here.
            var seed = new Dictionary<string, List<AggroRangeConfig>>
            {
                ["EXAMPLE - Sabotender Corrido"] = new()
                {
                    new AggroRangeConfig { Type = AggroType.Aural, Shape = AggroShape.Circle, Radius = 10f, Color = 0xFF00FFFFu },
                    new AggroRangeConfig { Type = AggroType.Visual, Shape = AggroShape.Cone, Radius = 15f, ConeHalfAngleDegrees = 45, Color = 0xFF0000FFu },
                },

                // Weather-gated "Sprite" adds -> Magic aggro (aggros on nearby spell cast)
                ["Typhoon Sprite"] = new() { new AggroRangeConfig { Type = AggroType.Magic, Radius = 0f } },   // Anemos, spawns Jahannam, requires Gales
                ["Snowmelt Sprite"] = new() { new AggroRangeConfig { Type = AggroType.Magic, Radius = 0f } },  // Pagos, spawns Anapos, requires Fog
                ["Thunderstorm Sprite"] = new() { new AggroRangeConfig { Type = AggroType.Magic, Radius = 0f } }, // Pyros, spawns Flauros, requires Thunder

                // Night-only + undead-named spawners -> Blood/low-HP aggro (ashkin convention)
                ["Val Specter"] = new() { new AggroRangeConfig { Type = AggroType.Blood, Radius = 0f } },      // Anemos, spawns Lamashtu
                ["Shadow Wraith"] = new() { new AggroRangeConfig { Type = AggroType.Blood, Radius = 0f } },    // Anemos, spawns Pazuzu
                ["Duskfall Dullahan"] = new() { new AggroRangeConfig { Type = AggroType.Blood, Radius = 0f } }, // Anemos, spawns The White Rider
                ["Hydatos Wraith"] = new() { new AggroRangeConfig { Type = AggroType.Blood, Radius = 0f } },   // Hydatos, spawns King Goldemar
                ["Val Corpse"] = new() { new AggroRangeConfig { Type = AggroType.Blood, Radius = 0f } },       // Pagos, spawns Louhi
                ["Pyros Bhoot"] = new() { new AggroRangeConfig { Type = AggroType.Blood, Radius = 0f } },      // Pyros, spawns Leucosia
            };

            File.WriteAllText(_configPath, JsonConvert.SerializeObject(seed, Formatting.Indented));
        }

        private void LoadConfig()
        {
            try
            {
                var json = File.ReadAllText(_configPath);
                _aggroRanges = JsonConvert.DeserializeObject<Dictionary<string, List<AggroRangeConfig>>>(json) ?? new();
                _aggroRanges.Remove("EXAMPLE - Sabotender Corrido");
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
            AggroType.Aural => (AggroShape.Circle, 0xFF00FFFFu, 10f, 60),
            AggroType.Visual => (AggroShape.Cone, 0xFF0000FFu, 15f, 45),
            AggroType.Magic => (AggroShape.Circle, 0xFFFF7E27u, 0f, 60),
            AggroType.Blood => (AggroShape.Circle, 0xFFB000FFu, 0f, 60),
            _ => (AggroShape.Circle, 0xFFFFFFFFu, 0f, 60),
        };
    }
}
