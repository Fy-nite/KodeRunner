import javax.swing.*;
import javax.swing.border.TitledBorder;
import javax.swing.tree.DefaultMutableTreeNode;
import javax.swing.tree.DefaultTreeModel;
import javax.swing.tree.TreeSelectionModel;
import java.awt.*;
import java.awt.event.KeyAdapter;
import java.awt.event.KeyEvent;
import java.awt.event.MouseAdapter;
import java.awt.event.MouseEvent;
import java.io.File;
import java.io.FileWriter;
import java.io.IOException;
import java.nio.file.Files;
import java.util.HashMap;
import java.util.Map;
import com.google.gson.Gson;
import org.fife.ui.rsyntaxtextarea.RSyntaxTextArea;
import org.fife.ui.rsyntaxtextarea.SyntaxConstants;
import org.fife.ui.rtextarea.RTextScrollPane;

public class CodeEditorPanel extends JPanel implements OutputHandler {
    private KodeRunnerTester parent;
    
    // File management
    private JTree fileTree;
    private DefaultTreeModel treeModel;
    private JTextField projectsPathField;
    private String currentProjectPath = "";
    private String currentFileName = "";
    
    // Code editing with syntax highlighting
    private RSyntaxTextArea codeEditor;
    private JTextField projectNameField;
    private JTextField fileNameField;
    private JComboBox<String> languageCombo;
    
    // PMS Integration
    private JTextField outputNameField;
    private JCheckBox runOnBuildCheckBox;
    private JButton buildProjectBtn;
    private JButton runProjectBtn;
    private JButton sendCodeBtn;
    
    // Terminal Integration
    private JTextArea outputArea;
    private JTextField terminalInput;
    private JButton sendInputBtn;
    private JCheckBox autoScrollCheckBox;
    
    // UI Components
    private JButton loadFileBtn;
    private JButton saveFileBtn;
    private JButton newFileBtn;
    private JButton deleteFileBtn;
    private JButton refreshTreeBtn;
    private JLabel fileStatusLabel;
    
    private Gson gson;
    
    public CodeEditorPanel(KodeRunnerTester parent) {
        this.parent = parent;
        this.gson = new Gson();
        initializeUI();
        setupEventHandlers();
        refreshFileTree();
    }
    
    private void initializeUI() {
        setLayout(new BorderLayout());
        
        // Create main split pane (horizontal split)
        JSplitPane mainSplitPane = new JSplitPane(JSplitPane.HORIZONTAL_SPLIT);
        
        // Left side - File browser and project tools
        JPanel leftPanel = createLeftPanel();
        leftPanel.setPreferredSize(new Dimension(300, 600));
        mainSplitPane.setLeftComponent(leftPanel);
        
        // Right side - Code editor and output with terminal
        JPanel rightPanel = createRightPanel();
        mainSplitPane.setRightComponent(rightPanel);
        
        mainSplitPane.setDividerLocation(300);
        add(mainSplitPane, BorderLayout.CENTER);
        
        // Top toolbar
        JPanel toolbar = createToolbar();
        add(toolbar, BorderLayout.NORTH);
        
        // Bottom status bar
        fileStatusLabel = new JLabel("Ready - Select a file to edit");
        fileStatusLabel.setBorder(BorderFactory.createEtchedBorder());
        add(fileStatusLabel, BorderLayout.SOUTH);
    }
    
    private JPanel createToolbar() {
        JPanel toolbar = new JPanel(new FlowLayout(FlowLayout.LEFT));
        toolbar.setBorder(new TitledBorder("Project Development Environment"));
        
        // Connection controls
        JButton connectCodeBtn = new JButton("Connect Code");
        JButton connectPMSBtn = new JButton("Connect PMS");
        connectCodeBtn.addActionListener(e -> parent.connectToEndpoint("/code"));
        connectPMSBtn.addActionListener(e -> parent.connectToEndpoint("/PMS"));
        
        toolbar.add(connectCodeBtn);
        toolbar.add(connectPMSBtn);
        toolbar.add(new JSeparator(SwingConstants.VERTICAL));
        
        // Project path
        toolbar.add(new JLabel("Projects:"));
        projectsPathField = new JTextField("koderunner/Projects", 15);
        toolbar.add(projectsPathField);
        
        refreshTreeBtn = new JButton("🔄");
        refreshTreeBtn.setToolTipText("Refresh project tree");
        refreshTreeBtn.addActionListener(e -> refreshFileTree());
        toolbar.add(refreshTreeBtn);
        
        return toolbar;
    }
    
    private JPanel createLeftPanel() {
        JPanel panel = new JPanel(new BorderLayout());
        
        // File tree
        JPanel treePanel = createFileTreePanel();
        
        // Project settings panel
        JPanel settingsPanel = createProjectSettingsPanel();
        
        // Split between tree and settings
        JSplitPane leftSplitPane = new JSplitPane(JSplitPane.VERTICAL_SPLIT);
        leftSplitPane.setTopComponent(treePanel);
        leftSplitPane.setBottomComponent(settingsPanel);
        leftSplitPane.setDividerLocation(300);
        
        panel.add(leftSplitPane, BorderLayout.CENTER);
        
        return panel;
    }
    
