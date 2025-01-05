namespace KodeRunner.Terminal
{
    class Terminal
    {
        static Window commands;
        static Window connections;
        static Window runnables;
        static Window log;
        
        public static void init()
        {
            var w = Console.WindowWidth;
            var h = Console.WindowHeight;
            Console.Clear();
            commands =    new Window(0     , 0       , w/2f+2 , h+2       , "Console", true);
            connections = new Window(w/2f+1, 0       , w/2f+1 , h/4f+1.5f , "Connections");
            runnables =   new Window(w/2f+1, h/4f    , w/2f+1 , h/4f      , "Runnables");
            log =         new Window(w/2f+1, h/2f    , w/2f+1 , h/2f+2    , "Logs");

            Console.Write("\x1b[1;1H\x1b[?25l");
            _ = Task.Run(handleCommands);
            _ = Task.Run(RunnablesWindow);
            //_ = Task.Run(handleCommands);
        }

        public static void Write(string str, string window)
        {
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
                            commands.WriteLine("Unknown command. Type 'help' for available commands.");
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
            commands.WriteLine("Available commands:");
            commands.WriteLine("  list                  - List all active connections");
            commands.WriteLine("  disconnect <id>       - Disconnect a specific connection");
            commands.WriteLine("  disconnecttype <type> - Disconnect all connections of a type");
            commands.WriteLine("  import <project file> - Import a .KRproject file");
            commands.WriteLine("  export <project name> - Export a project into a .KRproject file");
            commands.WriteLine("  help                  - Show this help message");
        }

        static void ListConnections()
        {
            var connections = Program.connectionManager.ListConnections();
            commands.WriteLine("\nActive connections:");
            commands.WriteLine("ID                     Type     Connected At          Client Info");
            commands.WriteLine("---------------------- -------- -------------------- ------------");
            foreach (var conn in connections)
            {
                commands.WriteLine(
                    $"{conn.Id, -22} {conn.Type, -8} {conn.ConnectedAt:yyyy-MM-dd HH:mm:ss} {conn.ClientInfo}"
                );
            }
            commands.WriteChar('\n');
        }
        static async Task<string> ReadString()
        {
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
        static async Task RunnablesWindow()
        {
            int spacing = (int)Math.Round(runnables.bw / (float)(RunnablesHeader.Length*2));
            int len = 0;
            int mul = 1;
            string header = "";
            for (int i=0; i<RunnablesHeader.Length; i++)
            {
                var str = RunnablesHeader[i];
                header += new string(Enumerable.Repeat(' ', (spacing*mul)-(str.Length/2)-len).ToArray());
                len = str.Length/2;
                mul = 2;
                header += str;
            }
            header += "\n" + new string(Enumerable.Repeat('-', runnables.bw).ToArray()) + "\n";
            while (true)
            {
                runnables.Clear();
                runnables.Write(header);
                await Task.Delay(Core.RunnablesUpdateTime);
            }
        }
    }
}
