using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EurekaTrackerServer;

var builder = WebApplication.CreateBuilder(args);

var dataDirectory = builder.Configuration["DataDirectory"] ?? "/data";
var db = new Db(dataDirectory);
var rooms = new RoomManager();

builder.Services.AddSingleton(db);
builder.Services.AddSingleton(rooms);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });

var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

// Static NM roster per zone (id/name/level), for the web frontend to render without
// duplicating the plugin's full respawn-condition logic.
app.MapGet("/api/zones", () => Results.Ok(ZoneData.Zones));

// Create (or copy) a tracker instance. No password - anyone with the share code can join
// and edit; see EditorsUpdate for the "who's currently editing" courtesy signal instead.
app.MapPost("/api/instances", (CreateInstanceRequest request, Db db) =>
{
    if (!string.IsNullOrWhiteSpace(request.CopyFrom))
    {
        var source = db.GetInstance(request.CopyFrom);
        if (source is null)
            return Results.NotFound(new { error = "source instance not found" });

        var copy = db.CreateInstance(GenerateUniqueId(db), source.ZoneId);
        db.CopyKillTimes(source.Id, copy.Id);
        return Results.Ok(new { id = copy.Id });
    }

    var created = db.CreateInstance(GenerateUniqueId(db), request.ZoneId);
    return Results.Ok(new { id = created.Id });
});

// List public tracker share-codes for a given zone + datacenter (used by the plugin's /etrackers command).
app.MapGet("/api/instances", (int zoneId, int dataCenterId, Db db) =>
{
    return Results.Ok(new { ids = db.GetPublicInstanceIds(zoneId, dataCenterId) });
});

// Real-time tracker channel: join, receive current state, push kill-time / visibility updates.
app.Map("/ws/{instanceId}", async (HttpContext context, string instanceId, Db db, RoomManager rooms) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var instance = db.GetInstance(instanceId);
    if (instance is null)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var connection = rooms.Join(instanceId, socket);

    try
    {
        var initial = new InitialPayload
        {
            ZoneId = instance.ZoneId,
            KillTimes = db.GetKillTimes(instanceId),
            Public = instance.Public,
            DataCenterId = instance.DataCenterId,
            Viewers = rooms.ViewerCount(instanceId),
            Editors = rooms.EditorCount(instanceId),
        };
        await SendAsync(socket, initial, jsonOptions);
        await rooms.BroadcastAsync(instanceId, Serialize(new ViewersUpdate { Count = rooms.ViewerCount(instanceId) }, jsonOptions));

        var buffer = new byte[4096];
        while (socket.State == WebSocketState.Open)
        {
            using var messageStream = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;
                messageStream.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close)
                break;

            messageStream.Seek(0, SeekOrigin.Begin);
            ClientMessage? message;
            try
            {
                message = await JsonSerializer.DeserializeAsync<ClientMessage>(messageStream, jsonOptions);
            }
            catch (JsonException)
            {
                await SendAsync(socket, new ErrorMessage { Message = "invalid_json" }, jsonOptions);
                continue;
            }

            if (message is null)
                continue;

            await HandleMessage(instanceId, connection, message, db, rooms, jsonOptions);
        }
    }
    finally
    {
        rooms.Leave(instanceId, connection);
        if (socket.State == WebSocketState.Open)
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None);

        await rooms.BroadcastAsync(instanceId, Serialize(new ViewersUpdate { Count = rooms.ViewerCount(instanceId) }, jsonOptions));
        await rooms.BroadcastAsync(instanceId, Serialize(new EditorsUpdate { Count = rooms.EditorCount(instanceId) }, jsonOptions));
    }
});

// Pretty tracker links (/ABC123) aren't real files - let the SPA's client-side router
// (wwwroot/app.js, reading location.pathname) handle them. Doesn't shadow /api or /ws since
// those routes are already matched above.
app.MapFallbackToFile("index.html");

app.Run();

static async Task HandleMessage(string instanceId, Connection connection, ClientMessage message, Db db, RoomManager rooms, JsonSerializerOptions jsonOptions)
{
    switch (message.Type)
    {
        case "set_editing":
            {
                if (message.Editing is null)
                    break;

                connection.IsEditing = message.Editing.Value;
                await rooms.BroadcastAsync(instanceId, Serialize(new EditorsUpdate { Count = rooms.EditorCount(instanceId) }, jsonOptions));
                break;
            }

        case "set_kill_time":
            {
                if (message.MonsterId is null || message.Time is null)
                    break;

                db.SetKillTime(instanceId, message.MonsterId.Value, message.Time.Value);
                await rooms.BroadcastAsync(instanceId, Serialize(new KillTimesUpdate { KillTimes = db.GetKillTimes(instanceId) }, jsonOptions));
                break;
            }

        case "reset_kill":
            {
                if (message.MonsterId is null)
                    break;

                db.ResetKill(instanceId, message.MonsterId.Value);
                await rooms.BroadcastAsync(instanceId, Serialize(new KillTimesUpdate { KillTimes = db.GetKillTimes(instanceId) }, jsonOptions));
                break;
            }

        case "reset_all":
            {
                db.ResetAll(instanceId);
                await rooms.BroadcastAsync(instanceId, Serialize(new KillTimesUpdate { KillTimes = db.GetKillTimes(instanceId) }, jsonOptions));
                break;
            }

        case "set_visibility":
            {
                var isPublic = message.DataCenterId is not null;
                db.SetVisibility(instanceId, isPublic, message.DataCenterId);
                await rooms.BroadcastAsync(instanceId, Serialize(new VisibilityUpdate { Public = isPublic, DataCenterId = message.DataCenterId }, jsonOptions));
                break;
            }

        default:
            await SendAsync(connection.Socket, new ErrorMessage { Message = $"unknown_type:{message.Type}" }, jsonOptions);
            break;
    }
}

static string Serialize<T>(T value, JsonSerializerOptions options) => JsonSerializer.Serialize(value, options);

static Task SendAsync<T>(WebSocket socket, T value, JsonSerializerOptions options)
{
    var bytes = Encoding.UTF8.GetBytes(Serialize(value, options));
    return socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
}

static string GenerateUniqueId(Db db)
{
    string id;
    do
    {
        id = GenerateId();
    } while (db.InstanceExists(id));

    return id;
}

static string GenerateId()
{
    const string alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"; // no 0/O/1/I to avoid ambiguity
    Span<byte> randomBytes = stackalloc byte[6];
    RandomNumberGenerator.Fill(randomBytes);

    var chars = new char[6];
    for (var i = 0; i < 6; i++)
        chars[i] = alphabet[randomBytes[i] % alphabet.Length];

    return new string(chars);
}

sealed record CreateInstanceRequest(int ZoneId, string? CopyFrom);
