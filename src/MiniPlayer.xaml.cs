using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
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

        ApplyVariant();
        // The size comes from the layout (SizeToContent), so place the window once it is measured.
        Loaded += (_, _) =>
        {
            var area = SystemParameters.WorkArea;
            Left = settings.MiniLeft ?? area.Right - ActualWidth - 16;
            Top = settings.MiniTop ?? area.Bottom - ActualHeight - 16;
            KeepOnScreen();
        };
        SizeChanged += (_, _) => { if (IsLoaded) KeepOnScreen(); };

        MouseRightButtonUp += (_, e) => { ShowVariantMenu(); e.Handled = true; };

        // Clip the content to the rounded frame.
        Frame.SizeChanged += (_, _) =>
            Frame.Clip = new System.Windows.Media.RectangleGeometry(new Rect(0, 0, Frame.ActualWidth, Frame.ActualHeight), 8, 8);
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
        CoverProgressScale.ScaleX = ProgressScale.ScaleX;
        CoverTitle.Text = TrackTitle.Text;
        CoverArtist.Text = state.Artist;
        CoverPlay.Content = PlayButton.Content;
        CoverPlay.ToolTip = PlayButton.ToolTip;
        CoverLike.Content = LikeButton.Content;
        if (state.Liked) CoverLike.Foreground = Glyphs.Accent;
        else CoverLike.Foreground = System.Windows.Media.Brushes.WhiteSmoke;
        Time.Text = state.Duration > 0 ? $"{FormatTime(state.Position)} / {FormatTime(state.Duration)}" : "";

        if (state.Artwork != _artworkUrl)
        {
            _artworkUrl = state.Artwork;
            Artwork.Source = Uri.TryCreate(state.Artwork, UriKind.Absolute, out var uri)
                ? new BitmapImage(uri)
                : null;
            CoverArt.Source = Artwork.Source;
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
        var bar = (FrameworkElement)sender;
        var fraction = e.GetPosition(bar).X / bar.ActualWidth;
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

    // ---- Variants: layout, size, opacity, always on top ----

    private static readonly (double Scale, Func<string> Label)[] Sizes =
        [(0.85, () => L.MiniSizeSmall), (1.0, () => L.MiniSizeMedium), (1.25, () => L.MiniSizeLarge)];
    private static readonly double[] Opacities = [1.0, 0.9, 0.75, 0.6];

    private void ApplyVariant()
    {
        CompactLayout.Visibility = _settings.MiniCover ? Visibility.Collapsed : Visibility.Visible;
        CoverLayout.Visibility = _settings.MiniCover ? Visibility.Visible : Visibility.Collapsed;
        Scale.ScaleX = Scale.ScaleY = _settings.MiniScale;
        Topmost = _settings.MiniTopmost;
        Opacity = _settings.MiniOpacity;
    }

    private void ChangeVariant(Action<Settings> change)
    {
        change(_settings);
        _settings.Save();
        ApplyVariant();
    }

    private void ShowVariantMenu()
    {
        var menu = new ContextMenu
        {
            Style = (Style)Application.Current.Resources["DarkMenu"],
            PlacementTarget = this,
            Placement = PlacementMode.MousePoint,
        };
        MenuItem Item(string text, bool check, Action action)
        {
            var item = new MenuItem { Header = (check ? "✓  " : "     ") + text, Style = (Style)Application.Current.Resources["DarkMenuItem"] };
            item.Click += (_, _) => action();
            return item;
        }
        Separator Line() => new() { Style = (Style)Application.Current.Resources["DarkSeparator"] };

        menu.Items.Add(Item(L.MiniLayoutCompact, !_settings.MiniCover, () => ChangeVariant(s => s.MiniCover = false)));
        menu.Items.Add(Item(L.MiniLayoutCover, _settings.MiniCover, () => ChangeVariant(s => s.MiniCover = true)));
        menu.Items.Add(Line());
        foreach (var (scale, label) in Sizes)
            menu.Items.Add(Item(label(), Math.Abs(_settings.MiniScale - scale) < 0.01, () => ChangeVariant(s => s.MiniScale = scale)));
        menu.Items.Add(Line());
        foreach (var opacity in Opacities)
            menu.Items.Add(Item(L.MiniOpacity((int)(opacity * 100)), Math.Abs(_settings.MiniOpacity - opacity) < 0.01, () => ChangeVariant(s => s.MiniOpacity = opacity)));
        menu.Items.Add(Line());
        menu.Items.Add(Item(L.AlwaysOnTop, _settings.MiniTopmost, () => ChangeVariant(s => s.MiniTopmost = !s.MiniTopmost)));
        menu.Items.Add(Line());
        menu.Items.Add(Item(L.FullWindow, false, _main.ShowMain));
        menu.IsOpen = true;
    }

    private void OnCoverHover(object sender, MouseEventArgs e)
    {
        var show = CoverLayout.IsMouseOver;
        CoverOverlay.BeginAnimation(OpacityProperty, new DoubleAnimation(show ? 1 : 0, TimeSpan.FromMilliseconds(150)));
    }

    private void KeepOnScreen()
    {
        var area = SystemParameters.WorkArea;
        Left = Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - ActualWidth));
        Top = Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - ActualHeight));
    }

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
