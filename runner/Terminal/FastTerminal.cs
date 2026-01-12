using System.Collections.Concurrent;
using System.Text;

namespace KodeRunner.Terminal
{
    public class FastTerminal
    {
        private static FastWindow? _commands;
        private static FastWindow? _connections;
        private static FastWindow? _runnables;
        private static FastWindow? _logs;
        
        private static readonly ConcurrentQueue<string> _logQueue = new();
        private static readonly ConcurrentQueue<Action> _updateQueue = new();
        private static readonly Timer _renderTimer;
        private static readonly object _renderLock = new object();
        
        public static bool AdvancedTerm { get; private set; } = true;
        
        private static volatile bool _isInitialized = false;
        private static volatile bool _isShuttingDown = false;
        
        static FastTerminal()
        {
            _renderTimer = new Timer(RenderTick, null, Timeout.Infinite, Timeout.Infinite);
        }
        
        public static void Initialize()
        {
            if (_isInitialized) return;
            
            var w = Console.WindowWidth;
            var h = Console.WindowHeight;
            
            if (w < 120 || h < 30)
            {
                AdvancedTerm = false;
                Console.WriteLine("Console size is too small for advanced terminal UI.");
                _ = Task.Run(HandleCommandsSimple);
                return;
            }
            
            AdvancedTerm = true;
            
            // Setup console
            Console.Clear();
            Console.CursorVisible = false;
            Console.OutputEncoding = Encoding.UTF8;
            
            // Create windows with better layout
            var halfWidth = w / 2;
            var quarterHeight = h / 4;
            
            _commands = new FastWindow(0, 0, halfWidth, h, "Console", true);
            _connections = new FastWindow(halfWidth, 0, w - halfWidth, quarterHeight, "Connections");
            _runnables = new FastWindow(halfWidth, quarterHeight, w - halfWidth, quarterHeight, "Runnables");
            _logs = new FastWindow(halfWidth, quarterHeight * 2, w - halfWidth, h - (quarterHeight * 2), "Logs");
            
            _isInitialized = true;
            
            // Start background tasks
            _ = Task.Run(HandleCommandsAdvanced);
            _ = Task.Run(ProcessLogQueue);
            _ = Task.Run(ProcessUpdateQueue);
            
            // Start render timer (60 FPS)
            _renderTimer.Change(0, 16);
            
            WriteToConsole("Fast Terminal UI initialized");
        }
        
        // Add method to force simple mode for CLI operations
        public static void InitializeSimpleMode()
        {
            if (_isInitialized) return;
            
            AdvancedTerm = false;
            _isInitialized = true;
            
            // No advanced UI components in simple mode
            Console.WriteLine("KodeRunner CLI mode initialized");
        }
        
        private static void RenderTick(object? state)
        {
            if (!_isInitialized || _isShuttingDown || !AdvancedTerm) return;
            
            lock (_renderLock)
            {
                try
                {
                    _commands?.Render();
                    _connections?.Render();
                    _runnables?.Render();
                    _logs?.Render();
                }
                catch (Exception ex)
                {
                    // Silently handle render errors to avoid breaking the UI
                    Logger.Log($"Render error: {ex.Message}", "Warning");
                }
            }
        }
        
        public static void WriteToWindow(string text, string window)
        {
            if (!AdvancedTerm)
            {
                Console.Write(text);
                return;
            }
            
            _updateQueue.Enqueue(() =>
            {
                var targetWindow = window.ToLower() switch
                {
                    "console" or "commands" => _commands,
                    "connections" => _connections,
                    "runnables" => _runnables,
                    "logs" => _logs,
                    _ => _commands
                };
                
                targetWindow?.Write(text);
            });
        }
        
        public static void WriteLineToWindow(string text, string window)
        {
            WriteToWindow(text + "\n", window);
        }
        
        public static void WriteToConsole(string text)
        {
            if (AdvancedTerm)
            {
                _commands?.Write(text);
            }
            else
            {
                Console.Write(text);
            }
        }
        
        public static void WriteLineToConsole(string text)
        {
            WriteToConsole(text + "\n");
        }
        