    private JPanel createFileTreePanel() {
        JPanel panel = new JPanel(new BorderLayout());
        panel.setBorder(new TitledBorder("Project Files"));
        
        // Create tree
        DefaultMutableTreeNode root = new DefaultMutableTreeNode("Projects");
        treeModel = new DefaultTreeModel(root);
        fileTree = new JTree(treeModel);
        fileTree.setRootVisible(true);
        fileTree.setShowsRootHandles(true);
        fileTree.getSelectionModel().setSelectionMode(TreeSelectionModel.SINGLE_TREE_SELECTION);
        
        JScrollPane treeScroll = new JScrollPane(fileTree);
        panel.add(treeScroll, BorderLayout.CENTER);
        
        // File operations panel
        JPanel fileOpsPanel = new JPanel(new FlowLayout(FlowLayout.LEFT));
        newFileBtn = new JButton("➕");
        newFileBtn.setToolTipText("New File");
        deleteFileBtn = new JButton("🗑️");
        deleteFileBtn.setToolTipText("Delete File");
        deleteFileBtn.setEnabled(false);
        
        fileOpsPanel.add(newFileBtn);
        fileOpsPanel.add(deleteFileBtn);
        
        panel.add(fileOpsPanel, BorderLayout.SOUTH);
        
        return panel;
    }
    

    private JPanel createRightPanel() {
        JPanel panel = new JPanel(new BorderLayout());
        
        // Create split pane for editor and output/terminal
        JSplitPane rightSplitPane = new JSplitPane(JSplitPane.VERTICAL_SPLIT);
        
        // Top - Code editor
        JPanel editorPanel = createEditorPanel();
        rightSplitPane.setTopComponent(editorPanel);
        
        // Bottom - Output area with integrated terminal
        JPanel outputTerminalPanel = createOutputTerminalPanel();
        rightSplitPane.setBottomComponent(outputTerminalPanel);
        
        rightSplitPane.setDividerLocation(400);
        panel.add(rightSplitPane, BorderLayout.CENTER);
        
        return panel;
    }
    
    private JPanel createEditorPanel() {
        JPanel panel = new JPanel(new BorderLayout());
        panel.setBorder(new TitledBorder("Code Editor"));
        
        // Editor toolbar
        JPanel editorToolbar = new JPanel(new FlowLayout(FlowLayout.LEFT));
        loadFileBtn = new JButton("📁 Load");
        saveFileBtn = new JButton("💾 Save");
        JButton clearBtn = new JButton("🗑️ Clear");
        
        // Syntax highlighting options
        JLabel syntaxLabel = new JLabel("Syntax:");
        JComboBox<String> syntaxCombo = new JComboBox<>(new String[]{
            "Auto", "Python", "JavaScript", "Java", "C", "C#", "JSON", "XML", "Plain Text"
        });
        syntaxCombo.addActionListener(e -> updateSyntaxHighlighting((String) syntaxCombo.getSelectedItem()));
        
        loadFileBtn.addActionListener(e -> loadExternalFile());
        saveFileBtn.addActionListener(e -> saveCurrentFile());
        clearBtn.addActionListener(e -> codeEditor.setText("")  );
        
        editorToolbar.add(loadFileBtn);
        editorToolbar.add(saveFileBtn);
        editorToolbar.add(clearBtn);
        editorToolbar.add(new JSeparator(SwingConstants.VERTICAL));
        editorToolbar.add(syntaxLabel);
        editorToolbar.add(syntaxCombo);
        
        panel.add(editorToolbar, BorderLayout.NORTH);
        
        // Code editor with syntax highlighting
        codeEditor = new RSyntaxTextArea(20, 60);
        codeEditor.setSyntaxEditingStyle(SyntaxConstants.SYNTAX_STYLE_PYTHON);
        codeEditor.setCodeFoldingEnabled(true);
        codeEditor.setAntiAliasingEnabled(true);
        codeEditor.setAutoIndentEnabled(true);
        codeEditor.setCloseCurlyBraces(true);
        codeEditor.setMarkOccurrences(true);
  
        codeEditor.setTabSize(4);
        codeEditor.setFont(new Font(Font.MONOSPACED, Font.PLAIN, 14));
        
        RTextScrollPane editorScroll = new RTextScrollPane(codeEditor);
        editorScroll.setFoldIndicatorEnabled(true);
        panel.add(editorScroll, BorderLayout.CENTER);
        
        // Set default code
        setDefaultCode();
        
        return panel;
    }
    
    private JPanel createOutputTerminalPanel() {
        JPanel panel = new JPanel(new BorderLayout());
        
        // Create tabbed pane for output and terminal
        JTabbedPane outputTabs = new JTabbedPane();
        
        // Output tab
        JPanel outputPanel = createOutputPanel();
        outputTabs.addTab("📤 Output", outputPanel);
        
        // Terminal tab  
        JPanel terminalPanel = createTerminalPanel();
        outputTabs.addTab("⚡ Terminal", terminalPanel);
        
        // Combined view tab
        JPanel combinedPanel = createCombinedOutputTerminalPanel();
        outputTabs.addTab("🔄 Combined", combinedPanel);
        
        panel.add(outputTabs, BorderLayout.CENTER);
        
        return panel;
    }
    
