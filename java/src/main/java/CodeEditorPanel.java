import javax.swing.*;
import javax.swing.border.TitledBorder;
import java.awt.*;
import java.awt.event.ActionEvent;
import java.awt.event.ActionListener;
import java.io.File;
import java.io.FileWriter;
import java.io.IOException;
import java.nio.file.Files;

public class CodeEditorPanel extends JPanel implements OutputHandler {
    private KodeRunnerTester parent;
    private JTextArea codeEditor;
    private JTextField projectNameField;
    private JTextField fileNameField;
    private JComboBox<String> languageCombo;
    private JTextArea outputArea;
    private JButton sendCodeBtn;
    private JButton loadFileBtn;
    private JButton saveFileBtn;
    private JButton generateHeaderBtn;
    private JButton loadTemplateBtn;
    private JButton insertHeaderBtn;
    
    public CodeEditorPanel(KodeRunnerTester parent) {
        this.parent = parent;
        initializeUI();
        setupEventHandlers();
    }
    
    private void initializeUI() {
        setLayout(new BorderLayout());
        
        // Create toolbar
        JPanel toolbar = createToolbar();
        add(toolbar, BorderLayout.NORTH);
        
        // Create split pane
        JSplitPane splitPane = new JSplitPane(JSplitPane.VERTICAL_SPLIT);
        
        // Code editor
        codeEditor = new JTextArea();
        codeEditor.setFont(new Font(Font.MONOSPACED, Font.PLAIN, 14));
        codeEditor.setTabSize(4);
        JScrollPane editorScroll = new JScrollPane(codeEditor);
        editorScroll.setBorder(new TitledBorder("Code Editor"));
        editorScroll.setPreferredSize(new Dimension(600, 400));
        
        // Output area
        outputArea = new JTextArea();
        outputArea.setEditable(false);
        outputArea.setFont(new Font(Font.MONOSPACED, Font.PLAIN, 12));
        outputArea.setBackground(Color.BLACK);
        outputArea.setForeground(Color.GREEN);
        JScrollPane outputScroll = new JScrollPane(outputArea);
        outputScroll.setBorder(new TitledBorder("Output"));
        outputScroll.setPreferredSize(new Dimension(600, 200));
        
        splitPane.setTopComponent(editorScroll);
        splitPane.setBottomComponent(outputScroll);
        splitPane.setDividerLocation(400);
        
        add(splitPane, BorderLayout.CENTER);
        
        // Set default code
        setDefaultCode();
    }
    
    private JPanel createToolbar() {
        JPanel mainToolbar = new JPanel(new BorderLayout());
        
        // First row - Project settings
        JPanel projectPanel = new JPanel(new FlowLayout(FlowLayout.LEFT));
        projectPanel.add(new JLabel("Project:"));
        projectNameField = new JTextField("TestProject", 12);
        projectPanel.add(projectNameField);
        
        projectPanel.add(new JLabel("File:"));
        fileNameField = new JTextField("main.py", 12);
        projectPanel.add(fileNameField);
        
        projectPanel.add(new JLabel("Language:"));
        // Use expanded language list from LanguageConfig
        String[] allLanguages = {"python", "csharp", "nodejs", "c", "j-masm", "java", "go", "rust", "testrunner"};
        languageCombo = new JComboBox<>(allLanguages);
        projectPanel.add(languageCombo);
        
        mainToolbar.add(projectPanel, BorderLayout.NORTH);
        
        // Second row - Action buttons
        JPanel actionPanel = new JPanel(new FlowLayout(FlowLayout.LEFT));
        
        // File operations
        loadFileBtn = new JButton("Load File");
        saveFileBtn = new JButton("Save File");
        actionPanel.add(loadFileBtn);
        actionPanel.add(saveFileBtn);
        
        // Separator
        actionPanel.add(new JSeparator(SwingConstants.VERTICAL));
        
        // Template and header operations
        generateHeaderBtn = new JButton("Generate Header");
        generateHeaderBtn.setToolTipText("Generate language-specific project header");
        loadTemplateBtn = new JButton("Load Template");
        loadTemplateBtn.setToolTipText("Load language-specific code template");
        insertHeaderBtn = new JButton("Insert Header Only");
        insertHeaderBtn.setToolTipText("Insert header at current cursor position");
        
        actionPanel.add(generateHeaderBtn);
        actionPanel.add(loadTemplateBtn);
        actionPanel.add(insertHeaderBtn);
        
        // Separator
        actionPanel.add(new JSeparator(SwingConstants.VERTICAL));
        
        // Execution operations
        sendCodeBtn = new JButton("Send Code");
        sendCodeBtn.setBackground(new Color(46, 125, 50));
        sendCodeBtn.setForeground(Color.WHITE);
        JButton clearBtn = new JButton("Clear Output");
        JButton connectBtn = new JButton("Connect");
        
        actionPanel.add(sendCodeBtn);
        actionPanel.add(clearBtn);
        actionPanel.add(connectBtn);
        
        clearBtn.addActionListener(e -> outputArea.setText(""));
        connectBtn.addActionListener(e -> parent.connectToEndpoint("/code"));
        
        mainToolbar.add(actionPanel, BorderLayout.CENTER);
        
        return mainToolbar;
    }
    
