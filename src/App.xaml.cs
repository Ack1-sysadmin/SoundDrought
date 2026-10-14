using System;
using System.Threading;
using System.Windows;

namespace SoundDrought;

public partial class App : Application
{
    // Debug builds get their own instance and data folder, so they can run next to the installed app.
#if DEBUG
    public const string InstanceName = "SoundDrought.Debug";
#else
    public const string InstanceName = "SoundDrought";
#endif
    private const string MutexName = InstanceName + ".SingleInstance";
    private const string ShowEventName = InstanceName + ".Show";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, MutexName, out bool isFirst);
        if (!isFirst)
        {
            // Another instance is running: ask it to come to the front and quit.
            if (EventWaitHandle.TryOpenExisting(ShowEventName, out var existing))
                existing.Set();
            Shutdown();
            return;
        }

        base.OnStartup(e);

        var window = new MainWindow(Array.IndexOf(e.Args, AutoStart.MinimizedArg) >= 0);
        MainWindow = window;
        window.Show();

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var listener = new Thread(() =>
        {
            while (_showEvent.WaitOne())
                Dispatcher.BeginInvoke(window.Restore);
        }) { IsBackground = true };
        listener.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
