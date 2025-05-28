import javax.swing.*;
import javax.swing.border.TitledBorder;
import java.awt.*;
import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import java.util.HashMap;
import java.util.Map;

public class PMSTestPanel extends JPanel implements OutputHandler {
    private KodeRunnerTester parent;
    private JTextField projectNameField;
    private JTextField mainFileField;
    private JComboBox<String> buildSystemCombo;
    private JTextField outputField;
    private JCheckBox runOnBuildCheck;
    private JTextArea jsonPreview;
    private JTextArea outputArea;
    private JButton sendPMSBtn;
    private Gson gson;
    
    public PMSTestPanel(KodeRunnerTester parent) {
        this.parent = parent;
        this.gson = new GsonBuilder().setPrettyPrinting().create();
        initializeUI();
        setupEventHandlers();
    }
    
    private void initializeUI() {
        setLayout(new BorderLayout());
        
        // Create input panel
        JPanel inputPanel = createInputPanel();
        add(inputPanel, BorderLayout.NORTH);
        
        // Create split pane for JSON preview and output
        JSplitPane splitPane = new JSplitPane(JSplitPane.HORIZONTAL_SPLIT);
        
        // JSON preview
        jsonPreview = new JTextArea();
        jsonPreview.setFont(new Font(Font.MONOSPACED, Font.PLAIN, 12));
        jsonPreview.setEditable(false);
        JScrollPane jsonScroll = new JScrollPane(jsonPreview);
        jsonScroll.setBorder(new TitledBorder("JSON Preview"));
        
        // Output area
        outputArea = new JTextArea();
        outputArea.setEditable(false);
        outputArea.setFont(new Font(Font.MONOSPACED, Font.PLAIN, 12));
        outputArea.setBackground(Color.BLACK);
        outputArea.setForeground(Color.CYAN);
        JScrollPane outputScroll = new JScrollPane(outputArea);
        outputScroll.setBorder(new TitledBorder("PMS Output"));
        
        splitPane.setLeftComponent(jsonScroll);
        splitPane.setRightComponent(outputScroll);
        splitPane.setDividerLocation(400);
        
        add(splitPane, BorderLayout.CENTER);
        
        updateJsonPreview();
    }
    
    private JPanel createInputPanel() {
        JPanel panel = new JPanel(new GridBagLayout());
        panel.setBorder(new TitledBorder("PMS Configuration"));
        GridBagConstraints gbc = new GridBagConstraints();
        gbc.insets = new Insets(5, 5, 5, 5);
        
        // Project Name
        gbc.gridx = 0; gbc.gridy = 0;
        panel.add(new JLabel("Project Name:"), gbc);
        gbc.gridx = 1;
        projectNameField = new JTextField("TestProject", 15);
        panel.add(projectNameField, gbc);
        
        // Main File
        gbc.gridx = 0; gbc.gridy = 1;
        panel.add(new JLabel("Main File:"), gbc);
        gbc.gridx = 1;
        mainFileField = new JTextField("main.py", 15);
        panel.add(mainFileField, gbc);
        
        // Build System
        gbc.gridx = 0; gbc.gridy = 2;
        panel.add(new JLabel("Build System:"), gbc);
        gbc.gridx = 1;
        buildSystemCombo = new JComboBox<>(new String[]{
            "python", "csharp", "nodejs", "c", "gcc", "j-masm", "testrunner"
        });
        panel.add(buildSystemCombo, gbc);
        
        // Output
        gbc.gridx = 0; gbc.gridy = 3;
        panel.add(new JLabel("Output:"), gbc);
        gbc.gridx = 1;
        outputField = new JTextField("output", 15);
        panel.add(outputField, gbc);
        
        // Run on Build
        gbc.gridx = 0; gbc.gridy = 4;
        gbc.gridwidth = 2;
        runOnBuildCheck = new JCheckBox("Run on Build", true);
        panel.add(runOnBuildCheck, gbc);
        
        // Buttons
        gbc.gridy = 5;
        JPanel buttonPanel = new JPanel(new FlowLayout());
        sendPMSBtn = new JButton("Send PMS Request");
        JButton connectBtn = new JButton("Connect to PMS");
        JButton clearBtn = new JButton("Clear Output");
        
        buttonPanel.add(connectBtn);
        buttonPanel.add(sendPMSBtn);
        buttonPanel.add(clearBtn);
        panel.add(buttonPanel, gbc);
        
        connectBtn.addActionListener(e -> parent.connectToEndpoint("/PMS"));
        clearBtn.addActionListener(e -> outputArea.setText(""));
        
        return panel;
    }
    
    private void setupEventHandlers() {
        sendPMSBtn.addActionListener(e -> sendPMSRequest());
        
        // Update JSON preview when fields change
        projectNameField.addActionListener(e -> updateJsonPreview());
        mainFileField.addActionListener(e -> updateJsonPreview());
        buildSystemCombo.addActionListener(e -> updateJsonPreview());
        outputField.addActionListener(e -> updateJsonPreview());
        runOnBuildCheck.addActionListener(e -> updateJsonPreview());
        
        // Also update on key release for text fields
        projectNameField.addKeyListener(new java.awt.event.KeyAdapter() {
            public void keyReleased(java.awt.event.KeyEvent evt) {
                updateJsonPreview();
            }
        });
        mainFileField.addKeyListener(new java.awt.event.KeyAdapter() {
            public void keyReleased(java.awt.event.KeyEvent evt) {
                updateJsonPreview();
            }
        });
        outputField.addKeyListener(new java.awt.event.KeyAdapter() {
            public void keyReleased(java.awt.event.KeyEvent evt) {
                updateJsonPreview();
            }
        });
    }
    
    private void updateJsonPreview() {
        Map<String, String> pmsData = new HashMap<>();
        pmsData.put("PMS_System", "1.2.0");
        pmsData.put("Project_Name", projectNameField.getText());
        pmsData.put("Main_File", mainFileField.getText());
        pmsData.put("Project_Build_Systems", (String) buildSystemCombo.getSelectedItem());
        pmsData.put("Project_Output", outputField.getText());
        pmsData.put("Run_On_Build", runOnBuildCheck.isSelected() ? "True" : "False");
        
        String json = gson.toJson(pmsData);
        jsonPreview.setText(json);
    }
    
    private void sendPMSRequest() {
        if (!parent.isConnectedTo("/PMS")) {
            parent.updateStatus("Not connected to /PMS endpoint");
            return;
        }
        
        updateJsonPreview();
        String json = jsonPreview.getText();
        parent.sendToEndpoint("/PMS", json);
        parent.updateStatus("PMS request sent");
    }
    
    @Override
    public void handleOutput(String endpoint, String message) {
        if ("/PMS".equals(endpoint)) {
            outputArea.append(message);
            outputArea.setCaretPosition(outputArea.getDocument().getLength());
        }
    }
}
