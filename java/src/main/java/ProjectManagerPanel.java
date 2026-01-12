import javax.swing.*;
import javax.swing.border.TitledBorder;
import javax.swing.tree.DefaultMutableTreeNode;
import javax.swing.tree.DefaultTreeModel;
import java.awt.*;
import java.awt.event.MouseAdapter;
import java.awt.event.MouseEvent;
import java.io.File;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.List;
import java.util.stream.Stream;
import com.google.gson.Gson;
import com.google.gson.JsonObject;

public class ProjectManagerPanel extends JPanel implements OutputHandler {
    private KodeRunnerTester parent;
    private JTree projectTree;
    private DefaultTreeModel treeModel;
    private JTextArea projectInfoArea;
    private JButton refreshBtn;
    private JButton newProjectBtn;
    private JButton deleteProjectBtn;
    private JButton importProjectBtn;
    private JButton exportProjectBtn;
    private JButton openInEditorBtn;
    private JTextField projectsPathField;
    private Gson gson;
    
    public ProjectManagerPanel(KodeRunnerTester parent) {
        this.parent = parent;
        this.gson = new Gson();
        initializeUI();
        setupEventHandlers();
        refreshProjectTree();
    }
    
    private void initializeUI() {
        setLayout(new BorderLayout());
        
        // Top toolbar
        JPanel toolbar = createToolbar();
        add(toolbar, BorderLayout.NORTH);
        
        // Create split pane
        JSplitPane splitPane = new JSplitPane(JSplitPane.HORIZONTAL_SPLIT);
        
        // Left side - Project tree
        JPanel treePanel = createProjectTreePanel();
        splitPane.setLeftComponent(treePanel);
        
        // Right side - Project details
        JPanel detailsPanel = createDetailsPanel();
        splitPane.setRightComponent(detailsPanel);
        
        splitPane.setDividerLocation(300);
        add(splitPane, BorderLayout.CENTER);
    }
    
    private JPanel createToolbar() {
        JPanel toolbar = new JPanel(new FlowLayout(FlowLayout.LEFT));
        toolbar.setBorder(new TitledBorder("Project Management"));
        
        toolbar.add(new JLabel("Projects Path:"));
        projectsPathField = new JTextField("koderunner/Projects", 20);
        toolbar.add(projectsPathField);
        
        refreshBtn = new JButton("🔄 Refresh");
        newProjectBtn = new JButton("➕ New Project");
        deleteProjectBtn = new JButton("🗑️ Delete");
        importProjectBtn = new JButton("📥 Import");
        exportProjectBtn = new JButton("📤 Export");
        
        toolbar.add(refreshBtn);
        toolbar.add(newProjectBtn);
        toolbar.add(deleteProjectBtn);
        toolbar.add(importProjectBtn);
        toolbar.add(exportProjectBtn);
        
        return toolbar;
    }
    
    private JPanel createProjectTreePanel() {
        JPanel panel = new JPanel(new BorderLayout());
        panel.setBorder(new TitledBorder("Projects"));
        
        // Create tree
        DefaultMutableTreeNode root = new DefaultMutableTreeNode("Projects");
        treeModel = new DefaultTreeModel(root);
        projectTree = new JTree(treeModel);
        projectTree.setRootVisible(true);
        projectTree.setShowsRootHandles(true);
        
        JScrollPane treeScroll = new JScrollPane(projectTree);
        panel.add(treeScroll, BorderLayout.CENTER);
        
        // Bottom buttons for tree actions
        JPanel treeButtons = new JPanel(new FlowLayout());
        openInEditorBtn = new JButton("Open in Editor");
        openInEditorBtn.setEnabled(false);
        
        JButton runProjectBtn = new JButton("Run Project");
        runProjectBtn.setEnabled(false);
        
        treeButtons.add(openInEditorBtn);
        treeButtons.add(runProjectBtn);
        panel.add(treeButtons, BorderLayout.SOUTH);
        
        return panel;
    }
    
    private JPanel createDetailsPanel() {
        JPanel panel = new JPanel(new BorderLayout());
        panel.setBorder(new TitledBorder("Project Details"));
        
        projectInfoArea = new JTextArea();
        projectInfoArea.setEditable(false);
        projectInfoArea.setFont(new Font(Font.MONOSPACED, Font.PLAIN, 12));
        projectInfoArea.setText("Select a project to view details...");
        
        JScrollPane infoScroll = new JScrollPane(projectInfoArea);
        panel.add(infoScroll, BorderLayout.CENTER);
        
        return panel;
    }
    
