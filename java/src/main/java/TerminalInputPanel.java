import javax.swing.*;
import javax.swing.border.TitledBorder;
import java.awt.*;
import java.awt.event.ActionEvent;
import java.awt.event.KeyEvent;

public class TerminalInputPanel extends JPanel implements OutputHandler {
    private KodeRunnerTester parent;
    private JTextField inputField;
    private JTextArea outputArea;
    private JButton sendBtn;
    private JButton connectBtn;
    
    public TerminalInputPanel(KodeRunnerTester parent) {
        this.parent = parent;
        initializeUI();
        setupEventHandlers();
    }
    
    private void initializeUI() {
        setLayout(new BorderLayout());
        
        // Create input panel
        JPanel inputPanel = new JPanel(new BorderLayout());
        inputPanel.setBorder(new TitledBorder("Terminal Input"));
        
        inputField = new JTextField();
        inputField.setFont(new Font(Font.MONOSPACED, Font.PLAIN, 14));
        sendBtn = new JButton("Send");
        connectBtn = new JButton("Connect");
        
        JPanel buttonPanel = new JPanel(new FlowLayout());
        buttonPanel.add(connectBtn);
        buttonPanel.add(sendBtn);
        
        inputPanel.add(new JLabel("Command:"), BorderLayout.WEST);
        inputPanel.add(inputField, BorderLayout.CENTER);
        inputPanel.add(buttonPanel, BorderLayout.EAST);
        
        add(inputPanel, BorderLayout.NORTH);
        
        // Create output area
        outputArea = new JTextArea();
        outputArea.setEditable(false);
        outputArea.setFont(new Font(Font.MONOSPACED, Font.PLAIN, 12));
        outputArea.setBackground(Color.BLACK);
        outputArea.setForeground(Color.WHITE);
        JScrollPane outputScroll = new JScrollPane(outputArea);
        outputScroll.setBorder(new TitledBorder("Terminal Output"));
        
        add(outputScroll, BorderLayout.CENTER);
        
        // Add some instructions
        outputArea.setText("Terminal Input Tester\n" +
                          "===================\n" +
                          "1. Connect to the terminal input endpoint\n" +
                          "2. Type commands and press Enter or click Send\n" +
                          "3. Commands will be sent to the active process in KodeRunner\n\n");
    }
    
    private void setupEventHandlers() {
        sendBtn.addActionListener(this::sendInput);
        connectBtn.addActionListener(e -> parent.connectToEndpoint("/terminput"));
        
        // Send on Enter key
        inputField.addActionListener(this::sendInput);
        
        // Handle Ctrl+C for interruption
        inputField.getInputMap().put(KeyStroke.getKeyStroke(KeyEvent.VK_C, KeyEvent.CTRL_DOWN_MASK), "interrupt");
        inputField.getActionMap().put("interrupt", new AbstractAction() {
            @Override
            public void actionPerformed(ActionEvent e) {
                sendInput("\u0003"); // Send Ctrl+C character
            }
        });
    }
    
    private void sendInput(ActionEvent e) {
        sendInput(inputField.getText());
        inputField.setText("");
    }
    
    private void sendInput(String input) {
        if (!parent.isConnectedTo("/terminput")) {
            parent.updateStatus("Not connected to /terminput endpoint");
            return;
        }
        
        parent.sendToEndpoint("/terminput", input);
        outputArea.append("> " + input + "\n");
        outputArea.setCaretPosition(outputArea.getDocument().getLength());
    }
    
    @Override
    public void handleOutput(String endpoint, String message) {
        // Only handle messages from terminput endpoint
        if ("/terminput".equals(endpoint)) {
            outputArea.append("[TERMINPUT] " + message + "\n");
            outputArea.setCaretPosition(outputArea.getDocument().getLength());
        }
    }
}
