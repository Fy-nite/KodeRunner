using System.Text.Json;

namespace KodeRunner.Config
{
    public class Configuration
    {
        public int ProcessTimeoutSeconds { get; set; } = 30;
        public string LogLevel { get; set; } = "Info";
        public int BufferSize { get; set; } = 8192;
        public bool EnableDebugMode { get; set; } = false;
        public WebServerConfig WebServer { get; set; } = new();
        public LoggingConfig Logging { get; set; } = new();

        public class WebServerConfig
        {
            public string Host { get; set; } = "localhost";
            public int Port { get; set; } = 5000;
            public int MaxConnections { get; set; } = 100;
        }

        public class LoggingConfig
        {
            public bool EnableFileLogging { get; set; } = true;
            public bool EnableConsoleLogging { get; set; } = true;
            public string LogDirectory { get; set; } = "Logs";
            public int MaxLogFiles { get; set; } = 10;
            public int MaxLogSize { get; set; } = 10485760; // 10MB
        }

        public static Configuration Load()
        {
            var configDir = Core.GetPath(Core.ConfigDir);
            var configPath = Path.Combine(configDir, "config.json");

            // Ensure config directory exists
            if (!Directory.Exists(configDir))
            {
                try
                {
                    Directory.CreateDirectory(configDir);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to create config directory: {ex.Message}", "Error");
                    return new Configuration();
                }
            }

            // Load or create config file
            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<Configuration>(json);
                    if (config != null)
                    {
                        return config;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to load configuration: {ex.Message}", "Error");
                }
            }

            // Create default config if loading failed or file doesn't exist
            var defaultConfig = new Configuration();
            try
            {
                Save(defaultConfig);
                Logger.Log("Created new default configuration file", "Info");
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to save default configuration: {ex.Message}", "Error");
            }

            return defaultConfig;
        }

        public static void Save(Configuration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var configDir = Core.GetPath(Core.ConfigDir);
            var configPath = Path.Combine(configDir, "config.json");
            var options = new JsonSerializerOptions { WriteIndented = true };

            try
            {
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                var json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to save configuration: {ex.Message}", "Error");
                throw; // Re-throw to allow caller to handle the error
            }
        }
    }
}
