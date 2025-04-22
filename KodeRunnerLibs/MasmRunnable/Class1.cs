namespace MasmRunnable;


[Runnable("microasm", "MASM", 0)]
public class MicroASMRunnable : IRunnable
{
    public string Name => "microasm";
    public string Language => "MASM";
    public int Priority => 0;
    public string description => "Executes MicroASM projects";

    public void Execute(Provider.ISettingsProvider settings)
    {
        Console.WriteLine("Running MicroASM project");

        var terminalProcess = new TerminalProcess();

        // Capture the PMS WebSocket from settings
        WebSocket pmsWebSocket = settings.PmsWebSocket;

        terminalProcess.OnOutput += async (output) =>
        {
            if (pmsWebSocket != null && pmsWebSocket.State == WebSocketState.Open)
            {
                var bytes = Encoding.UTF8.GetBytes(output);
                await pmsWebSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
            }
        };

        var codePath = Path.Combine(Core.RootDir, Core.CodeDir, settings.ProjectName);
        var mainFilePath = Path.Combine(codePath, settings.Main_File);
        

        terminalProcess.ExecuteCommand(runCommand).Wait();
    }
}
