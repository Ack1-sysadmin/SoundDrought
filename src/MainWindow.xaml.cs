using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

namespace SoundDrought;

public partial class MainWindow : Window
{
    private const string HomeUrl = "https://soundcloud.com/discover";

    // Sign-in providers that SoundCloud opens as popups; they must stay popups so the OAuth handshake works.
    private static readonly string[] AuthHosts =
        ["accounts.google.com", "appleid.apple.com", "www.facebook.com", "facebook.com", "m.facebook.com"];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Settings _settings = Settings.Load();
    private readonly TrayIcon _tray;
    private readonly DiscordPresence _discord = new();
    private readonly bool _startHidden;
    private MiniPlayer? _mini;
    private bool _miniMode;
    private PlayerState _state = new();

    public MainWindow(bool startHidden)
    {
        InitializeComponent();
        _startHidden = startHidden;
        if (startHidden)
        {
            // Autostart: the window must be shown once for WebView2 to initialize, so show it off-screen.
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = Top = -32000;
            ShowInTaskbar = false;
            ShowActivated = false;
        }
        else
        {
            _settings.ApplyTo(this);
        }

        ThumbPrev.ImageSource = Glyphs.Render(Glyphs.Previous);
        ThumbNext.ImageSource = Glyphs.Render(Glyphs.Next);
        ThumbPlay.ImageSource = Glyphs.Render(Glyphs.Play);

        _tray = new TrayIcon(this, _settings);
        _tray.SettingsChanged += () => _discord.Update(_state, _settings.DiscordStatus);

        SourceInitialized += (_, _) => Native.UseDarkTitleBar(new WindowInteropHelper(this).Handle);
        Loaded += async (_, _) => await InitWebViewAsync();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.M && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ShowMini();
                e.Handled = true;
            }
        };
    }

    public bool IsExiting { get; private set; }

    private async Task InitWebViewAsync()
    {
        // Keep the player responsive while the window is hidden in the tray or mini mode.
        var options = new CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments = "--disable-background-timer-throttling --disable-renderer-backgrounding",
        };
        var env = await CoreWebView2Environment.CreateAsync(null, Settings.PathFor("WebView2"), options);
        await Web.EnsureCoreWebView2Async(env);

        var core = Web.CoreWebView2;
        core.Settings.IsStatusBarEnabled = false;
#if !DEBUG
        core.Settings.AreDevToolsEnabled = false;
#endif
        await core.AddScriptToExecuteOnDocumentCreatedAsync(LoadResource("inject.js"));
        core.WebMessageReceived += OnWebMessage;
        core.NewWindowRequested += OnNewWindowRequested;

        Web.ZoomFactor = _settings.Zoom;
        Web.ZoomFactorChanged += (_, _) => _settings.Zoom = Web.ZoomFactor;

        core.Navigate(IsSoundCloud(_settings.LastUrl) ? _settings.LastUrl! : HomeUrl);

        if (_startHidden)
        {
            Hide();
            _settings.ApplyTo(this);
            ShowInTaskbar = true;
            ShowActivated = true;
        }
    }

    public void Send(string command) => Web.CoreWebView2?.PostWebMessageAsString(command);

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var state = JsonSerializer.Deserialize<PlayerState>(e.WebMessageAsJson, JsonOptions);
        if (state is null) return;
        _state = state;

        Title = state.HasTrack ? $"{state.Title} – {state.Artist}" : "SoundDrought";
        ThumbPlay.ImageSource = Glyphs.Render(state.Playing ? Glyphs.Pause : Glyphs.Play);
        ThumbPlay.Description = state.Playing ? L.Pause : L.Play;
        _tray.Update(state);
        _mini?.Update(state);
        _discord.Update(state, _settings.DiscordStatus);
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri)) return;

        // Login popups (window.open with a size) keep WebView2's default popup window.
        if (Array.IndexOf(AuthHosts, uri.Host) >= 0) return;
        if (IsSoundCloud(e.Uri) && e.WindowFeatures.HasSize) return;

        e.Handled = true;
        if (IsSoundCloud(e.Uri))
            Web.CoreWebView2.Navigate(e.Uri);
        else
            OpenExternal(e.Uri);
    }

    private static bool IsSoundCloud(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        (uri.Host == "soundcloud.com" || uri.Host.EndsWith(".soundcloud.com", StringComparison.OrdinalIgnoreCase));

    private static void OpenExternal(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    private static string LoadResource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Brings back whichever view (full window or mini player) the user was in.</summary>
    public void Restore()
    {
        if (_miniMode) ShowMini();
        else ShowMain();
    }

    public void ShowMain()
    {
        _miniMode = false;
        _mini?.Hide();
        BringToFront();
    }

    public void ShowMini()
    {
        _miniMode = true;
        if (_mini is null)
        {
            _mini = new MiniPlayer(this, _settings);
            _mini.Update(_state);
        }
        Hide();
        _mini.Show();
        _mini.Activate();
    }

    private void BringToFront()
    {
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    public void ToggleVisibility()
    {
        if (_miniMode && _mini!.IsVisible)
            _mini.Hide();
        else if (!_miniMode && IsVisible && WindowState != WindowState.Minimized && IsActive)
            Hide();
        else
            Restore();
    }

    public void Exit()
    {
        IsExiting = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _settings.CaptureFrom(this);
        if (IsSoundCloud(Web.CoreWebView2?.Source))
            _settings.LastUrl = Web.CoreWebView2!.Source;
        _settings.Save();

        if (!IsExiting && _settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            _tray.ShowHintOnce();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        IsExiting = true;
        _mini?.Close();
        _tray.Dispose();
        _discord.Dispose();
        Web.Dispose();
        base.OnClosed(e);
        Application.Current.Shutdown();
    }

    private void ThumbPrev_Click(object? sender, EventArgs e) => Send("prev");
    private void ThumbPlay_Click(object? sender, EventArgs e) => Send("toggle");
    private void ThumbNext_Click(object? sender, EventArgs e) => Send("next");
}

public sealed class PlayerState
{
    public bool HasTrack { get; set; }
    public bool Playing { get; set; }
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public bool Liked { get; set; }
    public string Artwork { get; set; } = "";
    public double Position { get; set; }
    public double Duration { get; set; }
    public string Url { get; set; } = "";
    public string ArtistUrl { get; set; } = "";
}
