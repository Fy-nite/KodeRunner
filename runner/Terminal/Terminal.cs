namespace KodeRunner.Terminal
{
    class Terminal
    {
        static Window commands;
        static Window connections;
        static Window runnables;
        public static void init()
        {
            var w = Console.WindowWidth;
            var h = Console.WindowHeight;
            Console.Clear();
            commands = new Window(0, 0, (w+1)/2+1, h+2, "Console");
            connections = new Window((w+1)/2, 0, (w+1)/2+1, (h+1)/2+1, "Connections");
            runnables = new Window((w+1)/2, (h+1)/2, (w+1)/2+1, (h+1)/2+2, "Runnables");

            Console.Write("\x1b[1;1H\x1b[?25l");
            _ = Task.Run(handleCommands); // Process commands without stoping entire console interface
            
        }
        
        public static async Task handleCommands()
        {
            while (true)
            {
                var command = await Console.In.ReadLineAsync();
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
                            Console.WriteLine("Unknown command. Type 'help' for available commands.");
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
            Console.WriteLine("Available commands:");
            Console.WriteLine("  list                  - List all active connections");
            Console.WriteLine("  disconnect <id>       - Disconnect a specific connection");
            Console.WriteLine("  disconnecttype <type> - Disconnect all connections of a type");
            Console.WriteLine("  import <project file> - Import a .KRproject file");
            Console.WriteLine("  export <project name> - Export a project into a .KRproject file");
            Console.WriteLine("  help                  - Show this help message");
        }

        static void ListConnections()
        {
            var connections = Program.connectionManager.ListConnections();
            Console.WriteLine("\nActive connections:");
            Console.WriteLine("ID                     Type     Connected At          Client Info");
            Console.WriteLine("---------------------- -------- -------------------- ------------");
            foreach (var conn in connections)
            {
                Console.WriteLine(
                    $"{conn.Id, -22} {conn.Type, -8} {conn.ConnectedAt:yyyy-MM-dd HH:mm:ss} {conn.ClientInfo}"
                );
            }
            Console.WriteLine();
        }
        public static void Update()
        {
            commands.Update();
            connections.Update();
            runnables.Update();
        }
        public static void UpdateLoop()
        {
            while (true)
            {
                Update();
                commands.Write($"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa a");
            }
        }
    }
}
