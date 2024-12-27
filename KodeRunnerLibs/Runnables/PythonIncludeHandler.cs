using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using KodeRunner;

namespace KodeRunnerLibs.Runnables
{
    [LanguageIncludeHandler("python", 1)]
    public class PythonIncludeHandler : ILanguageIncludeHandler
    {
        public string Language => "python";

        public async Task<List<string>> ProcessIncludes(string projectPath, string[] includes)
        {
            var copiedFiles = new List<string>();
            var pythonIncludesPath = Path.Combine(Core.IncludesDir, "python");

            foreach (var include in includes)
            {
                if (include.EndsWith(".py"))
                {
                    var sourcePath = Path.Combine(pythonIncludesPath, include);
                    var destPath = Path.Combine(projectPath, include);

                    if (File.Exists(sourcePath))
                    {
                        var destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }

                        await using (var sourceStream = File.OpenRead(sourcePath))
                        await using (var destStream = File.Create(destPath))
                        {
                            await sourceStream.CopyToAsync(destStream);
                        }

                        copiedFiles.Add(include);
                        Logger.Log($"Included Python file: {include}", "Info");
                    }
                }
            }

            return copiedFiles;
        }
    }
}
