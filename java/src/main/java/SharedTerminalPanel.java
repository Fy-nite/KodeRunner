import javax.swing.*;
import javax.swing.border.TitledBorder;
import java.awt.*;
import java.awt.event.KeyAdapter;
import java.awt.event.KeyEvent;
import com.google.gson.Gson;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import java.util.HashMap;
import java.util.Map;

public class SharedTerminalPanel extends JPanel implements OutputHandler {
    private KodeRunnerTester parent;
    private JTextField projectNameField;
    private JButton createSessionBtn;
    private JButton connectSessionBtn;
    private JTextField sessionIdField;
    private JTextArea terminalOutput;
    private JTextField terminalInput;
    private JButton sendInputBtn;
    private JButton disconnectBtn;
    private JLabel statusLabel;
    private Gson gson;
    private String currentSessionEndpoint = null;
    private String currentSessionId = null;
    
    public SharedTerminalPanel(KodeRunnerTester parent) {
        this.parent = parent;
        this.gson = new Gson();
        initializeUI();
        setupEventHandlers();
    }
    
    private void initializeUI() {
        setLayout(new BorderLayout());
        
        // Create control panel
        JPanel controlPanel = createControlPanel();
        add(controlPanel, BorderLayout.NORTH);
        
        // Create terminal area
        JPanel terminalPanel = createTerminalPanel();
        add(terminalPanel, BorderLayout.CENTER);
        
        // Status bar
        statusLabel = new JLabel("Not connected to any shared terminal session");
        statusLabel.setBorder(BorderFactory.createEtchedBorder());
        add(statusLabel, BorderLayout.SOUTH);
    }
    
    private JPanel createControlPanel() {
        JPanel mainPanel = new JPanel(new BorderLayout());
        
        // Session creation panel
        JPanel createPanel = new JPanel(new FlowLayout(FlowLayout.LEFT));
        createPanel.setBorder(new TitledBorder("Create Session"));
        
        createPanel.add(new JLabel("Project Name:"));
        projectNameField = new JTextField("TestProject", 15);
        createPanel.add(projectNameField);
        
        createSessionBtn = new JButton("Create Shared Session");
        createSessionBtn.setBackground(new Color(46, 125, 50));
        createSessionBtn.setForeground(Color.WHITE);
        createPanel.add(createSessionBtn);
        
        mainPanel.add(createPanel, BorderLayout.NORTH);
        
        // Session connection panel
        JPanel connectPanel = new JPanel(new FlowLayout(FlowLayout.LEFT));
        connectPanel.setBorder(new TitledBorder("Connect to Session"));
        
        connectPanel.add(new JLabel("Session Endpoint:"));
        sessionIdField = new JTextField("/terminal/example-abc123", 20);
        connectPanel.add(sessionIdField);
        
        connectSessionBtn = new JButton("Connect to Session");
        connectSessionBtn.setBackground(new Color(33, 150, 243));
        connectSessionBtn.setForeground(Color.WHITE);
        connectPanel.add(connectSessionBtn);
        
        disconnectBtn = new JButton("Disconnect");
        disconnectBtn.setBackground(Color.RED);
        disconnectBtn.setForeground(Color.WHITE);
        disconnectBtn.setEnabled(false);
        connectPanel.add(disconnectBtn);
        
        mainPanel.add(connectPanel, BorderLayout.CENTER);
        
        // Connection status panel
        JPanel statusPanel = new JPanel(new FlowLayout(FlowLayout.LEFT));
        JButton connectCreatorBtn = new JButton("Connect to Terminal Creator");
        statusPanel.add(connectCreatorBtn);
        
        JButton sendPingBtn = new JButton("Send Ping");
        statusPanel.add(sendPingBtn);
        
        mainPanel.add(statusPanel, BorderLayout.SOUTH);
        
        // Add event handlers for new buttons
        connectCreatorBtn.addActionListener(e -> parent.connectToEndpoint("/terminal/create"));
        sendPingBtn.addActionListener(e -> sendPing());
        
        return mainPanel;
    }
    
