using System;
using System.Runtime.InteropServices;

namespace SoundDrought;

internal static partial class Native
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void UseDarkTitleBar(IntPtr hwnd)
    {
        int enabled = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int));
    }

    private const int SM_CXSIZEFRAME = 32;
    private const int SM_CXPADDEDBORDER = 92;

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetricsForDpi(int index, uint dpi);

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(IntPtr hwnd);

    /// <summary>Thickness of the sizing frame in physical pixels (how far a maximized window overhangs the screen).</summary>
    public static int FrameThicknessPixels(IntPtr hwnd)
    {
        uint dpi = GetDpiForWindow(hwnd);
        if (dpi == 0) dpi = 96;
        return GetSystemMetricsForDpi(SM_CXSIZEFRAME, dpi) + GetSystemMetricsForDpi(SM_CXPADDEDBORDER, dpi);
    }

    private const int APPMODEL_ERROR_NO_PACKAGE = 15700;

    [LibraryImport("kernel32.dll")]
    private static partial int GetCurrentPackageFullName(ref uint length, IntPtr name);

    /// <summary>True when running as an MSIX package (Microsoft Store build).</summary>
    public static bool IsPackaged { get; } = DetectPackage();

    private static bool DetectPackage()
    {
        uint length = 0;
        return GetCurrentPackageFullName(ref length, IntPtr.Zero) != APPMODEL_ERROR_NO_PACKAGE;
    }

}
