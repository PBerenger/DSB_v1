using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

namespace DoubleSceensBlack;

internal static class MonitorDiscovery
{
    public static List<MonitorChoice> GetScreens()
    {
        List<MonitorChoice> monitors = [];
        int errorCode = 0;
        MonitorEnumerationCallback callback = (IntPtr handle, IntPtr deviceContext, ref NativeRectangle rectangle, IntPtr data) =>
        {
            MonitorInfo info = new() { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(handle, ref info))
            {
                errorCode = Marshal.GetLastWin32Error();
                return false;
            }

            monitors.Add(new MonitorChoice(
                info.DeviceName,
                info.DeviceName,
                Rectangle.FromLTRB(
                    info.Monitor.Left,
                    info.Monitor.Top,
                    info.Monitor.Right,
                    info.Monitor.Bottom)));
            return true;
        };

        if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero))
        {
            throw new Win32Exception(errorCode != 0 ? errorCode : Marshal.GetLastWin32Error());
        }

        return monitors;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public uint Size;
        public NativeRectangle Monitor;
        public NativeRectangle Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    private delegate bool MonitorEnumerationCallback(
        IntPtr monitor,
        IntPtr deviceContext,
        ref NativeRectangle monitorRectangle,
        IntPtr data);

    [DllImport("user32.dll", EntryPoint = "EnumDisplayMonitors", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        IntPtr deviceContext,
        IntPtr clipRectangle,
        MonitorEnumerationCallback callback,
        IntPtr data);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);
}
