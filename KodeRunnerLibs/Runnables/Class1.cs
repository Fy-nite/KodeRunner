using System;
using System.IO;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using KodeRunner;
using Python.Runtime;



[Runnable("microasm", "j-masm", 0)]
public class MicroASMRunnable : IRunnable
{
    public string Name => "microasm";
    public string Language => "MASM";
    public int Priority => 0;
    public string description => "Executes MicroASM projects with jmasm interpreter";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running MicroASM project");

        var terminalProcess = new TerminalProcess();

        // Capture the PMS WebSocket from settings
        WebSocket pmsWebSocket = settings.PmsWebSocket;

        terminalProcess.OnOutput += async (output) =>
        {
            // Always output to console for CLI mode
            Console.Write(output);
            
            // Also send to WebSocket if available (WebSocket mode)
            if (pmsWebSocket != null && pmsWebSocket.State == WebSocketState.Open)
            {
                var bytes = Encoding.UTF8.GetBytes(output);
                // sleep for a few milis
                await Task.Delay(50); // Adjust delay as needed
                // Send the output to the PMS WebSocket
                await pmsWebSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
            }
        };

        var codePath = Path.Combine(Core.RootDir, Core.CodeDir, settings.ProjectName);
        var mainFilePath = Path.Combine(codePath, settings.Main_File);
        var runCommand = $"jmasm \"{mainFilePath}\"";
        Console.WriteLine(runCommand);

        terminalProcess.ExecuteCommand(runCommand).Wait();
    }
}

// dotnet runnable with metadata
[Runnable("dotnet", "csharp", 1)]
public class DotNetRunnable : IRunnable
{
    public string Name => "dotnet";
    public string Language => "csharp";
    public int Priority => 1;
    public string description => "Executes .NET projects with metadata";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running .NET project with metadata");
        Console.WriteLine($"Project Name: {settings.ProjectName}");
        Console.WriteLine($"Project Path: {settings.ProjectPath}");

        var terminalProcess = new TerminalProcess();

        // Capture the PMS WebSocket from settings
        WebSocket pmsWebSocket = settings.PmsWebSocket;

        terminalProcess.OnOutput += async (output) =>
        {
            // Always output to console for CLI mode
            Console.Write(output);
            
            // Also send to WebSocket if available (WebSocket mode)
            if (pmsWebSocket != null && pmsWebSocket.State == WebSocketState.Open)
            {
                var bytes = Encoding.UTF8.GetBytes(output);
                await pmsWebSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
            }
        };

        var codePath = Path.Combine(Core.RootDir, Core.CodeDir, settings.ProjectName);
        var mainFilePath = Path.Combine(codePath, settings.Main_File);
        string runCommand;
        if (mainFilePath.EndsWith(".csproj") || mainFilePath.EndsWith(".fsproj") || mainFilePath.EndsWith(".vbproj"))
        {
            runCommand = $"dotnet run --project \"{mainFilePath}\"";
        }
        else
        {
            // Look for a project file in the directory
            var projectFile = Directory.GetFiles(codePath, "*.csproj").FirstOrDefault() 
                           ?? Directory.GetFiles(codePath, "*.fsproj").FirstOrDefault()
                           ?? Directory.GetFiles(codePath, "*.vbproj").FirstOrDefault();
                           
            if (projectFile != null)
            {
                runCommand = $"dotnet run --project \"{projectFile}\"";
            }
            else
            {
                throw new FileNotFoundException("No .NET project file found in the directory.");
            }
        }

        Console.WriteLine(runCommand);

        terminalProcess.ExecuteCommand(runCommand).Wait();
    }
}


//lua runnable with metadata
[Runnable("lua", "lua", 0)]
public class LuaRunnable : IRunnable
{
    public string Name => "lua";
    public string Language => "lua";
    public int Priority => 0;
    public string description => "Executes lua projects with metadata";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running lua project with metadata");
        Console.WriteLine($"Project Name: {settings.ProjectName}");
        Console.WriteLine($"Project Path: {settings.ProjectPath}");

        var terminalProcess = new TerminalProcess();

        // Capture the PMS WebSocket from settings
        WebSocket pmsWebSocket = settings.PmsWebSocket;

