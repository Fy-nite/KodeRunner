# KodeRunner Desktop Tester

A Java Swing application for testing KodeRunner's WebSocket endpoints from a desktop environment.

## Features

- **Code Editor**: Send code files to KodeRunner with project metadata
- **PMS Testing**: Test the Project Management System with JSON configuration
- **Terminal Input**: Send commands to running processes
- **Process Control**: Stop running processes remotely
- **Shared Terminal**: Create and connect to shared terminal sessions for collaborative development
- **Connection Monitor**: Monitor all WebSocket communications
- **Settings**: Configurable Look & Feel and connection preferences

## New in v2.1

- **Shared Terminal Support**: Create and join shared terminal sessions
- **Dynamic Endpoint Connection**: Connect to dynamically created terminal endpoints
- **Enhanced Message Handling**: Support for structured JSON messages
- **Collaborative Features**: Multiple users can share the same terminal session

## Prerequisites

- Java 17 or higher
- Maven 3.6 or higher
- Running KodeRunner instance (v3.0+ for shared terminal features)

## Building and Running

1. Navigate to the java directory:
   ```bash
   cd d:\koderunner\java
   ```

2. Install dependencies:
   ```bash
   mvn clean install
   ```

3. Run the application:
   ```bash
   mvn exec:java
   ```

## Usage

1. **Start KodeRunner**: Make sure your KodeRunner instance is running
2. **Configure Connection**: Set the host and port (default: localhost:5000)
3. **Connect**: Use "Connect All" or connect to individual endpoints
4. **Test Features**:
   - Write code in the editor and send it to KodeRunner
   - Configure PMS settings and test project builds
   - Send terminal commands to running processes
   - Create shared terminal sessions for collaborative development
   - Monitor all communications in the connection log

## Shared Terminal Usage

1. **Create Session**: 
   - Go to "Shared Terminal" tab
   - Click "Connect to Terminal Creator" to connect to the `/terminal/create` endpoint
   - Enter a project name and click "Create Shared Session"
   - Copy the generated endpoint path

2. **Connect to Session**:
   - Enter the session endpoint (e.g., `/terminal/project-abc123`)
   - Click "Connect to Session"
   - You can now send commands and receive output

3. **Send Commands**:
   - Type commands in the terminal input field
   - Press Enter or click "Send" to execute
   - Commands are sent as structured JSON messages

## Endpoints Tested

- `/code` - Code submission and file management
- `/PMS` - Project Management System
- `/terminput` - Terminal input for running processes
- `/stop` - Process control and termination
- `/terminal/create` - Shared terminal session creation
- `/terminal/{sessionId}` - Dynamic shared terminal endpoints

## Message Formats

### Shared Terminal Input
```json
{
  "type": "input",
  "data": "command text",
  "timestamp": 1234567890
}
```

### Session Creation
```json
{
  "action": "create",
  "project": "ProjectName"
}
```

## Troubleshooting

- Ensure KodeRunner v3.0+ is running for shared terminal features
- Check firewall settings if connection fails
- Use the Connection Monitor to debug communication issues
- For shared terminals, ensure the `/terminal/create` endpoint is connected first
