using System.Text.Json;

namespace CodeIsland.WinUI.Models;

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
    JsonElement Raw)
{
    public static HookEvent? TryParse(byte[] payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;

        var eventName = FirstString(root, "hook_event_name", "hookEventName", "event_name", "eventName");
        if (string.IsNullOrWhiteSpace(eventName)) return null;

        var sessionId = FirstString(root, "session_id", "sessionId") ?? "default";
        var toolName = FirstString(root, "tool_name", "toolName", "tool", "name") ?? FirstString(root, "payload", "toolName");
        var toolInput = FirstObject(root, "tool_input", "toolInput", "input", "arguments", "args", "params");
        var description = BuildToolDescription(toolName, toolInput, root);
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
            FirstString(root, "question"),
            StringArray(root, "options"),
            raw);
    }

    private static string? BuildToolDescription(string? toolName, JsonElement? input, JsonElement root)
    {
        if (input is { ValueKind: JsonValueKind.Object } obj)
        {
            if (toolName is "Bash" or "execute_command") return FirstString(obj, "description", "command");
            if (FirstString(obj, "file_path", "path") is { } path) return Path.GetFileName(path);
            if (FirstString(obj, "pattern", "query", "prompt", "command") is { } text) return text.Length > 80 ? text[..80] : text;
        }
        return FirstString(root, "message", "text", "summary", "status", "detail", "content");
    }

    private static string[] StringArray(JsonElement element, string key)
    {
        if (!element.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Array) return [];
        return value.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)).Cast<string>().ToArray();
    }

    private static JsonElement? FirstObject(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Object) return value;
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