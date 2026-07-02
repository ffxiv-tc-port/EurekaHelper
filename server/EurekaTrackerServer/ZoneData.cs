using System.Text.Json;

namespace EurekaTrackerServer;

public sealed record ZoneMonster(int Id, string BossName, int Level);

public sealed record Zone(int ZoneId, List<ZoneMonster> Monsters);

// Static NM roster per Eureka zone, dumped once from the plugin's own zone/fate definitions
// (EurekaHelper/XIV/Zones/*.cs) so the web frontend can render monster names without
// duplicating the plugin's full respawn-condition logic. See Data/zones.json.
public static class ZoneData
{
    public static readonly List<Zone> Zones = Load();

    private static List<Zone> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "zones.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<Zone>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new List<Zone>();
    }
}
