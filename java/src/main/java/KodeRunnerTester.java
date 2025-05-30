import javax.swing.*;
import javax.swing.border.TitledBorder;
import java.awt.*;
import java.awt.event.ActionEvent;
import java.awt.event.ActionListener;
import java.io.IOException;
import java.net.URI;
import java.util.HashMap;
import java.util.Map;

public class KodeRunnerTester extends JFrame {
    private JTabbedPane tabbedPane;
    private Map<String, WebSocketClient> connections;
    private JTextField hostField;
    private JTextField portField;
    private JLabel statusLabel;
    private ConfigManager configManager;
    
    public KodeRunnerTester() {
        connections = new HashMap<>();
        configManager = new ConfigManager();
        initializeUI();
        setupEventHandlers();
        loadInitialConfig();
    }
    
    private void initializeUI() {
        setTitle("KodeRunner Desktop Tester v2.3 - Unified Development Environment");
        setDefaultCloseOperation(JFrame.EXIT_ON_CLOSE);
        setSize(1600, 1000); // Wider for the new layout
        setLocationRelativeTo(null);
        
        // Create main layout
        setLayout(new BorderLayout());
        
        // Create connection panel
        JPanel connectionPanel = createConnectionPanel();
        add(connectionPanel, BorderLayout.NORTH);
        
        // Create tabbed pane with consolidated tabs
        tabbedPane = new JTabbedPane();
        
        // Main development environment (consolidates Code Editor, PMS, Project Manager)
        tabbedPane.addTab("Development", new CodeEditorPanel(this));
        
        // Keep essential specialized tabs
        tabbedPane.addTab("Terminal Input", new TerminalInputPanel(this));
        tabbedPane.addTab("Shared Terminal", new SharedTerminalPanel(this));
        tabbedPane.addTab("Performance Monitor", new PerformanceMonitorPanel(this));
        tabbedPane.addTab("Connection Monitor", new ConnectionMonitorPanel(this));
        tabbedPane.addTab("Settings", new SettingsPanel(this));
        tabbedPane.addTab("About", new AboutPanel());
        
        add(tabbedPane, BorderLayout.CENTER);
        
        // Status bar
        statusLabel = new JLabel("Ready - Unified development environment for KodeRunner");
        statusLabel.setBorder(BorderFactory.createEtchedBorder());
        add(statusLabel, BorderLayout.SOUTH);
    }
    
    private JPanel createConnectionPanel() {
        JPanel panel = new JPanel(new FlowLayout(FlowLayout.LEFT));
        panel.setBorder(new TitledBorder("Connection Settings"));
        
        panel.add(new JLabel("Host:"));
        hostField = new JTextField("localhost", 10);
        panel.add(hostField);
        
        panel.add(new JLabel("Port:"));
        portField = new JTextField("5000", 5);
        panel.add(portField);
        
        JButton connectAllBtn = new JButton("Connect All");
        JButton disconnectAllBtn = new JButton("Disconnect All");
        
        panel.add(connectAllBtn);
        panel.add(disconnectAllBtn);
        
        connectAllBtn.addActionListener(e -> connectToAllEndpoints());
        disconnectAllBtn.addActionListener(e -> disconnectFromAllEndpoints());
        
        return panel;
    }
    
    private void setupEventHandlers() {
        // Add any global event handlers here
    }
    
    private void loadInitialConfig() {
        AppConfig config = configManager.loadConfig();
        
        // Set default server values
        String[] serverParts = config.getDefaultServer().replace("ws://", "").split(":");
        if (serverParts.length >= 2) {
            hostField.setText(serverParts[0]);
            portField.setText(serverParts[1]);
        }
        
        // Apply look and feel
        try {
            UIManager.LookAndFeelInfo[] lafs = UIManager.getInstalledLookAndFeels();
            for (UIManager.LookAndFeelInfo laf : lafs) {
                if (laf.getName().equals(config.getLookAndFeel())) {
                    UIManager.setLookAndFeel(laf.getClassName());
                    SwingUtilities.updateComponentTreeUI(this);
                    break;
                }
            }
        } catch (Exception e) {
            updateStatus("Error applying saved Look and Feel: " + e.getMessage());
        }
        
        // Auto-connect if enabled
        if (config.isAutoConnect()) {
            SwingUtilities.invokeLater(this::connectToAllEndpoints);
        }
    }
    
    public ConfigManager getConfigManager() {
        return configManager;
    }
    
