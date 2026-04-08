using System.Diagnostics;

namespace vCamService.App.Services;

public static class FfmpegChecker
{
    public static bool IsAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo("ffmpeg", "-version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            process?.WaitForExit(3000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static string GetInstallInstructions() =>
        "FFmpeg is required but was not found on your PATH.\n\n" +
        "To install FFmpeg:\n" +
        "  winget install ffmpeg\n\n" +
        "After installing, restart vCamService.\n\n" +
        "Or download from: https://ffmpeg.org/download.html";
}