    private JPanel createTerminalPanel() {
        JPanel panel = new JPanel(new BorderLayout());
        
        // Terminal output
        terminalOutput = new JTextArea();
        terminalOutput.setEditable(false);
        terminalOutput.setFont(new Font(Font.MONOSPACED, Font.PLAIN, 12));
        terminalOutput.setBackground(Color.BLACK);
        terminalOutput.setForeground(Color.GREEN);
        terminalOutput.setText("Shared Terminal Output\n" +
                              "======================\n" +
                              "Create or connect to a shared terminal session to begin.\n\n");
        
        JScrollPane outputScroll = new JScrollPane(terminalOutput);
        outputScroll.setBorder(new TitledBorder("Terminal Output"));
        outputScroll.setPreferredSize(new Dimension(600, 400));
        
        panel.add(outputScroll, BorderLayout.CENTER);
        
        // Terminal input
        JPanel inputPanel = new JPanel(new BorderLayout());
        inputPanel.setBorder(new TitledBorder("Terminal Input"));
        
        terminalInput = new JTextField();
        terminalInput.setFont(new Font(Font.MONOSPACED, Font.PLAIN, 12));
        terminalInput.setEnabled(false);
        sendInputBtn = new JButton("Send");
        sendInputBtn.setEnabled(false);
        
        inputPanel.add(terminalInput, BorderLayout.CENTER);
        inputPanel.add(sendInputBtn, BorderLayout.EAST);
        
        panel.add(inputPanel, BorderLayout.SOUTH);
        
        return panel;
    }
    
    private void setupEventHandlers() {
        createSessionBtn.addActionListener(e -> createSharedSession());
        connectSessionBtn.addActionListener(e -> connectToSession());
        disconnectBtn.addActionListener(e -> disconnectFromSession());
        sendInputBtn.addActionListener(e -> sendTerminalInput());
        
        // Enter key in terminal input
        terminalInput.addKeyListener(new KeyAdapter() {
            @Override
            public void keyPressed(KeyEvent e) {
                if (e.getKeyCode() == KeyEvent.VK_ENTER) {
                    sendTerminalInput();
                }
            }
        });
    }
    
    private void createSharedSession() {
        if (!parent.isConnectedTo("/terminal/create")) {
            parent.updateStatus("Not connected to /terminal/create endpoint");
            terminalOutput.append("[ERROR] Not connected to terminal creator endpoint\n");
            return;
        }
        
        String projectName = projectNameField.getText().trim();
        if (projectName.isEmpty()) {
            JOptionPane.showMessageDialog(this, "Please enter a project name.", "Missing Project Name", JOptionPane.WARNING_MESSAGE);
            return;
        }
        
        // Send session creation request
        Map<String, String> request = new HashMap<>();
        request.put("action", "create");
        request.put("project", projectName);
        
        String json = gson.toJson(request);
        parent.sendToEndpoint("/terminal/create", json);
        
        terminalOutput.append("[CREATE] Requesting shared terminal session for project: " + projectName + "\n");
        terminalOutput.setCaretPosition(terminalOutput.getDocument().getLength());
        parent.updateStatus("Requesting shared terminal session...");
    }
    
    private void connectToSession() {
        String endpoint = sessionIdField.getText().trim();
        if (endpoint.isEmpty()) {
            JOptionPane.showMessageDialog(this, "Please enter a session endpoint.", "Missing Endpoint", JOptionPane.WARNING_MESSAGE);
            return;
        }
        
        if (!endpoint.startsWith("/")) {
            endpoint = "/" + endpoint;
        }
        
        try {
            parent.connectToDynamicEndpoint(endpoint);
            currentSessionEndpoint = endpoint;
            currentSessionId = extractSessionId(endpoint);
            
            // Enable terminal input
            terminalInput.setEnabled(true);
            sendInputBtn.setEnabled(true);
            disconnectBtn.setEnabled(true);
            connectSessionBtn.setEnabled(false);
            
            terminalOutput.append("[CONNECT] Connected to shared terminal: " + endpoint + "\n");
            terminalOutput.setCaretPosition(terminalOutput.getDocument().getLength());
            updateStatus("Connected to " + endpoint);
            
        } catch (Exception e) {
            terminalOutput.append("[ERROR] Failed to connect to " + endpoint + ": " + e.getMessage() + "\n");
            parent.updateStatus("Failed to connect to session: " + e.getMessage());
        }
    }
    
    private void disconnectFromSession() {
        if (currentSessionEndpoint != null) {
            parent.disconnectFromEndpoint(currentSessionEndpoint);
            
            // Disable terminal input
            terminalInput.setEnabled(false);
            sendInputBtn.setEnabled(false);
            disconnectBtn.setEnabled(false);
            connectSessionBtn.setEnabled(true);
            
            terminalOutput.append("[DISCONNECT] Disconnected from " + currentSessionEndpoint + "\n");
            terminalOutput.setCaretPosition(terminalOutput.getDocument().getLength());
            updateStatus("Disconnected from session");
            
            currentSessionEndpoint = null;
            currentSessionId = null;
        }
    }
    