    private JPanel createOutputPanel() {
        JPanel panel = new JPanel(new BorderLayout());
        panel.setBorder(new TitledBorder("Build & Run Output"));
        
        outputArea = new JTextArea();
        outputArea.setEditable(false);
        outputArea.setFont(new Font(Font.MONOSPACED, Font.PLAIN, 12));
        outputArea.setBackground(Color.BLACK);
        outputArea.setForeground(Color.GREEN);
        
        JScrollPane outputScroll = new JScrollPane(outputArea);
        panel.add(outputScroll, BorderLayout.CENTER);
        
        // Output toolbar
        JPanel outputToolbar = new JPanel(new FlowLayout(FlowLayout.LEFT));
        JButton clearOutputBtn = new JButton("Clear Output");
        autoScrollCheckBox = new JCheckBox("Auto-scroll", true);
        
        clearOutputBtn.addActionListener(e -> outputArea.setText(""));
        
        outputToolbar.add(clearOutputBtn);
        outputToolbar.add(autoScrollCheckBox);
        
        panel.add(outputToolbar, BorderLayout.SOUTH);
        
        return panel;
    }
    
    private JPanel createTerminalPanel() {
        JPanel panel = new JPanel(new BorderLayout());
        panel.setBorder(new TitledBorder("Interactive Terminal"));
        
        // Terminal output area
        JTextArea terminalOutput = new JTextArea();
        terminalOutput.setEditable(false);
        terminalOutput.setFont(new Font(Font.MONOSPACED, Font.PLAIN, 12));
        terminalOutput.setBackground(new Color(20, 20, 20));
        terminalOutput.setForeground(Color.CYAN);
        terminalOutput.setText("Terminal ready. Connect to /terminput endpoint to send commands.\n");
        
        JScrollPane terminalScroll = new JScrollPane(terminalOutput);
        panel.add(terminalScroll, BorderLayout.CENTER);
        
        // Terminal input
        JPanel inputPanel = new JPanel(new BorderLayout());
        inputPanel.setBorder(BorderFactory.createTitledBorder("Command Input"));
        
        terminalInput = new JTextField();
        terminalInput.setFont(new Font(Font.MONOSPACED, Font.PLAIN, 12));
        terminalInput.setBackground(new Color(30, 30, 30));
        terminalInput.setForeground(Color.WHITE);
        terminalInput.setCaretColor(Color.WHITE);
        
        sendInputBtn = new JButton("Send");
        sendInputBtn.addActionListener(e -> sendTerminalInput());
        
        JButton connectTerminalBtn = new JButton("Connect");
        connectTerminalBtn.addActionListener(e -> parent.connectToEndpoint("/terminput"));
        
        JPanel inputControls = new JPanel(new FlowLayout(FlowLayout.RIGHT));
        inputControls.add(connectTerminalBtn);
        inputControls.add(sendInputBtn);
        
        inputPanel.add(terminalInput, BorderLayout.CENTER);
        inputPanel.add(inputControls, BorderLayout.EAST);
        
        panel.add(inputPanel, BorderLayout.SOUTH);
        
        return panel;
    }
    
    private JPanel createCombinedOutputTerminalPanel() {
        JPanel panel = new JPanel(new BorderLayout());
        
        // Split between output and terminal input
        JSplitPane combinedSplit = new JSplitPane(JSplitPane.VERTICAL_SPLIT);
        
        // Top - Output area (shared with output tab)
        JPanel topPanel = new JPanel(new BorderLayout());
        topPanel.setBorder(new TitledBorder("Output & Terminal"));
        
        // Use the same output area but in a different container
        JScrollPane combinedOutputScroll = new JScrollPane(outputArea);
        topPanel.add(combinedOutputScroll, BorderLayout.CENTER);
        
        combinedSplit.setTopComponent(topPanel);
        
        // Bottom - Terminal input section
        JPanel terminalInputPanel = new JPanel(new BorderLayout());
        terminalInputPanel.setBorder(new TitledBorder("Terminal Input"));
        
        JPanel inputRow = new JPanel(new BorderLayout());
        
        // Terminal prompt
        JLabel promptLabel = new JLabel("$ ");
        promptLabel.setFont(new Font(Font.MONOSPACED, Font.BOLD, 12));
        promptLabel.setForeground(Color.GREEN);
        
        // Use same terminal input field
        inputRow.add(promptLabel, BorderLayout.WEST);
        inputRow.add(terminalInput, BorderLayout.CENTER);
        
        // Input controls
        JPanel inputControls = new JPanel(new FlowLayout(FlowLayout.LEFT));
        
        JButton quickConnectBtn = new JButton("Connect Terminal");
        quickConnectBtn.addActionListener(e -> parent.connectToEndpoint("/terminput"));
        
        JButton clearTerminalBtn = new JButton("Clear");
        clearTerminalBtn.addActionListener(e -> {
            // Clear only terminal-related output
            String[] lines = outputArea.getText().split("\n");
            StringBuilder filtered = new StringBuilder();
            for (String line : lines) {
                if (!line.startsWith("[TERMINPUT]") && !line.startsWith("[TERMINAL]")) {
                    filtered.append(line).append("\n");
                }
            }
            outputArea.setText(filtered.toString());
        });
        
        inputControls.add(quickConnectBtn);
        inputControls.add(sendInputBtn);
        inputControls.add(clearTerminalBtn);
        
        terminalInputPanel.add(inputRow, BorderLayout.CENTER);
        terminalInputPanel.add(inputControls, BorderLayout.SOUTH);
        
        combinedSplit.setBottomComponent(terminalInputPanel);
        combinedSplit.setDividerLocation(300);
        
        panel.add(combinedSplit, BorderLayout.CENTER);
        
        return panel;
    }
    
