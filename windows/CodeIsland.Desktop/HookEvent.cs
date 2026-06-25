using System.Text.Json;

namespace CodeIsland.Desktop;

public sealed record HookEvent(
    string EventName,
    string SessionId,
    string? ToolName,
    string? ToolUseId,
    string? AgentId,
    string? Source,
    string? Cwd,
    string? ToolDescription,
    string? Question,
    string[] Options,
    string[] OptionDescriptions,
    string? Model,
    string? PermissionMode,
    string? UserPrompt,
    string? AssistantMessage,
    string? TermApp,
    string? WindowsTerminalSession,
    string? WindowTitleHint,
    int[] AncestorProcessIds,
    int? ProcessId,
    JsonElement Raw)
{
    public static HookEvent? TryParse(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;

        var eventName = FirstString(root, "hook_event_name", "hookEventName", "event_name", "eventName");
        if (string.IsNullOrWhiteSpace(eventName)) return null;

        var sessionId = FirstString(root, "session_id", "sessionId") ?? "default";
        var payload = FirstObject(root, "payload", "data");
        var toolName = FirstString(root, "tool_name", "toolName", "tool", "name")
            ?? FirstString(payload, "tool_name", "toolName", "tool", "name");
        var toolInput = FirstObject(root, "tool_input", "toolInput", "input", "arguments", "args", "params");
        if (toolInput is null) toolInput = FirstObject(payload, "tool_input", "toolInput", "input", "arguments", "args", "params");
        var description = BuildToolDescription(toolName, toolInput, payload, root);
        var options = StringArray(root, "options");
        if (options.Length == 0) options = StringArray(payload, "options");
        var optionDescriptions = StringArray(root, "descriptions", "option_descriptions", "optionDescriptions");
        if (optionDescriptions.Length == 0) optionDescriptions = StringArray(payload, "descriptions", "option_descriptions", "optionDescriptions");
        var raw = root.Clone();
        return new HookEvent(
            eventName,
            sessionId,
            toolName,
            FirstString(root, "tool_use_id", "toolUseId"),
            FirstString(root, "agent_id", "agentId"),
            FirstString(root, "_source", "source"),
            FirstString(root, "cwd"),
            description,
            FirstString(root, "question") ?? FirstString(payload, "question"),
            options,
            optionDescriptions,
            FirstString(root, "model") ?? FirstString(payload, "model"),
            FirstString(root, "permission_mode", "permissionMode") ?? FirstString(payload, "permission_mode", "permissionMode"),
            FirstString(root, "prompt", "last_user_message", "userPrompt") ?? FirstString(payload, "prompt", "last_user_message", "userPrompt"),
            FirstString(root, "assistant_message", "last_assistant_message", "response", "message", "text", "summary")
                ?? FirstString(payload, "assistant_message", "last_assistant_message", "response", "message", "text", "summary"),
            FirstString(root, "_term_app", "termApp") ?? FirstString(payload, "_term_app", "termApp"),
            FirstString(root, "_wt_session", "wtSession") ?? FirstString(payload, "_wt_session", "wtSession"),
            FirstString(root, "_window_title_hint", "windowTitleHint") ?? FirstString(payload, "_window_title_hint", "windowTitleHint"),
            IntArray(root, "_ancestor_pids", "ancestorPids"),
            FirstInt(root, "_ppid", "pid") ?? FirstInt(payload, "_ppid", "pid"),
            raw);
    }

    private static int[] IntArray(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Array) continue;
            return value.EnumerateArray()
                .Select(v => v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var number) ? number : 0)
                .Where(v => v > 0)
                .ToArray();
        }
        return [];
    }

    private static string? BuildToolDescription(string? toolName, JsonElement? input, JsonElement? payload, JsonElement root)
    {
        if (input is { ValueKind: JsonValueKind.Object } obj)
        {
            if (toolName is "Bash" or "execute_command") return FirstString(obj, "description", "command");
            if (FirstString(obj, "file_path", "path") is { } path) return Path.GetFileName(path);
            if (FirstString(obj, "pattern", "query", "prompt", "command") is { } text) return text.Length > 80 ? text[..80] : text;
        }
        return FirstString(root, "message", "text", "summary", "status", "detail", "content")
            ?? FirstString(payload, "message", "text", "summary", "status", "detail", "content");
    }

    private static string[] StringArray(JsonElement? element, params string[] keys)
    {
        return element is { } value ? StringArray(value, keys) : [];
    }

    private static string[] StringArray(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Array) continue;
            return value.EnumerateArray()
                .Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Cast<string>()
                .ToArray();
        }
        return [];
    }

    private static JsonElement? FirstObject(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Object) return value;
        }
        return null;
    }

    private static JsonElement? FirstObject(JsonElement? element, params string[] keys)
    {
        return element is { } value ? FirstObject(value, keys) : null;
    }

    private static string? FirstString(JsonElement? element, params string[] keys)
    {
        return element is { } value ? FirstString(value, keys) : null;
    }

    private static int? FirstInt(JsonElement? element, params string[] keys)
    {
        return element is { } value ? FirstInt(value, keys) : null;
    }

    private static int? FirstInt(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var value)) continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number)) return number;
        }
        return null;
    }

    private static string? FirstString(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (element.TryGetProperty(key, out var value))
            {
                if (value.ValueKind == JsonValueKind.String)
                {
                    var text = value.GetString();
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }
                else if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                {
                    return value.ToString();
                }
            }
        }
        return null;
    }
}
