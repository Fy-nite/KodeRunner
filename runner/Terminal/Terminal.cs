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
            if (w<150)
            {
                advancedterm = false;
            }
            if (advancedterm) {
                Console.Clear();
                commands =    new Window(0     , 0     , w/2f+2 , h+2       , "Console", true);
                connections = new Window(w/2f+1, 0     , w/2f+1 , h/4f+1.5f , "Connections");
                runnables =   new Window(w/2f+1, h/4f  , w/2f+1 , h/4f      , "Runnables");
                log =         new Window(w/2f+1, h/2f-0.5f, w/2f+1 , h/2f+2    , "Logs");

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
            } else {
                Console.WriteLine(str);
            }
        }

        public static void Write(string str, string window)
        {
            if (!advancedterm) {
                Console.Write(str);
                return;
            }
            switch (window) {
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
                commands.Write("> ");
                var command = await ReadString();
                if (string.IsNullOrEmpty(command))
                    continue;

                var parts = command.Split(' ');
                try {
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
                        case "help":
                            ShowHelp();
                            break;
                        case "import":
                            Implementations.Import(parts[1]);
                            break;
                        case "export":
                            Implementations.Export(parts[1]);
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
            commandsWriteLine("  help                  - Show this help message");
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
                    $"{conn.Id, -22} {conn.Type, -8} {conn.ConnectedAt:yyyy-MM-dd HH:mm:ss} {conn.ClientInfo}"
                );
            }
            if (advancedterm)
            {
                commands.WriteChar('\n');
            } else {Console.Write('\n');}
        }
        static async Task<string> ReadString()
        {
            if (!advancedterm) {
                return await Console.In.ReadLineAsync();
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
                        } else {
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
            int spacing = (int)Math.Round(len / (float)(data.Length+1));
            int sub = 0;
            int mul = 1;
            string ret = "";
            for (int i=0; i<data.Length; i++)
            {
                var str = data[i];
                if (i == 2)
                    ret += new string(Enumerable.Repeat(' ', (spacing*mul)-(str.Length/2)-sub).ToArray());
                else
                    ret += new string(Enumerable.Repeat(' ', (spacing*mul)-((str.Length+1)/2)-sub).ToArray());
                sub = str.Length/2;
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
            if (!advancedterm) {return;}
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
                try {
                    connections.WriteLine(CreatePaddedString(
                        new string[] {
                            connection.Id,
                            connection.Type,
                            $"{connection.ConnectedAt:yyyy-MM-dd HH:mm:ss}",
                            connection.ClientInfo
                        },
                        connections.bw
                    ));
                } catch (Exception ex) {Logger.Log(ex.Message, "Error");}
            }

            connections.Update();
        }
    }
}
