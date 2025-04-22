using KodeRunner;
using System.Net.WebSockets;
using System.Text;
namespace shitters
{
    [Runnable("testrunner", "testrunner", 0)]
    class TestRunner : IRunnable
    {
        public string Name => "testrunner";
        public string Language => "testrunner";
        public int Priority => 0;
        public string description => "test runner for examples";

        public void Execute(Provider.ISettingsProvider settings)
        {
            Console.WriteLine("Running testrunner project");

            var terminalProcess = new TerminalProcess();

            // Capture the PMS WebSocket from settings
            WebSocket pmsWebSocket = settings.PmsWebSocket;

            terminalProcess.OnOutput += async (output) =>
            {
                
                if (pmsWebSocket != null && pmsWebSocket.State == WebSocketState.Open)
                {
                    var bytes = Encoding.UTF8.GetBytes(output);
                    await pmsWebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            };

            terminalProcess.ExecuteCommand("echo \"hello world\"").Wait();
        }
    }
}