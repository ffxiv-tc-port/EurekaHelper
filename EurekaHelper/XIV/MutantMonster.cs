using System.Collections.Generic;
using System.Numerics;

namespace EurekaHelper.XIV
{
    // Which time-of-day window (if any) a mutant monster can trigger under a given weather.
    public enum MutationWindow
    {
        None,
        Day,
        Night,
        Any,
    }

    // Which of the two outcomes a specific mob is fixed to when it triggers - sourced from the
    // same wiki page (each mob's row shows exactly one of the two status icons), not something
    // that varies per-trigger.
    public enum MutationOutcome
    {
        Adapted, // 環境適應
        Mutated, // 突然變異
    }

    // A regular field mob that can temporarily buff into a stronger "變異" (mutant) form under
    // certain weather/time conditions - unrelated to NM/aggro tracking. Whether it's CURRENTLY
    // showing the mutated status is read live off its status effects (see SplatoonManager's
    // AdaptedStatusName/MutatedStatusName); WeatherWindows here is the static "which
    // weather+time-of-day combos can even trigger it" table (sourced from a community wiki page),
    // used to highlight it BEFORE it actually procs.
    public class MutantMonster
    {
        public int Level { get; private set; }
        public string Name { get; private set; }
        public ushort TerritoryId { get; private set; }
        public ushort MapId { get; private set; }

        // Null for mobs the wiki lists as roaming multiple checkpoints across the zone (e.g.
        // 虛無炎龍/虛無冰雪龍) rather than a single fixed spot - still worth listing/tracking
        // status for, just nothing to navigate to.
        public Vector2? Position { get; private set; }
        public IReadOnlyDictionary<EurekaWeather, MutationWindow> WeatherWindows { get; private set; }
        public MutationOutcome PredictedOutcome { get; private set; }

        private MutantMonster(int level, string name, ushort territoryId, ushort mapId, Vector2? position,
            MutationOutcome predictedOutcome, Dictionary<EurekaWeather, MutationWindow> weatherWindows)
        {
            Level = level;
            Name = name;
            TerritoryId = territoryId;
            MapId = mapId;
            Position = position;
            PredictedOutcome = predictedOutcome;
            WeatherWindows = weatherWindows;
        }

        // Hydatos-style: 5 weathers (FairSkies, Showers, Gloom, Thunderstorms, Snow).
        public static MutantMonster ForHydatos(int level, string name, ushort territoryId, ushort mapId, Vector2? position,
            MutationOutcome predictedOutcome,
            MutationWindow fairSkies, MutationWindow showers, MutationWindow gloom, MutationWindow thunderstorms, MutationWindow snow) =>
            new(level, name, territoryId, mapId, position, predictedOutcome, new Dictionary<EurekaWeather, MutationWindow>
            {
                [EurekaWeather.FairSkies] = fairSkies,
                [EurekaWeather.Showers] = showers,
                [EurekaWeather.Gloom] = gloom,
                [EurekaWeather.Thunderstorms] = thunderstorms,
                [EurekaWeather.Snow] = snow,
            });

        // Pyros-style: 6 weathers (FairSkies, HeatWaves, Thunder, Blizzards, UmbralWind, Snow).
        public static MutantMonster ForPyros(int level, string name, ushort territoryId, ushort mapId, Vector2? position,
            MutationOutcome predictedOutcome,
            MutationWindow fairSkies, MutationWindow heatWaves, MutationWindow thunder, MutationWindow blizzards, MutationWindow umbralWind, MutationWindow snow) =>
            new(level, name, territoryId, mapId, position, predictedOutcome, new Dictionary<EurekaWeather, MutationWindow>
            {
                [EurekaWeather.FairSkies] = fairSkies,
                [EurekaWeather.HeatWaves] = heatWaves,
                [EurekaWeather.Thunder] = thunder,
                [EurekaWeather.Blizzards] = blizzards,
                [EurekaWeather.UmbralWind] = umbralWind,
                [EurekaWeather.Snow] = snow,
            });

        // Pagos-style: 6 weathers (FairSkies, Fog, HeatWaves, Thunder, Snow, Blizzards).
        public static MutantMonster ForPagos(int level, string name, ushort territoryId, ushort mapId, Vector2? position,
            MutationOutcome predictedOutcome,
            MutationWindow fairSkies, MutationWindow fog, MutationWindow heatWaves, MutationWindow thunder, MutationWindow snow, MutationWindow blizzards) =>
            new(level, name, territoryId, mapId, position, predictedOutcome, new Dictionary<EurekaWeather, MutationWindow>
            {
                [EurekaWeather.FairSkies] = fairSkies,
                [EurekaWeather.Fog] = fog,
                [EurekaWeather.HeatWaves] = heatWaves,
                [EurekaWeather.Thunder] = thunder,
                [EurekaWeather.Snow] = snow,
                [EurekaWeather.Blizzards] = blizzards,
            });

        public bool IsEligibleNow(EurekaWeather currentWeather, bool isNight)
        {
            if (!WeatherWindows.TryGetValue(currentWeather, out var window))
                return false;

            return window switch
            {
                MutationWindow.Any => true,
                MutationWindow.Day => !isNight,
                MutationWindow.Night => isNight,
                _ => false,
            };
        }
    }
}
