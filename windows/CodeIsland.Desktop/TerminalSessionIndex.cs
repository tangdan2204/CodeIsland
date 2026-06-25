using System.Text.Json;

namespace CodeIsland.Desktop;

public sealed record TerminalSessionEntry(
    string SessionId,
    string? Source,
    string? Cwd,
    string? WindowsTerminalSession,
    string? TermApp,
    string? WindowTitleHint,
    int? ProcessId,
    int[] AncestorProcessIds,
    string? DisplayName,
    DateTimeOffset UpdatedAt);

public static class TerminalSessionIndex
{
    private static readonly string IndexPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".codeisland",
        "windows-terminal-sessions.json");

    public static string PathOnDisk => IndexPath;

    public static void Upsert(SessionState session)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(IndexPath)!);
            var entries = Load().Where(e => e.SessionId != session.SessionId).ToList();
            entries.Insert(0, new TerminalSessionEntry(
                session.SessionId,
                session.Source,
                session.Cwd,
                session.WindowsTerminalSession,
                session.TermApp,
                session.WindowTitleHint,
                session.ProcessId,
                session.AncestorProcessIds,
                session.DisplayName,
                DateTimeOffset.Now));
            entries = entries
                .Where(e => DateTimeOffset.Now - e.UpdatedAt < TimeSpan.FromDays(7))
                .Take(80)
                .ToList();
            File.WriteAllText(IndexPath, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
        }
    }

    public static TerminalSessionEntry? Find(string sessionId)
    {
        return Load().FirstOrDefault(e => string.Equals(e.SessionId, sessionId, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<TerminalSessionEntry> Load()
    {
        try
        {
            if (!File.Exists(IndexPath)) return [];
            return JsonSerializer.Deserialize<TerminalSessionEntry[]>(File.ReadAllText(IndexPath)) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
