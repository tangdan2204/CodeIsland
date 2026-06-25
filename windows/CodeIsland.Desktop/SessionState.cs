using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CodeIsland.Desktop;

public sealed class SessionState : INotifyPropertyChanged
{
    private AgentStatus status = AgentStatus.Idle;
    private string? currentTool;
    private string? cwd;
    private string? source;
    private string? lastEvent;
    private string? lastMessage;
    private string? model;
    private string? permissionMode;
    private string? lastUserPrompt;
    private string? lastAssistantMessage;
    private string? completionText;
    private string? termApp;
    private string? windowsTerminalSession;
    private string? windowTitleHint;
    private int? processId;
    private int[] ancestorProcessIds = [];
    private int activeSubagentCount;
    private DateTimeOffset lastActivity = DateTimeOffset.Now;
    private int toolCallCount;

    public string SessionId { get; }

    public SessionState(string sessionId) => SessionId = sessionId;

    public AgentStatus Status { get => status; set => SetField(ref status, value); }
    public string? CurrentTool { get => currentTool; set => SetField(ref currentTool, value); }
    public string? Cwd { get => cwd; set => SetField(ref cwd, value); }
    public string? Source { get => source; set => SetField(ref source, value); }
    public string? LastEvent { get => lastEvent; set => SetField(ref lastEvent, value); }
    public string? LastMessage { get => lastMessage; set => SetField(ref lastMessage, value); }
    public string? Model { get => model; set => SetField(ref model, value); }
    public string? PermissionMode { get => permissionMode; set => SetField(ref permissionMode, value); }
    public string? LastUserPrompt { get => lastUserPrompt; set => SetField(ref lastUserPrompt, value); }
    public string? LastAssistantMessage { get => lastAssistantMessage; set => SetField(ref lastAssistantMessage, value); }
    public string? CompletionText { get => completionText; set => SetField(ref completionText, value); }
    public string? TermApp { get => termApp; set => SetField(ref termApp, value); }
    public string? WindowsTerminalSession { get => windowsTerminalSession; set => SetField(ref windowsTerminalSession, value); }
    public string? WindowTitleHint { get => windowTitleHint; set => SetField(ref windowTitleHint, value); }
    public int? ProcessId { get => processId; set => SetField(ref processId, value); }
    public int[] AncestorProcessIds { get => ancestorProcessIds; set => SetField(ref ancestorProcessIds, value); }
    public int ActiveSubagentCount { get => activeSubagentCount; set => SetField(ref activeSubagentCount, value); }
    public DateTimeOffset LastActivity { get => lastActivity; set => SetField(ref lastActivity, value); }
    public int ToolCallCount { get => toolCallCount; set => SetField(ref toolCallCount, value); }