        public static void LogMessage(string message)
        {
            _logQueue.Enqueue($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
        
        private static async Task ProcessLogQueue()
        {
            while (!_isShuttingDown)
            {
                while (_logQueue.TryDequeue(out var message))
                {
                    if (AdvancedTerm)
                    {
                        _logs?.WriteLine(message);
                    }
                }
                
                await Task.Delay(50); // Process logs 20 times per second
            }
        }
        
        private static async Task ProcessUpdateQueue()
        {
            while (!_isShuttingDown)
            {
                var processed = 0;
                while (_updateQueue.TryDequeue(out var update) && processed < 10)
                {
                    try
                    {
                        update();
                        processed++;
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Update error: {ex.Message}", "Warning");
                    }
                }
                
                await Task.Delay(16); // Process updates at 60 FPS
            }
        }
        
        private static async Task HandleCommandsSimple()
        {
            while (!_isShuttingDown)
            {
                Console.Write("> ");
                var command = await Console.In.ReadLineAsync();
                if (!string.IsNullOrEmpty(command))
                {
                    await ProcessCommand(command);
                }
            }
        }
        
        private static async Task HandleCommandsAdvanced()
        {
            var inputBuffer = new StringBuilder();
            
            while (!_isShuttingDown)
            {
                _commands?.Write("> ");
                
                var input = await ReadLineAsync();
                if (!string.IsNullOrEmpty(input))
                {
                    await ProcessCommand(input);
                }
            }
        }
        
        private static async Task<string> ReadLineAsync()
        {
            return await Task.Run(() =>
            {
                var input = new StringBuilder();
                
                while (true)
                {
                    var keyInfo = Console.ReadKey(true);
                    
                    switch (keyInfo.Key)
                    {
                        case ConsoleKey.Enter:
                            _commands?.Write("\n");
                            return input.ToString();
                            
                        case ConsoleKey.Backspace:
                            if (input.Length > 0)
                            {
                                input.Remove(input.Length - 1, 1);
                                _commands?.Backspace();
                            }
                            break;
                            
                        case ConsoleKey.Tab:
                            input.Append("    ");
                            _commands?.Write("    ");
                            break;
                            
                        default:
                            if (!char.IsControl(keyInfo.KeyChar))
                            {
                                input.Append(keyInfo.KeyChar);
                                _commands?.Write(keyInfo.KeyChar.ToString());
                            }
                            break;
                    }
                }
            });
        }
        
        private static async Task ProcessCommand(string command)
        {
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;
            
            try
            {
                switch (parts[0].ToLower())
                {
                    case "clear":
                        if (AdvancedTerm)
                        {
                            _commands?.Clear();
                        }
                        else
                        {
                            Console.Clear();
                        }
                        break;
                        
                    case "list":
                        ListConnections();
                        break;
                        
                    case "disconnect":
                        if (parts.Length > 1)
                        {
                            await Program.connectionManager.DisconnectById(parts[1]);
                            WriteLineToConsole($"Disconnected: {parts[1]}");
                        }
                        break;
                        
                    case "disconnecttype":
                        if (parts.Length > 1)
                        {
                            await Program.connectionManager.DisconnectByType(parts[1]);
                            WriteLineToConsole($"Disconnected all {parts[1]} connections");
                        }
                        break;
                        
                    case "input":
                        if (parts.Length > 1)
                        {
                            var input = string.Join(" ", parts.Skip(1));
                            bool sent = Program.SendInputToActiveProcess(input);
                            WriteLineToConsole(sent ? "Input sent to active process" : "No active process found");
                        }
                        else
                        {
                            WriteLineToConsole("Usage: input <text to send>");
                        }
                        break;
                        
                    case "stop":
                        WriteLineToConsole("Stopping all processes...");
                        TerminalProcess.StopAllProcesses();
                        WriteLineToConsole("All processes stopped");
                        break;
                        
                    case "endpoints":
                        ListDynamicEndpoints();
                        break;
                        
                    case "cleanup":
                        CleanupEndpoints();
                        break;
                        
                    case "metrics":
                        ShowMetrics(parts);
                        break;
                        
                    case "collab":
                        HandleCollaboration(parts);
                        break;
                        
                    case "sandbox":
                        HandleSandboxCommand(parts);
                        break;
                        
                    case "terminal":
                        HandleTerminalCommand(parts);
                        break;
                        
                    case "status":
                        ShowSystemStatus();
                        break;
                        
                    case "refresh":
                        if (AdvancedTerm)
                        {
                            RefreshWindows();
                        }
                        break;
                        
                    case "help":
                        ShowHelp();
                        break;
                        
                    case "exit":
                    case "quit":
                        _isShuttingDown = true;
                        Environment.Exit(0);
                        break;
                        
                    default:
                        WriteLineToConsole($"Unknown command: {parts[0]}. Type 'help' for available commands.");
                        break;
                }
            }
            catch (Exception ex)
            {
                WriteLineToConsole($"Error: {ex.Message}");
                Logger.Log($"Command error: {ex.Message}", "Error");
            }
        }
        
        private static void ShowHelp()
        {
            WriteLineToConsole("Available commands:");
            WriteLineToConsole("  clear                 - Clear console");
            WriteLineToConsole("  list                  - List active connections");
            WriteLineToConsole("  disconnect <id>       - Disconnect a specific connection");
            WriteLineToConsole("  disconnecttype <type> - Disconnect all connections of a type");
            WriteLineToConsole("  input <text>          - Send input to active terminal process");
            WriteLineToConsole("  stop                  - Stop all active processes");
            WriteLineToConsole("  endpoints             - List dynamic endpoints");
            WriteLineToConsole("  cleanup               - Clean up expired endpoints");
            WriteLineToConsole("  metrics [days]        - Show execution metrics");
            WriteLineToConsole("  collab create <proj>  - Create collaboration session");
            WriteLineToConsole("  sandbox list          - List sandbox policies");
            WriteLineToConsole("  terminal create <proj> - Create shared terminal session");
            WriteLineToConsole("  terminal list         - List active terminal sessions");
            WriteLineToConsole("  terminal close <id>   - Close terminal session");
            WriteLineToConsole("  status                - Show system status overview");
            WriteLineToConsole("  refresh               - Refresh UI windows");
            WriteLineToConsole("  help                  - Show this help");
            WriteLineToConsole("  exit/quit             - Exit application");
        }
        
        private static void ShowMetrics(string[] parts)
        {
            var metricsCollector = new Analytics.MetricsCollector();
            var days = parts.Length > 1 && int.TryParse(parts[1], out var d) ? d : 7;
            var since = DateTime.UtcNow.AddDays(-days);
            var stats = metricsCollector.GetStats(since);

            WriteLineToConsole($"\n=== Execution Metrics (Last {days} days) ===");
            WriteLineToConsole($"Total Executions: {stats.TotalExecutions}");
            WriteLineToConsole($"Successful: {stats.SuccessfulExecutions} ({(stats.TotalExecutions > 0 ? stats.SuccessfulExecutions * 100.0 / stats.TotalExecutions : 0):F1}%)");
            WriteLineToConsole($"Average Duration: {stats.AverageExecutionTime.TotalSeconds:F2}s");
            WriteLineToConsole($"Average Memory: {stats.AverageMemoryUsage:F1}MB");
            
            if (stats.TopLanguages.Any())
            {
                WriteLineToConsole("\nTop Languages:");
                foreach (var lang in stats.TopLanguages)
                {
                    WriteLineToConsole($"  {lang.Key}: {lang.Value} executions");
                }
            }
        }

        private static void HandleCollaboration(string[] parts)
        {
            if (parts.Length < 2)
            {
                WriteLineToConsole("Usage: collab create <project-name>");
                return;
            }

            switch (parts[1].ToLower())
            {
                case "create":
                    if (parts.Length > 2)
                    {
                        var sessionId = Program.collaborationManager.CreateSession(parts[2], "console");
                        WriteLineToConsole($"Collaboration session created: {sessionId}");
                    }
                    break;
                default:
                    WriteLineToConsole("Unknown collaboration command");
                    break;
            }
        }

        private static void HandleSandboxCommand(string[] parts)
        {
            if (parts.Length < 2)
            {
                WriteLineToConsole("Usage: sandbox list");
                return;
            }

            switch (parts[1].ToLower())
            {
                case "list":
                    WriteLineToConsole("Available sandbox policies:");
                    WriteLineToConsole("  default - Basic restrictions (512MB, 30s, no network)");
                    WriteLineToConsole("  trusted - Extended permissions (2GB, 5min, network allowed)");
                    break;
                default:
                    WriteLineToConsole("Unknown sandbox command");
                    break;
            }
        }

        private static void HandleTerminalCommand(string[] parts)
        {
            if (parts.Length < 2)
            {
                WriteLineToConsole("Usage: terminal <create|list|close> [args]");
                return;
            }

            switch (parts[1].ToLower())
            {
                case "create":
                    if (parts.Length > 2)
                    {
                        var projectName = parts[2];
                        var endpoint = DynamicEndpointGenerator.CreateSharedTerminalEndpoint(
                            projectName, "console");
                        if (endpoint != null)
                        {
                            WriteLineToConsole($"Created shared terminal: {endpoint}");
                        }
                        else
                        {
                            WriteLineToConsole($"Failed to create shared terminal for {projectName}");
                        }
                    }
                    else
                    {
                        WriteLineToConsole("Usage: terminal create <project-name>");
                    }
                    break;
                    
                case "list":
                    var sessions = SharedTerminalManager.GetActiveSessionDetails();
                    var endpoints = DynamicEndpointGenerator.GetActiveEndpoints();
                    
                    WriteLineToConsole("\nActive Terminal Sessions:");
                    if (sessions.Any())
                    {
                        foreach (var session in sessions)
                        {
                            WriteLineToConsole($"  {session}");
                        }
                    }
                    else
                    {
                        WriteLineToConsole("  No active terminal sessions");
                    }
                    
                    WriteLineToConsole("\nActive Endpoints:");
                    if (endpoints.Any())
                    {
                        foreach (var endpoint in endpoints.Take(5))
                        {
                            WriteLineToConsole($"  {endpoint}");
                        }
                        if (endpoints.Count > 5)
                        {
                            WriteLineToConsole($"  ... and {endpoints.Count - 5} more");
                        }
                    }
                    else
                    {
                        WriteLineToConsole("  No active endpoints");
                    }
                    
                    // Show statistics
                    var stats = SharedTerminalManager.GetStatistics();
                    WriteLineToConsole($"\nStatistics: {stats.ActiveSessions}/{stats.TotalSessions} active sessions, {stats.TotalConnections} total connections");
                    break;
                    
                case "close":
                    if (parts.Length > 2)
                    {
                        var sessionId = parts[2];
                        SharedTerminalManager.CloseSession(sessionId);
                        WriteLineToConsole($"Closed terminal session: {sessionId}");
                    }
                    else
                    {
                        WriteLineToConsole("Usage: terminal close <session-id>");
                    }
                    break;
                    
                default:
                    WriteLineToConsole("Unknown terminal command. Use: create, list, close");
                    break;
            }
        }

        private static void ListDynamicEndpoints()
        {
            var endpoints = DynamicEndpointGenerator.GetAllEndpoints();
            var stats = DynamicEndpointGenerator.GetStatistics();
            
            WriteLineToConsole($"\nDynamic Endpoints ({stats.TotalEndpoints} total, {stats.ActiveEndpoints} active):");
            WriteLineToConsole("Path                   Session ID           Connections  Created");
            WriteLineToConsole("---------------------- -------------------- ------------ --------");
            
            foreach (var endpoint in endpoints.Take(10))
            {
                var age = DateTime.UtcNow - endpoint.Value.CreatedAt;
                var ageStr = age.TotalMinutes < 60 
                    ? $"{age.TotalMinutes:F0}m ago"
                    : $"{age.TotalHours:F1}h ago";
                    
                WriteLineToConsole($"{endpoint.Key,-22} {endpoint.Value.SessionId,-20} {endpoint.Value.ConnectionCount,-12} {ageStr}");
            }
            
            if (endpoints.Count == 0)
            {
                WriteLineToConsole("  No dynamic endpoints registered");
            }
            else if (endpoints.Count > 10)
            {
                WriteLineToConsole($"  ... and {endpoints.Count - 10} more endpoints");
            }
            
            WriteLineToConsole($"\nTotal connections across all endpoints: {stats.TotalConnections}");
        }
        
        private static void CleanupEndpoints()
        {
            var cleanedUp = DynamicEndpointGenerator.CleanupExpiredEndpoints(TimeSpan.FromHours(1));
            WriteLineToConsole($"Cleaned up {cleanedUp} expired endpoints");
        }

        private static void ShowSystemStatus()
        {
            var connections = Program.connectionManager.ListConnections();
            var sessionStats = SharedTerminalManager.GetStatistics();
            var dynamicStats = DynamicEndpointGenerator.GetStatistics();
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime();

            WriteLineToConsole("\n=== KodeRunner System Status ===");
            WriteLineToConsole($"Version: {Core.GetVersion()}");
            WriteLineToConsole($"Uptime: {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m");
            WriteLineToConsole($"Memory: {process.WorkingSet64 / 1024 / 1024:F1} MB");
            WriteLineToConsole($"Threads: {process.Threads.Count}");
            WriteLineToConsole("");
            WriteLineToConsole("Connections:");
            WriteLineToConsole($"  Active: {connections.Count()}");
            WriteLineToConsole($"  Dynamic Endpoints: {dynamicStats.ActiveEndpoints}/{dynamicStats.TotalEndpoints}");
            WriteLineToConsole("");
            WriteLineToConsole("Terminal Sessions:");
            WriteLineToConsole($"  Active: {sessionStats.ActiveSessions}");
            WriteLineToConsole($"  Total Connections: {sessionStats.TotalConnections}");
            WriteLineToConsole("");
            
            // Show project count
            var projectsDir = Core.GetPath(Core.CodeDir);
            var projectCount = Directory.Exists(projectsDir) ? Directory.GetDirectories(projectsDir).Length : 0;
            WriteLineToConsole($"Projects: {projectCount}");
        }
        
        private static void RefreshWindows()
        {
            _updateQueue.Enqueue(() =>
            {
                UpdateConnections();
                UpdateRunnables();
            });
        }
        
        public static void UpdateConnections()
        {
            if (!AdvancedTerm) return;
            
            _updateQueue.Enqueue(() =>
            {
                _connections?.Clear();
                _connections?.WriteLine("Active Connections:");
                _connections?.WriteLine("─".PadRight(_connections.ContentWidth, '─'));
                
                var connections = Program.connectionManager.ListConnections();
                foreach (var conn in connections.Take(10)) // Limit to prevent overflow
                {
                    var age = DateTime.UtcNow - conn.ConnectedAt;
                    var ageStr = age.TotalMinutes < 60 ? $"{age.TotalMinutes:F0}m" : $"{age.TotalHours:F1}h";
                    _connections?.WriteLine($"{conn.Type} ({ageStr})");
                }
                
                if (connections.Count() > 10)
                {
                    _connections?.WriteLine($"... and {connections.Count() - 10} more");
                }
            });
        }
        
        public static void UpdateRunnables()
        {
            if (!AdvancedTerm) return;
            
            _updateQueue.Enqueue(() =>
            {
                _runnables?.Clear();
                _runnables?.WriteLine("Available Runnables:");
                _runnables?.WriteLine("─".PadRight(_runnables.ContentWidth, '─'));
                
                // Get actual runnable data if available
                try
                {
                    // This would integrate with the actual runnable manager
                    var runnables = GetRunnablesList();
                    if (runnables.Any())
                    {
                        foreach (var runnable in runnables.Take(8)) // Limit display
                        {
                            _runnables?.WriteLine($"{runnable.Name} ({runnable.Language}) - {runnable.Priority}");
                        }
                        if (runnables.Count > 8)
                        {
                            _runnables?.WriteLine($"... and {runnables.Count - 8} more");
                        }
                    }
                    else
                    {
                        _runnables?.WriteLine("No runnables loaded");
                    }
                }
                catch
                {
                    // Fallback to static display
                    _runnables?.WriteLine("Python - High Priority");
                    _runnables?.WriteLine("C# - High Priority");
                    _runnables?.WriteLine("Node.js - Medium Priority");
                    _runnables?.WriteLine("C - Medium Priority");
                    _runnables?.WriteLine("Java - Low Priority");
                }
            });
        }

        private static List<(string Name, string Language, string Priority)> GetRunnablesList()
        {
            try
            {
                // Access the actual runnable manager from Program
                var runnableManager = GetRunnableManager();
                if (runnableManager == null)
                {
                    return new List<(string Name, string Language, string Priority)>();
                }
                
                // Use reflection to get the runnables list from RunnableManager
                var runnableManagerType = runnableManager.GetType();
                var runnablesField = runnableManagerType.GetField("runnables", 
                    System.Reflection.BindingFlags.Instance | 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Public);
                
                if (runnablesField?.GetValue(runnableManager) is not IEnumerable<object> runnables)
                {
                    return new List<(string Name, string Language, string Priority)>();
                }
                
                var result = new List<(string Name, string Language, string Priority)>();
                
                foreach (var runnable in runnables)
                {
                    var runnableType = runnable.GetType();
                    
                    // Try to get Name property
                    var nameProperty = runnableType.GetProperty("Name") ?? 
                                      runnableType.GetProperty("RunnableName") ??
                                      runnableType.GetProperty("name");
                    var name = nameProperty?.GetValue(runnable)?.ToString() ?? runnableType.Name;
                    
                    // Try to get Language property
                    var languageProperty = runnableType.GetProperty("Language") ?? 
                                          runnableType.GetProperty("lang") ??
                                          runnableType.GetProperty("SupportedLanguage");
                    var language = languageProperty?.GetValue(runnable)?.ToString() ?? "Unknown";
                    
                    // Try to get Priority property
                    var priorityProperty = runnableType.GetProperty("Priority") ?? 
                                          runnableType.GetProperty("ExecutionPriority");
                    var priority = priorityProperty?.GetValue(runnable)?.ToString() ?? "Medium";
                    
                    result.Add((name, language, priority));
                }
                
                return result.OrderBy(r => r.Priority).ThenBy(r => r.Name).ToList();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error getting runnables list: {ex.Message}", "Warning");
                // Fallback to static list if there's an error
                return new List<(string Name, string Language, string Priority)>
                {
                    ("Python Runner", "Python", "High"),
                    ("C# Compiler", "C#", "High"),
                    ("Node.js Runtime", "JavaScript", "Medium"),
                    ("GCC Compiler", "C", "Medium"),
                    ("Java JVM", "Java", "Low"),
                    ("WebAssembly", "WASM", "Low")
                };
            }
        }

        // Helper method to get the runnable manager from Program
        private static RunnableManager? GetRunnableManager()
        {
            try
            {
                // Use reflection to access the private static field from Program
                var programType = typeof(Program);
                var field = programType.GetField("runnableManager", 
                    System.Reflection.BindingFlags.Static | 
                    System.Reflection.BindingFlags.NonPublic);
                
                return field?.GetValue(null) as RunnableManager;
            }
            catch
            {
                return null;
            }
        }

        // Add method to integrate with logging system
        public static void LogToTerminal(string message, string level = "Info")
        {
            var colorPrefix = level.ToLower() switch
            {
                "error" => "[ERROR]",
                "warning" => "[WARN]",
                "info" => "[INFO]",
                _ => "[LOG]"
            };
            
            LogMessage($"{colorPrefix} {message}");
        }

        // Enhanced connection display with more details
        private static void ListConnections()
        {
            var connections = Program.connectionManager.ListConnections();
            WriteLineToConsole($"\n=== Active Connections ({connections.Count()}) ===");
            
            if (!connections.Any())
            {
                WriteLineToConsole("No active connections");
                return;
            }

            WriteLineToConsole("ID                     Type           Age        Client");
            WriteLineToConsole("---------------------- -------------- ---------- --------");
            
            foreach (var conn in connections.Take(15))
            {
                var age = DateTime.UtcNow - conn.ConnectedAt;
                var ageStr = age.TotalMinutes < 60 
                    ? $"{age.TotalMinutes:F0}m" 
                    : $"{age.TotalHours:F1}h";
                
                WriteLineToConsole($"{conn.Id,-22} {conn.Type,-14} {ageStr,-10} {conn.ClientInfo}");
            }
            
            if (connections.Count() > 15)
            {
                WriteLineToConsole($"... and {connections.Count() - 15} more connections");
            }

            // Show connection statistics
            var connectionsByType = connections.GroupBy(c => c.Type).ToDictionary(g => g.Key, g => g.Count());
            WriteLineToConsole("\nBy Type:");
            foreach (var kvp in connectionsByType.OrderByDescending(x => x.Value))
            {
                WriteLineToConsole($"  {kvp.Key}: {kvp.Value}");
            }
        }
    }
}
