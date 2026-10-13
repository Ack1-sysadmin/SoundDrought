using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace SoundDrought;

public sealed class Settings
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoundDrought");

    private static readonly string FilePath = Path.Combine(Dir, "settings.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public double? Left { get; set; }
    public double? Top { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }
    public bool Maximized { get; set; }
    public double Zoom { get; set; } = 1.0;
    public bool CloseToTray { get; set; } = true;
    public bool TrayHintShown { get; set; }
    public string? LastUrl { get; set; }
    public double? MiniLeft { get; set; }
    public double? MiniTop { get; set; }
    public bool DiscordStatus { get; set; } = true;

    public static string PathFor(string name) => Path.Combine(Dir, name);

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupt or unreadable settings: start fresh.
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not worth crashing over.
        }
    }

    public void ApplyTo(Window window)
    {
        if (Left is { } l && Top is { } t && Width is { } w && Height is { } h &&
            new Rect(l, t, w, h).IntersectsWith(new Rect(
                SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight)))
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = l;
            window.Top = t;
            window.Width = w;
            window.Height = h;
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        if (Maximized)
            window.WindowState = WindowState.Maximized;
    }

    public void CaptureFrom(Window window)
    {
        var bounds = window.WindowState == WindowState.Normal
            ? new Rect(window.Left, window.Top, window.Width, window.Height)
            : window.RestoreBounds;
        if (bounds.IsEmpty) return;

        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
        Maximized = window.WindowState == WindowState.Maximized;
    }
}
