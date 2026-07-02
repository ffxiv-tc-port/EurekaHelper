using Microsoft.Data.Sqlite;

namespace EurekaTrackerServer;

public sealed class Db
{
    private readonly string _connectionString;

    public Db(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        var dbPath = Path.Combine(dataDirectory, "tracker.db");
        _connectionString = $"Data Source={dbPath}";
        Initialize();
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private void Initialize()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS instances (
                id TEXT PRIMARY KEY,
                zone_id INTEGER NOT NULL,
                public INTEGER NOT NULL DEFAULT 0,
                data_center_id INTEGER NULL,
                created_at INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS kill_times (
                instance_id TEXT NOT NULL,
                monster_id INTEGER NOT NULL,
                kill_time INTEGER NOT NULL,
                PRIMARY KEY (instance_id, monster_id),
                FOREIGN KEY (instance_id) REFERENCES instances(id)
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public Instance CreateInstance(string id, int zoneId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO instances (id, zone_id, public, data_center_id, created_at) VALUES ($id, $zoneId, 0, NULL, $createdAt)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$zoneId", zoneId);
        cmd.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        cmd.ExecuteNonQuery();

        return new Instance { Id = id, ZoneId = zoneId };
    }

    public Instance? GetInstance(string id)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT zone_id, public, data_center_id FROM instances WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;

        return new Instance
        {
            Id = id,
            ZoneId = reader.GetInt32(0),
            Public = reader.GetInt32(1) != 0,
            DataCenterId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
        };
    }

    public List<string> GetPublicInstanceIds(int zoneId, int dataCenterId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM instances WHERE public = 1 AND zone_id = $zoneId AND data_center_id = $dataCenterId";
        cmd.Parameters.AddWithValue("$zoneId", zoneId);
        cmd.Parameters.AddWithValue("$dataCenterId", dataCenterId);
        using var reader = cmd.ExecuteReader();

        var result = new List<string>();
        while (reader.Read())
            result.Add(reader.GetString(0));

        return result;
    }

    public bool InstanceExists(string id)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM instances WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteScalar() != null;
    }

    public Dictionary<int, long> GetKillTimes(string instanceId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT monster_id, kill_time FROM kill_times WHERE instance_id = $instanceId";
        cmd.Parameters.AddWithValue("$instanceId", instanceId);
        using var reader = cmd.ExecuteReader();

        var result = new Dictionary<int, long>();
        while (reader.Read())
            result[reader.GetInt32(0)] = reader.GetInt64(1);

        return result;
    }

    public void SetKillTime(string instanceId, int monsterId, long time)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO kill_times (instance_id, monster_id, kill_time) VALUES ($instanceId, $monsterId, $time)
            ON CONFLICT (instance_id, monster_id) DO UPDATE SET kill_time = excluded.kill_time
            """;
        cmd.Parameters.AddWithValue("$instanceId", instanceId);
        cmd.Parameters.AddWithValue("$monsterId", monsterId);
        cmd.Parameters.AddWithValue("$time", time);
        cmd.ExecuteNonQuery();
    }

    public void ResetKill(string instanceId, int monsterId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM kill_times WHERE instance_id = $instanceId AND monster_id = $monsterId";
        cmd.Parameters.AddWithValue("$instanceId", instanceId);
        cmd.Parameters.AddWithValue("$monsterId", monsterId);
        cmd.ExecuteNonQuery();
    }

    public void ResetAll(string instanceId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM kill_times WHERE instance_id = $instanceId";
        cmd.Parameters.AddWithValue("$instanceId", instanceId);
        cmd.ExecuteNonQuery();
    }

    public void SetVisibility(string instanceId, bool isPublic, int? dataCenterId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE instances SET public = $public, data_center_id = $dataCenterId WHERE id = $id";
        cmd.Parameters.AddWithValue("$public", isPublic ? 1 : 0);
        cmd.Parameters.AddWithValue("$dataCenterId", (object?)dataCenterId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", instanceId);
        cmd.ExecuteNonQuery();
    }

    public void CopyKillTimes(string fromInstanceId, string toInstanceId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO kill_times (instance_id, monster_id, kill_time)
            SELECT $to, monster_id, kill_time FROM kill_times WHERE instance_id = $from
            """;
        cmd.Parameters.AddWithValue("$to", toInstanceId);
        cmd.Parameters.AddWithValue("$from", fromInstanceId);
        cmd.ExecuteNonQuery();
    }
}
