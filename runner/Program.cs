using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Reflection.Metadata;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using KodeRunner;
using KodeRunner.Config;
using Newtonsoft.Json;

namespace KodeRunner
{
    class Program
    {
        static ConnectionManager connectionManager = new ConnectionManager();
        static Dictionary<string, WebSocket> activeConnections =
            new Dictionary<string, WebSocket>();
        static TerminalProcess terminalProcess = new TerminalProcess();

        // Define the PMS_VERSION constant
        const string PMS_VERSION = "1.2.0";

        // Consolidate duplicate comment regex patterns
        public static List<string> CommentRegexes = new List<string>
        {
            @"^#.*$",
            @"^//.*$",
            @"^/\*.*\*/$",
            @"^<!--.*-->$",
            @"^\"".*\""$",
            @"^;.*$",
            @"^--",
            @"^/\*.*$", // Block comment start
            @".*\*/$", // Block comment end
        };

        // Add the RunnableManager as a static field
        static RunnableManager runnableManager = new RunnableManager();

        /// <summary>
        /// Main entry point for the application.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        static async Task Main(string[] args)
        {
            var config = Configuration.Load();
            Logger.Log("Starting KodeRunner...");
            // setup args for cmd creating dirs
            if (args.Length > 0)
            {
                if (args[0] == "init")
                {
                    BuildProcess initBuildProcess = new BuildProcess();
                    initBuildProcess.SetupCodeDir();
                    // stop the program after creating the dirs
                    return;
                }
            }
            var server = new HttpListener();
            server.Prefixes.Add($"http://{config.WebServer.Host}:{config.WebServer.Port}/");

            Provider.SettingsProvider settings = new Provider.SettingsProvider();
            runnableManager.LoadRunnables();

            // check the runnables dir for any dlls
            if (Directory.Exists(Core.RunnableDir))
            {
                // if there any dlls in the directory, load them
                if (Directory.GetFiles(Core.RunnableDir, "*.dll").Length > 0)
                {
                    runnableManager.LoadRunnablesFromDirectory(Core.RunnableDir);
                }
            }

            runnableManager.print();
            server.Start();

            Logger.Log($"KodeRunner v{Core.GetVersion()} started");
            Logger.Log(
                $"WebSocket server started at ws://{config.WebServer.Host}:{config.WebServer.Port}/"
            );

            BuildProcess buildProcess = new BuildProcess();
            buildProcess.SetupCodeDir();

            // Start the console command processor
            _ = Task.Run(ProcessConsoleCommands);

            while (true)
            {
                var context = await server.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    var path = context.Request.Url.AbsolutePath;
                    var wsContext = await context.AcceptWebSocketAsync(null);
                    var connectionId = "";

                    switch (path)
                    {
                        case "/code":
                            connectionId = connectionManager.AddConnection(
                                "code",
                                wsContext.WebSocket
                            );
                            await connectionManager.SendToConnection(
                                connectionId,
                                $"Welcome to KodeRunner Code Service\nYour connection ID: {connectionId}"
                            );
                            _ = HandleCodeWebSocket(wsContext.WebSocket, connectionId);
                            break;
                        case "/PMS":
                            connectionId = connectionManager.AddConnection(
                                "pms",
                                wsContext.WebSocket
                            );
                            await connectionManager.SendToConnection(
                                connectionId,
                                $"Welcome to KodeRunner!\nPMS Version: {PMS_VERSION}\nYour connection ID: {connectionId}\n"
                            );
                            _ = HandlePmsWebSocket(wsContext.WebSocket, connectionId);
                            break;
                        case "/stop":
                            connectionId = connectionManager.AddConnection(
                                "stop",
                                wsContext.WebSocket
                            );
                            await connectionManager.SendToConnection(
                                connectionId,
                                $"Welcome to KodeRunner Stop Service\nYour connection ID: {connectionId}\n"
                            );
                            _ = HandleStopWebSocket(wsContext.WebSocket, connectionId);
                            break;
                        case "/terminput":
                            connectionId = connectionManager.AddConnection(
                                "terminput",
                                wsContext.WebSocket
                            );
                            await connectionManager.SendToConnection(
                                connectionId,
                                $"Welcome to KodeRunner Terminal Input Service\nYour connection ID: {connectionId}\n"
                            );
                            _ = HandleTerminalInput(wsContext.WebSocket, connectionId);
                            break;
                        default:
                            Console.WriteLine($"Invalid endpoint: {path}");
                            break;
                    }
                }
            }
        }

