using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace EurekaTrackerServer;

public sealed class Connection
{
    public required WebSocket Socket { get; init; }
    public bool CanModify { get; set; }
}

public sealed class Room
{
    public readonly ConcurrentDictionary<Connection, byte> Connections = new();
}

// Tracks which live WebSocket connections are subscribed to which tracker instance,
// so writes from one client can be broadcast to everyone else watching the same instance.
public sealed class RoomManager
{
    private readonly ConcurrentDictionary<string, Room> _rooms = new();

    public Connection Join(string instanceId, WebSocket socket, bool canModify)
    {
        var room = _rooms.GetOrAdd(instanceId, _ => new Room());
        var connection = new Connection { Socket = socket, CanModify = canModify };
        room.Connections[connection] = 0;
        return connection;
    }

    public void Leave(string instanceId, Connection connection)
    {
        if (_rooms.TryGetValue(instanceId, out var room))
        {
            room.Connections.TryRemove(connection, out _);
            if (room.Connections.IsEmpty)
                _rooms.TryRemove(instanceId, out _);
        }
    }

    public int ViewerCount(string instanceId) =>
        _rooms.TryGetValue(instanceId, out var room) ? room.Connections.Count : 0;

    public async Task BroadcastAsync(string instanceId, string json, Connection? except = null)
    {
        if (!_rooms.TryGetValue(instanceId, out var room))
            return;

        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        foreach (var connection in room.Connections.Keys)
        {
            if (connection == except)
                continue;
            if (connection.Socket.State != WebSocketState.Open)
                continue;

            try
            {
                await connection.Socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception)
            {
                // Best-effort broadcast; a dead connection will be cleaned up by its own receive loop.
            }
        }
    }
}
