using System.Media;

namespace CodeIsland.Desktop;

public static class SoundManager
{
    public static void PlayForEvent(string normalizedEvent)
    {
        if (!WindowsSettings.Current.SoundEnabled) return;
        var sound = normalizedEvent switch
        {
            "SessionStart" => "8bit_start.wav",
            "TaskRoundComplete" or "Stop" or "SessionEnd" => "8bit_complete.wav",
            "PostToolUseFailure" => "8bit_error.wav",
            "PermissionRequest" or "Notification" => "8bit_approval.wav",
            "UserPromptSubmit" => "8bit_submit.wav",
            _ => null
        };
        if (sound is null) return;
        var path = Path.Combine(AppContext.BaseDirectory, "sounds", sound);
        if (!File.Exists(path)) return;
        try
        {
            using var player = new SoundPlayer(path);
            player.Play();
        }
        catch
        {
        }
    }
}
