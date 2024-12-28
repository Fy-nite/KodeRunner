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
            var configPath = Core.GetPath(Core.ConfigDir, "config.json");
            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    return JsonSerializer.Deserialize<Configuration>(json) ?? new Configuration();
                }
                catch
                {
                    return new Configuration();
                }
            }

            var config = new Configuration();
            Save(config);
            return config;
        }

        public static void Save(Configuration config)
        {
            var configPath = Core.GetPath(Core.ConfigDir, "config.json");
            var options = new JsonSerializerOptions { WriteIndented = true };

            try
            {
                File.WriteAllText(configPath, JsonSerializer.Serialize(config, options));
            }
            catch
            {
                // create the directory and try again
                Directory.CreateDirectory(Core.ConfigDir);
                File.WriteAllText(configPath, JsonSerializer.Serialize(config, options));
            }
        }
    }
}
