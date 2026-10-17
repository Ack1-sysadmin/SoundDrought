# Microsoft Store listing

Texts to paste into Partner Center (Store listings), in English and German.

## Basics

| Field | Value |
|---|---|
| Category | Music |
| Privacy policy URL | https://ack1-sysadmin.github.io/SoundDrought/privacy.html |
| Website | https://ack1-sysadmin.github.io/SoundDrought/ |
| Support contact | https://github.com/Ack1-sysadmin/SoundDrought/issues |
| Pricing | Free |
| Age rating | IARC questionnaire: no violence, no user-to-user communication inside the app itself (it shows the SoundCloud website), no purchases |

## English (en-us)

**Product name:** SoundDrought

**Short description**
A slim desktop app for SoundCloud: media keys, mini player, equalizer and more.

**Description**
SoundDrought brings SoundCloud to your Windows desktop. It shows the regular soundcloud.com, so your account, likes, playlists and Go+ work as usual, and adds what a browser tab can't do:

• Media keys and the Windows media overlay
• Mini player that stays on top, as a compact bar or cover only, with volume and seek bar
• Equalizer with presets and loudness levelling
• Play shuffled button for playlists and likes
• Volume with the mouse wheel
• Sleep timer, and resume where you left off after a restart
• Taskbar controls, progress in the taskbar button and a jump list
• Discord status ("Listening to …"), can be turned off
• Clean title bar that fits any window size
• Lives in the tray and keeps playing when you close the window

SoundDrought is free and open source (MIT license). It is an independent project and not affiliated with or endorsed by SoundCloud.

**Features (one per line)**
Media keys and Windows media overlay
Mini player (compact or cover only)
Equalizer and loudness levelling
Play shuffled for playlists and likes
Volume with the mouse wheel
Sleep timer
Resume playback after restart
Discord status
Tray icon and taskbar controls

**Keywords:** soundcloud, music, player, desktop, mini player, equalizer, discord

## Deutsch (de-de)

**Kurzbeschreibung**
Schlanke Desktop-App für SoundCloud: Medientasten, Mini-Player, Equalizer und mehr.

**Beschreibung**
SoundDrought bringt SoundCloud auf deinen Windows-Desktop. Die App zeigt das normale soundcloud.com, dein Konto, deine Likes, Playlists und Go+ funktionieren also wie gewohnt, und ergänzt, was ein Browser-Tab nicht kann:

• Medientasten und das Windows-Medien-Overlay
• Mini-Player, der immer im Vordergrund bleibt, als kompakte Leiste oder nur mit Cover, mit Lautstärke und Fortschrittsleiste
• Equalizer mit Voreinstellungen und Lautstärke-Angleichung
• Zufallswiedergabe für Playlists und Likes
• Lautstärke per Mausrad
• Sleep-Timer und Fortsetzen nach einem Neustart
• Steuerung in der Taskleiste, Fortschritt im Taskleisten-Symbol und Sprungliste
• Discord-Status („Hört … zu"), abschaltbar
• Aufgeräumte Titelleiste, die in jede Fenstergröße passt
• Läuft im Infobereich weiter, wenn du das Fenster schließt

SoundDrought ist kostenlos und Open Source (MIT-Lizenz). Es ist ein unabhängiges Projekt und steht in keiner Verbindung zu SoundCloud.

## Notes for certification

SoundDrought is a WPF desktop app (Win32) packaged as MSIX; it needs `runFullTrust`.
It displays soundcloud.com in the WebView2 runtime that is part of Windows and adds desktop
features around it. Browsing works without an account; playing music needs a (free) SoundCloud account,
because SoundCloud asks visitors to sign in before playback.
Source code: https://github.com/Ack1-sysadmin/SoundDrought
