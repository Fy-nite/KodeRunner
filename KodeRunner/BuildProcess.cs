using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using KodeRunner;
using Tuvalu.logger;
namespace KodeRunner
{
   #region StartBuildProcess
    public class BuildProcess : IRunnable, IDisposable, IAsyncDisposable
    {
        public string Name => "Build Process";
        public string Language => "ANY";
        public int Priority => 9999999;
        public string description => "Executes build process for all languages that have been registered for IRunnable";

        private readonly Process _process;
        public event Action<string> OnOutput;
        public List<string> CommentRegexes = new List<string>
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
            @"^--"
        };
        public BuildProcess()
        {
            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash", // Use "cmd.exe" for Windows
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = false
                }
            };

            _process.Start();
            StartOutputReader();
        }
        public void Dispose()
        {
            _process.Kill();
            _process.Dispose();
        }
        public async ValueTask DisposeAsync()
        {
            _process.Kill();
            _process.Dispose();
            await Task.CompletedTask;
        }
        private void StartOutputReader()
        {
            Task.Run(async () =>
            {
                var buffer = new byte[1024];
                while (!_process.HasExited)
                {
                    int read = await _process.StandardOutput.BaseStream.ReadAsync(buffer, 0, buffer.Length);
                    if (read > 0)
                    {
                        string output = System.Text.Encoding.UTF8.GetString(buffer, 0, read);
                        OnOutput?.Invoke(output);
                    }
                }
            });
        }

        public void SetupCodeDir()
        {
            string Cpath = Path.Combine(Core.RootDir, Core.CodeDir);
            string Bpath = Path.Combine(Core.RootDir, Core.BuildDir);
            string Tpath = Path.Combine(Core.RootDir, Core.TempDir);
            string Opath = Path.Combine(Core.RootDir, Core.OutputDir);
            string Lpath = Path.Combine(Core.RootDir, Core.LogDir);
            string Rpath = Path.Combine(Core.RootDir, Core.RunnableDir);

            if (!Directory.Exists(Cpath))
            {
                Directory.CreateDirectory(Cpath);
                Logger.Log(Core.LoggerHandle + "created Code path");
            }
            if (!Directory.Exists(Bpath))
            {
                Directory.CreateDirectory(Bpath);
                Logger.Log(Core.LoggerHandle + "Created Build path");
            }
            if (!Directory.Exists(Tpath))
            {
                Directory.CreateDirectory(Tpath);
                Logger.Log(Core.LoggerHandle + "Created Temp path");
            }
            if (!Directory.Exists(Opath))
            {
                Directory.CreateDirectory(Opath);
                Logger.Log(Core.LoggerHandle + "Created Output path");
            }
            if (!Directory.Exists(Lpath))
            {
                Directory.CreateDirectory(Lpath);
                Logger.Log(Core.LoggerHandle + "Created Log path");
            }
            if (!Directory.Exists(Rpath))
            {
                Directory.CreateDirectory(Rpath);
                Logger.Log(Core.LoggerHandle + "Created Runnable path");
            }
            

        }

        public async Task SendInput(string input)
        {
            await _process.StandardInput.WriteLineAsync(input);
        }
        public void Execute(Provider.ISettingsProvider settings)
        {
            throw new NotImplementedException();
        }
    }

}
#endregion