    private JPanel createProjectSettingsPanel() {
        JPanel panel = new JPanel(new GridBagLayout());
        panel.setBorder(new TitledBorder("Project Settings"));
        GridBagConstraints gbc = new GridBagConstraints();
        gbc.insets = new Insets(3, 3, 3, 3);
        gbc.anchor = GridBagConstraints.WEST;
        
        // Project name
        gbc.gridx = 0; gbc.gridy = 0;
        panel.add(new JLabel("Project:"), gbc);
        gbc.gridx = 1; gbc.fill = GridBagConstraints.HORIZONTAL; gbc.weightx = 1.0;
        projectNameField = new JTextField("TestProject", 12);
        panel.add(projectNameField, gbc);
        
        // Current file
        gbc.gridx = 0; gbc.gridy = 1; gbc.fill = GridBagConstraints.NONE; gbc.weightx = 0;
        panel.add(new JLabel("File:"), gbc);
        gbc.gridx = 1; gbc.fill = GridBagConstraints.HORIZONTAL; gbc.weightx = 1.0;
        fileNameField = new JTextField("main.py", 12);
        panel.add(fileNameField, gbc);
        
        // Language
        gbc.gridx = 0; gbc.gridy = 2; gbc.fill = GridBagConstraints.NONE; gbc.weightx = 0;
        panel.add(new JLabel("Language:"), gbc);
        gbc.gridx = 1; gbc.fill = GridBagConstraints.HORIZONTAL; gbc.weightx = 1.0;
        String[] languages = {"python", "csharp", "nodejs", "c", "j-masm", "java", "go", "rust"};
        languageCombo = new JComboBox<>(languages);
        panel.add(languageCombo, gbc);
        
        // Output name
        gbc.gridx = 0; gbc.gridy = 3; gbc.fill = GridBagConstraints.NONE; gbc.weightx = 0;
        panel.add(new JLabel("Output:"), gbc);
        gbc.gridx = 1; gbc.fill = GridBagConstraints.HORIZONTAL; gbc.weightx = 1.0;
        outputNameField = new JTextField("output", 12);
        panel.add(outputNameField, gbc);
        
        // Run on build checkbox
        gbc.gridx = 0; gbc.gridy = 4; gbc.gridwidth = 2; gbc.fill = GridBagConstraints.HORIZONTAL;
        runOnBuildCheckBox = new JCheckBox("Run after build", true);
        panel.add(runOnBuildCheckBox, gbc);
        
        // Action buttons
        gbc.gridx = 0; gbc.gridy = 5; gbc.gridwidth = 2; gbc.fill = GridBagConstraints.HORIZONTAL;
        JPanel buttonPanel = new JPanel(new GridLayout(2, 2, 2, 2));
        
        sendCodeBtn = new JButton("Send Code");
        sendCodeBtn.setBackground(new Color(76, 175, 80));
        sendCodeBtn.setForeground(Color.WHITE);
        
        buildProjectBtn = new JButton("Build Only");
        buildProjectBtn.setBackground(new Color(255, 152, 0));
        buildProjectBtn.setForeground(Color.WHITE);
        
        runProjectBtn = new JButton("Build & Run");
        runProjectBtn.setBackground(new Color(46, 125, 50));
        runProjectBtn.setForeground(Color.WHITE);
        
        JButton stopBtn = new JButton("Stop All");
        stopBtn.setBackground(Color.RED);
        stopBtn.setForeground(Color.WHITE);
        stopBtn.addActionListener(e -> stopAllProcesses());
        
        buttonPanel.add(sendCodeBtn);
        buttonPanel.add(buildProjectBtn);
        buttonPanel.add(runProjectBtn);
        buttonPanel.add(stopBtn);
        
        panel.add(buttonPanel, gbc);
        
        return panel;
    }
    
