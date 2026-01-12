import javax.swing.*;
import javax.swing.border.TitledBorder;
import java.awt.*;
import java.awt.event.ActionEvent;
import java.awt.event.ActionListener;
import java.util.HashMap;
import java.util.Map;

public class EndpointSelectorPanel extends JPanel {
    private KodeRunnerTester mainApp;
    private JComboBox<EndpointGroup> endpointGroupCombo;
    private JTextArea endpointListArea;
    private JButton connectGroupBtn;
    private JButton disconnectGroupBtn;
    private JCheckBox autoConnectCheck;
    
    private enum EndpointGroup {
        ALL("All Endpoints", "Connect to all available endpoints"),
        API("API Endpoints", "Production-ready API interface"),
        DEV("Development Tools", "Debugging and development tools"),
        ADMIN("Administration", "System management interface"),
        TERMINAL("Terminal Services", "Terminal and process management"),
        COLLAB("Collaboration", "Real-time collaboration features"),
        SANDBOX("Sandbox", "Secure execution environment"),
        LEGACY("Legacy Endpoints", "Backward compatibility endpoints");
        
        private final String displayName;
        private final String description;
        
        EndpointGroup(String displayName, String description) {
            this.displayName = displayName;
            this.description = description;
        }
        
        @Override
        public String toString() {
            return displayName;
        }
        
        public String getDescription() {
            return description;
        }
    }
    
    public EndpointSelectorPanel(KodeRunnerTester mainApp) {
        this.mainApp = mainApp;
        initializeUI();
        setupEventHandlers();
    }
    
    private void initializeUI() {
        setLayout(new BorderLayout());
        setBorder(new TitledBorder("Endpoint Management"));
        
        // Top panel with controls
        JPanel controlPanel = new JPanel(new FlowLayout(FlowLayout.LEFT));
        
        controlPanel.add(new JLabel("Endpoint Group:"));
        endpointGroupCombo = new JComboBox<>(EndpointGroup.values());
        endpointGroupCombo.setSelectedItem(EndpointGroup.ALL);
        controlPanel.add(endpointGroupCombo);
        
        connectGroupBtn = new JButton("Connect Group");
        disconnectGroupBtn = new JButton("Disconnect Group");
        controlPanel.add(connectGroupBtn);
        controlPanel.add(disconnectGroupBtn);
        
        autoConnectCheck = new JCheckBox("Auto-connect on startup", false);
        controlPanel.add(autoConnectCheck);
        
        add(controlPanel, BorderLayout.NORTH);
        
        // Center panel with endpoint details
        JPanel centerPanel = new JPanel(new BorderLayout());
        centerPanel.setBorder(new TitledBorder("Endpoints in Selected Group"));
        
        endpointListArea = new JTextArea(10, 40);
        endpointListArea.setEditable(false);
        endpointListArea.setFont(new Font(Font.MONOSPACED, Font.PLAIN, 12));
        
        JScrollPane scrollPane = new JScrollPane(endpointListArea);
        centerPanel.add(scrollPane, BorderLayout.CENTER);
        
        add(centerPanel, BorderLayout.CENTER);
        
        // Bottom panel with individual endpoint controls
        JPanel bottomPanel = createIndividualEndpointPanel();
        add(bottomPanel, BorderLayout.SOUTH);
        
        // Update display for initial selection
        updateEndpointDisplay();
    }
    
    private JPanel createIndividualEndpointPanel() {
        JPanel panel = new JPanel(new BorderLayout());
        panel.setBorder(new TitledBorder("Individual Endpoint Control"));
        
        JPanel inputPanel = new JPanel(new FlowLayout(FlowLayout.LEFT));
        inputPanel.add(new JLabel("Custom Endpoint:"));
        
        JTextField customEndpointField = new JTextField("/api/execute", 20);
        inputPanel.add(customEndpointField);
        
        JButton connectCustomBtn = new JButton("Connect");
        JButton disconnectCustomBtn = new JButton("Disconnect");
        
        connectCustomBtn.addActionListener(e -> {
            String endpoint = customEndpointField.getText().trim();
            if (!endpoint.isEmpty()) {
                mainApp.connectToEndpoint(endpoint);
            }
        });
        
        disconnectCustomBtn.addActionListener(e -> {
            String endpoint = customEndpointField.getText().trim();
            if (!endpoint.isEmpty()) {
                mainApp.disconnectFromEndpoint(endpoint);
            }
        });
        
        inputPanel.add(connectCustomBtn);
        inputPanel.add(disconnectCustomBtn);
        
        panel.add(inputPanel, BorderLayout.NORTH);
        
        // Quick connect buttons for common endpoints
        JPanel quickConnectPanel = new JPanel(new FlowLayout(FlowLayout.LEFT));
        quickConnectPanel.add(new JLabel("Quick Connect:"));
        
        String[] commonEndpoints = {
            "/api/execute", "/api/status", "/dev/syntax", "/terminal/input", 
            "/admin/system", "/collab/session", "/sandbox/execute"
        };
        
        for (String endpoint : commonEndpoints) {
            JButton btn = new JButton(endpoint.substring(endpoint.lastIndexOf('/') + 1));
            btn.setToolTipText("Connect to " + endpoint);
            btn.addActionListener(e -> mainApp.connectToEndpoint(endpoint));
            quickConnectPanel.add(btn);
        }
        
        panel.add(quickConnectPanel, BorderLayout.CENTER);
        
        return panel;
    }
    
