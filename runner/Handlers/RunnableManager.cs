using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace KodeRunner
{
    public class RunnableManager
    {
        private readonly Dictionary<string, Action<Provider.ISettingsProvider>> _runnables =
            new Dictionary<string, Action<Provider.ISettingsProvider>>();

        // Add error event handling
        public event Action<string> OnError;

        // Add runnable loaded event
        public event Action<string> OnRunnableLoaded;

        // Add method to check if runnable exists
        public bool HasRunnable(string language)
        {
            return _runnables.Any(r => r.Key.StartsWith(language));
        }

        /// <summary>
        /// Prints the registered runnables.
        /// </summary>
        public void print()
        {
            foreach (var runnable in _runnables)
            {
                var attr = runnable.Value.Method.GetCustomAttribute<RunnableAttribute>();
                Terminal.Terminal.Write(
                    $"Language: {runnable.Key}, Name: {runnable.Value.Method.Name}, Priority: {attr?.Priority ?? 0}\n",
                    "Logs"
                );
            }
        }

        /// <summary>
        /// Loads runnables from the specified directory.
        /// </summary>
        /// <param name="path">The path to the directory containing runnable assemblies.</param>
        public void LoadRunnablesFromDirectory(string path)
        {
            var dllFiles = Directory.GetFiles(path, "*.dll");
            foreach (var dllPath in dllFiles)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(dllPath);
                    var runnables = assembly
                        .GetTypes()
                        .Where(type =>
                            typeof(IRunnable).IsAssignableFrom(type)
                            && !type.IsInterface
                            && !type.IsAbstract
                        )
                        .Select(type => (IRunnable)Activator.CreateInstance(type))
                        .ToList();

                    foreach (var runnable in runnables)
                    {
                        var attribute = runnable.GetType().GetCustomAttribute<RunnableAttribute>();
                        if (attribute != null)
                        {
                            RegisterRunnable(
                                attribute.Name,
                                runnable.Name,
                                runnable.Execute,
                                attribute.Priority
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error loading assembly {dllPath}: {ex.Message}", "Error");
                }
            }
        }

        /// <summary>
        /// Loads runnables from the current AppDomain.
        /// </summary>
        public void LoadRunnables()
        {
            var runnables = AppDomain
                .CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type =>
                    typeof(IRunnable).IsAssignableFrom(type)
                    && !type.IsInterface
                    && !type.IsAbstract
                )
                .Select(type => (IRunnable)Activator.CreateInstance(type))
                .ToList();
            if (runnables.Count == 0)
            {
                // register the runables inside the directory
                LoadRunnablesFromDirectory("Runnables");
            }
            foreach (var runnable in runnables)
            {
                var attribute = runnable.GetType().GetCustomAttribute<RunnableAttribute>();
                if (attribute != null)
                {
                    RegisterRunnable(
                        attribute.Name,
                        runnable.Name,
                        runnable.Execute,
                        attribute.Priority
                    );
                }
            }
        }

        /// <summary>
        /// Executes all registered runnables.
        /// </summary>
        /// <param name="settings">The settings to pass to the runnables.</param>
        public void ExecuteAll(Provider.ISettingsProvider settings)
        {
            foreach (var runnable in _runnables)
            {
                runnable.Value(settings);
            }
        }

        /// <summary>
        /// Executes the first runnable matching the specified language.
        /// </summary>
        /// <param name="language">The language to match.</param>
        /// <param name="settings">The settings to pass to the runnable.</param>
        /// <exception cref="KeyNotFoundException">Thrown if no runnable is found for the specified language.</exception>
        public void ExecuteFirstMatchingLanguage(
            string language,
            Provider.ISettingsProvider settings
        )
        {
            try
            {
                foreach (var runnable in _runnables)
                {
                    if (runnable.Key.StartsWith(language))
                    {
                        runnable.Value(settings);
                        return;
                    }
                }
                LoadRunnables();
                // Try one more time after loading runnables
                foreach (var runnable in _runnables)
                {
                    if (runnable.Key.StartsWith(language))
                    {
                        runnable.Value(settings);
                        return;
                    }
                }
                Logger.Log($"No runnable found for language: {language}", "Warning");
            }
            catch (Exception ex)
            {
                Logger.Log(
                    $"Error executing runnable for language {language}: {ex.Message}", "Error"
                );
            }
        }

        /// <summary>
        /// Registers a runnable.
        /// </summary>
        /// <param name="name">The name of the runnable.</param>
        /// <param name="language">The language of the runnable.</param>
        /// <param name="action">The action to execute the runnable.</param>
        /// <param name="priority">The priority of the runnable.</param>
        /// <param name="description">The description of the runnable.</param>
        public void RegisterRunnable(
            string name,
            string language,
            Action<Provider.ISettingsProvider> action,
            int priority = 0,
            string description = null
        )
        {
            Terminal.Terminal.AddRunnable(name, language, priority);
            string key = $"{language}_{name}_{priority}";
            _runnables[key] = action;
        }

        /// <summary>
        /// Executes a runnable by key.
        /// </summary>
        /// <param name="key">The key of the runnable.</param>
        /// <param name="settings">The settings to pass to the runnable.</param>
        /// <exception cref="KeyNotFoundException">Thrown if no runnable is found for the specified key.</exception>
        public void Execute(string key, Provider.ISettingsProvider settings)
        {
            if (_runnables.TryGetValue(key, out var action))
            {
                action(settings);
            }
            else
            {
                throw new KeyNotFoundException($"No runnable found for key: {key}");
            }
        }
    }
}