    private void setupEventHandlers() {
        // File tree selection
        fileTree.addTreeSelectionListener(e -> {
            var selectedPath = fileTree.getSelectionPath();
            if (selectedPath != null && selectedPath.getPathCount() >= 3) {
                // Project -> File selected
                String projectName = selectedPath.getPathComponent(1).toString();
                String fileName = selectedPath.getPathComponent(2).toString();
                loadProjectFile(projectName, fileName);
                deleteFileBtn.setEnabled(true);
            } else if (selectedPath != null && selectedPath.getPathCount() == 2) {
                // Project selected
                String projectName = selectedPath.getPathComponent(1).toString();
                projectNameField.setText(projectName);
                deleteFileBtn.setEnabled(false);
            } else {
                deleteFileBtn.setEnabled(false);
            }
        });
        
        // Double-click to edit
        fileTree.addMouseListener(new MouseAdapter() {
            @Override
            public void mouseClicked(MouseEvent e) {
                if (e.getClickCount() == 2) {
                    var selectedPath = fileTree.getSelectionPath();
                    if (selectedPath != null && selectedPath.getPathCount() >= 3) {
                        String projectName = selectedPath.getPathComponent(1).toString();
                        String fileName = selectedPath.getPathComponent(2).toString();
                        loadProjectFile(projectName, fileName);
                    }
                }
            }
        });
        
        // Action buttons
        sendCodeBtn.addActionListener(e -> sendCode());
        buildProjectBtn.addActionListener(e -> buildProject());
        runProjectBtn.addActionListener(e -> runProject());
        newFileBtn.addActionListener(e -> createNewFile());
        deleteFileBtn.addActionListener(e -> deleteSelectedFile());
        
        // Terminal input handling
        terminalInput.addKeyListener(new KeyAdapter() {
            @Override
            public void keyPressed(KeyEvent e) {
                if (e.getKeyCode() == KeyEvent.VK_ENTER) {
                    sendTerminalInput();
                }
                else if (e.getKeyCode() == KeyEvent.VK_UP) {
                    // Could implement command history here
                }
            }
        });
        
        // Auto-update syntax highlighting when language changes
        languageCombo.addActionListener(e -> {
            updateFileExtension();
            updateSyntaxHighlighting("Auto");
        });
        
        // Auto-update syntax when file changes
        fileTree.addTreeSelectionListener(e -> {
            updateSyntaxHighlighting("Auto");
        });
    }
    
    private void refreshFileTree() {
        String projectsPath = projectsPathField.getText();
        File projectsDir = new File(projectsPath);
        
        DefaultMutableTreeNode root = new DefaultMutableTreeNode("Projects");
        
        if (projectsDir.exists() && projectsDir.isDirectory()) {
            File[] projects = projectsDir.listFiles(File::isDirectory);
            if (projects != null) {
                for (File project : projects) {
                    DefaultMutableTreeNode projectNode = new DefaultMutableTreeNode(project.getName());
                    
                    // Add files to project node
                    File[] files = project.listFiles(File::isFile);
                    if (files != null) {
                        for (File file : files) {
                            String icon = getFileIcon(file.getName());
                            DefaultMutableTreeNode fileNode = new DefaultMutableTreeNode(icon + " " + file.getName());
                            projectNode.add(fileNode);
                        }
                    }
                    
                    root.add(projectNode);
                }
            }
        }
        
        treeModel.setRoot(root);
        treeModel.reload();
        
        // Expand all projects
        for (int i = 0; i < fileTree.getRowCount(); i++) {
            fileTree.expandRow(i);
        }
        
        parent.updateStatus("Refreshed project tree - found " + root.getChildCount() + " projects");
    }
    
    private String getFileIcon(String fileName) {
        String ext = fileName.contains(".") ? 
            fileName.substring(fileName.lastIndexOf('.') + 1).toLowerCase() : "";
        return switch (ext) {
            case "py" -> "🐍";
            case "js" -> "📜";
            case "java" -> "☕";
            case "cs" -> "🔷";
            case "c", "h" -> "📝";
            case "json" -> "📋";
            case "md" -> "📖";
            case "txt" -> "📄";
            default -> "📄";
        };
    }
    
    private void loadProjectFile(String projectName, String fileName) {
        try {
            // Remove icon from filename
            if (fileName.contains(" ")) {
                fileName = fileName.substring(fileName.indexOf(' ') + 1);
            }
            
            String projectPath = projectsPathField.getText() + File.separator + projectName;
            File file = new File(projectPath, fileName);
            
            if (file.exists()) {
                // Save current file before loading new one
                saveCurrentFileIfModified();
                
                String content = Files.readString(file.toPath());
                codeEditor.setText(content);
                codeEditor.setCaretPosition(0);
                
                // Update UI
                projectNameField.setText(projectName);
                fileNameField.setText(fileName);
                currentProjectPath = projectPath;
                currentFileName = fileName;
                
                // Auto-detect language
                String language = detectLanguageFromFile(fileName);
                if (language != null) {
                    languageCombo.setSelectedItem(language);
                }
                
                fileStatusLabel.setText("Loaded: " + projectName + "/" + fileName);
                parent.updateStatus("Loaded file: " + fileName);
            }
        } catch (Exception e) {
            parent.updateStatus("Error loading file: " + e.getMessage());
        }
    }
    
