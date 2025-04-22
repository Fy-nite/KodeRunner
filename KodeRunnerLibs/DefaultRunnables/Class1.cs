using System;
using System.IO;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using KodeRunner;
using Python.Runtime;


[Runnable("dotnet", "csharp", 1)]
public class ModifiedDotnetRunnable : IRunnable
{
    public string Name => "dotnet";
    public string Language => "csharp";
    public int Priority => 1;
    public string description => "Executes dotnet projects with metadata";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine($"Running dotnet project: {settings.ProjectName}");
        Console.WriteLine($"Project Path: {settings.ProjectPath}");

        var terminalProcess = new TerminalProcess();

        // Capture the PMS WebSocket from settings
        WebSocket pmsWebSocket = settings.PmsWebSocket;

        terminalProcess.OnOutput += async (output) =>
        {
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

        var buildCommand = $"dotnet build \"{settings.ProjectPath}\"";
        var runCommand = $"dotnet run --project \"{settings.ProjectPath}\"";

        terminalProcess.ExecuteCommand(buildCommand).Wait();

        if (settings.Run_On_Build)
        {
            terminalProcess.ExecuteCommand(runCommand).Wait();
        }
    }
}

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
