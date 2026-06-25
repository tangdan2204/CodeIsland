using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace CodeIsland.Desktop;

public sealed class GlobalHotKeyManager : IDisposable
{
    private const int WmHotKey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private readonly Dictionary<int, Action> handlers = [];
    private HwndSource? source;
    private int nextId = 1;

    public void Attach(System.Windows.Window window)
    {
        if (source is not null) return;
        var helper = new WindowInteropHelper(window);
        source = HwndSource.FromHwnd(helper.Handle);
        source?.AddHook(WndProc);
    }

    public void RegisterDefaultShortcuts(MainWindow window)
    {
        Register(ModControl | ModShift, KeyInterop.VirtualKeyFromKey(System.Windows.Input.Key.I), window.TogglePanel);
        Register(ModControl | ModShift, KeyInterop.VirtualKeyFromKey(System.Windows.Input.Key.A), () => window.Store.ApprovePermission());
        Register(ModControl | ModShift, KeyInterop.VirtualKeyFromKey(System.Windows.Input.Key.Y), () => window.Store.ApprovePermission(always: true));
        Register(ModControl | ModShift, KeyInterop.VirtualKeyFromKey(System.Windows.Input.Key.D), () => window.Store.DenyPermission());
        Register(ModControl | ModShift, KeyInterop.VirtualKeyFromKey(System.Windows.Input.Key.S), () => window.Store.SkipQuestion());
        Register(ModControl | ModShift, KeyInterop.VirtualKeyFromKey(System.Windows.Input.Key.J), window.JumpToPrimarySession);
    }

    private void Register(uint modifiers, int virtualKey, Action action)
    {
        if (source is null) return;
        var id = nextId++;
        if (RegisterHotKey(source.Handle, id, modifiers, (uint)virtualKey))
        {
            handlers[id] = action;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotKey && handlers.TryGetValue(wParam.ToInt32(), out var action))
        {
            action();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (source is not null)
        {
            foreach (var id in handlers.Keys.ToList()) UnregisterHotKey(source.Handle, id);
            source.RemoveHook(WndProc);
        }
        handlers.Clear();
        source = null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
