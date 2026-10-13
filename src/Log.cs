using System;
using System.IO;

namespace SoundDrought;

/// <summary>Tiny diagnostic log at %LOCALAPPDATA%\SoundDrought\app.log (reset when it grows past 512 KB).</summary>
public static class Log
{
    private static readonly string FilePath = Settings.PathFor("app.log");
    private static readonly object Gate = new();

    public static void Write(string message)
    {
        lock (Gate)
        {
            try
            {
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > 512 * 1024) file.Delete();
                Directory.CreateDirectory(file.DirectoryName!);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
