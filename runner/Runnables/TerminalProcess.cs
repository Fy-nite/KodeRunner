using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KodeRunner.Config;

namespace KodeRunner
{
    public class TerminalProcess : IAsyncDisposable
    {
#pragma warning disable CS8618
        public event Action<string> OnOutput;
#pragma warning restore CS8618

        // Add buffer size constant
        private const int BUFFER_SIZE = 8192;

        // Add process timeout
        private readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

        // Add cancellation support
        private CancellationTokenSource _cts = new CancellationTokenSource();

        // Add configuration
        private readonly Configuration _config;

        public TerminalProcess()
        {
            _config = Configuration.Load();
            _cts = new CancellationTokenSource();
        }

        /// <summary>
        /// Clears the buffer when the process is done.
        /// </summary>
        public async Task ClearBuffer()
        {
            await Task.Delay(1000);
            OnOutput?.Invoke("\n");
        }

        // Add static process tracking
        private static ConcurrentDictionary<int, Process> ActiveProcesses =
            new ConcurrentDictionary<int, Process>();

        private void SendOutput(string output)
        {
            if (string.IsNullOrEmpty(output))
                return;
            OnOutput?.Invoke(TerminalCodeParser.ParseToResonite(output));
        }

        /// <summary>
        /// Sends input to the active process.
        /// </summary>
        /// <param name="input">The input to send.</param>
        /// <returns>True if input was sent successfully, otherwise false.</returns>
        public bool SendInput(string input)
        {
            if (ActiveProcesses.Count == 0)
                return false;

            try
            {
                var process = ActiveProcesses[ActiveProcesses.Keys.First()];
                if (process.StartInfo.RedirectStandardInput)
                {
                    process.StandardInput.Write(input + Environment.NewLine);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Disposes the process asynchronously.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            StopAllProcesses();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Executes a command in the terminal.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <returns>The exit code of the process.</returns>
        public async Task<int> ExecuteCommand(string command)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_config.ProcessTimeoutSeconds));

            try
            {
                var tcs = new TaskCompletionSource<int>();
                var iswindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                    System.Runtime.InteropServices.OSPlatform.Windows
                );

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = iswindows ? "powershell.exe" : "/bin/bash",
                        Arguments = iswindows ? $"-Command \"{command}\"" : $"-c \"{command}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = true, // Add this line
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8,
                    },
                    EnableRaisingEvents = true,
                };

                process.Exited += (sender, args) =>
                {
                    ActiveProcesses.TryRemove(process.Id, out _);
                    tcs.SetResult(process.ExitCode);
                    process.Dispose();
                };

                process.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        foreach (char c in args.Data)
                        {
                            SendOutput(c.ToString());
                        }
                    }
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        foreach (char c in args.Data)
                        {
                            SendOutput(c.ToString());
                        }
                    }
                };

                process.Start();
                ActiveProcesses.TryAdd(process.Id, process);

                // Read standard output asynchronously
                _ = Task.Run(async () =>
                {
                    var buffer = new char[1];
                    while (!process.StandardOutput.EndOfStream)
                    {
                        int read = await process.StandardOutput.ReadAsync(buffer, 0, buffer.Length);
                        if (read > 0)
                        {
                            OnOutput?.Invoke(buffer[0].ToString());
                        }
                    }
                });

                // Read standard error asynchronously
                _ = Task.Run(async () =>
                {
                    var buffer = new char[1];
                    while (!process.StandardError.EndOfStream)
                    {
                        int read = await process.StandardError.ReadAsync(buffer, 0, buffer.Length);
                        if (read > 0)
                        {
                            OnOutput?.Invoke(buffer[0].ToString());
                        }
                    }
                });

                var processTask = process.WaitForExitAsync(timeoutCts.Token);

                await using (
                    timeoutCts.Token.Register(() =>
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(true);
                            Logger.Log(
                                $"Process timed out after {_config.ProcessTimeoutSeconds} seconds",
                                "Warning"
                            );
                        }
                    })
                )
                {
                    await processTask;
                }

                return await tcs.Task;
            }
            catch (OperationCanceledException)
            {
                Logger.Log($"Process execution cancelled", "Warning");
                return -1;
            }
        }

        /// <summary>
        /// Stops all active processes.
        /// </summary>
        public static void StopAllProcesses()
        {
            foreach (var processEntry in ActiveProcesses)
            {
                try
                {
                    var process = processEntry.Value;
                    if (!process.HasExited)
                    {
                        process.Kill(true); // Force kill the process and its children
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error stopping process {processEntry.Key}: {ex.Message}");
                }
            }
            ActiveProcesses.Clear();
        }
    }
}