    private void saveCurrentFile() {
        if (currentProjectPath.isEmpty() || currentFileName.isEmpty()) {
            saveAsNewFile();
            return;
        }
        
        try {
            File file = new File(currentProjectPath, currentFileName);
            Files.writeString(file.toPath(), codeEditor.getText());
            fileStatusLabel.setText("Saved: " + projectNameField.getText() + "/" + currentFileName);
            parent.updateStatus("Saved file: " + currentFileName);
        } catch (Exception e) {
            parent.updateStatus("Error saving file: " + e.getMessage());
        }
    }
    
    private void saveAsNewFile() {
        String projectName = projectNameField.getText().trim();
        String fileName = fileNameField.getText().trim();
        
        if (projectName.isEmpty() || fileName.isEmpty()) {
            JOptionPane.showMessageDialog(this, "Please specify project and file name", "Save File", JOptionPane.WARNING_MESSAGE);
            return;
        }
        
        try {
            String projectPath = projectsPathField.getText() + File.separator + projectName;
            File projectDir = new File(projectPath);
            if (!projectDir.exists()) {
                projectDir.mkdirs();
            }
            
            File file = new File(projectDir, fileName);
            Files.writeString(file.toPath(), codeEditor.getText());
            
            currentProjectPath = projectPath;
            currentFileName = fileName;
            
            refreshFileTree();
            fileStatusLabel.setText("Saved: " + projectName + "/" + fileName);
            parent.updateStatus("Saved new file: " + fileName);
        } catch (Exception e) {
            parent.updateStatus("Error saving file: " + e.getMessage());
        }
    }
    
    private void saveCurrentFileIfModified() {
        // Simple auto-save - in a real app you'd check for modifications
        if (!currentProjectPath.isEmpty() && !currentFileName.isEmpty()) {
            try {
                File file = new File(currentProjectPath, currentFileName);
                Files.writeString(file.toPath(), codeEditor.getText());
            } catch (Exception e) {
                // Ignore auto-save errors
            }
        }
    }
    
    private void createNewFile() {
        String projectName = projectNameField.getText().trim();
        if (projectName.isEmpty()) {
            JOptionPane.showMessageDialog(this, "Please select or enter a project name", "New File", JOptionPane.WARNING_MESSAGE);
            return;
        }
        
        String fileName = JOptionPane.showInputDialog(this, "Enter file name:", "New File", JOptionPane.PLAIN_MESSAGE);
        if (fileName == null || fileName.trim().isEmpty()) {
            return;
        }
        
        try {
            String projectPath = projectsPathField.getText() + File.separator + projectName;
            File projectDir = new File(projectPath);
            if (!projectDir.exists()) {
                projectDir.mkdirs();
            }
            
            File file = new File(projectDir, fileName.trim());
            if (file.exists()) {
                JOptionPane.showMessageDialog(this, "File already exists!", "New File", JOptionPane.WARNING_MESSAGE);
                return;
            }
            
            // Create empty file
            Files.writeString(file.toPath(), "");
            
            refreshFileTree();
            loadProjectFile(projectName, fileName.trim());
            parent.updateStatus("Created new file: " + fileName);
            
        } catch (Exception e) {
            parent.updateStatus("Error creating file: " + e.getMessage());
        }
    }
    
    private void deleteSelectedFile() {
        var selectedPath = fileTree.getSelectionPath();
        if (selectedPath == null || selectedPath.getPathCount() < 3) return;
        
        String projectName = selectedPath.getPathComponent(1).toString();
        String fileName = selectedPath.getPathComponent(2).toString();
        
        // Remove icon from filename
        if (fileName.contains(" ")) {
            fileName = fileName.substring(fileName.indexOf(' ') + 1);
        }
        
        int result = JOptionPane.showConfirmDialog(this, 
            "Delete file '" + fileName + "'?", "Delete File", 
            JOptionPane.YES_NO_OPTION, JOptionPane.WARNING_MESSAGE);
        
        if (result == JOptionPane.YES_OPTION) {
            try {
                String projectPath = projectsPathField.getText() + File.separator + projectName;
                File file = new File(projectPath, fileName);
                
                if (file.delete()) {
                    refreshFileTree();
                    if (fileName.equals(currentFileName)) {
                        codeEditor.setText("");
                        currentFileName = "";
                        currentProjectPath = "";
                    }
                    parent.updateStatus("Deleted file: " + fileName);
                } else {
                    parent.updateStatus("Failed to delete file: " + fileName);
                }
            } catch (Exception e) {
                parent.updateStatus("Error deleting file: " + e.getMessage());
            }
        }
    }
    
    private void sendCode() {
        if (!parent.isConnectedTo("/code")) {
            parent.updateStatus("Not connected to /code endpoint");
            return;
        }
        
        saveCurrentFile();
        parent.sendToEndpoint("/code", codeEditor.getText());
        parent.updateStatus("Code sent to KodeRunner");
        outputArea.append("[CODE] Sent code to server\n");
    }
    
    private void buildProject() {
        runProjectWithSettings(false);
    }
    
    private void runProject() {
        runProjectWithSettings(true);
    }
    
