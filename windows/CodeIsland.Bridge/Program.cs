using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CodeIsland.Bridge;

internal static class Program
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".codeisland",
        "bridge.log");

    private static async Task<int> Main(string[] args)
    {
        var source = ArgValue(args, "--source");
        var eventName = ArgValue(args, "--event");
        Log($"start source={source ?? ""} args={string.Join(" ", args)} cwd={Environment.CurrentDirectory}");

        string input;
        {
            using var reader = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
            var readTask = reader.ReadToEndAsync();
            var completed = await Task.WhenAny(readTask, Task.Delay(5000));
            if (completed != readTask)
            {
                Log("stdin read timeout (5s)");
                Environment.Exit(0);
                return 0; // unreachable, satisfies compiler
            }
            input = (await readTask).TrimStart('\uFEFF');
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            Log("empty stdin");
            return 0;
        }

        JsonObject obj;
        try
        {
            obj = JsonNode.Parse(input)?.AsObject() ?? new JsonObject();
        }
        catch (Exception ex)
        {
            Log("json parse failed: " + ex.Message);
            return 0;
        }

        if (StringProp(obj, "hook_event_name", "hookEventName", "event_name", "eventName", "event") is not { } rawEvent)
        {
            rawEvent = eventName ?? "Notification";
            obj["hook_event_name"] = rawEvent;
        }
        else if (!obj.ContainsKey("hook_event_name"))
        {
            obj["hook_event_name"] = rawEvent;
        }

        if (StringProp(obj, "session_id", "sessionId") is not { } sessionId)
        {
            sessionId = string.IsNullOrWhiteSpace(source)
                ? $"windows-{Environment.ProcessId}"
                : $"{source}-windows-{Environment.ProcessId}";
            obj["session_id"] = sessionId;
        }
        else if (!obj.ContainsKey("session_id"))
        {
            obj["session_id"] = sessionId;
        }

        if (!string.IsNullOrWhiteSpace(source)) obj["_source"] = source;
        obj["_bridge_pid"] = Environment.ProcessId;
        obj["_ppid"] = ParentProcessId(Environment.ProcessId) ?? Environment.ProcessId;
        obj["_ancestor_pids"] = new JsonArray(AncestorProcessIds(Environment.ProcessId).Select(pid => JsonValue.Create(pid)).ToArray<JsonNode?>());
        var wtSession = Environment.GetEnvironmentVariable("WT_SESSION");
        obj["_wt_session"] = wtSession;
        obj["_window_title_hint"] ??= BuildWindowTitleHint(obj, source, wtSession);
        obj["_term_app"] = wtSession is null
            ? Environment.GetEnvironmentVariable("TERM_PROGRAM")
            : "Windows Terminal";
        obj["cwd"] ??= Environment.CurrentDirectory;

        var payload = Encoding.UTF8.GetBytes(obj.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
        var normalized = NormalizeEvent(rawEvent);
        var blocking = normalized is "PermissionRequest" || (normalized is "Notification" && obj.ContainsKey("question"));

        try
        {
            using var pipe = new NamedPipeClientStream(".", "codeisland", PipeDirection.InOut, PipeOptions.Asynchronous);
            pipe.Connect(blocking ? 3000 : 1000);
            Log($"connected event={rawEvent} normalized={normalized} session={sessionId} blocking={blocking}");

            var length = BitConverter.GetBytes(payload.Length);
            await pipe.WriteAsync(length, 0, length.Length);
            await pipe.WriteAsync(payload, 0, payload.Length);
            if (!blocking)
            {
                Log("non-blocking sent");
                return 0;
            }

            var responseLengthBytes = await ReadExactAsync(pipe, 4, CancellationToken.None);
            var responseLength = BitConverter.ToInt32(responseLengthBytes, 0);
            var response = responseLength > 0
                ? await ReadExactAsync(pipe, responseLength, CancellationToken.None)
                : [];
            Log($"response bytes={response.Length}");
            if (response.Length > 0)
            {
                Console.Out.Write(Encoding.UTF8.GetString(response));
            }
        }
        catch (Exception ex)
        {
            Log("pipe failed: " + ex.GetType().Name + " " + ex.Message);
            return 0;
        }

        Log("done");
        return 0;
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int length, CancellationToken token)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), token);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
        return buffer;
    }

    private static string? ArgValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name) return args[i + 1];
        }
        return null;
    }

    private static string? StringProp(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj.TryGetPropertyValue(name, out var node) && node is not null)
            {
                var text = node.GetValueKind() == JsonValueKind.String
                    ? node.GetValue<string>()
                    : node.ToJsonString();
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
        }
        return null;
    }

    private static string NormalizeEvent(string name) => name switch
    {
        "permission_request" => "PermissionRequest",
        "pre_tool_use" or "preToolUse" or "BeforeTool" or "beforeShellExecution" => "PreToolUse",
        "post_tool_use" or "postToolUse" or "AfterTool" or "afterShellExecution" => "PostToolUse",
        "user_prompt_submit" or "userPromptSubmit" or "userPromptSubmitted" or "beforeSubmitPrompt" => "UserPromptSubmit",
        "session_start" or "sessionStart" => "SessionStart",
        "session_end" or "sessionEnd" => "SessionEnd",
        "notification" => "Notification",
        "stop" => "Stop",
        _ => name
    };

    private static string BuildWindowTitleHint(JsonObject obj, string? source, string? wtSession)
    {
        var sessionId = StringProp(obj, "session_id", "sessionId") ?? "session";
        var cwd = StringProp(obj, "cwd") ?? Environment.CurrentDirectory;
        var folder = Path.GetFileName(cwd.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(folder)) folder = "session";
        var prefix = sessionId.Length <= 16 ? sessionId : sessionId[..16];
        var sourceName = string.IsNullOrWhiteSpace(source) ? StringProp(obj, "_source", "source") ?? "agent" : source;
        return string.IsNullOrWhiteSpace(wtSession)
            ? $"CodeIsland {sourceName} {folder} {prefix}"
            : $"CodeIsland {sourceName} {folder} {prefix} {wtSession}";
    }

    private static int? ParentProcessId(int pid)
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher($"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId={pid}");
            foreach (System.Management.ManagementObject item in searcher.Get())
            {
                return Convert.ToInt32(item["ParentProcessId"]);
            }
        }
        catch
        {
        }
        return null;
    }

    private static IEnumerable<int> AncestorProcessIds(int pid)
    {
        var seen = new HashSet<int>();
        var current = pid;
        for (var i = 0; i < 12; i++)
        {
            var parent = ParentProcessId(current);
            if (parent is null || parent <= 0 || !seen.Add(parent.Value)) yield break;
            yield return parent.Value;
            current = parent.Value;
        }
    }

    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