    private void setupEventHandlers() {
        sendCodeBtn.addActionListener(e -> sendCode());
        loadFileBtn.addActionListener(e -> loadFile());
        saveFileBtn.addActionListener(e -> saveFile());
        generateHeaderBtn.addActionListener(e -> generateCompleteCode());
        loadTemplateBtn.addActionListener(e -> loadLanguageTemplate());
        insertHeaderBtn.addActionListener(e -> insertHeaderAtCursor());
        
        languageCombo.addActionListener(e -> updateFileExtension());
        
        // Update file extension when project name changes
        projectNameField.addActionListener(e -> updateMainFileName());
        fileNameField.addActionListener(e -> updateLanguageFromExtension());
    }
    
    private void updateFileExtension() {
        String language = (String) languageCombo.getSelectedItem();
        if (language != null) {
            LanguageConfig.LanguageInfo info = LanguageConfig.getLanguageInfo(language);
            String currentName = fileNameField.getText();
            String baseName = currentName.contains(".") ? 
                currentName.substring(0, currentName.lastIndexOf('.')) : currentName;
            
            fileNameField.setText(baseName + info.extension);
        }
    }
    
    private void updateMainFileName() {
        String language = (String) languageCombo.getSelectedItem();
        if (language != null && "java".equals(language)) {
            // For Java, the file name should match the class name
            String projectName = projectNameField.getText();
            if (!projectName.isEmpty()) {
                fileNameField.setText("Main.java");
            }
        }
    }
    
    private void updateLanguageFromExtension() {
        String fileName = fileNameField.getText();
        if (fileName.contains(".")) {
            String extension = fileName.substring(fileName.lastIndexOf('.'));
            
            // Try to match extension to language
            for (String lang : LanguageConfig.getSupportedLanguages()) {
                LanguageConfig.LanguageInfo info = LanguageConfig.getLanguageInfo(lang);
                if (info.extension.equals(extension)) {
                    languageCombo.setSelectedItem(lang);
                    break;
                }
            }
        }
    }
    
    private void generateCompleteCode() {
        String language = (String) languageCombo.getSelectedItem();
        String projectName = projectNameField.getText().trim();
        String fileName = fileNameField.getText().trim();
        
        if (projectName.isEmpty()) {
            JOptionPane.showMessageDialog(this, "Please enter a project name.", "Missing Project Name", JOptionPane.WARNING_MESSAGE);
            return;
        }
        
        if (fileName.isEmpty()) {
            JOptionPane.showMessageDialog(this, "Please enter a file name.", "Missing File Name", JOptionPane.WARNING_MESSAGE);
            return;
        }
        
        if (language != null) {
            LanguageConfig.LanguageInfo info = LanguageConfig.getLanguageInfo(language);
            String header = LanguageConfig.generateHeader(language, projectName, fileName);
            String template = info.template;
            
            StringBuilder fullCode = new StringBuilder();
            fullCode.append(header);
            fullCode.append("\n");
            fullCode.append(template);
            
            // Confirm before replacing content if editor is not empty
            if (!codeEditor.getText().trim().isEmpty()) {
                int result = JOptionPane.showConfirmDialog(this, 
                    "This will replace the current code. Continue?", 
                    "Replace Code", 
                    JOptionPane.YES_NO_OPTION);
                if (result != JOptionPane.YES_OPTION) {
                    return;
                }
            }
            
            codeEditor.setText(fullCode.toString());
            codeEditor.setCaretPosition(fullCode.length()); // Move cursor to end
            parent.updateStatus("Generated " + language + " template with header");
        }
    }
    
