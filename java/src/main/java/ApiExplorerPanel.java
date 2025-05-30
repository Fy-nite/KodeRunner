import javax.swing.*;
import javax.swing.border.TitledBorder;
import javax.swing.tree.DefaultMutableTreeNode;
import javax.swing.tree.DefaultTreeModel;
import java.awt.*;
import java.awt.event.MouseAdapter;
import java.awt.event.MouseEvent;
import java.util.HashMap;
import java.util.Map;

public class ApiExplorerPanel extends JPanel implements OutputHandler {
    private KodeRunnerTester mainApp;
    private JTree endpointTree;
    private JTextArea requestArea;
    private JTextArea responseArea;
    private JTextField selectedEndpointField;
    private JButton sendButton;
    private JComboBox<String> requestTemplateCombo;
    
    public ApiExplorerPanel(KodeRunnerTester mainApp) {
        this.mainApp = mainApp;
        initializeUI();
        setupEventHandlers();
    }
    
    private void initializeUI() {
        setLayout(new BorderLayout());
        
        // Left panel: Endpoint tree
        JPanel leftPanel = createEndpointTreePanel();
        
        // Right panel: Request/Response
        JPanel rightPanel = createRequestResponsePanel();
        
        JSplitPane splitPane = new JSplitPane(JSplitPane.HORIZONTAL_SPLIT, leftPanel, rightPanel);
        splitPane.setDividerLocation(400);
        
        add(splitPane, BorderLayout.CENTER);
        
        // Bottom panel: Controls
        JPanel controlPanel = createControlPanel();
        add(controlPanel, BorderLayout.SOUTH);
    }
    
    private JPanel createEndpointTreePanel() {
        JPanel panel = new JPanel(new BorderLayout());
        panel.setBorder(new TitledBorder("API Endpoints"));
        
        // Create hierarchical tree structure
        DefaultMutableTreeNode root = new DefaultMutableTreeNode("KodeRunner API");
        
        // API endpoints
        DefaultMutableTreeNode apiNode = new DefaultMutableTreeNode("API (/api/)");
        apiNode.add(new DefaultMutableTreeNode("/api/execute"));
        apiNode.add(new DefaultMutableTreeNode("/api/build"));
        apiNode.add(new DefaultMutableTreeNode("/api/upload"));
        apiNode.add(new DefaultMutableTreeNode("/api/status"));
        root.add(apiNode);
        
        // Development endpoints
        DefaultMutableTreeNode devNode = new DefaultMutableTreeNode("Development (/dev/)");
        devNode.add(new DefaultMutableTreeNode("/dev/syntax"));
        devNode.add(new DefaultMutableTreeNode("/dev/debug"));
        devNode.add(new DefaultMutableTreeNode("/dev/logs"));
        devNode.add(new DefaultMutableTreeNode("/dev/metrics"));
        root.add(devNode);
        
        // Terminal endpoints
        DefaultMutableTreeNode terminalNode = new DefaultMutableTreeNode("Terminal (/terminal/)");
        terminalNode.add(new DefaultMutableTreeNode("/terminal/create"));
        terminalNode.add(new DefaultMutableTreeNode("/terminal/input"));
        terminalNode.add(new DefaultMutableTreeNode("/terminal/shared/<id>"));
        root.add(terminalNode);
        
        // Admin endpoints
        DefaultMutableTreeNode adminNode = new DefaultMutableTreeNode("Administration (/admin/)");
        adminNode.add(new DefaultMutableTreeNode("/admin/system"));
        adminNode.add(new DefaultMutableTreeNode("/admin/connections"));
        adminNode.add(new DefaultMutableTreeNode("/admin/processes"));
        root.add(adminNode);
        
        // Collaboration endpoints
        DefaultMutableTreeNode collabNode = new DefaultMutableTreeNode("Collaboration (/collab/)");
        collabNode.add(new DefaultMutableTreeNode("/collab/session"));
        collabNode.add(new DefaultMutableTreeNode("/collab/sync"));
        root.add(collabNode);
        
        // Sandbox endpoints
        DefaultMutableTreeNode sandboxNode = new DefaultMutableTreeNode("Sandbox (/sandbox/)");
        sandboxNode.add(new DefaultMutableTreeNode("/sandbox/execute"));
        sandboxNode.add(new DefaultMutableTreeNode("/sandbox/monitor"));
        root.add(sandboxNode);
        
        // Legacy endpoints
        DefaultMutableTreeNode legacyNode = new DefaultMutableTreeNode("Legacy Endpoints");
        legacyNode.add(new DefaultMutableTreeNode("/code"));
        legacyNode.add(new DefaultMutableTreeNode("/PMS"));
        legacyNode.add(new DefaultMutableTreeNode("/stop"));
        legacyNode.add(new DefaultMutableTreeNode("/terminput"));
        legacyNode.add(new DefaultMutableTreeNode("/syntax"));
        root.add(legacyNode);
        
        endpointTree = new JTree(new DefaultTreeModel(root));
        endpointTree.setRootVisible(true);
        endpointTree.setShowsRootHandles(true);
        
        // Expand all nodes by default
        for (int i = 0; i < endpointTree.getRowCount(); i++) {
            endpointTree.expandRow(i);
        }
        
        JScrollPane treeScrollPane = new JScrollPane(endpointTree);
        panel.add(treeScrollPane, BorderLayout.CENTER);
        
        return panel;
    }
    
