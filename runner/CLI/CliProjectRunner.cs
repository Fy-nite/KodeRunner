using KodeRunner.Security;
using System.Text;

namespace KodeRunner.CLI
{
    public class CliProjectRunner
    {
        private readonly RunnableManager _runnableManager;
        private readonly SandboxManager _sandboxManager;

        public CliProjectRunner()
        {
            try
            {
                // Ensure directories exist before initializing components
                Program.EnsureFolders();
                
                _runnableManager = new RunnableManager();
                _runnableManager.LoadRunnables();
                
                // Check if runnables directory exists and load external runnables
                if (Directory.Exists(Core.RunnableDir))
                {
                    if (Directory.GetFiles(Core.RunnableDir, "*.dll").Length > 0)
                    {
                        _runnableManager.LoadRunnablesFromDirectory(Core.RunnableDir);
                    }
                }
                
                _sandboxManager = new SandboxManager();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing CLI runner: {ex.Message}");
                throw;
            }
        }

        public async Task RunProject(string projectPath, string language, string mainFile, string sandboxPolicy = "default")
        {
            try
            {
                // Validate inputs
                if (!Directory.Exists(projectPath))
                {
                    Console.WriteLine($"Error: Project directory not found: {projectPath}");
                    return;
                }

                if (!string.IsNullOrEmpty(mainFile) && !File.Exists(Path.Combine(projectPath, mainFile)))
                {
                    Console.WriteLine($"Error: Main file not found: {mainFile}");
                    return;
                }

                // Check if runnable exists for language
                if (!_runnableManager.HasRunnable(language))
                {
                    Console.WriteLine($"Error: No runnable found for language: {language}");
                    Console.WriteLine("Available languages:");
                    _runnableManager.print();
                    return;
                }

                // Prepare settings
                var settings = new Provider.SettingsProvider
                {
                    Language = language,
                    ProjectName = Path.GetFileName(projectPath),
                    ProjectPath = projectPath,
                    Main_File = mainFile ?? "",
                    Run_On_Build = true,
                    Output = "output"
                };

                Console.WriteLine($"Executing {language} project with sandbox policy: {sandboxPolicy}");
                Console.WriteLine(new string('=', 50));

                var startTime = DateTime.UtcNow;

                // Execute with sandbox if not none
                if (sandboxPolicy != "none")
                {
                    await ExecuteWithSandbox(settings, sandboxPolicy);
                }
                else
                {
                    _runnableManager.ExecuteFirstMatchingLanguage(language, settings);
                }

                var duration = DateTime.UtcNow - startTime;
                Console.WriteLine(new string('=', 50));
                Console.WriteLine($"Execution completed in {duration.TotalSeconds:F2} seconds");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing project: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        public async Task BuildProject(string projectPath, string language, string outputName)
        {
            try
            {
                if (!Directory.Exists(projectPath))
                {
                    Console.WriteLine($"Error: Project directory not found: {projectPath}");
                    return;
                }

                if (!_runnableManager.HasRunnable(language))
                {
                    Console.WriteLine($"Error: No runnable found for language: {language}");
                    Console.WriteLine("Available runnables:");
                    _runnableManager.print();
                    return;
                }

                var settings = new Provider.SettingsProvider
                {
                    Language = language,
                    ProjectName = Path.GetFileName(projectPath),
                    ProjectPath = projectPath,
                    Run_On_Build = false,
                    Output = outputName,
                    Main_File = "" // Set empty for build-only operations
                };

                Console.WriteLine($"Building {language} project...");
                Console.WriteLine(new string('=', 50));

                var startTime = DateTime.UtcNow;
                
                try
                {
                    _runnableManager.ExecuteFirstMatchingLanguage(language, settings);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during build execution: {ex.Message}");
                    throw;
                }
                
                var duration = DateTime.UtcNow - startTime;

                Console.WriteLine(new string('=', 50));
                Console.WriteLine($"Build completed in {duration.TotalSeconds:F2} seconds");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error building project: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        private async Task ExecuteWithSandbox(Provider.SettingsProvider settings, string sandboxPolicy)
        {
            try
            {
                var command = BuildExecutionCommand(settings);
                var result = await _sandboxManager.ExecuteInSandbox(command, sandboxPolicy);

                if (!result.Success)
                {
                    Console.WriteLine($"Sandbox execution failed: {result.Error}");
                    if (result.ResourceUsage != null)
                    {
                        Console.WriteLine($"Resource usage - Memory: {result.ResourceUsage.MaxMemoryUsedMB}MB, CPU: {result.ResourceUsage.CpuTimeSeconds:F2}s");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in sandbox execution: {ex.Message}");
                throw;
            }
        }

        private string BuildExecutionCommand(Provider.SettingsProvider settings)
        {
            var projectPath = settings.ProjectPath;
            var mainFile = settings.Main_File;
            var language = settings.Language.ToLower();

            return language switch
            {
                "python" => $"python \"{Path.Combine(projectPath, mainFile)}\"",
                "nodejs" => $"node \"{Path.Combine(projectPath, mainFile)}\"",
                "c" => $"gcc -o \"{Path.Combine(projectPath, settings.Output)}\" \"{Path.Combine(projectPath, mainFile)}\" && \"{Path.Combine(projectPath, settings.Output)}\"",
                "csharp" => $"dotnet run --project \"{projectPath}\"",
                _ => throw new NotSupportedException($"CLI execution not supported for language: {language}")
            };
        }
    }
}
