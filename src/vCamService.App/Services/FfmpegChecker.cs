using System.Diagnostics;
using System.IO;

namespace vCamService.App.Services;

public static class FfmpegChecker
{
    public static bool IsAvailable()
    {
        // Try PATH first
        if (TryRunFfmpeg("ffmpeg")) return true;

        // Try winget install location
        var winget = FindWingetFfmpeg();
        if (winget != null && TryRunFfmpeg(winget)) return true;

        // Try common install locations
        string[] fallbacks =
        [
            @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
            @"C:\ffmpeg\bin\ffmpeg.exe"
        ];
        return fallbacks.Any(p => File.Exists(p) && TryRunFfmpeg(p));
    }

    private static bool TryRunFfmpeg(string path)
    {
        try
        {
            var psi = new ProcessStartInfo(path, "-version")
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
        catch { return false; }
    }

    private static string? FindWingetFfmpeg()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var wingetPkgs = Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
        if (!Directory.Exists(wingetPkgs)) return null;

        try
        {
            foreach (var file in Directory.EnumerateFiles(wingetPkgs, "ffmpeg.exe", SearchOption.AllDirectories))
                return file;
        }
        catch { /* Permission errors — skip */ }

        return null;
    }

    public static string GetInstallInstructions() =>
        "FFmpeg is required but was not found on your PATH.\n\n" +
        "To install FFmpeg:\n" +
        "  winget install ffmpeg\n\n" +
        "After installing, restart vCamService.\n\n" +
        "Or download from: https://ffmpeg.org/download.html";
}
