namespace EurekaTrackerServer;

public sealed class Instance
{
    public required string Id { get; init; }
    public required int ZoneId { get; init; }
    public required string Password { get; init; }
    public bool Public { get; set; }
    public int? DataCenterId { get; set; }
}

// Client -> server messages
public sealed class ClientMessage
{
    public string Type { get; set; } = string.Empty;
    public int? MonsterId { get; set; }
    public long? Time { get; set; }
    public string? Password { get; set; }
    public int? DataCenterId { get; set; }
}

// Server -> client messages (serialized with a discriminating "type" field)
public sealed class InitialPayload
{
    public string Type => "initial";
    public required int ZoneId { get; init; }
    public required Dictionary<int, long> KillTimes { get; init; }
    public required bool Public { get; init; }
    public required int? DataCenterId { get; init; }
    public required bool CanModify { get; init; }
    public required int Viewers { get; init; }
}

public sealed class KillTimesUpdate
{
    public string Type => "kill_times";
    public required Dictionary<int, long> KillTimes { get; init; }
}

public sealed class VisibilityUpdate
{
    public string Type => "visibility";
    public required bool Public { get; init; }
    public required int? DataCenterId { get; init; }
}

public sealed class ViewersUpdate
{
    public string Type => "viewers";
    public required int Count { get; init; }
}

public sealed class PasswordSetResult
{
    public string Type => "password_set";
    public required bool Success { get; init; }
    public string? Password { get; init; }
}

public sealed class ErrorMessage
{
    public string Type => "error";
    public required string Message { get; init; }
}
