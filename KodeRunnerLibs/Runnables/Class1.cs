using System;
using System.IO;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using KodeRunner;


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
        var runCommand = $"masm ==run \"{mainFilePath}\"";
        Console.WriteLine(runCommand);

        terminalProcess.ExecuteCommand(runCommand).Wait();
    }
}

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

[Runnable("cpp", "g++", 0)]
public class CppRunnable : IRunnable
{
    public string Name => "cpp";
    public string Language => "cpp";
    public int Priority => 0;
    public string description => "Executes C++ projects";
    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running C++ project");

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

        terminalProcess.ExecuteCommand($"echo '\u001b[32m<color=green>Building C++ project...\u001b[0m'").Wait();
        terminalProcess.ExecuteCommand($"g++ -o \"{outputFilePath}\" \"{mainFilePath}\"").Wait();

        if (settings.Run_On_Build)
        {
            terminalProcess.ExecuteCommand($"echo '\u001b[32mRunning program...\u001b[0m'").Wait();
            terminalProcess.ExecuteCommand($"\"{outputFilePath}\"").Wait();
        }
    }
}

// Contract runnable. ccl is the compiler for the Contract language and takes a .ct file directly.

[Runnable("contract", "ccl", 0)]
public class ContractRunnable : IRunnable
{
    public string Name => "contract";
    public string Language => "ccl";
    public int Priority => 0;
    public string description => "Executes Contract (.ct) projects using the ccl compiler";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running contract project");

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
        var mainFilePath = Path.Combine(codePath, settings.Main_File);
        var runCommand = $"ccl \"{mainFilePath}\"";
        Console.WriteLine(runCommand);

        terminalProcess.ExecuteCommand(runCommand).Wait();
    }
}

[Runnable("go", "go", 0)]
public class GoRunnable : IRunnable
{
    public string Name => "go";
    public string Language => "go";
    public int Priority => 0;
    public string description => "Executes Go projects";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running Go project");

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
        var mainFilePath = Path.Combine(codePath, settings.Main_File);
        var runCommand = $"go run \"{mainFilePath}\"";
        Console.WriteLine(runCommand);

        terminalProcess.ExecuteCommand(runCommand).Wait();
    }
}

[Runnable("rust", "rust", 0)]
public class RustRunnable : IRunnable
{
    public string Name => "rust";
    public string Language => "rust";
    public int Priority => 0;
    public string description => "Executes Rust projects";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running Rust project");

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
        var mainFilePath = Path.Combine(codePath, settings.Main_File);
        var cargoTomlPath = Path.Combine(codePath, "Cargo.toml");

        if (File.Exists(cargoTomlPath))
        {
            var runCommand = $"cargo run --manifest-path \"{cargoTomlPath}\"";
            Console.WriteLine(runCommand);
            terminalProcess.ExecuteCommand(runCommand).Wait();
        }
        else
        {
            var outputFilePath = Path.Combine(codePath, settings.Output);
            terminalProcess.ExecuteCommand($"echo '\u001b[32m<color=green>Building Rust project...\u001b[0m'").Wait();
            terminalProcess.ExecuteCommand($"rustc -o \"{outputFilePath}\" \"{mainFilePath}\"").Wait();

            if (settings.Run_On_Build)
            {
                terminalProcess.ExecuteCommand($"echo '\u001b[32mRunning program...\u001b[0m'").Wait();
                terminalProcess.ExecuteCommand($"\"{outputFilePath}\"").Wait();
            }
        }
    }
}

[Runnable("ruby", "ruby", 0)]
public class RubyRunnable : IRunnable
{
    public string Name => "ruby";
    public string Language => "ruby";
    public int Priority => 0;
    public string description => "Executes Ruby projects";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running Ruby project");

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
        var mainFilePath = Path.Combine(codePath, settings.Main_File);
        var runCommand = $"ruby \"{mainFilePath}\"";
        Console.WriteLine(runCommand);

        terminalProcess.ExecuteCommand(runCommand).Wait();
    }
}

[Runnable("php", "php", 0)]
public class PhpRunnable : IRunnable
{
    public string Name => "php";
    public string Language => "php";
    public int Priority => 0;
    public string description => "Executes PHP projects";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running PHP project");

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
        var mainFilePath = Path.Combine(codePath, settings.Main_File);
        var runCommand = $"php \"{mainFilePath}\"";
        Console.WriteLine(runCommand);

        terminalProcess.ExecuteCommand(runCommand).Wait();
    }
}

[Runnable("java", "java", 0)]
public class JavaRunnable : IRunnable
{
    public string Name => "java";
    public string Language => "java";
    public int Priority => 0;
    public string description => "Executes Java projects";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running Java project");

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
        var mainFilePath = Path.Combine(codePath, settings.Main_File);
        var className = Path.GetFileNameWithoutExtension(settings.Main_File);

        terminalProcess.ExecuteCommand($"echo '\u001b[32m<color=green>Building Java project...\u001b[0m'").Wait();
        terminalProcess.ExecuteCommand($"javac \"{mainFilePath}\"").Wait();

