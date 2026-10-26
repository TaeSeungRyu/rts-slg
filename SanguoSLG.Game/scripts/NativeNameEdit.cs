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
    private const uint WsBorder = 0x00800000;
    private const uint EsAutoHScroll = 0x0080;
    private const uint SwpNoZOrder = 0x0004;
    private readonly LineEdit _placeholder;
    private readonly Viewport _viewport;
    private nint _handle;

    private NativeNameEdit(nint handle, LineEdit placeholder, Viewport viewport)
    {
        _handle = handle;
        _placeholder = placeholder;
        _viewport = viewport;
        SyncBounds();
        SetFocus(_handle);
    }

    public static NativeNameEdit? Create(LineEdit placeholder, Viewport viewport)
    {
        var parent = (nint)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle);
        if (parent == 0) return null;
        var handle = CreateWindowExW(0, "EDIT", placeholder.Text,
            WsChild | WsVisible | WsTabStop | WsBorder | EsAutoHScroll,
            0, 0, 1, 1, parent, 0, 0, 0);
        return handle == 0 ? null : new NativeNameEdit(handle, placeholder, viewport);
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
            Mathf.RoundToInt(rect.Position.X * scaleX), Mathf.RoundToInt(rect.Position.Y * scaleY),
            Mathf.Max(1, Mathf.RoundToInt(rect.Size.X * scaleX)),
            Mathf.Max(1, Mathf.RoundToInt(rect.Size.Y * scaleY)), SwpNoZOrder);
    }

    public void Dispose()
    {
        if (_handle == 0) return;
        DestroyWindow(_handle);
        _handle = 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

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
    private static extern bool SetWindowPos(nint window, nint after, int x, int y,
        int width, int height, uint flags);
    [DllImport("user32.dll")]
    private static extern nint SetFocus(nint window);
    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(nint window);
}
