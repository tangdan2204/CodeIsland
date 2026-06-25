using System.Runtime.InteropServices;
using System.Text;

namespace CodeIsland.Desktop;

public static class WindowsTerminalActivator
{
    private sealed record WindowCandidate(IntPtr Handle, int ProcessId, string ProcessName, string Title);

    private static readonly Dictionary<string, string> SourceProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["codex"] = "Codex",
        ["cursor"] = "Cursor",
        ["trae"] = "Trae",
        ["qoder"] = "Qoder",
        ["droid"] = "Factory",
        ["codebuddy"] = "CodeBuddy",
        ["opencode"] = "OpenCode",
    };

    public static void Activate(SessionState session)
    {
        if (TryActivateExisting(session)) return;
        LaunchAtWorkspace(session);
    }

    public static bool IsSessionVisible(SessionState session)
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero) return false;
        var title = GetWindowTitle(foreground);
        GetWindowThreadProcessId(foreground, out var pid);
        var foregroundProcess = SafeProcess((int)pid);
        if (foregroundProcess is null) return false;
        var candidate = new WindowCandidate(foreground, (int)pid, foregroundProcess.ProcessName, title);
        return CandidateScore(candidate, session) >= 80;
    }

    private static bool TryActivateExisting(SessionState session)
    {
        var candidates = EnumerateWindows()
            .Select(c => (candidate: c, score: CandidateScore(c, session)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ToList();
        foreach (var (candidate, _) in candidates)
        {
            if (TryActivateWindow(candidate.Handle)) return true;
        }
        return false;
    }

    private static int CandidateScore(WindowCandidate candidate, SessionState session)
    {
        var score = 0;
        var title = candidate.Title;
        var terminal = IsTerminalProcessName(candidate.ProcessName);
        var entry = TerminalSessionIndex.Find(session.SessionId);

        if (session.ProcessId is { } pid && pid == candidate.ProcessId) score += 100;
        if (session.AncestorProcessIds.Contains(candidate.ProcessId)) score += 90;
        if (entry?.ProcessId is { } indexedPid && indexedPid == candidate.ProcessId) score += 90;
        if (entry?.AncestorProcessIds.Contains(candidate.ProcessId) == true) score += 85;
        if (!string.IsNullOrWhiteSpace(session.WindowsTerminalSession) && title.Contains(session.WindowsTerminalSession, StringComparison.OrdinalIgnoreCase)) score += 95;
        if (!string.IsNullOrWhiteSpace(entry?.WindowsTerminalSession) && title.Contains(entry.WindowsTerminalSession, StringComparison.OrdinalIgnoreCase)) score += 90;
        if (!string.IsNullOrWhiteSpace(session.WindowTitleHint) && title.Contains(session.WindowTitleHint, StringComparison.OrdinalIgnoreCase)) score += 100;
        if (!string.IsNullOrWhiteSpace(entry?.WindowTitleHint) && title.Contains(entry.WindowTitleHint, StringComparison.OrdinalIgnoreCase)) score += 95;
        if (TitleContainsSessionToken(title, session.SessionId)) score += 85;
        if (terminal && TitleMatchesSession(title, session)) score += 75;
        if (!terminal && SourceProcessNames.TryGetValue(session.SourceKey, out var app) && candidate.ProcessName.Contains(app, StringComparison.OrdinalIgnoreCase)) score += 60;
        if (terminal && !string.IsNullOrWhiteSpace(session.Cwd) && TitleMatchesCwd(title, session.Cwd)) score += 55;
        if (terminal && !string.IsNullOrWhiteSpace(entry?.Cwd) && TitleMatchesCwd(title, entry.Cwd)) score += 50;
        return score;
    }

    private static bool TryActivateWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return false;
        ShowWindow(handle, 9);
        return SetForegroundWindow(handle);
    }

    private static void LaunchAtWorkspace(SessionState session)
    {
        var cwd = string.IsNullOrWhiteSpace(session.Cwd) || !Directory.Exists(session.Cwd)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : session.Cwd;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "wt.exe",
                Arguments = $"-d \"{cwd}\"",
                UseShellExecute = true
            });
        }
        catch
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = cwd, UseShellExecute = true });
        }
    }

    private static bool TitleMatchesSession(string title, SessionState session)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        if (!string.IsNullOrWhiteSpace(session.Cwd) && TitleMatchesCwd(title, session.Cwd)) return true;
        return !string.IsNullOrWhiteSpace(session.DisplayName) && title.Contains(session.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TitleMatchesCwd(string title, string cwd)
    {
        var folder = Path.GetFileName(cwd.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return !string.IsNullOrWhiteSpace(folder) && title.Contains(folder, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TitleContainsSessionToken(string title, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(sessionId)) return false;
        var token = sessionId.Length <= 16 ? sessionId : sessionId[..16];
        return title.Contains(sessionId, StringComparison.OrdinalIgnoreCase)
            || title.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<WindowCandidate> EnumerateWindows()
    {
        var windows = new List<WindowCandidate>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            var title = GetWindowTitle(hwnd);
            if (string.IsNullOrWhiteSpace(title)) return true;
            GetWindowThreadProcessId(hwnd, out var pid);
            var proc = SafeProcess((int)pid);
            if (proc is null) return true;
            windows.Add(new WindowCandidate(hwnd, (int)pid, proc.ProcessName, title));
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    private static bool IsTerminalProcessName(string processName)
    {
        return processName.Contains("WindowsTerminal", StringComparison.OrdinalIgnoreCase)
            || processName.Equals("wt", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("PowerShell", StringComparison.OrdinalIgnoreCase)
            || processName.Equals("cmd", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("wezterm", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("alacritty", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("tabby", StringComparison.OrdinalIgnoreCase);
    }

    private static System.Diagnostics.Process? SafeProcess(int pid)
    {
        try { return System.Diagnostics.Process.GetProcessById(pid); }
        catch { return null; }
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var length = GetWindowTextLength(hwnd);
        if (length <= 0) return string.Empty;
        var builder = new StringBuilder(length + 1);
        GetWindowText(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
}
