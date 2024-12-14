using System;
using System.IO;
using System.Reflection;

namespace KodeRunner
{
    public class Core
    {
        public static string GetVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        // Base directory for all KodeRunner files
        public static string RootDir = Path.Combine(Directory.GetCurrentDirectory(), "koderunner");
        
        // All other directories are now relative to RootDir
        public static string LoggerHandle = "[KodeRunner]: ";
        public static string CodeDir = "Projects";
        public static string BuildDir = "Builds";
        public static string TempDir = "Temp";
        public static string OutputDir = "Output";
        public static string LogDir = "Logs";
        public static string ConfigDir = "Config";
        public static string ConfigFile = "config.json";
        
        // Updated to use Path.Combine for proper path construction
        public static string ConfigPath = Path.Combine(RootDir, ConfigDir, ConfigFile);
        public static string RunnableDir = Path.Combine(RootDir, "Runnables");
    }
}