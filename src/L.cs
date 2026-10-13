using System.Globalization;

namespace SoundDrought;

/// <summary>UI strings in the system language: German for "de", English for everything else.</summary>
public static class L
{
    private static readonly bool German = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";

    private static string T(string de, string en) => German ? de : en;

    public static string Play => T("Abspielen", "Play");
    public static string Pause => T("Pausieren", "Pause");
    public static string Next => T("Weiter", "Next");
    public static string Previous => T("Zurück", "Previous");
    public static string Like => T("Gefällt mir", "Like");
    public static string NothingPlaying => T("Nichts wird abgespielt", "Nothing playing");

    public static string ShowWindow => T("Fenster anzeigen", "Show window");
    public static string MiniPlayer => "Mini-Player";
    public static string MiniPlayerShortcut => T("Strg+M", "Ctrl+M");
    public static string FullWindow => T("Großes Fenster (Strg+M)", "Full window (Ctrl+M)");
    public static string HideToTray => T("In den Infobereich", "Hide to tray");
    public static string CloseToTray => T("Beim Schließen in den Infobereich", "Keep running in tray when closed");
    public static string StartWithWindows => T("Mit Windows starten", "Start with Windows");
    public static string DiscordStatus => T("Discord-Status anzeigen", "Show Discord status");
    public static string Exit => T("Beenden", "Exit");

    public static string TrayHintTitle => T("Läuft weiter im Hintergrund", "Still playing in the background");
    public static string TrayHintText => T(
        "Klick aufs Symbol öffnet das Fenster, Rechtsklick zeigt die Steuerung.",
        "Click the icon to open the window, right-click for controls.");

    public static string ListenOnSoundCloud => T("Auf SoundCloud anhören", "Listen on SoundCloud");

    // Navigation bar
    public static string Back => T("Zurück", "Back");
    public static string Forward => T("Vorwärts", "Forward");
    public static string Home => T("Start", "Home");
    public static string SearchPlaceholder => T("Was möchtest du hören?", "What do you want to listen to?");
    public static string Feed => "Feed";
    public static string Library => T("Bibliothek", "Library");
    public static string Notifications => T("Benachrichtigungen", "Notifications");
    public static string Messages => T("Nachrichten", "Messages");
    public static string Account => T("Konto", "Account");
    public static string Profile => T("Profil", "Profile");
    public static string Likes => "Likes";
    public static string Playlists => "Playlists";
    public static string Stations => T("Sender", "Stations");
    public static string Following => T("Folge ich", "Following");
    public static string History => T("Verlauf", "History");
    public static string Upload => T("Hochladen", "Upload");
    public static string SoundCloudSettings => T("Einstellungen", "Settings");
    public static string SignIn => T("Anmelden", "Sign in");
    public static string SignOut => T("Abmelden", "Sign out");
    public static string Minimize => T("Minimieren", "Minimize");
    public static string Maximize => T("Maximieren", "Maximize");
    public static string Restore => T("Wiederherstellen", "Restore");
    public static string Close => T("Schließen", "Close");
}
