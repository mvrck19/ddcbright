import logging
import sys

from monitorcontrol import VCPError, get_monitors
from PyQt5.QtCore import Qt
from PyQt5.QtWidgets import QComboBox, QLabel, QSlider, QVBoxLayout, QWidget

logging.basicConfig(level=logging.INFO)

DEFAULT_ACCENT = "#0078d4"


def clamp_brightness(value: int) -> int:
    return max(0, min(100, int(value)))


def is_dark_mode() -> bool:
    if sys.platform != "win32":
        return False
    import winreg
    try:
        with winreg.OpenKey(
            winreg.HKEY_CURRENT_USER,
            r"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
        ) as key:
            value, _ = winreg.QueryValueEx(key, "AppsUseLightTheme")
        return value == 0
    except OSError:
        return False


def get_accent_color() -> str:
    if sys.platform != "win32":
        return DEFAULT_ACCENT
    import winreg
    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Software\Microsoft\Windows\DWM") as key:
            value, _ = winreg.QueryValueEx(key, "AccentColor")
        # AccentColor is a DWORD packed as 0xAABBGGRR.
        r = value & 0xFF
        g = (value >> 8) & 0xFF
        b = (value >> 16) & 0xFF
        return f"#{r:02x}{g:02x}{b:02x}"
    except OSError:
        return DEFAULT_ACCENT


def _apply_windows_backdrop(hwnd: int, dark: bool) -> bool:
    """Best-effort DWM Acrylic backdrop + native rounded corners (Windows 11
    22H2+). Returns True if the translucent backdrop applied, so the caller
    can skip painting a solid background over it. No-ops (returns False) on
    older Windows where these DWM attributes don't exist."""
    import ctypes

    DWMWA_USE_IMMERSIVE_DARK_MODE = 20
    DWMWA_WINDOW_CORNER_PREFERENCE = 33
    DWMWA_SYSTEMBACKDROP_TYPE = 38
    DWMWCP_ROUND = 2
    DWMSBT_TRANSIENTWINDOW = 3  # backdrop meant for flyouts/context menus

    dwmapi = ctypes.windll.dwmapi

    def set_attr(attr, value) -> bool:
        c_value = ctypes.c_int(value)
        hresult = dwmapi.DwmSetWindowAttribute(
            ctypes.c_void_p(hwnd), attr, ctypes.byref(c_value), ctypes.sizeof(c_value)
        )
        return hresult == 0

    set_attr(DWMWA_USE_IMMERSIVE_DARK_MODE, 1 if dark else 0)
    set_attr(DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND)
    return set_attr(DWMWA_SYSTEMBACKDROP_TYPE, DWMSBT_TRANSIENTWINDOW)


def build_stylesheet(dark: bool, accent: str, translucent: bool) -> str:
    text_color = "#f0f0f0" if dark else "#1a1a1a"
    control_bg = "rgba(255,255,255,0.08)" if dark else "rgba(0,0,0,0.05)"
    border = "1px solid rgba(255,255,255,0.12)" if dark else "1px solid rgba(0,0,0,0.08)"
    panel_bg = "transparent" if translucent else ("#2b2b2b" if dark else "#f3f3f3")

    return f"""
    #popup {{
        background-color: {panel_bg};
        border: {border};
        border-radius: 8px;
    }}
    QLabel {{ color: {text_color}; background: transparent; }}
    QComboBox {{
        color: {text_color};
        background-color: {control_bg};
        border: 1px solid transparent;
        border-radius: 4px;
        padding: 4px 8px;
    }}
    QComboBox:hover {{ border: 1px solid {accent}; }}
    QSlider::groove:horizontal {{
        height: 4px;
        background: {control_bg};
        border-radius: 2px;
    }}
    QSlider::sub-page:horizontal {{
        background: {accent};
        border-radius: 2px;
    }}
    QSlider::handle:horizontal {{
        background: {accent};
        width: 16px;
        height: 16px;
        margin: -6px 0;
        border-radius: 8px;
    }}
    """


class BrightnessControl(QWidget):
    def __init__(self):
        super().__init__()
        self.monitors = []
        self.init_ui()

    def init_ui(self):
        self.setWindowTitle('Brightness Control')
        # ponytail: Qt.Popup gives click-away dismissal for free, matching
        # how the OS's own volume/brightness flyouts behave.
        self.setWindowFlags(Qt.Popup | Qt.FramelessWindowHint)
        self.setAttribute(Qt.WA_TranslucentBackground)
        # Plain QWidget ignores stylesheet background/border-radius unless
        # this is set -- without it the card styling silently doesn't paint.
        self.setAttribute(Qt.WA_StyledBackground, True)
        self.setObjectName("popup")

        dark = is_dark_mode()
        translucent = False
        if sys.platform == "win32":
            translucent = _apply_windows_backdrop(int(self.winId()), dark)
        self.setStyleSheet(build_stylesheet(dark, get_accent_color(), translucent))

        layout = QVBoxLayout()
        layout.setContentsMargins(16, 12, 16, 12)
        layout.setSpacing(8)

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
