namespace CodeIsland.Desktop;

public partial class MainWindow : System.Windows.Window
{
    private readonly NamedPipeHookServer hookServer;
    private readonly System.Windows.Threading.DispatcherTimer collapseTimer = new();
    private readonly System.Windows.Threading.DispatcherTimer hoverExpandTimer = new();
    private readonly System.Windows.Threading.DispatcherTimer hoverLeaveTimer = new();
    private readonly System.Windows.Threading.DispatcherTimer completionPreviewTimer = new();
    private readonly GlobalHotKeyManager hotKeyManager = new();
    private SettingsWindow? settingsWindow;
    private bool allowClose;
    private bool completionPreviewActive;
    private string currentVisualStatus = "Idle";
    public SessionStore Store { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = Store;
        Store.EventReceived += (_, hookEvent) => Dispatcher.Invoke(() =>
        {
            var normalized = EventNormalizer.Normalize(hookEvent.EventName);
            UpdateSummary(hookEvent, normalized);
            UpdateStats();
            ApplyStatusVisual(normalized);
            ShowForEvent(normalized);
        });
        Store.BlockingRequestChanged += (_, _) => Dispatcher.Invoke(UpdateBlockingPanel);
        Store.SessionsChanged += (_, _) => Dispatcher.Invoke(UpdateStats);
        Store.CompletionCardsChanged += (_, _) => Dispatcher.Invoke(UpdateCompletionPanel);
        hookServer = new NamedPipeHookServer(Store, Dispatcher);
        hookServer.Start();
        Store.RestoreSavedSessions();
        collapseTimer.Interval = TimeSpan.FromSeconds(Math.Max(3, WindowsSettings.Current.AutoCollapseSeconds));
        collapseTimer.Tick += (_, _) => CollapseToCompact();
        hoverExpandTimer.Interval = TimeSpan.FromMilliseconds(260);
        hoverExpandTimer.Tick += (_, _) =>
        {
            hoverExpandTimer.Stop();
            if (WindowsSettings.Current.ExpandOnHover && !Store.HasBlockingRequest) ExpandPanelTemporarily();
        };
        hoverLeaveTimer.Interval = TimeSpan.FromMilliseconds(320);
        hoverLeaveTimer.Tick += (_, _) =>
        {
            hoverLeaveTimer.Stop();
            if (WindowsSettings.Current.CollapseOnMouseLeave && !Store.HasBlockingRequest) CollapseToCompact();
        };
        completionPreviewTimer.Interval = TimeSpan.FromSeconds(7);
        completionPreviewTimer.Tick += (_, _) =>
        {
            completionPreviewTimer.Stop();
            if (completionPreviewActive && !IsMouseOver && !Store.HasBlockingRequest) CollapseToCompact();
        };
        WindowsSettings.Changed += (_, _) => Dispatcher.Invoke(ApplySettings);
        Loaded += (_, _) =>
        {
            hotKeyManager.Attach(this);
            hotKeyManager.RegisterDefaultShortcuts(this);
            UpdateStats();
            ApplyStatusVisual(Store.PrimarySession?.StatusText ?? "Idle");
            ShowCompact();
        };
    }

    public async Task InstallHooksAsync()
    {
        try
        {
            TitleText.Text = "Hooks installed successfully"; StatusTagText.Text = "Hooks";
            SubtitleText.Text = await HookInstaller.InstallAsync();
            ExpandPanelTemporarily();
        }
        catch (Exception ex)
        {
            TitleText.Text = "Installation failed"; StatusTagText.Text = "Error";
            SubtitleText.Text = ex.Message;
            ExpandPanelTemporarily();
        }
    }

