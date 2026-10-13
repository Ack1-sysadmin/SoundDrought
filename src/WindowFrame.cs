using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace SoundDrought;

/// <summary>
/// Removes the native title bar but keeps the rest of the window frame (the approach Windows Terminal uses).
/// Left, right and bottom stay real non-client borders, which Windows 10/11 draws invisibly outside the
/// window, so the WebView2 child window can never cover them. The top resize edge and the caption area
/// (empty parts of the navigation bar) are handled in WM_NCHITTEST.
/// </summary>
internal static partial class WindowFrame
{
    private const int WM_NCCALCSIZE = 0x0083;
    private const int WM_NCHITTEST = 0x0084;
    private const int HTCLIENT = 1, HTCAPTION = 2, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14;
    private const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_FRAMECHANGED = 0x20;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    private static partial IntPtr DefWindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsZoomed(IntPtr hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    /// <param name="captionHeight">Height of the custom title bar in WPF units.</param>
    public static void Attach(Window window, double captionHeight)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        HwndSource.FromHwnd(hwnd).AddHook((IntPtr h, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            msg switch
            {
                WM_NCCALCSIZE when wParam != IntPtr.Zero => CalcSize(h, lParam, ref handled),
                WM_NCHITTEST => HitTest(window, h, wParam, lParam, captionHeight, ref handled),
                _ => IntPtr.Zero,
            });

        // Apply the new frame right away.
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
    }

    private static IntPtr CalcSize(IntPtr hwnd, IntPtr lParam, ref bool handled)
    {
        // Let Windows compute the standard frame, then give the title bar area back to the client.
        int top = Marshal.PtrToStructure<RECT>(lParam).Top;
        DefWindowProc(hwnd, WM_NCCALCSIZE, (IntPtr)1, lParam);

        var client = Marshal.PtrToStructure<RECT>(lParam);
        // A maximized window overhangs the screen by its frame; keep the content on screen.
        client.Top = top + (IsZoomed(hwnd) ? Native.FrameThicknessPixels(hwnd) : 0);
        Marshal.StructureToPtr(client, lParam, false);

        handled = true;
        return IntPtr.Zero;
    }

    private static IntPtr HitTest(Window window, IntPtr hwnd, IntPtr wParam, IntPtr lParam, double captionHeight, ref bool handled)
    {
        // Side and bottom borders are real frame: Windows knows them.
        var hit = DefWindowProc(hwnd, WM_NCHITTEST, wParam, lParam);
        if (hit != (IntPtr)HTCLIENT) return IntPtr.Zero;

        int x = (short)((long)lParam & 0xFFFF), y = (short)(((long)lParam >> 16) & 0xFFFF);
        var point = window.PointFromScreen(new Point(x, y));
        const double resize = 5;

        handled = true;
        if (window.WindowState == WindowState.Normal && point.Y < resize)
        {
            if (point.X < resize * 2) return HTTOPLEFT;
            if (point.X > window.ActualWidth - resize * 2) return HTTOPRIGHT;
            return HTTOP;
        }
        if (point.Y < captionHeight && !IsInteractive(window, point)) return HTCAPTION;

        handled = false;
        return IntPtr.Zero;
    }

    /// <summary>True when the point is over a control marked IsHitTestVisibleInChrome (buttons, search box).</summary>
    private static bool IsInteractive(Window window, Point point)
    {
        for (var e = window.InputHitTest(point) as DependencyObject; e != null && e != window;
             e = e is Visual ? VisualTreeHelper.GetParent(e) : LogicalTreeHelper.GetParent(e))
        {
            if (e is IInputElement input && WindowChrome.GetIsHitTestVisibleInChrome(input)) return true;
        }
        return false;
    }
}