    private void loadLanguageTemplate() {
        String language = (String) languageCombo.getSelectedItem();
        if (language != null) {
            LanguageConfig.LanguageInfo info = LanguageConfig.getLanguageInfo(language);
            
            // Confirm before replacing content if editor is not empty
            if (!codeEditor.getText().trim().isEmpty()) {
                int result = JOptionPane.showConfirmDialog(this, 
                    "This will replace the current code with a " + language + " template. Continue?", 
                    "Load Template", 
                    JOptionPane.YES_NO_OPTION);
                if (result != JOptionPane.YES_OPTION) {
                    return;
                }
            }
            
            codeEditor.setText(info.template);
            codeEditor.setCaretPosition(0);
            parent.updateStatus("Loaded " + language + " template");
        }
    }
    
    private void insertHeaderAtCursor() {
        String language = (String) languageCombo.getSelectedItem();
        String projectName = projectNameField.getText().trim();
        String fileName = fileNameField.getText().trim();
        
        if (projectName.isEmpty()) {
            JOptionPane.showMessageDialog(this, "Please enter a project name.", "Missing Project Name", JOptionPane.WARNING_MESSAGE);
            return;
        }
        
        if (fileName.isEmpty()) {
            JOptionPane.showMessageDialog(this, "Please enter a file name.", "Missing File Name", JOptionPane.WARNING_MESSAGE);
            return;
        }
        
        if (language != null) {
            String header = LanguageConfig.generateHeader(language, projectName, fileName);
            int caretPos = codeEditor.getCaretPosition();
            
            try {
                codeEditor.insert(header + "\n", caretPos);
                parent.updateStatus("Inserted " + language + " header at cursor position");
            } catch (Exception e) {
                parent.updateStatus("Error inserting header: " + e.getMessage());
            }
        }
    }
    
    private void sendCode() {
        if (!parent.isConnectedTo("/code")) {
            parent.updateStatus("Not connected to /code endpoint");
            return;
        }
        
        String projectName = projectNameField.getText();
        String fileName = fileNameField.getText();
        String code = codeEditor.getText();
        
        // Format code without metadata comments
        StringBuilder formattedCode = new StringBuilder();
        formattedCode.append(code);
        
        parent.sendToEndpoint("/code", formattedCode.toString());
        parent.updateStatus("Code sent to KodeRunner");
    }
    
    private void loadFile() {
        JFileChooser fileChooser = new JFileChooser();
        if (fileChooser.showOpenDialog(this) == JFileChooser.APPROVE_OPTION) {
            try {
                File file = fileChooser.getSelectedFile();
                String content = Files.readString(file.toPath());
                codeEditor.setText(content);
                fileNameField.setText(file.getName());
                parent.updateStatus("File loaded: " + file.getName());
            } catch (IOException e) {
                parent.updateStatus("Error loading file: " + e.getMessage());
            }
        }
    }
    
    private void saveFile() {
        JFileChooser fileChooser = new JFileChooser();
        fileChooser.setSelectedFile(new File(fileNameField.getText()));
        if (fileChooser.showSaveDialog(this) == JFileChooser.APPROVE_OPTION) {
            try {
                File file = fileChooser.getSelectedFile();
                try (FileWriter writer = new FileWriter(file)) {
                    writer.write(codeEditor.getText());
                }
                parent.updateStatus("File saved: " + file.getName());
            } catch (IOException e) {
                parent.updateStatus("Error saving file: " + e.getMessage());
            }
        }
    }
    
    private void setDefaultCode() {
        // Start with Python template as default
        String defaultCode = LanguageConfig.generateHeader("python", "TestProject", "main.py") + 
                           "\n" + LanguageConfig.getLanguageInfo("python").template;
        codeEditor.setText(defaultCode);
    }
    
    @Override
    public void handleOutput(String endpoint, String message) {
        if ("/code".equals(endpoint)) {
            outputArea.append("[CODE] " + message + "\n");
            outputArea.setCaretPosition(outputArea.getDocument().getLength());
        }
    }
}
