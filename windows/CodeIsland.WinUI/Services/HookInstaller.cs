using System.Text.Json;
using System.Text.Json.Nodes;

namespace CodeIsland.WinUI.Services;

public static class HookInstaller
{
    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private static readonly string InstallDir = Path.Combine(Home, ".codeisland");
    private static readonly string BridgeExe = Path.Combine(InstallDir, "CodeIsland.Bridge.exe");

    public static string BridgeCommand(string source)
    {
        return $"\"{BridgeExe}\" --source {source}";
    }

    public static async Task<string> InstallAsync()
    {
        Directory.CreateDirectory(InstallDir);
        CopyBridge();

        var results = new List<string>
        {
            InstallClaudeLike("Claude", "claude", Path.Combine(Home, ".claude", "settings.json"), includePermission: true),
            InstallCodex(),
            InstallClaudeLike("Qwen", "qwen", Path.Combine(Home, ".qwen", "settings.json"), includePermission: true),
            InstallClaudeLike("Qoder", "qoder", Path.Combine(Home, ".qoder", "settings.json"), includePermission: true),
            InstallClaudeLike("CodeBuddy", "codebuddy", Path.Combine(Home, ".codebuddy", "settings.json"), includePermission: true),
            InstallNested("Gemini", "gemini", Path.Combine(Home, ".gemini", "settings.json"), new[] { "BeforeTool", "AfterTool", "BeforeAgent", "AfterAgent" }),
            InstallFlat("Cursor", "cursor", Path.Combine(Home, ".cursor", "hooks.json"))
        };

        await Task.CompletedTask;
        return string.Join(Environment.NewLine, results);
    }

