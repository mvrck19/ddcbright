import logging

from monitorcontrol import VCPError, get_monitors
from PyQt5.QtCore import Qt
from PyQt5.QtWidgets import QComboBox, QLabel, QSlider, QVBoxLayout, QWidget

logging.basicConfig(level=logging.INFO)


def clamp_brightness(value: int) -> int:
    return max(0, min(100, int(value)))


class BrightnessControl(QWidget):
    def __init__(self):
        super().__init__()
        self.monitors = []
        self.init_ui()

    def init_ui(self):
        self.setWindowTitle('Brightness Control')
        layout = QVBoxLayout()

        self.monitor_selector = QComboBox()
        self.populate_monitors()
        self.monitor_selector.currentIndexChanged.connect(self.update_brightness_label)

        self.brightness_slider = QSlider(Qt.Horizontal)
        self.brightness_slider.setMinimum(0)
        self.brightness_slider.setMaximum(100)

        self.brightness_label = QLabel()
        self.update_brightness_label()
        self.brightness_slider.valueChanged.connect(self.set_brightness)

        layout.addWidget(self.monitor_selector)
        layout.addWidget(self.brightness_label)
        layout.addWidget(self.brightness_slider)

        self.setLayout(layout)

    def populate_monitors(self):
        try:
            self.monitors = get_monitors()
        except VCPError as e:
            logging.error(f"Error detecting monitors: {e}")
            self.monitors = []

        for index, monitor in enumerate(self.monitors):
            self.monitor_selector.addItem(f'Monitor {index + 1}', userData=monitor)

    def current_monitor(self):
        return self.monitor_selector.currentData()

    def get_brightness(self, monitor):
        if monitor is None:
            return 0
        try:
            with monitor:
                return clamp_brightness(monitor.get_luminance())
        except VCPError as e:
            logging.error(f"Error getting brightness: {e}")
            self.brightness_label.setText("Error reading brightness")
            return 0

    def set_brightness(self):
        monitor = self.current_monitor()
        if monitor is None:
            return

        brightness_value = clamp_brightness(self.brightness_slider.value())
        try:
            with monitor:
                monitor.set_luminance(brightness_value)
            self.brightness_label.setText(f'Current Brightness: {brightness_value}%')
        except VCPError as e:
            logging.error(f"Error setting brightness: {e}")
            self.brightness_label.setText("Error setting brightness")

    def update_brightness_label(self):
        brightness = self.get_brightness(self.current_monitor())
        self.brightness_label.setText(f'Current Brightness: {brightness}%')
        self.brightness_slider.blockSignals(True)
        self.brightness_slider.setValue(brightness)
        self.brightness_slider.blockSignals(False)