    private void runProjectWithSettings(boolean run) {
        if (!parent.isConnectedTo("/PMS")) {
            parent.updateStatus("Not connected to /PMS endpoint");
            return;
        }
        
        saveCurrentFile();
        
        // Create PMS message
        Map<String, String> pmsData = new HashMap<>();
        pmsData.put("PMS_System", "1.2.0");
        pmsData.put("Project_Name", projectNameField.getText());
        pmsData.put("Main_File", fileNameField.getText());
        pmsData.put("Project_Build_Systems", (String) languageCombo.getSelectedItem());
        pmsData.put("Project_Output", outputNameField.getText());
        pmsData.put("Run_On_Build", run || runOnBuildCheckBox.isSelected() ? "True" : "False");
        
        String json = gson.toJson(pmsData);
        parent.sendToEndpoint("/PMS", json);
        
        String action = run ? "Running" : "Building";
        parent.updateStatus(action + " project: " + projectNameField.getText());
        outputArea.append("[PMS] " + action + " project with settings:\n");
        outputArea.append("  Language: " + languageCombo.getSelectedItem() + "\n");
        outputArea.append("  Main File: " + fileNameField.getText() + "\n");
        outputArea.append("  Output: " + outputNameField.getText() + "\n\n");
    }
    
    private void stopAllProcesses() {
        if (!parent.isConnectedTo("/stop")) {
            parent.connectToEndpoint("/stop");
        }
        
        Map<String, Boolean> stopMessage = new HashMap<>();
        stopMessage.put("stopped", true);
        
        String json = gson.toJson(stopMessage);
        parent.sendToEndpoint("/stop", json);
        
        parent.updateStatus("Sent stop signal to all processes");
        outputArea.append("[STOP] Sent stop signal to all processes\n\n");
    }
    
    private void loadExternalFile() {
        JFileChooser fileChooser = new JFileChooser();
        if (fileChooser.showOpenDialog(this) == JFileChooser.APPROVE_OPTION) {
            try {
                File file = fileChooser.getSelectedFile();
                String content = Files.readString(file.toPath());
                codeEditor.setText(content);
                fileNameField.setText(file.getName());
                
                // Auto-detect language
                String language = detectLanguageFromFile(file.getName());
                if (language != null) {
                    languageCombo.setSelectedItem(language);
                }
                
                parent.updateStatus("Loaded external file: " + file.getName());
            } catch (IOException e) {
                parent.updateStatus("Error loading file: " + e.getMessage());
            }
        }
    }
    
    private String detectLanguageFromFile(String fileName) {
        String ext = fileName.contains(".") ? 
            fileName.substring(fileName.lastIndexOf('.') + 1).toLowerCase() : "";
        return switch (ext) {
            case "py" -> "python";
            case "js" -> "nodejs";
            case "java" -> "java";
            case "cs" -> "csharp";
            case "c", "h" -> "c";
            default -> null;
        };
    }
    
    private void updateFileExtension() {
        String language = (String) languageCombo.getSelectedItem();
        if (language != null && !fileNameField.getText().isEmpty()) {
            String currentName = fileNameField.getText();
            String baseName = currentName.contains(".") ? 
                currentName.substring(0, currentName.lastIndexOf('.')) : currentName;
            
            String extension = switch (language) {
                case "python" -> ".py";
                case "nodejs" -> ".js";
                case "java" -> ".java";
                case "csharp" -> ".cs";
                case "c" -> ".c";
                default -> "";
            };
            
            if (!extension.isEmpty()) {
                fileNameField.setText(baseName + extension);
            }
        }
    }
    
    private void setDefaultCode() {
        String defaultCode = """
            # KodeRunner Project
            # Edit this code and use the buttons to build/run your project
            
            def main():
                print("Hello from KodeRunner!")
                print("Use the sidebar to manage project files")
                print("Use the buttons below to build and run")
                print("Use the terminal tab to send interactive commands")
            
            if __name__ == "__main__":
                main()
            """;
        codeEditor.setText(defaultCode);
        updateSyntaxHighlighting("Auto");
    }
    
    // Public method for loading projects from other panels
    public void loadProject(String projectName) {
        projectNameField.setText(projectName);
        refreshFileTree();
        
        // Try to find and load main file
        String projectPath = projectsPathField.getText() + File.separator + projectName;
        File projectDir = new File(projectPath);
        
        if (projectDir.exists()) {
            String language = detectProjectLanguage(projectDir);
            String mainFile = detectMainFile(projectDir, language);
            
            if (language != null) {
                languageCombo.setSelectedItem(language);
            }
            
            if (mainFile != null) {
                loadProjectFile(projectName, mainFile);
            }
        }
    }
    
    private String detectProjectLanguage(File projectDir) {
        File[] files = projectDir.listFiles();
        if (files == null) return null;
        
        for (File file : files) {
            String name = file.getName().toLowerCase();
            if (name.endsWith(".py")) return "python";
            if (name.endsWith(".js")) return "nodejs";
            if (name.endsWith(".java")) return "java";
            if (name.endsWith(".cs") || name.endsWith(".csproj")) return "csharp";
            if (name.endsWith(".c")) return "c";
        }
        return null;
    }
    
