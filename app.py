import sys
import subprocess
from PyQt5.QtWidgets import QApplication, QWidget, QVBoxLayout, QLabel, QComboBox, QSlider
from PyQt5.QtCore import Qt
import re
import time 
import logging

logging.basicConfig(level=logging.DEBUG)

class BrightnessControl(QWidget):
    def __init__(self):
        super().__init__()
        self.init_ui()

    def init_ui(self):
        self.setWindowTitle('Brightness Control')
        layout = QVBoxLayout()

        self.monitor_selector = QComboBox()
        self.populate_monitors()
        self.monitor_selector.currentIndexChanged.connect(self.update_brightness_label)

        self.brightness_label = QLabel()
        self.update_brightness_label()

        self.brightness_slider = QSlider(Qt.Horizontal)
        self.brightness_slider.setMinimum(0)
        self.brightness_slider.setMaximum(100)

        self.brightness_slider.setValue(self.get_brightness('0'))
        self.brightness_slider.sliderReleased.connect(self.set_brightness)
        
        layout.addWidget(self.monitor_selector)
        layout.addWidget(self.brightness_label)
        layout.addWidget(self.brightness_slider)

        self.setLayout(layout)

    def populate_monitors(self):
        try:
            result = subprocess.check_output(['ddcutil', 'detect']).decode('utf-8')
            monitors = re.findall(r'Display \d+\n\s+I2C bus:\s+/dev/i2c-(\d+)\n\s+DRM connector:\s+(\S+)', result)

            for monitor in monitors:
                i2c_bus, drm_connector = monitor
                self.monitor_selector.addItem(f'/dev/i2c-{i2c_bus} - {drm_connector}')

        except subprocess.CalledProcessError as e:
            print(f"Error detecting monitors: {e}")

    def get_brightness(self, i2c_bus):
        try:
            result = subprocess.check_output(['ddcutil','--bus',f'{i2c_bus[-1]}' ,'getvcp', '10']).decode('utf-8')
            match = re.search(r'current value =\s*(\d+)', result)
            if match:
                brightness = int(match.group(1))
                return brightness
            else:
                print("Error parsing brightness value.")
                return 0
        except subprocess.CalledProcessError as e:
            print(f"Error getting brightness: {e}")
            return 0

    def set_brightness(self):
        start_time = time.time()

        brightness_value = self.brightness_slider.value()
        try:
            subprocess.run(['ddcutil', 'setvcp', '10', str(brightness_value),f'--bus',self.monitor_selector.currentText().split("i2c-")[1].split()[0],'--sleep-multiplier','.1'])
            self.brightness_label.setText(f'Current Brightness: {brightness_value}%')
        except subprocess.CalledProcessError as e:
            print(f"Error setting brightness: {e}")
        logging.debug(f"Time taken: {time.time() - start_time}")

    def update_brightness_label(self):
        i2c_bus = re.search(r'/dev/i2c-(\d+)', self.monitor_selector.currentText()).group(1)
        brightness = self.get_brightness(i2c_bus)
        self.brightness_label.setText(f'Current Brightness: {brightness}%')

if __name__ == '__main__':
    app = QApplication(sys.argv)
    window = BrightnessControl()
    window.show()
    sys.exit(app.exec_())
