using System.Text.Json;

namespace CodeIsland.Desktop;

public sealed class WindowsSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".codeisland",
        "windows-settings.json");

    public bool AutoCollapse { get; set; } = true;
    public bool ShowWhenIdle { get; set; }
    public bool SoundEnabled { get; set; }
    public bool LaunchAtLogin { get; set; }
    public int AutoCollapseSeconds { get; set; } = 10;
    public int MaxVisibleSessions { get; set; } = 5;
    public string DefaultSource { get; set; } = "codex";
    public bool ExpandOnActivity { get; set; }
    public bool ExpandOnlyForBlocking { get; set; } = true;
    public bool SmartSuppress { get; set; } = true;
    public bool AutoExpandOnCompletion { get; set; } = true;
    public bool ExpandOnHover { get; set; } = true;
    public bool CollapseOnMouseLeave { get; set; } = true;
    public bool ShowToolStatus { get; set; } = true;
    public int CollapsedWidthScale { get; set; } = 100;
    public string PanelPlacement { get; set; } = "BottomRight";
    public bool SourceCodexEnabled { get; set; } = true;
    public bool SourceClaudeEnabled { get; set; } = true;
    public bool SourceGeminiEnabled { get; set; } = true;
    public bool SourceCursorEnabled { get; set; } = true;
    public bool SourceTraeEnabled { get; set; } = true;
    public bool SourceQwenEnabled { get; set; } = true;
    public bool SourceQoderEnabled { get; set; } = true;
    public bool SourceOpenCodeEnabled { get; set; } = true;
    public bool SourcePiEnabled { get; set; } = true;
    public bool SourceOmpEnabled { get; set; } = true;
    public bool SourceKimiEnabled { get; set; } = true;
    public bool SourceClineEnabled { get; set; } = true;

    public bool IsSourceEnabled(string source) => source.Trim().ToLowerInvariant() switch
    {
        "codex" => SourceCodexEnabled,
        "claude" => SourceClaudeEnabled,
        "gemini" or "google-antigravity" => SourceGeminiEnabled,
        "cursor" => SourceCursorEnabled,
        "trae" or "traecn" => SourceTraeEnabled,
        "qwen" => SourceQwenEnabled,
        "qoder" => SourceQoderEnabled,
        "opencode" => SourceOpenCodeEnabled,
        "pi" => SourcePiEnabled,
        "omp" => SourceOmpEnabled,
        "kimi" => SourceKimiEnabled,
        "cline" => SourceClineEnabled,
        _ => true
    };

    public static WindowsSettings Current { get; private set; } = Load();
    public static event EventHandler? Changed;

    public static WindowsSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                return JsonSerializer.Deserialize<WindowsSettings>(File.ReadAllText(SettingsPath)) ?? new WindowsSettings();
            }
        }
        catch
        {
        }
        return new WindowsSettings();
    }

    public static void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        Changed?.Invoke(null, EventArgs.Empty);
    }
}
