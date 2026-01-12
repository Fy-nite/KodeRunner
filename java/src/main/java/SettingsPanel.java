import javax.swing.*;
import javax.swing.border.TitledBorder;
import java.awt.*;
import java.awt.event.ActionEvent;
import java.awt.event.ActionListener;
import java.io.File;

public class SettingsPanel extends JPanel {
    private KodeRunnerTester parent;
    private JComboBox<String> lafCombo;
    private JTextField defaultServerField;
    private JSpinner fontSizeSpinner;
    private JCheckBox autoConnectCheckBox;
    private JTextField defaultProjectField;
    private ConfigManager configManager;
    
    public SettingsPanel(KodeRunnerTester parent) {
        this.parent = parent;
        this.configManager = new ConfigManager();
        initializeUI();
        loadSettings();
        setupEventHandlers();
    }
    
    private void initializeUI() {
        setLayout(new BorderLayout());
        
        JPanel mainPanel = new JPanel(new GridBagLayout());
        GridBagConstraints gbc = new GridBagConstraints();
        gbc.insets = new Insets(5, 5, 5, 5);
        gbc.anchor = GridBagConstraints.WEST;
        
        // Look and Feel section
        JPanel lafPanel = new JPanel(new FlowLayout(FlowLayout.LEFT));
        lafPanel.setBorder(new TitledBorder("Appearance"));
        
        lafPanel.add(new JLabel("Look and Feel:"));
        lafCombo = new JComboBox<>(getAvailableLookAndFeels());
        lafPanel.add(lafCombo);
        
        JButton applyLafBtn = new JButton("Apply");
        applyLafBtn.addActionListener(e -> applyLookAndFeel());
        lafPanel.add(applyLafBtn);
        
        lafPanel.add(new JLabel("Font Size:"));
        fontSizeSpinner = new JSpinner(new SpinnerNumberModel(12, 8, 24, 1));
        lafPanel.add(fontSizeSpinner);
        
        gbc.gridx = 0; gbc.gridy = 0; gbc.gridwidth = 2; gbc.fill = GridBagConstraints.HORIZONTAL;
        mainPanel.add(lafPanel, gbc);
        
        // Connection settings
        JPanel connPanel = new JPanel(new GridBagLayout());
        connPanel.setBorder(new TitledBorder("Connection Settings"));
        GridBagConstraints connGbc = new GridBagConstraints();
        connGbc.insets = new Insets(3, 3, 3, 3);
        
        connGbc.gridx = 0; connGbc.gridy = 0;
        connPanel.add(new JLabel("Default Server:"), connGbc);
        connGbc.gridx = 1; connGbc.fill = GridBagConstraints.HORIZONTAL; connGbc.weightx = 1.0;
        defaultServerField = new JTextField("ws://localhost:8080", 20);
        connPanel.add(defaultServerField, connGbc);
        
        connGbc.gridx = 0; connGbc.gridy = 1; connGbc.fill = GridBagConstraints.NONE; connGbc.weightx = 0;
        autoConnectCheckBox = new JCheckBox("Auto-connect on startup");
        connPanel.add(autoConnectCheckBox, connGbc);
        
        // Add new connection settings
        connGbc.gridx = 0; connGbc.gridy = 2;
        connPanel.add(new JLabel("Connection Timeout:"), connGbc);
        connGbc.gridx = 1; connGbc.fill = GridBagConstraints.HORIZONTAL; connGbc.weightx = 1.0;
        JSpinner timeoutSpinner = new JSpinner(new SpinnerNumberModel(30, 5, 300, 5));
        connPanel.add(timeoutSpinner, connGbc);
        
        connGbc.gridx = 0; connGbc.gridy = 3; connGbc.fill = GridBagConstraints.NONE; connGbc.weightx = 0;
        JCheckBox autoReconnectCheckBox = new JCheckBox("Auto-reconnect on disconnect");
        connPanel.add(autoReconnectCheckBox, connGbc);
        
        gbc.gridx = 0; gbc.gridy = 1; gbc.gridwidth = 2; gbc.fill = GridBagConstraints.HORIZONTAL;
        mainPanel.add(connPanel, gbc);
        
        // Project settings
        JPanel projPanel = new JPanel(new GridBagLayout());
        projPanel.setBorder(new TitledBorder("Project Settings"));
        GridBagConstraints projGbc = new GridBagConstraints();
        projGbc.insets = new Insets(3, 3, 3, 3);
        
        projGbc.gridx = 0; projGbc.gridy = 0;
        projPanel.add(new JLabel("Default Project Name:"), projGbc);
        projGbc.gridx = 1; projGbc.fill = GridBagConstraints.HORIZONTAL; projGbc.weightx = 1.0;
        defaultProjectField = new JTextField("TestProject", 20);
        projPanel.add(defaultProjectField, projGbc);
        
        projGbc.gridx = 0; projGbc.gridy = 1; projGbc.fill = GridBagConstraints.NONE; projGbc.weightx = 0;
        projPanel.add(new JLabel("Projects Directory:"), projGbc);
        projGbc.gridx = 1; projGbc.fill = GridBagConstraints.HORIZONTAL; projGbc.weightx = 1.0;
        JTextField projectsDirField = new JTextField("koderunner/Projects", 20);
        projPanel.add(projectsDirField, projGbc);
        
        projGbc.gridx = 0; projGbc.gridy = 2; projGbc.fill = GridBagConstraints.NONE; projGbc.weightx = 0;
        JCheckBox autoSaveCheckBox = new JCheckBox("Auto-save code changes");
        projPanel.add(autoSaveCheckBox, projGbc);
        
        gbc.gridx = 0; gbc.gridy = 2; gbc.gridwidth = 2; gbc.fill = GridBagConstraints.HORIZONTAL;
        mainPanel.add(projPanel, gbc);
        
        // Monitoring settings
        JPanel monitorPanel = new JPanel(new GridBagLayout());
        monitorPanel.setBorder(new TitledBorder("Monitoring Settings"));
        GridBagConstraints monGbc = new GridBagConstraints();
        monGbc.insets = new Insets(3, 3, 3, 3);
        
        monGbc.gridx = 0; monGbc.gridy = 0;
        monitorPanel.add(new JLabel("Performance Update Interval:"), monGbc);
        monGbc.gridx = 1; monGbc.fill = GridBagConstraints.HORIZONTAL; monGbc.weightx = 1.0;
        JSpinner perfIntervalSpinner = new JSpinner(new SpinnerNumberModel(2, 1, 60, 1));
        monitorPanel.add(perfIntervalSpinner, monGbc);
        monGbc.gridx = 2; monGbc.fill = GridBagConstraints.NONE; monGbc.weightx = 0;
        monitorPanel.add(new JLabel("seconds"), monGbc);
        
        monGbc.gridx = 0; monGbc.gridy = 1; monGbc.gridwidth = 3; monGbc.fill = GridBagConstraints.HORIZONTAL;
        JCheckBox enableLoggingCheckBox = new JCheckBox("Enable detailed WebSocket logging");
        monitorPanel.add(enableLoggingCheckBox, monGbc);
        
        gbc.gridx = 0; gbc.gridy = 3; gbc.gridwidth = 2; gbc.fill = GridBagConstraints.HORIZONTAL;
        mainPanel.add(monitorPanel, gbc);
        
        // Buttons
        JPanel buttonPanel = new JPanel(new FlowLayout());
        JButton saveBtn = new JButton("Save Settings");
        JButton resetBtn = new JButton("Reset to Defaults");
        JButton exportBtn = new JButton("Export Settings");
        JButton importBtn = new JButton("Import Settings");
        
        saveBtn.addActionListener(e -> saveSettings());
        resetBtn.addActionListener(e -> resetToDefaults());
        exportBtn.addActionListener(e -> exportSettings());
        importBtn.addActionListener(e -> importSettings());
        
        buttonPanel.add(saveBtn);
        buttonPanel.add(resetBtn);
        buttonPanel.add(exportBtn);
        buttonPanel.add(importBtn);
        
        gbc.gridx = 0; gbc.gridy = 4; gbc.gridwidth = 2; gbc.fill = GridBagConstraints.HORIZONTAL;
        mainPanel.add(buttonPanel, gbc);
        
        add(mainPanel, BorderLayout.NORTH);
    }
    