        terminalProcess.OnOutput += async (output) =>
        {
            // Always output to console for CLI mode
            Console.Write(output);

            // Also send to WebSocket if available (WebSocket mode)
            if (pmsWebSocket != null && pmsWebSocket.State == WebSocketState.Open)
            {
                var bytes = Encoding.UTF8.GetBytes(output);
                await pmsWebSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
            }
        };

        var codePath = Path.Combine(Core.RootDir, Core.CodeDir, settings.ProjectName);
        var mainFilePath = Path.Combine(codePath, settings.Main_File);
        var runCommand = $"lua \"{mainFilePath}\"";
        Console.WriteLine(runCommand);

        terminalProcess.ExecuteCommand(runCommand).Wait();
    }
}

// rust runnable with metadata
[Runnable("rust", "rust", 0)]
public class RustRunnable : IRunnable
{
    public string Name => "rust";
    public string Language => "rust";
    public int Priority => 0;
    public string description => "Executes rust projects with metadata";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running rust project with metadata");
        Console.WriteLine($"Project Name: {settings.ProjectName}");
        Console.WriteLine($"Project Path: {settings.ProjectPath}");

        var terminalProcess = new TerminalProcess();

        // Capture the PMS WebSocket from settings
        WebSocket pmsWebSocket = settings.PmsWebSocket;

        terminalProcess.OnOutput += async (output) =>
        {
            // Always output to console for CLI mode
            Console.Write(output);
            
            // Also send to WebSocket if available (WebSocket mode)
            if (pmsWebSocket != null && pmsWebSocket.State == WebSocketState.Open)
            {
                var bytes = Encoding.UTF8.GetBytes(output);
                await pmsWebSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
            }
        };

        var codePath = Path.Combine(Core.RootDir, Core.CodeDir, settings.ProjectName);
        var mainFilePath = Path.Combine(codePath, settings.Main_File);
    string runCommand;
    if (mainFilePath.EndsWith("Cargo.toml"))
    {
        // If the main file is Cargo.toml, run from its directory
        var cargoDir = Path.GetDirectoryName(mainFilePath);
        runCommand = $"cd \"{cargoDir}\" ; cargo run";
    }
    else
    {
        // Look for Cargo.toml in the project directory
        var cargoFile = Path.Combine(codePath, "Cargo.toml");
        
        if (File.Exists(cargoFile))
        {
            // Run using cargo if Cargo.toml exists
            runCommand = $"cd \"{codePath}\" ; cargo run";
        }
        else
        {
            // Fallback to direct rustc compilation if no Cargo.toml
            var outputName = Path.GetFileNameWithoutExtension(mainFilePath);
            var outputPath = Path.Combine(codePath, outputName);
            runCommand = $"rustc \"{mainFilePath}\" -o \"{outputPath}\" ; \"{outputPath}\"";
        }
    }
        
        Console.WriteLine(runCommand);

        terminalProcess.ExecuteCommand(runCommand).Wait();
    }
}

// java runnable with metadata
[Runnable("java", "java", 0)]
public class ModifiedJavaRunnable : IRunnable
{
    public string Name => "java";
    public string Language => "java";
    public int Priority => 0;
    public string description => "Executes java projects with metadata";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running java project with metadata");
        Console.WriteLine($"Project Name: {settings.ProjectName}");
        Console.WriteLine($"Project Path: {settings.ProjectPath}");

        var terminalProcess = new TerminalProcess();

        // Capture the PMS WebSocket from settings
        WebSocket pmsWebSocket = settings.PmsWebSocket;

        terminalProcess.OnOutput += async (output) =>
        {
            // Always output to console for CLI mode
            Console.Write(output);
            
            // Also send to WebSocket if available (WebSocket mode)
            if (pmsWebSocket != null && pmsWebSocket.State == WebSocketState.Open)
            {
                var bytes = Encoding.UTF8.GetBytes(output);
                await pmsWebSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
            }
        };

        var codePath = Path.Combine(Core.RootDir, Core.CodeDir, settings.ProjectName);
        var mainFilePath = Path.Combine(codePath, settings.Main_File);
        var runCommand = $"java {mainFilePath}";

        Console.WriteLine(runCommand);

        terminalProcess.ExecuteCommand(runCommand).Wait();
    }
}

