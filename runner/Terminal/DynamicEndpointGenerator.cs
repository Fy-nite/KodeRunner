using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace KodeRunner.Terminal
{
    /// <summary>
    /// Represents a dynamic WebSocket endpoint handler
    /// </summary>
    public class DynamicEndpoint
    {
        /// <summary>
        /// The handler function for the WebSocket connection
        /// </summary>
        public Func<WebSocket, string, string, int, Task> Handler { get; set; }
        
        /// <summary>
        /// The session ID associated with this endpoint
        /// </summary>
        public string SessionId { get; set; }
        
        /// <summary>
        /// When this endpoint was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Whether this endpoint is currently active
        /// </summary>
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// Number of active connections to this endpoint
        /// </summary>
        public int ConnectionCount;
    }

    /// <summary>
    /// Manages dynamic WebSocket endpoints for shared terminal sessions
    /// </summary>
    public static class DynamicEndpointGenerator
    {
        private static readonly ConcurrentDictionary<string, DynamicEndpoint> _endpoints 
            = new ConcurrentDictionary<string, DynamicEndpoint>();

        /// <summary>
        /// Register a new dynamic endpoint
        /// </summary>
        /// <param name="path">The endpoint path (e.g., "/terminal/abc123")</param>
        /// <param name="endpoint">The dynamic endpoint configuration</param>
        /// <returns>True if registered successfully, false if path already exists</returns>
        public static bool RegisterEndpoint(string path, DynamicEndpoint endpoint)
        {
            if (string.IsNullOrEmpty(path) || endpoint == null)
                return false;

            // Normalize the path to ensure it starts with /
            if (!path.StartsWith("/"))
                path = "/" + path;

            var success = _endpoints.TryAdd(path, endpoint);
            if (success)
            {
                Logger.Log($"Registered dynamic endpoint: {path} with session {endpoint.SessionId}", "Info");
            }
            else
            {
                Logger.Log($"Failed to register dynamic endpoint: {path} (already exists)", "Warning");
            }
            
            return success;
        }

        /// <summary>
        /// Try to get an endpoint handler for the given path
        /// </summary>
        /// <param name="path">The endpoint path to look up</param>
        /// <param name="endpoint">The found endpoint, if any</param>
        /// <returns>True if endpoint found, false otherwise</returns>
        public static bool TryGetEndpointHandler(string path, out DynamicEndpoint endpoint)
        {
            endpoint = null;
            
            if (string.IsNullOrEmpty(path))
                return false;

            // Normalize the path
            if (!path.StartsWith("/"))
                path = "/" + path;

            var found = _endpoints.TryGetValue(path, out endpoint);
            if (found && endpoint != null)
            {
                // Increment connection count
                Interlocked.Increment(ref endpoint.ConnectionCount);
                Logger.Log($"Connection to dynamic endpoint: {path} (total connections: {endpoint.ConnectionCount})", "Info");
            }

            return found;
        }

        /// <summary>
        /// Unregister a dynamic endpoint
        /// </summary>
        /// <param name="path">The endpoint path to remove</param>
        /// <returns>True if removed successfully, false if not found</returns>
        public static bool UnregisterEndpoint(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            // Normalize the path
            if (!path.StartsWith("/"))
                path = "/" + path;

            var removed = _endpoints.TryRemove(path, out var endpoint);
            if (removed && endpoint != null)
            {
                endpoint.IsActive = false;
                Logger.Log($"Unregistered dynamic endpoint: {path} with session {endpoint.SessionId}", "Info");
            }

            return removed;
        }

        /// <summary>
        /// Decrement connection count when a connection closes
        /// </summary>
        /// <param name="path">The endpoint path</param>
        public static void DecrementConnectionCount(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            // Normalize the path
            if (!path.StartsWith("/"))
                path = "/" + path;

            if (_endpoints.TryGetValue(path, out var endpoint))
            {
                var newCount = Interlocked.Decrement(ref endpoint.ConnectionCount);
                Logger.Log($"Disconnection from dynamic endpoint: {path} (remaining connections: {newCount})", "Info");
                
                // Optionally auto-cleanup endpoints with no connections after a timeout
                if (newCount <= 0)
                {
                    _ = Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(async _ =>
                    {
                        if (_endpoints.TryGetValue(path, out var ep) && ep.ConnectionCount <= 0)
                        {
                            Logger.Log($"Auto-cleaning up unused endpoint: {path}", "Info");
                            UnregisterEndpoint(path);
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Get all active endpoints
        /// </summary>
        /// <returns>A dictionary of all active endpoints</returns>
        public static IReadOnlyDictionary<string, DynamicEndpoint> GetAllEndpoints()
        {
            return _endpoints.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Get endpoints for a specific session ID
        /// </summary>
        /// <param name="sessionId">The session ID to search for</param>
        /// <returns>All endpoints associated with the session</returns>
        public static IEnumerable<KeyValuePair<string, DynamicEndpoint>> GetEndpointsForSession(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                return Enumerable.Empty<KeyValuePair<string, DynamicEndpoint>>();

            return _endpoints.Where(kvp => kvp.Value.SessionId == sessionId);
        }

        /// <summary>
        /// Clean up expired or inactive endpoints
        /// </summary>
        /// <param name="maxAge">Maximum age for endpoints before cleanup</param>
        /// <returns>Number of endpoints cleaned up</returns>
        public static int CleanupExpiredEndpoints(TimeSpan maxAge)
        {
            var cutoffTime = DateTime.UtcNow - maxAge;
            var expiredEndpoints = _endpoints
                .Where(kvp => kvp.Value.CreatedAt < cutoffTime || !kvp.Value.IsActive)
                .ToList();

            int cleanedUp = 0;
            foreach (var expired in expiredEndpoints)
            {
                if (UnregisterEndpoint(expired.Key))
                {
                    cleanedUp++;
                }
            }

            if (cleanedUp > 0)
            {
                Logger.Log($"Cleaned up {cleanedUp} expired dynamic endpoints", "Info");
            }

            return cleanedUp;
        }

        /// <summary>
        /// Get statistics about dynamic endpoints
        /// </summary>
        /// <returns>Endpoint statistics</returns>
        public static EndpointStatistics GetStatistics()
        {
            var endpoints = _endpoints.Values.ToList();
            return new EndpointStatistics
            {
                TotalEndpoints = endpoints.Count,
                ActiveEndpoints = endpoints.Count(e => e.IsActive),
                TotalConnections = endpoints.Sum(e => e.ConnectionCount),
                AverageAge = endpoints.Any() 
                    ? TimeSpan.FromTicks((long)endpoints.Average(e => (DateTime.UtcNow - e.CreatedAt).Ticks))
                    : TimeSpan.Zero
            };
        }

        /// <summary>
        /// Create a shared terminal endpoint for a specific project
        /// </summary>
        /// <param name="projectName">The project name</param>
        /// <param name="createdBy">Who created this endpoint</param>
        /// <returns>The endpoint path</returns>
        public static string CreateSharedTerminalEndpoint(string projectName, string createdBy)
        {
            var sessionId = Guid.NewGuid().ToString("N")[..8]; // Short session ID
            var endpoint = $"/terminal/{projectName}-{sessionId}";
            
            var dynamicEndpoint = new DynamicEndpoint
            {
                Handler = async (ws, connId, sessId, bufferSize) => 
                {
                    // This will be handled by Program.HandleSharedTerminal
                    await Program.HandleSharedTerminal(ws, connId, sessId, bufferSize);
                },
                SessionId = sessionId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            if (RegisterEndpoint(endpoint, dynamicEndpoint))
            {
                // Register with SharedTerminalManager using the correct parameters
                bool registered = SharedTerminalManager.RegisterSession(sessionId, projectName, endpoint, createdBy);
                if (registered)
                {
                    Logger.Log($"Created shared terminal endpoint: {endpoint} for project {projectName}", "Info");
                    return endpoint;
                }
                else
                {
                    // If session registration failed, clean up the endpoint
                    UnregisterEndpoint(endpoint);
                    Logger.Log($"Failed to register session for endpoint: {endpoint}", "Error");
                }
            }

            return null;
        }

        /// <summary>
        /// Get list of active endpoint paths
        /// </summary>
        /// <returns>List of active endpoint paths</returns>
        public static List<string> GetActiveEndpoints()
        {
            return _endpoints
                .Where(kvp => kvp.Value.IsActive)
                .Select(kvp => kvp.Key)
                .ToList();
        }

        /// <summary>
        /// Close and unregister an endpoint by session ID
        /// </summary>
        /// <param name="sessionId">The session ID to close</param>
        /// <returns>True if closed successfully</returns>
        public static bool CloseEndpointBySession(string sessionId)
        {
            var endpointToClose = _endpoints
                .FirstOrDefault(kvp => kvp.Value.SessionId == sessionId);

            if (endpointToClose.Key != null)
            {
                return UnregisterEndpoint(endpointToClose.Key);
            }

            return false;
        }

        /// <summary>
        /// Get endpoint by session ID
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <returns>The endpoint path if found, null otherwise</returns>
        public static string GetEndpointBySession(string sessionId)
        {
            var endpoint = _endpoints
                .FirstOrDefault(kvp => kvp.Value.SessionId == sessionId);

            return endpoint.Key;
        }
    }

    /// <summary>
    /// Statistics about dynamic endpoints
    /// </summary>
    public class EndpointStatistics
    {
        public int TotalEndpoints { get; set; }
        public int ActiveEndpoints { get; set; }
        public int TotalConnections { get; set; }
        public TimeSpan AverageAge { get; set; }
    }
}