    private String[] getAvailableLookAndFeels() {
        UIManager.LookAndFeelInfo[] lafs = UIManager.getInstalledLookAndFeels();
        String[] lafNames = new String[lafs.length];
        for (int i = 0; i < lafs.length; i++) {
            lafNames[i] = lafs[i].getName();
        }
        return lafNames;
    }
    
    private void setupEventHandlers() {
        fontSizeSpinner.addChangeListener(e -> updateFontSize());
    }
    
    private void applyLookAndFeel() {
        String selectedLaf = (String) lafCombo.getSelectedItem();
        try {
            UIManager.LookAndFeelInfo[] lafs = UIManager.getInstalledLookAndFeels();
            for (UIManager.LookAndFeelInfo laf : lafs) {
                if (laf.getName().equals(selectedLaf)) {
                    UIManager.setLookAndFeel(laf.getClassName());
                    SwingUtilities.updateComponentTreeUI(parent);
                    parent.updateStatus("Look and Feel applied: " + selectedLaf);
                    break;
                }
            }
        } catch (Exception e) {
            parent.updateStatus("Error applying Look and Feel: " + e.getMessage());
        }
    }
    
    private void updateFontSize() {
        int fontSize = (Integer) fontSizeSpinner.getValue();
        // This would need to be implemented to update font sizes across the application
        parent.updateStatus("Font size updated to: " + fontSize);
    }
    
