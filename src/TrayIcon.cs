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

        _nowPlaying = new ToolStripMenuItem(L.NothingPlaying) { Enabled = false };
        _playPause = new ToolStripMenuItem(L.Play, null, (_, _) => _window.Send("toggle"));
        _like = new ToolStripMenuItem(L.Like, null, (_, _) => _window.Send("like"));

        var closeToTray = Toggle(L.CloseToTray, settings.CloseToTray, on => _settings.CloseToTray = on);
        var discord = Toggle(L.DiscordStatus, settings.DiscordStatus, on => _settings.DiscordStatus = on);
        var autoStart = new ToolStripMenuItem(L.StartWithWindows) { Checked = AutoStart.IsEnabled, CheckOnClick = true };
        autoStart.CheckedChanged += (_, _) => AutoStart.IsEnabled = autoStart.Checked;

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(new ToolStripItem[]
        {
            _nowPlaying,
            new ToolStripSeparator(),
            _playPause,
            new ToolStripMenuItem(L.Next, null, (_, _) => _window.Send("next")),
            new ToolStripMenuItem(L.Previous, null, (_, _) => _window.Send("prev")),
            _like,
            new ToolStripSeparator(),
            new ToolStripMenuItem(L.ShowWindow, null, (_, _) => _window.ShowMain()),
            new ToolStripMenuItem(L.MiniPlayer, null, (_, _) => _window.ShowMini()) { ShortcutKeyDisplayString = L.MiniPlayerShortcut },
            new ToolStripSeparator(),
            closeToTray,
            autoStart,
            discord,
            new ToolStripSeparator(),
            new ToolStripMenuItem(L.Exit, null, (_, _) => _window.Exit()),
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

    /// <summary>Raised after a setting was toggled from the menu.</summary>
    public event Action? SettingsChanged;

    private ToolStripMenuItem Toggle(string text, bool initial, Action<bool> apply)
    {
        var item = new ToolStripMenuItem(text) { Checked = initial, CheckOnClick = true };
        item.CheckedChanged += (_, _) =>
        {
            apply(item.Checked);
            _settings.Save();
            SettingsChanged?.Invoke();
        };
        return item;
    }

    public void Update(PlayerState state)
    {
        var track = state.HasTrack ? $"{state.Title} – {state.Artist}" : L.NothingPlaying;
        _nowPlaying.Text = track.Replace("&", "&&");
        _playPause.Text = state.Playing ? L.Pause : L.Play;
        _like.Checked = state.Liked;
        _icon.Text = Truncate(state.HasTrack ? $"{(state.Playing ? "▶" : "⏸")} {track}" : "SoundDrought", 127);
    }

    public void ShowHintOnce()
    {
        if (_settings.TrayHintShown) return;
        _settings.TrayHintShown = true;
        _settings.Save();
        _icon.ShowBalloonTip(3000, L.TrayHintTitle, L.TrayHintText, ToolTipIcon.None);
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
