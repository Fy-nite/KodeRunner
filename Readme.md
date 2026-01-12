# KodeRunner

Welcome to KodeRunner V3 - A WebSocket-based code execution server platform.

KodeRunner is an interactive server that provides VSCode-like functionality for remote clients, specifically designed for integration with platforms like Resonite. It allows users to run 15+ programming languages from any application that supports WebSocket connections.

## Features

- **Multi-Language Support**: Execute code in 15+ programming languages including Python, C#, JavaScript, C, Java, WebAssembly, and more
- **WebSocket API**: Real-time communication for interactive development experiences
- **Sandbox Security**: Secure code execution with configurable resource limits and command filtering
- **Project Management**: Organize, import, and export projects with metadata
- **Terminal Interface**: Built-in terminal with command processing and output streaming
- **Shared Terminal Sessions**: Collaborative development with multiple users sharing terminal sessions
- **Plugin System**: Extensible architecture for adding new language support
- **CLI Support**: Command-line interface for server management and project execution
- **Desktop Client**: Enhanced Java Swing client with unified development environment

## Desktop Client Features

The KodeRunner Desktop Tester provides a comprehensive development environment:

### Unified Development Tab
- **File Browser**: Visual project and file management with tree view
- **Code Editor**: Syntax-aware editor with auto-save functionality
- **Integrated PMS**: Build and run controls directly in the editor
- **Project Settings**: Language detection, build configuration, and output settings
- **Real-time Output**: Live feedback from build and execution processes

### Additional Tabs
- **Terminal Input**: Send commands to running processes
- **Shared Terminal**: Create and join collaborative terminal sessions
- **Performance Monitor**: Real-time monitoring of connections and system metrics
- **Connection Monitor**: WebSocket communication debugging
- **Settings**: Customizable preferences and connection configuration

## Supported Languages

- C# (.NET)
- Python
- JavaScript (Node.js)
- C (GCC)
- Java
- WebAssembly (WASM)
- MicroASM (j-masm)
- And more via the plugin system

## Building KodeRunner

To build KodeRunner, you need to have .NET 8 or higher installed on your machine.