    private JPanel createRequestResponsePanel() {
        JPanel panel = new JPanel(new BorderLayout());
        
        // Request panel
        JPanel requestPanel = new JPanel(new BorderLayout());
        requestPanel.setBorder(new TitledBorder("Request"));
        
        requestArea = new JTextArea(10, 40);
        requestArea.setFont(new Font(Font.MONOSPACED, Font.PLAIN, 12));
        JScrollPane requestScrollPane = new JScrollPane(requestArea);
        requestPanel.add(requestScrollPane, BorderLayout.CENTER);
        
        // Response panel
        JPanel responsePanel = new JPanel(new BorderLayout());
        responsePanel.setBorder(new TitledBorder("Response"));
        
        responseArea = new JTextArea(10, 40);
        responseArea.setFont(new Font(Font.MONOSPACED, Font.PLAIN, 12));
        responseArea.setEditable(false);
        JScrollPane responseScrollPane = new JScrollPane(responseArea);
        responsePanel.add(responseScrollPane, BorderLayout.CENTER);
        
        JSplitPane splitPane = new JSplitPane(JSplitPane.VERTICAL_SPLIT, requestPanel, responsePanel);
        splitPane.setDividerLocation(200);
        
        panel.add(splitPane, BorderLayout.CENTER);
        
        return panel;
    }
    
    private JPanel createControlPanel() {
        JPanel panel = new JPanel(new FlowLayout(FlowLayout.LEFT));
        panel.setBorder(new TitledBorder("Controls"));
        
        panel.add(new JLabel("Endpoint:"));
        selectedEndpointField = new JTextField("/api/execute", 20);
        panel.add(selectedEndpointField);
        
        JButton connectBtn = new JButton("Connect");
        JButton disconnectBtn = new JButton("Disconnect");
        sendButton = new JButton("Send Request");
        JButton clearBtn = new JButton("Clear");
        
        panel.add(connectBtn);
        panel.add(disconnectBtn);
        panel.add(sendButton);
        panel.add(clearBtn);
        
        // Request templates
        panel.add(new JLabel("Template:"));
        requestTemplateCombo = new JComboBox<>(new String[]{
            "Empty", "PMS Execute", "Code Upload", "Terminal Input", "System Query"
        });
        panel.add(requestTemplateCombo);
        
        JButton loadTemplateBtn = new JButton("Load Template");
        panel.add(loadTemplateBtn);
        
        // Event handlers
        connectBtn.addActionListener(e -> {
            String endpoint = selectedEndpointField.getText().trim();
            if (!endpoint.isEmpty()) {
                mainApp.connectToEndpoint(endpoint);
            }
        });
        
        disconnectBtn.addActionListener(e -> {
            String endpoint = selectedEndpointField.getText().trim();
            if (!endpoint.isEmpty()) {
                mainApp.disconnectFromEndpoint(endpoint);
            }
        });
        
        sendButton.addActionListener(e -> sendRequest());
        clearBtn.addActionListener(e -> {
            requestArea.setText("");
            responseArea.setText("");
        });
        
        loadTemplateBtn.addActionListener(e -> loadRequestTemplate());
        
        return panel;
    }
    
