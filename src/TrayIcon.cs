using System;
using System.Drawing;
using System.Windows.Forms;

namespace SoundDrought;

public sealed class TrayIcon : IDisposable
{
    private readonly MainWindow _window;
    private readonly Settings _settings;
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _nowPlaying;
    private readonly ToolStripMenuItem _playPause;
    private readonly ToolStripMenuItem _like;

    public TrayIcon(MainWindow window, Settings settings)
    {
        _window = window;
        _settings = settings;

        _nowPlaying = new ToolStripMenuItem("Nichts wird abgespielt") { Enabled = false };
        _playPause = new ToolStripMenuItem("Abspielen", null, (_, _) => _window.Send("toggle"));
        _like = new ToolStripMenuItem("Gefällt mir", null, (_, _) => _window.Send("like"));
        var closeToTray = new ToolStripMenuItem("Beim Schließen in den Infobereich")
        {
            Checked = settings.CloseToTray,
            CheckOnClick = true,
        };
        closeToTray.CheckedChanged += (_, _) =>
        {
            _settings.CloseToTray = closeToTray.Checked;
            _settings.Save();
        };
        var autoStart = new ToolStripMenuItem("Mit Windows starten") { Checked = AutoStart.IsEnabled, CheckOnClick = true };
        autoStart.CheckedChanged += (_, _) => AutoStart.IsEnabled = autoStart.Checked;

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(new ToolStripItem[]
        {
            _nowPlaying,
            new ToolStripSeparator(),
            _playPause,
            new ToolStripMenuItem("Weiter", null, (_, _) => _window.Send("next")),
            new ToolStripMenuItem("Zurück", null, (_, _) => _window.Send("prev")),
            _like,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Fenster anzeigen", null, (_, _) => _window.ShowMain()),
            new ToolStripMenuItem("Mini-Player", null, (_, _) => _window.ShowMini()) { ShortcutKeyDisplayString = "Strg+M" },
            new ToolStripSeparator(),
            closeToTray,
            autoStart,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Beenden", null, (_, _) => _window.Exit()),
        });

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "SoundDrought",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) _window.ToggleVisibility();
            else if (e.Button == MouseButtons.Middle) _window.Send("toggle");
        };
    }

    public void Update(PlayerState state)
    {
        var track = state.HasTrack ? $"{state.Title} – {state.Artist}" : "Nichts wird abgespielt";
        _nowPlaying.Text = track.Replace("&", "&&");
        _playPause.Text = state.Playing ? "Pausieren" : "Abspielen";
        _like.Checked = state.Liked;
        _icon.Text = Truncate(state.HasTrack ? $"{(state.Playing ? "▶" : "⏸")} {track}" : "SoundDrought", 127);
    }

    public void ShowHintOnce()
    {
        if (_settings.TrayHintShown) return;
        _settings.TrayHintShown = true;
        _settings.Save();
        _icon.ShowBalloonTip(3000, "Läuft weiter im Hintergrund",
            "Klick aufs Symbol öffnet das Fenster, Rechtsklick zeigt die Steuerung.", ToolTipIcon.None);
    }

    private static Icon LoadIcon()
    {
        var info = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
        using var stream = info!.Stream;
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
