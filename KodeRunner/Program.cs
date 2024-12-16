using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using KodeRunner;
using System.Reflection.Metadata;

namespace KodeRunner
{
    class Program
    {
        static Dictionary<string, WebSocket> activeConnections = new Dictionary<string, WebSocket>();
        static TerminalProcess terminalProcess = new TerminalProcess();
        
        public static List<string> CommentRegexes = new List<string>
        {
            @"^#.*$",
            @"^//.*$",
            @"^/\*.*\*/$",
            @"^<!--.*-->$",
            @"^\"".*\""$",
            @"^//.*$",
            @"^<!--.*-->$",
            @"^/\*.*\*/$",
            @"^;.*$",
            @"^--",
            @"^#.*$",
            @"^//.*$",
            @"^/\*.*\*/$",
            @"^<!--.*-->$",
            @"^\"".*\""$",
            @"^//.*$",
            @"^<!--.*-->$",
            // start of block comment
            @"^/\*.*$",
            // end of block comment
            @".*\*/$"
        };

        public static string PMS_VERSION = "1.2.2";

        // Add the RunnableManager as a static field
        static RunnableManager runnableManager = new RunnableManager();

        /// <summary>
        /// Main entry point for the application.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        static async Task Main(string[] args)
        {
            var server = new HttpListener();
            server.Prefixes.Add("http://localhost:5000/");

            Provider.SettingsProvider settings = new Provider.SettingsProvider();
            // Remove the local declaration of runnableManager
            // RunnableManager runnableManager = new RunnableManager();
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

            Console.WriteLine($"KodeRunner v{Core.GetVersion()} started");  
            Console.WriteLine($"PMS v{PMS_VERSION} started");

            BuildProcess buildProcess = new BuildProcess();
            buildProcess.SetupCodeDir();

            Console.WriteLine("WebSocket server started at ws://localhost:8000/");
            
            while (true)
            {
                var context = await server.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    var path = context.Request.Url.AbsolutePath;
                    var wsContext = await context.AcceptWebSocketAsync(null);

                    switch (path)
                    {
                        case "/code":
                            _ = HandleCodeWebSocket(wsContext.WebSocket);
                            break;
                        case "/PMS":
                            _ = HandlePmsWebSocket(wsContext.WebSocket);
                            break;
                        case "/stop":
                            _ = HandleStopWebSocket(wsContext.WebSocket);
                            break;
                        case "/terminput":
                            // handle terminal input
                            _ = HandleTerminalInput(wsContext.WebSocket);
                            break;
                        default:
                            Console.WriteLine($"Invalid endpoint: {path}");
                            break;
                    }
                }
            }
        }

