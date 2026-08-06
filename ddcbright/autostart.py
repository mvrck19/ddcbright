"""Registers ddcbright to launch itself at login, once, idempotently."""
import logging
import sys
from pathlib import Path

APP_NAME = "ddcbright"


def _register_windows():
    import winreg

    key_path = r"Software\Microsoft\Windows\CurrentVersion\Run"
    command = f'"{sys.executable}" -m ddcbright'
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER, key_path, 0, winreg.KEY_ALL_ACCESS) as key:
        try:
            existing, _ = winreg.QueryValueEx(key, APP_NAME)
            if existing == command:
                return
        except FileNotFoundError:
            pass
        winreg.SetValueEx(key, APP_NAME, 0, winreg.REG_SZ, command)


def _register_linux():
    autostart_dir = Path.home() / ".config" / "autostart"
    dest = autostart_dir / f"{APP_NAME}.desktop"
    if dest.exists():
        return

    icon_path = Path(__file__).parent / "sun.png"
    repo_root = Path(__file__).parent.parent
    entry = (
        "[Desktop Entry]\n"
        "Type=Application\n"
        "Name=DDCBright\n"
        "Comment=Brightness Control Application\n"
        f"Exec=bash -c \"PYTHONPATH='{repo_root}' '{sys.executable}' -m ddcbright\"\n"
        f"Icon={icon_path}\n"
        "Terminal=false\n"
        "Categories=Utility;\n"
    )
    autostart_dir.mkdir(parents=True, exist_ok=True)
    dest.write_text(entry)


def register():
    """Best-effort; a failure here should never stop the app from running."""
    try:
        if sys.platform == "win32":
            _register_windows()
        elif sys.platform.startswith("linux"):
            _register_linux()
    except OSError as e:
        logging.warning(f"Could not register autostart: {e}")
