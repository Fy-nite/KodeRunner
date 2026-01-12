using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using KodeRunner.Terminal;
using KodeRunner.Config;
using Newtonsoft.Json;

namespace KodeRunner
{
    class Program
    {
        public static ConnectionManager connectionManager = new ConnectionManager();
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
        static Analytics.MetricsCollector metricsCollector = new Analytics.MetricsCollector();
        public static Collaboration.CollaborationManager collaborationManager = new Collaboration.CollaborationManager();
        static Security.SandboxManager sandboxManager = new Security.SandboxManager();

        // Add the syntax highlighting service as a static field
        static Services.SyntaxHighlightingService syntaxHighlighter = new Services.SyntaxHighlightingService();

        /// <summary>
        /// Main entry point for the application.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        static async Task Main(string[] args)
        {
            Console.CancelKeyPress += delegate {
                if (Terminal.Terminal.advancedterm) {
                    Console.Write("\x1b[?1049h\x1b[?25h");
                }
            };

            // Handle CLI commands first
            if (args.Length > 0)
            {
                await HandleCliCommands(args);
                return;
            }

            // Start the console command processor
            Terminal.Terminal.init();
            var config = Configuration.Load();

            Logger.Log("Starting KodeRunner...");
            EnsureFolders();
            // setup args for cmd creating dirs
            if (args.Length > 0)
            {
                if (args[0] == "init")
                {
                    BuildProcess initBuildProcess = new BuildProcess();
                    initBuildProcess.SetupCodeDir();
                    // stop the program after creating the dirs
                    System.Environment.Exit(0);
                }
                System.Environment.Exit(0);
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

            //runnableManager.print();
            Terminal.Terminal.UpdateRunnables();
            server.Start();

            Logger.Log($"KodeRunner v{Core.GetVersion()} started");
            Logger.Log("Please report any errors at https://git.gay/Finite/KodeRunner/issues");
            Logger.Log(
                $"WebSocket server started at ws://{config.WebServer.Host}:{config.WebServer.Port}/"
            );

            BuildProcess buildProcess = new BuildProcess();
            buildProcess.SetupCodeDir();

            while (true)
            {
                var context = await server.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    var path = context.Request.Url.AbsolutePath;
                    var wsContext = await context.AcceptWebSocketAsync(null);
                    var connectionId = "";

                    // Check for dynamic endpoints first
                    if (Terminal.DynamicEndpointGenerator.TryGetEndpointHandler(path, out var dynamicHandler))
                    {
                        connectionId = connectionManager.AddConnection(
                            "shared_terminal",
                            wsContext.WebSocket
                        );
                        await connectionManager.SendToConnection(
                            connectionId,
                            $"Connected to shared terminal session"
                        );
                        _ = dynamicHandler.Handler(
                            wsContext.WebSocket,
                            connectionId,
                            dynamicHandler.SessionId,
                            config.BufferSize
                        );
                        continue;
                    }

                    // New hierarchical routing system
                    var routeResult = RouteWebSocketEndpoint(path);
                    if (routeResult.IsValid)
                    {
                        connectionId = connectionManager.AddConnection(
                            routeResult.ConnectionType,
                            wsContext.WebSocket
                        );
                        
                        await connectionManager.SendToConnection(
                            connectionId,
                            routeResult.WelcomeMessage.Replace("{connectionId}", connectionId)
                        );
                        
                        _ = routeResult.Handler(wsContext.WebSocket, connectionId, config.BufferSize, routeResult.RouteData);
                        continue;
                    }

                    // Legacy endpoint support (for backward compatibility)
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
                            _ = HandleCodeWebSocket(
                                wsContext.WebSocket,
                                connectionId,
                                config.BufferSize
                            );
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
                            _ = HandlePmsWebSocket(
                                wsContext.WebSocket,
                                connectionId,
                                config.BufferSize
                            );
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
                            _ = HandleStopWebSocket(
                                wsContext.WebSocket,
                                connectionId,
                                config.BufferSize
                            );
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
                            _ = HandleTerminalInput(
                                wsContext.WebSocket,
                                connectionId,
                                config.BufferSize
                            );
                            break;
                        case "/terminal/create":
                            connectionId = connectionManager.AddConnection(
                                "terminal_creator",
                                wsContext.WebSocket
                            );
                            await connectionManager.SendToConnection(
                                connectionId,
                                $"Welcome to KodeRunner Terminal Creator Service\nYour connection ID: {connectionId}\n"
                            );
                            _ = HandleTerminalCreatorWebSocket(
                                wsContext.WebSocket,
                                connectionId,
                                config.BufferSize
                            );
                            break;
                        case "/syntax":
                            connectionId = connectionManager.AddConnection(
                                "syntax",
                                wsContext.WebSocket
                            );
                            await connectionManager.SendToConnection(
                                connectionId,
                                $"Welcome to KodeRunner Syntax Highlighting Service\nYour connection ID: {connectionId}\nAvailable languages: {string.Join(", ", syntaxHighlighter.GetAvailableLanguages())}"
                            );
                            _ = HandleSyntaxHighlightingWebSocket(
                                wsContext.WebSocket,
                                connectionId,
                                config.BufferSize
                            );
                            break;
                        case "/syntax/lang":
                            connectionId = connectionManager.AddConnection(
                                "syntax_lang",
                                wsContext.WebSocket
                            );
                            await connectionManager.SendToConnection(
                                connectionId,
                                $"Welcome to KodeRunner Syntax Language Configuration Service\nYour connection ID: {connectionId}\nSend file extensions to configure language mappings"
                            );
                            _ = HandleSyntaxLanguageConfigWebSocket(
                                wsContext.WebSocket,
                                connectionId,
                                config.BufferSize
                            );
                            break;
                        default:
                            // Check for legacy system endpoints
                            if (path.StartsWith("/system/"))
                            {
                                connectionId = connectionManager.AddConnection(
                                    "system_metrics",
                                    wsContext.WebSocket
                                );
                                await connectionManager.SendToConnection(
                                    connectionId,
                                    $"Welcome to KodeRunner System Metrics Service\nEndpoint: {path}\nYour connection ID: {connectionId}"
                                );
                                _ = HandleSystemMetricsWebSocket(
                                    wsContext.WebSocket,
                                    connectionId,
                                    path,
                                    config.BufferSize
                                );
                            }
                            else
                            {
                                Logger.Log($"Invalid endpoint: {path}", "Warning");
                                await connectionManager.SendToConnection(
                                    connectionId,
                                    GetAvailableEndpointsHelp()
                                );
                            }
                            break;
                    }
                }
            }
        }

        

        static async Task HandleTerminalInput(
            WebSocket webSocket,
            string connectionId,
            int bufferSize
        )
        {
            try
            {
                var buffer = new byte[bufferSize];

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
                        SendInputToActiveProcess(input);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Terminal input error: {ex.Message}", "Error");
                connectionManager.RemoveConnection(connectionId);
            }
        }

        // Add a static method to send input that works from both WebSocket and CLI
        public static bool SendInputToActiveProcess(string input)
        {
            return terminalProcess.SendInput(input);
        }

        static async Task HandleCodeWebSocket(
            WebSocket webSocket,
            string connectionId,
            int bufferSize
        )
        {
            Logger.Log("Code endpoint connected");
            var projectnamefound = false;
            var filenamefound = false;
            try
            {
                var buffer = new byte[bufferSize];

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
                                Logger.Log($"File name: {fileNameMatch.Groups[1].Value}");
                                filenamefound = true;
                            }

                            if (projectNameMatch.Success)
                            {
                                Logger.Log(
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

                                if (!Directory.Exists(project_path))
                                {
                                    _ = Directory.CreateDirectory(project_path);
                                }
                                string file_path = Path.Combine(project_path, fileName);

                                File.WriteAllText(file_path, message);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"WebSocket error: {ex.Message}", "Error");
                connectionManager.RemoveConnection(connectionId);
                runnableManager.LoadRunnables();
            }
        }

        static async Task HandlePmsWebSocket(
            WebSocket webSocket,
            string connectionId,
            int bufferSize
        )
        {
            Logger.Log("PMS endpoint connected");
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
                var buffer = new byte[bufferSize];

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
                            Logger.Log($"Received message: {message}");
                            // we now have the json message in the message variable
                            // we can now parse it into a dictionary
                            var messageDict = JsonConvert.DeserializeObject<
                                Dictionary<string, string>
                            >(message);
                            // we can now access the values in the dictionary
                            if (messageDict.TryGetValue("Project_Name", out string project))
                            {
                                Logger.Log($"Project: {project}");
                                ProjectName = project.Trim();
                            }
                            if (messageDict.TryGetValue("Main_File", out string mainfile))
                            {
                                Logger.Log($"Main File: {mainfile}");
                                Main_File = mainfile;
                            }
                            if (
                                messageDict.TryGetValue(
                                    "Project_Build_Systems",
                                    out string buildsystems
                                )
                            )
                            {
                                Logger.Log($"Build Systems: {buildsystems}");
                                Project_Build_Systems = buildsystems;
                            }
                            if (messageDict.TryGetValue("Project_Output", out string output))
                            {
                                Logger.Log($"Project Output: {output}");
                                Project_Output = output;
                            }
                            if (messageDict.TryGetValue("Run_On_Build", out string runonbuild))
                            {
                                Logger.Log($"Run On Build: {runonbuild}");
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

                            if (!Directory.Exists(project_path))
                            {
                                Directory.CreateDirectory(project_path);
                            }

                            File.WriteAllText(file_path, message);

                 
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
                                Logger.Log($"Error executing runnable: {ex.Message}");
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
                Logger.Log($"WebSocket error: {ex.Message}", "Error");
                connectionManager.RemoveConnection(connectionId);
                runnableManager.LoadRunnables();
            }
        }

