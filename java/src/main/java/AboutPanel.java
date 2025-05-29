import javax.swing.*;
import javax.swing.border.TitledBorder;
import java.awt.*;
import java.awt.datatransfer.StringSelection;
import java.awt.event.ActionEvent;
import java.awt.event.ActionListener;
import java.net.URI;

public class AboutPanel extends JPanel {
    
    public AboutPanel() {
        initializeUI();
    }
    
    private void initializeUI() {
        setLayout(new BorderLayout());
        
        JPanel mainPanel = new JPanel(new GridBagLayout());
        GridBagConstraints gbc = new GridBagConstraints();
        gbc.insets = new Insets(10, 10, 10, 10);
        
        // Application icon/logo area
        JPanel iconPanel = new JPanel(new FlowLayout());
        JLabel iconLabel = new JLabel("🚀", SwingConstants.CENTER);
        iconLabel.setFont(new Font(Font.SANS_SERIF, Font.PLAIN, 48));
        iconPanel.add(iconLabel);
        
        gbc.gridx = 0; gbc.gridy = 0; gbc.gridwidth = 2;
        mainPanel.add(iconPanel, gbc);
        
        // Application info
        JPanel infoPanel = new JPanel(new GridBagLayout());
        infoPanel.setBorder(new TitledBorder("Application Information"));
        GridBagConstraints infoGbc = new GridBagConstraints();
        infoGbc.insets = new Insets(5, 5, 5, 5);
        infoGbc.anchor = GridBagConstraints.WEST;
        
        addInfoRow(infoPanel, infoGbc, 0, "Application:", "KodeRunner Desktop Tester");
        addInfoRow(infoPanel, infoGbc, 1, "Version:", "1.0.0");
        addInfoRow(infoPanel, infoGbc, 2, "Author:", "KodeRunner Team");
        addInfoRow(infoPanel, infoGbc, 3, "Java Version:", System.getProperty("java.version"));
        addInfoRow(infoPanel, infoGbc, 4, "OS:", System.getProperty("os.name") + " " + System.getProperty("os.version"));
        
        gbc.gridx = 0; gbc.gridy = 1; gbc.gridwidth = 2; gbc.fill = GridBagConstraints.HORIZONTAL;
        mainPanel.add(infoPanel, gbc);
        
        // Description
        JPanel descPanel = new JPanel(new BorderLayout());
        descPanel.setBorder(new TitledBorder("About"));
        
        JTextArea descArea = new JTextArea();
        descArea.setEditable(false);
        descArea.setBackground(getBackground());
        descArea.setFont(new Font(Font.SANS_SERIF, Font.PLAIN, 12));
        descArea.setText(
            "KodeRunner Desktop Tester is a comprehensive testing client for the KodeRunner platform.\n\n" +
            "Features:\n" +
            "• Code execution testing across multiple programming languages\n" +
            "• Real-time terminal interaction via WebSocket connections\n" +
            "• Shared terminal sessions for collaborative development\n" +
            "• Dynamic endpoint creation and management\n" +
            "• Process management and monitoring\n" +
            "• Customizable user interface with multiple Look & Feel options\n" +
            "• Configuration management for persistent settings\n" +
            "• Multi-endpoint connection monitoring\n" +
            "• Interactive terminal input with JSON message support\n\n" +
            "This application allows developers to test and interact with KodeRunner services\n" +
            "in a user-friendly desktop environment, including the new shared terminal\n" +
            "functionality for collaborative code execution and debugging."
        );
        descArea.setLineWrap(true);
        descArea.setWrapStyleWord(true);
        
        JScrollPane scrollPane = new JScrollPane(descArea);
        scrollPane.setPreferredSize(new Dimension(500, 200));
        descPanel.add(scrollPane, BorderLayout.CENTER);
        
        gbc.gridx = 0; gbc.gridy = 2; gbc.gridwidth = 2; gbc.fill = GridBagConstraints.BOTH; gbc.weightx = 1.0; gbc.weighty = 1.0;
        mainPanel.add(descPanel, gbc);
        
        // Links panel
        JPanel linksPanel = new JPanel(new FlowLayout());
        linksPanel.setBorder(new TitledBorder("Links"));
        
        JButton githubBtn = new JButton("GitHub Repository");
        JButton docsBtn = new JButton("Documentation");
        JButton licenseBtn = new JButton("View License");
        
        githubBtn.addActionListener(e -> openURL("https://github.com/koderunner"));
        docsBtn.addActionListener(e -> openURL("https://docs.koderunner.com"));
        licenseBtn.addActionListener(e -> showLicense());
        
        linksPanel.add(githubBtn);
        linksPanel.add(docsBtn);
        linksPanel.add(licenseBtn);
        
        gbc.gridx = 0; gbc.gridy = 3; gbc.gridwidth = 2; gbc.fill = GridBagConstraints.HORIZONTAL; gbc.weighty = 0;
        mainPanel.add(linksPanel, gbc);
        
        add(mainPanel, BorderLayout.CENTER);
    }
    
