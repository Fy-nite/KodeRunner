namespace KodeRunner.Terminal
{
    class Terminal
    {
        public static bool advancedterm => FastTerminal.AdvancedTerm;

        public static void init()
        {
            FastTerminal.Initialize();
        }

        public static void Write(string str, string window)
        {
            FastTerminal.WriteToWindow(str, window);
        }

        public static void WriteLine(string str, string window)
        {
            FastTerminal.WriteLineToWindow(str, window);
        }

        // Legacy compatibility methods
        public static void UpdateRunnables()
        {
            FastTerminal.UpdateRunnables();
        }

        public static void UpdateConnections()
        {
            FastTerminal.UpdateConnections();
        }

        public static void AddRunnable(string name, string language, int priority)
        {
            // This could be enhanced to work with the new system
            FastTerminal.LogMessage($"Runnable added: {name} ({language}) - Priority: {priority}");
        }

        // Add new enhanced logging methods
        public static void LogToTerminal(string message, string level = "Info")
        {
            FastTerminal.LogToTerminal(message, level);
        }

        // Enhanced methods for better integration
        public static async Task HandleCommands()
        {
            // This method exists for compatibility but the new system handles commands internally
            await Task.CompletedTask;
        }
    }
}
