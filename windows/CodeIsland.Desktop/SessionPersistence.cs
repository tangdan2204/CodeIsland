using System.Text.Json;

namespace CodeIsland.Desktop;

public static class SessionPersistence
{
    private static readonly string SessionPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".codeisland",
        "windows-sessions.json");

    public static void Save(IEnumerable<SessionState> sessions)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SessionPath)!);
            var dto = sessions.Take(20).Select(SessionDto.From).ToArray();
            File.WriteAllText(SessionPath, JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
        }
    }

    public static IEnumerable<SessionState> Load()
    {
        if (!File.Exists(SessionPath)) yield break;
        SessionDto[]? dto;
        try
        {
            dto = JsonSerializer.Deserialize<SessionDto[]>(File.ReadAllText(SessionPath));
        }
        catch
        {
            yield break;
        }
        if (dto is null) yield break;
        var maxCount = Math.Clamp(WindowsSettings.Current.MaxVisibleSessions, 1, 20);
        foreach (var item in dto.OrderByDescending(item => item.LastActivity).Take(maxCount))
        {
            var session = new SessionState(item.SessionId)
            {
                Source = item.Source,
                Cwd = item.Cwd,
                Model = item.Model,
                PermissionMode = item.PermissionMode,
                LastUserPrompt = item.LastUserPrompt,
                LastAssistantMessage = item.LastAssistantMessage,
                LastMessage = item.LastMessage,
                LastEvent = item.LastEvent,
                CompletionText = item.CompletionText,
                TermApp = item.TermApp,
                WindowsTerminalSession = item.WindowsTerminalSession,
                WindowTitleHint = item.WindowTitleHint,
                ProcessId = item.ProcessId,
                AncestorProcessIds = item.AncestorProcessIds ?? [],
                LastActivity = item.LastActivity,
                ToolCallCount = item.ToolCallCount,
                Status = AgentStatus.Idle
            };
            foreach (var msg in item.RecentMessages ?? []) session.AddRecentMessage(msg.Role, msg.Text);
            foreach (var tool in item.ToolHistory ?? []) session.ToolHistory.Add(tool);
            session.RefreshDerived();
            TerminalSessionIndex.Upsert(session);
            yield return session;
        }
    }

    private sealed class SessionDto
    {
        public required string SessionId { get; init; }
        public string? Source { get; init; }
        public string? Cwd { get; init; }
        public string? Model { get; init; }
        public string? PermissionMode { get; init; }
        public string? LastUserPrompt { get; init; }
        public string? LastAssistantMessage { get; init; }
        public string? LastMessage { get; init; }
        public string? LastEvent { get; init; }
        public string? CompletionText { get; init; }
        public string? TermApp { get; init; }
        public string? WindowsTerminalSession { get; init; }
        public string? WindowTitleHint { get; init; }
        public int? ProcessId { get; init; }
        public int[]? AncestorProcessIds { get; init; }
        public DateTimeOffset LastActivity { get; init; }
        public int ToolCallCount { get; init; }
        public ChatMessageState[]? RecentMessages { get; init; }
        public ToolHistoryState[]? ToolHistory { get; init; }

        public static SessionDto From(SessionState session) => new()
        {
            SessionId = session.SessionId,
            Source = session.Source,
            Cwd = session.Cwd,
            Model = session.Model,
            PermissionMode = session.PermissionMode,
            LastUserPrompt = session.LastUserPrompt,
            LastAssistantMessage = session.LastAssistantMessage,
            LastMessage = session.LastMessage,
            LastEvent = session.LastEvent,
            CompletionText = session.CompletionText,
            TermApp = session.TermApp,
            WindowsTerminalSession = session.WindowsTerminalSession,
            WindowTitleHint = session.WindowTitleHint,
            ProcessId = session.ProcessId,
            AncestorProcessIds = session.AncestorProcessIds,
            LastActivity = session.LastActivity,
            ToolCallCount = session.ToolCallCount,
            RecentMessages = session.RecentMessages.ToArray(),
            ToolHistory = session.ToolHistory.ToArray()
        };
    }
}