// python runnable with metadata
[Runnable("python", "python", 0)]
public class ModifiedPythonRunnable : IRunnable
{
    public string Name => "python";
    public string Language => "python";
    public int Priority => 0;
    public string description => "Executes python projects with metadata";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running python with metadata");
        Console.WriteLine($"Project Name: {settings.ProjectName}");
        Console.WriteLine($"Project Path: {settings.ProjectPath}");

        var terminalProcess = new TerminalProcess();

        // Capture the PMS WebSocket from settings
        WebSocket pmsWebSocket = settings.PmsWebSocket;

        terminalProcess.OnOutput += async (output) =>
        {
            // Always output to console for CLI mode
            Console.Write(output);
            
            // Also send to WebSocket if available (WebSocket mode)
            if (pmsWebSocket != null && pmsWebSocket.State == WebSocketState.Open)
            {
                var bytes = Encoding.UTF8.GetBytes(output);
                await pmsWebSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
            }
        };

        var codePath = Path.Combine(Core.RootDir, Core.CodeDir, settings.ProjectName);
        var mainFilePath = Path.Combine(codePath, settings.Main_File);
        var runCommand =
            Environment.OSVersion.Platform == PlatformID.Win32NT
                ? $"py \"{mainFilePath}\""
                : $"python3 \"{mainFilePath}\"";
        Console.WriteLine(runCommand);

        terminalProcess.ExecuteCommand(runCommand).Wait();
    }
}

// nodejs runnable

[Runnable("nodejs", "nodejs", 0)]
public class NodeJsRunnable : IRunnable
{
    public string Name => "nodejs";
    public string Language => "nodejs";
    public int Priority => 0;
    public string description => "Executes NodeJS projects";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running NodeJS project");

        var terminalProcess = new TerminalProcess();

        // Capture the PMS WebSocket from settings
        WebSocket pmsWebSocket = settings.PmsWebSocket;

        terminalProcess.OnOutput += async (output) =>
        {
            // Always output to console for CLI mode
            Console.Write(output);
            
            // Also send to WebSocket if available (WebSocket mode)
            if (pmsWebSocket != null && pmsWebSocket.State == WebSocketState.Open)
            {
                var bytes = Encoding.UTF8.GetBytes(output);
                await pmsWebSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
            }
        };

        var codePath = Path.Combine(Core.RootDir, Core.CodeDir, settings.ProjectName);
        var mainFilePath = Path.Combine(codePath, settings.Main_File);
        var runCommand = $"node \"{mainFilePath}\"";
        Console.WriteLine(runCommand);
        // check if the project has a package.json file
        var packageJsonPath = Path.Combine(codePath, "package.json");

        if (settings.RunArgs != null)
        {
            // we want to install a package with said name through npm
            var installCommand = $"npm install {settings.RunArgs}";
            terminalProcess.ExecuteCommand(installCommand).Wait();
        }
        terminalProcess.ExecuteCommand(runCommand).Wait();
    }
}
[Runnable("c", "gcc", 0)]
public class CRunnable : IRunnable
{
    public string Name => "c";
    public string Language => "c";
    public int Priority => 0; 
    public string description => "Executes C projects";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running C project");

        var terminalProcess = new TerminalProcess();

        WebSocket pmsWebSocket = settings.PmsWebSocket;

        terminalProcess.OnOutput += async (output) =>
        {
            // Always output to console for CLI mode
            Console.Write(output);
            
            // Also send to WebSocket if available (WebSocket mode)
            if (pmsWebSocket != null && pmsWebSocket.State == WebSocketState.Open)
            {
                var bytes = Encoding.UTF8.GetBytes(output);
                await pmsWebSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
            }
        };

        var codePath = Path.Combine(Core.RootDir, Core.CodeDir, settings.ProjectName);
        var outputFilePath = Path.Combine(codePath, settings.Output);
        var mainFilePath = Path.Combine(codePath, settings.Main_File);

        terminalProcess.ExecuteCommand($"echo '\u001b[32m<color=green>Building C project...\u001b[0m'").Wait();
        terminalProcess.ExecuteCommand($"gcc -o \"{outputFilePath}\" \"{mainFilePath}\"").Wait();

        if (settings.Run_On_Build)
        {
            terminalProcess.ExecuteCommand($"echo '\u001b[32mRunning program...\u001b[0m'").Wait();
            terminalProcess.ExecuteCommand($"\"{outputFilePath}\"").Wait();
        }
    }
}
