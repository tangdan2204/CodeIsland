using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace CodeIsland.Desktop;

public sealed class PermissionRequestState
{
    public required string SessionId { get; init; }
    public required string ToolName { get; init; }
    public string? Detail { get; init; }
    public TaskCompletionSource<string> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public sealed class QuestionRequestState
{
    public required string SessionId { get; init; }
    public required string Question { get; init; }
    public string[] Options { get; init; } = [];
    public TaskCompletionSource<string> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public sealed class CompletionCardState
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public required string SessionId { get; init; }
    public required string Source { get; init; }
    public required string Title { get; init; }
    public required string Detail { get; init; }
    public string? Cwd { get; init; }
    public string? Model { get; init; }
    public int ToolCallCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
    public string SourceKey => SessionState.NormalizeSource(Source);
    public string SourceIconPath => $"cli-icons/{SourceKey}.png";
    public string MascotPath => File.Exists(Path.Combine(AppContext.BaseDirectory, "mascots", SourceKey + ".gif"))
        ? $"mascots/{SourceKey}.gif"
        : SourceKey == "claude" ? "mascot-claude.gif" : "mascot-codex.gif";
    public string TimeText => CreatedAt.ToLocalTime().ToString("HH:mm:ss");
    public string MetaText
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Model)) parts.Add(Model!);
            if (ToolCallCount > 0) parts.Add(ToolCallCount == 1 ? "1 tool" : $"{ToolCallCount} tools");
            if (!string.IsNullOrWhiteSpace(Cwd)) parts.Add(Path.GetFileName(Cwd.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
            return parts.Count == 0 ? SessionId : string.Join(" / ", parts);
        }
    }
}

public sealed class SessionStore
{
    private readonly Dictionary<string, SessionState> byId = [];
    public ObservableCollection<SessionState> Sessions { get; } = [];
    public ObservableCollection<CompletionCardState> CompletionCards { get; } = [];
    public PermissionRequestState? PendingPermission { get; private set; }
    public QuestionRequestState? PendingQuestion { get; private set; }
    public event EventHandler<HookEvent>? EventReceived;
    public event EventHandler? BlockingRequestChanged;
    public event EventHandler? SessionsChanged;
    public event EventHandler? CompletionCardsChanged;

    public int ActiveSessionCount => Sessions.Count(s => s.Status != AgentStatus.Idle);
    public int TotalToolCallCount => Sessions.Sum(s => s.ToolCallCount);
    public SessionState? PrimarySession => Sessions.FirstOrDefault(s => s.Status != AgentStatus.Idle) ?? Sessions.FirstOrDefault();
    public bool HasBlockingRequest => PendingPermission is not null || PendingQuestion is not null;
    public bool HasCompletionCards => CompletionCards.Count > 0;