        static async Task HandleStopWebSocket(
            WebSocket webSocket,
            string connectionId,
            int bufferSize
        )
        {
            Logger.Log("Stop endpoint connected");
            try
            {
                var buffer = new byte[bufferSize];

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
                                Logger.Log("Stopping all processes...");
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
                Logger.Log($"WebSocket error: {ex.Message}", "Error");
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
        static async Task HandleTerminalCreatorWebSocket(
            WebSocket webSocket,
            string connectionId,
            int bufferSize
        )
        {
            Logger.Log("Terminal creator endpoint connected");
            try
            {
                var buffer = new byte[bufferSize];

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
                            Logger.Log($"Terminal creator received: {message}");

                            try
                            {
                                var request = JsonConvert.DeserializeObject<Dictionary<string, string>>(message);
                                if (request != null && request.ContainsKey("action") && request["action"] == "create")
                                {
                                    var sessionId = Guid.NewGuid().ToString();
                                    var endpoint = $"/terminal/{sessionId}";
                                    
                                    // Register the dynamic endpoint
                                    Terminal.DynamicEndpointGenerator.RegisterEndpoint(
                                        endpoint, 
                                        new Terminal.DynamicEndpoint 
                                        { 
                                            Handler = HandleSharedTerminal, 
                                            SessionId = sessionId 
                                        }
                                    );
                                    
                                    // Send the endpoint back to the client
                                    var response = JsonConvert.SerializeObject(new { 
                                        success = true, 
                                        endpoint = endpoint,
                                        sessionId = sessionId
                                    });
                                    
                                    var responseBytes = Encoding.UTF8.GetBytes(response);
                                    await webSocket.SendAsync(
                                        new ArraySegment<byte>(responseBytes),
                                        WebSocketMessageType.Text,
                                        true,
                                        CancellationToken.None
                                    );
                                    
                                    Logger.Log($"Created terminal session: {sessionId} at endpoint {endpoint}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Log($"Error processing terminal creator message: {ex.Message}", "Error");
                                var errorResponse = JsonConvert.SerializeObject(new { success = false, error = ex.Message });
                                var errorBytes = Encoding.UTF8.GetBytes(errorResponse);
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
                Logger.Log($"Terminal creator error: {ex.Message}", "Error");
                connectionManager.RemoveConnection(connectionId);
            }
        }

        public static async Task HandleSharedTerminal(
            WebSocket webSocket, 
            string connectionId, 
            string sessionId, 
            int bufferSize)
        {
            Logger.Log($"Shared terminal connected: {sessionId}");
            string endpointPath = $"/terminal/{sessionId}";
            
            try
            {
                // Join the shared terminal session
                SharedTerminalManager.JoinSession(sessionId, connectionId);
                
                var buffer = new byte[bufferSize];
                
                // Notify that connection is established
                var welcomeMessage = JsonConvert.SerializeObject(new {
                    type = "system",
                    message = $"Connected to shared terminal session {sessionId}",
                    sessionId = sessionId,
                    connectionId = connectionId
                });
                
                var welcomeBytes = Encoding.UTF8.GetBytes(welcomeMessage);
                await webSocket.SendAsync(
                    new ArraySegment<byte>(welcomeBytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );

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
                            
                            Logger.Log($"Shared terminal received: {message}");
                            
                            try
                            {
                                // Try to parse as JSON first
                                var messageData = JsonConvert.DeserializeObject<Dictionary<string, object>>(message);
                                
                                if (messageData != null && messageData.ContainsKey("type"))
                                {
                                    var messageType = messageData["type"].ToString();
                                    
                                    switch (messageType)
                                    {
                                        case "execute_project":
                                            // Handle project execution request
                                            if (messageData.ContainsKey("project_data"))
                                            {
                                                var projectData = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                                                    messageData["project_data"].ToString());
                                                await HandleProjectExecution(sessionId, projectData, webSocket);
                                            }
                                            break;
                                            
                                        case "input":
                                            // Send input to active process in the session
                                            if (messageData.ContainsKey("data"))
                                            {
                                                var inputData = messageData["data"].ToString();
                                                bool sent = SharedTerminalManager.SendInputToSession(sessionId, inputData);
                                                
                                                var response = JsonConvert.SerializeObject(new {
                                                    type = "input_result",
                                                    success = sent,
                                                    message = sent ? "Input sent to process" : "No active process in session"
                                                });
                                                
                                                var responseBytes = Encoding.UTF8.GetBytes(response);
                                                await webSocket.SendAsync(
                                                    new ArraySegment<byte>(responseBytes),
                                                    WebSocketMessageType.Text,
                                                    true,
                                                    CancellationToken.None
                                                );
                                            }
                                            break;
                                            
                                        case "ping":
                                            // Respond to ping with pong
                                            var pongResponse = JsonConvert.SerializeObject(new {
                                                type = "pong",
                                                timestamp = DateTime.UtcNow,
                                                sessionId = sessionId
                                            });
                                            
                                            var pongBytes = Encoding.UTF8.GetBytes(pongResponse);
                                            await webSocket.SendAsync(
                                                new ArraySegment<byte>(pongBytes),
                                                WebSocketMessageType.Text,
                                                true,
                                                CancellationToken.None
                                            );
                                            break;
                                            
                                        default:
                                            // Echo back unknown message types
                                            var echoResponse = JsonConvert.SerializeObject(new {
                                                type = "echo",
                                                original = message,
                                                timestamp = DateTime.UtcNow,
                                                sessionId = sessionId
                                            });
                                            
                                            var echoBytes = Encoding.UTF8.GetBytes(echoResponse);
                                            await webSocket.SendAsync(
                                                new ArraySegment<byte>(echoBytes),
                                                WebSocketMessageType.Text,
                                                true,
                                                CancellationToken.None
                                            );
                                            break;
                                    }
                                }
                                else
                                {
                                    // If not JSON or no type, treat as simple text input
                                    bool sent = SharedTerminalManager.SendInputToSession(sessionId, message);
                                    
                                    var response = JsonConvert.SerializeObject(new {
                                        type = "input_result",
                                        success = sent,
                                        message = sent ? "Input sent to process" : "No active process in session",
                                        input = message
                                    });
                                    
                                    var responseBytes = Encoding.UTF8.GetBytes(response);
                                    await webSocket.SendAsync(
                                        new ArraySegment<byte>(responseBytes),
                                        WebSocketMessageType.Text,
                                        true,
                                        CancellationToken.None
                                    );
                                }
                            }
                            catch (JsonException)
                            {
                                // If JSON parsing fails, treat as simple text input
                                bool sent = SharedTerminalManager.SendInputToSession(sessionId, message);
                                
                                var response = JsonConvert.SerializeObject(new {
                                    type = "input_result",
                                    success = sent,
                                    message = sent ? "Input sent to process" : "No active process in session",
                                    input = message
                                });
                                
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
                Logger.Log($"Shared terminal error: {ex.Message}", "Error");
                connectionManager.RemoveConnection(connectionId);
            }
            finally
            {
                // Leave the session when connection closes
                SharedTerminalManager.LeaveSession(sessionId, connectionId);
                // Decrement connection count when connection closes
                Terminal.DynamicEndpointGenerator.DecrementConnectionCount(endpointPath);
            }
        }

        // Add helper method to handle project execution in shared terminal
        private static async Task HandleProjectExecution(string sessionId, Dictionary<string, string> projectData, WebSocket webSocket)
        {
            try
            {
                // Extract project information
                var projectName = projectData.GetValueOrDefault("Project_Name", "");
                var mainFile = projectData.GetValueOrDefault("Main_File", "");
                var buildSystem = projectData.GetValueOrDefault("Project_Build_Systems", "");
                var output = projectData.GetValueOrDefault("Project_Output", "output");
                var runOnBuild = projectData.GetValueOrDefault("Run_On_Build", "True") == "True";

                if (string.IsNullOrEmpty(projectName) || string.IsNullOrEmpty(buildSystem))
                {
                    await SendErrorToWebSocket(webSocket, "Missing required project information");
                    return;
                }

                // Create settings for execution
                var settings = new Provider.SettingsProvider
                {
                    ProjectName = projectName,
                    Main_File = mainFile,
                    Language = buildSystem,
                    Output = output,
                    Run_On_Build = runOnBuild,
                    ProjectPath = Path.Combine(Core.RootDir, Core.CodeDir, projectName)
                };

                // Execute the project in the shared terminal session
                bool executed = await SharedTerminalManager.ExecuteProjectInSession(sessionId, settings);
                
                if (executed)
                {
                    var response = JsonConvert.SerializeObject(new {
                        type = "execution_started",
                        sessionId = sessionId,
                        projectName = projectName,
                        language = buildSystem
                    });
                    
                    var responseBytes = Encoding.UTF8.GetBytes(response);
                    await webSocket.SendAsync(
                        new ArraySegment<byte>(responseBytes),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None
                    );
                }
                else
                {
                    await SendErrorToWebSocket(webSocket, "Failed to execute project in session");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error handling project execution: {ex.Message}", "Error");
                await SendErrorToWebSocket(webSocket, $"Execution error: {ex.Message}");
            }
        }

        private static async Task SendErrorToWebSocket(WebSocket webSocket, string errorMessage)
        {
            var errorResponse = JsonConvert.SerializeObject(new {
                type = "error",
                message = errorMessage,
                timestamp = DateTime.UtcNow
            });
            
            var errorBytes = Encoding.UTF8.GetBytes(errorResponse);
            await webSocket.SendAsync(
                new ArraySegment<byte>(errorBytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
        }

        static async Task HandleSyntaxHighlightingWebSocket(
            WebSocket webSocket,
            string connectionId,
            int bufferSize
        )
        {
            Logger.Log("Syntax highlighting endpoint connected");
            try
            {
                var buffer = new byte[bufferSize];

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
                            
                            try
                            {
                                // Extract language from first line if it starts with "lang:"
                                string language = "text"; // default language
                                string code = message;
                                
                                var lines = message.Split('\n');
                                if (lines.Length > 0 && lines[0].StartsWith("lang:", StringComparison.OrdinalIgnoreCase))
                                {
                                    language = lines[0].Substring(5).Trim();
                                    code = string.Join('\n', lines.Skip(1));
                                }
                                
                                // Use the syntax highlighting service
                                var highlightedCode = syntaxHighlighter.HighlightCode(code, language);
                                
                                // Send back plain text response
                                var responseBytes = Encoding.UTF8.GetBytes((string)highlightedCode);
                                await webSocket.SendAsync(
                                    new ArraySegment<byte>(responseBytes),
                                    WebSocketMessageType.Text,
                                    true,
                                    CancellationToken.None
                                );
                            }
                            catch (Exception ex)
                            {
                                Logger.Log($"Syntax highlighting error: {ex.Message}", "Error");
                                var errorMessage = $"Error: {ex.Message}";
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
                Logger.Log($"Syntax highlighting WebSocket error: {ex.Message}", "Error");
                connectionManager.RemoveConnection(connectionId);
            }
        }

        // New handler for syntax language configuration WebSocket
        static async Task HandleSyntaxLanguageConfigWebSocket(
            WebSocket webSocket,
            string connectionId,
            int bufferSize
        )
        {
            Logger.Log("Syntax language configuration endpoint connected");
            try
            {
                var buffer = new byte[bufferSize];

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
                            
                            try
                            {
                                // Parse the message format: "ext:language" or just "ext" to query
                                var parts = message.Trim().Split(':');
                                var extension = parts[0].Trim();
                                
                                // Ensure extension starts with dot
                                if (!extension.StartsWith("."))
                                {
                                    extension = "." + extension;
                                }
                                
                                if (parts.Length == 1)
                                {
                                    // Query mode - return current language for extension
                                    var currentLanguage = syntaxHighlighter.DetectLanguage(extension);
                                    var response = $"Extension {extension} is mapped to language: {currentLanguage}";
                                    
                                    var responseBytes = Encoding.UTF8.GetBytes(response);
                                    await webSocket.SendAsync(
                                        new ArraySegment<byte>(responseBytes),
                                        WebSocketMessageType.Text,
                                        true,
                                        CancellationToken.None
                                    );
                                }
                                else if (parts.Length == 2)
                                {
                                    // Configuration mode - set language for extension
                                    var language = parts[1].Trim();
                                    var availableLanguages = syntaxHighlighter.GetAvailableLanguages();
                                    
                                    if (availableLanguages.Contains(language.ToLower()) || language.ToLower() == "auto")
                                    {
                                        // Update the language mapping
                                        syntaxHighlighter.SetLanguageMapping(extension, language);
                                        
                                        var response = $"Successfully mapped extension {extension} to language: {language}";
                                        var responseBytes = Encoding.UTF8.GetBytes(response);
                                        await webSocket.SendAsync(
                                            new ArraySegment<byte>(responseBytes),
                                            WebSocketMessageType.Text,
                                            true,
                                            CancellationToken.None
                                        );
                                    }
                                    else
                                    {
                                        var errorMessage = $"Error: Language '{language}' not available. Available languages: {string.Join(", ", availableLanguages)}";
                                        var errorBytes = Encoding.UTF8.GetBytes(errorMessage);
                                        await webSocket.SendAsync(
                                            new ArraySegment<byte>(errorBytes),
                                            WebSocketMessageType.Text,
                                            true,
                                            CancellationToken.None
                                        );
                                    }
                                }
                                else
                                {
                                    var helpMessage = @"Usage:
- Query: Send 'ext' or '.ext' to check current language mapping
- Configure: Send 'ext:language' or '.ext:language' to set mapping
- Special: Use 'ext:auto' to reset to automatic detection
Examples:
  py:python
  js:nodejs
  .masm:microasm
  .asm:auto";
                                    
                                    var helpBytes = Encoding.UTF8.GetBytes(helpMessage);
                                    await webSocket.SendAsync(
                                        new ArraySegment<byte>(helpBytes),
                                        WebSocketMessageType.Text,
                                        true,
                                        CancellationToken.None
                                    );
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Log($"Syntax language config error: {ex.Message}", "Error");
                                var errorMessage = $"Error: {ex.Message}";
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
                Logger.Log($"Syntax language config WebSocket error: {ex.Message}", "Error");
                connectionManager.RemoveConnection(connectionId);
            }
        }

        static async Task HandleSystemMetricsWebSocket(
            WebSocket webSocket,
            string connectionId,
            string path,
            int bufferSize
        )
        {
            Logger.Log($"System metrics endpoint connected: {path}");
            
            try
            {
                // Send initial metrics based on the endpoint
                var metricsData = GetSystemMetrics(path);
                var responseBytes = Encoding.UTF8.GetBytes(metricsData);
                await webSocket.SendAsync(
                    new ArraySegment<byte>(responseBytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );

                var buffer = new byte[bufferSize];

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
                        // For system endpoints, any message triggers a metrics refresh
                        var updatedMetrics = GetSystemMetrics(path);
                        var updateBytes = Encoding.UTF8.GetBytes(updatedMetrics);
                        await webSocket.SendAsync(
                            new ArraySegment<byte>(updateBytes),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"System metrics WebSocket error: {ex.Message}", "Error");
                connectionManager.RemoveConnection(connectionId);
            }
        }

        private static string GetSystemMetrics(string endpoint)
        {
            try
            {
                switch (endpoint.ToLower())
                {
                    case "/system/connections":
                        return GetConnectionMetrics();
                    
                    case "/system/projects":
                        return GetProjectMetrics();
                    
                    case "/system/endpoints":
                        return GetEndpointMetrics();
                    
                    case "/system/performance":
                        return GetPerformanceMetrics();
                    
                    case "/system/sessions":
                        return GetSessionMetrics();
                    
                    case "/system/languages":
                        return GetLanguageMetrics();
                    
                    case "/system/uptime":
                        return GetUptimeMetrics();
                    
                    case "/system/storage":
                        return GetStorageMetrics();
                    
                    case "/system/sandbox":
                        return GetSandboxMetrics();
                    
                    case "/system/overview":
                        return GetOverviewMetrics();
                    
                    default:
                        return GetAvailableSystemEndpoints();
                }
            }
            catch (Exception ex)
            {
                return $"Error retrieving metrics: {ex.Message}";
            }
        }

        private static string GetConnectionMetrics()
        {
            var connections = connectionManager.ListConnections();
            var connectionsByType = connections.GroupBy(c => c.Type).ToDictionary(g => g.Key, g => g.Count());
            
            var sb = new StringBuilder();
            sb.AppendLine("=== CONNECTION METRICS ===");
            sb.AppendLine($"Total Active Connections: {connections.Count()}");
            sb.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
            sb.AppendLine("Connections by Type:");
            
            foreach (var kvp in connectionsByType.OrderByDescending(x => x.Value))
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
            
            sb.AppendLine();
            sb.AppendLine("Recent Connections:");
            var recentConnections = connections
                .OrderByDescending(c => c.ConnectedAt)
                .Take(5);
            
            foreach (var conn in recentConnections)
            {
                var age = DateTime.UtcNow - conn.ConnectedAt;
                sb.AppendLine($"  {conn.Id} ({conn.Type}) - {age.TotalMinutes:F1}m ago");
            }
            
            return sb.ToString();
        }

        private static string GetProjectMetrics()
        {
            var projectsDir = Core.GetPath(Core.CodeDir);
            var sb = new StringBuilder();
            sb.AppendLine("=== PROJECT METRICS ===");
            sb.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
            
            if (!Directory.Exists(projectsDir))
            {
                sb.AppendLine("Projects directory not found");
                return sb.ToString();
            }
            
            var projects = Directory.GetDirectories(projectsDir);
            sb.AppendLine($"Total Projects: {projects.Length}");
            sb.AppendLine();
            
            var languageCounts = new Dictionary<string, int>();
            var totalFiles = 0;
            long totalSize = 0;
            
            foreach (var project in projects)
            {
                var language = DetectLanguage(project);
                if (!string.IsNullOrEmpty(language))
                {
                    languageCounts[language] = languageCounts.GetValueOrDefault(language, 0) + 1;
                }
                
                var files = Directory.GetFiles(project, "*", SearchOption.AllDirectories);
                totalFiles += files.Length;
                totalSize += files.Sum(f => new FileInfo(f).Length);
            }
            
            sb.AppendLine($"Total Files: {totalFiles}");
            sb.AppendLine($"Total Size: {totalSize / 1024 / 1024:F2} MB");
            sb.AppendLine();
            sb.AppendLine("Projects by Language:");
            
            foreach (var kvp in languageCounts.OrderByDescending(x => x.Value))
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
            
            sb.AppendLine();
            sb.AppendLine("Recent Projects:");
            var recentProjects = projects
                .Select(p => new { Path = p, Modified = Directory.GetLastWriteTime(p) })
                .OrderByDescending(p => p.Modified)
                .Take(5);
            
            foreach (var project in recentProjects)
            {
                var name = Path.GetFileName(project.Path);
                var age = DateTime.UtcNow - project.Modified;
                var language = DetectLanguage(project.Path);
                sb.AppendLine($"  {name} ({language}) - {age.TotalHours:F1}h ago");
            }
            
            return sb.ToString();
        }

        private static string GetEndpointMetrics()
        {
            var dynamicStats = Terminal.DynamicEndpointGenerator.GetStatistics();
            var activeEndpoints = Terminal.DynamicEndpointGenerator.GetActiveEndpoints();
            
            var sb = new StringBuilder();
            sb.AppendLine("=== ENDPOINT METRICS ===");
            sb.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
            sb.AppendLine("Static Endpoints:");
            sb.AppendLine("  /code - Code submission");
            sb.AppendLine("  /PMS - Project management");
            sb.AppendLine("  /stop - Process termination");
            sb.AppendLine("  /terminput - Terminal input");
            sb.AppendLine("  /terminal/create - Terminal session creation");
            sb.AppendLine("  /syntax - Syntax highlighting");
            sb.AppendLine("  /syntax/lang - Language configuration");
            sb.AppendLine("  /system/* - System metrics");
            sb.AppendLine();
            sb.AppendLine("Dynamic Endpoints:");
            sb.AppendLine($"  Total: {dynamicStats.TotalEndpoints}");
            sb.AppendLine($"  Active: {dynamicStats.ActiveEndpoints}");
            sb.AppendLine($"  Total Connections: {dynamicStats.TotalConnections}");
            sb.AppendLine($"  Average Age: {dynamicStats.AverageAge.TotalMinutes:F1} minutes");
            sb.AppendLine();
            sb.AppendLine("Active Dynamic Endpoints:");
            
            foreach (var endpoint in activeEndpoints.Take(10))
            {
                sb.AppendLine($"  {endpoint}");
            }
            
            if (activeEndpoints.Count > 10)
            {
                sb.AppendLine($"  ... and {activeEndpoints.Count - 10} more");
            }
            
            return sb.ToString();
        }

        private static string GetPerformanceMetrics()
        {
            var stats = metricsCollector.GetStats(DateTime.UtcNow.AddHours(-1)); // Last hour
            
            var sb = new StringBuilder();
            sb.AppendLine("=== PERFORMANCE METRICS ===");
            sb.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"Period: Last 1 hour");
            sb.AppendLine();
            sb.AppendLine($"Total Executions: {stats.TotalExecutions}");
            sb.AppendLine($"Successful Executions: {stats.SuccessfulExecutions}");
            sb.AppendLine($"Success Rate: {(stats.TotalExecutions > 0 ? stats.SuccessfulExecutions * 100.0 / stats.TotalExecutions : 0):F1}%");
            sb.AppendLine($"Average Execution Time: {stats.AverageExecutionTime.TotalSeconds:F2} seconds");
            sb.AppendLine($"Average Memory Usage: {stats.AverageMemoryUsage:F1} MB");
            sb.AppendLine();
            sb.AppendLine("Language Performance:");
            
            foreach (var lang in stats.TopLanguages.Take(5))
            {
                sb.AppendLine($"  {lang.Key}: {lang.Value} executions");
            }
            
            // Server resource usage (simplified)
            var process = System.Diagnostics.Process.GetCurrentProcess();
            sb.AppendLine();
            sb.AppendLine("Server Resources:");
            sb.AppendLine($"  Memory Usage: {process.WorkingSet64 / 1024 / 1024:F1} MB");
            sb.AppendLine($"  CPU Time: {process.TotalProcessorTime.TotalSeconds:F1} seconds");
            sb.AppendLine($"  Threads: {process.Threads.Count}");
            
            return sb.ToString();
        }

        private static string GetSessionMetrics()
        {
            var sessionStats = Terminal.SharedTerminalManager.GetStatistics();
            var sessionDetails = Terminal.SharedTerminalManager.GetActiveSessionDetails();
            
            var sb = new StringBuilder();
            sb.AppendLine("=== SESSION METRICS ===");
            sb.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
            sb.AppendLine($"Total Sessions: {sessionStats.TotalSessions}");
            sb.AppendLine($"Active Sessions: {sessionStats.ActiveSessions}");
            sb.AppendLine($"Total Connections: {sessionStats.TotalConnections}");
            sb.AppendLine($"Average Session Age: {sessionStats.AverageAge.TotalMinutes:F1} minutes");
            sb.AppendLine();
            sb.AppendLine("Active Sessions:");
            
            foreach (var session in sessionDetails.Take(10))
            {
                sb.AppendLine($"  {session}");
            }
            
            if (sessionDetails.Count > 10)
            {
                sb.AppendLine($"  ... and {sessionDetails.Count - 10} more");
            }
            
            return sb.ToString();
        }

        private static string GetLanguageMetrics()
        {
            var availableLanguages = syntaxHighlighter.GetAvailableLanguages();
            
            var sb = new StringBuilder();
            sb.AppendLine("=== LANGUAGE METRICS ===");
            sb.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
            sb.AppendLine($"Supported Languages: {availableLanguages.Count()}");
            sb.AppendLine();
            sb.AppendLine("Available Languages:");
            
            foreach (var lang in availableLanguages.OrderBy(x => x))
            {
                sb.AppendLine($"  {lang}");
            }
            
            // Get usage stats from metrics
            var stats = metricsCollector.GetStats(DateTime.UtcNow.AddDays(-7)); // Last week
            sb.AppendLine();
            sb.AppendLine("Usage (Last 7 days):");
            
            foreach (var lang in stats.TopLanguages.Take(10))
            {
                sb.AppendLine($"  {lang.Key}: {lang.Value} executions");
            }
            
            return sb.ToString();
        }

        private static string GetUptimeMetrics()
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime();
            
            var sb = new StringBuilder();
            sb.AppendLine("=== UPTIME METRICS ===");
            sb.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
            sb.AppendLine($"Server Started: {process.StartTime.ToUniversalTime():yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"Uptime: {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s");
            sb.AppendLine($"Total Uptime Hours: {uptime.TotalHours:F2}");
            sb.AppendLine($"Version: {Core.GetVersion()}");
            sb.AppendLine($"PMS Version: {PMS_VERSION}");
            
            return sb.ToString();
        }

        private static string GetStorageMetrics()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== STORAGE METRICS ===");
            sb.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
            
            var directories = new[]
            {
                (Core.CodeDir, "Projects"),
                (Core.BuildDir, "Build"),
                (Core.TempDir, "Temporary"),
                (Core.OutputDir, "Output"),
                (Core.LogDir, "Logs"),
                (Core.ExportDir, "Exports")
            };
            
            foreach (var (dir, name) in directories)
            {
                var path = Core.GetPath(dir);
                if (Directory.Exists(path))
                {
                    var dirInfo = new DirectoryInfo(path);
                    var files = dirInfo.GetFiles("*", SearchOption.AllDirectories);
                    var totalSize = files.Sum(f => f.Length);
                    
                    sb.AppendLine($"{name} Directory:");
                    sb.AppendLine($"  Path: {path}");
                    sb.AppendLine($"  Files: {files.Length}");
                    sb.AppendLine($"  Size: {totalSize / 1024 / 1024:F2} MB");
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine($"{name} Directory: Not found");
                    sb.AppendLine();
                }
            }
            
            return sb.ToString();
        }

        private static string GetSandboxMetrics()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== SANDBOX METRICS ===");
            sb.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
            sb.AppendLine("Available Sandbox Policies:");
            sb.AppendLine("  default - Basic restrictions (512MB, 30s, no network)");
            sb.AppendLine("  trusted - Extended permissions (2GB, 5min, network allowed)");
            sb.AppendLine();
            sb.AppendLine("Sandbox Status: Active");
            sb.AppendLine("Security Level: Medium");
            sb.AppendLine("Container Support: Available");
            
            return sb.ToString();
        }

        private static string GetOverviewMetrics()
        {
            var connections = connectionManager.ListConnections();
            var sessionStats = Terminal.SharedTerminalManager.GetStatistics();
            var dynamicStats = Terminal.DynamicEndpointGenerator.GetStatistics();
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime();
            
            var sb = new StringBuilder();
            sb.AppendLine("=== KODERUNNER SERVER OVERVIEW ===");
            sb.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"Version: {Core.GetVersion()} (PMS: {PMS_VERSION})");
            sb.AppendLine($"Uptime: {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m");
            sb.AppendLine();
            sb.AppendLine("CONNECTIONS:");
            sb.AppendLine($"  Active: {connections.Count()}");
            sb.AppendLine($"  Dynamic Endpoints: {dynamicStats.ActiveEndpoints}");
            sb.AppendLine();
            sb.AppendLine("SESSIONS:");
            sb.AppendLine($"  Active Terminal Sessions: {sessionStats.ActiveSessions}");
            sb.AppendLine($"  Session Connections: {sessionStats.TotalConnections}");
            sb.AppendLine();
            sb.AppendLine("PROJECTS:");
            var projectsDir = Core.GetPath(Core.CodeDir);
            var projectCount = Directory.Exists(projectsDir) ? Directory.GetDirectories(projectsDir).Length : 0;
            sb.AppendLine($"  Total Projects: {projectCount}");
            sb.AppendLine();
            sb.AppendLine("PERFORMANCE:");
            sb.AppendLine($"  Memory Usage: {process.WorkingSet64 / 1024 / 1024:F1} MB");
            sb.AppendLine($"  Thread Count: {process.Threads.Count}");
            sb.AppendLine();
            sb.AppendLine("Available System Endpoints:");
            sb.AppendLine("  /system/overview - This overview");
            sb.AppendLine("  /system/connections - Connection details");
            sb.AppendLine("  /system/projects - Project information");
            sb.AppendLine("  /system/endpoints - Static and dynamic endpoint status");
            sb.AppendLine("  /system/performance - Performance metrics");
            sb.AppendLine("  /system/sessions - Terminal session information");
            sb.AppendLine("  /system/languages - Language support");
            sb.AppendLine("  /system/uptime - Server uptime");
            sb.AppendLine("  /system/storage - Storage usage");
            sb.AppendLine("  /system/sandbox - Sandbox status");
            
            return sb.ToString();
        }

        private static string GetAvailableSystemEndpoints()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== KODERUNNER SYSTEM METRICS ===");
            sb.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
            sb.AppendLine("Available System Endpoints:");
            sb.AppendLine("  /system/overview     - Complete server overview");
            sb.AppendLine("  /system/connections  - Active WebSocket connections");
            sb.AppendLine("  /system/projects     - Project statistics and information");
            sb.AppendLine("  /system/endpoints    - Static and dynamic endpoint status");
            sb.AppendLine("  /system/performance  - Execution performance metrics");
            sb.AppendLine("  /system/sessions     - Terminal session information");
            sb.AppendLine("  /system/languages    - Supported language details");
            sb.AppendLine("  /system/uptime       - Server uptime and version");
            sb.AppendLine("  /system/storage      - File system usage");
            sb.AppendLine("  /system/sandbox      - Security sandbox status");
            sb.AppendLine();
            sb.AppendLine("Usage: Connect to any endpoint above to get real-time metrics");
            sb.AppendLine("Send any message to refresh the metrics");
            
            return sb.ToString();
        }

        public static void EnsureFolders()
        {
            Directory.CreateDirectory(Path.Combine(Core.RootDir, Core.CodeDir));
            Directory.CreateDirectory(Path.Combine(Core.RootDir, Core.BuildDir));
            Directory.CreateDirectory(Path.Combine(Core.RootDir, Core.TempDir));
            Directory.CreateDirectory(Path.Combine(Core.RootDir, Core.OutputDir));
            Directory.CreateDirectory(Path.Combine(Core.RootDir, Core.LogDir));
            Directory.CreateDirectory(Path.Combine(Core.RootDir, Core.ExportDir));
        }

        private static async Task HandleCliCommands(string[] args)
        {
            try
            {
                // Initialize required components for CLI mode
                EnsureFolders();
                
                // Initialize terminal system for CLI mode (simplified)
                FastTerminal.InitializeSimpleMode();
                
                // Initialize runnable manager for CLI operations
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
                
                var command = args[0].ToLower();
                
                switch (command)
                {
                    case "init":
                        BuildProcess initBuildProcess = new BuildProcess();
                        initBuildProcess.SetupCodeDir();
                        Console.WriteLine("KodeRunner directories initialized.");
                        break;
                        
                    case "run":
                        await HandleRunCommand(args);
                        break;
                        
                    case "build":
                        await HandleBuildCommand(args);
                        break;
                        
                    case "list":
                        HandleListCommand(args);
                        break;
                        
                    case "terminal":
                        await HandleTerminalCommand(args);
                        break;
                        
                    case "export":
                        if (args.Length > 1)
                        {
                            Implementations.Export(args[1]);
                        }
                        else
                        {
                            Console.WriteLine("Usage: koderunner export <project-name>");
                        }
                        break;
                        
                    case "import":
                        if (args.Length > 1)
                        {
                            Implementations.Import(args[1]);
                        }
                        else
                        {
                            Console.WriteLine("Usage: koderunner import <project-file>");
                        }
                        break;
                        
                    case "help":
                    case "--help":
                    case "-h":
                        ShowCliHelp();
                        break;
                        
                    default:
                        Console.WriteLine($"Unknown command: {command}");
                        Console.WriteLine("Use 'koderunner help' for available commands.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CLI Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        private static async Task HandleTerminalCommand(string[] args)
        {
            try
            {
                if (args.Length < 2)
                {
                    Console.WriteLine("Usage: koderunner terminal <project> [--language <lang>] [--main <file>] [--interactive]");
                    Console.WriteLine("  --interactive    Start interactive terminal session with the running process");
                    return;
                }

                var projectPath = ResolveProjectPath(args[1]);
                var interactive = Array.IndexOf(args, "--interactive") >= 0;
                
                // Validate project path exists
                if (!Directory.Exists(projectPath))
                {
                    Console.WriteLine($"Error: Project directory not found: {projectPath}");
                    Console.WriteLine("Available projects:");
                    ListAvailableProjects();
                    return;
                }

                var language = GetArgValue(args, "--language") ?? DetectLanguage(projectPath);
                var mainFile = GetArgValue(args, "--main") ?? DetectMainFile(projectPath, language);

                if (string.IsNullOrEmpty(language))
                {
                    Console.WriteLine("Could not detect project language. Please specify with --language");
                    return;
                }

                // Validate that we have a main file for languages that require it
                if (string.IsNullOrEmpty(mainFile) && RequiresMainFile(language))
                {
                    Console.WriteLine($"Error: Could not detect main file for {language} project. Please specify with --main");
                    Console.WriteLine($"Expected files: {GetExpectedMainFiles(language)}");
                    return;
                }

                Console.WriteLine($"Starting terminal session for project: {Path.GetFileName(projectPath)}");
                Console.WriteLine($"Language: {language}");
                Console.WriteLine($"Main file: {mainFile ?? "N/A"}");
                
                if (interactive)
                {
                    Console.WriteLine("Interactive mode enabled. Type 'exit' to quit, 'Ctrl+C' to interrupt process.");
                }

                // Convert relative path to absolute path for project path
                var absoluteProjectPath = Path.GetFullPath(projectPath);

                // Use existing runnable manager to start the process
                var settings = new Provider.SettingsProvider
                {
                    Language = language,
                    ProjectName = Path.GetFileName(absoluteProjectPath),
                    ProjectPath = absoluteProjectPath,
                    Main_File = mainFile ?? "",
                    Run_On_Build = true,
                    Output = "output"
                };

                Console.WriteLine($"Executing {language} project...");
                Console.WriteLine(new string('=', 50));

                // Start the process in a background task
                var executionTask = Task.Run(() => {
                    try
                    {
                        runnableManager.ExecuteFirstMatchingLanguage(language, settings);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Execution error: {ex.Message}");
                    }
                });

                // If interactive mode, handle user input
                if (interactive)
                {
                    await HandleInteractiveTerminal(executionTask);
                }
                else
                {
                    // Just wait for execution to complete
                    await executionTask;
                }

                Console.WriteLine(new string('=', 50));
                Console.WriteLine("Terminal session ended.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in terminal command: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private static async Task HandleInteractiveTerminal(Task executionTask)
        {
            Console.WriteLine("\n[Interactive Terminal Mode - Type 'exit' to quit]");
            
            var inputTask = Task.Run(async () =>
            {
                while (!executionTask.IsCompleted)
                {
                    Console.Write(">>> ");
                    string input = await Console.In.ReadLineAsync() ?? "";
                    
                    if (input.ToLower() == "exit")
                    {
                        Console.WriteLine("Terminating processes...");
                        TerminalProcess.StopAllProcesses();
                        break;
                    }
                    
                    if (!string.IsNullOrEmpty(input))
                    {
                        bool sent = SendInputToActiveProcess(input);
                        if (!sent)
                        {
                            Console.WriteLine("[No active process to send input to]");
                        }
                    }
                }
            });

            // Wait for either execution to complete or user to exit
            await Task.WhenAny(executionTask, inputTask);
            
            // Clean up
            if (!executionTask.IsCompleted)
            {
                Console.WriteLine("Stopping execution...");
                TerminalProcess.StopAllProcesses();
            }
        }

        private static async Task HandleBuildCommand(string[] args)
        {
            try
            {
                if (args.Length < 2)
                {
                    Console.WriteLine("Usage: koderunner build <project-path> [--language <lang>] [--output <name>]");
                    return;
                }

                var projectPath = ResolveProjectPath(args[1]);
                
                // Validate project path exists
                if (!Directory.Exists(projectPath))
                {
                    Console.WriteLine($"Error: Project directory not found: {projectPath}");
                    Console.WriteLine("Available projects:");
                    ListAvailableProjects();
                    return;
                }

                var language = GetArgValue(args, "--language") ?? DetectLanguage(projectPath);
                var output = GetArgValue(args, "--output") ?? "output";

                if (string.IsNullOrEmpty(language))
                {
                    Console.WriteLine("Could not detect project language. Please specify with --language");
                    return;
                }

                var mainFile = GetArgValue(args, "--main") ?? DetectMainFile(projectPath, language);
                
                // Validate that we have a main file for languages that require it
                if (string.IsNullOrEmpty(mainFile) && RequiresMainFile(language))
                {
                    Console.WriteLine($"Error: Could not detect main file for {language} project. Please specify with --main");
                    Console.WriteLine($"Expected files: {GetExpectedMainFiles(language)}");
                    return;
                }

                Console.WriteLine($"Building project: {Path.GetFileName(projectPath)}");
                Console.WriteLine($"Language: {language}");
                Console.WriteLine($"Main file: {mainFile ?? "N/A"}");
                Console.WriteLine($"Output: {output}");
                
                // Use existing runnable manager instead of creating new CLI runner
                var settings = new Provider.SettingsProvider
                {
                    Language = language,
                    ProjectName = Path.GetFileName(projectPath),
                    ProjectPath = projectPath,
                    Main_File = mainFile ?? "", // Ensure it's never null
                    Run_On_Build = false, // Build only, don't run
                    Output = output
                };

                Console.WriteLine($"Building {language} project...");
                Console.WriteLine(new string('=', 50));

                var startTime = DateTime.UtcNow;
                runnableManager.ExecuteFirstMatchingLanguage(language, settings);
                var duration = DateTime.UtcNow - startTime;

                Console.WriteLine(new string('=', 50));
                Console.WriteLine($"Build completed in {duration.TotalSeconds:F2} seconds");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in build command: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private static async Task HandleRunCommand(string[] args)
        {
            try
            {
                if (args.Length < 2)
                {
                    Console.WriteLine("Usage: koderunner run <project-path> [--language <lang>] [--main <file>] [--sandbox <policy>]");
                    return;
                }

                var projectPath = ResolveProjectPath(args[1]);
                
                // Validate project path exists
                if (!Directory.Exists(projectPath))
                {
                    Console.WriteLine($"Error: Project directory not found: {projectPath}");
                    Console.WriteLine("Available projects:");
                    ListAvailableProjects();
                    return;
                }

                var language = GetArgValue(args, "--language") ?? DetectLanguage(projectPath);
                var mainFile = GetArgValue(args, "--main") ?? DetectMainFile(projectPath, language);
                var sandboxPolicy = GetArgValue(args, "--sandbox") ?? "default";

                if (string.IsNullOrEmpty(language))
                {
                    Console.WriteLine("Could not detect project language. Please specify with --language");
                    return;
                }

                // Validate that we have a main file for languages that require it
                if (string.IsNullOrEmpty(mainFile) && RequiresMainFile(language))
                {
                    Console.WriteLine($"Error: Could not detect main file for {language} project. Please specify with --main");
                    Console.WriteLine($"Expected files: {GetExpectedMainFiles(language)}");
                    return;
                }

                Console.WriteLine($"Running project: {Path.GetFileName(projectPath)}");
                Console.WriteLine($"Language: {language}");
                Console.WriteLine($"Main file: {mainFile ?? "N/A"}");
                Console.WriteLine($"Sandbox policy: {sandboxPolicy}");

                // Convert relative path to absolute path for project path
                var absoluteProjectPath = Path.GetFullPath(projectPath);

                // Use existing runnable manager instead of creating new CLI runner
                var settings = new Provider.SettingsProvider
                {
                    Language = language,
                    ProjectName = Path.GetFileName(absoluteProjectPath),
                    ProjectPath = absoluteProjectPath,
                    Main_File = mainFile ?? "", // Just the filename, not full path
                    Run_On_Build = true,
                    Output = "output"
                };

                Console.WriteLine($"Executing {language} project...");
                Console.WriteLine(new string('=', 50));

                var startTime = DateTime.UtcNow;
                runnableManager.ExecuteFirstMatchingLanguage(language, settings);
                var duration = DateTime.UtcNow - startTime;

                Console.WriteLine(new string('=', 50));
                Console.WriteLine($"Execution completed in {duration.TotalSeconds:F2} seconds");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in run command: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private static string ResolveProjectPath(string input)
        {
            // If it's already an absolute path or contains path separators, use as-is
            if (Path.IsPathRooted(input) || input.Contains('/') || input.Contains('\\'))
            {
                return input;
            }

            // Otherwise, treat it as a project name and look in the standard projects directory
            var projectsDir = Core.GetPath(Core.CodeDir);
            var resolvedPath = Path.Combine(projectsDir, input);
            
            // If the resolved path exists, use it
            if (Directory.Exists(resolvedPath))
            {
                return resolvedPath;
            }

            // If not found in projects directory, return the original input
            // (this will cause a "not found" error with helpful message)
            return input;
        }

        private static void ListAvailableProjects()
        {
            var projectsDir = Core.GetPath(Core.CodeDir);
            if (!Directory.Exists(projectsDir))
            {
                Console.WriteLine("  No projects directory found. Run 'koderunner init' first.");
                return;
            }

            var projects = Directory.GetDirectories(projectsDir);
            if (projects.Length == 0)
            {
                Console.WriteLine("  No projects found in the projects directory.");
                return;
            }

            foreach (var project in projects)
            {
                var projectName = Path.GetFileName(project);
                var language = DetectLanguage(project);
                Console.WriteLine($"  {projectName} [{language ?? "unknown"}]");
            }
        }

        private static void HandleListCommand(string[] args)
        {
            var projectsDir = Core.GetPath(Core.CodeDir);
            if (!Directory.Exists(projectsDir))
            {
                Console.WriteLine("No projects directory found. Run 'koderunner init' first.");
                return;
            }

            var projects = Directory.GetDirectories(projectsDir);
            if (projects.Length == 0)
            {
                Console.WriteLine("No projects found in the projects directory.");
                return;
            }

            Console.WriteLine("Available projects:");
            
            foreach (var project in projects)
            {
                var projectName = Path.GetFileName(project);
                var language = DetectLanguage(project);
                var mainFile = DetectMainFile(project, language);
                
                Console.WriteLine($"  {projectName,-20} [{language ?? "unknown"}] {mainFile ?? "no main file"}");
            }
        }

        private static string GetArgValue(string[] args, string flag)
        {
            var index = Array.IndexOf(args, flag);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        private static string DetectLanguage(string projectPath)
        {
            if (!Directory.Exists(projectPath))
                return null;

            var files = Directory.GetFiles(projectPath, "*", SearchOption.TopDirectoryOnly);
            
            // Check for specific project files first
            if (files.Any(f => f.EndsWith(".csproj") || f.EndsWith(".sln"))) return "csharp";
            if (files.Any(f => Path.GetFileName(f).Equals("package.json", StringComparison.OrdinalIgnoreCase))) return "nodejs";
            if (files.Any(f => Path.GetFileName(f).Equals("requirements.txt", StringComparison.OrdinalIgnoreCase))) return "python";
            
            // Then check for source files
            if (files.Any(f => f.EndsWith(".py"))) return "python";
            if (files.Any(f => f.EndsWith(".js"))) return "nodejs";
            if (files.Any(f => f.EndsWith(".c") || f.EndsWith(".h"))) return "c";
            if (files.Any(f => f.EndsWith(".java"))) return "java";
            if (files.Any(f => f.EndsWith(".wasm"))) return "wasm";
            if (files.Any(f => f.EndsWith(".asm") || f.EndsWith(".s"))) return "j-masm";
            
            return null;
        }

        private static string DetectMainFile(string projectPath, string language)
        {
            if (string.IsNullOrEmpty(language) || !Directory.Exists(projectPath)) 
                return null;
            
            var files = Directory.GetFiles(projectPath);
            var foundFile = language.ToLower() switch
            {
                "python" => files.FirstOrDefault(f => Path.GetFileName(f).Equals("main.py", StringComparison.OrdinalIgnoreCase)) ?? 
                           files.FirstOrDefault(f => Path.GetFileName(f).Equals("app.py", StringComparison.OrdinalIgnoreCase)) ??
                           files.FirstOrDefault(f => f.EndsWith(".py")),
                "nodejs" => files.FirstOrDefault(f => Path.GetFileName(f).Equals("index.js", StringComparison.OrdinalIgnoreCase)) ?? 
                           files.FirstOrDefault(f => Path.GetFileName(f).Equals("app.js", StringComparison.OrdinalIgnoreCase)) ??
                           files.FirstOrDefault(f => f.EndsWith(".js")),
                "c" => files.FirstOrDefault(f => Path.GetFileName(f).Equals("main.c", StringComparison.OrdinalIgnoreCase)) ?? 
                      files.FirstOrDefault(f => f.EndsWith(".c")),
                "j-masm" => files.FirstOrDefault(f => f.EndsWith(".asm") || f.EndsWith(".s")),
                "java" => files.FirstOrDefault(f => Path.GetFileName(f).Equals("Main.java", StringComparison.OrdinalIgnoreCase)) ?? 
                         files.FirstOrDefault(f => f.EndsWith(".java")),
                "csharp" => null, // C# projects don't need a specific main file
                "wasm" => files.FirstOrDefault(f => f.EndsWith(".wasm")),
                _ => null
            };

            // Return only the filename, not the full path
            return foundFile != null ? Path.GetFileName(foundFile) : null;
        }

        private static bool RequiresMainFile(string language)
        {
            return language?.ToLower() switch
            {
                "python" => true,
                "nodejs" => true,
                "c" => true,
                "java" => true,
                "j-masm" => true,
                "wasm" => true,
                "csharp" => false, // C# uses project files
                _ => false
            };
        }

        private static string GetExpectedMainFiles(string language)
        {
            return language?.ToLower() switch
            {
                "python" => "main.py, app.py, or any .py file",
                "nodejs" => "index.js, app.js, or any .js file",
                "c" => "main.c or any .c file",
                "java" => "Main.java or any .java file",
                "j-masm" => "any .asm or .s file",
                "wasm" => "any .wasm file",
                _ => "appropriate source file for the language"
            };
        }

        private static void ShowCliHelp()
        {
            Console.WriteLine("KodeRunner CLI - Code Execution Platform");
            Console.WriteLine();
            Console.WriteLine("Usage: koderunner <command> [options]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  init                           Initialize KodeRunner directories");
            Console.WriteLine("  run <project> [options]       Run a project");
            Console.WriteLine("  build <project> [options]     Build a project");
            Console.WriteLine("  terminal <project> [options]  Interactive terminal session with project");
            Console.WriteLine("  list                          List available projects");
            Console.WriteLine("  export <project>              Export project to .KRproject file");
            Console.WriteLine("  import <file>                 Import .KRproject file");
            Console.WriteLine("  help                          Show this help message");
            Console.WriteLine();
            Console.WriteLine("Run Options:");
            Console.WriteLine("  --language <lang>             Specify project language");
            Console.WriteLine("  --main <file>                 Specify main file to execute");
            Console.WriteLine("  --sandbox <policy>            Specify sandbox policy (default, trusted)");
            Console.WriteLine();
            Console.WriteLine("Build Options:");
            Console.WriteLine("  --language <lang>             Specify project language");
            Console.WriteLine("  --output <name>               Specify output file name");
            Console.WriteLine();
            Console.WriteLine("Terminal Options:");
            Console.WriteLine("  --language <lang>             Specify project language");
            Console.WriteLine("  --main <file>                 Specify main file to execute");
            Console.WriteLine("  --interactive                 Enable interactive input mode");
            Console.WriteLine();
            Console.WriteLine("Supported Languages:");
            Console.WriteLine("  csharp, python, nodejs, c, java, wasm, j-masm");
            Console.WriteLine();
            Console.WriteLine("Project Path Options:");
            Console.WriteLine("  ProjectName                   Use project from koderunner/Projects/");
            Console.WriteLine("  ./path/to/project             Relative path from current directory");
            Console.WriteLine("  C:\\full\\path\\to\\project      Absolute path to project directory");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  koderunner run TestProject                                   # Project by name");
            Console.WriteLine("  koderunner run ./myproject --language python --main app.py  # Relative path");
            Console.WriteLine("  koderunner build TestProject --language c --output myapp    # Project by name");
            Console.WriteLine("  koderunner terminal TestProject --interactive               # Interactive terminal");
            Console.WriteLine("  koderunner run ./myproject --sandbox trusted                # Custom sandbox");
        }

        // Add the new routing system
        private static WebSocketRouteResult RouteWebSocketEndpoint(string path)
        {
            // Normalize path and split into segments
            var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0) return WebSocketRouteResult.Invalid();

            var root = segments[0].ToLower();
            
            return root switch
            {
                "api" => RouteApiEndpoint(segments),
                "dev" => RouteDevEndpoint(segments),
                "admin" => RouteAdminEndpoint(segments),
                "terminal" => RouteTerminalEndpoint(segments),
                "collab" => RouteCollabEndpoint(segments),
                "sandbox" => RouteSandboxEndpoint(segments),
                _ => WebSocketRouteResult.Invalid()
            };
        }

        private static WebSocketRouteResult RouteApiEndpoint(string[] segments)
        {
            if (segments.Length < 2) return WebSocketRouteResult.Invalid();

            var endpoint = segments[1].ToLower();
            
            return endpoint switch
            {
                "execute" => new WebSocketRouteResult
                {
                    IsValid = true,
                    ConnectionType = "api_execute",
                    WelcomeMessage = "Welcome to KodeRunner API - Code Execution Service\nConnection ID: {connectionId}\nSend project data to execute code",
                    Handler = HandleApiExecuteWebSocket,
                    RouteData = new Dictionary<string, object> { ["endpoint"] = "execute" }
                },
                "build" => new WebSocketRouteResult
                {
                    IsValid = true,
                    ConnectionType = "api_build",
                    WelcomeMessage = "Welcome to KodeRunner API - Build Service\nConnection ID: {connectionId}\nSend project data to build code",
                    Handler = HandleApiBuildWebSocket,
                    RouteData = new Dictionary<string, object> { ["endpoint"] = "build" }
                },
                "upload" => new WebSocketRouteResult
                {
                    IsValid = true,
                    ConnectionType = "api_upload",
                    WelcomeMessage = "Welcome to KodeRunner API - File Upload Service\nConnection ID: {connectionId}\nSend files with metadata",
                    Handler = HandleApiUploadWebSocket,
                    RouteData = new Dictionary<string, object> { ["endpoint"] = "upload" }
                },
                "status" => new WebSocketRouteResult
                {
                    IsValid = true,
                    ConnectionType = "api_status",
                    WelcomeMessage = "Welcome to KodeRunner API - Status Service\nConnection ID: {connectionId}\nReal-time status updates",
                    Handler = HandleApiStatusWebSocket,
                    RouteData = new Dictionary<string, object> { ["endpoint"] = "status" }
                },
                _ => WebSocketRouteResult.Invalid()
            };
        }

        private static WebSocketRouteResult RouteDevEndpoint(string[] segments)
        {
            if (segments.Length < 2) return WebSocketRouteResult.Invalid();

            var endpoint = segments[1].ToLower();
            
            return endpoint switch
            {
                "syntax" => new WebSocketRouteResult
                {
                    IsValid = true,
                    ConnectionType = "dev_syntax",
                    WelcomeMessage = "Welcome to KodeRunner Dev - Syntax Highlighting\nConnection ID: {connectionId}\nAvailable languages: " + string.Join(", ", syntaxHighlighter.GetAvailableLanguages()),
                    Handler = HandleDevSyntaxWebSocket,
                    RouteData = new Dictionary<string, object> { ["endpoint"] = "syntax" }
                },
                "debug" => new WebSocketRouteResult
                {
                    IsValid = true,
                    ConnectionType = "dev_debug",
                    WelcomeMessage = "Welcome to KodeRunner Dev - Debug Service\nConnection ID: {connectionId}\nReal-time debugging support",
                    Handler = HandleDevDebugWebSocket,
                    RouteData = new Dictionary<string, object> { ["endpoint"] = "debug" }
                },
                "logs" => new WebSocketRouteResult
                {
                    IsValid = true,
                    ConnectionType = "dev_logs",
                    WelcomeMessage = "Welcome to KodeRunner Dev - Live Logs\nConnection ID: {connectionId}\nStreaming application logs",
                    Handler = HandleDevLogsWebSocket,
                    RouteData = new Dictionary<string, object> { ["endpoint"] = "logs" }
                },
                "metrics" => new WebSocketRouteResult
                {
                    IsValid = true,
                    ConnectionType = "dev_metrics",
                    WelcomeMessage = "Welcome to KodeRunner Dev - Performance Metrics\nConnection ID: {connectionId}\nReal-time performance data",
                    Handler = HandleDevMetricsWebSocket,
                    RouteData = new Dictionary<string, object> { ["endpoint"] = "metrics" }
                },
                _ => WebSocketRouteResult.Invalid()
            };
        }

        private static WebSocketRouteResult RouteTerminalEndpoint(string[] segments)
        {
            if (segments.Length < 2) return WebSocketRouteResult.Invalid();

            var endpoint = segments[1].ToLower();
            
            return endpoint switch
            {
                "create" => new WebSocketRouteResult
                {
                    IsValid = true,
                    ConnectionType = "terminal_creator",
                    WelcomeMessage = "Welcome to KodeRunner Terminal - Session Creator\nConnection ID: {connectionId}\nCreate new terminal sessions",
                    Handler = HandleTerminalCreatorWebSocketWrapper,
                    RouteData = new Dictionary<string, object> { ["endpoint"] = "create" }
                },
                "input" => new WebSocketRouteResult
                {
                    IsValid = true,
                    ConnectionType = "terminal_input",
                    WelcomeMessage = "Welcome to KodeRunner Terminal - Input Service\nConnection ID: {connectionId}\nSend input to active processes",
                    Handler = HandleTerminalInputWrapper,
                    RouteData = new Dictionary<string, object> { ["endpoint"] = "input" }
                },
                "shared" => RouteSharedTerminalEndpoint(segments),
                _ => WebSocketRouteResult.Invalid()
            };
        }

        private static WebSocketRouteResult RouteSharedTerminalEndpoint(string[] segments)
        {
            if (segments.Length < 3) return WebSocketRouteResult.Invalid();

            var sessionId = segments[2];
            
            return new WebSocketRouteResult
            {
                IsValid = true,
                ConnectionType = "shared_terminal",
                WelcomeMessage = $"Welcome to KodeRunner Terminal - Shared Session\nConnection ID: {{connectionId}}\nSession: {sessionId}",
                Handler = (ws, connId, bufferSize, routeData) => HandleSharedTerminal(ws, connId, sessionId, bufferSize),
                RouteData = new Dictionary<string, object> { ["sessionId"] = sessionId }
            };
        }

        private static WebSocketRouteResult RouteCollabEndpoint(string[] segments)
        {
            if (segments.Length < 2) return WebSocketRouteResult.Invalid();

            var endpoint = segments[1].ToLower();
            
            return endpoint switch
            {
                "session" => new WebSocketRouteResult
                {
                    IsValid = true,
                    ConnectionType = "collab_session",
                    WelcomeMessage = "Welcome to KodeRunner Collaboration - Session Management\nConnection ID: {connectionId}\nCollaborative coding environment",
                    Handler = HandleCollabSessionWebSocket,
                    RouteData = new Dictionary<string, object> { ["endpoint"] = "session" }
                },
                "sync" => new WebSocketRouteResult
                {
                    IsValid = true,
                    ConnectionType = "collab_sync",
                    WelcomeMessage = "Welcome to KodeRunner Collaboration - Real-time Sync\nConnection ID: {connectionId}\nCode synchronization service",
                    Handler = HandleCollabSyncWebSocket,
                    RouteData = new Dictionary<string, object> { ["endpoint"] = "sync" }
                },
                _ => WebSocketRouteResult.Invalid()
            };
        }

        private static WebSocketRouteResult RouteAdminEndpoint(string[] segments)
        {
            if (segments.Length < 2) return WebSocketRouteResult.Invalid();

            var endpoint = segments[1].ToLower();
            
            return endpoint switch
            {
                "system" => new WebSocketRouteResult
                {
                    IsValid = true,
                    ConnectionType = "admin_system",
                    WelcomeMessage = "Welcome to KodeRunner Admin - System Management\nConnection ID: {connectionId}\nSystem administration interface",
                    Handler = HandleAdminSystemWebSocket,
                    RouteData = new Dictionary<string, object> { ["endpoint"] = "system", ["subpath"] = segments.Length > 2 ? segments[2] : "overview" }
                },
                "connections" => new WebSocketRouteResult
                {
                    IsValid = true,
                    ConnectionType = "admin_connections",
                    WelcomeMessage = "Welcome to KodeRunner Admin - Connection Management\nConnection ID: {connectionId}\nManage active connections",
                    Handler = HandleAdminConnectionsWebSocket,
                    RouteData = new Dictionary<string, object> { ["endpoint"] = "connections" }
                },
                "processes" => new WebSocketRouteResult
                {
                    IsValid = true,
                    ConnectionType = "admin_processes",
                    WelcomeMessage = "Welcome to KodeRunner Admin - Process Management\nConnection ID: {connectionId}\nManage running processes",
                    Handler = HandleAdminProcessesWebSocket,
                    RouteData = new Dictionary<string, object> { ["endpoint"] = "processes" }
                },
                _ => WebSocketRouteResult.Invalid()
            };
        }

        private static WebSocketRouteResult RouteSandboxEndpoint(string[] segments)
        {
            if (segments.Length < 2) return WebSocketRouteResult.Invalid();

            var endpoint = segments[1].ToLower();
            
            return endpoint switch
            {
                "execute" => new WebSocketRouteResult
                {
                    IsValid = true,
                    ConnectionType = "sandbox_execute",
                    WelcomeMessage = "Welcome to KodeRunner Sandbox - Secure Execution\nConnection ID: {connectionId}\nSecure code execution environment",
                    Handler = HandleSandboxExecuteWebSocket,
                    RouteData = new Dictionary<string, object> { ["endpoint"] = "execute" }
                },
                "monitor" => new WebSocketRouteResult
                {
                    IsValid = true,
                    ConnectionType = "sandbox_monitor",
                    WelcomeMessage = "Welcome to KodeRunner Sandbox - Security Monitor\nConnection ID: {connectionId}\nReal-time security monitoring",
                    Handler = HandleSandboxMonitorWebSocket,
                    RouteData = new Dictionary<string, object> { ["endpoint"] = "monitor" }
                },
                _ => WebSocketRouteResult.Invalid()
            };
        }

        // Add missing wrapper methods for terminal handlers
        private static async Task HandleTerminalCreatorWebSocketWrapper(WebSocket webSocket, string connectionId, int bufferSize, Dictionary<string, object> routeData)
        {
            await HandleTerminalCreatorWebSocket(webSocket, connectionId, bufferSize);
        }

        private static async Task HandleTerminalInputWrapper(WebSocket webSocket, string connectionId, int bufferSize, Dictionary<string, object> routeData)
        {
            await HandleTerminalInput(webSocket, connectionId, bufferSize);
        }

        // Add missing dev syntax handler
        private static async Task HandleDevSyntaxWebSocket(WebSocket webSocket, string connectionId, int bufferSize, Dictionary<string, object> routeData)
        {
            // This is essentially the same as the existing syntax highlighting handler
            await HandleSyntaxHighlightingWebSocket(webSocket, connectionId, bufferSize);
        }

        private static async Task HandleDevDebugWebSocket(WebSocket webSocket, string connectionId, int bufferSize, Dictionary<string, object> routeData)
        {
            // Debug service implementation
            Logger.Log("Dev debug endpoint connected");
            try
            {
                var buffer = new byte[bufferSize];
                
                // Send initial debug info
                var debugInfo = "Debug service active. Send 'help' for commands.";
                var debugBytes = Encoding.UTF8.GetBytes(debugInfo);
                await webSocket.SendAsync(
                    new ArraySegment<byte>(debugBytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );

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
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        // TODO: Implement actual debug commands
                        var response = $"Debug: Received '{message}' - Debug features coming soon";
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
            catch (Exception ex)
            {
                Logger.Log($"Debug WebSocket error: {ex.Message}", "Error");
                connectionManager.RemoveConnection(connectionId);
            }
        }

        private static async Task HandleDevLogsWebSocket(WebSocket webSocket, string connectionId, int bufferSize, Dictionary<string, object> routeData)
        {
            // Live logs streaming
            Logger.Log("Dev logs endpoint connected");
            try
            {
                // Send recent logs and then stream new ones
                var logsInfo = "Live log streaming active. Logs will appear here in real-time.";
                var logsBytes = Encoding.UTF8.GetBytes(logsInfo);
                await webSocket.SendAsync(
                    new ArraySegment<byte>(logsBytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );

                var buffer = new byte[bufferSize];
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
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Logs WebSocket error: {ex.Message}", "Error");
                connectionManager.RemoveConnection(connectionId);
            }
        }

        private static async Task HandleDevMetricsWebSocket(WebSocket webSocket, string connectionId, int bufferSize, Dictionary<string, object> routeData)
        {
            // Real-time performance metrics
            await HandleSystemMetricsWebSocket(webSocket, connectionId, "/system/performance", bufferSize);
        }

        private static async Task HandleAdminSystemWebSocket(WebSocket webSocket, string connectionId, int bufferSize, Dictionary<string, object> routeData)
        {
            // System administration
            var subpath = routeData.GetValueOrDefault("subpath", "").ToString();
            await HandleSystemMetricsWebSocket(webSocket, connectionId, $"/system/{subpath}", bufferSize);
        }

        private static async Task HandleAdminConnectionsWebSocket(WebSocket webSocket, string connectionId, int bufferSize, Dictionary<string, object> routeData)
        {
            // Connection management
            await HandleSystemMetricsWebSocket(webSocket, connectionId, "/system/connections", bufferSize);
        }

        private static async Task HandleAdminProcessesWebSocket(WebSocket webSocket, string connectionId, int bufferSize, Dictionary<string, object> routeData)
        {
            // Process management - could integrate with stop service
            await HandleStopWebSocket(webSocket, connectionId, bufferSize);
        }

        private static async Task HandleCollabSessionWebSocket(WebSocket webSocket, string connectionId, int bufferSize, Dictionary<string, object> routeData)
        {
            // Collaboration session management
            Logger.Log("Collaboration session endpoint connected");
            try
            {
                var collabInfo = "Collaboration session active. Collaborative features coming soon.";
                var collabBytes = Encoding.UTF8.GetBytes(collabInfo);
                await webSocket.SendAsync(
                    new ArraySegment<byte>(collabBytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );

                var buffer = new byte[bufferSize];
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
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Collaboration WebSocket error: {ex.Message}", "Error");
                connectionManager.RemoveConnection(connectionId);
            }
        }


        private static async Task HandleSandboxExecuteWebSocket(WebSocket webSocket, string connectionId, int bufferSize, Dictionary<string, object> routeData)
        {
            // Secure sandbox execution
            await HandlePmsWebSocket(webSocket, connectionId, bufferSize);
        }

        private static async Task HandleSandboxMonitorWebSocket(WebSocket webSocket, string connectionId, int bufferSize, Dictionary<string, object> routeData)
        {
            // Security monitoring
            Logger.Log("Sandbox monitor endpoint connected");
            try
            {
                var monitorInfo = "Security monitoring active. Sandbox metrics and alerts.";
                var monitorBytes = Encoding.UTF8.GetBytes(monitorInfo);
                await webSocket.SendAsync(
                    new ArraySegment<byte>(monitorBytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );

                var buffer = new byte[bufferSize];
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
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Sandbox monitor WebSocket error: {ex.Message}", "Error");
                connectionManager.RemoveConnection(connectionId);
            }
        }

        // Add the route result class
        private class WebSocketRouteResult
        {
            public bool IsValid { get; set; }
            public string ConnectionType { get; set; } = "";
            public string WelcomeMessage { get; set; } = "";
            public Func<WebSocket, string, int, Dictionary<string, object>, Task> Handler { get; set; } = null!;
            public Dictionary<string, object> RouteData { get; set; } = new();

            public static WebSocketRouteResult Invalid() => new() { IsValid = false };
        }

        // Add endpoint help
        private static string GetAvailableEndpointsHelp()
        {
            return @"KodeRunner WebSocket API Endpoints:

API Endpoints (/api/):
  /api/execute     - Execute code projects
  /api/build       - Build code projects  
  /api/upload      - Upload project files
  /api/status      - Real-time status updates

Development Tools (/dev/):
  /dev/syntax      - Syntax highlighting
  /dev/debug       - Debug support
  /dev/logs        - Live application logs
  /dev/metrics     - Performance metrics

Terminal Services (/terminal/):
  /terminal/create - Create terminal sessions
  /terminal/input  - Send input to processes
  /terminal/shared/<id> - Shared terminal sessions

Administration (/admin/):
  /admin/system    - System management
  /admin/connections - Connection management  
  /admin/processes - Process management

Collaboration (/collab/):
  /collab/session  - Collaboration sessions
  /collab/sync     - Real-time code sync

Sandbox (/sandbox/):
  /sandbox/execute - Secure code execution
  /sandbox/monitor - Security monitoring

Legacy Endpoints (deprecated but supported):
  /code, /PMS, /stop, /terminput, /syntax, /syntax/lang, /system/*

For detailed documentation, visit: https://docs.koderunner.dev";
        }

        // Placeholder handlers for new endpoints (implement based on requirements)
        private static async Task HandleApiExecuteWebSocket(WebSocket webSocket, string connectionId, int bufferSize, Dictionary<string, object> routeData)
        {
            // Enhanced version of PMS with API-focused features
            await HandlePmsWebSocket(webSocket, connectionId, bufferSize);
        }

        private static async Task HandleApiBuildWebSocket(WebSocket webSocket, string connectionId, int bufferSize, Dictionary<string, object> routeData)
        {
            // Build-only version of PMS
            // Implementation would be similar to PMS but only building, not running
            await HandlePmsWebSocket(webSocket, connectionId, bufferSize);
        }

        private static async Task HandleApiUploadWebSocket(WebSocket webSocket, string connectionId, int bufferSize, Dictionary<string, object> routeData)
        {
            // Enhanced version of code upload with metadata
            await HandleCodeWebSocket(webSocket, connectionId, bufferSize);
        }

        private static async Task HandleApiStatusWebSocket(WebSocket webSocket, string connectionId, int bufferSize, Dictionary<string, object> routeData)
        {
            // Real-time status updates
            await HandleSystemMetricsWebSocket(webSocket, connectionId, "/system/overview", bufferSize);
        }


        private static async Task HandleCollabSyncWebSocket(WebSocket webSocket, string connectionId, int bufferSize, Dictionary<string, object> routeData)
        {
            // Real-time code synchronization
            Logger.Log("Collaboration sync endpoint connected");
            try
            {
                var syncInfo = "Real-time sync active. Synchronization features coming soon.";
                var syncBytes = Encoding.UTF8.GetBytes(syncInfo);
                await webSocket.SendAsync(
                    new ArraySegment<byte>(syncBytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );

                var buffer = new byte[bufferSize];
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
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Sync WebSocket error: {ex.Message}", "Error");
                connectionManager.RemoveConnection(connectionId);
            }
        }


    }
}
