using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace SoundDrought;

public partial class MiniPlayer : Window
{
    private readonly MainWindow _main;
    private readonly Settings _settings;
    private string _artworkUrl = "";
    private DateTime _lastVolumeSend;
    private double _volume;

    public MiniPlayer(MainWindow main, Settings settings)
    {
        InitializeComponent();
        _main = main;
        _settings = settings;

        var area = SystemParameters.WorkArea;
        Left = settings.MiniLeft ?? area.Right - Width - 16;
        Top = settings.MiniTop ?? area.Bottom - Height - 16;
        if (!area.IntersectsWith(new Rect(Left, Top, Width, Height)))
        {
            Left = area.Right - Width - 16;
            Top = area.Bottom - Height - 16;
        }

        SourceInitialized += (_, _) => Native.UseRoundedCorners(new WindowInteropHelper(this).Handle);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.M && Keyboard.Modifiers == ModifierKeys.Control) _main.ShowMain();
            else if (e.Key == Key.Space) _main.Send("toggle");
        };
        PreviewMouseWheel += (_, e) =>
        {
            double step = e.Delta > 0 ? 0.05 : -0.05;
            _main.Send("volumeBy:" + step.ToString(CultureInfo.InvariantCulture));
            ShowVolume(Math.Clamp(Math.Round((_volume + step) * 20) / 20, 0, 1)); // instant feedback; the page confirms
            e.Handled = true;
        };
    }

    public void Update(PlayerState state)
    {
        TrackTitle.Text = state.HasTrack ? state.Title : L.NothingPlaying;
        TrackArtist.Text = state.Artist;
        PlayButton.Content = state.Playing ? Glyphs.Pause : Glyphs.Play;
        PlayButton.ToolTip = state.Playing ? L.Pause : L.Play;
        LikeButton.Content = state.Liked ? Glyphs.HeartFilled : Glyphs.Heart;
        if (state.Liked) LikeButton.Foreground = Glyphs.Accent;
        else LikeButton.ClearValue(ForegroundProperty);

        if (!VolumeSlider.IsMouseCaptureWithin) ShowVolume(state.Muted ? 0 : state.Volume);
        MuteButton.ToolTip = state.Muted ? L.Unmute : L.Mute;
        System.Windows.Automation.AutomationProperties.SetName(MuteButton, (string)MuteButton.ToolTip);

        ProgressScale.ScaleX = state.Duration > 0 ? Math.Clamp(state.Position / state.Duration, 0, 1) : 0;
        Time.Text = state.Duration > 0 ? $"{FormatTime(state.Position)} / {FormatTime(state.Duration)}" : "";

        if (state.Artwork != _artworkUrl)
        {
            _artworkUrl = state.Artwork;
            Artwork.Source = Uri.TryCreate(state.Artwork, UriKind.Absolute, out var uri)
                ? new BitmapImage(uri)
                : null;
        }
    }

    /// <summary>Shows a volume without sending it back to the page.</summary>
    private void ShowVolume(double volume)
    {
        _volume = volume;
        _updatingVolume = true;
        VolumeSlider.Value = volume;
        _updatingVolume = false;
        UpdateVolumeIcon(volume);
    }

    private void UpdateVolumeIcon(double volume)
    {
        VolumeSlider.ToolTip = $"{L.Volume}: {Math.Round(volume * 100)} %";
        MuteButton.Content = volume <= 0 ? "\uE74F"   // muted
            : volume < 0.34 ? "\uE993"                 // one wave
            : volume < 0.67 ? "\uE994"                 // two waves
            : "\uE995";                                // three waves
    }

    // ---- Volume slider ----

    private bool _updatingVolume;

    private void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingVolume) return;
        _volume = e.NewValue;
        UpdateVolumeIcon(e.NewValue);
        // While dragging, update the page at most ~20 times per second; the drag end sends the final value.
        if (VolumeSlider.IsMouseCaptureWithin && DateTime.UtcNow - _lastVolumeSend < TimeSpan.FromMilliseconds(50)) return;
        SendVolume(e.NewValue);
    }

    private void OnVolumeDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e) =>
        SendVolume(VolumeSlider.Value);

    private void SendVolume(double volume)
    {
        _lastVolumeSend = DateTime.UtcNow;
        _main.Send("volume:" + volume.ToString("0.###", CultureInfo.InvariantCulture));
    }
    private static string FormatTime(double seconds)
    {
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1
            ? t.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : t.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void OnSeek(object sender, MouseButtonEventArgs e)
    {
        var fraction = e.GetPosition(Timeline).X / Timeline.ActualWidth;
        _main.Send("seek:" + fraction.ToString("0.###", CultureInfo.InvariantCulture));
        e.Handled = true; // don't start a window drag
    }

    private void OnPrev(object sender, RoutedEventArgs e) => _main.Send("prev");
    private void OnPlay(object sender, RoutedEventArgs e) => _main.Send("toggle");
    private void OnNext(object sender, RoutedEventArgs e) => _main.Send("next");
    private void OnLike(object sender, RoutedEventArgs e) => _main.Send("like");
    private void OnMute(object sender, RoutedEventArgs e) => _main.Send("mute");
    private void OnExpand(object sender, RoutedEventArgs e) => _main.ShowMain();
    private void OnHide(object sender, RoutedEventArgs e) => Hide();

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        _settings.MiniLeft = Left;
        _settings.MiniTop = Top;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Alt+F4 hides to the tray like the close button; the app closes it for real on exit.
        if (!_main.IsExiting)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }
}