    public void RestoreSavedSessions()
    {
        foreach (var session in SessionPersistence.Load())
        {
            if (byId.ContainsKey(session.SessionId)) continue;
            byId[session.SessionId] = session;
            Sessions.Add(session);
        }
        PruneVisibleSessions();
        SessionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SaveSessions() => SessionPersistence.Save(Sessions);

    public string Apply(HookEvent hookEvent)
    {
        var normalized = EventNormalizer.Normalize(hookEvent.EventName);
        var session = GetOrCreate(hookEvent.SessionId);
        session.LastEvent = normalized;
        session.Source = hookEvent.Source ?? session.Source;
        session.Cwd = string.IsNullOrWhiteSpace(hookEvent.Cwd) ? session.Cwd : hookEvent.Cwd;
        session.Model = string.IsNullOrWhiteSpace(hookEvent.Model) ? session.Model : hookEvent.Model;
        session.PermissionMode = string.IsNullOrWhiteSpace(hookEvent.PermissionMode) ? session.PermissionMode : hookEvent.PermissionMode;
        session.TermApp = string.IsNullOrWhiteSpace(hookEvent.TermApp) ? session.TermApp : hookEvent.TermApp;
        session.WindowsTerminalSession = string.IsNullOrWhiteSpace(hookEvent.WindowsTerminalSession) ? session.WindowsTerminalSession : hookEvent.WindowsTerminalSession;
        session.WindowTitleHint = string.IsNullOrWhiteSpace(hookEvent.WindowTitleHint) ? session.WindowTitleHint : hookEvent.WindowTitleHint;
        session.ProcessId = hookEvent.ProcessId ?? session.ProcessId;
        if (hookEvent.AncestorProcessIds.Length > 0) session.AncestorProcessIds = hookEvent.AncestorProcessIds;
        session.CurrentTool = hookEvent.ToolName ?? session.CurrentTool;
        session.LastMessage = hookEvent.ToolDescription ?? hookEvent.Question ?? session.LastMessage;
        session.LastActivity = DateTimeOffset.Now;
        ApplyMessages(session, hookEvent, normalized);
        ApplyHistory(session, hookEvent, normalized);
        ApplyStatus(session, hookEvent, normalized);
        ApplyCompletion(session, hookEvent, normalized);
        session.RefreshDerived();
        TerminalSessionIndex.Upsert(session);
        Reorder(session);
        PruneVisibleSessions();
        SaveSessions();
        SoundManager.PlayForEvent(normalized);
        EventReceived?.Invoke(this, hookEvent);
        SessionsChanged?.Invoke(this, EventArgs.Empty);
        return normalized;
    }

    public async Task<string> HandleBlockingAsync(HookEvent hookEvent, string normalized, CancellationToken token)
    {
        if (normalized == "PermissionRequest")
        {
            var request = new PermissionRequestState { SessionId = hookEvent.SessionId, ToolName = hookEvent.ToolName ?? "Tool", Detail = hookEvent.ToolDescription };
            PendingPermission = request;
            BlockingRequestChanged?.Invoke(this, EventArgs.Empty);
            try
            {
                using var registration = token.Register(() => request.Completion.TrySetResult(PermissionResponse(false)));
                return await request.Completion.Task.ConfigureAwait(false);
            }
            finally
            {
                if (ReferenceEquals(PendingPermission, request)) PendingPermission = null;
                BlockingRequestChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        if (normalized == "Notification" && hookEvent.Question is not null)
        {
            var request = new QuestionRequestState { SessionId = hookEvent.SessionId, Question = hookEvent.Question, Options = hookEvent.Options };
            PendingQuestion = request;
            BlockingRequestChanged?.Invoke(this, EventArgs.Empty);
            try
            {
                using var registration = token.Register(() => request.Completion.TrySetResult("{}"));
                return await request.Completion.Task.ConfigureAwait(false);
            }
            finally
            {
                if (ReferenceEquals(PendingQuestion, request)) PendingQuestion = null;
                BlockingRequestChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        return "{}";
    }

    public void ApprovePermission(bool always = false) => PendingPermission?.Completion.TrySetResult(PermissionResponse(true, always));
    public void DenyPermission() => PendingPermission?.Completion.TrySetResult(PermissionResponse(false));
    public void SkipQuestion() => PendingQuestion?.Completion.TrySetResult("{}");
    public void AnswerQuestion(string answer)
    {
        var escaped = JsonValue.Create(answer)?.ToJsonString() ?? "\"\"";
        PendingQuestion?.Completion.TrySetResult($"{{\"answer\":{escaped}}}");
    }

    public void DismissCompletion(CompletionCardState card)
    {
        CompletionCards.Remove(card);
        CompletionCardsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearCompletions()
    {
        CompletionCards.Clear();
        CompletionCardsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string PermissionResponse(bool allow, bool always = false)
    {
        var behavior = allow ? "allow" : "deny";
        var updatedInput = always ? ",\"updatedInput\":{\"alwaysAllow\":true}" : "";
        return $"{{\"hookSpecificOutput\":{{\"hookEventName\":\"PermissionRequest\",\"decision\":{{\"behavior\":\"{behavior}\"{updatedInput}}}}}}}";
    }

    private static void ApplyStatus(SessionState session, HookEvent hookEvent, string normalized)
    {
        switch (normalized)
        {
            case "SessionStart":
            case "UserPromptSubmit":
                session.Status = AgentStatus.Processing;
                session.CurrentTool = null;
                break;
            case "SubagentStart":
                session.ActiveSubagentCount += 1;
                session.Status = AgentStatus.Running;
                session.CurrentTool = "Agent";
                session.LastMessage = hookEvent.ToolDescription ?? hookEvent.AgentId ?? session.LastMessage;
                break;
            case "PreCompact":
            case "PostCompact":
                session.Status = AgentStatus.Processing;
                break;
            case "PreToolUse":
                session.Status = AgentStatus.Running;
                session.ToolCallCount += 1;
                break;
            case "PostToolUse":
            case "PostToolUseFailure":
                session.Status = AgentStatus.Processing;
                break;
            case "SubagentStop":
                session.ActiveSubagentCount = Math.Max(0, session.ActiveSubagentCount - 1);
                session.Status = AgentStatus.Processing;
                break;
            case "PermissionRequest":
                session.Status = AgentStatus.WaitingApproval;
                break;
            case "Notification":
                session.Status = hookEvent.Question is null ? AgentStatus.Processing : AgentStatus.WaitingQuestion;
                break;
            case "Stop":
            case "SessionEnd":
            case "TaskRoundComplete":
                session.Status = AgentStatus.Idle;
                session.CurrentTool = null;
                session.CompletionText = hookEvent.AssistantMessage ?? hookEvent.ToolDescription ?? session.LastAssistantMessage ?? "Completed";
                break;
        }
    }

    private void ApplyCompletion(SessionState session, HookEvent hookEvent, string normalized)
    {
        if (normalized is not ("Stop" or "SessionEnd" or "TaskRoundComplete")) return;
        var text = hookEvent.AssistantMessage
            ?? hookEvent.ToolDescription
            ?? session.CompletionText
            ?? session.LastAssistantMessage
            ?? "Completed";
        var title = session.DisplayName;
        var source = session.SourceDisplay;
        CompletionCards.Insert(0, new CompletionCardState
        {
            SessionId = session.SessionId,
            Source = source,
            Title = title,
            Detail = text.Length > 260 ? text[..260] : text,
            Cwd = session.Cwd,
            Model = session.Model,
            ToolCallCount = session.ToolCallCount
        });
        while (CompletionCards.Count > 8) CompletionCards.RemoveAt(CompletionCards.Count - 1);
        CompletionCardsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void ApplyMessages(SessionState session, HookEvent hookEvent, string normalized)
    {
        if (normalized == "UserPromptSubmit" && !string.IsNullOrWhiteSpace(hookEvent.UserPrompt))
        {
            session.LastUserPrompt = hookEvent.UserPrompt;
            session.AddRecentMessage("user", hookEvent.UserPrompt);
        }

        if (normalized is "AfterAgentResponse" or "TaskRoundComplete" or "Stop" or "SessionEnd" or "Notification"
            && !string.IsNullOrWhiteSpace(hookEvent.AssistantMessage)
            && hookEvent.AssistantMessage != hookEvent.Question)
        {
            session.LastAssistantMessage = hookEvent.AssistantMessage;
            session.AddRecentMessage("assistant", hookEvent.AssistantMessage);
        }
    }

    private static void ApplyHistory(SessionState session, HookEvent hookEvent, string normalized)
    {
        if (normalized is "PostToolUse" or "PostToolUseFailure")
        {
            var tool = session.CurrentTool ?? hookEvent.ToolName;
            if (!string.IsNullOrWhiteSpace(tool))
            {
                var success = normalized != "PostToolUseFailure";
                session.RecordTool(tool, session.LastMessage ?? hookEvent.ToolDescription, success);
            }
        }
    }

    private SessionState GetOrCreate(string sessionId)
    {
        if (byId.TryGetValue(sessionId, out var existing)) return existing;
        var session = new SessionState(sessionId);
        byId[sessionId] = session;
        Sessions.Insert(0, session);
        SessionsChanged?.Invoke(this, EventArgs.Empty);
        return session;
    }

    private void Reorder(SessionState session)
    {
        var index = Sessions.IndexOf(session);
        if (index > 0) Sessions.Move(index, 0);
    }

    private void PruneVisibleSessions()
    {
        var maxCount = Math.Clamp(WindowsSettings.Current.MaxVisibleSessions, 1, 20);
        while (Sessions.Count > maxCount)
        {
            var removed = Sessions[^1];
            Sessions.RemoveAt(Sessions.Count - 1);
            byId.Remove(removed.SessionId);
        }
    }
}
