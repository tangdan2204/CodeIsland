namespace CodeIsland.Desktop;

public partial class SettingsWindow : System.Windows.Window
{
    private readonly Dictionary<string, System.Windows.FrameworkElement> pages;
    public SessionStore? Store { get; set; }

    public SettingsWindow()
    {
        InitializeComponent();
        pages = new Dictionary<string, System.Windows.FrameworkElement>
        {
            ["General"] = GeneralPage,
            ["Behavior"] = BehaviorPage,
            ["Appearance"] = AppearancePage,
            ["Shortcuts"] = ShortcutsPage,
            ["CLIs"] = ClisPage,
            ["Mascots"] = MascotsPage,
            ["Sound"] = SoundPage,
            ["Hooks"] = HooksPage,
            ["About"] = AboutPage,
        };
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = WindowsSettings.Current;
        ShowWhenIdleCheck.IsChecked = settings.ShowWhenIdle;
        LaunchAtLoginCheck.IsChecked = settings.LaunchAtLogin || StartupManager.IsLaunchAtLoginEnabled();
        AutoCollapseCheck.IsChecked = settings.AutoCollapse;
        CollapseOnMouseLeaveCheck.IsChecked = settings.CollapseOnMouseLeave;
        ExpandOnHoverCheck.IsChecked = settings.ExpandOnHover;
        SmartSuppressCheck.IsChecked = settings.SmartSuppress;
        AutoExpandCompletionCheck.IsChecked = settings.AutoExpandOnCompletion;
        ExpandOnActivityCheck.IsChecked = settings.ExpandOnActivity;
        ExpandOnlyBlockingCheck.IsChecked = settings.ExpandOnlyForBlocking;
        SoundEnabledCheck.IsChecked = settings.SoundEnabled;
        ShowToolStatusCheck.IsChecked = settings.ShowToolStatus;
        CliCodexCheck.IsChecked = settings.SourceCodexEnabled;
        CliClaudeCheck.IsChecked = settings.SourceClaudeEnabled;
        CliGeminiCheck.IsChecked = settings.SourceGeminiEnabled;
        CliCursorCheck.IsChecked = settings.SourceCursorEnabled;
        CliTraeCheck.IsChecked = settings.SourceTraeEnabled;
        CliQwenCheck.IsChecked = settings.SourceQwenEnabled;
        CliQoderCheck.IsChecked = settings.SourceQoderEnabled;
        CliOpenCodeCheck.IsChecked = settings.SourceOpenCodeEnabled;
        CliPiCheck.IsChecked = settings.SourcePiEnabled;
        CliOmpCheck.IsChecked = settings.SourceOmpEnabled;
        CliKimiCheck.IsChecked = settings.SourceKimiEnabled;
        CliClineCheck.IsChecked = settings.SourceClineEnabled;
        foreach (System.Windows.Controls.ComboBoxItem item in PanelPlacementCombo.Items)
        {
            if (string.Equals(item.Tag?.ToString(), settings.PanelPlacement, StringComparison.OrdinalIgnoreCase))
            {
                PanelPlacementCombo.SelectedItem = item;
                break;
            }
        }
        PanelPlacementCombo.SelectedIndex = PanelPlacementCombo.SelectedIndex < 0 ? 0 : PanelPlacementCombo.SelectedIndex;
        AutoCollapseSecondsBox.Text = settings.AutoCollapseSeconds.ToString();
        MaxVisibleSessionsBox.Text = settings.MaxVisibleSessions.ToString();
        CollapsedWidthScaleBox.Text = settings.CollapsedWidthScale.ToString();
        foreach (System.Windows.Controls.ComboBoxItem item in DefaultSourceCombo.Items)
        {
            if (string.Equals(item.Content?.ToString(), settings.DefaultSource, StringComparison.OrdinalIgnoreCase))
            {
                DefaultSourceCombo.SelectedItem = item;
                break;
            }
        }
        DefaultSourceCombo.SelectedIndex = DefaultSourceCombo.SelectedIndex < 0 ? 0 : DefaultSourceCombo.SelectedIndex;
    }

