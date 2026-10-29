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

### 🔎 Quick Launcher
Press **Alt + Space** and type. It opens any Start menu app (Store apps too), websites, files and Potato commands, does quick maths (`=12*7`), and searches the web. Apps you open most rank first.

### 🎯 Snippet Wheel
Hold **Ctrl + Alt + S** and a round menu of up to 8 actions pops up at your mouse. Pick with 1-8, the arrows or the mouse, then let go. It never steals focus, so typed text lands where you were typing.

### 🎮 Gamer Mode
Blocks the Windows key, Alt+Tab, optionally Alt+F4, and the Sticky Keys popups. Toggle it with **Ctrl + Alt + G**, or let it switch on by itself when a game (Minecraft, Roblox, ...) is in front.

### 🚨 Panic Key
**Ctrl + Alt + H** minimizes every window, mutes the PC and pauses music and videos. Press it again and everything comes back.

### 🔊 Key Sounds
Mechanical keyboard sounds on any keyboard, in 5 packs made from scratch in code (Thock, Clicky, Typewriter, Bubble, Soft), with low-latency mixing.

### ⌨️ Typing Trainer
Live WPM from your real typing, plus daily stats and a 7-day chart. It only counts keys, never what you type. Typing tests focus on the letters you miss most.

### 🖱️ Mouse Jiggler
**Ctrl + Alt + J** keeps the PC awake and your Teams/Discord status active, with an invisible 1-pixel nudge. It can run only while you're idle, and stop after a set time.

### 🔠 Symbol Lock
Caps Lock for symbols. **Shift + Caps Lock** makes the number row type `! @ # $ %` without holding Shift. Hold Shift to get the numbers back.

### ✍️ Text Expander & Text Tools
Type `;mail` and it becomes your email (`{date}`, `{time}`, `{clipboard}` work too). Select text and press **Ctrl + Alt + X** for UPPERCASE, lowercase, Title Case, Sentence case, join lines, sort lines or word count. **Ctrl + Alt + Shift + V** pastes as plain text.

### 📝 Quick Notes
**Ctrl + Alt + Q** pops up a notepad with several notes that save by themselves (plain `.txt` files).

### 🪟 Window Snapper & Pin Any Window
**Ctrl + Alt + arrows** snap the window in front to halves (press again for ⅓ and ⅔). **Ctrl + Alt + Home/PgUp/End/PgDn** snap to corners, **Ctrl + Alt + M** moves it to the next screen. **Ctrl + Alt + T** keeps any window on top, and **Ctrl + Alt + [ / ]** makes it see-through.

### 🎯 Grid Jump
**Ctrl + Alt + K** (or **G** in mouse mode) covers the screen with a lettered grid. Type two letters and the mouse jumps there.

### 📸 Screenshot
**Ctrl + Print Screen**: drag an area, blur private parts or draw boxes, then copy (Enter) or save (Ctrl + S).

### 🔉 App Volume
**Ctrl + Alt + = / - / 0** turns only the app in front up, down or mute, plus a mini mixer on the settings page.

### 💾 Settings Backup
Export everything (settings, hotkeys, snippets, expansions, notes) to one `.potatokeys` file and import it on another PC.

### 🛡️ Safety Lock
The shield button in the title bar (or the tray menu, or **Ctrl + Alt + Shift + L**) switches every module off at once. It stays on, even after a restart, until you click **Unlock** with your own mouse. Clicks and keys sent by other programs or scripts can't unlock it.

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
| `Modules/` | Every module, plus the `Engine` that routes keys between them |
| `Audio/` | Key Sounds synthesizer and mixer (NAudio) |
| `LauncherWindow.cs`, `SnippetWheelWindow.cs` | Quick Launcher and Snippet Wheel pop-ups |
| `Services/Updater.cs` | GitHub Releases auto-updater |
| `Services/Startup.cs` | Start-with-Windows (per-user Run key) |
| `TrayIcon.cs` | Notification-area icon and menu |
| `MainWindow*.cs`, `App.xaml` | UI, theme and pages |
| `installer/` | Inno Setup wizard script and artwork |
| `tools/` | Release script, icon/art generator (`make-art.py`) |

## Notes
- **AltGr** (Right Alt) never triggers Ctrl+Alt hotkeys, so `@ € { }` still type on European keyboards.
- Keys from the Windows On-Screen Keyboard and accessibility tools work with every module.
- Windows doesn't let a normal app control apps running as administrator (Task Manager, installers). Run Potato Keys+ as admin if you need that.
- Settings live in `%APPDATA%\PotatoKeysPlus\settings.json`. Crashes are logged to `crash.log` in the same folder.

## License
[MIT](LICENSE) © 2026 Harel