    private void loadSettings() {
        AppConfig config = configManager.loadConfig();
        lafCombo.setSelectedItem(config.getLookAndFeel());
        defaultServerField.setText(config.getDefaultServer());
        fontSizeSpinner.setValue(config.getFontSize());
        autoConnectCheckBox.setSelected(config.isAutoConnect());
        defaultProjectField.setText(config.getDefaultProject());
    }
    
    private void saveSettings() {
        AppConfig config = new AppConfig();
        config.setLookAndFeel((String) lafCombo.getSelectedItem());
        config.setDefaultServer(defaultServerField.getText());
        config.setFontSize((Integer) fontSizeSpinner.getValue());
        config.setAutoConnect(autoConnectCheckBox.isSelected());
        config.setDefaultProject(defaultProjectField.getText());
        
        configManager.saveConfig(config);
        parent.updateStatus("Settings saved successfully");
    }
    
    private void resetToDefaults() {
        lafCombo.setSelectedItem("Nimbus");
        defaultServerField.setText("ws://localhost:8080");
        fontSizeSpinner.setValue(12);
        autoConnectCheckBox.setSelected(false);
        defaultProjectField.setText("TestProject");
        parent.updateStatus("Settings reset to defaults");
    }
    
    private void exportSettings() {
        JFileChooser fileChooser = new JFileChooser();
        fileChooser.setSelectedFile(new File("koderunner-settings.json"));
        if (fileChooser.showSaveDialog(this) == JFileChooser.APPROVE_OPTION) {
            // Implementation would save current settings to JSON file
            parent.updateStatus("Settings export functionality not yet implemented");
        }
    }
    
    private void importSettings() {
        JFileChooser fileChooser = new JFileChooser();
        fileChooser.setFileFilter(new javax.swing.filechooser.FileNameExtensionFilter("JSON files (*.json)", "json"));
        if (fileChooser.showOpenDialog(this) == JFileChooser.APPROVE_OPTION) {
            // Implementation would load settings from JSON file
            parent.updateStatus("Settings import functionality not yet implemented");
        }
    }
    
    public AppConfig getCurrentConfig() {
        return configManager.loadConfig();
    }
}
