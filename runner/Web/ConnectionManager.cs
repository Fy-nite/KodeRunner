using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KodeRunner
{
    public class ConnectionManager
    {
        private readonly ConcurrentDictionary<string, WebSocketConnection> _connections = new();

        public class WebSocketConnection
        {
            public string Id { get; }
            public string Type { get; }
            public WebSocket Socket { get; }
            public DateTime ConnectedAt { get; }
            public string ClientInfo { get; set; }

            public WebSocketConnection(string id, string type, WebSocket socket)
            {
                Id = id;
                Type = type;
                Socket = socket;
                ConnectedAt = DateTime.UtcNow;
                ClientInfo = "Unknown";
            }
        }

        public string AddConnection(string type, WebSocket socket)
        {
            var id = GenerateConnectionId();
            var connection = new WebSocketConnection(id, type, socket);
            _connections.TryAdd(id, connection);
            Logger.Log($"New {type} connection: {id}", "Info");
            return id;
        }

        public async Task DisconnectById(string id)
        {
            if (_connections.TryRemove(id, out var connection))
            {
                try
                {
                    if (connection.Socket.State == WebSocketState.Open)
                    {
                        await connection.Socket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Disconnected by administrator",
                            CancellationToken.None
                        );
                    }
                    Logger.Log($"Disconnected {connection.Type} connection: {id}", "Info");
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error disconnecting {id}: {ex.Message}", "Error");
                }
            }
        }

        public async Task DisconnectByType(string type)
        {
            var connectionsToRemove = _connections.Values.Where(c => c.Type == type);
            foreach (var connection in connectionsToRemove)
            {
                await DisconnectById(connection.Id);
            }
        }

        public IEnumerable<WebSocketConnection> ListConnections()
        {
            return _connections.Values;
        }

        public bool TryGetConnection(string id, out WebSocketConnection connection)
        {
            return _connections.TryGetValue(id, out connection);
        }

        public void RemoveConnection(string id)
        {
            _connections.TryRemove(id, out _);
        }

        private string GenerateConnectionId()
        {
            return $"{DateTime.UtcNow.Ticks:x8}-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }

        public async Task SendToConnection(string id, string message)
        {
            if (
                _connections.TryGetValue(id, out var connection)
                && connection.Socket.State == WebSocketState.Open
            )
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await connection.Socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
            }
        }
    }
}
