using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace KodeRunner.Terminal
{
    /// <summary>
    /// Represents a shared terminal session
    /// </summary>
    public class SharedTerminalSession
    {
        public string SessionId { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string HostConnectionId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public TerminalProcess TerminalProcess { get; set; } = null!;
    }

    /// <summary>
    /// Manages shared terminal sessions across the server
    /// </summary>
    public static class SharedTerminalManager
    {
        private static readonly ConcurrentDictionary<string, SharedTerminalSession> _sessions = new();
        private static readonly ConcurrentDictionary<string, List<string>> _sessionConnections = new();

        public static string CreateSharedSession(string projectName, string hostConnectionId)
        {
            var sessionId = GenerateSessionId();
            var session = new SharedTerminalSession
            {
                SessionId = sessionId,
                ProjectName = projectName,
                HostConnectionId = hostConnectionId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                TerminalProcess = new TerminalProcess()
            };

            // Set up terminal output broadcasting
            session.TerminalProcess.OnOutput += async (output) =>
            {
                await BroadcastToSession(sessionId, output);
            };

            _sessions[sessionId] = session;
            _sessionConnections[sessionId] = new List<string> { hostConnectionId };

            Logger.Log($"Created shared terminal session: {sessionId} for project: {projectName}");
            return sessionId;
        }

        public static bool JoinSession(string sessionId, string connectionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session) || !session.IsActive)
                return false;

            if (!_sessionConnections.TryGetValue(sessionId, out var connections))
                return false;

            connections.Add(connectionId);
            Logger.Log($"Connection {connectionId} joined shared terminal session {sessionId}");
            return true;
        }

        public static void LeaveSession(string sessionId, string connectionId)
        {
            if (_sessionConnections.TryGetValue(sessionId, out var connections))
            {
                connections.Remove(connectionId);
                Logger.Log($"Connection {connectionId} left shared terminal session {sessionId}");

                // Clean up empty sessions
                if (connections.Count == 0)
                {
                    CloseSession(sessionId);
                }
            }
        }

        public static bool SendInputToSession(string sessionId, string input)
        {
            if (_sessions.TryGetValue(sessionId, out var session) && session.IsActive)
            {
                return session.TerminalProcess.SendInput(input);
            }
            return false;
        }

        public static async Task ExecuteCommandInSession(string sessionId, string command)
        {
            if (_sessions.TryGetValue(sessionId, out var session) && session.IsActive)
            {
                await session.TerminalProcess.ExecuteCommand(command);
            }
        }

        private static async Task BroadcastToSession(string sessionId, string message)
        {
            if (!_sessionConnections.TryGetValue(sessionId, out var connections))
                return;

            var bytes = Encoding.UTF8.GetBytes(message);
            var tasks = connections.Select(async connectionId =>
            {
                try
                {
                    if (Program.connectionManager.TryGetConnection(connectionId, out var connection) &&
                        connection.Socket.State == WebSocketState.Open)
                    {
                        await connection.Socket.SendAsync(
                            new ArraySegment<byte>(bytes),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None
                        );
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error broadcasting to connection {connectionId}: {ex.Message}", "Error");
                }
            });

            await Task.WhenAll(tasks);
        }

        public static void CloseSession(string sessionId)
        {
            if (_sessions.TryRemove(sessionId, out var session))
            {
                session.IsActive = false;
                session.TerminalProcess?.DisposeAsync();
                _sessionConnections.TryRemove(sessionId, out _);
                Logger.Log($"Closed shared terminal session: {sessionId}");
            }
        }

        // Register Session function - Fixed to use the provided sessionId
        public static bool RegisterSession(string sessionId, string projectName, string endpoint, string createdBy)
        {
            if (_sessions.ContainsKey(sessionId))
            {
                Logger.Log($"Session {sessionId} already exists. Skipping registration.");
                return false;
            }

            var session = new SharedTerminalSession
            {
                SessionId = sessionId,
                ProjectName = projectName,
                HostConnectionId = createdBy, // Use createdBy as the initial host
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                TerminalProcess = new TerminalProcess()
            };

            // Set up terminal output broadcasting
            session.TerminalProcess.OnOutput += async (output) =>
            {
                await BroadcastToSession(sessionId, output);
            };

            _sessions[sessionId] = session;
            _sessionConnections[sessionId] = new List<string>();

            Logger.Log($"Registered shared terminal session: {sessionId} for project: {projectName} by {createdBy}");
            return true;
        }

        // Add method to execute commands in a session
        public static async Task<bool> ExecuteProjectInSession(string sessionId, Provider.ISettingsProvider settings)
        {
            if (!_sessions.TryGetValue(sessionId, out var session) || !session.IsActive)
            {
                Logger.Log($"Cannot execute project in session {sessionId}: session not found or inactive", "Warning");
                return false;
            }

            try
            {
                Logger.Log($"Executing project in shared terminal session {sessionId}");
                
                // Use the runnable manager to execute the project
                var runnableManager = new RunnableManager();
                runnableManager.LoadRunnables();
                
                // Execute in a background task to not block
                _ = Task.Run(() =>
                {
                    try
                    {
                        runnableManager.ExecuteFirstMatchingLanguage(settings.Language, settings);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Error executing project in session {sessionId}: {ex.Message}", "Error");
                        
                        // Send error message to session
                        _ = BroadcastToSession(sessionId, $"Error executing project: {ex.Message}\n");
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to execute project in session {sessionId}: {ex.Message}", "Error");
                return false;
            }
        }

        // Add method to get session statistics
        public static SessionStatistics GetStatistics()
        {
            var sessions = _sessions.Values.ToList();
            var connections = _sessionConnections.Values.SelectMany(c => c).Count();
            
            return new SessionStatistics
            {
                TotalSessions = sessions.Count,
                ActiveSessions = sessions.Count(s => s.IsActive),
                TotalConnections = connections,
                AverageAge = sessions.Any()
                    ? TimeSpan.FromTicks((long)sessions.Average(s => (DateTime.UtcNow - s.CreatedAt).Ticks))
                    : TimeSpan.Zero
            };
        }

        // Enhanced method to get active sessions with more detail
        public static List<string> GetActiveSessionDetails()
        {
            return _sessions.Values
                .Where(s => s.IsActive)
                .Select(s => 
                {
                    var connectionCount = _sessionConnections.TryGetValue(s.SessionId, out var conns) ? conns.Count : 0;
                    var age = DateTime.UtcNow - s.CreatedAt;
                    var ageStr = age.TotalMinutes < 60 ? $"{age.TotalMinutes:F0}m" : $"{age.TotalHours:F1}h";
                    return $"{s.SessionId} [{s.ProjectName}] - {connectionCount} connections, {ageStr} old";
                })
                .ToList();
        }

        public static SharedTerminalSession? GetSession(string sessionId)
        {
            _sessions.TryGetValue(sessionId, out var session);
            return session;
        }

        public static string[] GetActiveSessions()
        {
            return _sessions.Where(kvp => kvp.Value.IsActive).Select(kvp => kvp.Key).ToArray();
        }

        private static string GenerateSessionId()
        {
            return $"term_{DateTime.UtcNow.Ticks:x8}_{Guid.NewGuid().ToString("N")[..8]}";
        }
    }

    /// <summary>
    /// Statistics about shared terminal sessions
    /// </summary>
    public class SessionStatistics
    {
        public int TotalSessions { get; set; }
        public int ActiveSessions { get; set; }
        public int TotalConnections { get; set; }
        public TimeSpan AverageAge { get; set; }
    }
}