    private static void CopyBridge()
    {
        var appDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(appDir, "CodeIsland.Bridge.exe"),
            Path.GetFullPath(Path.Combine(appDir, "..", "..", "..", "..", "..", "..", "CodeIsland.Bridge", "bin", "Release", "net8.0-windows", "win-x64", "publish", "CodeIsland.Bridge.exe")),
            Path.GetFullPath(Path.Combine(appDir, "..", "..", "..", "..", "..", "..", "CodeIsland.Bridge", "bin", "Release", "net8.0-windows", "CodeIsland.Bridge.exe")),
            Path.GetFullPath(Path.Combine(appDir, "..", "..", "..", "..", "..", "..", "CodeIsland.Bridge", "bin", "Release", "net8.0-windows", "win-x64", "publish", "CodeIsland.Bridge.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "CodeIsland.Bridge", "bin", "Debug", "net8.0-windows", "CodeIsland.Bridge.exe"))
        };
        var source = candidates.FirstOrDefault(File.Exists);
        if (source is null)
        {
            throw new FileNotFoundException("CodeIsland.Bridge.exe not found. Build the bridge project first.");
        }
        File.Copy(source, BridgeExe, overwrite: true);
    }

    private static string InstallClaudeLike(string name, string source, string settingsPath, bool includePermission)
    {
        if (!ShouldInstall(settingsPath, source)) return $"{name}: skipped";
        var root = ReadJsonObject(settingsPath);
        var hooks = root["hooks"] as JsonObject ?? new JsonObject();
        RemoveOurs(hooks);
        var cmd = BridgeCommand(source);
        var events = new List<string> { "UserPromptSubmit", "PreToolUse", "PostToolUse", "PostToolUseFailure", "Stop", "SessionStart", "SessionEnd", "SubagentStart", "SubagentStop", "Notification", "PreCompact" };
        if (includePermission) events.Add("PermissionRequest");
        foreach (var ev in events)
        {
            var timeout = ev is "PermissionRequest" or "Notification" ? 86400 : 60;
            hooks[ev] = new JsonArray(MakeNestedEntry(cmd, timeout, ev is "PreToolUse" or "PostToolUse" or "PermissionRequest" or "Notification" ? "*" : null));
        }
        root["hooks"] = hooks;
        WriteJson(settingsPath, root);
        return $"{name}: installed";
    }

    private static string InstallCodex()
    {
        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (string.IsNullOrWhiteSpace(codexHome)) codexHome = Path.Combine(Home, ".codex");
        var hooksPath = Path.Combine(codexHome, "hooks.json");
        if (!ShouldInstall(hooksPath, "codex")) return "Codex: skipped";
        var root = ReadJsonObject(hooksPath);
        var hooks = root["hooks"] as JsonObject ?? new JsonObject();
        RemoveOurs(hooks);
        var cmd = BridgeCommand("codex");
        foreach (var ev in new[] { "SessionStart", "SessionEnd", "UserPromptSubmit", "PreToolUse", "PostToolUse", "PermissionRequest", "Stop" })
        {
            hooks[ev] = new JsonArray(MakeNestedEntry(cmd, ev == "PermissionRequest" ? 86400 : 60));
        }
        root["hooks"] = hooks;
        WriteJson(hooksPath, root);
        EnsureCodexFeature(Path.Combine(codexHome, "config.toml"));
        return "Codex: installed";
    }

    private static string InstallNested(string name, string source, string settingsPath, IEnumerable<string> events)
    {
        if (!ShouldInstall(settingsPath, source)) return $"{name}: skipped";
        var root = ReadJsonObject(settingsPath);
        var hooks = root["hooks"] as JsonObject ?? new JsonObject();
        RemoveOurs(hooks);
        var cmd = BridgeCommand(source);
        foreach (var ev in events)
        {
            hooks[ev] = new JsonArray(MakeNestedEntry(cmd, 60));
        }
        root["hooks"] = hooks;
        WriteJson(settingsPath, root);
        return $"{name}: installed";
    }

    private static string InstallFlat(string name, string source, string settingsPath)
    {
        if (!ShouldInstall(settingsPath, source)) return $"{name}: skipped";
        var root = ReadJsonObject(settingsPath);
        var hooks = root["hooks"] as JsonObject ?? new JsonObject();
        RemoveOurs(hooks);
        var cmd = BridgeCommand(source);
        foreach (var ev in new[] { "beforeSubmitPrompt", "beforeShellExecution", "afterShellExecution", "beforeReadFile", "afterFileEdit", "beforeMCPExecution", "afterMCPExecution", "afterAgentThought", "afterAgentResponse", "stop" })
        {
            hooks[ev] = new JsonArray(new JsonObject { ["command"] = cmd });
        }
        root["hooks"] = hooks;
        WriteJson(settingsPath, root);
        return $"{name}: installed";
    }

    private static bool ShouldInstall(string configPath, string binaryName)
    {
        return File.Exists(configPath) || Directory.Exists(Path.GetDirectoryName(configPath) ?? "") || CommandExists(binaryName);
    }

    private static bool CommandExists(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        return path.Split(Path.PathSeparator).Any(dir => File.Exists(Path.Combine(dir, name + ".exe")) || File.Exists(Path.Combine(dir, name + ".cmd")) || File.Exists(Path.Combine(dir, name)));
    }

    private static JsonObject MakeNestedEntry(string cmd, int timeout, string? matcher = null)
    {
        var entry = new JsonObject
        {
            ["hooks"] = new JsonArray(new JsonObject { ["type"] = "command", ["command"] = cmd, ["timeout"] = timeout })
        };
        if (matcher is not null) entry["matcher"] = matcher;
        return entry;
    }

    private static JsonObject ReadJsonObject(string path)
    {
        try
        {
            if (File.Exists(path)) return JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? new JsonObject();
        }
        catch
        {
        }
        return new JsonObject();
    }

    private static void WriteJson(string path, JsonObject root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".codeisland.tmp";
        File.WriteAllText(tmp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Copy(tmp, path, overwrite: true);
        File.Delete(tmp);
    }

    private static void RemoveOurs(JsonObject hooks)
    {
        foreach (var key in hooks.Select(kv => kv.Key).ToList())
        {
            if (hooks[key] is not JsonArray entries) continue;
            var kept = new JsonArray();
            foreach (var entry in entries)
            {
                if (entry is null) continue;
                if (!entry.ToJsonString().Contains("CodeIsland.Bridge", StringComparison.OrdinalIgnoreCase) && !entry.ToJsonString().Contains("codeisland", StringComparison.OrdinalIgnoreCase))
                {
                    kept.Add(entry.DeepClone());
                }
            }
            if (kept.Count == 0) hooks.Remove(key); else hooks[key] = kept;
        }
    }

    private static void EnsureCodexFeature(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var content = File.Exists(path) ? File.ReadAllText(path) : "";
        if (content.Contains("hooks = true", StringComparison.OrdinalIgnoreCase)) return;
        if (content.Contains("[features]")) content = content.Replace("[features]", "[features]" + Environment.NewLine + "hooks = true");
        else content = content.TrimEnd() + Environment.NewLine + Environment.NewLine + "[features]" + Environment.NewLine + "hooks = true" + Environment.NewLine;
        File.WriteAllText(path, content);
    }
}