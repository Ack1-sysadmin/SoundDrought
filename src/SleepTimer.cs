using System;
using System.Windows.Threading;

namespace SoundDrought;

/// <summary>Pauses playback after a number of minutes or when the current track ends.</summary>
public sealed class SleepTimer
{
    private readonly Action _pause;
    private readonly Action _changed;
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(15) };
    private DateTime _due;
    private string? _trackUrl;

    public SleepTimer(Action pause, Action changed)
    {
        _pause = pause;
        _changed = changed;
        _tick.Tick += (_, _) =>
        {
            if (DateTime.Now >= _due) Fire();
            else _changed();
        };
    }

    public int? Minutes { get; private set; }
    public bool AfterTrack { get; private set; }
    public bool Active => Minutes != null || AfterTrack;

    /// <summary>Text for the tray (menu title and tooltip), or null when off.</summary>
    public string? Status =>
        Minutes != null ? L.SleepRemaining(Math.Max(1, (int)Math.Ceiling((_due - DateTime.Now).TotalMinutes)))
        : AfterTrack ? L.SleepAfterTrackActive
        : null;

    public void Start(int minutes)
    {
        Cancel();
        Minutes = minutes;
        _due = DateTime.Now.AddMinutes(minutes);
        _tick.Start();
    }

    /// <summary>Pause as soon as the player moves on from the track that is playing now.</summary>
    public void AfterThisTrack(string? currentTrackUrl = null)
    {
        Cancel();
        AfterTrack = true;
        _trackUrl = currentTrackUrl;
    }

    public void Cancel()
    {
        _tick.Stop();
        Minutes = null;
        AfterTrack = false;
        _trackUrl = null;
    }

    /// <summary>Called with every player state update.</summary>
    public void Observe(PlayerState state)
    {
        if (!AfterTrack || !state.HasTrack) return;
        _trackUrl ??= state.Url;
        if (state.Url != _trackUrl) Fire();
    }

    private void Fire()
    {
        Cancel();
        _pause();
        _changed();
    }
}
