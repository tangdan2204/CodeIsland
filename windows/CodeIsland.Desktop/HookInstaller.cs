using System.Text.Json;
using System.Text.Json.Nodes;

namespace CodeIsland.Desktop;

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
            InstallClaudeLike("Factory", "droid", Path.Combine(Home, ".factory", "settings.json"), includePermission: true),
            InstallClaudeLike("CodyBuddyCN", "codybuddycn", Path.Combine(Home, ".codybuddycn", "settings.json"), includePermission: true),
            InstallClaudeLike("StepFun", "stepfun", Path.Combine(Home, ".stepfun", "settings.json"), includePermission: true),
            InstallClaudeLike("AntiGravity", "antigravity", Path.Combine(Home, ".antigravity", "settings.json"), includePermission: true),
            InstallClaudeLike("WorkBuddy", "workbuddy", Path.Combine(Home, ".workbuddy", "settings.json"), includePermission: true),
            InstallNested("Gemini", "gemini", Path.Combine(Home, ".gemini", "settings.json"), new[] { "BeforeTool", "AfterTool", "BeforeAgent", "AfterAgent" }),
            InstallNestedNamed("Google Antigravity", "google-antigravity", Path.Combine(Home, ".gemini", "config", "hooks.json"), "codeisland"),
            InstallFlat("Cursor", "cursor", Path.Combine(Home, ".cursor", "hooks.json")),
            InstallFlat("Trae", "trae", Path.Combine(Home, ".trae", "hooks.json")),
            InstallFlat("Trae CN", "traecn", Path.Combine(Home, ".trae-cn", "hooks.json")),
            InstallOpenCodePlugin(),
            InstallPiExtension("Pi", "pi", Path.Combine(Home, ".pi", "agent"), Path.Combine(Home, ".pi", "agent", "extensions", "codeisland.ts")),
            InstallPiExtension("OMP", "omp", Path.Combine(Home, ".omp", "agent"), Path.Combine(Home, ".omp", "agent", "extensions", "codeisland.ts")),
            InstallCopilot(),
            InstallKimi(),
            InstallCline()
        };

        await Task.CompletedTask;
        return string.Join(Environment.NewLine, results);
    }

    public static Task<string> UninstallAsync()
    {
        var results = new List<string>
        {
            UninstallJsonHooks("Claude", Path.Combine(Home, ".claude", "settings.json"), "hooks"),
            UninstallJsonHooks("Codex", Path.Combine(Environment.GetEnvironmentVariable("CODEX_HOME") ?? Path.Combine(Home, ".codex"), "hooks.json"), "hooks"),
            UninstallJsonHooks("Qwen", Path.Combine(Home, ".qwen", "settings.json"), "hooks"),
            UninstallJsonHooks("Qoder", Path.Combine(Home, ".qoder", "settings.json"), "hooks"),
            UninstallJsonHooks("CodeBuddy", Path.Combine(Home, ".codebuddy", "settings.json"), "hooks"),
            UninstallJsonHooks("Factory", Path.Combine(Home, ".factory", "settings.json"), "hooks"),
            UninstallJsonHooks("CodyBuddyCN", Path.Combine(Home, ".codybuddycn", "settings.json"), "hooks"),
            UninstallJsonHooks("StepFun", Path.Combine(Home, ".stepfun", "settings.json"), "hooks"),
            UninstallJsonHooks("AntiGravity", Path.Combine(Home, ".antigravity", "settings.json"), "hooks"),
            UninstallJsonHooks("WorkBuddy", Path.Combine(Home, ".workbuddy", "settings.json"), "hooks"),
            UninstallJsonHooks("Gemini", Path.Combine(Home, ".gemini", "settings.json"), "hooks"),
            UninstallJsonHooks("Google Antigravity", Path.Combine(Home, ".gemini", "config", "hooks.json"), "codeisland"),
            UninstallJsonHooks("Cursor", Path.Combine(Home, ".cursor", "hooks.json"), "hooks"),
            UninstallJsonHooks("Trae", Path.Combine(Home, ".trae", "hooks.json"), "hooks"),
            UninstallJsonHooks("Trae CN", Path.Combine(Home, ".trae-cn", "hooks.json"), "hooks"),
            UninstallOpenCodePlugin(),
            UninstallOwnedFile("Pi", Path.Combine(Home, ".pi", "agent", "extensions", "codeisland.ts"), "CodeIsland pi extension"),
            UninstallOwnedFile("OMP", Path.Combine(Home, ".omp", "agent", "extensions", "codeisland.ts"), "CodeIsland Integration Extension"),
            UninstallJsonHooks("Copilot", Path.Combine(Home, ".copilot", "hooks", "codeisland.json"), "hooks"),
            UninstallKimi(),
            UninstallCline()
        };
        return Task.FromResult(string.Join(Environment.NewLine, results));
    }

    public static Task<string> StatusAsync()
    {
        var results = new List<string>
        {
            HookStatus("Claude", Path.Combine(Home, ".claude", "settings.json"), "hooks"),
            HookStatus("Codex", Path.Combine(Environment.GetEnvironmentVariable("CODEX_HOME") ?? Path.Combine(Home, ".codex"), "hooks.json"), "hooks"),
            HookStatus("Gemini", Path.Combine(Home, ".gemini", "settings.json"), "hooks"),
            HookStatus("Cursor", Path.Combine(Home, ".cursor", "hooks.json"), "hooks"),
            HookStatus("Qwen", Path.Combine(Home, ".qwen", "settings.json"), "hooks"),
            HookStatus("Qoder", Path.Combine(Home, ".qoder", "settings.json"), "hooks"),
            HookStatus("CodeBuddy", Path.Combine(Home, ".codebuddy", "settings.json"), "hooks"),
            HookStatus("OpenCode", Path.Combine(Home, ".config", "opencode", "plugins", "codeisland.js"), "plugin"),
            HookStatus("Pi", Path.Combine(Home, ".pi", "agent", "extensions", "codeisland.ts"), "extension"),
            HookStatus("OMP", Path.Combine(Home, ".omp", "agent", "extensions", "codeisland.ts"), "extension"),
            HookStatus("Kimi", Path.Combine(Home, ".kimi", "config.toml"), "toml"),
            HookStatus("Cline", Path.Combine(Home, "Documents", "Cline", "Hooks"), "cline")
        };
        return Task.FromResult(string.Join(Environment.NewLine, results));
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

    private static string InstallNestedNamed(string name, string source, string settingsPath, string configKey)
    {
        if (!ShouldInstall(settingsPath, source)) return $"{name}: skipped";
        var root = ReadJsonObject(settingsPath);
        var hooks = root[configKey] as JsonObject ?? new JsonObject();
        RemoveOurs(hooks);
        var cmd = BridgeCommand(source);
        foreach (var ev in new[] { "UserPromptSubmit", "PreToolUse", "PostToolUse", "PostToolUseFailure", "PermissionRequest", "Stop", "SessionStart", "SessionEnd", "Notification", "PreCompact" })
        {
            var timeout = ev is "PermissionRequest" or "Notification" ? 86400 : 60;
            hooks[ev] = new JsonArray(MakeNestedEntry(cmd + " --event " + ev, timeout, ev is "PreToolUse" or "PostToolUse" or "PermissionRequest" or "Notification" ? "*" : null));
        }
        root[configKey] = hooks;
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

    private static string InstallCopilot()
    {
        var settingsPath = Path.Combine(Home, ".copilot", "hooks", "codeisland.json");
        if (!ShouldInstall(settingsPath, "copilot")) return "Copilot: skipped";
        var root = ReadJsonObject(settingsPath);
        root["version"] ??= 1;
        var hooks = root["hooks"] as JsonObject ?? new JsonObject();
        RemoveOurs(hooks);
        var cmd = BridgeCommand("copilot");
        foreach (var ev in new[] { "sessionStart", "sessionEnd", "userPromptSubmitted", "preToolUse", "postToolUse", "errorOccurred" })
        {
            hooks[ev] = new JsonArray(new JsonObject { ["type"] = "command", ["bash"] = cmd, ["timeoutSec"] = 60 });
        }
        root["hooks"] = hooks;
        WriteJson(settingsPath, root);
        return "Copilot: installed";
    }

    private static string InstallKimi()
    {
        var settingsPath = Path.Combine(Home, ".kimi", "config.toml");
        if (!ShouldInstall(settingsPath, "kimi")) return "Kimi: skipped";
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        var content = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : "";
        var markerStart = "# >>> codeisland hooks >>>";
        var markerEnd = "# <<< codeisland hooks <<<";
        content = RemoveMarkedBlock(content, markerStart, markerEnd).TrimEnd();
        var block = new List<string> { markerStart };
        foreach (var ev in new[] { "session_start", "session_end", "user_prompt_submit", "pre_tool_use", "post_tool_use", "post_tool_use_failure", "permission_request", "notification", "stop" })
        {
            block.Add("[[hooks]]");
            block.Add($"event = \"{ev}\"");
            block.Add($"command = \"{BridgeCommand("kimi").Replace("\\", "\\\\").Replace("\"", "\\\"")} --event {ev}\"");
            block.Add("timeout = 60");
            block.Add("");
        }
        block.Add(markerEnd);
        File.WriteAllText(settingsPath, content + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, block) + Environment.NewLine);
        return "Kimi: installed";
    }

    private static string InstallCline()
    {
        var hooksDir = Path.Combine(Home, "Documents", "Cline", "Hooks");
        if (!Directory.Exists(Path.GetDirectoryName(hooksDir) ?? "") && !CommandExists("code")) return "Cline: skipped";
        Directory.CreateDirectory(hooksDir);
        foreach (var ev in new[] { "UserPromptSubmit", "PreToolUse", "PostToolUse", "TaskStart", "TaskResume", "TaskCancel", "TaskComplete", "PreCompact" })
        {
            var script = "@echo off" + Environment.NewLine
                + BridgeCommand("cline") + " --event " + ev + " >nul 2>nul" + Environment.NewLine
                + "echo {\"cancel\":false}" + Environment.NewLine;
            File.WriteAllText(Path.Combine(hooksDir, ev + ".cmd"), script);
        }
        return "Cline: installed";
    }

    private static string InstallOpenCodePlugin()
    {
        if (!WindowsSettings.Current.IsSourceEnabled("opencode")) return "OpenCode: skipped";
        var configDir = Path.Combine(Home, ".config", "opencode");
        if (!Directory.Exists(configDir) && !CommandExists("opencode")) return "OpenCode: skipped";

        var pluginDir = Path.Combine(configDir, "plugins");
        var pluginPath = Path.Combine(pluginDir, "codeisland.js");
        Directory.CreateDirectory(pluginDir);
        var source = WindowsPluginSource("codeisland-opencode.js", "opencode");
        File.WriteAllText(pluginPath, source);

        var oldPlugin = Path.Combine(pluginDir, "vibe-island.js");
        if (File.Exists(oldPlugin)) File.Delete(oldPlugin);

        var targetPath = File.Exists(Path.Combine(configDir, "opencode.jsonc"))
            ? Path.Combine(configDir, "opencode.jsonc")
            : Path.Combine(configDir, "opencode.json");
        var pluginRef = new Uri(pluginPath).AbsoluteUri;
        var original = File.Exists(targetPath) ? File.ReadAllText(targetPath) : null;
        var merged = MergeOpenCodePluginRef(original, pluginRef);
        if (merged is null) return "OpenCode: plugin staged, config parse failed";
        if (!string.IsNullOrEmpty(original)) BackupOnce(targetPath, original);
        File.WriteAllText(targetPath, merged);

        var legacyPath = Path.Combine(configDir, "config.json");
        if (File.Exists(legacyPath) && !string.Equals(legacyPath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            var legacy = File.ReadAllText(legacyPath);
            var cleaned = RemoveOpenCodePluginRef(legacy);
            if (cleaned is not null)
            {
                BackupOnce(legacyPath, legacy);
                File.WriteAllText(legacyPath, cleaned);
            }
        }
        return "OpenCode: installed";
    }

    private static string InstallPiExtension(string name, string sourceName, string agentDir, string extensionPath)
    {
        if (!WindowsSettings.Current.IsSourceEnabled(sourceName)) return $"{name}: skipped";
        if (!Directory.Exists(agentDir) && !CommandExists(sourceName)) return $"{name}: skipped";
        Directory.CreateDirectory(Path.GetDirectoryName(extensionPath)!);
        var fileName = sourceName.Equals("omp", StringComparison.OrdinalIgnoreCase) ? "codeisland-omp.ts" : "codeisland-pi.ts";
        File.WriteAllText(extensionPath, WindowsPluginSource(fileName, sourceName));
        return $"{name}: installed";
    }

    private static string WindowsPluginSource(string fileName, string sourceName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", fileName);
        if (!File.Exists(path))
        {
            path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "Sources", "CodeIsland", "Resources", fileName));
        }
        if (!File.Exists(path)) throw new FileNotFoundException($"Resource not found: {fileName}");
        return ToWindowsNodeBridge(File.ReadAllText(path), sourceName, fileName.EndsWith(".ts", StringComparison.OrdinalIgnoreCase));
    }

    private static string ToWindowsNodeBridge(string source, string sourceName, bool typed)
    {
        var normalized = source.Replace("\r\n", "\n");
        normalized = ReplaceFunctionBlock(normalized, "sendToSocket", WindowsNodeBridgeBlock(sourceName, typed));
        normalized = ReplaceFunctionBlock(normalized, "sendAndWaitResponse", WindowsSendAndWaitBlock(sourceName, typed));
        normalized = ReplaceFunctionBlock(normalized, "detectTty", "function detectTty(): string | null {\n  return null;\n}");
        var ttyBlockStart = normalized.IndexOf("    let detectedTty = null;", StringComparison.Ordinal);
        var collectEnvStart = normalized.IndexOf("    function collectEnv()", StringComparison.Ordinal);
        if (ttyBlockStart >= 0 && collectEnvStart > ttyBlockStart)
        {
            normalized = normalized[..ttyBlockStart] + "    const detectedTty = null;\n\n" + normalized[collectEnvStart..];
        }
        normalized = normalized
            .Replace("import { connect } from \"node:net\";\n", "")
            .Replace("import { getuid } from \"node:process\";\n", "")
            .Replace("import { connect } from \"net\";\n", "")
            .Replace("import { getuid } from \"process\";\n", "")
            .Replace("const SOCKET = `/tmp/codeisland-${getuid()}.sock`;", "")
            .Replace("const userId = getuid?.() ?? 0;\nconst SOCKET_PATH = `/tmp/codeisland-${userId}.sock`;", "")
            .Replace("const BRIDGE_PATH = `${homedir()}/.codeisland/codeisland-bridge`;", "const BRIDGE_PATH = `${homedir()}/.codeisland/CodeIsland.Bridge.exe`;")
            .Replace("const BRIDGE_PATH = require(\"path\").join(require(\"os\").homedir(), \".codeisland\", \"codeisland-bridge\");", "const BRIDGE_PATH = require(\"path\").join(require(\"os\").homedir(), \".codeisland\", \"CodeIsland.Bridge.exe\");")
            .Replace("const tty = detectTty();", "const tty = null;")
            .Replace("const isOsc2Terminal = [\"ghostty\", \"xterm-ghostty\"].includes(termProg) && !process.env.TMUX;", "const isOsc2Terminal = false;");
        return normalized.Replace("\n", Environment.NewLine);
    }

    private static string ReplaceFunctionBlock(string source, string name, string replacement)
    {
        var start = source.IndexOf("function " + name, StringComparison.Ordinal);
        if (start < 0) return source;
        var brace = source.IndexOf('{', start);
        if (brace < 0) return source;
        var depth = 0;
        var inString = false;
        var quote = '\0';
        var escape = false;
        for (var i = brace; i < source.Length; i++)
        {
            var c = source[i];
            if (inString)
            {
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                if (c == quote) inString = false;
                continue;
            }
            if (c is '\"' or '\'' or '`') { inString = true; quote = c; continue; }
            if (c == '{') depth++;
            if (c != '}') continue;
            depth--;
            if (depth == 0) return source[..start] + replacement + source[(i + 1)..];
        }
        return source;
    }

    private static string WindowsNodeBridgeBlock(string sourceName, bool typed)
    {
        var signature = typed ? "function sendToSocket(json: object): Promise<boolean>" : "function sendToSocket(json)";
        return $$"""
{{signature}} {
  return new Promise((resolve) => {
    try {
      const run = (typeof execFile === "function") ? execFile : require("child_process").execFile;
      const child = run(BRIDGE_PATH, ["--source", "{{sourceName}}"], {
        timeout: 5000, maxBuffer: 1024 * 1024,
      }, (error) => resolve(!error));
      child.stdin.write(JSON.stringify(json));
      child.stdin.end();
    } catch { resolve(false); }
  });
}
""";
    }

    private static string WindowsSendAndWaitBlock(string sourceName, bool typed)
    {
        var signature = typed
            ? "function sendAndWaitResponse(payload: object, timeoutMs = 300000): Promise<Record<string, unknown> | null>"
            : "function sendAndWaitResponse(payload, timeoutMs = 300000)";
        return $$"""
{{signature}} {
  return new Promise((resolve) => {
    const exists = (typeof existsSync === "function") ? existsSync : require("fs").existsSync;
    const run = (typeof execFile === "function") ? execFile : require("child_process").execFile;
    if (!exists(BRIDGE_PATH)) {
      resolve(null);
      return;
    }
    try {
      const child = run(
        BRIDGE_PATH,
        ["--source", "{{sourceName}}"],
        { timeout: timeoutMs, maxBuffer: 1_048_576 },
        (error, stdout) => {
          if (error) {
            resolve(null);
            return;
          }
          try {
            resolve(JSON.parse(stdout));
          } catch {
            resolve(null);
          }
        },
      );
      child.stdin.write(JSON.stringify(payload));
      child.stdin.end();
    } catch {
      resolve(null);
    }
  });
}
""";
    }

    private static bool ShouldInstall(string configPath, string binaryName)
    {
        if (!WindowsSettings.Current.IsSourceEnabled(binaryName)) return false;
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

    private static string? MergeOpenCodePluginRef(string? originalContents, string pluginRef)
    {
        if (string.IsNullOrEmpty(originalContents))
        {
            var newRoot = new JsonObject
            {
                ["$schema"] = "https://opencode.ai/config.json",
                ["plugin"] = new JsonArray(pluginRef)
            };
            return newRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
        }

        var root = ParseJsoncObject(originalContents);
        if (root is null) return null;
        var plugins = ReadStringArray(root, "plugin")
            .Where(p => !IsCodeIslandRef(p))
            .Append(pluginRef)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        root["plugin"] = new JsonArray(plugins.Select(p => JsonValue.Create(p)).ToArray<JsonNode?>());
        root["$schema"] ??= "https://opencode.ai/config.json";
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
    }

    private static string? RemoveOpenCodePluginRef(string originalContents)
    {
        var root = ParseJsoncObject(originalContents);
        if (root is null) return null;
        var plugins = ReadStringArray(root, "plugin").Where(p => !IsCodeIslandRef(p)).ToArray();
        if (plugins.Length == ReadStringArray(root, "plugin").Length) return null;
        if (plugins.Length == 0) root.Remove("plugin");
        else root["plugin"] = new JsonArray(plugins.Select(p => JsonValue.Create(p)).ToArray<JsonNode?>());
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
    }

    private static JsonObject? ParseJsoncObject(string contents)
    {
        try
        {
            return JsonNode.Parse(StripJsonComments(contents), documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true })?.AsObject();
        }
        catch
        {
            return null;
        }
    }

    private static string[] ReadStringArray(JsonObject root, string key)
    {
        return root[key] is JsonArray array
            ? array.Select(v => v?.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : null)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Cast<string>()
                .ToArray()
            : [];
    }

    private static string StripJsonComments(string input)
    {
        var result = new System.Text.StringBuilder(input.Length);
        var inString = false;
        var escape = false;
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (inString)
            {
                result.Append(c);
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                if (c == '"') inString = false;
                continue;
            }
            if (c == '"') { inString = true; result.Append(c); continue; }
            if (c == '/' && i + 1 < input.Length && input[i + 1] == '/')
            {
                i += 2;
                while (i < input.Length && input[i] != '\n') i++;
                if (i < input.Length) result.Append('\n');
                continue;
            }
            if (c == '/' && i + 1 < input.Length && input[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < input.Length && !(input[i] == '*' && input[i + 1] == '/')) i++;
                i++;
                continue;
            }
            result.Append(c);
        }
        return result.ToString();
    }

    private static bool IsCodeIslandRef(string value)
    {
        return value.Contains("codeisland", StringComparison.OrdinalIgnoreCase)
            || value.Contains("vibe-island", StringComparison.OrdinalIgnoreCase)
            || value.Contains("vibenotch", StringComparison.OrdinalIgnoreCase);
    }

    private static void BackupOnce(string path, string original)
    {
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileName(path);
        if (Directory.EnumerateFiles(dir, name + ".codeisland.bak.*").Any()) return;
        var backup = path + ".codeisland.bak." + DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        File.WriteAllText(backup, original);
    }

    private static string UninstallJsonHooks(string name, string settingsPath, string key)
    {
        if (!File.Exists(settingsPath)) return $"{name}: missing";
        var root = ReadJsonObject(settingsPath);
        if (root[key] is not JsonObject hooks) return $"{name}: no hooks";
        RemoveOurs(hooks);
        if (hooks.Count == 0) root.Remove(key); else root[key] = hooks;
        WriteJson(settingsPath, root);
        return $"{name}: uninstalled";
    }

    private static string UninstallKimi()
    {
        var settingsPath = Path.Combine(Home, ".kimi", "config.toml");
        if (!File.Exists(settingsPath)) return "Kimi: missing";
        var content = File.ReadAllText(settingsPath);
        content = RemoveMarkedBlock(content, "# >>> codeisland hooks >>>", "# <<< codeisland hooks <<<").TrimEnd() + Environment.NewLine;
        File.WriteAllText(settingsPath, content);
        return "Kimi: uninstalled";
    }

    private static string UninstallCline()
    {
        var hooksDir = Path.Combine(Home, "Documents", "Cline", "Hooks");
        if (!Directory.Exists(hooksDir)) return "Cline: missing";
        foreach (var ev in new[] { "UserPromptSubmit", "PreToolUse", "PostToolUse", "TaskStart", "TaskResume", "TaskCancel", "TaskComplete", "PreCompact" })
        {
            var path = Path.Combine(hooksDir, ev + ".cmd");
            if (File.Exists(path) && File.ReadAllText(path).Contains("CodeIsland.Bridge", StringComparison.OrdinalIgnoreCase)) File.Delete(path);
        }
        return "Cline: uninstalled";
    }

    private static string UninstallOpenCodePlugin()
    {
        var configDir = Path.Combine(Home, ".config", "opencode");
        var pluginPath = Path.Combine(configDir, "plugins", "codeisland.js");
        var pluginDeleted = false;
        if (File.Exists(pluginPath))
        {
            File.Delete(pluginPath);
            pluginDeleted = true;
        }
        var touched = false;
        foreach (var configPath in new[] { Path.Combine(configDir, "opencode.jsonc"), Path.Combine(configDir, "opencode.json"), Path.Combine(configDir, "config.json") })
        {
            if (!File.Exists(configPath)) continue;
            var original = File.ReadAllText(configPath);
            var cleaned = RemoveOpenCodePluginRef(original);
            if (cleaned is null) continue;
            BackupOnce(configPath, original);
            File.WriteAllText(configPath, cleaned);
            touched = true;
        }
        return pluginDeleted || touched ? "OpenCode: uninstalled" : "OpenCode: missing";
    }

    private static string UninstallOwnedFile(string name, string path, string marker)
    {
        if (!File.Exists(path)) return $"{name}: missing";
        if (!File.ReadAllText(path).Contains(marker, StringComparison.OrdinalIgnoreCase)) return $"{name}: not owned";
        File.Delete(path);
        return $"{name}: uninstalled";
    }

    private static string HookStatus(string name, string path, string key)
    {
        if (key == "toml") return File.Exists(path) && File.ReadAllText(path).Contains("codeisland hooks", StringComparison.OrdinalIgnoreCase) ? $"{name}: installed" : $"{name}: missing";
        if (key == "cline") return Directory.Exists(path) && Directory.EnumerateFiles(path, "*.cmd").Any(f => File.ReadAllText(f).Contains("CodeIsland.Bridge", StringComparison.OrdinalIgnoreCase)) ? $"{name}: installed" : $"{name}: missing";
        if (key is "plugin" or "extension") return File.Exists(path) && File.ReadAllText(path).Contains("CodeIsland.Bridge.exe", StringComparison.OrdinalIgnoreCase) ? $"{name}: installed" : $"{name}: missing";
        if (!File.Exists(path)) return $"{name}: missing";
        return File.ReadAllText(path).Contains("CodeIsland.Bridge", StringComparison.OrdinalIgnoreCase) ? $"{name}: installed" : $"{name}: not installed";
    }

    private static string RemoveMarkedBlock(string content, string markerStart, string markerEnd)
    {
        var start = content.IndexOf(markerStart, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return content;
        var end = content.IndexOf(markerEnd, start, StringComparison.OrdinalIgnoreCase);
        if (end < 0) return content[..start];
        end += markerEnd.Length;
        return content.Remove(start, end - start);
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
