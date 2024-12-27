using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace KodeRunner
{
    [AttributeUsage(AttributeTargets.Class)]
    public class IncludeHandlerAttribute : Attribute
    {
        public string Language { get; }
        public int Priority { get; }

        public IncludeHandlerAttribute(string language, int priority = 0)
        {
            Language = language;
            Priority = priority;
        }
    }

    public interface IIncludeHandler
    {
        Task<List<string>> ProcessIncludes(string projectPath, string[] includes);
        string Language { get; }
    }

    public class IncludeManager
    {
        private readonly Dictionary<string, IIncludeHandler> _handlers = new();

        public IncludeManager()
        {
            LoadHandlers();
        }

        private void LoadHandlers()
        {
            // Look for handlers in the current assembly and any assemblies in the Runnables directory
            var assemblies = new List<Assembly> { Assembly.GetExecutingAssembly() };
            var runnablesPath = Core.RunnableDir;

            if (Directory.Exists(runnablesPath))
            {
                foreach (var file in Directory.GetFiles(runnablesPath, "*.dll"))
                {
                    try
                    {
                        assemblies.Add(Assembly.LoadFrom(file));
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Error loading assembly {file}: {ex.Message}", "Error");
                    }
                }
            }

            foreach (var assembly in assemblies)
            {
                try
                {
                    var handlerTypes = assembly
                        .GetTypes()
                        .Where(t =>
                            typeof(IIncludeHandler).IsAssignableFrom(t)
                            && !t.IsInterface
                            && !t.IsAbstract
                        )
                        .Select(t => new
                        {
                            Type = t,
                            Attr = t.GetCustomAttribute<IncludeHandlerAttribute>(),
                        })
                        .Where(x => x.Attr != null)
                        .OrderByDescending(x => x.Attr.Priority);

                    foreach (var handler in handlerTypes)
                    {
                        var instance = (IIncludeHandler)Activator.CreateInstance(handler.Type);
                        _handlers[handler.Attr.Language.ToLower()] = instance;
                        Logger.Log($"Loaded include handler for {handler.Attr.Language}", "Info");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error loading handlers from assembly: {ex.Message}", "Error");
                }
            }
        }

        public async Task<List<string>> ProcessIncludes(
            string projectPath,
            string language,
            string[] includes
        )
        {
            if (includes == null || includes.Length == 0)
                return new List<string>();

            language = language.ToLower();

            // Try to find a specific handler first
            if (_handlers.TryGetValue(language, out var handler))
            {
                return await handler.ProcessIncludes(projectPath, includes);
            }

            // Fall back to default handler
            if (_handlers.TryGetValue("default", out var defaultHandler))
            {
                return await defaultHandler.ProcessIncludes(projectPath, includes);
            }

            Logger.Log($"No include handler found for language: {language}", "Warning");
            return new List<string>();
        }
    }

    // Example default include handler
    [IncludeHandler("default", 0)]
    public class DefaultIncludeHandler : IIncludeHandler
    {
        public string Language => "default";

        public async Task<List<string>> ProcessIncludes(string projectPath, string[] includes)
        {
            var copiedFiles = new List<string>();
            var includesBasePath = Path.Combine(Core.IncludesDir, "common");

            if (!Directory.Exists(includesBasePath))
            {
                Logger.Log("Common includes directory not found", "Warning");
                return copiedFiles;
            }

            foreach (var include in includes)
            {
                try
                {
                    var sourcePath = Path.Combine(includesBasePath, include);
                    var destPath = Path.Combine(projectPath, include);

                    if (!File.Exists(sourcePath))
                    {
                        Logger.Log($"Include file not found: {include}", "Warning");
                        continue;
                    }

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
                    Logger.Log($"Included file: {include}", "Info");
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error including file {include}: {ex.Message}", "Error");
                }
            }

            return copiedFiles;
        }
    }
}
