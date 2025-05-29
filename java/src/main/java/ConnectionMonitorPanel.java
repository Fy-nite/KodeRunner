import javax.swing.*;
import javax.swing.border.TitledBorder;
import java.awt.*;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.concurrent.ConcurrentHashMap;

public class ConnectionMonitorPanel extends JPanel implements OutputHandler {
    private KodeRunnerTester parent;
    private JTextArea logArea;
    private JCheckBox autoScrollCheckBox;
    private JButton clearLogBtn;
    private JButton connectAllBtn;
    private JButton disconnectAllBtn;
    private SimpleDateFormat timeFormat;
    private ConcurrentHashMap<String, Long> lastMessageTime;
    private static final long DUPLICATE_THRESHOLD_MS = 100; // Prevent duplicates within 100ms
    
    public ConnectionMonitorPanel(KodeRunnerTester parent) {
        this.parent = parent;
        this.timeFormat = new SimpleDateFormat("HH:mm:ss.SSS");
        this.lastMessageTime = new ConcurrentHashMap<>();
        initializeUI();
        setupEventHandlers();
    }
    
    private void initializeUI() {
        setLayout(new BorderLayout());
        
        // Control panel
        JPanel controlPanel = new JPanel(new FlowLayout(FlowLayout.LEFT));
        controlPanel.setBorder(new TitledBorder("Connection Control"));
        
        connectAllBtn = new JButton("Connect All");
        disconnectAllBtn = new JButton("Disconnect All");
        clearLogBtn = new JButton("Clear Log");
        autoScrollCheckBox = new JCheckBox("Auto-scroll", true);
        
        controlPanel.add(connectAllBtn);
        controlPanel.add(disconnectAllBtn);
        controlPanel.add(clearLogBtn);
        controlPanel.add(autoScrollCheckBox);
        
        add(controlPanel, BorderLayout.NORTH);
        
        // Log area
        logArea = new JTextArea();
        logArea.setEditable(false);
        logArea.setFont(new Font(Font.MONOSPACED, Font.PLAIN, 11));
        logArea.setBackground(new Color(30, 30, 30));
        logArea.setForeground(Color.CYAN);
        
        JScrollPane scrollPane = new JScrollPane(logArea);
        scrollPane.setBorder(new TitledBorder("Connection Log"));
        add(scrollPane, BorderLayout.CENTER);
        
        // Initial message
        appendToLog("Connection Monitor initialized. Waiting for WebSocket activity...");
    }
    
    private void setupEventHandlers() {
        connectAllBtn.addActionListener(e -> parent.connectToAllEndpoints());
        disconnectAllBtn.addActionListener(e -> parent.disconnectFromAllEndpoints());
        clearLogBtn.addActionListener(e -> {
            logArea.setText("");
            lastMessageTime.clear();
            appendToLog("Log cleared at " + timeFormat.format(new Date()));
        });
    }
    
    private void appendToLog(String message) {
        SwingUtilities.invokeLater(() -> {
            String timestamp = timeFormat.format(new Date());
            logArea.append("[" + timestamp + "] " + message + "\n");
            
            if (autoScrollCheckBox.isSelected()) {
                logArea.setCaretPosition(logArea.getDocument().getLength());
            }
        });
    }
    
    private boolean isDuplicateMessage(String endpoint, String message) {
        String key = endpoint + ":" + message.hashCode();
        long currentTime = System.currentTimeMillis();
        Long lastTime = lastMessageTime.get(key);
        
        if (lastTime != null && (currentTime - lastTime) < DUPLICATE_THRESHOLD_MS) {
            return true;
        }
        
        lastMessageTime.put(key, currentTime);
        return false;
    }
    
    @Override
    public void handleOutput(String endpoint, String message) {
        // Prevent duplicate logging
        if (isDuplicateMessage(endpoint, message)) {
            return;
        }
        
        String logMessage = String.format("RECV [%s]: %s", endpoint, 
            message.length() > 200 ? message.substring(0, 197) + "..." : message);
        appendToLog(logMessage);
    }
}
