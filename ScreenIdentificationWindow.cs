using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace DoubleSceensBlack;

internal sealed class ScreenIdentificationWindow : Window
{
    private const int ExtendedWindowStyle = -20;
    private const int ToolWindowStyle = 0x00000080;
    private const int NoActivateStyle = 0x08000000;
    private const uint SetWindowPositionFlags = 0x0004 | 0x0010 | 0x0200;
    private static readonly TimeSpan DisplayDuration = TimeSpan.FromSeconds(2);

    private readonly System.Drawing.Rectangle _monitorBounds;
    private readonly DispatcherTimer _displayTimer;

    public ScreenIdentificationWindow(MonitorChoice monitor, int screenNumber)
    {
        _monitorBounds = monitor.Bounds;
        WindowStyle = WindowStyle.None;
        WindowStartupLocation = WindowStartupLocation.Manual;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        IsHitTestVisible = false;
        Left = 0;
        Top = 0;
        Width = monitor.Bounds.Width;
        Height = monitor.Bounds.Height;
        Title = $"Identification de l’écran {screenNumber}";

        Content = new System.Windows.Controls.Grid
        {
            Background = System.Windows.Media.Brushes.Transparent,
            Children =
            {
                new System.Windows.Controls.Border
                {
                    Width = 220,
                    Height = 220,
                    CornerRadius = new CornerRadius(110),
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(238, 35, 67, 110)),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Child = new System.Windows.Controls.StackPanel
                    {
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = System.Windows.VerticalAlignment.Center,
                        Children =
                        {
                            new System.Windows.Controls.TextBlock
                            {
                                Text = screenNumber.ToString(),
                                Foreground = System.Windows.Media.Brushes.White,
                                FontSize = 108,
                                FontWeight = System.Windows.FontWeights.Bold,
                                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                                TextAlignment = System.Windows.TextAlignment.Center
                            },
                            new System.Windows.Controls.TextBlock
                            {
                                Text = "ÉCRAN",
                                Foreground = System.Windows.Media.Brushes.White,
                                FontSize = 18,
                                FontWeight = System.Windows.FontWeights.SemiBold,
                                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                                TextAlignment = System.Windows.TextAlignment.Center
                            }
                        }
                    }
                }
            }
        };

        _displayTimer = new DispatcherTimer { Interval = DisplayDuration };
        _displayTimer.Tick += (_, _) =>
        {
            _displayTimer.Stop();
            Close();
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        IntPtr handle = new WindowInteropHelper(this).Handle;
        IntPtr style = GetWindowLongPtr(handle, ExtendedWindowStyle);
        SetWindowLongPtr(
            handle,
            ExtendedWindowStyle,
            new IntPtr(style.ToInt64() | ToolWindowStyle | NoActivateStyle));
        PositionOverMonitor(handle);
        SizeForMonitorDpi(handle);
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        IntPtr handle = new WindowInteropHelper(this).Handle;
        SizeForMonitorDpi(handle);
        PositionOverMonitor(handle);
        _displayTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _displayTimer.Stop();
        base.OnClosed(e);
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