    private void setupEventHandlers() {
        refreshBtn.addActionListener(e -> refreshProjectTree());
        newProjectBtn.addActionListener(e -> createNewProject());
        deleteProjectBtn.addActionListener(e -> deleteSelectedProject());
        importProjectBtn.addActionListener(e -> importProject());
        exportProjectBtn.addActionListener(e -> exportSelectedProject());
        openInEditorBtn.addActionListener(e -> openProjectInEditor());
        
        projectTree.addTreeSelectionListener(e -> updateProjectDetails());
        
        projectTree.addMouseListener(new MouseAdapter() {
            @Override
            public void mouseClicked(MouseEvent e) {
                if (e.getClickCount() == 2) {
                    openProjectInEditor();
                }
            }
        });
    }
    
    private void refreshProjectTree() {
        String projectsPath = projectsPathField.getText();
        File projectsDir = new File(projectsPath);
        
        DefaultMutableTreeNode root = new DefaultMutableTreeNode("Projects (" + projectsPath + ")");
        
        if (projectsDir.exists() && projectsDir.isDirectory()) {
            File[] projects = projectsDir.listFiles(File::isDirectory);
            if (projects != null) {
                for (File project : projects) {
                    DefaultMutableTreeNode projectNode = new DefaultMutableTreeNode(project.getName());
                    
                    // Add files to project node
                    File[] files = project.listFiles();
                    if (files != null) {
                        for (File file : files) {
                            if (file.isFile()) {
                                String icon = getFileIcon(file.getName());
                                projectNode.add(new DefaultMutableTreeNode(icon + " " + file.getName()));
                            }
                        }
                    }
                    
                    root.add(projectNode);
                }
            }
        }
        
        treeModel.setRoot(root);
        treeModel.reload();
        
        // Enable/disable buttons based on selection
        updateButtonStates();
        
        parent.updateStatus("Refreshed project tree - found " + root.getChildCount() + " projects");
    }
    
