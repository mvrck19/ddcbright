#!/usr/bin/env python3
import sys
from pathlib import Path

from PyQt5.QtCore import Qt
from PyQt5.QtGui import QIcon
from PyQt5.QtWidgets import QAction, QApplication, QMenu, QMessageBox, QSystemTrayIcon

from . import autostart
from .app import BrightnessControl

ICON_PATH = Path(__file__).parent / ("sun.ico" if sys.platform == "win32" else "sun.png")


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
    if sys.platform == "win32":
        import ctypes
        # Must run before QApplication/any window exists, or Windows keeps
        # grouping/labeling the app under "Python" in the taskbar.
        ctypes.windll.shell32.SetCurrentProcessExplicitAppUserModelID("mvrck19.ddcbright")

    QApplication.setAttribute(Qt.AA_EnableHighDpiScaling, True)
    QApplication.setAttribute(Qt.AA_UseHighDpiPixmaps, True)

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
        if reason != QSystemTrayIcon.Trigger:
            return

        icon_rect = tray.geometry()
        size = brightness_control.sizeHint()
        screen = QApplication.screenAt(icon_rect.center()) or QApplication.primaryScreen()
        available = screen.availableGeometry()

        if icon_rect.isValid() and not icon_rect.isEmpty():
            x = icon_rect.right() - size.width()
            y = icon_rect.top() - size.height()
        else:
            # ponytail: some Linux tray implementations don't report icon
            # geometry -- fall back to the bottom-right corner, where a
            # taskbar tray usually lives.
            x = available.right() - size.width()
            y = available.bottom() - size.height()

        x = max(available.left(), min(x, available.right() - size.width()))
        y = max(available.top(), min(y, available.bottom() - size.height()))

        brightness_control.move(x, y)
        brightness_control.show()
        brightness_control.activateWindow()

    tray.activated.connect(on_tray_icon_clicked)
    tray.setContextMenu(menu)

    app.exec_()


if __name__ == '__main__':
    main()