    private void sendTerminalInput() {
        if (currentSessionEndpoint == null || !parent.isConnectedTo(currentSessionEndpoint)) {
            terminalOutput.append("[ERROR] Not connected to any session\n");
            return;
        }
        
        String input = terminalInput.getText();
        if (input.isEmpty()) {
            return;
        }
        
        // Create JSON message for structured input
        Map<String, Object> message = new HashMap<>();
        message.put("type", "input");
        message.put("data", input);
        message.put("timestamp", System.currentTimeMillis());
        
        String json = gson.toJson(message);
        parent.sendToEndpoint(currentSessionEndpoint, json);
        
        terminalOutput.append("[INPUT] " + input + "\n");
        terminalOutput.setCaretPosition(terminalOutput.getDocument().getLength());
        terminalInput.setText("");
    }
    
    private void sendPing() {
        if (currentSessionEndpoint == null || !parent.isConnectedTo(currentSessionEndpoint)) {
            terminalOutput.append("[ERROR] Not connected to any session\n");
            return;
        }
        
        Map<String, Object> ping = new HashMap<>();
        ping.put("type", "ping");
        ping.put("timestamp", System.currentTimeMillis());
        
        String json = gson.toJson(ping);
        parent.sendToEndpoint(currentSessionEndpoint, json);
        
        terminalOutput.append("[PING] Sent ping to session\n");
        terminalOutput.setCaretPosition(terminalOutput.getDocument().getLength());
    }
    
    private String extractSessionId(String endpoint) {
        // Extract session ID from endpoint like "/terminal/project-abc123"
        if (endpoint.startsWith("/terminal/")) {
            return endpoint.substring("/terminal/".length());
        }
        return endpoint;
    }
    
    private void updateStatus(String message) {
        statusLabel.setText(message);
        parent.updateStatus(message);
    }
    
    @Override
    public void handleOutput(String endpoint, String message) {
        SwingUtilities.invokeLater(() -> {
            try {
                // Only handle messages relevant to this panel
                if ("/terminal/create".equals(endpoint)) {
                    // Handle terminal creation response
                    JsonObject response = JsonParser.parseString(message).getAsJsonObject();
                    if (response.has("success") && response.get("success").getAsBoolean()) {
                        String createdEndpoint = response.get("endpoint").getAsString();
                        String sessionId = response.get("sessionId").getAsString();
                        
                        terminalOutput.append("[CREATED] Session created: " + sessionId + "\n");
                        terminalOutput.append("[CREATED] Endpoint: " + createdEndpoint + "\n");
                        sessionIdField.setText(createdEndpoint);
                        
                        updateStatus("Session created: " + sessionId);
                    } else {
                        String error = response.has("error") ? response.get("error").getAsString() : "Unknown error";
                        terminalOutput.append("[ERROR] Failed to create session: " + error + "\n");
                        updateStatus("Failed to create session: " + error);
                    }
                } else if (endpoint.equals(currentSessionEndpoint)) {
                    // Handle shared terminal messages
                    try {
                        JsonObject messageObj = JsonParser.parseString(message).getAsJsonObject();
                        String type = messageObj.has("type") ? messageObj.get("type").getAsString() : "unknown";
                        
                        switch (type) {
                            case "system":
                                String systemMsg = messageObj.get("message").getAsString();
                                terminalOutput.append("[SYSTEM] " + systemMsg + "\n");
                                break;
                            case "pong":
                                terminalOutput.append("[PONG] Received pong from session\n");
                                break;
                            case "input_result":
                                boolean success = messageObj.get("success").getAsBoolean();
                                String resultMsg = messageObj.get("message").getAsString();
                                terminalOutput.append("[RESULT] " + resultMsg + " (success: " + success + ")\n");
                                break;
                            case "execution_started":
                                String projectName = messageObj.get("projectName").getAsString();
                                String language = messageObj.get("language").getAsString();
                                terminalOutput.append("[EXEC] Started execution of " + projectName + " (" + language + ")\n");
                                break;
                            case "error":
                                String errorMsg = messageObj.get("message").getAsString();
                                terminalOutput.append("[ERROR] " + errorMsg + "\n");
                                break;
                            default:
                                terminalOutput.append("[" + type.toUpperCase() + "] " + message + "\n");
                                break;
                        }
                    } catch (Exception e) {
                        // If not JSON, treat as plain text output
                        terminalOutput.append("[OUTPUT] " + message + "\n");
                    }
                }
                // Ignore messages from other endpoints that aren't relevant to this panel
                
                terminalOutput.setCaretPosition(terminalOutput.getDocument().getLength());
                
            } catch (Exception e) {
                terminalOutput.append("[ERROR] Error processing message: " + e.getMessage() + "\n");
                terminalOutput.setCaretPosition(terminalOutput.getDocument().getLength());
            }
        });
    }
}
