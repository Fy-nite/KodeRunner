namespace KodeRunner.Terminal
{
    class Terminal
    {
        static Window commands;
        static Window connections;
        static Window runnables;
        static Window log;

        public static bool advancedterm = true;

        public static void init()
        {
            var w = Console.WindowWidth;
            var h = Console.WindowHeight;
            if (w < 150 || h < 50)
            {
                advancedterm = false;
       
                Console.WriteLine("Console size is too small to enable advanced terminal features."); 
                // create a task
                      _ = Task.Run(handleCommands);
            }
            if (advancedterm)
            {
                Console.Clear();
                commands = new Window(0, 0, w / 2f + 2, h + 2, "Console", true);
                connections = new Window(w / 2f + 1, 0, w / 2f + 1, h / 4f + 1.5f, "Connections");
                runnables = new Window(w / 2f + 1, h / 4f, w / 2f + 1, h / 4f, "Runnables");
                log = new Window(w / 2f + 1, h / 2f - 0.5f, w / 2f + 1, h / 2f + 2, "Logs");

                Console.Write("\x1b[1;1H\x1b[?25l");
                _ = Task.Run(handleCommands);
                _ = Task.Run(RunnablesWindow);
                _ = Task.Run(ConnectionsWindow);
            }
        }

        private static void commandsWriteLine(string str)
        {
            if (advancedterm)
            {
                commands.WriteLine(str);
            }
            else
            {
                Console.WriteLine(str);
            }
        }

        public static void Write(string str, string window)
        {
            if (!advancedterm)
            {
                Console.Write(str);
                return;
            }
            switch (window)
            {
                case "Console":
                    commands.Write(str);
                    break;
                case "Connections":
                    connections.Write(str);
                    break;
                case "Runnables":
                    runnables.Write(str);
                    break;
                case "Logs":
                    log.Write(str);
                    break;
            }
        }

        public static async Task handleCommands()
        {
            while (true)
            {
                if (advancedterm)
                {
                    commands.Write("> ");
                }
                else
                {
                    Console.Write("> ");
                }
                
                var command = await ReadString();
                if (string.IsNullOrEmpty(command))
                    continue;

                var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                try
                {
                    switch (parts[0].ToLower())
                    {
                        case "list":
                            ListConnections();
                            break;
                        case "disconnect":
                            if (parts.Length > 1)
                            {
                                await Program.connectionManager.DisconnectById(parts[1]);
                            }
                            break;
                        case "disconnecttype":
                            if (parts.Length > 1)
                            {
                                await Program.connectionManager.DisconnectByType(parts[1]);
                            }
                            break;
                        case "input":
                            if (parts.Length > 1)
                            {
                                var input = string.Join(" ", parts.Skip(1));
                                bool sent = Program.SendInputToActiveProcess(input);
                                commandsWriteLine(sent ? "Input sent to active process" : "No active process found");
                            }
                            else
                            {
                                commandsWriteLine("Usage: input <text to send>");
                            }
                            break;
                        case "stop":
                            commandsWriteLine("Stopping all processes...");
                            TerminalProcess.StopAllProcesses();
                            commandsWriteLine("All processes stopped");
                            break;
                        case "endpoints":
                            ListDynamicEndpoints();
                            break;
                        case "cleanup":
                            CleanupEndpoints();
                            break;
                        case "help":
                            ShowHelp();
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
                        case "clear":
                            if (advancedterm)
                            {
                                commands.Clear();
                            }
                            else
                            {
                                Console.Clear();
                            }
                            break;
                        case "terminal":
                            HandleTerminalCommand(parts);
                            break;
                        default:
                            commandsWriteLine("Unknown command. Type 'help' for available commands.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error while processing command {parts[0]}: {ex.Message}", "error");
                }
            }
        }
        static void ShowHelp()
        {
            commandsWriteLine("Available commands:");
            commandsWriteLine("  list                  - List all active connections");
            commandsWriteLine("  disconnect <id>       - Disconnect a specific connection");
            commandsWriteLine("  disconnecttype <type> - Disconnect all connections of a type");
            commandsWriteLine("  import <project file> - Import a .KRproject file");
            commandsWriteLine("  export <project name> - Export a project into a .KRproject file");
            commandsWriteLine("  metrics [days]        - Show execution metrics");
            commandsWriteLine("  collab create <proj>  - Create collaboration session");
            commandsWriteLine("  sandbox list          - List sandbox policies");
            commandsWriteLine("  input <text>          - Send input to active terminal process");
            commandsWriteLine("  stop                  - Stop all active processes");
            commandsWriteLine("  endpoints             - List dynamic endpoints");
            commandsWriteLine("  cleanup               - Clean up expired endpoints");
            commandsWriteLine("  clear                 - Clear the console");
            commandsWriteLine("  help                  - Show this help message");
        }

        static void ShowMetrics(string[] parts)
        {
            var metricsCollector = new Analytics.MetricsCollector();
            var days = parts.Length > 1 && int.TryParse(parts[1], out var d) ? d : 7;
            var since = DateTime.UtcNow.AddDays(-days);
            var stats = metricsCollector.GetStats(since);

            commandsWriteLine($"\n=== Execution Metrics (Last {days} days) ===");
            commandsWriteLine($"Total Executions: {stats.TotalExecutions}");
            commandsWriteLine($"Successful: {stats.SuccessfulExecutions} ({(stats.TotalExecutions > 0 ? stats.SuccessfulExecutions * 100.0 / stats.TotalExecutions : 0):F1}%)");
            commandsWriteLine($"Average Duration: {stats.AverageExecutionTime.TotalSeconds:F2}s");
            commandsWriteLine($"Average Memory: {stats.AverageMemoryUsage:F1}MB");
            
            if (stats.TopLanguages.Any())
            {
                commandsWriteLine("\nTop Languages:");
                foreach (var lang in stats.TopLanguages)
                {
                    commandsWriteLine($"  {lang.Key}: {lang.Value} executions");
                }
            }
        }

        static void HandleCollaboration(string[] parts)
        {
            if (parts.Length < 2)
            {
                commandsWriteLine("Usage: collab create <project-name>");
                return;
            }

            switch (parts[1].ToLower())
            {
                case "create":
                    if (parts.Length > 2)
                    {
                        var sessionId = Program.collaborationManager.CreateSession(parts[2], "console");
                        commandsWriteLine($"Collaboration session created: {sessionId}");
                    }
                    break;
                default:
                    commandsWriteLine("Unknown collaboration command");
                    break;
            }
        }

        static void HandleSandboxCommand(string[] parts)
        {
            if (parts.Length < 2)
            {
                commandsWriteLine("Usage: sandbox list");
                return;
            }

            switch (parts[1].ToLower())
            {
                case "list":
                    commandsWriteLine("Available sandbox policies:");
                    commandsWriteLine("  default - Basic restrictions (512MB, 30s, no network)");
                    commandsWriteLine("  trusted - Extended permissions (2GB, 5min, network allowed)");
                    break;
                default:
                    commandsWriteLine("Unknown sandbox command");
                    break;
            }
        }

        static void ListConnections()
        {
            var connections = Program.connectionManager.ListConnections();
            commandsWriteLine("\nActive connections:");
            commandsWriteLine("ID                     Type     Connected At          Client Info");
            commandsWriteLine("---------------------- -------- -------------------- ------------");
            foreach (var conn in connections)
            {
                commandsWriteLine(
                    $"{conn.Id,-22} {conn.Type,-8} {conn.ConnectedAt:yyyy-MM-dd HH:mm:ss} {conn.ClientInfo}"
                );
            }
            if (advancedterm)
            {
                commands.WriteChar('\n');
            }
            else { Console.Write('\n'); }
        }
        static async Task<string> ReadString()
        {
            if (!advancedterm)
            {
                // Ensure console input is properly initialized
     
                string? input = await Console.In.ReadLineAsync();
                return input ?? string.Empty;  // Handle null case
            }
            return await Task.Run(() =>
            {
                string input = "";
                while (true)
                {
                    ConsoleKeyInfo key = Console.ReadKey(true);  // Suppress the key from appearing on screen

                    if (key.Key == ConsoleKey.Enter)
                    {
                        commands.WriteChar('\n');
                        break;
                    }
                    else if (key.Key == ConsoleKey.Backspace)
                    {
                        if (input.Length > 0)
                        {
                            input = input.Substring(0, input.Length - 1);
                            commands.Backspace();
                        }
                    }
                    else
                    {
                        if (key.Key == ConsoleKey.Tab)
                        {
                            input += "    ";
                            commands.Write("    ");  // Optionally show '*' for each typed character
                        }
                        else
                        {
                            input += key.KeyChar;
                            commands.WriteChar(key.KeyChar);  // Optionally show '*' for each typed character
                        }
                    }
                }
                return input;
            });
        }
        static readonly string[] RunnablesHeader = { "Name", "Language", "Priority" };
        static string CreatePaddedString(string[] data, int len)
        {
            int spacing = (int)Math.Round(len / (float)(data.Length + 1));
            int sub = 0;
            int mul = 1;
            string ret = "";
            for (int i = 0; i < data.Length; i++)
            {
                var str = data[i];
                if (i == 2)
                    ret += new string(Enumerable.Repeat(' ', (spacing * mul) - (str.Length / 2) - sub).ToArray());
                else
                    ret += new string(Enumerable.Repeat(' ', (spacing * mul) - ((str.Length + 1) / 2) - sub).ToArray());
                sub = str.Length / 2;
                mul = 1;
                ret += str;
            }
            return ret;
        }
        private static readonly List<Tuple<string, string, int>> runnables_list = new List<Tuple<string, string, int>>();
        static string header;
        static async Task RunnablesWindow()
        {
            header = CreatePaddedString(RunnablesHeader, runnables.bw);
            header += "\n" + new string(Enumerable.Repeat('-', runnables.bw).ToArray());
            runnables.DisableAutoUpdate();
            while (true)
            {
                UpdateRunnables();
                await Task.Delay(Core.RunnablesUpdateTime);
            }
        }
        public static void UpdateRunnables()
        {
            if (!advancedterm) { return; }
            runnables.Clear();
            runnables.Write(header);

            foreach (var runnable in runnables_list)
            {
                runnables.WriteLine(CreatePaddedString(
                    new string[] { runnable.Item1, runnable.Item2, runnable.Item3.ToString() },
                    runnables.bw
                ));
            }

            runnables.Update();
        }
        public static void AddRunnable(string name, string language, int priority)
        {
            runnables_list.Add(new Tuple<string, string, int>(name, language, priority));
        }

        static readonly string[] ConnectionsHeader = { "ID", "Type", "Connected At", "Client Info" };
        static string connections_header;
        static async Task ConnectionsWindow()
        {
            connections_header = CreatePaddedString(ConnectionsHeader, connections.bw);
            connections_header += "\n" + new string(Enumerable.Repeat('-', connections.bw).ToArray());
            connections.DisableAutoUpdate();
            while (true)
            {
                UpdateConnections();
                await Task.Delay(1000);
            }
        }
        public static void UpdateConnections()
        {
            connections.Clear();
            connections.Write(connections_header);

            foreach (var connection in Program.connectionManager.ListConnections())
            {
                try
                {
                    connections.WriteLine(CreatePaddedString(
                        new string[] {
                            connection.Id,
                            connection.Type,
                            $"{connection.ConnectedAt:yyyy-MM-dd HH:mm:ss}",
                            connection.ClientInfo
                        },
                        connections.bw
                    ));
                }
                catch (Exception ex) { Logger.Log(ex.Message, "Error"); }
            }

            connections.Update();
        }
        static void HandleTerminalCommand(string[] parts)
        {
            if (parts.Length < 2)
            {
                commandsWriteLine("Usage: terminal <create|list|close> [args]");
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
                            commandsWriteLine($"Created shared terminal: {endpoint}");
                        }
                        else
                        {
                            commandsWriteLine($"Failed to create shared terminal for {projectName}");
                        }
                    }
                    else
                    {
                        commandsWriteLine("Usage: terminal create <project-name>");
                    }
                    break;
                    
                case "list":
                    var sessions = SharedTerminalManager.GetActiveSessionDetails();
                    var endpoints = DynamicEndpointGenerator.GetActiveEndpoints();
                    
                    commandsWriteLine("\nActive Terminal Sessions:");
                    if (sessions.Any())
                    {
                        foreach (var session in sessions)
                        {
                            commandsWriteLine($"  {session}");
                        }
                    }
                    else
                    {
                        commandsWriteLine("  No active terminal sessions");
                    }
                    
                    commandsWriteLine("\nActive Endpoints:");
                    if (endpoints.Any())
                    {
                        foreach (var endpoint in endpoints)
                        {
                            commandsWriteLine($"  {endpoint}");
                        }
                    }
                    else
                    {
                        commandsWriteLine("  No active endpoints");
                    }
                    
                    // Show statistics
                    var stats = SharedTerminalManager.GetStatistics();
                    commandsWriteLine($"\nStatistics: {stats.ActiveSessions}/{stats.TotalSessions} active sessions, {stats.TotalConnections} total connections");
                    break;
                    
                case "close":
                    if (parts.Length > 2)
                    {
                        var sessionId = parts[2];
                        SharedTerminalManager.CloseSession(sessionId);
                        commandsWriteLine($"Closed terminal session: {sessionId}");
                    }
                    else
                    {
                        commandsWriteLine("Usage: terminal close <session-id>");
                    }
                    break;
                    
                default:
                    commandsWriteLine("Unknown terminal command. Use: create, list, close");
                    break;
            }
        }
        static void ListDynamicEndpoints()
        {
            var endpoints = DynamicEndpointGenerator.GetAllEndpoints();
            var stats = DynamicEndpointGenerator.GetStatistics();
            
            commandsWriteLine($"\nDynamic Endpoints ({stats.TotalEndpoints} total, {stats.ActiveEndpoints} active):");
            commandsWriteLine("Path                   Session ID           Connections  Created");
            commandsWriteLine("---------------------- -------------------- ------------ --------");
            
            foreach (var endpoint in endpoints)
            {
                var age = DateTime.UtcNow - endpoint.Value.CreatedAt;
                var ageStr = age.TotalMinutes < 60 
                    ? $"{age.TotalMinutes:F0}m ago"
                    : $"{age.TotalHours:F1}h ago";
                    
                commandsWriteLine($"{endpoint.Key,-22} {endpoint.Value.SessionId,-20} {endpoint.Value.ConnectionCount,-12} {ageStr}");
            }
            
            if (endpoints.Count == 0)
            {
                commandsWriteLine("  No dynamic endpoints registered");
            }
            
            commandsWriteLine($"\nTotal connections across all endpoints: {stats.TotalConnections}");
        }
        
        static void CleanupEndpoints()
        {
            var cleanedUp = DynamicEndpointGenerator.CleanupExpiredEndpoints(TimeSpan.FromHours(1));
            commandsWriteLine($"Cleaned up {cleanedUp} expired endpoints");
        }
    }
}