    private void setupEventHandlers() {
        // Tree selection handler
        endpointTree.addTreeSelectionListener(e -> {
            DefaultMutableTreeNode node = (DefaultMutableTreeNode) endpointTree.getLastSelectedPathComponent();
            if (node != null && node.isLeaf()) {
                String endpoint = node.toString();
                if (endpoint.startsWith("/")) {
                    selectedEndpointField.setText(endpoint);
                    loadDefaultRequest(endpoint);
                }
            }
        });
        
        // Double-click to connect
        endpointTree.addMouseListener(new MouseAdapter() {
            @Override
            public void mouseClicked(MouseEvent e) {
                if (e.getClickCount() == 2) {
                    DefaultMutableTreeNode node = (DefaultMutableTreeNode) endpointTree.getLastSelectedPathComponent();
                    if (node != null && node.isLeaf()) {
                        String endpoint = node.toString();
                        if (endpoint.startsWith("/") && !endpoint.contains("<id>")) {
                            mainApp.connectToEndpoint(endpoint);
                        }
                    }
                }
            }
        });
    }
    
    private void loadDefaultRequest(String endpoint) {
        String defaultRequest = switch (endpoint) {
            case "/api/execute", "/PMS" -> "{\n" +
                "  \"PMS_System\": \"1.2.0\",\n" +
                "  \"Project_Name\": \"test_project\",\n" +
                "  \"Main_File\": \"main.py\",\n" +
                "  \"Project_Build_Systems\": \"python\",\n" +
                "  \"Project_Output\": \"output\",\n" +
                "  \"Run_On_Build\": \"True\"\n" +
                "}";
            case "/terminal/input", "/terminput" -> "print('Hello from terminal input!')";
            case "/stop" -> "{\n" +
                "  \"stopped\": true\n" +
                "}";
            case "/dev/syntax", "/syntax" -> "lang:python\n" +
                "def hello_world():\n" +
                "    print('Hello, World!')\n" +
                "    return 42";
            case "/admin/system" -> "refresh";
            default -> "";
        };
        
        requestArea.setText(defaultRequest);
    }
    
    private void loadRequestTemplate() {
        String template = (String) requestTemplateCombo.getSelectedItem();
        String content = switch (template) {
            case "PMS Execute" -> "{\n" +
                "  \"PMS_System\": \"1.2.0\",\n" +
                "  \"Project_Name\": \"example_project\",\n" +
                "  \"Main_File\": \"main.py\",\n" +
                "  \"Project_Build_Systems\": \"python\",\n" +
                "  \"Project_Output\": \"output\",\n" +
                "  \"Run_On_Build\": \"True\"\n" +
                "}";
            case "Code Upload" -> "# File_name: test.py\n" +
                "# Project: example_project\n" +
                "\n" +
                "def main():\n" +
                "    print('Hello from uploaded code!')\n" +
                "\n" +
                "if __name__ == '__main__':\n" +
                "    main()";
            case "Terminal Input" -> "echo 'Hello from terminal!'";
            case "System Query" -> "status";
            default -> "";
        };
        
        requestArea.setText(content);
    }
    
    private void sendRequest() {
        String endpoint = selectedEndpointField.getText().trim();
        String request = requestArea.getText();
        
        if (endpoint.isEmpty()) {
            responseArea.append("[Error] No endpoint selected\n\n");
            return;
        }
        
        if (!mainApp.isConnectedTo(endpoint)) {
            responseArea.append("[Error] Not connected to " + endpoint + "\n\n");
            return;
        }
        
        try {
            mainApp.sendToEndpoint(endpoint, request);
            responseArea.append("[Sent to " + endpoint + "] " + 
                (request.length() > 100 ? request.substring(0, 97) + "..." : request) + "\n\n");
        } catch (Exception e) {
            responseArea.append("[Error] Failed to send: " + e.getMessage() + "\n\n");
        }
    }
    
    @Override
    public void handleOutput(String endpoint, String message) {
        SwingUtilities.invokeLater(() -> {
            responseArea.append("[" + endpoint + "] " + message + "\n\n");
            responseArea.setCaretPosition(responseArea.getDocument().getLength());
        });
    }
}
