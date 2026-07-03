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
                    elements.Add(new Element(range.Shape == AggroShape.Cone ? ElementType.ConeRelativeToObjectPosition : ElementType.CircleRelativeToActorPosition)
                    {
                        refActorType = RefActorType.IGameObjectWithSpecifiedAttribute,
                        refActorName = bossName,
                        radius = range.Radius,
                        coneAngleMin = -range.ConeHalfAngleDegrees,
                        coneAngleMax = range.ConeHalfAngleDegrees,
                        includeRotation = range.Shape == AggroShape.Cone,
                        color = range.Color,
                        Filled = false,
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

            // Example entry only - NOT verified real aggro data, just illustrates the file shape.
            var example = new Dictionary<string, List<AggroRangeConfig>>
            {
                ["EXAMPLE - Sabotender Corrido"] = new()
                {
                    new AggroRangeConfig { Type = AggroType.Aural, Shape = AggroShape.Circle, Radius = 12f, Color = 0xFF00FFFFu },
                    new AggroRangeConfig { Type = AggroType.Visual, Shape = AggroShape.Cone, Radius = 15f, ConeHalfAngleDegrees = 60, Color = 0xFF0000FFu },
                },
            };

            File.WriteAllText(_configPath, JsonConvert.SerializeObject(example, Formatting.Indented));
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
    }

    public static class AggroTypeDefaults
    {
        // Just sane starting points so the Debug tab isn't blank when you switch type -
        // radius/color are always freely editable/pickable before committing an entry.
        public static (AggroShape Shape, uint Color) Get(AggroType type) => type switch
        {
            AggroType.Aural => (AggroShape.Circle, 0xFF00FFFFu),
            AggroType.Visual => (AggroShape.Cone, 0xFF0000FFu),
            AggroType.Magic => (AggroShape.Circle, 0xFFFF7E27u),
            AggroType.Blood => (AggroShape.Circle, 0xFFB000FFu),
            _ => (AggroShape.Circle, 0xFFFFFFFFu),
        };
    }
}