        static async Task HandleTerminalInput(WebSocket webSocket)
        {
            Console.WriteLine("Terminal input endpoint connected");
            try
            {
                activeConnections["terminput"] = webSocket;
                var buffer = new byte[8192];

                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                        activeConnections.Remove("terminput");
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            await memoryStream.WriteAsync(buffer, 0, result.Count);
                            memoryStream.Seek(0, SeekOrigin.Begin);
                            var message = await ReadFromMemoryStream(memoryStream);
                            Console.WriteLine($"Received message: {message}");
                            Console.WriteLine(terminalProcess.SendInput(message));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebSocket error: {ex.Message}");
                activeConnections.Remove("terminput");
                runnableManager.LoadRunnables();
            }
        }
        static async Task HandleCodeWebSocket(WebSocket webSocket)
        {
            Console.WriteLine("Code endpoint connected");
            var projectnamefound = false;
            var filenamefound = false;
            try
            {
                activeConnections["code"] = webSocket;
                var buffer = new byte[8192];

                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                        activeConnections.Remove("code");
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            await memoryStream.WriteAsync(buffer, 0, result.Count);
                            memoryStream.Seek(0, SeekOrigin.Begin);
                            var message = await ReadFromMemoryStream(memoryStream);
                            var lines = message.Split('\n');
                            if (lines.Length == 0) continue;
                            //Console.WriteLine($"Received message: {message}");
                            var commentLines = lines.Where(line => CommentRegexes.Any(regex => Regex.IsMatch(line, regex))).ToList();
                            var commentContent = new StringBuilder();
                            bool inBlockComment = false;

                            foreach (var line in lines)
                            {
                                if (Regex.IsMatch(line, @"^/\*.*\*/$")) // Single line block comment
                                {
                                    commentContent.AppendLine(line);
                                }
                                else if (Regex.IsMatch(line, @"^/\*.*$")) // Start of block comment
                                {
                                    commentContent.AppendLine(line);
                                    inBlockComment = true;
                                }
                                else if (Regex.IsMatch(line, @".*\*/$")) // End of block comment
                                {
                                    commentContent.AppendLine(line);
                                    inBlockComment = false;
                                }
                                else if (inBlockComment || CommentRegexes.Any(regex => Regex.IsMatch(line, regex)))
                                {
                                    commentContent.AppendLine(line);
                                }
                                else
                                {
                                    commentContent.AppendLine(line);
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
                                Console.WriteLine($"Project name: {projectNameMatch.Groups[1].Value}");
                                projectnamefound = true;
                            }
                            if (projectnamefound && filenamefound)
                            {
                                // Trim project and file names to remove any extra whitespace
                                string projectName = projectNameMatch.Groups[1].Value.Trim();
                                string fileName = fileNameMatch.Groups[1].Value.Trim();

                                // Use Path.Combine to construct paths
                                string project_path = Path.Combine(Core.RootDir, Core.CodeDir, projectName);
                                string file_path = Path.Combine(project_path, fileName);

                                if (!Directory.Exists(project_path))
                                {
                                    Directory.CreateDirectory(project_path);
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
                runnableManager.LoadRunnables();
                activeConnections.Remove("code");
            }
        }


        public static async Task<string> ReadFromMemoryStream(MemoryStream memoryStream)
        {
            memoryStream.Seek(0, SeekOrigin.Begin);
            using (var reader = new StreamReader(memoryStream, Encoding.UTF8))
            {
                return await reader.ReadToEndAsync();
            }
        }

        public static async Task SendToWebSocket(string endpoint, string message)
        {
            if (activeConnections.TryGetValue(endpoint, out WebSocket socket) && socket.State == WebSocketState.Open)
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        static async Task HandlePmsWebSocket(WebSocket webSocket)
        {
            Console.WriteLine("PMS endpoint connected");
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
                activeConnections["PMS"] = webSocket;
                var buffer = new byte[8192];

                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                        activeConnections.Remove("PMS");
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            await memoryStream.WriteAsync(buffer, 0, result.Count);
                            memoryStream.Seek(0, SeekOrigin.Begin);
                            var message = await ReadFromMemoryStream(memoryStream);
                            Console.WriteLine($"Received message: {message}");
                            // we now have the json message in the message variable
                            // we can now parse it into a dictionary
                            var messageDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(message);
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
                            if (messageDict.TryGetValue("Project_Build_Systems", out string buildsystems))
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
                            
                            // Ensure parent directories exist
                            string project_path = Path.Combine(Core.RootDir, Core.CodeDir, ProjectName);
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
                            settings.ProjectPath = Path.Combine(Core.RootDir, Core.CodeDir, ProjectName);
                            // Use the existing runnableManager instance
                            runnableManager.ExecuteFirstMatchingLanguage(Project_Build_Systems, settings);
                            runnableManager.print();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebSocket error: {ex.Message}");
                activeConnections.Remove("PMS");
                runnableManager.LoadRunnables();
            }

        }

        static async Task HandleStopWebSocket(WebSocket webSocket)
        {
            Console.WriteLine("Stop endpoint connected");
            try
            {
                activeConnections["stop"] = webSocket;
                var buffer = new byte[1024];

                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                        activeConnections.Remove("stop");
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        await using (var memoryStream = new MemoryStream())
                        {
                            await memoryStream.WriteAsync(buffer.AsMemory(0, result.Count), CancellationToken.None);
                            var message = await ReadFromMemoryStream(memoryStream);
                            var messageDict = JsonConvert.DeserializeObject<Dictionary<string, bool>>(message);

                            if (messageDict != null && messageDict.TryGetValue("stopped", out bool shouldStop) && shouldStop)
                            {
                                Console.WriteLine("Stopping all processes...");
                                TerminalProcess.StopAllProcesses();
                                
                                // Send confirmation back to client
                                var response = JsonConvert.SerializeObject(new { stopped = true, message = "All processes stopped" });
                                var responseBytes = Encoding.UTF8.GetBytes(response);
                                await webSocket.SendAsync(new ArraySegment<byte>(responseBytes), WebSocketMessageType.Text, true, CancellationToken.None);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebSocket error: {ex.Message}");
                runnableManager.LoadRunnables();
                activeConnections.Remove("stop");
            }
        }
    }
}
