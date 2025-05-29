import javax.swing.*;
import javax.swing.border.TitledBorder;
import java.awt.*;
import java.awt.event.ActionEvent;
import java.awt.event.ActionListener;
import java.text.SimpleDateFormat;
import java.util.*;
import java.util.List;
import java.util.concurrent.ConcurrentLinkedQueue;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;

public class PerformanceMonitorPanel extends JPanel implements OutputHandler {
    private KodeRunnerTester parent;
    private JTextArea metricsArea;
    private JButton startMonitoringBtn;
    private JButton stopMonitoringBtn;
    private JButton clearBtn;
    private JCheckBox autoScrollCheckBox;
    private JSpinner updateIntervalSpinner;
    private JProgressBar cpuProgressBar;
    private JProgressBar memoryProgressBar;
    private JLabel connectionCountLabel;
    private JLabel uptimeLabel;
    private ScheduledExecutorService monitoringExecutor;
    private SimpleDateFormat timeFormat;
    private Queue<String> recentMetrics;
    private long startTime;
    private int maxMetricsHistory = 1000;
    
    public PerformanceMonitorPanel(KodeRunnerTester parent) {
        this.parent = parent;
        this.timeFormat = new SimpleDateFormat("HH:mm:ss");
        this.recentMetrics = new ConcurrentLinkedQueue<>();
        this.startTime = System.currentTimeMillis();
        initializeUI();
        setupEventHandlers();
    }
    
    private void initializeUI() {
        setLayout(new BorderLayout());
        
        // Control panel
        JPanel controlPanel = createControlPanel();
        add(controlPanel, BorderLayout.NORTH);
        
        // Main content
        JSplitPane splitPane = new JSplitPane(JSplitPane.VERTICAL_SPLIT);
        
        // Top - Real-time metrics
        JPanel metricsPanel = createMetricsPanel();
        splitPane.setTopComponent(metricsPanel);
        
        // Bottom - Detailed log
        JPanel logPanel = createLogPanel();
        splitPane.setBottomComponent(logPanel);
        
        splitPane.setDividerLocation(200);
        add(splitPane, BorderLayout.CENTER);
    }
    
    private JPanel createControlPanel() {
        JPanel panel = new JPanel(new FlowLayout(FlowLayout.LEFT));
        panel.setBorder(new TitledBorder("Performance Monitoring"));
        
        startMonitoringBtn = new JButton("▶️ Start Monitoring");
        stopMonitoringBtn = new JButton("⏹️ Stop Monitoring");
        clearBtn = new JButton("🗑️ Clear");
        
        panel.add(new JLabel("Update Interval:"));
        updateIntervalSpinner = new JSpinner(new SpinnerNumberModel(2, 1, 30, 1));
        panel.add(updateIntervalSpinner);
        panel.add(new JLabel("seconds"));
        
        panel.add(startMonitoringBtn);
        panel.add(stopMonitoringBtn);
        panel.add(clearBtn);
        
        autoScrollCheckBox = new JCheckBox("Auto-scroll", true);
        panel.add(autoScrollCheckBox);
        
        stopMonitoringBtn.setEnabled(false);
        
        return panel;
    }
    
