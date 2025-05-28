import javax.swing.*;
import javax.swing.border.TitledBorder;
import java.awt.*;

public class ConnectionMonitorPanel extends JPanel implements OutputHandler {
    private KodeRunnerTester parent;
    private JTextArea logArea;
    private JButton clearBtn;
    
    public ConnectionMonitorPanel(KodeRunnerTester parent) {
        this.parent = parent;
        initializeUI();
        setupEventHandlers();
    }
    
    private void initializeUI() {
        setLayout(new BorderLayout());
        
        // Create toolbar
        JPanel toolbar = new JPanel(new FlowLayout());
        clearBtn = new JButton("Clear Log");
        JButton refreshBtn = new JButton("Refresh Status");
        
        toolbar.add(clearBtn);
        toolbar.add(refreshBtn);
        
        add(toolbar, BorderLayout.NORTH);
        
        // Create log area
        logArea = new JTextArea();
        logArea.setEditable(false);
        logArea.setFont(new Font(Font.MONOSPACED, Font.PLAIN, 11));
        JScrollPane logScroll = new JScrollPane(logArea);
        logScroll.setBorder(new TitledBorder("Connection Log"));
        
        add(logScroll, BorderLayout.CENTER);
        
        // Add initial content
        logArea.setText("Connection Monitor\n" +
                       "=================\n" +
                       "This panel shows all WebSocket communication.\n\n");
        
        refreshBtn.addActionListener(e -> showConnectionStatus());
    }
    
    private void setupEventHandlers() {
        clearBtn.addActionListener(e -> logArea.setText(""));
    }
    
    private void showConnectionStatus() {
        logArea.append("\n=== Connection Status ===\n");
        String[] endpoints = {"/code", "/PMS", "/terminput", "/stop"};
        
        for (String endpoint : endpoints) {
            boolean connected = parent.isConnectedTo(endpoint);
            logArea.append(String.format("%-12s: %s\n", endpoint, connected ? "CONNECTED" : "DISCONNECTED"));
        }
        logArea.append("========================\n\n");
        logArea.setCaretPosition(logArea.getDocument().getLength());
    }
    
    @Override
    public void handleOutput(String endpoint, String message) {
        String timestamp = java.time.LocalTime.now().toString();
        logArea.append(String.format("[%s] %s: %s\n", timestamp, endpoint, message));
        logArea.setCaretPosition(logArea.getDocument().getLength());
    }
}
