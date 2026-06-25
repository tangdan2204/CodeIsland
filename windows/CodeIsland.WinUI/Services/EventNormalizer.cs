namespace CodeIsland.WinUI.Services;

public static class EventNormalizer
{
    public static string Normalize(string name)
    {
        return name switch
        {
            "beforeSubmitPrompt" => "UserPromptSubmit",
            "beforeShellExecution" => "PreToolUse",
            "afterShellExecution" => "PostToolUse",
            "beforeReadFile" => "PreToolUse",
            "afterFileEdit" => "PostToolUse",
            "beforeMCPExecution" => "PreToolUse",
            "afterMCPExecution" => "PostToolUse",
            "afterAgentThought" => "Notification",
            "afterAgentResponse" => "AfterAgentResponse",
            "stop" => "Stop",
            "BeforeTool" => "PreToolUse",
            "AfterTool" => "PostToolUse",
            "BeforeAgent" => "SubagentStart",
            "AfterAgent" => "SubagentStop",
            "sessionStart" => "SessionStart",
            "sessionEnd" => "SessionEnd",
            "userPromptSubmitted" => "UserPromptSubmit",
            "preToolUse" => "PreToolUse",
            "postToolUse" => "PostToolUse",
            "errorOccurred" => "Notification",
            "agentSpawn" => "SessionStart",
            "userPromptSubmit" => "UserPromptSubmit",
            "session_start" => "SessionStart",
            "session_end" => "SessionEnd",
            "user_prompt_submit" => "UserPromptSubmit",
            "pre_tool_use" => "PreToolUse",
            "post_tool_use" => "PostToolUse",
            "post_tool_use_failure" => "PostToolUseFailure",
            "permission_request" => "PermissionRequest",
            "subagent_start" => "SubagentStart",
            "subagent_stop" => "SubagentStop",
            "pre_compact" => "PreCompact",
            "post_compact" => "PostCompact",
            "notification" => "Notification",
            "pre_tool_call" => "PreToolUse",
            "post_tool_call" => "PostToolUse",
            "pre_llm_call" => "UserPromptSubmit",
            "on_session_start" => "SessionStart",
            "on_session_end" => "SessionEnd",
            "on_session_reset" => "SessionEnd",
            "TaskStart" => "SessionStart",
            "TaskResume" => "UserPromptSubmit",
            "TaskComplete" => "TaskRoundComplete",
            "TaskCancel" => "TaskRoundComplete",
            _ => name
        };
    }
}