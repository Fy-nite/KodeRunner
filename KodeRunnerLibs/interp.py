from operator import and_
import sys
import argparse
from PySide6.QtWidgets import (
    QApplication, QMainWindow, QWidget, QVBoxLayout, QHBoxLayout, QTextEdit, 
    QTableWidget, QTableWidgetItem, QPushButton, QSlider, QLabel, QCheckBox
)
from PySide6.QtGui import QFont
from PySide6.QtCore import Qt, QTimer

class RetroTechApp(QMainWindow):
    def __init__(self):
        super().__init__()
       
        # Main Window Setup
        self.setWindowTitle("Assembly Interpreter 2007")
        self.setGeometry(100, 100, 1000, 600)
        self.setStyleSheet("background-color: #E5E5E5; color: #000;")

        # Central Widget
        central_widget = QWidget()
        self.setCentralWidget(central_widget)
        layout = QVBoxLayout()
        central_widget.setLayout(layout)

        # Header Label
        header = QLabel("Assembly Interpreter 2007", self)
        header.setFont(QFont("Verdana", 16))
        header.setAlignment(Qt.AlignCenter)
        header.setStyleSheet("background-color: #C0C0C0; border: 2px solid #A0A0A0; padding: 10px;")
        layout.addWidget(header)

        # Code Editor & Register Viewer
        middle_layout = QHBoxLayout()

        # Code Editor
        self.code_editor = QTextEdit(self)
        self.code_editor.setFont(QFont("Courier", 10))
        self.code_editor.setPlaceholderText("Enter your assembly code here...")
        self.code_editor.setStyleSheet("border: 2px solid #A0A0A0; background-color: #FFFFFF;")
        middle_layout.addWidget(self.code_editor)

        # Register Viewer
        self.register_viewer = QTableWidget(8, 2, self)
        self.register_viewer.setHorizontalHeaderLabels(["Register", "Value"])
        self.register_viewer.setFont(QFont("Verdana", 10))
        self.register_viewer.setStyleSheet(
            "border: 2px solid #A0A0A0; background-color: #F5F5F5;"
        )
        for i in range(8):
            self.register_viewer.setItem(i, 0, QTableWidgetItem(f"R{i}"))
            self.register_viewer.setItem(i, 1, QTableWidgetItem("0"))
        middle_layout.addWidget(self.register_viewer)

        layout.addLayout(middle_layout)

        # Memory Viewer
        memory_label = QLabel("Memory Viewer", self)
        memory_label.setFont(QFont("Verdana", 12))
        memory_label.setStyleSheet("padding: 5px;")
        layout.addWidget(memory_label)

        self.memory_viewer = QTextEdit(self)
        self.memory_viewer.setFont(QFont("Courier", 10))
        self.memory_viewer.setStyleSheet("border: 2px solid #A0A0A0; background-color: #FFFFFF;")
        self.memory_viewer.setPlaceholderText("Memory content will appear here...")
        layout.addWidget(self.memory_viewer)

        # Control Panel
        control_panel = QHBoxLayout()

        self.run_button = QPushButton("Run", self)
        self.run_button.setFont(QFont("Verdana", 10))
        self.run_button.setStyleSheet(
            "background-color: #DFFFD6; border: 2px solid #A0A0A0; padding: 5px;"
        )
        control_panel.addWidget(self.run_button)

        self.step_button = QPushButton("Step", self)
        self.step_button.setFont(QFont("Verdana", 10))
        self.step_button.setStyleSheet(
            "background-color: #FFF7D6; border: 2px solid #A0A0A0; padding: 5px;"
        )
        control_panel.addWidget(self.step_button)

        self.reset_button = QPushButton("Reset", self)
        self.reset_button.setFont(QFont("Verdana", 10))
        self.reset_button.setStyleSheet(
            "background-color: #FFD6D6; border: 2px solid #A0A0A0; padding: 5px;"
        )
        control_panel.addWidget(self.reset_button)

        speed_slider = QSlider(Qt.Horizontal, self)
        speed_slider.setStyleSheet("background-color: #FFFFFF; border: 1px solid #A0A0A0;")
        control_panel.addWidget(speed_slider)

        # Debug Mode Checkbox
        self.debug_checkbox = QCheckBox("Debug Mode", self)
        self.debug_checkbox.setFont(QFont("Verdana", 10))
        self.debug_checkbox.setStyleSheet("padding: 5px;")
        self.debug_checkbox.stateChanged.connect(self.toggle_debug_mode)
        control_panel.addWidget(self.debug_checkbox)

        layout.addLayout(control_panel)

        self.run_button.clicked.connect(self.run_code)
        self.step_button.clicked.connect(self.step_code)
        self.reset_button.clicked.connect(self.reset_machine)
        
        # Check if the output variable has changed
        self.timer = QTimer(self)
        self.timer.timeout.connect(self.updateOutput)
        self.timer.start(500)  # Update every 500 milliseconds
        self.debug_checkbox.stateChanged.connect(self.toggle_debug_mode)

    def run_code(self):
        code = self.code_editor.toPlainText()
        self.interp.run_code(code)
        self.update_register_viewer()

    def step_code(self):
        self.interp.step()
        self.update_register_viewer()

    def reset_machine(self):
        self.interp.reset()
        self.update_register_viewer()
        self.memory_viewer.clear()

    def toggle_debug_mode(self):
        is_checked = self.debug_checkbox.isChecked()
        self.interp.debug = is_checked
        
        
        # Enable/disable step button based on debug mode
        self.step_button.setEnabled(is_checked)

    def update_register_viewer(self):
        for i in range(8):
            self.register_viewer.setItem(i, 1, QTableWidgetItem(str(getattr(self.interp, f"R{i}"))))

    def updateOutput(self):
        current_output = self.interp.output
        if current_output != self.memory_viewer.toPlainText():
            self.memory_viewer.setPlainText(current_output)