    private async void InstallHooks_Click(object sender, System.Windows.RoutedEventArgs e) => await InstallHooksAsync();
    private void Settings_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        settingsWindow ??= new SettingsWindow { Owner = this, Store = Store };
        settingsWindow.Store = Store;
        settingsWindow.Show();
        settingsWindow.Activate();
    }
    private void Hide_Click(object sender, System.Windows.RoutedEventArgs e) => Hide();
    private void Approve_Click(object sender, System.Windows.RoutedEventArgs e) => Store.ApprovePermission();
    private void Always_Click(object sender, System.Windows.RoutedEventArgs e) => Store.ApprovePermission(always: true);
    private void Deny_Click(object sender, System.Windows.RoutedEventArgs e) => Store.DenyPermission();
    private void Answer_Click(object sender, System.Windows.RoutedEventArgs e) => Store.AnswerQuestion(QuestionAnswerBox.Text);
    private void QuestionAnswerBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter) return;
        Store.AnswerQuestion(QuestionAnswerBox.Text);
        e.Handled = true;
    }
    private void QuestionOption_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Content: string answer }) Store.AnswerQuestion(answer);
    }

    private void CompletionJump_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not System.Windows.FrameworkElement { DataContext: CompletionCardState card }) return;
        var session = Store.Sessions.FirstOrDefault(s => s.SessionId == card.SessionId);
        if (session is not null) JumpToSession(session);
    }

    private void DismissCompletion_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement { DataContext: CompletionCardState card }) Store.DismissCompletion(card);
    }

    private void ClearCompletions_Click(object sender, System.Windows.RoutedEventArgs e) => Store.ClearCompletions();

    private void SessionItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement { DataContext: SessionState session })
        {
            JumpToSession(session);
        }
    }

    private static void JumpToSession(SessionState session)
    {
        WindowsTerminalActivator.Activate(session);
    }

    public void TogglePanel()
    {
        completionPreviewActive = false;
        if (ExpandedPanel.Visibility == System.Windows.Visibility.Visible) CollapseToCompact();
        else ExpandPanelTemporarily();
    }

    public void ShowExpanded() => ExpandPanelTemporarily();

    public void JumpToPrimarySession()
    {
        if (Store.PrimarySession is { } session) JumpToSession(session);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        Store.SaveSessions();
        if (!allowClose)
        {
            e.Cancel = true;
            Hide();
        }
    }

    public void Shutdown()
    {
        allowClose = true;
        Store.SaveSessions();
        hotKeyManager.Dispose();
        Close();
    }

    private static string MapStatus(string normalized) => normalized switch
    {
        "PermissionRequest" => "Waiting Approval",
        "Notification" => "Processing",
        "PreToolUse" => "Running",
        "PostToolUse" => "Processing",
        "SessionStart" => "Processing",
        "SessionEnd" => "Idle",
        "Stop" => "Idle",
        _ => normalized
    };

    private void UpdateSummary(HookEvent hookEvent, string normalized)
    {
        var source = string.IsNullOrWhiteSpace(hookEvent.Source) ? "Agent" : hookEvent.Source;
        var status = MapStatus(normalized);
        SourceTagText.Text = source;
        StatusTagText.Text = status;
        TitleText.Text = source;
        SubtitleText.Text = hookEvent.ToolDescription ?? hookEvent.ToolName ?? hookEvent.Cwd ?? hookEvent.SessionId;
        var sourceKey = SessionState.NormalizeSource(source);
        MascotImage.Source = LoadImage(SourceMascotPath(sourceKey));
        SourceIconImage.Source = LoadImage($"cli-icons/{sourceKey}.png");
    }

    private void ApplyStatusVisual(string statusOrEvent)
    {
        currentVisualStatus = statusOrEvent;
        var status = statusOrEvent switch
        {
            "PermissionRequest" or "Waiting Approval" or "Waiting" => AgentStatus.WaitingApproval,
            "Question" or "WaitingQuestion" => AgentStatus.WaitingQuestion,
            "PreToolUse" or "Running" => AgentStatus.Running,
            "Notification" or "PostToolUse" or "SessionStart" or "Processing" => AgentStatus.Processing,
            _ => AgentStatus.Idle
        };
        var accent = (System.Windows.Media.SolidColorBrush)new StatusAccentConverter().Convert(status, typeof(System.Windows.Media.Brush), null!, System.Globalization.CultureInfo.InvariantCulture);
        StatusRailFill.Background = accent;
        MascotShell.BorderBrush = accent;
        StatusChip.Background = new System.Windows.Media.SolidColorBrush(status switch
        {
            AgentStatus.Running => System.Windows.Media.Color.FromRgb(11, 39, 25),
            AgentStatus.Processing => System.Windows.Media.Color.FromRgb(18, 27, 48),
            AgentStatus.WaitingApproval => System.Windows.Media.Color.FromRgb(53, 36, 18),
            AgentStatus.WaitingQuestion => System.Windows.Media.Color.FromRgb(38, 28, 55),
            _ => System.Windows.Media.Color.FromRgb(18, 24, 36)
        });
        StatusTagText.Foreground = accent;
        CompactToolText.Foreground = accent;

        var urgent = status is AgentStatus.WaitingApproval or AgentStatus.WaitingQuestion;
        var active = status is AgentStatus.Running or AgentStatus.Processing;
        StatusRailFill.Width = urgent ? 150 : active ? 118 : 72;
        StatusRail.Opacity = urgent ? 1.0 : active ? 0.86 : 0.52;
        StartStatusPulse(urgent, active);
    }

    private void StartStatusPulse(bool urgent, bool active)
    {
        StatusRailFill.BeginAnimation(OpacityProperty, null);
        MascotShell.BeginAnimation(OpacityProperty, null);
        if (!urgent && !active)
        {
            StatusRailFill.Opacity = 0.7;
            MascotShell.Opacity = 1;
            return;
        }

        var duration = urgent ? 640 : 1150;
        var minOpacity = urgent ? 0.42 : 0.58;
        var railPulse = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 1,
            To = minOpacity,
            AutoReverse = true,
            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
            Duration = TimeSpan.FromMilliseconds(duration),
            EasingFunction = new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut }
        };
        StatusRailFill.BeginAnimation(OpacityProperty, railPulse);

        if (urgent)
        {
            var mascotPulse = railPulse.Clone();
            mascotPulse.To = 0.72;
            MascotShell.BeginAnimation(OpacityProperty, mascotPulse);
        }
        else
        {
            MascotShell.Opacity = 1;
        }
    }

    private static System.Windows.Media.ImageSource LoadImage(string relativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var uri = File.Exists(path) ? new Uri(path, UriKind.Absolute) : new Uri(relativePath, UriKind.Relative);
        var image = new System.Windows.Media.Imaging.BitmapImage();
        image.BeginInit();
        image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        image.UriSource = uri;
        image.EndInit();
        return image;
    }

    private static string SourceMascotPath(string sourceKey)
    {
        return File.Exists(Path.Combine(AppContext.BaseDirectory, "mascots", sourceKey + ".gif"))
            ? $"mascots/{sourceKey}.gif"
            : sourceKey == "claude" ? "mascot-claude.gif" : "mascot-codex.gif";
    }

    private void UpdateBlockingPanel()
    {
        if (Store.PendingPermission is { } permission)
        {
            BlockingPanel.Visibility = System.Windows.Visibility.Visible;
            SessionsList.Visibility = System.Windows.Visibility.Visible;
            BlockingTitle.Text = $"Permission: {permission.ToolName}";
            BlockingSubtitle.Text = permission.SessionId;
            BlockingGlyph.Text = "!";
            BlockingQueueText.Text = Store.PendingPermission is null ? "" : "1/1";
            BlockingDetail.Text = permission.Detail ?? permission.SessionId;
            QuestionAnswerBox.Visibility = System.Windows.Visibility.Collapsed;
            SetInteractiveMode(false);
            QuestionOptionsList.Visibility = System.Windows.Visibility.Collapsed;
            AnswerButton.Visibility = System.Windows.Visibility.Collapsed;
            ApproveButton.Visibility = System.Windows.Visibility.Visible;
            AlwaysButton.Visibility = System.Windows.Visibility.Visible;
            DenyButton.Visibility = System.Windows.Visibility.Visible;
            ExpandPanelTemporarily();
            ShowInteractiveBlockingPanel(focusQuestion: false);
            return;
        }
        if (Store.PendingQuestion is { } question)
        {
            BlockingPanel.Visibility = System.Windows.Visibility.Visible;
            SessionsList.Visibility = System.Windows.Visibility.Visible;
            BlockingTitle.Text = "Question";
            BlockingSubtitle.Text = question.SessionId;
            BlockingGlyph.Text = "?";
            BlockingQueueText.Text = Store.PendingQuestion is null ? "" : "1/1";
            BlockingDetail.Text = question.Options.Length == 0 ? question.Question : question.Question + Environment.NewLine + string.Join(Environment.NewLine, question.Options);
            QuestionOptionsList.ItemsSource = question.Options;
            QuestionOptionsList.Visibility = question.Options.Length == 0 ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
            QuestionAnswerBox.Visibility = System.Windows.Visibility.Visible;
            QuestionAnswerBox.Text = "";
            AnswerButton.Visibility = System.Windows.Visibility.Visible;
            ApproveButton.Visibility = System.Windows.Visibility.Collapsed;
            AlwaysButton.Visibility = System.Windows.Visibility.Collapsed;
            DenyButton.Visibility = System.Windows.Visibility.Collapsed;
            ExpandPanelTemporarily();
            ShowInteractiveBlockingPanel(focusQuestion: true);
            return;
        }
        BlockingPanel.Visibility = System.Windows.Visibility.Collapsed;
        SetInteractiveMode(false);
        CollapseToCompact();
    }

    private void ShowNoActivate()
    {
        Show();
    }

    private void ShowInteractiveBlockingPanel(bool focusQuestion)
    {
        SetInteractiveMode(true);
        Show();
        Activate();
        if (focusQuestion)
        {
            QuestionAnswerBox.Focus();
            System.Windows.Input.Keyboard.Focus(QuestionAnswerBox);
        }
        else
        {
            ApproveButton.Focus();
            System.Windows.Input.Keyboard.Focus(ApproveButton);
        }
    }

    private void SetInteractiveMode(bool interactive)
    {
        Focusable = interactive;
        Topmost = true;
    }

    private void ShowForEvent(string normalized)
    {
        if (WindowsSettings.Current.SmartSuppress && Store.PrimarySession is { } primary && WindowsTerminalActivator.IsSessionVisible(primary))
        {
            ShowCompact();
            return;
        }

        if (normalized is "PermissionRequest" || Store.HasBlockingRequest)
        {
            ExpandPanelTemporarily();
            return;
        }

        if (normalized is "Stop" or "SessionEnd" or "TaskRoundComplete" && WindowsSettings.Current.AutoExpandOnCompletion)
        {
            ShowCompletionTemporarily();
            return;
        }

        if (WindowsSettings.Current.ExpandOnActivity && !WindowsSettings.Current.ExpandOnlyForBlocking)
        {
            ExpandPanelTemporarily();
            return;
        }

        ShowCompact();
    }

    private void ExpandPanelTemporarily()
    {
        completionPreviewActive = false;
        completionPreviewTimer.Stop();
        SetInteractiveMode(Store.HasBlockingRequest);
        AnimateWidth(620);
        HeaderButtons.Visibility = System.Windows.Visibility.Visible;
        StatsChip.Visibility = System.Windows.Visibility.Visible;
        CompactTextStack.Visibility = System.Windows.Visibility.Visible;
        CompactToolText.Visibility = System.Windows.Visibility.Collapsed;
        ExpandedPanel.Visibility = System.Windows.Visibility.Visible;
        SessionsList.Visibility = System.Windows.Visibility.Visible;
        UpdateCompletionPanel();
        EmptyState.Visibility = Store.Sessions.Count == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        ApplyChromeForPlacement(expanded: true);
        PlacePanel();
        ShowNoActivate();
        collapseTimer.Stop();
        if (WindowsSettings.Current.AutoCollapse && BlockingPanel.Visibility != System.Windows.Visibility.Visible)
        {
            collapseTimer.Start();
        }
    }

    private void ShowCompact()
    {
        completionPreviewActive = false;
        completionPreviewTimer.Stop();
        SetInteractiveMode(false);
        var scale = Math.Clamp(WindowsSettings.Current.CollapsedWidthScale, 50, 150) / 100.0;
        var targetWidth = WindowsSettings.Current.ShowToolStatus ? 340 * scale : 220 * scale;
        AnimateWidth(targetWidth);
        HeaderButtons.Visibility = System.Windows.Visibility.Collapsed;
        StatsChip.Visibility = System.Windows.Visibility.Collapsed;
        ExpandedPanel.Visibility = System.Windows.Visibility.Collapsed;
        BlockingPanel.Visibility = System.Windows.Visibility.Collapsed;
        CompletionPanel.Visibility = System.Windows.Visibility.Collapsed;
        CompletionPanel.Opacity = 0;
        SessionsList.Visibility = System.Windows.Visibility.Collapsed;
        EmptyState.Visibility = System.Windows.Visibility.Collapsed;
        CompactTextStack.Visibility = WindowsSettings.Current.ShowToolStatus ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        CompactToolText.Visibility = WindowsSettings.Current.ShowToolStatus ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        ApplyChromeForPlacement(expanded: false);
        PlacePanel();
        if (WindowsSettings.Current.ShowWhenIdle || Store.Sessions.Count > 0) ShowNoActivate(); else Hide();
    }

    private void CollapseToCompact()
    {
        collapseTimer.Stop();
        ShowCompact();
    }

    private void ShowCompletionTemporarily()
    {
        completionPreviewActive = true;
        completionPreviewTimer.Interval = TimeSpan.FromSeconds(7);
        SetInteractiveMode(false);
        AnimateWidth(460);
        HeaderButtons.Visibility = System.Windows.Visibility.Collapsed;
        StatsChip.Visibility = System.Windows.Visibility.Collapsed;
        CompactTextStack.Visibility = System.Windows.Visibility.Visible;
        CompactToolText.Visibility = System.Windows.Visibility.Collapsed;
        BlockingPanel.Visibility = System.Windows.Visibility.Collapsed;
        ExpandedPanel.Visibility = System.Windows.Visibility.Visible;
        CompletionPanel.Visibility = Store.HasCompletionCards ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        SessionsList.Visibility = System.Windows.Visibility.Collapsed;
        EmptyState.Visibility = System.Windows.Visibility.Collapsed;
        ApplyChromeForPlacement(expanded: true);
        PlacePanel();
        ShowNoActivate();
        AnimatePanelOpacity(CompletionPanel, CompletionPanel.Visibility == System.Windows.Visibility.Visible ? 1 : 0);
        collapseTimer.Stop();
        completionPreviewTimer.Stop();
        completionPreviewTimer.Start();
    }

    private void UpdateStats()
    {
        StatsText.Text = $"{Store.Sessions.Count} sessions / {Store.TotalToolCallCount} tools";
        EmptyState.Visibility = Store.Sessions.Count == 0 && !Store.HasCompletionCards && ExpandedPanel.Visibility == System.Windows.Visibility.Visible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        if (Store.PrimarySession is { } primary)
        {
            TitleText.Text = primary.SourceDisplay;
            SubtitleText.Text = primary.ActivityLine;
            CompactToolText.Text = primary.CompactToolText;
            SourceTagText.Text = primary.SourceDisplay;
            StatusTagText.Text = primary.StatusText;
            MascotImage.Source = LoadImage(primary.MascotPath);
            SourceIconImage.Source = LoadImage(primary.SourceIconPath);
            ApplyStatusVisual(primary.StatusText);
        }
    }

    private void UpdateCompletionPanel()
    {
        CompletionPanel.Visibility = Store.HasCompletionCards && ExpandedPanel.Visibility == System.Windows.Visibility.Visible
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;
        if (CompletionPanel.Visibility == System.Windows.Visibility.Visible && CompletionPanel.Opacity < 1) AnimatePanelOpacity(CompletionPanel, 1);
        EmptyState.Visibility = Store.Sessions.Count == 0 && !Store.HasCompletionCards && ExpandedPanel.Visibility == System.Windows.Visibility.Visible
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;
    }

    private void ApplySettings()
    {
        collapseTimer.Interval = TimeSpan.FromSeconds(Math.Max(3, WindowsSettings.Current.AutoCollapseSeconds));
        UpdateStats();
        ApplyStatusVisual(currentVisualStatus);
        ApplyChromeForPlacement(ExpandedPanel.Visibility == System.Windows.Visibility.Visible);
        PlacePanel();
    }

    private void ApplyChromeForPlacement(bool expanded)
    {
        var topDocked = string.Equals(WindowsSettings.Current.PanelPlacement, "TopCenter", StringComparison.OrdinalIgnoreCase);
        if (topDocked)
        {
            IslandRoot.CornerRadius = expanded ? new System.Windows.CornerRadius(0, 0, 22, 22) : new System.Windows.CornerRadius(0, 0, 18, 18);
            CompactBar.CornerRadius = expanded ? new System.Windows.CornerRadius(0) : new System.Windows.CornerRadius(0, 0, 18, 18);
            return;
        }

        IslandRoot.CornerRadius = expanded ? new System.Windows.CornerRadius(18) : new System.Windows.CornerRadius(18);
        CompactBar.CornerRadius = expanded ? new System.Windows.CornerRadius(18, 18, 0, 0) : new System.Windows.CornerRadius(18);
    }

    private void PlacePanel()
    {
        var area = System.Windows.SystemParameters.WorkArea;
        var width = ActualWidth > 1 ? ActualWidth : Width;
        var height = ActualHeight > 1 ? ActualHeight : Height;
        const double margin = 18;

        switch (WindowsSettings.Current.PanelPlacement?.Trim().ToLowerInvariant())
        {
            case "topcenter":
                Left = area.Left + (area.Width - width) / 2;
                Top = area.Top;
                break;
            case "bottomcenter":
                Left = area.Left + (area.Width - width) / 2;
                Top = area.Bottom - height - margin;
                break;
            case "bottomleft":
                Left = area.Left + margin;
                Top = area.Bottom - height - margin;
                break;
            case "bottomright":
            default:
                Left = area.Right - width - margin;
                Top = area.Bottom - height - margin;
                break;
        }
    }

    protected override void OnRenderSizeChanged(System.Windows.SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        PlacePanel();
    }

    private void AnimateWidth(double targetWidth)
    {
        if (Math.Abs(Width - targetWidth) < 0.5) return;
        BeginAnimation(WidthProperty, new System.Windows.Media.Animation.DoubleAnimation
        {
            To = targetWidth,
            Duration = TimeSpan.FromMilliseconds(260),
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        });
    }

    private static void AnimatePanelOpacity(System.Windows.UIElement element, double targetOpacity)
    {
        element.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation
        {
            To = targetOpacity,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        });
    }
}

public partial class MainWindow
{
    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            ExpandPanelTemporarily();
            return;
        }
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove();
    }

    private void IslandRoot_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        hoverLeaveTimer.Stop();
        if (completionPreviewActive)
        {
            completionPreviewTimer.Stop();
            return;
        }
        if (ExpandedPanel.Visibility == System.Windows.Visibility.Visible) return;
        hoverExpandTimer.Stop();
        hoverExpandTimer.Start();
    }

    private void IslandRoot_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        hoverExpandTimer.Stop();
        if (Store.HasBlockingRequest) return;
        if (completionPreviewActive)
        {
            completionPreviewTimer.Stop();
            completionPreviewTimer.Interval = TimeSpan.FromMilliseconds(1200);
            completionPreviewTimer.Start();
            return;
        }
        hoverLeaveTimer.Stop();
        hoverLeaveTimer.Start();
    }
}