    private String getFileIcon(String fileName) {
        String ext = fileName.substring(fileName.lastIndexOf('.') + 1).toLowerCase();
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
    
    private void updateProjectDetails() {
        var selectedPath = projectTree.getSelectionPath();
        if (selectedPath == null || selectedPath.getPathCount() < 2) {
            projectInfoArea.setText("Select a project to view details...");
            updateButtonStates();
            return;
        }
        
        String projectName = selectedPath.getPathComponent(1).toString();
        String projectPath = projectsPathField.getText() + File.separator + projectName;
        File projectDir = new File(projectPath);
        
        if (!projectDir.exists()) {
            projectInfoArea.setText("Project directory not found: " + projectPath);
            return;
        }
        
        StringBuilder details = new StringBuilder();
        details.append("Project: ").append(projectName).append("\n");
        details.append("Path: ").append(projectPath).append("\n");
        details.append("Created: ").append(new java.util.Date(projectDir.lastModified())).append("\n\n");
        
        // Count files by type
        File[] files = projectDir.listFiles();
        if (files != null) {
            int totalFiles = 0;
            int codeFiles = 0;
            int configFiles = 0;
            long totalSize = 0;
            
            for (File file : files) {
                if (file.isFile()) {
                    totalFiles++;
                    totalSize += file.length();
                    
                    String name = file.getName().toLowerCase();
                    if (name.endsWith(".py") || name.endsWith(".js") || name.endsWith(".java") || 
                        name.endsWith(".cs") || name.endsWith(".c") || name.endsWith(".h")) {
                        codeFiles++;
                    } else if (name.endsWith(".json") || name.endsWith(".config") || name.endsWith(".xml")) {
                        configFiles++;
                    }
                }
            }
            
            details.append("Files: ").append(totalFiles).append(" total\n");
            details.append("Code files: ").append(codeFiles).append("\n");
            details.append("Config files: ").append(configFiles).append("\n");
            details.append("Total size: ").append(formatBytes(totalSize)).append("\n\n");
        }
        
        // Try to detect main file and language
        String detectedLanguage = detectProjectLanguage(projectDir);
        String mainFile = detectMainFile(projectDir, detectedLanguage);
        
        details.append("Detected Language: ").append(detectedLanguage != null ? detectedLanguage : "Unknown").append("\n");
        details.append("Main File: ").append(mainFile != null ? mainFile : "Not found").append("\n\n");
        
        // Show file list
        details.append("Files:\n");
        if (files != null) {
            for (File file : files) {
                if (file.isFile()) {
                    details.append("  ").append(getFileIcon(file.getName()))
                           .append(" ").append(file.getName())
                           .append(" (").append(formatBytes(file.length())).append(")\n");
                }
            }
        }
        
        projectInfoArea.setText(details.toString());
        projectInfoArea.setCaretPosition(0);
        
        updateButtonStates();
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
        
        // Look for common main file names
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
        
        // If no standard main file found, return first file of the type
        String extension = switch (language) {
            case "python" -> ".py";
            case "nodejs" -> ".js";
            case "java" -> ".java";
            case "csharp" -> ".cs";
            case "c" -> ".c";
            default -> "";
        };
        
        for (File file : files) {
            if (file.getName().endsWith(extension)) {
                return file.getName();
            }
        }
        
        return null;
    }
    
    private String formatBytes(long bytes) {
        if (bytes < 1024) return bytes + " B";
        if (bytes < 1024 * 1024) return String.format("%.1f KB", bytes / 1024.0);
        return String.format("%.1f MB", bytes / (1024.0 * 1024.0));
    }
    
    private void updateButtonStates() {
        var selectedPath = projectTree.getSelectionPath();
        boolean projectSelected = selectedPath != null && selectedPath.getPathCount() >= 2;
        
        deleteProjectBtn.setEnabled(projectSelected);
        exportProjectBtn.setEnabled(projectSelected);
        openInEditorBtn.setEnabled(projectSelected);
    }
    
    private void createNewProject() {
        String projectName = JOptionPane.showInputDialog(this, "Enter project name:", "New Project", JOptionPane.PLAIN_MESSAGE);
        if (projectName == null || projectName.trim().isEmpty()) {
            return;
        }
        
        // Select language
        String[] languages = {"python", "nodejs", "java", "csharp", "c"};
        String language = (String) JOptionPane.showInputDialog(this, "Select language:", "New Project", 
            JOptionPane.PLAIN_MESSAGE, null, languages, "python");
        
        if (language == null) return;
        
        try {
            String projectPath = projectsPathField.getText() + File.separator + projectName.trim();
            File projectDir = new File(projectPath);
            
            if (projectDir.exists()) {
                JOptionPane.showMessageDialog(this, "Project already exists!", "Error", JOptionPane.ERROR_MESSAGE);
                return;
            }
            
            projectDir.mkdirs();
            
            // Create initial files based on language
            createInitialProjectFiles(projectDir, projectName.trim(), language);
            
            refreshProjectTree();
            parent.updateStatus("Created new project: " + projectName);
            
        } catch (Exception e) {
            JOptionPane.showMessageDialog(this, "Error creating project: " + e.getMessage(), "Error", JOptionPane.ERROR_MESSAGE);
        }
    }
    
    private void createInitialProjectFiles(File projectDir, String projectName, String language) throws IOException {
        switch (language) {
            case "python":
                Files.writeString(projectDir.toPath().resolve("main.py"), 
                    "# " + projectName + "\n# Python project\n\ndef main():\n    print(\"Hello from " + projectName + "!\")\n\nif __name__ == \"__main__\":\n    main()\n");
                break;
            case "nodejs":
                Files.writeString(projectDir.toPath().resolve("index.js"), 
                    "// " + projectName + "\n// Node.js project\n\nconsole.log(\"Hello from " + projectName + "!\");\n");
                Files.writeString(projectDir.toPath().resolve("package.json"), 
                    "{\n  \"name\": \"" + projectName.toLowerCase() + "\",\n  \"version\": \"1.0.0\",\n  \"main\": \"index.js\"\n}\n");
                break;
            case "java":
                Files.writeString(projectDir.toPath().resolve("Main.java"), 
                    "// " + projectName + "\n// Java project\n\npublic class Main {\n    public static void main(String[] args) {\n        System.out.println(\"Hello from " + projectName + "!\");\n    }\n}\n");
                break;
            case "csharp":
                Files.writeString(projectDir.toPath().resolve("Program.cs"), 
                    "// " + projectName + "\n// C# project\n\nusing System;\n\nclass Program {\n    static void Main() {\n        Console.WriteLine(\"Hello from " + projectName + "!\");\n    }\n}\n");
                break;
            case "c":
                Files.writeString(projectDir.toPath().resolve("main.c"), 
                    "// " + projectName + "\n// C project\n\n#include <stdio.h>\n\nint main() {\n    printf(\"Hello from " + projectName + "!\\n\");\n    return 0;\n}\n");
                break;
        }
    }
    
    private void deleteSelectedProject() {
        var selectedPath = projectTree.getSelectionPath();
        if (selectedPath == null || selectedPath.getPathCount() < 2) return;
        
        String projectName = selectedPath.getPathComponent(1).toString();
        
        int result = JOptionPane.showConfirmDialog(this, 
            "Are you sure you want to delete project '" + projectName + "'?\nThis action cannot be undone.", 
            "Delete Project", JOptionPane.YES_NO_OPTION, JOptionPane.WARNING_MESSAGE);
        
        if (result == JOptionPane.YES_OPTION) {
            try {
                String projectPath = projectsPathField.getText() + File.separator + projectName;
                File projectDir = new File(projectPath);
                deleteDirectory(projectDir);
                refreshProjectTree();
                parent.updateStatus("Deleted project: " + projectName);
            } catch (Exception e) {
                JOptionPane.showMessageDialog(this, "Error deleting project: " + e.getMessage(), "Error", JOptionPane.ERROR_MESSAGE);
            }
        }
    }
    
    private void deleteDirectory(File dir) throws IOException {
        if (dir.isDirectory()) {
            File[] files = dir.listFiles();
            if (files != null) {
                for (File file : files) {
                    deleteDirectory(file);
                }
            }
        }
        if (!dir.delete()) {
            throw new IOException("Failed to delete: " + dir.getAbsolutePath());
        }
    }
    
    private void importProject() {
        JFileChooser fileChooser = new JFileChooser();
        fileChooser.setFileFilter(new javax.swing.filechooser.FileNameExtensionFilter("KodeRunner Projects (*.KRproject)", "KRproject"));
        
        if (fileChooser.showOpenDialog(this) == JFileChooser.APPROVE_OPTION) {
            // Implementation would depend on KodeRunner's import format
            parent.updateStatus("Import functionality not yet implemented");
        }
    }
    
    private void exportSelectedProject() {
        var selectedPath = projectTree.getSelectionPath();
        if (selectedPath == null || selectedPath.getPathCount() < 2) return;
        
        String projectName = selectedPath.getPathComponent(1).toString();
        
        JFileChooser fileChooser = new JFileChooser();
        fileChooser.setSelectedFile(new File(projectName + ".KRproject"));
        fileChooser.setFileFilter(new javax.swing.filechooser.FileNameExtensionFilter("KodeRunner Projects (*.KRproject)", "KRproject"));
        
        if (fileChooser.showSaveDialog(this) == JFileChooser.APPROVE_OPTION) {
            // Implementation would depend on KodeRunner's export format
            parent.updateStatus("Export functionality not yet implemented");
        }
    }
    
    private void openProjectInEditor() {
        var selectedPath = projectTree.getSelectionPath();
        if (selectedPath == null || selectedPath.getPathCount() < 2) return;
        
        String projectName = selectedPath.getPathComponent(1).toString();
        
        // Switch to Code Editor tab and populate with project
        SwingUtilities.invokeLater(() -> {
            JTabbedPane tabbedPane = (JTabbedPane) getParent();
            for (int i = 0; i < tabbedPane.getTabCount(); i++) {
                if ("Code Editor".equals(tabbedPane.getTitleAt(i))) {
                    tabbedPane.setSelectedIndex(i);
                    
                    Component component = tabbedPane.getComponentAt(i);
                    if (component instanceof CodeEditorPanel) {
                        ((CodeEditorPanel) component).loadProject(projectName);
                    }
                    break;
                }
            }
        });
        
        parent.updateStatus("Opened project in editor: " + projectName);
    }
    
    @Override
    public void handleOutput(String endpoint, String message) {
        // Handle any relevant WebSocket messages for project management
    }
}