    private void addInfoRow(JPanel parent, GridBagConstraints gbc, int row, String label, String value) {
        gbc.gridx = 0; gbc.gridy = row; gbc.weightx = 0;
        JLabel labelComp = new JLabel(label);
        labelComp.setFont(labelComp.getFont().deriveFont(Font.BOLD));
        parent.add(labelComp, gbc);
        
        gbc.gridx = 1; gbc.weightx = 1.0;
        parent.add(new JLabel(value), gbc);
    }
    
    private void openURL(String url) {
        try {
            if (Desktop.isDesktopSupported()) {
                Desktop.getDesktop().browse(URI.create(url));
            } else {
                // Fallback: copy to clipboard
                StringSelection selection = new StringSelection(url);
                Toolkit.getDefaultToolkit().getSystemClipboard().setContents(selection, null);
                JOptionPane.showMessageDialog(this, 
                    "URL copied to clipboard: " + url, 
                    "Browser not available", 
                    JOptionPane.INFORMATION_MESSAGE);
            }
        } catch (Exception e) {
            JOptionPane.showMessageDialog(this, 
                "Could not open URL: " + url + "\nError: " + e.getMessage(), 
                "Error", 
                JOptionPane.ERROR_MESSAGE);
        }
    }
    
    private void showLicense() {
        JDialog licenseDialog = new JDialog((Frame) SwingUtilities.getWindowAncestor(this), "License", true);
        licenseDialog.setSize(600, 400);
        licenseDialog.setLocationRelativeTo(this);
        
        JTextArea licenseArea = new JTextArea();
        licenseArea.setEditable(false);
        licenseArea.setFont(new Font(Font.MONOSPACED, Font.PLAIN, 11));
        licenseArea.setText(
            "MIT License\n\n" +
            "Copyright (c) 2024 KodeRunner Team\n\n" +
            "Permission is hereby granted, free of charge, to any person obtaining a copy\n" +
            "of this software and associated documentation files (the \"Software\"), to deal\n" +
            "in the Software without restriction, including without limitation the rights\n" +
            "to use, copy, modify, merge, publish, distribute, sublicense, and/or sell\n" +
            "copies of the Software, and to permit persons to whom the Software is\n" +
            "furnished to do so, subject to the following conditions:\n\n" +
            "The above copyright notice and this permission notice shall be included in all\n" +
            "copies or substantial portions of the Software.\n\n" +
            "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR\n" +
            "IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,\n" +
            "FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE\n" +
            "AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER\n" +
            "LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,\n" +
            "OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE\n" +
            "SOFTWARE."
        );
        
        JScrollPane scrollPane = new JScrollPane(licenseArea);
        licenseDialog.add(scrollPane);
        
        licenseDialog.setVisible(true);
    }
}
