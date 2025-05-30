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
        setTitle("KodeRunner Desktop Tester v2.4 - Hierarchical API Interface");
        setDefaultCloseOperation(JFrame.EXIT_ON_CLOSE);
        setSize(1600, 1000);
        setLocationRelativeTo(null);
        
        // Create main layout
        setLayout(new BorderLayout());
        
        // Create connection panel with enhanced endpoint selection
        JPanel connectionPanel = createEnhancedConnectionPanel();
        add(connectionPanel, BorderLayout.NORTH);
        
        // Create tabbed pane with updated tabs
        tabbedPane = new JTabbedPane();
        
        // Add endpoint selector as first tab
        tabbedPane.addTab("Endpoints", new EndpointSelectorPanel(this));
        
        // Main development environment
        tabbedPane.addTab("Development", new CodeEditorPanel(this));
        
        // Specialized tabs
        tabbedPane.addTab("Terminal Control", new TerminalInputPanel(this));
        tabbedPane.addTab("Shared Terminal", new SharedTerminalPanel(this));
        tabbedPane.addTab("System Monitor", new PerformanceMonitorPanel(this));
        tabbedPane.addTab("API Explorer", new ApiExplorerPanel(this));
        tabbedPane.addTab("Settings", new SettingsPanel(this));
        tabbedPane.addTab("About", new AboutPanel());
        
        add(tabbedPane, BorderLayout.CENTER);
        
        // Enhanced status bar
        statusLabel = new JLabel("Ready - Hierarchical WebSocket API v2.4");
        statusLabel.setBorder(BorderFactory.createEtchedBorder());
        add(statusLabel, BorderLayout.SOUTH);
    }
    
    private JPanel createEnhancedConnectionPanel() {
        JPanel panel = new JPanel(new FlowLayout(FlowLayout.LEFT));
        panel.setBorder(new TitledBorder("Server Connection"));
        
        panel.add(new JLabel("Host:"));
        hostField = new JTextField("localhost", 10);
        panel.add(hostField);
        
        panel.add(new JLabel("Port:"));
        portField = new JTextField("5000", 5);
        panel.add(portField);
        
        // Quick connect buttons for different endpoint groups
        JButton connectApiBtn = new JButton("API");
        JButton connectDevBtn = new JButton("Dev");
        JButton connectAdminBtn = new JButton("Admin");
        JButton connectLegacyBtn = new JButton("Legacy");
        JButton disconnectAllBtn = new JButton("Disconnect All");
        
        connectApiBtn.setToolTipText("Connect to API endpoints (/api/*)");
        connectDevBtn.setToolTipText("Connect to development endpoints (/dev/*)");
        connectAdminBtn.setToolTipText("Connect to admin endpoints (/admin/*)");
        connectLegacyBtn.setToolTipText("Connect to legacy endpoints for compatibility");
        
        panel.add(connectApiBtn);
        panel.add(connectDevBtn);
        panel.add(connectAdminBtn);
        panel.add(connectLegacyBtn);
        panel.add(disconnectAllBtn);
        
        // Add connection status indicator
        JLabel connectionStatus = new JLabel("●");
        connectionStatus.setForeground(Color.RED);
        connectionStatus.setToolTipText("Connection Status");
        panel.add(connectionStatus);
        
        // Event handlers for new buttons
        connectApiBtn.addActionListener(e -> connectToApiEndpoints());
        connectDevBtn.addActionListener(e -> connectToDevEndpoints());
        connectAdminBtn.addActionListener(e -> connectToAdminEndpoints());
        connectLegacyBtn.addActionListener(e -> connectToLegacyEndpoints());
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
        // Connect to new hierarchical endpoints
        connectToEndpoint("/api/execute");      // Enhanced PMS
        connectToEndpoint("/api/upload");       // Enhanced code upload
        connectToEndpoint("/api/status");       // Real-time status
        connectToEndpoint("/dev/syntax");       // Syntax highlighting
        connectToEndpoint("/terminal/input");   // Terminal input
        connectToEndpoint("/terminal/create");  // Terminal creation
        connectToEndpoint("/admin/system");     // System monitoring
        
        // Keep legacy endpoints for backward compatibility
        connectToEndpoint("/code");             // Legacy code upload
        connectToEndpoint("/PMS");              // Legacy project management
        connectToEndpoint("/stop");             // Process control
        
        updateStatus("Connected to new hierarchical endpoints - Enhanced API structure active");
    }
    
    public void connectToLegacyEndpoints() {
        // Original endpoint connections for compatibility
        connectToEndpoint("/code");
        connectToEndpoint("/PMS");
        connectToEndpoint("/terminput");
        connectToEndpoint("/stop");
        connectToEndpoint("/terminal/create");
        connectToEndpoint("/syntax");
        
        updateStatus("Connected to legacy endpoints - Backward compatibility mode");
    }
    
    public void connectToApiEndpoints() {
        // API-focused connections
        connectToEndpoint("/api/execute");
        connectToEndpoint("/api/build");
        connectToEndpoint("/api/upload");
        connectToEndpoint("/api/status");
        
        updateStatus("Connected to API endpoints - Production-ready interface");
    }
    
    public void connectToDevEndpoints() {
        // Development tool connections
        connectToEndpoint("/dev/syntax");
        connectToEndpoint("/dev/debug");
        connectToEndpoint("/dev/logs");
        connectToEndpoint("/dev/metrics");
        
        updateStatus("Connected to development endpoints - Enhanced debugging tools");
    }
    
    public void connectToAdminEndpoints() {
        // Administrative connections
        connectToEndpoint("/admin/system");
        connectToEndpoint("/admin/connections");
        connectToEndpoint("/admin/processes");
        
        updateStatus("Connected to admin endpoints - System management interface");
    }
    
    public void connectToCollabEndpoints() {
        // Collaboration connections
        connectToEndpoint("/collab/session");
        connectToEndpoint("/collab/sync");
        
        updateStatus("Connected to collaboration endpoints - Real-time coding collaboration");
    }
    
    public void connectToSandboxEndpoints() {
        // Sandbox connections
        connectToEndpoint("/sandbox/execute");
        connectToEndpoint("/sandbox/monitor");
        
        updateStatus("Connected to sandbox endpoints - Secure execution environment");
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
