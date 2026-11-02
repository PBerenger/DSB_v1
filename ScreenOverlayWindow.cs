using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DoubleSceensBlack;

internal sealed class ScreenOverlayWindow : Window
{
    private const int ExtendedWindowStyle = -20;
    private const int TransparentStyle = 0x00000020;
    private const int ToolWindowStyle = 0x00000080;
    private const int NoActivateStyle = 0x08000000;
    private const uint SetWindowPositionFlags = 0x0004 | 0x0010 | 0x0200;

    private readonly SolidColorBrush _overlayBrush;
    private readonly System.Drawing.Rectangle _monitorBounds;

    public ScreenOverlayWindow(MonitorChoice monitor, int intensity)
    {
        DeviceName = monitor.DeviceName;
        _monitorBounds = monitor.Bounds;
        _overlayBrush = CreateBackground(intensity);
        WindowStyle = WindowStyle.None;
        WindowStartupLocation = WindowStartupLocation.Manual;
        AllowsTransparency = true;
        Background = _overlayBrush;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        IsHitTestVisible = false;
        Left = 0;
        Top = 0;
        Width = monitor.Bounds.Width;
        Height = monitor.Bounds.Height;
    }

    public string DeviceName { get; }

    public void SetIntensity(int intensity)
    {
        _overlayBrush.Color = GetBackgroundColor(intensity);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        IntPtr handle = new WindowInteropHelper(this).Handle;
        IntPtr style = GetWindowLongPtr(handle, ExtendedWindowStyle);
        SetWindowLongPtr(
            handle,
            ExtendedWindowStyle,
            new IntPtr(style.ToInt64() | TransparentStyle | ToolWindowStyle | NoActivateStyle));
        PositionOverMonitor(handle);
        SizeForMonitorDpi(handle);
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        IntPtr handle = new WindowInteropHelper(this).Handle;
        SizeForMonitorDpi(handle);
        PositionOverMonitor(handle);
    }

    private void SizeForMonitorDpi(IntPtr handle)
    {
        uint dpi = GetDpiForWindow(handle);
        if (dpi == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        Width = _monitorBounds.Width * 96d / dpi;
        Height = _monitorBounds.Height * 96d / dpi;
    }

    private void PositionOverMonitor(IntPtr handle)
    {
        if (!SetWindowPos(
            handle,
            IntPtr.Zero,
            _monitorBounds.Left,
            _monitorBounds.Top,
            _monitorBounds.Width,
            _monitorBounds.Height,
            SetWindowPositionFlags))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private static SolidColorBrush CreateBackground(int intensity) =>
        new(GetBackgroundColor(intensity));

    private static System.Windows.Media.Color GetBackgroundColor(int intensity) =>
        System.Windows.Media.Color.FromArgb(
            (byte)(Math.Clamp(intensity, 0, 100) * 255 / 100), 0, 0, 0);

    private static IntPtr GetWindowLongPtr(IntPtr handle, int index) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(handle, index) : new IntPtr(GetWindowLong32(handle, index));

    private static IntPtr SetWindowLongPtr(IntPtr handle, int index, IntPtr value) =>
        IntPtr.Size == 8
            ? SetWindowLongPtr64(handle, index, value)
            : new IntPtr(SetWindowLong32(handle, index, value.ToInt32()));

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetDpiForWindow(IntPtr handle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr handle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr handle, int index, int value);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr handle, int index, IntPtr value);
}
