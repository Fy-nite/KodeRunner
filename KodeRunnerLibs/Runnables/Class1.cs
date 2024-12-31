using System;
using System.IO;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using KodeRunner;
using Python.Runtime;
using Finite.MicroASM;
// MicroASM runnable. This is a custom language that is not supported by KodeRunner out of the box.
// setup a runnable that uses pythonnet to interface with interp.py

[Runnable("microasm", "MASM", 0)]
public class MicroASMRunnable : IRunnable
{
    public string Name => "microasm";
    public string Language => "MASM";
    public int Priority => 0;
    public string description => "Executes MicroASM projects";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running MicroASM project");

        var terminalProcess = new TerminalProcess();

        // Capture the PMS WebSocket from settings
        WebSocket pmsWebSocket = settings.PmsWebSocket;

        // read the MASM file
        var codePath = Path.Combine(Core.RootDir, Core.CodeDir, settings.ProjectName);
        var mainFilePath = Path.Combine(codePath, settings.Main_File);

       // read the 
    }
}

[Runnable("dotnet", "csharp", 1)]
public class ModifiedDotnetRunnable : IRunnable
{
    public string Name => "dotnet";
    public string Language => "csharp";
    public int Priority => 1;
    public string description => "Executes C# projects";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running C# project");

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
        var outputFilePath = Path.Combine(codePath, settings.Output);
        var mainFilePath = Path.Combine(codePath, settings.Main_File);

        // Add some color to the output
        var buildCommand =
            $"echo '\u001b[32m<color=green>Building C# project...\u001b[0m' && "
            + $"dotnet build \"{codePath}\"";

        terminalProcess.ExecuteCommand(buildCommand).Wait();

        if (settings.Run_On_Build)
        {
            var runCommand =
                $"echo '\u001b[32mRunning program...\u001b[0m' && " + $"dotnet run \"{codePath}\"";
            terminalProcess.ExecuteCommand(runCommand).Wait();
        }
    }
}

// python
[Runnable("python", "python", 0)]
public class PythonRunnable : IRunnable
{
    public string Name => "python";
    public string Language => "python";
    public int Priority => 0;
    public string description => "Executes python projects";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running python project");

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
        var runCommand = $"python3 \"{mainFilePath}\"";

        terminalProcess.ExecuteCommand(runCommand).Wait();
    }
}

// nodejs runnable


[Runnable("nodejs", "nodejs", 0)]
public class node : IRunnable
{
    public string Name => "node";
    public string Language => "nodejs";
    public int Priority => 0;
    public string description => "Executes nodejs projects";

    public void Execute(Provider.ISettingsProvider settings)
    {
       // Console.WriteLine("Running nodejs project");

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
        var outputFilePath = Path.Combine(codePath, settings.Output);
        var mainFilePath = Path.Combine(codePath, settings.Main_File);

        // Add some color to the output
        var buildCommand =
            $"echo '\u001b[32m<color=green> running nodejs project...\u001b[0m' && "
            + $"node \"{mainFilePath}\"";

        terminalProcess.ExecuteCommand(buildCommand).Wait();

  
    }
}


// C runnable
[Runnable("c", "clang", 0)]
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
        var outputFilePath = Path.Combine(codePath, settings.Output);
        var mainFilePath = Path.Combine(codePath, settings.Main_File);

        // Add some color to the output
        var buildCommand =
            $"echo '\u001b[32m<color=green>Building C project...\u001b[0m' && "
            + $"clang -o \"{outputFilePath}\" \"{mainFilePath}\"";

        terminalProcess.ExecuteCommand(buildCommand).Wait();

        if (settings.Run_On_Build)
        {
            var runCommand =
                $"echo '\u001b[32mRunning program...\u001b[0m' && " + $"\"{outputFilePath}\"";
            terminalProcess.ExecuteCommand(runCommand).Wait();
        }
    }
}
