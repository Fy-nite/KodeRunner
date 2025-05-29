import javax.swing.*;
import javax.swing.border.TitledBorder;
import java.awt.*;
import com.google.gson.Gson;
import java.util.HashMap;
import java.util.Map;

public class ProcessControlPanel extends JPanel implements OutputHandler {
    private KodeRunnerTester parent;
    private JTextArea outputArea;
    private JButton stopBtn;
    private JButton connectBtn;
    private Gson gson;
    
    public ProcessControlPanel(KodeRunnerTester parent) {
        this.parent = parent;
        this.gson = new Gson();
        initializeUI();
        setupEventHandlers();
    }
    
    private void initializeUI() {
        setLayout(new BorderLayout());
        
        // Create control panel
        JPanel controlPanel = new JPanel(new FlowLayout());
        controlPanel.setBorder(new TitledBorder("Process Control"));
        
        connectBtn = new JButton("Connect to Stop Endpoint");
        stopBtn = new JButton("Stop All Processes");
        stopBtn.setBackground(Color.RED);
        stopBtn.setForeground(Color.WHITE);
        
        // Add terminal creator connection button
        JButton terminalCreatorBtn = new JButton("Connect to Terminal Creator");
        terminalCreatorBtn.setBackground(new Color(255, 152, 0));
        terminalCreatorBtn.setForeground(Color.WHITE);
        terminalCreatorBtn.addActionListener(e -> parent.connectToEndpoint("/terminal/create"));
        
        controlPanel.add(connectBtn);
        controlPanel.add(stopBtn);
        controlPanel.add(terminalCreatorBtn);
        
        add(controlPanel, BorderLayout.NORTH);
        
        // Create output area
        outputArea = new JTextArea();
        outputArea.setEditable(false);
        outputArea.setFont(new Font(Font.MONOSPACED, Font.PLAIN, 12));
        outputArea.setBackground(Color.BLACK);
        outputArea.setForeground(Color.YELLOW);
        JScrollPane outputScroll = new JScrollPane(outputArea);
        outputScroll.setBorder(new TitledBorder("Process Control Output"));
        
        add(outputScroll, BorderLayout.CENTER);
        
        // Add instructions
        outputArea.setText("Process Control Panel\n" +
                          "====================\n" +
                          "Use this panel to stop running processes in KodeRunner.\n" +
                          "This is useful when processes hang or need to be terminated.\n" +
                          "Also supports connection to terminal creator for shared sessions.\n\n");
    }
    
    private void setupEventHandlers() {
        connectBtn.addActionListener(e -> parent.connectToEndpoint("/stop"));
        stopBtn.addActionListener(e -> stopAllProcesses());
    }
    
    private void stopAllProcesses() {
        if (!parent.isConnectedTo("/stop")) {
            parent.updateStatus("Not connected to /stop endpoint");
            return;
        }
        
        Map<String, Boolean> stopRequest = new HashMap<>();
        stopRequest.put("stopped", true);
        
        String json = gson.toJson(stopRequest);
        parent.sendToEndpoint("/stop", json);
        parent.updateStatus("Stop request sent");
        
        outputArea.append("[STOP] Stop request sent to KodeRunner\n");
        outputArea.setCaretPosition(outputArea.getDocument().getLength());
    }
    
    @Override
    public void handleOutput(String endpoint, String message) {
        // Only handle messages from stop endpoint or terminal creator
        if ("/stop".equals(endpoint)) {
            outputArea.append("[STOP] " + message + "\n");
            outputArea.setCaretPosition(outputArea.getDocument().getLength());
        } else if ("/terminal/create".equals(endpoint)) {
            outputArea.append("[TERMINAL-CREATE] " + message + "\n");
            outputArea.setCaretPosition(outputArea.getDocument().getLength());
        }
    }
}