    public ObservableCollection<ToolHistoryState> ToolHistory { get; } = [];
    public ObservableCollection<ChatMessageState> RecentMessages { get; } = [];

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Cwd))
            {
                var trimmed = Cwd.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var name = Path.GetFileName(trimmed);
                if (!string.IsNullOrWhiteSpace(name)) return name;
            }
            return SessionId.Length <= 10 ? SessionId : SessionId[..10];
        }
    }

    public string ActivityLine => !string.IsNullOrWhiteSpace(CurrentTool)
        ? (string.IsNullOrWhiteSpace(LastMessage) ? CurrentTool! : $"{CurrentTool}: {LastMessage}")
        : (!string.IsNullOrWhiteSpace(LastMessage) ? LastMessage! : (LastEvent ?? "Idle"));

    public string CompactToolText => !string.IsNullOrWhiteSpace(CurrentTool)
        ? CurrentTool!
        : Status switch
        {
            AgentStatus.WaitingApproval => "approval",
            AgentStatus.WaitingQuestion => "question",
            AgentStatus.Processing => "thinking",
            AgentStatus.Running => "running",
            _ => "idle"
        };

    public string ProjectAndId
    {
        get
        {
            var project = DisplayName;
            return string.IsNullOrWhiteSpace(ShortId) ? project : $"{project} #{ShortId}";
        }
    }

    public string SourceDisplay => string.IsNullOrWhiteSpace(Source) ? "Agent" : Source!;
    public string SourceKey => NormalizeSource(Source);
    public string SourceIconPath => $"cli-icons/{SourceKey}.png";
    public string MascotPath => File.Exists(Path.Combine(AppContext.BaseDirectory, "mascots", SourceKey + ".gif"))
        ? $"mascots/{SourceKey}.gif"
        : SourceKey == "claude" ? "mascot-claude.gif" : "mascot-codex.gif";
    public string ShortId => SessionId.Length <= 6 ? SessionId : SessionId[..6];
    public string CwdDisplay => string.IsNullOrWhiteSpace(Cwd) ? "No workspace" : Cwd!;
    public string ToolCountText => ToolCallCount == 1 ? "1 tool" : $"{ToolCallCount} tools";
    public string LastActivityText => LastActivity.ToLocalTime().ToString("HH:mm:ss");
    public string ModelBadge => string.IsNullOrWhiteSpace(Model) ? "model ?" : Model!;
    public string PermissionBadge => string.IsNullOrWhiteSpace(PermissionMode) ? "perm ?" : PermissionMode!;
    public string TerminalBadge => string.IsNullOrWhiteSpace(TermApp) ? "terminal ?" : TermApp!;
    public string SubagentBadge => ActiveSubagentCount <= 0 ? "" : $"+{ActiveSubagentCount} sub";
    public bool HasRecentMessages => RecentMessages.Count > 0;
    public bool HasToolHistory => ToolHistory.Count > 0;

    public string StatusText => Status switch
    {
        AgentStatus.Processing => "Processing",
        AgentStatus.Running => "Running",
        AgentStatus.WaitingApproval => "Waiting",
        AgentStatus.WaitingQuestion => "Question",
        _ => "Idle"
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    public void AddRecentMessage(string role, string text, int maxCount = 3)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0) return;
        var last = RecentMessages.LastOrDefault();
        if (last?.Role == role && last.Text == trimmed) return;
        RecentMessages.Add(new ChatMessageState(role, trimmed));
        while (RecentMessages.Count > maxCount) RecentMessages.RemoveAt(0);
        OnPropertyChanged(nameof(HasRecentMessages));
    }

    public void RecordTool(string tool, string? description, bool success, string? agentType = null, int maxCount = 20)
    {
        ToolHistory.Add(new ToolHistoryState(tool, description, success, agentType, DateTimeOffset.Now));
        while (ToolHistory.Count > maxCount) ToolHistory.RemoveAt(0);
        OnPropertyChanged(nameof(HasToolHistory));
    }

    public void RefreshDerived()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(ActivityLine));
        OnPropertyChanged(nameof(CompactToolText));
        OnPropertyChanged(nameof(ProjectAndId));
        OnPropertyChanged(nameof(SourceDisplay));
        OnPropertyChanged(nameof(SourceKey));
        OnPropertyChanged(nameof(SourceIconPath));
        OnPropertyChanged(nameof(MascotPath));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(CwdDisplay));
        OnPropertyChanged(nameof(ToolCountText));
        OnPropertyChanged(nameof(LastActivityText));
        OnPropertyChanged(nameof(ModelBadge));
        OnPropertyChanged(nameof(PermissionBadge));
        OnPropertyChanged(nameof(TerminalBadge));
        OnPropertyChanged(nameof(SubagentBadge));
        OnPropertyChanged(nameof(HasRecentMessages));
        OnPropertyChanged(nameof(HasToolHistory));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
        if (name is nameof(Status)) OnPropertyChanged(nameof(StatusText));
        if (name is nameof(Cwd))
        {
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(CwdDisplay));
        }
        if (name is nameof(Source))
        {
            OnPropertyChanged(nameof(SourceDisplay));
            OnPropertyChanged(nameof(SourceKey));
            OnPropertyChanged(nameof(SourceIconPath));
            OnPropertyChanged(nameof(MascotPath));
        }
        if (name is nameof(CurrentTool) or nameof(LastEvent) or nameof(LastMessage)) OnPropertyChanged(nameof(ActivityLine));
        if (name is nameof(CurrentTool) or nameof(Status)) OnPropertyChanged(nameof(CompactToolText));
        if (name is nameof(ToolCallCount)) OnPropertyChanged(nameof(ToolCountText));
        if (name is nameof(LastActivity)) OnPropertyChanged(nameof(LastActivityText));
        if (name is nameof(Model)) OnPropertyChanged(nameof(ModelBadge));
        if (name is nameof(PermissionMode)) OnPropertyChanged(nameof(PermissionBadge));
        if (name is nameof(TermApp)) OnPropertyChanged(nameof(TerminalBadge));
        if (name is nameof(ActiveSubagentCount)) OnPropertyChanged(nameof(SubagentBadge));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public static string NormalizeSource(string? source)
    {
        var key = string.IsNullOrWhiteSpace(source) ? "codex" : source.Trim().ToLowerInvariant();
        return key switch
        {
            "cursor-cli" or "cursoragent" => "cursor",
            "qoder-cli" => "qoder",
            "traecli" or "trae-cn" or "traecn" => "trae",
            "google-antigravity" => "gemini",
            "qwen-code" => "qwen",
            "kimi-cli" => "kimi",
            "omp" or "oh-my-pi" => "pi",
            _ => key
        };
    }
}

public sealed record ChatMessageState(string Role, string Text)
{
    public string Prefix => Role == "user" ? ">" : "AI";
    public string RoleDisplay => Role == "user" ? "You" : "AI";
}

public sealed record ToolHistoryState(string Tool, string? Description, bool Success, string? AgentType, DateTimeOffset Timestamp)
{
    public string StatusGlyph => Success ? "OK" : "ERR";
    public string Summary => string.IsNullOrWhiteSpace(Description) ? Tool : $"{Tool}: {Description}";
    public string TimeText => Timestamp.ToLocalTime().ToString("HH:mm:ss");
    public string AgentText => string.IsNullOrWhiteSpace(AgentType) ? "" : AgentType!;
}
