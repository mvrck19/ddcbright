from PyQt5.QtWidgets import QApplication, QSystemTrayIcon, QMenu, QAction
from PyQt5.QtGui import QIcon
from app import BrightnessControl

app = QApplication([])
app.setQuitOnLastWindowClosed(False)

# Create BrightnessControl
brightness_control = BrightnessControl()

# Create the icon
icon = QIcon("/home/mvrc/Dev/ddcbrit/sun.png")

# Create the tray
tray = QSystemTrayIcon()
tray.setIcon(icon)
tray.setVisible(True)

# Create the menu
menu = QMenu()
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