using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CodeIsland.WinUI.Models;

public sealed class SessionState : INotifyPropertyChanged
{
    private AgentStatus status = AgentStatus.Idle;
    private string? currentTool;
    private string? cwd;
    private string? source;
    private string? lastEvent;
    private DateTimeOffset lastActivity = DateTimeOffset.Now;
    private int toolCallCount;

    public string SessionId { get; }

    public SessionState(string sessionId)
    {
        SessionId = sessionId;
    }

    public AgentStatus Status
    {
        get => status;
        set => SetField(ref status, value);
    }

    public string? CurrentTool
    {
        get => currentTool;
        set => SetField(ref currentTool, value);
    }

    public string? Cwd
    {
        get => cwd;
        set => SetField(ref cwd, value);
    }

    public string? Source
    {
        get => source;
        set => SetField(ref source, value);
    }

    public string? LastEvent
    {
        get => lastEvent;
        set => SetField(ref lastEvent, value);
    }

    public DateTimeOffset LastActivity
    {
        get => lastActivity;
        set => SetField(ref lastActivity, value);
    }

    public int ToolCallCount
    {
        get => toolCallCount;
        set => SetField(ref toolCallCount, value);
    }

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Cwd))
            {
                return Path.GetFileName(Cwd.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }

            return SessionId.Length <= 10 ? SessionId : SessionId[..10];
        }
    }

    public string StatusText => Status switch
    {
        AgentStatus.Processing => "Processing",
        AgentStatus.Running => "Running",
        AgentStatus.WaitingApproval => "Approval",
        AgentStatus.WaitingQuestion => "Question",
        _ => "Idle"
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshDerived()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(StatusText));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(name);
        if (name is nameof(Status))
        {
            OnPropertyChanged(nameof(StatusText));
        }
        if (name is nameof(Cwd))
        {
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
