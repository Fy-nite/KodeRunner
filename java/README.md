# KodeRunner Desktop Tester

A Java Swing application for testing KodeRunner's WebSocket endpoints from a desktop environment.

## Features

- **Code Editor**: Send code files to KodeRunner with project metadata
- **PMS Testing**: Test the Project Management System with JSON configuration
- **Terminal Input**: Send commands to running processes
- **Process Control**: Stop running processes remotely
- **Connection Monitor**: Monitor all WebSocket communications

## Prerequisites

- Java 17 or higher
- Maven 3.6 or higher
- Running KodeRunner instance

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
   - Monitor all communications in the connection log

## Endpoints Tested

- `/code` - Code submission and file management
- `/PMS` - Project Management System
- `/terminput` - Terminal input for running processes
- `/stop` - Process control and termination

## Troubleshooting

- Ensure KodeRunner is running before connecting
- Check firewall settings if connection fails
- Use the Connection Monitor to debug communication issues
