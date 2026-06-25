using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using CodeIsland.WinUI.Models;

namespace CodeIsland.WinUI.Services;

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

public sealed class SessionStore
{
    private readonly Dictionary<string, SessionState> byId = [];

    public ObservableCollection<SessionState> Sessions { get; } = [];
    public PermissionRequestState? PendingPermission { get; private set; }
    public QuestionRequestState? PendingQuestion { get; private set; }
    public event EventHandler<HookEvent>? EventReceived;
    public event EventHandler? BlockingRequestChanged;

    public string Apply(HookEvent hookEvent)
    {
        var normalized = EventNormalizer.Normalize(hookEvent.EventName);
        var session = GetOrCreate(hookEvent.SessionId);
        session.LastEvent = normalized;
        session.LastActivity = DateTimeOffset.Now;
        session.Source = NormalizeSource(hookEvent.Source) ?? session.Source;
        session.Cwd = string.IsNullOrWhiteSpace(hookEvent.Cwd) ? session.Cwd : hookEvent.Cwd;
        session.CurrentTool = hookEvent.ToolName ?? session.CurrentTool;

        ApplyStatus(session, hookEvent, normalized);
        session.RefreshDerived();
        EventReceived?.Invoke(this, hookEvent);
        return normalized;
    }

    public async Task<string> HandleBlockingAsync(HookEvent hookEvent, string normalized, CancellationToken token)
    {
        if (normalized == "PermissionRequest")
        {
            var request = new PermissionRequestState
            {
                SessionId = hookEvent.SessionId,
                ToolName = hookEvent.ToolName ?? "Tool",
                Detail = hookEvent.ToolDescription
            };
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
            var request = new QuestionRequestState
            {
                SessionId = hookEvent.SessionId,
                Question = hookEvent.Question,
                Options = hookEvent.Options
            };
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

    public void ApprovePermission(bool always = false)
    {
        PendingPermission?.Completion.TrySetResult(PermissionResponse(true, always));
    }

    public void DenyPermission()
    {
        PendingPermission?.Completion.TrySetResult(PermissionResponse(false));
    }

    public void AnswerQuestion(string answer)
    {
        var escaped = JsonValue.Create(answer)?.ToJsonString() ?? "\"\"";
        PendingQuestion?.Completion.TrySetResult($"{{\"answer\":{escaped}}}");
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
            case "SubagentStart":
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
            case "SubagentStop":
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
                break;
        }
    }

    private SessionState GetOrCreate(string sessionId)
    {
        if (byId.TryGetValue(sessionId, out var existing)) return existing;
        var session = new SessionState(sessionId);
        byId[sessionId] = session;
        Sessions.Insert(0, session);
        return session;
    }

    private static string? NormalizeSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        var normalized = source.Trim().ToLowerInvariant();
        return normalized switch
        {
            "factory" => "droid",
            "ag" or "anti-gravity" or "anti gravity" => "antigravity",
            "googleantigravity" or "google antigravity" or "google-anti-gravity" or "antigravity-ide" or "antigravity-cli" or "agy" => "google-antigravity",
            "work-buddy" or "workbody" => "workbuddy",
            "hermes-agent" or "hermes-agents" => "hermes",
            "qwen-code" or "qwencode" => "qwen",
            "cursor-agent" or "cursoragent" or "cursorcli" => "cursor-cli",
            "qodercli" => "qoder-cli",
            "kimi-cli" or "kimicli" => "kimi",
            "kiro-cli" or "kirocli" => "kiro",
            "codebuddycn" or "codybuddy-cn" => "codybuddycn",
            "step-fun" => "stepfun",
            "trae-cn" or "trae_cn" => "traecn",
            "omp" or "oh-my-pi" => "pi",
            _ => normalized
        };
    }
}