    private void Save_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var settings = WindowsSettings.Current;
        settings.ShowWhenIdle = ShowWhenIdleCheck.IsChecked == true;
        settings.LaunchAtLogin = LaunchAtLoginCheck.IsChecked == true;
        StartupManager.SetLaunchAtLogin(settings.LaunchAtLogin);
        settings.AutoCollapse = AutoCollapseCheck.IsChecked == true;
        settings.CollapseOnMouseLeave = CollapseOnMouseLeaveCheck.IsChecked == true;
        settings.ExpandOnHover = ExpandOnHoverCheck.IsChecked == true;
        settings.SmartSuppress = SmartSuppressCheck.IsChecked == true;
        settings.AutoExpandOnCompletion = AutoExpandCompletionCheck.IsChecked == true;
        settings.ExpandOnActivity = ExpandOnActivityCheck.IsChecked == true;
        settings.ExpandOnlyForBlocking = ExpandOnlyBlockingCheck.IsChecked == true;
        settings.SoundEnabled = SoundEnabledCheck.IsChecked == true;
        settings.ShowToolStatus = ShowToolStatusCheck.IsChecked == true;
        settings.SourceCodexEnabled = CliCodexCheck.IsChecked == true;
        settings.SourceClaudeEnabled = CliClaudeCheck.IsChecked == true;
        settings.SourceGeminiEnabled = CliGeminiCheck.IsChecked == true;
        settings.SourceCursorEnabled = CliCursorCheck.IsChecked == true;
        settings.SourceTraeEnabled = CliTraeCheck.IsChecked == true;
        settings.SourceQwenEnabled = CliQwenCheck.IsChecked == true;
        settings.SourceQoderEnabled = CliQoderCheck.IsChecked == true;
        settings.SourceOpenCodeEnabled = CliOpenCodeCheck.IsChecked == true;
        settings.SourcePiEnabled = CliPiCheck.IsChecked == true;
        settings.SourceOmpEnabled = CliOmpCheck.IsChecked == true;
        settings.SourceKimiEnabled = CliKimiCheck.IsChecked == true;
        settings.SourceClineEnabled = CliClineCheck.IsChecked == true;
        if (PanelPlacementCombo.SelectedItem is System.Windows.Controls.ComboBoxItem placementItem)
        {
            settings.PanelPlacement = placementItem.Tag?.ToString() ?? "BottomRight";
        }
        settings.AutoCollapseSeconds = Clamp(ParseInt(AutoCollapseSecondsBox.Text, settings.AutoCollapseSeconds), 3, 120);
        settings.MaxVisibleSessions = Clamp(ParseInt(MaxVisibleSessionsBox.Text, settings.MaxVisibleSessions), 1, 20);
        settings.CollapsedWidthScale = Clamp(ParseInt(CollapsedWidthScaleBox.Text, settings.CollapsedWidthScale), 50, 150);
        if (DefaultSourceCombo.SelectedItem is System.Windows.Controls.ComboBoxItem item)
        {
            settings.DefaultSource = item.Content?.ToString() ?? "codex";
        }
        WindowsSettings.Save();
        SaveStatusText.Text = "Saved";
    }

    private async void InstallHooks_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            HookStatusBox.Text = await HookInstaller.InstallAsync();
        }
        catch (Exception ex)
        {
            HookStatusBox.Text = "Install failed: " + ex.Message;
        }
    }

    private async void HookStatus_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        HookStatusBox.Text = await HookInstaller.StatusAsync();
    }

    private async void UninstallHooks_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        HookStatusBox.Text = await HookInstaller.UninstallAsync();
    }

    private void ExportDiagnostics_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (Store is null)
        {
            DiagnosticsStatusText.Text = "No session store attached.";
            return;
        }
        try
        {
            var path = DiagnosticsExporter.Export(Store);
            DiagnosticsStatusText.Text = "Exported: " + path;
        }
        catch (Exception ex)
        {
            DiagnosticsStatusText.Text = "Export failed: " + ex.Message;
        }
    }

    private void PlaySound_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var before = WindowsSettings.Current.SoundEnabled;
        WindowsSettings.Current.SoundEnabled = true;
        SoundManager.PlayForEvent("PermissionRequest");
        WindowsSettings.Current.SoundEnabled = before;
    }

    private void Tabs_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (Tabs.SelectedItem is not System.Windows.Controls.ListBoxItem item) return;
        var key = item.Content?.ToString() ?? "General";
        foreach (var page in pages.Values) page.Visibility = System.Windows.Visibility.Collapsed;
        if (pages.TryGetValue(key, out var selected)) selected.Visibility = System.Windows.Visibility.Visible;
        PageTitle.Text = key;
        PageSubtitle.Text = key switch
        {
            "Behavior" => "Collapse and session display",
            "Appearance" => "Panel visual parity",
            "Shortcuts" => "Global keyboard controls",
            "CLIs" => "Agent hook coverage",
            "Mascots" => "Agent character assets",
            "Sound" => "8-bit event feedback",
            "Hooks" => "CLI hook installation and repair",
            "About" => "Version and scope",
            _ => "Startup and panel defaults"
        };
    }

    private static int ParseInt(string text, int fallback) => int.TryParse(text, out var value) ? value : fallback;
    private static int Clamp(int value, int min, int max) => Math.Min(max, Math.Max(min, value));

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
