using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shell;

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
    private static readonly string PipeName = InstanceName + ".Args." + Environment.UserName;

    private Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, MutexName, out bool isFirst);
        if (!isFirst)
        {
            // Another instance is running: hand it our arguments (jump list entries) and quit.
            SendToRunningInstance(e.Args);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        var window = new MainWindow(Array.IndexOf(e.Args, AutoStart.MinimizedArg) >= 0);
        MainWindow = window;
        window.Show();
        if (Array.Exists(e.Args, a => a.StartsWith("--nav=", StringComparison.Ordinal) || a == "--mini"))
            window.HandleArgs(e.Args);

        SetJumpList();
        _ = ListenForOtherInstancesAsync(window);
    }

    private static void SendToRunningInstance(string[] args)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            pipe.Connect(2000);
            using var writer = new StreamWriter(pipe);
            writer.Write(string.Join('\n', args));
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
        }
    }

    private async Task ListenForOtherInstancesAsync(MainWindow window)
    {
        while (true)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync();
                using var reader = new StreamReader(pipe);
                var text = await reader.ReadToEndAsync();
                var args = text.Length == 0 ? [] : text.Split('\n');
                window.HandleArgs(args); // continuation runs on the UI thread
            }
            catch (IOException)
            {
                await Task.Delay(500);
            }
        }
    }

    /// <summary>Right-click menu of the taskbar button.</summary>
    private static void SetJumpList()
    {
        var exe = Environment.ProcessPath ?? "";
        JumpTask Task(string title, string args) => new()
        {
            Title = title,
            Arguments = args,
            ApplicationPath = exe,
            IconResourcePath = exe,
            IconResourceIndex = 0,
        };

        var list = new JumpList();
        list.JumpItems.Add(Task(L.Likes, "--nav=/you/likes"));
        list.JumpItems.Add(Task(L.Feed, "--nav=/feed"));
        list.JumpItems.Add(Task(L.History, "--nav=/you/history"));
        list.JumpItems.Add(Task(L.MiniPlayer, "--mini"));
        list.ShowRecentCategory = false;
        list.ShowFrequentCategory = false;
        JumpList.SetJumpList(Current, list);
    }

    /// <summary>
    /// Windows is shutting down or signing out: agree and quit right away instead of waiting for the
    /// final signal. The embedded WebView2 lives in other processes that shut down at the same time and
    /// can stall our window long enough for Windows to list the app as "preventing shutdown".
    /// </summary>
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        base.OnSessionEnding(e);
        if (!e.Cancel && MainWindow is MainWindow window)
            Dispatcher.BeginInvoke(window.Exit); // after answering Windows, so the answer is instant
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
