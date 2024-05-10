#!/usr/bin/env python3
from PyQt5.QtWidgets import QApplication, QSystemTrayIcon, QMenu, QAction, QMessageBox
from PyQt5.QtGui import QIcon
from app import BrightnessControl

def create_about_dialog():
    msgBox = QMessageBox()
    msgBox.setIcon(QMessageBox.Information)
    msgBox.setText("This is a brightness control application.\n\n"
        "For more information, please visit the project's GitHub page:\n"
        "https://github.com/mvrck19/ddcbright\n\n"
        "If you have any questions, you can contact me at:\n"
        "phaidos@gmail.com\n\n"
        "Icon made by Iconic Panda from Flaticon:\n"
        "https://www.flaticon.com/free-icons/brightness")
    msgBox.setWindowTitle("About DDCBright")
    msgBox.setStandardButtons(QMessageBox.Ok)
    msgBox.exec()

app = QApplication([])
app.setQuitOnLastWindowClosed(False)

# Create BrightnessControl
brightness_control = BrightnessControl()

# Create the icon
icon = QIcon("/usr/bin/ddcbright/sun.png")

# Create the tray
tray = QSystemTrayIcon()
tray.setIcon(icon)
tray.setVisible(True)

# Create the menu
menu = QMenu()
about_action = QAction("About")
about_action.triggered.connect(create_about_dialog)
menu.addAction(about_action)
quit_action = QAction("Quit")
quit_action.triggered.connect(app.quit)
menu.addAction(quit_action)

# Create BrightnessControl when tray icon is clicked
def on_tray_icon_clicked(reason):
    if reason == QSystemTrayIcon.Trigger:
        brightness_control.show()
        
tray.activated.connect(on_tray_icon_clicked)

# Add the menu to the tray
tray.setContextMenu(menu)

app.exec_()