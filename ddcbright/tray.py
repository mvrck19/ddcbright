#!/usr/bin/env python3
from pathlib import Path

from PyQt5.QtGui import QIcon
from PyQt5.QtWidgets import QAction, QApplication, QMenu, QMessageBox, QSystemTrayIcon

from . import autostart
from .app import BrightnessControl

ICON_PATH = Path(__file__).parent / "sun.png"


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


def main():
    autostart.register()

    app = QApplication([])
    app.setQuitOnLastWindowClosed(False)

    brightness_control = BrightnessControl()

    icon = QIcon(str(ICON_PATH))
    tray = QSystemTrayIcon()
    tray.setIcon(icon)
    tray.setVisible(True)

    menu = QMenu()
    about_action = QAction("About")
    about_action.triggered.connect(create_about_dialog)
    menu.addAction(about_action)
    quit_action = QAction("Quit")
    quit_action.triggered.connect(app.quit)
    menu.addAction(quit_action)

    def on_tray_icon_clicked(reason):
        if reason == QSystemTrayIcon.Trigger:
            brightness_control.show()

    tray.activated.connect(on_tray_icon_clicked)
    tray.setContextMenu(menu)

    app.exec_()


if __name__ == '__main__':
    main()
