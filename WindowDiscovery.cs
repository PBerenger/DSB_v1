using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace DoubleSceensBlack;

internal sealed class OpenWindowChoice
{
    public IntPtr Handle { get; }
    public string DisplayName { get; }

    public OpenWindowChoice(IntPtr handle, string displayName)
    {
        Handle = handle;
        DisplayName = displayName;
    }
}

internal static class WindowDiscovery
{
    private const int GetWindowOwner = 4;
    private const int ExtendedWindowStyleIndex = -20;
    private const int DwmWindowAttributeCloaked = 14;
    private const long ExtendedAppWindowStyle = 0x00040000;
    private const long ExtendedToolWindowStyle = 0x00000080;

    public static List<OpenWindowChoice> GetOpenWindows(IntPtr excludedWindow)
    {
        List<OpenWindowChoice> windows = [];
        uint currentProcessId = (uint)Environment.ProcessId;
        Exception? enumerationError = null;
        EnumWindowsCallback callback = (IntPtr handle, IntPtr parameter) =>
        {
            if (handle == excludedWindow
                || !IsWindowVisible(handle)
                || !HasTaskbarButton(handle))
            {
                return true;
            }

            int dwmResult = DwmGetWindowAttribute(
                handle,
                DwmWindowAttributeCloaked,
                out int cloaked,
                sizeof(int));
            if (dwmResult < 0)
            {
                enumerationError = Marshal.GetExceptionForHR(dwmResult)
                    ?? new Win32Exception(dwmResult);
                return false;
            }

            if (cloaked != 0)
            {
                return true;
            }

            GetWindowThreadProcessId(handle, out uint processId);
            if (processId == currentProcessId)
            {
                return true;
            }

            int titleLength = GetWindowTextLength(handle);
            if (titleLength <= 0)
            {
                return true;
            }

            StringBuilder title = new(titleLength + 1);
            if (GetWindowText(handle, title, title.Capacity) == 0)
            {
                return true;
            }

            windows.Add(new OpenWindowChoice(handle, title.ToString().Trim()));
            return true;
        };

        if (!EnumWindows(callback, IntPtr.Zero))
        {
            if (enumerationError is not null)
            {
                throw enumerationError;
            }

            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return windows
            .OrderBy(window => window.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static bool HasTaskbarButton(IntPtr handle)
    {
        long extendedStyle = GetExtendedWindowStyle(handle).ToInt64();
        bool isApplicationWindow = (extendedStyle & ExtendedAppWindowStyle) != 0;
        bool isToolWindow = (extendedStyle & ExtendedToolWindowStyle) != 0;
        bool hasOwner = GetWindow(handle, GetWindowOwner) != IntPtr.Zero;

        return isApplicationWindow || (!isToolWindow && !hasOwner);
    }

    private static IntPtr GetExtendedWindowStyle(IntPtr handle) =>
        IntPtr.Size == 8
            ? GetWindowLongPtr64(handle, ExtendedWindowStyleIndex)
            : new IntPtr(GetWindowLong32(handle, ExtendedWindowStyleIndex));

    private delegate bool EnumWindowsCallback(IntPtr handle, IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindow(IntPtr handle, int command);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr handle, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr handle, int index);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(IntPtr handle, StringBuilder text, int maximumCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr handle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr handle,
        int attribute,
        out int value,
        int valueSize);
}
