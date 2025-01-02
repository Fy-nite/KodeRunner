using System;
using System.IO;
using System.Reflection;

namespace KodeRunner
{
    public class Core
    {
        // Add version constants
        public const string VERSION = "1.0.0";
        public const string PMS_VERSION = "1.2.2";

        // Add PMS directory
        public static string PMSDir = "PMS";

        // Add helper method for path combining
        public static string GetPath(params string[] paths)
        {
            return Path.Combine(RootDir, Path.Combine(paths));
        }

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
        public static string ExportDir = "Exports";

        // Updated to use Path.Combine for proper path construction
        public static string ConfigPath = Path.Combine(RootDir, ConfigDir, ConfigFile);
        public static string RunnableDir = Path.Combine(RootDir, "Runnables");
        public static string IncludesDir = Path.Combine(RootDir, "Includes");
        public static HashSet<string> dirs = new HashSet<string>
        {
            CodeDir,
            BuildDir,
            TempDir,
            OutputDir,
            LogDir,
            ConfigDir,
            ExportDir,
        };
    }
}
