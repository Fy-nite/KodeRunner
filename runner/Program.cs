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
                        default:
                            Logger.Log($"Invalid endpoint: {path}", "Warning");
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
                Terminal.Terminal.advancedterm = false; // Disable advanced terminal features in CLI
                
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
    }
}
