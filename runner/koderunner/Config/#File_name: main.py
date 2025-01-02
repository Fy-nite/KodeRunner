#File_name: main.py
# Project: text
import os
import sys

def process_command(cmd, args=[]):
  if cmd == "new":
    print("happy new year")
  elif cmd == "ls":
    os.system("ls " + " ".join(args))
  else:
    print("invalid command: " + cmd)

if len(sys.argv) > 1:
  # If arguments are provided, process them 
  cmd = sys.argv[1]
  args = sys.argv[2:]  # Get all arguments after the command
  process_command(cmd, args)
else:
  # If no arguments, run interactive mode
  while True:
    input_line = input().split()
    cmd = input_line[0] if input_line else ""
    args = input_line[1:] if len(input_line) > 1 else []
    process_command(cmd, args)