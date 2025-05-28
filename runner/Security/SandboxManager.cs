using System.Diagnostics;
using System.Security;
using System.Text;
using KodeRunner.Config;

namespace KodeRunner.Security
{
    public class SandboxManager
    {
        private readonly Configuration _config;
        private readonly Dictionary<string, SandboxPolicy> _policies = new();

        public SandboxManager()
        {
            _config = Configuration.Load();
            InitializeDefaultPolicies();
        }

        private void InitializeDefaultPolicies()
        {
            _policies["default"] = new SandboxPolicy
            {
                MaxMemoryMB = 512,
                MaxCpuTimeSeconds = 30,
                AllowNetworkAccess = false,
                AllowFileSystemWrite = false,
                AllowedDirectories = new[] { Core.GetPath(Core.TempDir) },
                BlockedCommands = new[] { "rm", "del", "format", "shutdown" }
            };

            _policies["trusted"] = new SandboxPolicy
            {
                MaxMemoryMB = 2048,
                MaxCpuTimeSeconds = 300,
                AllowNetworkAccess = true,
                AllowFileSystemWrite = true,
                AllowedDirectories = new[] { Core.RootDir },
                BlockedCommands = new[] { "shutdown", "reboot" }
            };
        }

        public async Task<SandboxResult> ExecuteInSandbox(string command, string policyName = "default")
        {
            if (!_policies.TryGetValue(policyName, out var policy))
                policy = _policies["default"];

            var result = new SandboxResult { StartTime = DateTime.UtcNow };

            try
            {
                // Enhanced command validation
                if (IsCommandBlocked(command, policy))
                {
                    result.Success = false;
                    result.Error = "Command contains blocked operations";
                    Logger.Log($"Blocked command execution: {command}", "Warning");
                    return result;
                }

                using var process = new Process();
                process.StartInfo = CreateSandboxedStartInfo(command, policy);
                
                // Capture output for CLI
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();
                
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        outputBuilder.AppendLine(e.Data);
                        Console.WriteLine(e.Data);
                    }
                };
                
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                        Console.Error.WriteLine(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Monitor resource usage
                var monitorTask = MonitorResourceUsage(process, policy);
                
                var timeout = TimeSpan.FromSeconds(policy.MaxCpuTimeSeconds);
                using var cts = new CancellationTokenSource(timeout);
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                    result.ExitCode = process.ExitCode;
                    result.Success = process.ExitCode == 0;
                }
                catch (OperationCanceledException)
                {
                    try
                    {
                        process.Kill(true);
                    }
                    catch (Exception killEx)
                    {
                        Logger.Log($"Error killing process: {killEx.Message}", "Error");
                    }
                    result.Success = false;
                    result.Error = "Process exceeded time limit";
                }

                result.ResourceUsage = await monitorTask;
                result.StandardOutput = outputBuilder.ToString();
                result.StandardError = errorBuilder.ToString();
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                Logger.Log($"Sandbox execution error: {ex.Message}", "Error");
            }
            finally
            {
                result.EndTime = DateTime.UtcNow;
            }

            return result;
        }

        private bool IsCommandBlocked(string command, SandboxPolicy policy)
        {
            // Enhanced command blocking logic
            var normalizedCommand = command.ToLowerInvariant();
            
            foreach (var blockedCmd in policy.BlockedCommands)
            {
                // Check for exact command matches and dangerous patterns
                if (normalizedCommand.Contains(blockedCmd.ToLowerInvariant()) ||
                    normalizedCommand.Contains($" {blockedCmd.ToLowerInvariant()} ") ||
                    normalizedCommand.StartsWith(blockedCmd.ToLowerInvariant() + " "))
                {
                    return true;
                }
            }

            // Additional security checks
            var dangerousPatterns = new[]
            {
                "../../", // Path traversal
                "$(", // Command substitution
                "`", // Backtick command substitution
                "; ", // Command chaining
                "| ", // Pipe to potentially dangerous commands
                "&& ", // Command chaining
                "|| " // Command chaining
            };

            return dangerousPatterns.Any(pattern => normalizedCommand.Contains(pattern));
        }

        public IEnumerable<string> GetAvailablePolicies()
        {
            return _policies.Keys;
        }

        public SandboxPolicy GetPolicy(string name)
        {
            return _policies.TryGetValue(name, out var policy) ? policy : _policies["default"];
        }

        public void AddCustomPolicy(string name, SandboxPolicy policy)
        {
            _policies[name] = policy;
            Logger.Log($"Added custom sandbox policy: {name}");
        }

        private ProcessStartInfo CreateSandboxedStartInfo(string command, SandboxPolicy policy)
        {
            var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
            
            return new ProcessStartInfo
            {
                FileName = isWindows ? "powershell.exe" : "/bin/bash",
                Arguments = isWindows ? $"-Command \"{command}\"" : $"-c \"{command}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Core.GetPath(Core.TempDir)
            };
        }

        private async Task<ResourceUsage> MonitorResourceUsage(Process process, SandboxPolicy policy)
        {
            var usage = new ResourceUsage();
            var maxMemory = 0L;

            try
            {
                while (!process.HasExited)
                {
                    process.Refresh();
                    var currentMemory = process.WorkingSet64;
                    maxMemory = Math.Max(maxMemory, currentMemory);

                    if (currentMemory > policy.MaxMemoryMB * 1024 * 1024)
                    {
                        process.Kill(true);
                        throw new SecurityException("Process exceeded memory limit");
                    }

                    await Task.Delay(100);
                }
            }
            catch (InvalidOperationException)
            {
                // Process has exited
            }

            usage.MaxMemoryUsedMB = maxMemory / (1024 * 1024);
            usage.CpuTimeSeconds = process.TotalProcessorTime.TotalSeconds;
            
            return usage;
        }
    }

    public class SandboxPolicy
    {
        public int MaxMemoryMB { get; set; }
        public int MaxCpuTimeSeconds { get; set; }
        public bool AllowNetworkAccess { get; set; }
        public bool AllowFileSystemWrite { get; set; }
        public string[] AllowedDirectories { get; set; } = Array.Empty<string>();
        public string[] BlockedCommands { get; set; } = Array.Empty<string>();
    }

    public class SandboxResult
    {
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string Error { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public ResourceUsage ResourceUsage { get; set; }
        public string StandardOutput { get; set; } = string.Empty;
        public string StandardError { get; set; } = string.Empty;
        public TimeSpan Duration => EndTime - StartTime;
    }

    public class ResourceUsage
    {
        public long MaxMemoryUsedMB { get; set; }
        public double CpuTimeSeconds { get; set; }
    }
}
