import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import java.io.*;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;

public class ConfigManager {
    private static final String CONFIG_DIR = System.getProperty("user.home") + "/.koderunner";
    private static final String CONFIG_FILE = CONFIG_DIR + "/config.json";
    private Gson gson;
    
    public ConfigManager() {
        this.gson = new GsonBuilder().setPrettyPrinting().create();
        ensureConfigDirectory();
    }
    
    private void ensureConfigDirectory() {
        try {
            Path configPath = Paths.get(CONFIG_DIR);
            if (!Files.exists(configPath)) {
                Files.createDirectories(configPath);
            }
        } catch (IOException e) {
            System.err.println("Could not create config directory: " + e.getMessage());
        }
    }
    
    public AppConfig loadConfig() {
        try {
            Path configFile = Paths.get(CONFIG_FILE);
            if (Files.exists(configFile)) {
                String json = Files.readString(configFile);
                return gson.fromJson(json, AppConfig.class);
            }
        } catch (IOException e) {
            System.err.println("Error loading config: " + e.getMessage());
        }
        
        // Return default config if file doesn't exist or error occurred
        return createDefaultConfig();
    }
    
    public void saveConfig(AppConfig config) {
        try {
            String json = gson.toJson(config);
            Files.writeString(Paths.get(CONFIG_FILE), json);
        } catch (IOException e) {
            System.err.println("Error saving config: " + e.getMessage());
        }
    }
    
    private AppConfig createDefaultConfig() {
        AppConfig config = new AppConfig();
        config.setLookAndFeel("Nimbus");
        config.setDefaultServer("ws://localhost:8080");
        config.setFontSize(12);
        config.setAutoConnect(false);
        config.setDefaultProject("TestProject");
        return config;
    }
}