        if (settings.Run_On_Build)
        {
            terminalProcess.ExecuteCommand($"echo '\u001b[32mRunning program...\u001b[0m'").Wait();
            terminalProcess.ExecuteCommand($"java -cp \"{codePath}\" {className}").Wait();
        }
    }
}

[Runnable("typescript", "typescript", 0)]
public class TypeScriptRunnable : IRunnable
{
    public string Name => "typescript";
    public string Language => "typescript";
    public int Priority => 0;
    public string description => "Executes TypeScript projects";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running TypeScript project");

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
        var mainFilePath = Path.Combine(codePath, settings.Main_File);
        var jsFilePath = Path.ChangeExtension(mainFilePath, ".js");

        terminalProcess.ExecuteCommand($"echo '\u001b[32m<color=green>Compiling TypeScript...\u001b[0m'").Wait();
        terminalProcess.ExecuteCommand($"tsc \"{mainFilePath}\"").Wait();

        if (settings.Run_On_Build)
        {
            terminalProcess.ExecuteCommand($"echo '\u001b[32mRunning program...\u001b[0m'").Wait();
            terminalProcess.ExecuteCommand($"node \"{jsFilePath}\"").Wait();
        }
    }
}

[Runnable("lua", "lua", 0)]
public class LuaRunnable : IRunnable
{
    public string Name => "lua";
    public string Language => "lua";
    public int Priority => 0;
    public string description => "Executes Lua projects";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running Lua project");

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
        var mainFilePath = Path.Combine(codePath, settings.Main_File);
        var runCommand = $"lua \"{mainFilePath}\"";
        Console.WriteLine(runCommand);

        terminalProcess.ExecuteCommand(runCommand).Wait();
    }
}

[Runnable("bash", "bash", 0)]
public class BashRunnable : IRunnable
{
    public string Name => "bash";
    public string Language => "bash";
    public int Priority => 0;
    public string description => "Executes Bash shell scripts";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running Bash project");

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
        var mainFilePath = Path.Combine(codePath, settings.Main_File);
        var runCommand = $"bash \"{mainFilePath}\"";
        Console.WriteLine(runCommand);

        terminalProcess.ExecuteCommand(runCommand).Wait();
    }
}

[Runnable("kotlin", "kotlin", 0)]
public class KotlinRunnable : IRunnable
{
    public string Name => "kotlin";
    public string Language => "kotlin";
    public int Priority => 0;
    public string description => "Executes Kotlin projects";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running Kotlin project");

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
        var mainFilePath = Path.Combine(codePath, settings.Main_File);
        var runCommand = $"kotlin \"{mainFilePath}\"";
        Console.WriteLine(runCommand);

        terminalProcess.ExecuteCommand(runCommand).Wait();
    }
}

[Runnable("julia", "julia", 0)]
public class JuliaRunnable : IRunnable
{
    public string Name => "julia";
    public string Language => "julia";
    public int Priority => 0;
    public string description => "Executes Julia projects";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running Julia project");

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
        var mainFilePath = Path.Combine(codePath, settings.Main_File);
        var runCommand = $"julia \"{mainFilePath}\"";
        Console.WriteLine(runCommand);

        terminalProcess.ExecuteCommand(runCommand).Wait();
    }
}

[Runnable("r", "r", 0)]
public class RRunnable : IRunnable
{
    public string Name => "r";
    public string Language => "r";
    public int Priority => 0;
    public string description => "Executes R scripts";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running R project");

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
        var mainFilePath = Path.Combine(codePath, settings.Main_File);
        var runCommand = $"Rscript \"{mainFilePath}\"";
        Console.WriteLine(runCommand);

        terminalProcess.ExecuteCommand(runCommand).Wait();
    }
}

[Runnable("haskell", "haskell", 0)]
public class HaskellRunnable : IRunnable
{
    public string Name => "haskell";
    public string Language => "haskell";
    public int Priority => 0;
    public string description => "Executes Haskell projects";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running Haskell project");

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
        var mainFilePath = Path.Combine(codePath, settings.Main_File);
        var runCommand = $"runghc \"{mainFilePath}\"";
        Console.WriteLine(runCommand);

        terminalProcess.ExecuteCommand(runCommand).Wait();
    }
}

[Runnable("perl", "perl", 0)]
public class PerlRunnable : IRunnable
{
    public string Name => "perl";
    public string Language => "perl";
    public int Priority => 0;
    public string description => "Executes Perl scripts";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running Perl project");

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
        var mainFilePath = Path.Combine(codePath, settings.Main_File);
        var runCommand = $"perl \"{mainFilePath}\"";
        Console.WriteLine(runCommand);

        terminalProcess.ExecuteCommand(runCommand).Wait();
    }
}

[Runnable("solidity", "solidity", 0)]
public class SolidityRunnable : IRunnable
{
    public string Name => "solidity";
    public string Language => "solidity";
    public int Priority => 0;
    public string description => "Compiles Solidity contracts using solc";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Compiling Solidity project");

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
        var mainFilePath = Path.Combine(codePath, settings.Main_File);
        var buildDir = Path.Combine(codePath, "build");
        var runCommand = $"solc --bin --abi -o \"{buildDir}\" \"{mainFilePath}\"";
        Console.WriteLine(runCommand);

        terminalProcess.ExecuteCommand(runCommand).Wait();
    }
}
