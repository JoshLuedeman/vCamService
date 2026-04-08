using System.Diagnostics;
using vCamService.Core.Models;

namespace vCamService.Core.Services;

/// <summary>
/// Reads RTSP or MJPEG streams via FFmpeg subprocess.
/// FFmpeg decodes to raw BGRA frames piped to stdout.
/// Frames are written to FrameBuffer. Auto-reconnects via ReconnectManager.
/// </summary>
public sealed class StreamReader : IStreamReader
{
    private StreamStatus _status = StreamStatus.Disconnected;
    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private bool _disposed;

    public StreamStatus Status => _status;
    public FrameBuffer FrameBuffer { get; } = new();
    public event Action<StreamStatus>? StatusChanged;

    public Task StartAsync(StreamConfig config, CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _runTask = RunLoopAsync(config, _cts.Token);
        return Task.CompletedTask;
    }

    public void Stop()
    {
        _cts?.Cancel();
        _runTask?.Wait(TimeSpan.FromSeconds(5));
        FrameBuffer.Clear();
        SetStatus(StreamStatus.Disconnected);
    }

    private async Task RunLoopAsync(StreamConfig config, CancellationToken ct)
    {
        var reconnect = new ReconnectManager();

        while (!ct.IsCancellationRequested)
        {
            SetStatus(StreamStatus.Connecting);
            try
            {
                await ReadStreamAsync(config, reconnect, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (FileNotFoundException ex)
            {
                SetStatus(StreamStatus.Error);
                // ffmpeg not found — no point retrying
                _ = ex;
                break;
            }
            catch
            {
                // Stream error — will reconnect below
            }

            if (ct.IsCancellationRequested)
                break;

            SetStatus(StreamStatus.Reconnecting);
            if (!await reconnect.WaitAsync(ct).ConfigureAwait(false))
                break;
        }

        SetStatus(StreamStatus.Disconnected);
    }

    private async Task ReadStreamAsync(StreamConfig config, ReconnectManager reconnect, CancellationToken ct)
    {
        var args = BuildFfmpegArgs(config);
        var ffmpegPath = FindFfmpeg();

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        process.Start();

        // Drain stderr asynchronously to avoid deadlock
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        var frameSize = config.Width * config.Height * 4; // BGRA
        var buffer = new byte[frameSize];
        bool connected = false;

        try
        {
            var stdout = process.StandardOutput.BaseStream;
            while (!ct.IsCancellationRequested)
            {
                var bytesRead = await ReadExactAsync(stdout, buffer, frameSize, ct).ConfigureAwait(false);
                if (bytesRead < frameSize)
                    break;

                if (!connected)
                {
                    connected = true;
                    reconnect.Reset();
                    SetStatus(StreamStatus.Connected);
                }

                var frame = new byte[frameSize];
                Buffer.BlockCopy(buffer, 0, frame, 0, frameSize);
                FrameBuffer.Put(frame, config.Width, config.Height);
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                try { process.Kill(); } catch { }
            }
            process.WaitForExit(3000);
        }
    }

    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, int count, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await stream.ReadAsync(buffer, totalRead, count - totalRead, ct).ConfigureAwait(false);
            if (read == 0)
                return totalRead;
            totalRead += read;
        }
        return totalRead;
    }

    private static string BuildFfmpegArgs(StreamConfig config)
    {
        var parts = new List<string> { "-hide_banner", "-loglevel", "warning" };

        if (config.Protocol == "rtsp")
            parts.AddRange(["-rtsp_transport", config.RtspTransport]);

        parts.AddRange([
            "-fflags", "nobuffer",
            "-flags", "low_delay",
            "-analyzeduration", "500000",
            "-probesize", "500000",
            "-i", config.Url,
            "-f", "rawvideo",
            "-pix_fmt", "bgra",
            "-s", $"{config.Width}x{config.Height}",
            "-r", config.Fps.ToString(),
            "-vsync", "cfr",
            "pipe:1"
        ]);

        return string.Join(" ", parts.Select(p => p.Contains(' ') ? $"\"{p}\"" : p));
    }

    private static string FindFfmpeg()
    {
        // Check PATH
        var pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var dir in pathDirs)
        {
            var candidate = Path.Combine(dir, "ffmpeg.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        // Common install locations
        string[] fallbacks = [
            @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
            @"C:\ffmpeg\bin\ffmpeg.exe"
        ];
        foreach (var path in fallbacks)
        {
            if (File.Exists(path))
                return path;
        }

        throw new FileNotFoundException("ffmpeg.exe not found. Install via: winget install ffmpeg");
    }

    private void SetStatus(StreamStatus status)
    {
        _status = status;
        try { StatusChanged?.Invoke(status); } catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _cts?.Dispose();
    }
}
