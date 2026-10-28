using System.Runtime.InteropServices;
using System.Text;
using Godot;

namespace SanguoSLG.Game;

// The Windows EDIT control owns IME composition and its caret. Godot's LineEdit
// remains as a layout placeholder; changing its text during composition caused
// the next Korean syllable to replace the previous one.
internal sealed class NativeNameEdit : IDisposable
{
    private const uint WsChild = 0x40000000;
    private const uint WsVisible = 0x10000000;
    private const uint WsTabStop = 0x00010000;
    private const uint EsAutoHScroll = 0x0080;
    private const uint SwpNoZOrder = 0x0004;
    private const uint WmCtlColorEdit = 0x0133;
    private const uint EmSetMargins = 0x00D3;
    private const uint EcLeftMargin = 0x0001;
    private const uint EcRightMargin = 0x0002;
    private const uint Background = 0x0014171c; // COLORREF RGB(28, 23, 20)
    private const uint Foreground = 0x00c3f1ff; // COLORREF RGB(255, 241, 195)
    private readonly LineEdit _placeholder;
    private readonly Viewport _viewport;
    private readonly nint _parent;
    private readonly nint _backgroundBrush;
    private readonly SubclassProcedure _subclassProcedure;
    private nint _handle;

    private NativeNameEdit(nint handle, nint parent, LineEdit placeholder, Viewport viewport)
    {
        _handle = handle;
        _parent = parent;
        _placeholder = placeholder;
        _viewport = viewport;
        _backgroundBrush = CreateSolidBrush(Background);
        _subclassProcedure = ColorEdit;
        SetWindowSubclass(_parent, _subclassProcedure, (nuint)_handle, 0);
        SendMessageW(_handle, EmSetMargins, EcLeftMargin | EcRightMargin, (nint)((8 << 16) | 8));
        SyncBounds();
        SetFocus(_handle);
    }

    public static NativeNameEdit? Create(LineEdit placeholder, Viewport viewport)
    {
        var parent = (nint)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle);
        if (parent == 0) return null;
        var handle = CreateWindowExW(0, "EDIT", placeholder.Text,
            WsChild | WsVisible | WsTabStop | EsAutoHScroll,
            0, 0, 1, 1, parent, 0, 0, 0);
        return handle == 0 ? null : new NativeNameEdit(handle, parent, placeholder, viewport);
    }

    public string Text
    {
        get
        {
            if (_handle == 0) return "";
            var result = new StringBuilder(GetWindowTextLengthW(_handle) + 1);
            GetWindowTextW(_handle, result, result.Capacity);
            return result.ToString();
        }
    }

    public void SyncBounds()
    {
        if (_handle == 0 || !GodotObject.IsInstanceValid(_placeholder)
            || !_placeholder.IsInsideTree()) return;
        var parent = (nint)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle);
        if (parent == 0 || !GetClientRect(parent, out var client)) return;
        var viewportSize = _viewport.GetVisibleRect().Size;
        if (viewportSize.X <= 0 || viewportSize.Y <= 0) return;
        var scaleX = (float)client.Right / viewportSize.X;
        var scaleY = (float)client.Bottom / viewportSize.Y;
        var rect = _placeholder.GetGlobalRect();
        SetWindowPos(_handle, 0,
            Mathf.RoundToInt(rect.Position.X * scaleX) + 2, Mathf.RoundToInt(rect.Position.Y * scaleY) + 2,
            Mathf.Max(1, Mathf.RoundToInt(rect.Size.X * scaleX) - 4),
            Mathf.Max(1, Mathf.RoundToInt(rect.Size.Y * scaleY) - 4), SwpNoZOrder);
    }

    private nint ColorEdit(nint window, uint message, nint wParam, nint lParam, nuint subclassId, nuint data)
    {
        if (message == WmCtlColorEdit && lParam == _handle)
        {
            SetTextColor(wParam, Foreground);
            SetBkColor(wParam, Background);
            return _backgroundBrush;
        }
        return DefSubclassProc(window, message, wParam, lParam);
    }

    public void Dispose()
    {
        if (_handle == 0) return;
        // Windows hides the pointer while typing in EDIT. Return keyboard focus
        // before destroying it so the pointer is restored without a mouse move.
        if (GetFocus() == _handle) SetFocus(_parent);
        RemoveWindowSubclass(_parent, _subclassProcedure, (nuint)_handle);
        DestroyWindow(_handle);
        _handle = 0;
        DeleteObject(_backgroundBrush);
        // Windows' "hide pointer while typing" can leave the cursor hidden
        // after the EDIT child is destroyed, until a physical mouse move.
        var cursor = new CursorInfo { Size = (uint)Marshal.SizeOf<CursorInfo>() };
        if (GetCursorInfo(ref cursor) && (cursor.Flags & 1u) == 0)
            ShowCursor(true);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorInfo
    {
        public uint Size;
        public uint Flags;
        public nint Cursor;
        public int X;
        public int Y;
    }

    private delegate nint SubclassProcedure(nint window, uint message, nint wParam, nint lParam,
        nuint subclassId, nuint data);

    [DllImport("comctl32.dll")]
    private static extern bool SetWindowSubclass(nint window, SubclassProcedure callback, nuint subclassId, nuint data);
    [DllImport("comctl32.dll")]
    private static extern bool RemoveWindowSubclass(nint window, SubclassProcedure callback, nuint subclassId);
    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint window, uint message, nint wParam, nint lParam);
    [DllImport("gdi32.dll")]
    private static extern nint CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint handle);
    [DllImport("gdi32.dll")]
    private static extern uint SetTextColor(nint deviceContext, uint color);
    [DllImport("gdi32.dll")]
    private static extern uint SetBkColor(nint deviceContext, uint color);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessageW(nint window, uint message, uint wParam, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW", SetLastError = true)]
    private static extern nint CreateWindowExW(uint exStyle, string className, string windowName,
        uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextW")]
    private static extern int GetWindowTextW(nint window, StringBuilder text, int maxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextLengthW")]
    private static extern int GetWindowTextLengthW(nint window);
    [DllImport("user32.dll")]
    private static extern bool GetClientRect(nint window, out Rect rect);
    [DllImport("user32.dll")]
    private static extern nint GetFocus();
    [DllImport("user32.dll")]
    private static extern bool GetCursorInfo(ref CursorInfo cursor);
    [DllImport("user32.dll")]
    private static extern int ShowCursor(bool show);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint window, nint after, int x, int y,
        int width, int height, uint flags);
    [DllImport("user32.dll")]
    private static extern nint SetFocus(nint window);
    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(nint window);
}
