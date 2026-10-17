using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace SoundDrought;

public sealed class TrayIcon : IDisposable
{
    private static readonly int[] SleepChoices = [15, 30, 45, 60, 90];

    private readonly MainWindow _window;
    private readonly Settings _settings;
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _update;
    private readonly ToolStripSeparator _updateSeparator;
    private readonly ToolStripMenuItem _nowPlaying;
    private readonly ToolStripMenuItem _playPause;
    private readonly ToolStripMenuItem _like;
    private readonly ToolStripMenuItem _sleep;
    private string? _updateUrl;
    private string _trackTooltip = "SoundDrought";

    public TrayIcon(MainWindow window, Settings settings)
    {
        _window = window;
        _settings = settings;

        _update = new ToolStripMenuItem("", null, (_, _) => OpenUpdatePage()) { Visible = false, Font = new Font(SystemFonts.MenuFont!, FontStyle.Bold) };
        _updateSeparator = new ToolStripSeparator { Visible = false };
        _nowPlaying = new ToolStripMenuItem(L.NothingPlaying) { Enabled = false };
        _playPause = new ToolStripMenuItem(L.Play, null, (_, _) => _window.Send("toggle"));
        _like = new ToolStripMenuItem(L.Like, null, (_, _) => _window.Send("like"));
        _sleep = new ToolStripMenuItem(L.SleepTimer);
        _sleep.DropDownOpening += (_, _) => BuildSleepMenu();
        BuildSleepMenu();

        var closeToTray = Toggle(L.CloseToTray, settings.CloseToTray, on => _settings.CloseToTray = on);
        var discord = Toggle(L.DiscordStatus, settings.DiscordStatus, on => _settings.DiscordStatus = on);
        var upsells = Toggle(L.HideUpsells, settings.HideUpsells, on => _settings.HideUpsells = on);
        var updates = Toggle(L.CheckForUpdates, settings.CheckForUpdates, on => _settings.CheckForUpdates = on);
        updates.Visible = !Native.IsPackaged; // the Store updates the app itself

        ToolStripMenuItem autoStart;
        if (Native.IsPackaged)
        {
            // Packaged apps can't use the Run key; Windows manages their startup entry.
            autoStart = new ToolStripMenuItem(L.StartWithWindows + " …", null,
                (_, _) => Process.Start(new ProcessStartInfo("ms-settings:startupapps") { UseShellExecute = true }));
        }
        else
        {
            autoStart = new ToolStripMenuItem(L.StartWithWindows) { Checked = AutoStart.IsEnabled, CheckOnClick = true };
            autoStart.CheckedChanged += (_, _) => AutoStart.IsEnabled = autoStart.Checked;
        }

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(new ToolStripItem[]
        {
            _update,
            _updateSeparator,
            _nowPlaying,
            new ToolStripSeparator(),
            _playPause,
            new ToolStripMenuItem(L.Next, null, (_, _) => _window.Send("next")),
            new ToolStripMenuItem(L.Previous, null, (_, _) => _window.Send("prev")),
            _like,
            _sleep,
            new ToolStripSeparator(),
            new ToolStripMenuItem(L.ShowWindow, null, (_, _) => _window.ShowMain()),
            new ToolStripMenuItem(L.MiniPlayer, null, (_, _) => _window.ShowMini()) { ShortcutKeyDisplayString = L.MiniPlayerShortcut },
            new ToolStripMenuItem(L.Equalizer + " …", null, (_, _) => _window.ShowEqualizer()),
            new ToolStripSeparator(),
            closeToTray,
            autoStart,
            discord,
            upsells,
            updates,
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
        _icon.BalloonTipClicked += (_, _) => { if (_updateUrl != null) OpenUpdatePage(); };
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
        _trackTooltip = state.HasTrack ? $"{(state.Playing ? "▶" : "⏸")} {track}" : "SoundDrought";
        RefreshTooltip();
    }

    // ---- Sleep timer ----

    private void BuildSleepMenu()
    {
        _sleep.DropDownItems.Clear();
        var sleep = _window.Sleep;
        _sleep.DropDownItems.Add(new ToolStripMenuItem(L.SleepOff, null, (_, _) => { sleep.Cancel(); RefreshTooltip(); }) { Checked = !sleep.Active });
        foreach (var minutes in SleepChoices)
            _sleep.DropDownItems.Add(new ToolStripMenuItem(L.SleepMinutes(minutes), null, (_, _) => { sleep.Start(minutes); RefreshTooltip(); }) { Checked = sleep.Minutes == minutes });
        _sleep.DropDownItems.Add(new ToolStripMenuItem(L.SleepAfterTrack, null, (_, _) => { sleep.AfterThisTrack(); RefreshTooltip(); }) { Checked = sleep.AfterTrack });
        _sleep.Text = sleep.Status ?? L.SleepTimer;
    }

    public void RefreshTooltip()
    {
        var status = _window.Sleep.Status;
        var text = status is null ? _trackTooltip : $"{_trackTooltip}\n{status}";
        _icon.Text = Truncate(text, 127);
        _sleep.Text = status ?? L.SleepTimer;
    }

    // ---- Updates ----

    public void ShowUpdate(UpdateChecker.Release release)
    {
        _updateUrl = release.Url;
        _update.Text = L.UpdateAvailable(release.Tag);
        _update.Visible = _updateSeparator.Visible = true;

        // Pop up once per version, after that only the menu entry stays.
        if (_settings.NotifiedUpdate == release.Tag) return;
        _settings.NotifiedUpdate = release.Tag;
        _settings.Save();
        _icon.ShowBalloonTip(8000, L.UpdateBalloonTitle(release.Tag), L.UpdateBalloonText, ToolTipIcon.Info);
    }

    private void OpenUpdatePage()
    {
        if (_updateUrl != null)
            Process.Start(new ProcessStartInfo(_updateUrl) { UseShellExecute = true });
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
