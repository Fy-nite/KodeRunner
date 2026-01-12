import org.java_websocket.enums.ReadyState;
import org.java_websocket.handshake.ServerHandshake;
import java.net.URI;
import java.util.function.BiConsumer;

public class WebSocketClient extends org.java_websocket.client.WebSocketClient {
    private BiConsumer<String, String> outputHandler;
    private String endpointName;
    private StringBuilder messageBuffer = new StringBuilder();
    private boolean isPMSEndpoint;
    
    public WebSocketClient(URI serverUri) {
        super(serverUri);
        this.endpointName = serverUri.getPath();
        this.isPMSEndpoint = endpointName.contains("pms");
    }
    
    public void setOutputHandler(BiConsumer<String, String> handler) {
        this.outputHandler = handler;
    }
    
    @Override
    public void onOpen(ServerHandshake handshake) {
        System.out.println("Connected to " + endpointName);
        if (outputHandler != null) {
            outputHandler.accept(endpointName, "[CONNECTED] Connected to " + endpointName);
        }
    }
    
    @Override
    public void onMessage(String message) {
        if (isPMSEndpoint) {
            // Simply append all PMS output to buffer
            messageBuffer.append(message);
        } else {
            // Handle other endpoints normally
            System.out.println("Received from " + endpointName + ": " + message);
            if (outputHandler != null) {
                outputHandler.accept(endpointName, message);
            }
        }
    }
    
    @Override
    public void onClose(int code, String reason, boolean remote) {
        System.out.println("Disconnected from " + endpointName + ": " + reason);
        if (outputHandler != null) {
            outputHandler.accept(endpointName, "[DISCONNECTED] " + reason);
        }
    }
    
    @Override
    public void onError(Exception ex) {
        System.err.println("Error on " + endpointName + ": " + ex.getMessage());
        if (outputHandler != null) {
            outputHandler.accept(endpointName, "[ERROR] " + ex.getMessage());
        }
    }
    
    public boolean isOpen() {
        return getReadyState() == ReadyState.OPEN;
    }
    
    public void flushBuffer() {
        if (isPMSEndpoint && messageBuffer.length() > 0) {
            String bufferedContent = messageBuffer.toString();
            if (outputHandler != null) {
                outputHandler.accept("terminal", bufferedContent);
            }
            messageBuffer.setLength(0);
        }
    }
}