    private JPanel createMetricsPanel() {
        JPanel panel = new JPanel(new GridBagLayout());
        panel.setBorder(new TitledBorder("Real-time Metrics"));
        GridBagConstraints gbc = new GridBagConstraints();
        gbc.insets = new Insets(5, 5, 5, 5);
        gbc.anchor = GridBagConstraints.WEST;
        
        // Connection count
        gbc.gridx = 0; gbc.gridy = 0;
        panel.add(new JLabel("Active Connections:"), gbc);
        gbc.gridx = 1; gbc.weightx = 1.0; gbc.fill = GridBagConstraints.HORIZONTAL;
        connectionCountLabel = new JLabel("0");
        connectionCountLabel.setFont(connectionCountLabel.getFont().deriveFont(Font.BOLD, 14f));
        panel.add(connectionCountLabel, gbc);
        
        // Uptime
        gbc.gridx = 0; gbc.gridy = 1; gbc.weightx = 0; gbc.fill = GridBagConstraints.NONE;
        panel.add(new JLabel("Client Uptime:"), gbc);
        gbc.gridx = 1; gbc.weightx = 1.0; gbc.fill = GridBagConstraints.HORIZONTAL;
        uptimeLabel = new JLabel("00:00:00");
        uptimeLabel.setFont(uptimeLabel.getFont().deriveFont(Font.BOLD, 14f));
        panel.add(uptimeLabel, gbc);
        
        // CPU usage (simulated)
        gbc.gridx = 0; gbc.gridy = 2; gbc.weightx = 0; gbc.fill = GridBagConstraints.NONE;
        panel.add(new JLabel("CPU Usage:"), gbc);
        gbc.gridx = 1; gbc.weightx = 1.0; gbc.fill = GridBagConstraints.HORIZONTAL;
        cpuProgressBar = new JProgressBar(0, 100);
        cpuProgressBar.setStringPainted(true);
        cpuProgressBar.setValue(0);
        panel.add(cpuProgressBar, gbc);
        
        // Memory usage (simulated)
        gbc.gridx = 0; gbc.gridy = 3; gbc.weightx = 0; gbc.fill = GridBagConstraints.NONE;
        panel.add(new JLabel("Memory Usage:"), gbc);
        gbc.gridx = 1; gbc.weightx = 1.0; gbc.fill = GridBagConstraints.HORIZONTAL;
        memoryProgressBar = new JProgressBar(0, 100);
        memoryProgressBar.setStringPainted(true);
        memoryProgressBar.setValue(0);
        panel.add(memoryProgressBar, gbc);
        
        return panel;
    }
    
    private JPanel createLogPanel() {
        JPanel panel = new JPanel(new BorderLayout());
        panel.setBorder(new TitledBorder("Performance Log"));
        
        metricsArea = new JTextArea();
        metricsArea.setEditable(false);
        metricsArea.setFont(new Font(Font.MONOSPACED, Font.PLAIN, 11));
        metricsArea.setBackground(new Color(45, 45, 45));
        metricsArea.setForeground(Color.GREEN);
        
        JScrollPane scrollPane = new JScrollPane(metricsArea);
        panel.add(scrollPane, BorderLayout.CENTER);
        
        // Initial message
        appendToLog("Performance Monitor initialized. Click 'Start Monitoring' to begin.");
        
        return panel;
    }
    
    private void setupEventHandlers() {
        startMonitoringBtn.addActionListener(e -> startMonitoring());
        stopMonitoringBtn.addActionListener(e -> stopMonitoring());
        clearBtn.addActionListener(e -> clearLog());
    }
    
    private void startMonitoring() {
        if (monitoringExecutor != null && !monitoringExecutor.isShutdown()) {
            return;
        }
        
        int interval = (Integer) updateIntervalSpinner.getValue();
        monitoringExecutor = Executors.newScheduledThreadPool(1);
        
        monitoringExecutor.scheduleAtFixedRate(this::updateMetrics, 0, interval, TimeUnit.SECONDS);
        
        startMonitoringBtn.setEnabled(false);
        stopMonitoringBtn.setEnabled(true);
        updateIntervalSpinner.setEnabled(false);
        
        appendToLog("Started performance monitoring (interval: " + interval + "s)");
        parent.updateStatus("Performance monitoring started");
    }
    
    private void stopMonitoring() {
        if (monitoringExecutor != null) {
            monitoringExecutor.shutdown();
            try {
                if (!monitoringExecutor.awaitTermination(1, TimeUnit.SECONDS)) {
                    monitoringExecutor.shutdownNow();
                }
            } catch (InterruptedException e) {
                monitoringExecutor.shutdownNow();
                Thread.currentThread().interrupt();
            }
        }
        
        startMonitoringBtn.setEnabled(true);
        stopMonitoringBtn.setEnabled(false);
        updateIntervalSpinner.setEnabled(true);
        
        appendToLog("Stopped performance monitoring");
        parent.updateStatus("Performance monitoring stopped");
    }
    
