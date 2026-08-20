# ddcbright - Brightness Control Application

A tray/menu-bar utility for controlling monitor brightness over DDC/CI. It sits in the
system tray with a sun icon; click it to open the brightness flyout. It registers itself
to start automatically at login, so you don't need to launch it by hand each session.

## Platform support

Each platform gets its own native implementation rather than one shared compromise UI:

- **Windows**: a native C#/WPF app (`windows/DdcBright/`) with a real Mica/Acrylic
  flyout, matching your Windows light/dark theme and accent color.
- **Linux**: a Python/PyQt5 app (`ddcbright/`) via
  [`monitorcontrol`](https://github.com/newAM/monitorcontrol) for DDC/CI, packaged as
  `.deb` or Flatpak.
- **macOS**: not currently supported (`monitorcontrol` has no macOS backend).

## Installation

Grab the asset for your platform from the [Releases](https://github.com/mvrck19/ddcbright/releases)
page:

### Windows

- **Installer**: download `ddcbright-setup.exe` and run it — adds a Start Menu shortcut
  and a proper entry in Add/Remove Programs. No admin rights needed (installs to your
  user profile).
- **Portable**: download `ddcbright.exe` and run it directly — self-contained, nothing
  to install, nothing left behind but the autostart entry it registers on first run.

### Linux

```bash
sudo dpkg -i ddcbright.deb
pip install monitorcontrol  # not packaged for apt; one-time step
```

or install the Flatpak bundle:

```bash
flatpak install ddcbright.flatpak
```

### Run from source (Linux, or Windows without the native app)

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
- **Windows**: no extra setup needed; DDC/CI access uses the OS's own display API.

## Roadmap

Not implemented yet, but planned:
- Brightness schedules (e.g. dim automatically in the evening)
- Ambient-light-based auto-brightness using the webcam

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

For changes to the Windows app, verify with `dotnet test windows/DdcBright.UiTests/DdcBright.UiTests.csproj` (drives the real Settings window via FlaUI/UI Automation) before opening a PR.

## License

This project is licensed under the MIT License.
