<p align="center"><img src="Assets/icon-256.png" width="120" alt="Potato Keys+ logo"></p>

<h1 align="center">Potato Keys+</h1>
<p align="center"><b>Your keyboard, supercharged.</b> Move the mouse with keys, make your own hotkeys, and pin it all on top of your screen.</p>

<p align="center">
  <a href="https://github.com/Harel-k/PotatoKeysPlus/releases/latest"><b>⬇ Download the latest installer</b></a>
</p>

---

## Features

### 🖱️ Keyboard Mouse
No mouse? Press **F8** and your keyboard becomes the mouse.

| Action | Default key |
|---|---|
| Mouse mode on / off | `F8` |
| Move (speeds up while held) | Arrow keys |
| Left / right / middle click (hold to drag) | `Z` / `X` / `C` |
| Scroll up / down | `Page Up` / `Page Down` |
| Boost / Precision (hold) | `Left Shift` / `Left Ctrl` |

You can change every key. Sliders set the speed, acceleration and scroll speed, and a small badge shows when mouse mode is on.

### ⌨️ Hotkeys
Make shortcuts that work anywhere in Windows. Each one can type text, press other keys, open an app, file or website, control media, click the mouse, toggle mouse mode, or pin the window.

### 📌 Always there
- **Pin** keeps the window on top of everything. **Compact** shrinks it to a mini widget.
- Lives in the **system tray**, so closing the window keeps your hotkeys working.
- Optionally **starts with Windows**.
- **Updates itself** automatically from GitHub Releases.

## Install
Download `PotatoKeysPlus-Setup-x.y.z.exe` from [Releases](https://github.com/Harel-k/PotatoKeysPlus/releases) and run it. No admin needed, and .NET comes bundled.

> Windows SmartScreen may warn about an "unknown publisher" because the installer isn't code-signed yet. Click **More info → Run anyway**.

## Building it yourself
You need the [.NET 9 SDK](https://dotnet.microsoft.com/download) and, for the installer, [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`).

```bash
dotnet run
```

```bash
powershell -ExecutionPolicy Bypass -File tools/build-release.ps1
```
That builds `dist/PotatoKeysPlus-Setup-<version>.exe`.

## Releasing a new version
1. Bump `<Version>` in `PotatoKeysPlus.csproj` (e.g. `0.3.0`).
2. Commit, then push a matching tag:
   ```bash
   git tag v0.3.0 && git push origin main v0.3.0
   ```
3. GitHub Actions (`.github/workflows/release.yml`) builds the installer and publishes the release.
   You can also do it locally with `tools/build-release.ps1 -Publish`.

Installed copies check the latest release on startup and every 6 hours. They download the `Setup` asset, check its SHA-256, and reinstall silently, then the app restarts on the new version.

## Project layout
| Path | What it is |
|---|---|
| `Input/` | Global keyboard hook, key names, input injection |
| `Modules/` | Keyboard Mouse, Hotkeys, and the `Engine` that routes keys |
| `Services/Updater.cs` | GitHub Releases auto-updater |
| `Services/Startup.cs` | Start-with-Windows (per-user Run key) |
| `TrayIcon.cs` | Notification-area icon and menu |
| `MainWindow*.cs`, `App.xaml` | UI, theme and pages |
| `installer/` | Inno Setup wizard script and artwork |
| `tools/` | Release script, icon/art generator (`make-art.py`) |

## Notes
- Windows doesn't let a normal app control apps running as administrator (Task Manager, installers). Run Potato Keys+ as admin if you need that.
- Settings live in `%APPDATA%\PotatoKeysPlus\settings.json`. Crashes are logged to `crash.log` in the same folder.

## License
[MIT](LICENSE) © 2026 Harel