    private void updateMetrics() {
        SwingUtilities.invokeLater(() -> {
            try {
                // Update uptime
                long uptimeMs = System.currentTimeMillis() - startTime;
                long hours = uptimeMs / (1000 * 60 * 60);
                long minutes = (uptimeMs % (1000 * 60 * 60)) / (1000 * 60);
                long seconds = (uptimeMs % (1000 * 60)) / 1000;
                uptimeLabel.setText(String.format("%02d:%02d:%02d", hours, minutes, seconds));
                
                // Count active connections
                int connectionCount = countActiveConnections();
                connectionCountLabel.setText(String.valueOf(connectionCount));
                
                // Simulate CPU and memory usage
                int cpuUsage = (int) (Math.random() * 20) + (connectionCount * 5); // Simulate load based on connections
                int memoryUsage = (int) (Math.random() * 15) + 30 + (connectionCount * 3); // Base memory usage
                
                cpuUsage = Math.min(cpuUsage, 100);
                memoryUsage = Math.min(memoryUsage, 100);
                
                cpuProgressBar.setValue(cpuUsage);
                cpuProgressBar.setString(cpuUsage + "%");
                
                memoryProgressBar.setValue(memoryUsage);
                memoryProgressBar.setString(memoryUsage + "%");
                
                // Update colors based on usage
                updateProgressBarColor(cpuProgressBar, cpuUsage);
                updateProgressBarColor(memoryProgressBar, memoryUsage);
                
                // Log metrics
                String timestamp = timeFormat.format(new Date());
                String logEntry = String.format("[%s] Connections: %d, CPU: %d%%, Memory: %d%%, Uptime: %02d:%02d:%02d",
                    timestamp, connectionCount, cpuUsage, memoryUsage, hours, minutes, seconds);
                
                appendToLog(logEntry);
                
            } catch (Exception e) {
                appendToLog("Error updating metrics: " + e.getMessage());
            }
        });
    }
    
    private void updateProgressBarColor(JProgressBar bar, int value) {
        Color color;
        if (value < 50) {
            color = Color.GREEN;
        } else if (value < 80) {
            color = Color.ORANGE;
        } else {
            color = Color.RED;
        }
        bar.setForeground(color);
    }
    
    private int countActiveConnections() {
        // Count connections through the parent's connection manager
        // This is a simplified version - in reality, you'd query the actual connection count
        int count = 0;
        try {
            if (parent.isConnectedTo("/code")) count++;
            if (parent.isConnectedTo("/PMS")) count++;
            if (parent.isConnectedTo("/terminput")) count++;
            if (parent.isConnectedTo("/stop")) count++;
            if (parent.isConnectedTo("/terminal/create")) count++;
        } catch (Exception e) {
            // Ignore connection check errors
        }
        return count;
    }
    
    private void clearLog() {
        metricsArea.setText("");
        recentMetrics.clear();
        appendToLog("Performance log cleared at " + timeFormat.format(new Date()));
    }
    
    private void appendToLog(String message) {
        SwingUtilities.invokeLater(() -> {
            metricsArea.append(message + "\n");
            
            // Add to recent metrics queue
            recentMetrics.offer(message);
            
            // Keep only recent metrics
            while (recentMetrics.size() > maxMetricsHistory) {
                recentMetrics.poll();
            }
            
            // Auto-scroll if enabled
            if (autoScrollCheckBox.isSelected()) {
                metricsArea.setCaretPosition(metricsArea.getDocument().getLength());
            }
        });
    }
    
    @Override
    public void handleOutput(String endpoint, String message) {
        // Only log if monitoring is active and avoid duplicates
        if (monitoringExecutor != null && !monitoringExecutor.isShutdown()) {
            String timestamp = timeFormat.format(new Date());
            String logEntry = String.format("[%s] Activity on %s: %d bytes", 
                timestamp, endpoint, message.length());
            appendToLog(logEntry);
        }
    }
    
    // Cleanup when panel is destroyed
    public void cleanup() {
        stopMonitoring();
    }
}
