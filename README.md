# DDCBright

Screen brightness, actually controlled. A native Windows tray app for DDC/CI
monitor brightness — no Electron, no bundled runtime, just a real Mica
flyout that matches your Windows theme and gets out of your way.

<table>
<tr>
<td><img src="assets/screenshots/flyout-dark.png" width="320" alt="DDCBright flyout, dark theme, Ambient mode"></td>
<td><img src="assets/screenshots/settings-ambient.png" width="380" alt="DDCBright Settings, Ambient mode with the webcam Camera picker"></td>
</tr>
<tr>
<td><img src="assets/screenshots/flyout-light.png" width="320" alt="DDCBright flyout, light theme, Schedule mode"></td>
<td><img src="assets/screenshots/settings-schedule.png" width="380" alt="DDCBright Settings, Schedule mode with a gradual fade"></td>
</tr>
</table>

## Features

- **Flyout** — click the tray icon, drag a slider per monitor, done. Scroll over the tray icon to nudge brightness without even opening it.
- **Schedule** — dim in the evening, brighten in the morning, with an optional gradual fade instead of an instant jump.
- **Ambient** — samples your webcam every 30 seconds and estimates room brightness instead of following a fixed clock. Never dims below 10%, and Settings has a camera picker plus a one-click "Test now" to check it's actually reading your camera.
- **Native UI** — real Mica/Acrylic, matches your Windows light/dark theme and accent color, not a repainted cross-platform toolkit window.
- **Quiet** — registers its own autostart at login on first run; no installer nagging, no background service beyond the tray icon itself.

## Install

Grab an asset from [Releases](https://github.com/mvrck19/ddcbright/releases):

- **Installer** — download `ddcbright-setup.exe` and run it. Adds a Start Menu shortcut and a proper Add/Remove Programs entry. No admin rights needed (installs to your user profile).
- **Portable** — download `ddcbright.exe` and run it directly. Self-contained, nothing to install, nothing left behind but the autostart entry it registers on first run.

## Troubleshooting

- Make sure your monitor supports DDC/CI and it's enabled in the monitor's OSD menu — DDC/CI access uses the OS's own display API, no extra setup needed on Windows.
- Ambient mode not reacting? Open Settings → Ambient → **Test now** to see exactly what the camera captured, or check `%AppData%\ddcbright\ambient.log` for a per-attempt history.

## Linux (secondary)

A separate Python/PyQt5 implementation lives in `ddcbright/`, built on [`monitorcontrol`](https://github.com/newAM/monitorcontrol) for DDC/CI. It gets less attention than the Windows app.

```bash
sudo dpkg -i ddcbright.deb && pip install monitorcontrol   # not packaged for apt
# or
flatpak install ddcbright.flatpak
```

Needs `i2c` group access: `sudo usermod -aG i2c $USER`, then log out and back in. macOS isn't supported (`monitorcontrol` has no macOS backend).

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

For changes to the Windows app, verify with `dotnet test windows/DdcBright.UiTests/DdcBright.UiTests.csproj` (drives the real Settings window via FlaUI/UI Automation) before opening a PR.

## License

[MIT](LICENSE)
