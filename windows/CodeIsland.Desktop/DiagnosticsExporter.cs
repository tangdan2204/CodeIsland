using System.IO.Compression;
using System.Text.Json;

namespace CodeIsland.Desktop;

public static class DiagnosticsExporter
{
    public static string Export(SessionStore store)
    {
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        var root = Path.Combine(Path.GetTempPath(), "CodeIsland-Diagnostics-" + stamp);
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "state"));
        Directory.CreateDirectory(Path.Combine(root, "logs"));
        Directory.CreateDirectory(Path.Combine(root, "configs"));

        WriteJson(Path.Combine(root, "metadata.json"), new
        {
            exportedAt = DateTimeOffset.Now,
            os = Environment.OSVersion.ToString(),
            machine = Environment.MachineName,
            user = Environment.UserName,
            pipe = @"\\.\pipe\codeisland",
            terminalSessionIndex = TerminalSessionIndex.PathOnDisk,
            settings = WindowsSettings.Current
        });
        WriteJson(Path.Combine(root, "state", "sessions.json"), store.Sessions.Select(s => new
        {
            s.SessionId,
            s.Source,
            s.StatusText,
            s.Cwd,
            s.Model,
            s.PermissionMode,
            s.LastEvent,
            s.LastMessage,
            s.LastUserPrompt,
            s.LastAssistantMessage,
            s.TermApp,
            s.WindowsTerminalSession,
            s.WindowTitleHint,
            s.ProcessId,
            s.AncestorProcessIds,
            s.ToolCallCount,
            s.ActiveSubagentCount,
            recentMessages = s.RecentMessages,
            toolHistory = s.ToolHistory
        }).ToArray());
        CopyIfExists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codeisland", "bridge.log"), Path.Combine(root, "logs", "bridge.log"));
        CopyIfExists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codeisland", "windows-sessions.json"), Path.Combine(root, "state", "windows-sessions.json"));
        CopyIfExists(TerminalSessionIndex.PathOnDisk, Path.Combine(root, "state", "windows-terminal-sessions.json"));
        foreach (var path in ConfigPaths()) CopyIfExists(path.source, Path.Combine(root, "configs", path.name));

        var destination = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), $"CodeIsland-Diagnostics-{stamp}.zip");
        if (File.Exists(destination)) File.Delete(destination);
        ZipFile.CreateFromDirectory(root, destination);
        Directory.Delete(root, recursive: true);
        return destination;
    }

    private static IEnumerable<(string source, string name)> ConfigPaths()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return (Path.Combine(home, ".claude", "settings.json"), "claude-settings.json");
        yield return (Path.Combine(Environment.GetEnvironmentVariable("CODEX_HOME") ?? Path.Combine(home, ".codex"), "hooks.json"), "codex-hooks.json");
        yield return (Path.Combine(home, ".gemini", "settings.json"), "gemini-settings.json");
        yield return (Path.Combine(home, ".cursor", "hooks.json"), "cursor-hooks.json");
        yield return (Path.Combine(home, ".qwen", "settings.json"), "qwen-settings.json");
        yield return (Path.Combine(home, ".config", "opencode", "opencode.jsonc"), "opencode-jsonc.jsonc");
        yield return (Path.Combine(home, ".config", "opencode", "opencode.json"), "opencode-json.json");
        yield return (Path.Combine(home, ".config", "opencode", "config.json"), "opencode-config.json");
        yield return (Path.Combine(home, ".config", "opencode", "plugins", "codeisland.js"), "opencode-codeisland.js");
        yield return (Path.Combine(home, ".pi", "agent", "extensions", "codeisland.ts"), "pi-codeisland.ts");
        yield return (Path.Combine(home, ".omp", "agent", "extensions", "codeisland.ts"), "omp-codeisland.ts");
        yield return (Path.Combine(home, ".kimi", "config.toml"), "kimi-config.toml");
    }

    private static void CopyIfExists(string source, string destination)
    {
        if (!File.Exists(source)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }

    private static void WriteJson(string path, object value)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }
}