    public void connectToEndpoint(String endpoint) {
        try {
            String host = hostField.getText();
            String port = portField.getText();
            String url = String.format("ws://%s:%s%s", host, port, endpoint);
            
            WebSocketClient client = new WebSocketClient(URI.create(url));
            client.setOutputHandler(this::handleWebSocketOutput);
            client.connect();
            
            connections.put(endpoint, client);
            updateStatus("Connected to " + endpoint);
            
        } catch (Exception e) {
            updateStatus("Failed to connect to " + endpoint + ": " + e.getMessage());
        }
    }
    
    public void disconnectFromEndpoint(String endpoint) {
        WebSocketClient client = connections.get(endpoint);
        if (client != null) {
            try {
                client.close();
                connections.remove(endpoint);
                updateStatus("Disconnected from " + endpoint);
            } catch (Exception e) {
                updateStatus("Error disconnecting from " + endpoint + ": " + e.getMessage());
            }
        }
    }
    
    public void connectToAllEndpoints() {
        connectToEndpoint("/code");
        connectToEndpoint("/PMS");
        connectToEndpoint("/terminput");
        connectToEndpoint("/stop");
        connectToEndpoint("/terminal/create");
        connectToEndpoint("/syntax");  // Add syntax highlighting endpoint
        
        // Update status to reflect terminal integration
        updateStatus("Connected to all endpoints - Terminal input integrated in Development tab");
    }
    
    public void disconnectFromAllEndpoints() {
        for (String endpoint : new HashMap<>(connections).keySet()) {
            disconnectFromEndpoint(endpoint);
        }
        updateStatus("Disconnected from all endpoints");
    }
    
    public void sendToEndpoint(String endpoint, String message) {
        WebSocketClient client = connections.get(endpoint);
        if (client != null && client.isOpen()) {
            try {
                client.send(message);
                // Log the send operation for debugging
                System.out.println("[SEND] " + endpoint + ": " + 
                    (message.length() > 100 ? message.substring(0, 97) + "..." : message));
            } catch (Exception e) {
                updateStatus("Error sending to " + endpoint + ": " + e.getMessage());
            }
        } else {
            updateStatus("Not connected to " + endpoint);
        }
    }
    
    private void handleWebSocketOutput(String endpoint, String message) {
        SwingUtilities.invokeLater(() -> {
            // Get the currently selected tab
            Component selectedComponent = tabbedPane.getSelectedComponent();
            
            // Only forward to the currently selected tab if it handles output
            if (selectedComponent instanceof OutputHandler) {
                ((OutputHandler) selectedComponent).handleOutput(endpoint, message);
            }
            
            // Always forward to Connection Monitor and Performance Monitor for logging
            for (int i = 0; i < tabbedPane.getTabCount(); i++) {
                Component tabComponent = tabbedPane.getComponentAt(i);
                String tabTitle = tabbedPane.getTitleAt(i);
                
                // Forward to monitoring tabs regardless of selection
                if (tabComponent instanceof OutputHandler && 
                    (tabTitle.equals("Connection Monitor") || tabTitle.equals("Performance Monitor"))) {
                    ((OutputHandler) tabComponent).handleOutput(endpoint, message);
                }
            }
        });
    }
    
    public void updateStatus(String message) {
        SwingUtilities.invokeLater(() -> {
            statusLabel.setText(message);
            System.out.println("[Status] " + message);
        });
    }
    
    public boolean isConnectedTo(String endpoint) {
        WebSocketClient client = connections.get(endpoint);
        return client != null && client.isOpen();
    }
    
    public void connectToDynamicEndpoint(String endpoint) {
        try {
            String host = hostField.getText();
            String port = portField.getText();
            String url = String.format("ws://%s:%s%s", host, port, endpoint);
            
            WebSocketClient client = new WebSocketClient(URI.create(url));
            client.setOutputHandler((ep, msg) -> handleWebSocketOutput(endpoint, msg));
            client.connect();
            
            connections.put(endpoint, client);
            updateStatus("Connected to dynamic endpoint: " + endpoint);
            
        } catch (Exception e) {
            updateStatus("Failed to connect to " + endpoint + ": " + e.getMessage());
        }
    }
    
    // Override window closing to cleanup resources
    @Override
    protected void processWindowEvent(java.awt.event.WindowEvent e) {
        if (e.getID() == java.awt.event.WindowEvent.WINDOW_CLOSING) {
            // Cleanup performance monitor
            for (int i = 0; i < tabbedPane.getTabCount(); i++) {
                Component component = tabbedPane.getComponentAt(i);
                if (component instanceof PerformanceMonitorPanel) {
                    ((PerformanceMonitorPanel) component).cleanup();
                }
            }
            
            // Disconnect all connections
            disconnectFromAllEndpoints();
        }
        super.processWindowEvent(e);
    }
}
