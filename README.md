<p align="center">
  <img src="docs/icon.png" width="96" alt="SoundDrought icon">
</p>

<h1 align="center">SoundDrought</h1>

<p align="center"><b>SoundCloud for Windows. Not a single cloud in sight. ☀️</b></p>

<p align="center"><a href="https://ack1-sysadmin.github.io/SoundDrought/">Website</a> · <a href="../../releases/latest">Download</a></p>

SoundDrought is a small, fast desktop app for SoundCloud on Windows. It runs the real
soundcloud.com in the Edge engine (WebView2) that is already on your PC, and adds the
things a browser tab can't do:

- **Spotify-style title bar** with back/forward, search (`Ctrl+L`) and your account menu. It adapts to any window width, so you can make the window really small.
- **Media keys and the Windows media overlay** work out of the box
- **Tray icon**: left-click shows or hides the window, middle-click plays or pauses, right-click opens the controls
- **Taskbar thumbnail buttons**: ⏮ ⏯ ⏭
- **Mini player** (`Ctrl+M`): always on top, with artwork, like button, volume and a seek bar
- **Volume with the mouse wheel**: anywhere over the mini player, or over the player bar in the main window
- **Play shuffled**: a shuffle button on playlists and on likes pages starts the list in random order
- **Keeps playing in the tray** when you close the window
- **Discord status**: "Listening to …" with the track, artwork and a link to it (can be turned off in the tray menu)
- **Start with Windows**, straight to the tray
- **German or English UI**, following your Windows language
- Remembers your login, window position, zoom level (`Ctrl` + mouse wheel) and last page
- Dark title bar, single instance, a ~1.5 MB executable

Your login, likes, playlists and Go+ all work as usual, because it *is* SoundCloud, just without the browser around it.

## Install

1. Install the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x64), if you don't have it yet.
2. Download `SoundDrought.exe` from the [latest release](../../releases/latest) and run it.

Or with winget (once the package is approved): `winget install Ack1.SoundDrought`

The WebView2 runtime ships with Windows 10 and 11.

## Build

```powershell
dotnet build src -c Release                 # development build
dotnet publish src -c Release -r win-x64    # single-file exe in src/bin/Release/.../publish
```

Pushing a tag like `v0.2.0` makes GitHub Actions build the exe and attach it to a release.

## How it works

| File | Purpose |
|---|---|
| `src/MainWindow.xaml.cs` | Hosts WebView2, routes popups and external links, manages the views |
| `src/Assets/inject.js` | Runs inside soundcloud.com, reports player state and clicks the player controls |
| `src/NavBar.xaml` | The title bar that replaces SoundCloud's header |
| `src/MiniPlayer.xaml` | The compact always-on-top player |
| `src/TrayIcon.cs` | Tray icon and menu |
| `src/DiscordPresence.cs` | Discord status over Discord's local IPC pipe |
| `src/L.cs` | UI strings (German / English) |
| `tools/make-icon.ps1` | Generates `src/Assets/app.ico` |
| `docs/` | The website (GitHub Pages) |
| `packaging/winget/` | winget manifests |

App data, including the WebView2 profile, lives in `%LOCALAPPDATA%\SoundDrought`.

## Uninstall

1. Turn off "Start with Windows" in the tray menu (if you turned it on), then choose "Exit".
2. Delete `SoundDrought.exe`, or run `winget uninstall Ack1.SoundDrought` if you installed it with winget.
3. Delete `%LOCALAPPDATA%\SoundDrought` to remove your settings and sign-in.

## Privacy

SoundDrought has no servers, analytics or telemetry. It connects to soundcloud.com (the website you use),
to the sign-in provider you pick there, and, if Discord is running, tells your local Discord app which
track is playing (you can turn that off in the tray menu). Details: [privacy policy](https://ack1-sysadmin.github.io/SoundDrought/privacy.html).

## Code signing policy

Free code signing provided by [SignPath.io](https://signpath.io), certificate by [SignPath Foundation](https://signpath.org).
Signing starts once the project has been approved; releases up to v0.3.3 are unsigned.
Releases are built only by GitHub Actions from tagged commits, and every signing request is approved by hand.

| Role | Members |
|---|---|
| Committers and reviewers | [Ack1-sysadmin](https://github.com/Ack1-sysadmin) |
| Approvers | [Ack1-sysadmin](https://github.com/Ack1-sysadmin) |

Full policy: [Code signing policy](https://ack1-sysadmin.github.io/SoundDrought/code-signing.html)

## License

[MIT](LICENSE)

## Disclaimer

SoundDrought is an unofficial, independent project. It is not affiliated with or endorsed by
SoundCloud Global Limited & Co. KG. "SoundCloud" is a trademark of its respective owner.
