
import os
import sys
from PySide6.QtWidgets import QApplication
from PySide6.QtCore import Qt
import sqlite3
from ui.welcomewindow import WelcomeWindow

def main():
    app = QApplication(sys.argv) # start the welcome window

    window = WelcomeWindow()
    window.show()
    
    sys.exit(app.exec())

if __name__ == "__main__":
    main()
    