class RegisterMachine:

    def __init__(self, num_registers=16):
        self.RAX = 0
        self.RBX = 0
        self.RCX = 0
        self.RDX = 0
        self.RSI = 0
        self.RDI = 0
        self.RBP = 0
        self.RSP = 0
        self.R0 = 0
        self.R1 = 0
        self.R2 = 0
        self.R3 = 0
        self.R4 = 0
        self.R5 = 0
        self.R6 = 0
        self.R7 = 0
        self.R8 = 0
        self.R9 = 0
        self.R10 = 0
        self.R11 = 0
        self.R12 = 0
        self.R13 = 0
        self.R14 = 0
        self.R15 = 0
        
        self.RIP = 0
        self.STDOUT = None 
        self.current_code = ""
        self.current_instruction = ""
        self.debug_mode = False
        self.step_mode = False
        self.cpu_speed = 20.0
        self.output = ""
        self.output_handler = print  # Default to standard print function
        self.RFLAGS = 0
        self.memory = [0] * 1024
        self.num_registers = num_registers
        self.registers = [0] * num_registers
        self.ops = ["MOV",
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
                    "JMP",
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
                    "COV"
                    ]
        self.function_map = {
            "PRINTS": "print",
            "PRINTI": "print",
            "play": "play",
            "x20": "input",
            "x30": "add",
            "x40": "sub",
            "x50": "mul",
            "x60": "div",
        }
        self.callbacks = []
        self.ui = None
        
    def set_debug_mode(self, enabled=True):
        """Enable or disable debug mode"""
        self.debug_mode = enabled
        self.step_mode = enabled
        print(f"Debug mode {'enabled' if enabled else 'disabled'}")

    def set_cpu_speed(self, instructions_per_second):
        """Set the CPU execution speed in instructions per second"""
        self.cpu_speed = float(instructions_per_second)
        print(f"CPU speed set to {self.cpu_speed} instructions per second")

    def step(self):
        """Execute a single instruction in debug mode"""
        if not self.debug_mode:
            print("Debug mode must be enabled to use step")
            return False
        if self.RIP >= len(self.current_code):
            print("End of program reached")
            return False
        
        instruction = self.current_code[self.RIP].strip()
        print(f"Executing instruction {self.RIP}: {instruction}")
        self._execute_single_instruction(instruction)
        print(f"Register state:\n{self}")
        return True

    def run_code(self, code_string):
        """
        Execute code provided as a string
        Args:
            code_string (str): The code to execute, with instructions separated by newlines
        """
        # Split the code string into lines
        instructions = code_string.split('\n')
        # Reset RIP (instruction pointer) to start from beginning
        self.RIP = 0
        # Execute the instructions
        self.execute(instructions)

    def reset(self):
        """Reset the machine state"""
        self.RAX = 0
        self.RBX = 0
        self.RCX = 0
        self.RDX = 0
        self.RSI = 0
        self.RDI = 0
        self.RBP = 0
        self.RSP = 0
        self.R0 = 0
        self.R1 = 0
        self.R2 = 0
        self.R3 = 0
        self.R4 = 0
        self.R5 = 0
        self.R6 = 0
        self.R7 = 0
        self.R8 = 0
        self.R9 = 0
        self.R10 = 0
        self.R11 = 0
        self.R12 = 0
        self.R13 = 0
        self.R14 = 0
        self.R15 = 0
        self.RIP = 0
        self.RFLAGS = 0
        self.memory = [0] * 1024
        self.output = ""
        
        print("Machine state reset")

    def execute(self, instructions):
        import time
        self.current_code = instructions
        
        while self.RIP < len(instructions):
            instruction = instructions[self.RIP].strip()
            if not instruction or instruction.startswith(';'):
                self.RIP += 1
                continue

            if self.debug_mode:
                if self.step_mode:
                    break  # Exit the loop to allow stepping
                print(f"Executing instruction {self.RIP}: {instruction}")

            self._execute_single_instruction(instruction)

            if self.debug_mode:
                print(f"Register state:\n{self}")

            # Apply CPU speed limitation
            if self.cpu_speed > 0:
                time.sleep(1.0 / self.cpu_speed)

    def _execute_single_instruction(self, instruction):
        """Helper method to execute a single instruction"""
        self.current_instruction = instruction
        parts = instruction.split()
        op = parts[0].upper()

        if op == "INCLUDE":
            self.include(parts[1])
        elif op in self.ops:
            print(parts)
            
            getattr(self, op.lower())(*parts[1:])
        else:
            raise ValueError(f"Invalid instruction: {op}")
        self.RIP += 1
        self.notify_state_change()

    def include(self, filename):
        with open(filename, 'r') as f:
            included_instructions = f.readlines()
        self.execute(included_instructions)

    def mov(self, dest, src):
        value = int(src) if src.isdigit() else getattr(self, src)
        setattr(self, dest, value)

    def jeq(self, address):
        if self.RFLAGS == 1:
            self.RIP = int(address) - 1
            self.RFLAGS = 0
    def jmp(self, address):
        self.RIP = int(address) - 1
    
    
    def add(self, dest, src):
        value = getattr(self, src) + getattr(self, dest)
        setattr(self, dest, value)

    def sub(self, dest, src):
        value = getattr(self, dest) - getattr(self, src)
        setattr(self, dest, value)

    def and_(self, dest, src):
        value = and_(getattr(self, dest), getattr(self, src))
        setattr(self, dest, value)
        
    def or_(self, dest, src):
        
        value = getattr(self, dest) | getattr(self, src)
        setattr(self, dest, value)
        
    def xor(self, dest, src):
        value = getattr(self, dest) ^ getattr(self, src)
        setattr(self, dest, value)
    

        
    def shl(self, dest, src):
        value = getattr(self, dest) << getattr(self, src)
        setattr(self, dest, value)
        
    def shr(self, dest, src):
        
        value = getattr(self, dest) >> getattr(self, src)
        setattr(self, dest, value)
        
    def ror(self, dest, src):
        value = getattr(self, dest) >> getattr(self, src) | getattr(self, dest) << (32 - getattr(self, src))
        setattr(self, dest, value)
        
    def rol(self, dest, src):
        value = getattr(self, dest) << getattr(self, src) | getattr(self, dest) >> (32 - getattr(self, src))
        setattr(self, dest, value)
    
    def inc(self, dest):
        value = getattr(self, dest) + 1
        setattr(self, dest, value)

    def dec(self, dest):
        value = getattr(self, dest) - 1
        setattr(self, dest, value)
    
    def define(self, dest, length, *src_parts):
        """takes in the string register, the length of the string and the string it'self and stores it in the register

        Args:
            dest (reg): the string register to store into
            length (int): the length of the string without quotes
            src_parts (tuple): parts of the string in quotes that will be joined
        """
        # join all parts and remove the quotes from the start and end
        src = ' '.join(src_parts)
        src = src[1:-1]
        # store the string inside the register
        if int(length) == len(src):
            setattr(self, dest, src)
        else:
            raise ValueError("Length of string does not match the length provided, stopping execution")
    
    def neg(self, dest):
        value = -getattr(self, dest)
        setattr(self, dest, value)

    def mul(self, dest, src):
        value = getattr(self, dest) * getattr(self, src)
        setattr(self, dest, value)

    def div(self, dest, src):
        value = getattr(self, dest) // getattr(self, src)
        setattr(self, dest, value)




    def cmp(self, reg1, reg2):
        
        # if one of the registers are a int and the other is a register, do the comparison
        if reg1.isdigit() and not reg2.isdigit():
            print(reg1, reg2)
            if int(reg1) == getattr(self, reg2):
                self.RFLAGS = 1
            else:
                self.RFLAGS = 0
        elif reg2.isdigit() and not reg1.isdigit():
            if int(reg2) == getattr(self, reg1):
                self.RFLAGS = 1
            else:
                self.RFLAGS = 0
        else:
            if getattr(self, reg1) == getattr(self, reg2):
                self.RFLAGS = 1
            else:
                self.RFLAGS = 0
            
    def jg(self, address):  
        if self.RFLAGS == 0:
            self.RIP = int(address) - 1
    def jl(self, address):
        if self.RFLAGS == 1:
            self.RIP = int(address) - 1
    def jge(self, address):
        if self.RFLAGS != 1:
            self.RIP = int(address) - 1
    def jle(self, address):
        if self.RFLAGS != 0:
            self.RIP = int(address) - 1
            
            
            

    def jne(self, address):
        if self.RFLAGS == 0:
            self.RIP = int(address) - 1
            
    def call(self, *parts):
        function = self.function_map[parts[0]]
        if function == "print":
            self.print(parts[1])
        elif function == "input":
            self.input(parts[1])
        elif function == "wait":
            self.wait(parts[1])
        elif function == "play":
            self.play(parts[1], parts[2], parts[3])
        elif function in ["add", "sub", "mul", "div"]:
            getattr(self, function)(*parts[1:])

    def push(self, reg):
        self.RSP -= 1
        self.memory[self.RSP] = getattr(self, reg)

    def pop(self, reg):
        setattr(self, reg, self.memory[self.RSP])
        self.RSP += 1

    def print(self, reg):
        print(getattr(self, reg))
        self.output = str(getattr(self, reg))
        self.notify_state_change()

    def input(self, reg):
        value = input("Enter a value: ")
        setattr(self, reg, int(value))

    def hlt(self):
        pass

    def __str__(self):
        return f"RAX: {self.RAX}\nRBX: {self.RBX}\nRCX: {self.RCX}\nRDX: {self.RDX}\nRSI: {self.RSI}\nRDI: {self.RDI}\nRBP: {self.RBP}\nRSP: {self.RSP}\nR0: {self.R0}\nR1: {self.R1}\nR2: {self.R2}\nR3: {self.R3}\nR4: {self.R4}\nR5: {self.R5}\nR6: {self.R6}\nR7: {self.R7}\nR8: {self.R8}\nR9: {self.R9}\nR10: {self.R10}\nR11: {self.R11}\nR12: {self.R12}\nR13: {self.R13}\nR14: {self.R14}\nR15: {self.R15}\nRIP: {self.RIP}\nRFLAGS: {self.RFLAGS}\nOutput: {self.output}"

    def __repr__(self):
        return str(self)

    def __getitem__(self, key):
        return self.registers[key]

    def __setitem__(self, key, value):
        self.registers[key] = value

    def __len__(self):
        return self.num_registers

    def __iter__(self):
        return iter(self.registers)

    def __contains__(self, item):
        return item in self.registers

    def __eq__(self, other):
        return self.registers == other.registers

    def __ne__(self, other):
        return self.registers != other.registers

    def register_callback(self, callback):
        """Register a callback for state updates"""
        self.callbacks.append(callback)

    def notify_state_change(self):
        """Notify all registered callbacks of state changes"""
        for callback in self.callbacks:
            callback()

    def set_ui(self, ui):
        """Set reference to UI for updates"""
        self.ui = ui

    def launch_ui(self):
        """Launch the UI version of the interpreter"""
        app = QApplication(sys.argv)
        window = RetroTechApp()
        window.show()
        return app.exec()

