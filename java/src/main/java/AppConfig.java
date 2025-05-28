public class AppConfig {
    private String lookAndFeel = "Nimbus";
    private String defaultServer = "ws://localhost:8080";
    private int fontSize = 12;
    private boolean autoConnect = false;
    private String defaultProject = "TestProject";
    
    // Getters and setters
    public String getLookAndFeel() { return lookAndFeel; }
    public void setLookAndFeel(String lookAndFeel) { this.lookAndFeel = lookAndFeel; }
    
    public String getDefaultServer() { return defaultServer; }
    public void setDefaultServer(String defaultServer) { this.defaultServer = defaultServer; }
    
    public int getFontSize() { return fontSize; }
    public void setFontSize(int fontSize) { this.fontSize = fontSize; }
    
    public boolean isAutoConnect() { return autoConnect; }
    public void setAutoConnect(boolean autoConnect) { this.autoConnect = autoConnect; }
    
    public String getDefaultProject() { return defaultProject; }
    public void setDefaultProject(String defaultProject) { this.defaultProject = defaultProject; }
}
