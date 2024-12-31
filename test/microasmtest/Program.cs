using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Finite;

public class MicroASM
{
    // Register states
    public int RAX,
        RBX,
        RCX,
        RDX,
        RSI,
        RDI,
        RBP,
        RSP;
    public int R0,
        R1,
        R2,
        R3,
        R4,
        R5,
        R6,
        R7,
        R8,
        R9,
        R10,
        R11,
        R12,
        R13,
        R14,
        R15;
    public int RIP,
        RFLAGS;
    public string Output { get; private set; } = "";

    private int[] memory;
    private int numRegisters;
    private bool debugMode;
    private bool stepMode;
    private double cpuSpeed;
    private string currentInstruction;
    private List<string> currentCode;

    private readonly HashSet<string> ops = new HashSet<string>
    {
        "MOV",
        "ADD",
        "SUB",
        "MUL",
        "DIV",
        "JMP",
        "CMP",
        "JE",
        "JNE",
        "PUSH",
        "POP",
        "CALL",
        "HLT",
        "INC",
        "DEC",
        "JG",
        "JEQ",
        "JL",
        "JGE",
        "JLE",
        "ROR",
        "ROL",
        "AND",
        "OR",
        "XOR",
        "NOT",
        "SHL",
        "SHR",
        "NEG",
        "RET",
        "STRING",
        "DEFINE",
        "COV",
    };

    private readonly Dictionary<string, string> functionMap = new Dictionary<string, string>
    {
        { "PRINTS", "printf" },
        { "PRINTI", "printi" },
        { "play", "play" },
        { "x20", "input" },
        { "x30", "add" },
        { "x40", "sub" },
        { "x50", "mul" },
        { "x60", "div" },
    };

    public MicroASM(int numRegisters = 16)
    {
        this.numRegisters = numRegisters;
        memory = new int[1024];
        Reset();
        cpuSpeed = 20.0;
    }

    public void Reset()
    {
        RAX = RBX = RCX = RDX = RSI = RDI = RBP = RSP = 0;
        R0 = R1 = R2 = R3 = R4 = R5 = R6 = R7 = R8 = R9 = R10 = R11 = R12 = R13 = R14 = R15 = 0;
        RIP = RFLAGS = 0;
        Array.Clear(memory, 0, memory.Length);
        Output = "";
        Console.WriteLine("Machine state reset");
    }

    public void SetDebugMode(bool enabled = true)
    {
        debugMode = enabled;
        stepMode = enabled;
        Console.WriteLine($"Debug mode {(enabled ? "enabled" : "disabled")}");
    }

    public void RunCode(string codeString)
    {
        var instructions = codeString.Split(
            new[] { '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries
        );
        RIP = 0;
        Execute(instructions);
    }

    public void Execute(string[] instructions)
    {
        currentCode = new List<string>(instructions);

        while (RIP < instructions.Length)
        {
            string instruction = instructions[RIP].Trim();
            if (string.IsNullOrEmpty(instruction) || instruction.StartsWith(";"))
            {
                RIP++;
                continue;
            }

            if (debugMode)
            {
                if (stepMode)
                    break;
                Console.WriteLine($"Executing instruction {RIP}: {instruction}");
            }

            ExecuteSingleInstruction(instruction);

            if (debugMode)
                Console.WriteLine(this.ToString());

            if (cpuSpeed > 0)
                Thread.Sleep((int)(1000.0 / cpuSpeed));
        }
    }

    private void ExecuteSingleInstruction(string instruction)
    {
        currentInstruction = instruction;
        string[] parts = instruction.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string op = parts[0].ToUpper();

        if (op == "INCLUDE")
        {
            Include(parts[1]);
        }
        else if (ops.Contains(op))
        {
            // Execute the operation based on the opcode
            switch (op)
            {
                case "MOV":
                    Mov(parts[1], parts[2]);
                    break;
                case "ADD":
                    Add(parts[1], parts[2]);
                    break;
                case "CALL":
                    CALL(parts);
                    break;
            }
        }
        else
        {
            throw new ArgumentException($"Invalid instruction: {op}");
        }

        RIP++;
    }

    private void CALL(string[] args)
    {
        string func = args[1];
        if (functionMap.ContainsKey(func))
        {
            string mappedFunc = functionMap[func];
            var method = GetType()
                .GetMethod(
                    mappedFunc,
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
            if (method == null)
            {
                throw new ArgumentException($"Method not found: {mappedFunc}");
            }
            method.Invoke(this, new object[] { args[2] });
        }
        else
        {
            throw new ArgumentException($"Invalid function: {func}");
        }
    }

    private void Mov(string dest, string src)
    {
        int value = int.TryParse(src, out int numValue) ? numValue : GetRegisterValue(src);
        SetRegisterValue(dest, value);
    }

    private void Add(string dest, string src)
    {
        int destValue = GetRegisterValue(dest);
        int srcValue = GetRegisterValue(src);
        SetRegisterValue(dest, destValue + srcValue);
    }

    private void printi(string dest)
    {
        Console.WriteLine(GetRegisterValue(dest));
    }
    private void printf(string dest)
    {
        Console.WriteLine(GetRegisterValue(dest));
    }

    // Helper methods
    private int GetRegisterValue(string register)
    {
        return (int)GetType().GetField(register)?.GetValue(this);
    }

    private void SetRegisterValue(string register, int value)
    {
        GetType().GetField(register)?.SetValue(this, value);
    }

    private void Include(string filename)
    {
        string[] includedInstructions = File.ReadAllLines(filename);
        Execute(includedInstructions);
    }

    public override string ToString()
    {
        return $"RAX: {RAX}\nRBX: {RBX}\nRCX: {RCX}\nRDX: {RDX}\n"
            + $"RSI: {RSI}\nRDI: {RDI}\nRBP: {RBP}\nRSP: {RSP}\n"
            + $"RIP: {RIP}\nRFLAGS: {RFLAGS}\nOutput: {Output}";
    }
}

public class Program
{
    public static void Main()
    {
        var microASM = new MicroASM();
        microASM.RunCode(
            @" 
            MOV RAX 10 
            MOV RBX 20 
            ADD RAX RBX 
            CALL PRINTI RAX
        "
        );
    }
}