    private void setupEventHandlers() {
        connectGroupBtn.addActionListener(e -> connectSelectedGroup());
        disconnectGroupBtn.addActionListener(e -> disconnectSelectedGroup());
        
        endpointGroupCombo.addActionListener(e -> updateEndpointDisplay());
        
        autoConnectCheck.addActionListener(e -> {
            // Save auto-connect preference
            AppConfig config = mainApp.getConfigManager().loadConfig();
            config.setAutoConnect(autoConnectCheck.isSelected());
            mainApp.getConfigManager().saveConfig(config);
        });
    }
    
    private void updateEndpointDisplay() {
        EndpointGroup selected = (EndpointGroup) endpointGroupCombo.getSelectedItem();
        if (selected == null) return;
        
        StringBuilder sb = new StringBuilder();
        sb.append("Group: ").append(selected.toString()).append("\n");
        sb.append("Description: ").append(selected.getDescription()).append("\n\n");
        sb.append("Endpoints:\n");
        
        String[] endpoints = getEndpointsForGroup(selected);
        for (String endpoint : endpoints) {
            sb.append("  ").append(endpoint).append("\n");
        }
        
        endpointListArea.setText(sb.toString());
    }
    
    private String[] getEndpointsForGroup(EndpointGroup group) {
        return switch (group) {
            case ALL -> new String[] {
                "/api/execute", "/api/build", "/api/upload", "/api/status",
                "/dev/syntax", "/dev/debug", "/dev/logs", "/dev/metrics",
                "/terminal/create", "/terminal/input", "/terminal/shared/<id>",
                "/admin/system", "/admin/connections", "/admin/processes",
                "/collab/session", "/collab/sync",
                "/sandbox/execute", "/sandbox/monitor"
            };
            case API -> new String[] {
                "/api/execute", "/api/build", "/api/upload", "/api/status"
            };
            case DEV -> new String[] {
                "/dev/syntax", "/dev/debug", "/dev/logs", "/dev/metrics"
            };
            case ADMIN -> new String[] {
                "/admin/system", "/admin/connections", "/admin/processes"
            };
            case TERMINAL -> new String[] {
                "/terminal/create", "/terminal/input", "/terminal/shared/<id>"
            };
            case COLLAB -> new String[] {
                "/collab/session", "/collab/sync"
            };
            case SANDBOX -> new String[] {
                "/sandbox/execute", "/sandbox/monitor"
            };
            case LEGACY -> new String[] {
                "/code", "/PMS", "/stop", "/terminput", "/syntax", "/syntax/lang", "/system/*"
            };
        };
    }
    
    private void connectSelectedGroup() {
        EndpointGroup selected = (EndpointGroup) endpointGroupCombo.getSelectedItem();
        if (selected == null) return;
        
        switch (selected) {
            case ALL -> mainApp.connectToAllEndpoints();
            case API -> mainApp.connectToApiEndpoints();
            case DEV -> mainApp.connectToDevEndpoints();
            case ADMIN -> mainApp.connectToAdminEndpoints();
            case TERMINAL -> {
                mainApp.connectToEndpoint("/terminal/create");
                mainApp.connectToEndpoint("/terminal/input");
            }
            case COLLAB -> mainApp.connectToCollabEndpoints();
            case SANDBOX -> mainApp.connectToSandboxEndpoints();
            case LEGACY -> mainApp.connectToLegacyEndpoints();
        }
    }
    
    private void disconnectSelectedGroup() {
        EndpointGroup selected = (EndpointGroup) endpointGroupCombo.getSelectedItem();
        if (selected == null) return;
        
        String[] endpoints = getEndpointsForGroup(selected);
        for (String endpoint : endpoints) {
            if (!endpoint.contains("<id>") && !endpoint.contains("*")) {
                mainApp.disconnectFromEndpoint(endpoint);
            }
        }
    }
    
    public void setAutoConnect(boolean autoConnect) {
        autoConnectCheck.setSelected(autoConnect);
    }
}
