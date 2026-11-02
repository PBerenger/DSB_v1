using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

namespace DoubleSceensBlack;

internal static class WindowMover
{
    private const int ShowWindowRestore = 9;
    private const int ShowWindowMaximize = 3;
    private const uint SetWindowPositionNoZOrder = 0x0004;

    public static bool TryMove(
        IntPtr window,
        Rectangle monitorBounds,
        bool maximize,
        out string errorMessage)
    {
        if (!IsWindow(window) || !IsWindowVisible(window))
        {
            errorMessage = "Cette fenêtre n'est plus disponible. Actualisez la liste et réessayez.";
            return false;
        }

        if (monitorBounds.Width <= 0 || monitorBounds.Height <= 0)
        {
            errorMessage = "Les dimensions de l'écran de destination sont invalides.";
            return false;
        }

        ShowWindow(window, ShowWindowRestore);
        if (!GetWindowRect(window, out NativeRectangle windowBounds))
        {
            errorMessage = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            return false;
        }

        int width = Math.Clamp(windowBounds.Right - windowBounds.Left, 1, monitorBounds.Width);
        int height = Math.Clamp(windowBounds.Bottom - windowBounds.Top, 1, monitorBounds.Height);
        int left = monitorBounds.Left + Math.Max(0, (monitorBounds.Width - width) / 2);
        int top = monitorBounds.Top + Math.Max(0, (monitorBounds.Height - height) / 2);

        if (!SetWindowPos(
            window,
            IntPtr.Zero,
            left,
            top,
            width,
            height,
            SetWindowPositionNoZOrder))
        {
            errorMessage = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            return false;
        }

        if (maximize)
        {
            ShowWindow(window, ShowWindowMaximize);
        }

        errorMessage = string.Empty;
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRectangle rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);
}
