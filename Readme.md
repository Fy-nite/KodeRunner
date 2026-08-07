# KodeRunner

Welcome to KodeRunner V3.
KodeRunner is a interactive VSCode like install inside resonite.


KodeRunner allows users to run programming Languages from any place that supports Websockets.

# Language Support

KodeRunner ships with 21 built-in runnables. A runnable is selected by the
`Project_Build_Systems` field of the PMS project message. The language's
toolchain must be installed and available on `PATH` for the runnable to work.

| `Project_Build_Systems` | Language | Toolchain | Behavior |
| ----------------------- | -------- | --------- | -------- |
| `dotnet` | C# / .NET | `dotnet` | `dotnet build <ProjectPath>`, then `dotnet run --project <ProjectPath>` if `Run_On_Build` |
| `python` | Python | `py` (Windows) / `python3` (Unix) | `py <main_file>` / `python3 <main_file>` |
| `nodejs` | JavaScript / Node.js | `node`, `npm` | `node <main_file>`; runs `npm install <RunArgs>` first if `RunArgs` is set |
| `c` | C | `gcc` | `gcc -o <output> <main_file>`, runs the binary if `Run_On_Build` |
| `cpp` | C++ | `g++` | `g++ -o <output> <main_file>`, runs the binary if `Run_On_Build` |
| `contract` | Contract | `ccl` | `ccl <main_file>` — passes the `.ct` file directly to the compiler |
| `go` | Go | `go` | `go run <main_file>` |
| `rust` | Rust | `cargo` / `rustc` | `cargo run --manifest-path <Cargo.toml>` when a `Cargo.toml` exists, otherwise `rustc -o <output> <main_file>` + run |
| `ruby` | Ruby | `ruby` | `ruby <main_file>` |
| `php` | PHP | `php` | `php <main_file>` |
| `java` | Java | `javac` / `java` | `javac <main_file>`, then `java -cp <ProjectPath> <ClassName>` if `Run_On_Build` |
| `typescript` | TypeScript | `tsc` / `node` | `tsc <main_file>`, then `node <main_file>.js` if `Run_On_Build` |
| `lua` | Lua | `lua` | `lua <main_file>` |
| `bash` | Bash | `bash` | `bash <main_file>` |
| `kotlin` | Kotlin | `kotlin` | `kotlin <main_file>` |
| `julia` | Julia | `julia` | `julia <main_file>` |
| `r` | R | `Rscript` | `Rscript <main_file>` |
| `haskell` | Haskell | `runghc` | `runghc <main_file>` |
| `perl` | Perl | `perl` | `perl <main_file>` |
| `solidity` | Solidity | `solc` | `solc --bin --abi -o <ProjectPath>/build <main_file>` |
| `microasm` | MicroASM | `masm` | `masm --run <main_file>` |

All commands are executed through the built-in `TerminalProcess`, and every
line of output is forwarded back to the connected PMS client over WebSocket.

## Project layout

Each project lives in `koderunner/Projects/<Project_Name>/` with the source
files next to it:

```
koderunner/
└── Projects/
    └── myproject/
        ├── main.c          # the file named in Main_File
        └── config.json     # written automatically from the PMS message
```

## PMS project message

The PMS client sends a JSON message that selects the runnable and configures
the run. Only `Project_Name`, `Main_File` and `Project_Build_Systems` are
required; the rest are optional.

```json
{
  "Project_Name": "myproject",
  "Main_File": "main.ct",
  "Project_Build_Systems": "contract",
  "Project_Output": "main",
  "Run_On_Build": "True"
}
```

| Field | Used for |
| ----- | -------- |
| `Project_Name` | The project folder under `koderunner/Projects/` |
| `Main_File` | The main source file (e.g. `main.c`, `main.ct`, `main.py`) |
| `Project_Build_Systems` | **Selects the runnable** — must match a row in the table above |
| `Project_Output` | Output file name for compiled languages (`<output>` in the table) |
| `Run_On_Build` | `"True"`/`"False"` — whether to run after a successful build |

## Adding a custom runnable

1. Implement `IRunnable` and decorate the class with `[Runnable(name, language, priority)]`
   (see `runner/Core/IRunnable.cs` for the contract).
2. Build it into a DLL — either add the class to the `KodeRunnerLibs/Runnables`
   project, or build your own assembly referencing `KodeRunner`.
3. Drop the DLL into `koderunner/Runnables/` — KodeRunner scans that directory
   at startup and registers every `IRunnable` it finds.

# Building KodeRunner

To Build KodeRunner, you need to have Dotnet 8 or higher installed on your machine.

you can install Dotnet from [here](https://dotnet.microsoft.com/download)

just download the installer and it will setup everything for you.

After downloading the installer, just open a terminal in the same directory as it, run the program by doing ./Kodeinstaller and run the following command in the prompt.

```bash
install
```

# Running KodeRunner

Running KodeRunner is simple, just run the following command in the terminal.

```bash
./KodeRunner
```

