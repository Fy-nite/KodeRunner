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
        setTitle("KodeRunner Desktop Tester v2.0 - Enhanced Language Support");
        setDefaultCloseOperation(JFrame.EXIT_ON_CLOSE);
        setSize(1400, 900); // Slightly larger to accommodate new features
        setLocationRelativeTo(null);
        
        // Create main layout
        setLayout(new BorderLayout());
        
        // Create connection panel
        JPanel connectionPanel = createConnectionPanel();
        add(connectionPanel, BorderLayout.NORTH);
        
        // Create tabbed pane
        tabbedPane = new JTabbedPane();
        
        // Add tabs for different endpoints
        tabbedPane.addTab("Code Editor", new CodeEditorPanel(this));
        tabbedPane.addTab("PMS Testing", new PMSTestPanel(this));
        tabbedPane.addTab("Terminal Input", new TerminalInputPanel(this));
        tabbedPane.addTab("Process Control", new ProcessControlPanel(this));
        tabbedPane.addTab("Connection Monitor", new ConnectionMonitorPanel(this));
        tabbedPane.addTab("Settings", new SettingsPanel(this));
        tabbedPane.addTab("About", new AboutPanel());
        
        add(tabbedPane, BorderLayout.CENTER);
        
        // Status bar
        statusLabel = new JLabel("Ready - Enhanced with multi-language support");
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
    
    private void connectToAllEndpoints() {
        connectToEndpoint("/code");
        connectToEndpoint("/PMS");
        connectToEndpoint("/terminput");
        connectToEndpoint("/stop");
    }
    
    private void disconnectFromAllEndpoints() {
        for (String endpoint : connections.keySet()) {
            disconnectFromEndpoint(endpoint);
        }
    }
    
    public void sendToEndpoint(String endpoint, String message) {
        WebSocketClient client = connections.get(endpoint);
        if (client != null && client.isOpen()) {
            try {
                client.send(message);
            } catch (Exception e) {
                updateStatus("Error sending to " + endpoint + ": " + e.getMessage());
            }
        } else {
            updateStatus("Not connected to " + endpoint);
        }
    }
    
    private void handleWebSocketOutput(String endpoint, String message) {
        SwingUtilities.invokeLater(() -> {
            // Forward to appropriate panel
            Component selectedComponent = tabbedPane.getSelectedComponent();
            if (selectedComponent instanceof OutputHandler) {
                ((OutputHandler) selectedComponent).handleOutput(endpoint, message);
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
}