You can install .NET from [here](https://dotnet.microsoft.com/download)

### Prerequisites
- .NET 8 SDK or higher
- Docker (optional, for enhanced sandboxing)
- Git
- Java 17+ (for desktop client)
- Maven 3.6+ (for desktop client)

### Build Steps
1. Clone the repository
2. Navigate to the project directory
3. Build the server:
   ```bash
   dotnet build
   ```
4. Build the desktop client:
   ```bash
   cd java
   mvn clean install
   ```

## Running KodeRunner

### Server Mode (Default)
Start the WebSocket server for client connections:

```bash
dotnet run
```

The server will start on `localhost:5000` by default and provide WebSocket endpoints for:
- `/code` - Code submission and file management
- `/PMS` - Project Management System
- `/stop` - Process termination
- `/terminput` - Terminal input handling
- `/terminal/create` - Shared terminal session creation
- `/terminal/{sessionId}` - Dynamic shared terminal endpoints

### Desktop Client
Launch the enhanced desktop development environment:

```bash
cd java
mvn exec:java
```

The desktop client provides:
- Visual project management and file editing
- Integrated build and run controls
- Real-time output monitoring
- Shared terminal session management
- Performance and connection monitoring

### CLI Mode
Execute projects directly from the command line:

```bash
# Initialize directories
koderunner init

# Run a project by name (auto-resolves to koderunner/Projects/ProjectName)
koderunner run TestProject

# Build a project by name
koderunner build TestProject --language python

# Interactive terminal session with a project
koderunner terminal TestProject --interactive

# Run with relative path
koderunner run ./myproject --language python --main app.py

# List available projects
koderunner list

# Show help
koderunner help
```

**Path Resolution**: 
- **Project name only** (e.g., `TestProject`): Automatically resolves to `koderunner/Projects/TestProject`
- **Relative path** (e.g., `./myproject`): Relative to current working directory
- **Absolute path** (e.g., `C:\path\to\project`): Uses the exact path specified

### CLI Examples

```bash
# Simple project name - easiest method
koderunner run TestProject
koderunner build TestProject

# Interactive terminal with project
koderunner terminal TestProject --interactive

# Relative path from current directory
koderunner run ./koderunner/Projects/TestProject
koderunner build ./custom/project/path

# Absolute path
koderunner run "C:\Users\Me\Projects\MyProject"

# With language and main file specification
koderunner run TestProject --language python --main app.py

# Build with custom output name
koderunner build TestProject --language c --output myapp

# Interactive terminal session
koderunner terminal TestProject --language python --interactive

# Run with custom sandbox policy
koderunner run TestProject --sandbox trusted
```

### Interactive Terminal Mode

The `koderunner terminal` command provides an interactive session where you can send input to running processes:

```bash
# Start interactive terminal session
koderunner terminal MyPythonProject --interactive

# Once running, you can:
# - Type commands and press Enter to send input to the process
# - Type 'exit' to quit the session
# - Use Ctrl+C to interrupt the running process
```

### Troubleshooting CLI Issues

Common issues and solutions:

1. **"Project directory not found"**: 
   - Check the project name is correct: `koderunner list` to see available projects
   - For relative paths, ensure you're in the correct directory
   - For absolute paths, verify the full path exists

2. **"Could not detect language"**:
   - Add appropriate file extensions to your project (`.py`, `.js`, `.c`, etc.)
   - Use the `--language` parameter to specify explicitly

3. **"Could not detect main file"**:
   - Ensure your project has a main file with expected name (`main.py`, `index.js`, etc.)
   - Use the `--main` parameter to specify the filename explicitly

4. **"No active process to send input to"**:
   - This occurs when trying to send terminal input but no process is running
   - Start a project with `koderunner terminal <project> --interactive` first

```bash
# If auto-detection fails, specify everything explicitly
koderunner run TestProject --language python --main main.py

# For interactive sessions that need input
koderunner terminal TestProject --language python --main main.py --interactive
```

## WebSocket Protocol

KodeRunner uses a custom WebSocket protocol for real-time communication:

### Code Execution Endpoint (`/PMS`)
Send project metadata and receive execution results:

```json
{
  "PMS_System": "1.2.0",
  "Project_Name": "myproject",
  "Main_File": "main.py",
  "Project_Build_Systems": "python",
  "Project_Output": "output",
  "Run_On_Build": "True"
}
```

### Terminal Input Endpoint (`/terminput`)
Send input to running processes:

```
Simple text input sent directly to active terminal sessions
```

### Shared Terminal Creation (`/terminal/create`)
Create collaborative terminal sessions:

```json
{
  "action": "create",
  "project": "ProjectName"
}
```

## Configuration

KodeRunner uses a JSON configuration file located at `koderunner/Config/config.json`:

```json
{
  "ProcessTimeoutSeconds": 30,
  "LogLevel": "Info",
  "BufferSize": 8192,
  "WebServer": {
    "Host": "localhost",
    "Port": 5000
  },
  "Logging": {
    "EnableFileLogging": true,
    "EnableConsoleLogging": true
  }
}
```

## Security

KodeRunner includes a comprehensive sandboxing system:

- **Resource Limits**: CPU, memory, and execution time limits
- **Command Filtering**: Block dangerous commands and operations
- **File System Isolation**: Restrict file access to designated directories
- **Process Monitoring**: Real-time monitoring of executed processes

### Sandbox Policies

- **Default**: Restricted execution (512MB RAM, 30s timeout)
- **Trusted**: Extended permissions (2GB RAM, 5min timeout)
- **Custom**: User-defined policies via configuration

## Plugin Development

Extend KodeRunner with custom language support:

```csharp
[Runnable("mylang", "mylang", 0)]
public class MyLanguageRunnable : IRunnable
{
    public string Name => "mylang";
    public string Language => "mylang";
    public int Priority => 0;
    public string description => "My custom language runner";

    public void Execute(Provider.ISettingsProvider settings)
    {
        // Implementation here
    }
}
```

## Client Integration

KodeRunner is designed to be integrated with remote clients:

- **Resonite Integration**: Primary use case for VR/AR development
- **Desktop Client**: Full-featured Java Swing development environment
- **Web Applications**: JavaScript WebSocket clients
- **Desktop Applications**: .NET, Python, or other WebSocket-capable clients
- **Mobile Applications**: Any platform supporting WebSocket connections

## Project Structure

```
koderunner/
├── Projects/          # User projects
├── Builds/           # Build outputs
├── Logs/             # Server logs
├── Config/           # Configuration files
├── Exports/          # Exported projects (.KRproject files)
├── Temp/             # Temporary execution files
├── Runnables/        # Plugin assemblies
└── java/             # Desktop client source
    ├── src/main/java/   # Java source files
    ├── pom.xml         # Maven configuration
    └── README.md       # Client documentation
```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Implement your changes
4. Add tests if applicable
5. Submit a pull request

### Development Guidelines

- Follow C# coding conventions for server code
- Follow Java conventions for desktop client code
- Ensure thread safety for multi-client scenarios
- Add comprehensive error handling
- Document WebSocket protocol changes
- Test with multiple concurrent connections

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

- GitHub Issues: Report bugs and feature requests
- Documentation: See `/docs` for detailed API documentation
- Community: Join discussions in GitHub Discussions

---

**KodeRunner** - Bringing powerful code execution capabilities to any WebSocket-enabled application.