        static async Task ProcessConsoleCommands()
        {
            while (true)
            {
                var command = await Console.In.ReadLineAsync();
                if (string.IsNullOrEmpty(command))
                    continue;

                var parts = command.Split(' ');
                switch (parts[0].ToLower())
                {
                    case "list":
                        ListConnections();
                        break;
                    case "disconnect":
                        if (parts.Length > 1)
                        {
                            await connectionManager.DisconnectById(parts[1]);
                        }
                        break;
                    case "disconnecttype":
                        if (parts.Length > 1)
                        {
                            await connectionManager.DisconnectByType(parts[1]);
                        }
                        break;
                    case "help":
                        ShowHelp();
                        break;
                    default:
                        Console.WriteLine("Unknown command. Type 'help' for available commands.");
                        break;
                }
            }
        }

        static void ShowHelp()
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine("  list              - List all active connections");
            Console.WriteLine("  disconnect <id>    - Disconnect a specific connection");
            Console.WriteLine("  disconnecttype <type> - Disconnect all connections of a type");
            Console.WriteLine("  help              - Show this help message");
        }

        static void ListConnections()
        {
            var connections = connectionManager.ListConnections();
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

        static async Task HandleTerminalInput(WebSocket webSocket, string connectionId)
        {
            try
            {
                var buffer = new byte[8192];

                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None
                    );

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "",
                            CancellationToken.None
                        );
                        connectionManager.RemoveConnection(connectionId);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        // More efficient direct string conversion without memory stream
                        var input = Encoding.UTF8.GetString(buffer, 0, result.Count).TrimEnd();
                        _ = terminalProcess.SendInput(input);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Terminal input error: {ex.Message}", "Error");
                connectionManager.RemoveConnection(connectionId);
            }
        }

        static async Task HandleCodeWebSocket(WebSocket webSocket, string connectionId)
        {
            Console.WriteLine("Code endpoint connected");
            var projectnamefound = false;
            var filenamefound = false;
            try
            {
                var buffer = new byte[8192];

                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None
                    );

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "",
                            CancellationToken.None
                        );
                        connectionManager.RemoveConnection(connectionId);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            await memoryStream.WriteAsync(buffer, 0, result.Count);
                            _ = memoryStream.Seek(0, SeekOrigin.Begin);
                            var message = await ReadFromMemoryStream(memoryStream);
                            var lines = message.Split('\n');
                            if (lines.Length == 0)
                                continue;
                            //Console.WriteLine($"Received message: {message}");
                            var commentLines = lines
                                .Where(line =>
                                    CommentRegexes.Any(regex => Regex.IsMatch(line, regex))
                                )
                                .ToList();
                            var commentContent = new StringBuilder();
                            bool inBlockComment = false;

                            foreach (var line in lines)
                            {
                                if (Regex.IsMatch(line, @"^/\*.*\*/$")) // Single line block comment
                                {
                                    _ = commentContent.AppendLine(line);
                                }
                                else if (Regex.IsMatch(line, @"^/\*.*$")) // Start of block comment
                                {
                                    _ = commentContent.AppendLine(line);
                                    inBlockComment = true;
                                }
                                else if (Regex.IsMatch(line, @".*\*/$")) // End of block comment
                                {
                                    _ = commentContent.AppendLine(line);
                                    inBlockComment = false;
                                }
                                else if (
                                    inBlockComment
                                    || CommentRegexes.Any(regex => Regex.IsMatch(line, regex))
                                )
                                {
                                    _ = commentContent.AppendLine(line);
                                }
                                else
                                {
                                    _ = commentContent.AppendLine(line);
                                }
                            }

                            var commentText = commentContent.ToString();
                            var fileNameMatch = Regex.Match(commentText, @"File_name\s*:\s*(.*)");
                            var projectNameMatch = Regex.Match(commentText, @"Project\s*:\s*(.*)");

                            if (fileNameMatch.Success)
                            {
                                Console.WriteLine($"File name: {fileNameMatch.Groups[1].Value}");
                                filenamefound = true;
                            }

                            if (projectNameMatch.Success)
                            {
                                Console.WriteLine(
                                    $"Project name: {projectNameMatch.Groups[1].Value}"
                                );
                                projectnamefound = true;
                            }
                            if (projectnamefound && filenamefound)
                            {
                                // Trim project and file names to remove any extra whitespace
                                string projectName = projectNameMatch.Groups[1].Value.Trim();
                                string fileName = fileNameMatch.Groups[1].Value.Trim();

                                // Use Path.Combine to construct paths
                                string project_path = Path.Combine(
                                    Core.RootDir,
                                    Core.CodeDir,
                                    projectName
                                );
                                string file_path = Path.Combine(project_path, fileName);

                                if (!Directory.Exists(project_path))
                                {
                                    _ = Directory.CreateDirectory(project_path);
                                }

                                File.WriteAllText(file_path, message);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebSocket error: {ex.Message}");
                connectionManager.RemoveConnection(connectionId);
                runnableManager.LoadRunnables();
            }
        }

        public static async Task<string> ReadFromMemoryStream(MemoryStream memoryStream)
        {
            _ = memoryStream.Seek(0, SeekOrigin.Begin);
            using (var reader = new StreamReader(memoryStream, Encoding.UTF8))
            {
                return await reader.ReadToEndAsync();
            }
        }

        public static async Task SendToWebSocket(string endpoint, string message)
        {
            if (
                activeConnections.TryGetValue(endpoint, out WebSocket socket)
                && socket.State == WebSocketState.Open
            )
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
            }
        }

        static async Task HandlePmsWebSocket(WebSocket webSocket, string connectionId)
        {
            Console.WriteLine("PMS endpoint connected");
            _ = SendToWebSocket(
                "PMS",
                $"Welcome to KodeRunner!\n PMS Version: {PMS_VERSION}\n Have a nice day!"
            );
            // setup memory stream like in HandleCodeWebSocket
            var ProjectName = "";
            var FileName = "";
            var Main_File = "";
            var Project_Build_Systems = "";
            var Project_Output = "";
            var Run_On_Build = false;

            /*
            template for the json message
             {
            "PMS_System": "1.2.0",
        "Project_Name": "text",
        "Main_File": "main.c",
        "Project_Build_Systems": "cmake",
        "Project_Output": "main",
        "Run_On_Build": "True"
    }
            */

            try
            {
                var buffer = new byte[8192];

                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None
                    );

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "",
                            CancellationToken.None
                        );
                        connectionManager.RemoveConnection(connectionId);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            await memoryStream.WriteAsync(buffer, 0, result.Count);
                            _ = memoryStream.Seek(0, SeekOrigin.Begin);
                            var message = await ReadFromMemoryStream(memoryStream);
                            Console.WriteLine($"Received message: {message}");
                            // we now have the json message in the message variable
                            // we can now parse it into a dictionary
                            var messageDict = JsonConvert.DeserializeObject<
                                Dictionary<string, string>
                            >(message);
                            // we can now access the values in the dictionary
                            if (messageDict.TryGetValue("Project_Name", out string project))
                            {
                                Console.WriteLine($"Project: {project}");
                                ProjectName = project.Trim();
                            }
                            if (messageDict.TryGetValue("Main_File", out string mainfile))
                            {
                                Console.WriteLine($"Main File: {mainfile}");
                                Main_File = mainfile;
                            }
                            if (
                                messageDict.TryGetValue(
                                    "Project_Build_Systems",
                                    out string buildsystems
                                )
                            )
                            {
                                Console.WriteLine($"Build Systems: {buildsystems}");
                                Project_Build_Systems = buildsystems;
                            }
                            if (messageDict.TryGetValue("Project_Output", out string output))
                            {
                                Console.WriteLine($"Project Output: {output}");
                                Project_Output = output;
                            }
                            if (messageDict.TryGetValue("Run_On_Build", out string runonbuild))
                            {
                                Console.WriteLine($"Run On Build: {runonbuild}");
                                Run_On_Build = runonbuild == "True";
                            }

                            if (messageDict.TryGetValue("Includes", out string includesJson))
                            {
                                try
                                {
                                    var includes = JsonConvert.DeserializeObject<string[]>(
                                        includesJson
                                    );
                                    var includeProjectPath = Path.Combine(
                                        Core.RootDir,
                                        Core.CodeDir,
                                        ProjectName
                                    );
                                    var includeManager = new IncludeManager();
                                    var includedFiles = await includeManager.ProcessIncludes(
                                        includeProjectPath,
                                        Project_Build_Systems,
                                        includes
                                    );

                                    // Add included files to response
                                    if (includedFiles.Count > 0)
                                    {
                                        var includesResponse = JsonConvert.SerializeObject(
                                            new { included_files = includedFiles }
                                        );
                                        var responseBytes = Encoding.UTF8.GetBytes(
                                            includesResponse
                                        );
                                        await webSocket.SendAsync(
                                            new ArraySegment<byte>(responseBytes),
                                            WebSocketMessageType.Text,
                                            true,
                                            CancellationToken.None
                                        );
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Log($"Error processing includes: {ex.Message}", "Error");
                                }
                            }

                            // Ensure parent directories exist
                            string project_path = Path.Combine(
                                Core.RootDir,
                                Core.CodeDir,
                                ProjectName
                            );
                            string file_path = Path.Combine(project_path, Core.ConfigFile);

                            // if (!Directory.Exists(project_path))
                            // {
                            //     Directory.CreateDirectory(project_path);
                            // }

                            //File.WriteAllText(file_path, message);

                            // we can now build the project using the IRunnableManager
                            // we can use the project name to get the project directory, and the main file to build the project
                            // we can use the build systems to determine how to build the project

                            // look for the matching runnable
                            Provider.ISettingsProvider settings = new Provider.SettingsProvider();
                            settings.Main_File = Main_File;
                            settings.ProjectName = ProjectName;
                            settings.Run_On_Build = Run_On_Build;
                            settings.Language = Project_Build_Systems;
                            settings.Output = Project_Output;
                            settings.PmsWebSocket = webSocket; // Set the PMS WebSocket
                            // Set the ProjectPath
                            settings.ProjectPath = Path.Combine(
                                Core.RootDir,
                                Core.CodeDir,
                                ProjectName
                            );
                            // Use the existing runnableManager instance
                            try
                            {
                                runnableManager.ExecuteFirstMatchingLanguage(
                                    Project_Build_Systems,
                                    settings
                                );
                                runnableManager.print();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error executing runnable: {ex.Message}");
                                var errorMessage = JsonConvert.SerializeObject(
                                    new
                                    {
                                        error = true,
                                        message = $"Failed to execute runnable: {ex.Message}",
                                    }
                                );
                                var errorBytes = Encoding.UTF8.GetBytes(errorMessage);
                                await webSocket.SendAsync(
                                    new ArraySegment<byte>(errorBytes),
                                    WebSocketMessageType.Text,
                                    true,
                                    CancellationToken.None
                                );
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebSocket error: {ex.Message}");
                connectionManager.RemoveConnection(connectionId);
                runnableManager.LoadRunnables();
            }
        }

        static async Task HandleStopWebSocket(WebSocket webSocket, string connectionId)
        {
            Console.WriteLine("Stop endpoint connected");
            try
            {
                var buffer = new byte[1024];

                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None
                    );

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "",
                            CancellationToken.None
                        );
                        connectionManager.RemoveConnection(connectionId);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        await using (var memoryStream = new MemoryStream())
                        {
                            await memoryStream.WriteAsync(
                                buffer.AsMemory(0, result.Count),
                                CancellationToken.None
                            );
                            var message = await ReadFromMemoryStream(memoryStream);
                            var messageDict = JsonConvert.DeserializeObject<
                                Dictionary<string, bool>
                            >(message);

                            if (
                                messageDict != null
                                && messageDict.TryGetValue("stopped", out bool shouldStop)
                                && shouldStop
                            )
                            {
                                Console.WriteLine("Stopping all processes...");
                                TerminalProcess.StopAllProcesses();

                                // Send confirmation back to client
                                var response = JsonConvert.SerializeObject(
                                    new { stopped = true, message = "All processes stopped" }
                                );
                                var responseBytes = Encoding.UTF8.GetBytes(response);
                                await webSocket.SendAsync(
                                    new ArraySegment<byte>(responseBytes),
                                    WebSocketMessageType.Text,
                                    true,
                                    CancellationToken.None
                                );
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebSocket error: {ex.Message}");
                connectionManager.RemoveConnection(connectionId);
                runnableManager.LoadRunnables();
            }
        }
    }
}