def main():
    parser = argparse.ArgumentParser(description='MicroASM Interpreter')
    parser.add_argument('-ui', action='store_true', help='Launch with UI')
    parser.add_argument('files', nargs='*', help='Assembly files to execute')
    parser.add_argument('-debug' , action='store_true', help='Enable debug mode')
    parser.add_argument('-speed', type=float, help='Set CPU speed in instructions per second')
    args = parser.parse_args()

    machine = RegisterMachine()

    if args.ui:
        # Launch UI mode
        machine.launch_ui()
    elif args.debug:
        machine.set_debug_mode(True)
    elif args.speed:
        machine.set_cpu_speed(args.speed)
        
    else:
        # Command line mode
        for file in args.files:
            machine.include(file)
        #print(machine)

if __name__ == "__main__":
    main()

"""Supported instructions include:
    - MOV dest src: Move the value from src to dest.
    - ADD dest src: Add values from src, store the result in dest.
    - SUB dest src: Subtract the value of src from dest.
    - MUL dest src: Multiply the value of dest by src.
    - DIV dest src: Divide the value of dest by src.
    - JMP address: Jump to the specified address, where adderess is a line number starting from 0.
    - CMP reg1, reg2: Compare the values of reg1 and reg2 and set the RFLAGS register.
    - JE address: Jump to the specified address if the last comparison was equal.
    - JNE address: Jump to the specified address if the last comparison was not equal.
    - PUSH reg: Push the value of the specified register onto the stack.
    - POP reg: Pop the value from the stack into the specified register.
    - CALL name register: Call a function based on the address and pass the register as an argument.
    - HLT: Halt the execution.
    - INC reg: Increment the value of the specified register.
    - DEC reg: Decrement the value of the specified register.
    - DEFINE StringReg Length "String": Define a string in a string register.
    - NEG reg: Negate the value of the specified register.
    - ROR dest src: Rotate the bits of dest to the right by src bits.
    - ROL dest src: Rotate the bits of dest to the left by src bits.
    - AND dest src: Perform a bitwise AND operation between dest and src.
    - OR dest src: Perform a bitwise OR operation between dest and src.
 """
