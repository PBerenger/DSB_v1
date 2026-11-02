using System.Runtime.InteropServices;

namespace DoubleSceensBlack;

internal static class CursorVisibility
{
    private const uint SpiSetCursors = 0x0057;
    private const uint SpifSendChange = 0x0002;
    private const uint ArrowCursorId = 32512;
    private static readonly uint[] CursorIds =
    [
        32512, 32513, 32514, 32515, 32516,
        32642, 32643, 32644, 32645, 32646, 32648, 32649, 32650, 32651, 32671, 32672
    ];

    private static bool _isHidden;

    public static bool TryHide(out int errorCode)
    {
        errorCode = 0;
        if (_isHidden)
        {
            return true;
        }

        byte[] andMask = [0xFF, 0xFF];
        byte[] xorMask = [0x00, 0x00];
        foreach (uint cursorId in CursorIds)
        {
            IntPtr cursor = CreateCursor(IntPtr.Zero, 0, 0, 1, 1, andMask, xorMask);
            if (cursor == IntPtr.Zero)
            {
                errorCode = Marshal.GetLastWin32Error();
                RestoreSystemCursors(out int restoreError);
                if (restoreError != 0)
                {
                    errorCode = restoreError;
                }

                return false;
            }

            if (!SetSystemCursor(cursor, cursorId))
            {
                errorCode = Marshal.GetLastWin32Error();
                DestroyCursor(cursor);
                RestoreSystemCursors(out int restoreError);
                if (restoreError != 0)
                {
                    errorCode = restoreError;
                }

                return false;
            }
        }

        IntPtr arrowCursor = LoadCursor(IntPtr.Zero, new IntPtr(ArrowCursorId));
        if (arrowCursor == IntPtr.Zero)
        {
            errorCode = Marshal.GetLastWin32Error();
            RestoreSystemCursors(out int restoreError);
            if (restoreError != 0)
            {
                errorCode = restoreError;
            }

            return false;
        }

        SetCursor(arrowCursor);
        _isHidden = true;
        return true;
    }

    public static bool TryRestore(out int errorCode)
    {
        errorCode = 0;
        if (!_isHidden)
        {
            return true;
        }

        if (!RestoreSystemCursors(out errorCode))
        {
            return false;
        }

        IntPtr arrowCursor = LoadCursor(IntPtr.Zero, new IntPtr(ArrowCursorId));
        if (arrowCursor == IntPtr.Zero)
        {
            errorCode = Marshal.GetLastWin32Error();
            return false;
        }

        SetCursor(arrowCursor);
        _isHidden = false;
        return true;
    }

    private static bool RestoreSystemCursors(out int errorCode)
    {
        if (SystemParametersInfo(SpiSetCursors, 0, IntPtr.Zero, SpifSendChange))
        {
            errorCode = 0;
            return true;
        }

        errorCode = Marshal.GetLastWin32Error();
        return false;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateCursor(
        IntPtr instance,
        int hotspotX,
        int hotspotY,
        int width,
        int height,
        byte[] andMask,
        byte[] xorMask);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSystemCursor(IntPtr cursor, uint cursorId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyCursor(IntPtr cursor);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint action,
        uint parameter,
        IntPtr value,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr LoadCursor(IntPtr instance, IntPtr cursorName);

    [DllImport("user32.dll")]
    private static extern IntPtr SetCursor(IntPtr cursor);
}
