# ddcbright - Brightness Control Application

A tray/menu-bar utility for controlling monitor brightness over DDC/CI. It sits in the
system tray with a sun icon; click it to open the brightness slider. It registers itself
to start automatically at login, so you don't need to launch it by hand each session.

## Platform support

- **Linux** and **Windows** — via [`monitorcontrol`](https://github.com/newAM/monitorcontrol),
  which talks DDC/CI directly (no `ddcutil` install needed anymore).
- **macOS** — not currently supported (the underlying `monitorcontrol` library has no macOS
  backend).

## Installation

### Option 1: Download the `.deb` (Linux)

Download from the [Releases](https://github.com/mvrck19/ddcbright/releases) page, then:

```bash
sudo dpkg -i ddcbright.deb
pip install monitorcontrol  # not packaged for apt; one-time step
```

### Option 2: Run from source (Linux or Windows)

```bash
git clone https://github.com/mvrck19/ddcbright.git
cd ddcbright
pip install -r requirements.txt
python -m ddcbright
```

The first run registers autostart-at-login automatically (Windows: a Registry `Run` entry;
Linux: an `~/.config/autostart/ddcbright.desktop` entry). Quit any time from the tray menu.

## Troubleshooting

- Make sure your monitor supports DDC/CI and it's enabled in the monitor's OSD menu.
- **Linux**: your user needs access to the I2C devices — add yourself to the `i2c` group
  (`sudo usermod -aG i2c $USER`, then log out and back in).
- **Windows**: no extra setup needed beyond the pip install; DDC/CI access uses the OS's
  own display API.

## Roadmap

Not implemented yet, but planned:
- Brightness schedules (e.g. dim automatically in the evening)
- Ambient-light-based auto-brightness using the webcam

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License.