    private String detectMainFile(File projectDir, String language) {
        if (language == null) return null;
        
        File[] files = projectDir.listFiles();
        if (files == null) return null;
        
        String[] mainNames = switch (language) {
            case "python" -> new String[]{"main.py", "app.py", "__main__.py"};
            case "nodejs" -> new String[]{"index.js", "app.js", "main.js"};
            case "java" -> new String[]{"Main.java", "App.java"};
            case "csharp" -> new String[]{"Program.cs", "Main.cs"};
            case "c" -> new String[]{"main.c", "app.c"};
            default -> new String[]{};
        };
        
        for (String mainName : mainNames) {
            for (File file : files) {
                if (file.getName().equals(mainName)) {
                    return mainName;
                }
            }
        }
        
        return null;
    }
    
    private void sendTerminalInput() {
        if (!parent.isConnectedTo("/terminput")) {
            outputArea.append("[TERMINAL] Not connected to /terminput endpoint\n");
            appendToOutput("[TERMINAL] Not connected to /terminput endpoint\n");
            return;
        }
        
        String input = terminalInput.getText().trim();
        if (input.isEmpty()) {
            return;
        }
        
        // Send to terminal endpoint
        parent.sendToEndpoint("/terminput", input);
        
        // Echo in output
        appendToOutput("[TERMINAL] > " + input + "\n");
        
        // Clear input
        terminalInput.setText("");
    }
    
    private void updateSyntaxHighlighting(String syntaxChoice) {
        String syntaxStyle = SyntaxConstants.SYNTAX_STYLE_NONE;
        
        if ("Auto".equals(syntaxChoice)) {
            // Auto-detect based on file extension or language combo
            String language = (String) languageCombo.getSelectedItem();
            String fileName = fileNameField.getText();
            
            if (fileName.endsWith(".py")) {
                syntaxStyle = SyntaxConstants.SYNTAX_STYLE_PYTHON;
            } else if (fileName.endsWith(".js")) {
                syntaxStyle = SyntaxConstants.SYNTAX_STYLE_JAVASCRIPT;
            } else if (fileName.endsWith(".java")) {
                syntaxStyle = SyntaxConstants.SYNTAX_STYLE_JAVA;
            } else if (fileName.endsWith(".cs")) {
                syntaxStyle = SyntaxConstants.SYNTAX_STYLE_CSHARP;
            } else if (fileName.endsWith(".c") || fileName.endsWith(".h")) {
                syntaxStyle = SyntaxConstants.SYNTAX_STYLE_C;
            } else if (fileName.endsWith(".json")) {
                syntaxStyle = SyntaxConstants.SYNTAX_STYLE_JSON;
            } else if (fileName.endsWith(".xml")) {
                syntaxStyle = SyntaxConstants.SYNTAX_STYLE_XML;
            } else if (language != null) {
                // Fallback to language combo selection
                syntaxStyle = switch (language) {
                    case "python" -> SyntaxConstants.SYNTAX_STYLE_PYTHON;
                    case "nodejs" -> SyntaxConstants.SYNTAX_STYLE_JAVASCRIPT;
                    case "java" -> SyntaxConstants.SYNTAX_STYLE_JAVA;
                    case "csharp" -> SyntaxConstants.SYNTAX_STYLE_CSHARP;
                    case "c" -> SyntaxConstants.SYNTAX_STYLE_C;
                    default -> SyntaxConstants.SYNTAX_STYLE_NONE;
                };
            }
        } else {
            // Manual selection
            syntaxStyle = switch (syntaxChoice) {
                case "Python" -> SyntaxConstants.SYNTAX_STYLE_PYTHON;
                case "JavaScript" -> SyntaxConstants.SYNTAX_STYLE_JAVASCRIPT;
                case "Java" -> SyntaxConstants.SYNTAX_STYLE_JAVA;
                case "C" -> SyntaxConstants.SYNTAX_STYLE_C;
                case "C#" -> SyntaxConstants.SYNTAX_STYLE_CSHARP;
                case "JSON" -> SyntaxConstants.SYNTAX_STYLE_JSON;
                case "XML" -> SyntaxConstants.SYNTAX_STYLE_XML;
                default -> SyntaxConstants.SYNTAX_STYLE_NONE;
            };
        }
        
        codeEditor.setSyntaxEditingStyle(syntaxStyle);
        codeEditor.repaint();
    }
    
    private void appendToOutput(String message) {
        SwingUtilities.invokeLater(() -> {
            outputArea.append(message);
            if (autoScrollCheckBox.isSelected()) {
                outputArea.setCaretPosition(outputArea.getDocument().getLength());
            }
        });
    }
    
    @Override
    public void handleOutput(String endpoint, String message) {
        SwingUtilities.invokeLater(() -> {
            String prefix = switch (endpoint) {
                case "/code" -> "[CODE] ";
                case "/PMS" -> "";
                case "/stop" -> "[STOP] ";
                case "/terminput" -> "[TERMINAL] ";
                default -> "[" + endpoint + "] ";
            };
            
            appendToOutput(message);
        });
    }
}
