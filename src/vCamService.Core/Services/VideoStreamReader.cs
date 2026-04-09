using System.Diagnostics;

namespace vCamService.Core.Services;

/// <summary>
/// Spawns ffmpeg.exe to decode an MJPEG/RTSP/HTTP video stream and pipes
/// raw BGRA frames into a SharedFrameBuffer for the virtual camera COM server.
/// </summary>
public sealed class VideoStreamReader : IDisposable
{
    private readonly string _streamUrl;
    private int _width;
    private int _height;
    private int _fpsNum;
    private int _fpsDen;
    private int _frameSize;
    private SharedFrameBuffer? _sharedBuffer;
    private Process? _ffmpeg;
    private Thread? _readerThread;
    private Thread? _stderrThread;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public bool IsRunning { get; private set; }
    public ulong FramesWritten { get; private set; }
    public string? LastError { get; private set; }
    public int DetectedWidth => _width;
    public int DetectedHeight => _height;
    public int DetectedFpsNum => _fpsNum;
    public int DetectedFpsDen => _fpsDen;

    public event Action<string>? OnError;
    public event Action<string>? OnLog;

    public VideoStreamReader(string streamUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamUrl);
        _streamUrl = streamUrl;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsRunning) return;

        // Probe stream to detect native resolution and frame rate
        string ffprobePath = FindTool("ffprobe.exe");
        ProbeStream(ffprobePath);
        OnLog?.Invoke($"Detected stream: {_width}x{_height} @ {_fpsNum}/{_fpsDen} fps");

        _frameSize = _width * _height * 3 / 2; // NV12: Y plane + interleaved UV

        // Write stream config so the COM server knows what dimensions to use
        var config = new StreamConfig
        {
            Width = _width, Height = _height,
            FpsNumerator = _fpsNum, FpsDenominator = _fpsDen,
            PixelFormat = SharedFrameBuffer.PixelFormatNV12
        };
        config.Save();
        OnLog?.Invoke("Stream config saved for COM server");

        _cts = new CancellationTokenSource();

        // Start background thread that waits for MMF then launches ffmpeg
        _readerThread = new Thread(WaitAndStream) { IsBackground = true, Name = "vCam-WaitAndStream" };
        _readerThread.Start();

        IsRunning = true;
    }

    /// <summary>
    /// Background thread: starts ffmpeg immediately to pre-warm the decoder,
    /// then waits for COM server to create the shared memory buffer.
    /// Once buffer appears, reads frames directly into it (zero-copy).
    /// </summary>
    private void WaitAndStream()
    {
        var ct = _cts?.Token ?? CancellationToken.None;

        string ffmpegPath;
        try { ffmpegPath = FindTool("ffmpeg.exe"); }
        catch (Exception ex) { OnError?.Invoke(ex.Message); return; }

        // ffmpeg args: decode stream at native resolution/fps → output raw NV12 to stdout
        // Low-latency flags: minimize probe/analyze time, disable buffering
        string args = string.Join(" ",
            "-hide_banner",
            "-loglevel", "error",
            "-probesize", "32",
            "-analyzeduration", "0",
            "-fflags", "+nobuffer+flush_packets",
            "-flags", "low_delay",
            "-i", $"\"{_streamUrl}\"",
            "-f", "rawvideo",
            "-pix_fmt", "nv12",
            "-an",
            "-sn",
            "pipe:1");

        _ffmpeg = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false,
            },
            EnableRaisingEvents = true,
        };

        _ffmpeg.Exited += (_, _) =>
        {
            if (!_cts?.IsCancellationRequested ?? false)
            {
                LastError = "ffmpeg process exited unexpectedly";
                OnError?.Invoke(LastError);
            }
        };

        // Start ffmpeg NOW — let it connect and start decoding while we wait for the buffer
        _ffmpeg.Start();
        OnLog?.Invoke($"ffmpeg started (PID {_ffmpeg.Id}), pre-warming decoder...");

        _stderrThread = new Thread(DrainStderr) { IsBackground = true, Name = "vCam-StderrDrain" };
        _stderrThread.Start();

        // Wait for the COM server to create the shared memory buffer.
        // While waiting, drain ffmpeg stdout to prevent pipe blocking.
        OnLog?.Invoke("Waiting for shared memory buffer (open camera in an app)...");
        var stdout = _ffmpeg.StandardOutput.BaseStream;
        byte[] drainBuf = new byte[_frameSize];

        while (!ct.IsCancellationRequested)
        {
            _sharedBuffer = SharedFrameBuffer.OpenWriter();
            if (_sharedBuffer != null) break;

            // Drain one frame from ffmpeg to prevent pipe backup
            int drained = 0;
            while (drained < _frameSize)
            {
                int n = stdout.Read(drainBuf, drained, _frameSize - drained);
                if (n == 0) { OnLog?.Invoke("ffmpeg exited while waiting for buffer"); return; }
                drained += n;
            }
        }

        if (_sharedBuffer == null)
        {
            OnLog?.Invoke("Cancelled while waiting for shared buffer");
            return;
        }
        OnLog?.Invoke("Shared memory buffer connected — streaming");

        // Read frames directly into MMF (zero-copy)
        ReadFrames();
    }

    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;

        _cts?.Cancel();

        try
        {
            if (_ffmpeg != null && !_ffmpeg.HasExited)
            {
                // Close stdin to signal ffmpeg to exit gracefully
                try { _ffmpeg.StandardOutput.Close(); } catch { }
                if (!_ffmpeg.WaitForExit(2000))
                {
                    _ffmpeg.Kill(entireProcessTree: true);
                }
            }
        }
        catch { }

        _readerThread?.Join(3000);
        _stderrThread?.Join(1000);

        _ffmpeg?.Dispose();
        _ffmpeg = null;
        _sharedBuffer?.Dispose();
        _sharedBuffer = null;
        _cts?.Dispose();
        _cts = null;
    }

    private unsafe void ReadFrames()
    {
        var stream = _ffmpeg?.StandardOutput.BaseStream;
        if (stream == null) return;

        try
        {
            while (!(_cts?.IsCancellationRequested ?? true))
            {
                if (_sharedBuffer == null) return;

                // Get pointer to the inactive MMF slot — write directly, no intermediate buffer
                int writeSlot = _sharedBuffer.GetWriteSlot();
                byte* slotPtr = _sharedBuffer.GetSlotPointer(writeSlot);
                if (slotPtr == null) return;

                // Read exactly one frame from ffmpeg stdout directly into the MMF slot
                int totalRead = 0;
                while (totalRead < _frameSize)
                {
                    int bytesRead = stream.Read(new Span<byte>(slotPtr + totalRead, _frameSize - totalRead));
                    if (bytesRead == 0)
                    {
                        OnLog?.Invoke("ffmpeg stdout closed (EOF)");
                        return;
                    }
                    totalRead += bytesRead;
                }

                // Commit the slot (flip active, update sequence/counter/heartbeat)
                _sharedBuffer.CommitSlot(writeSlot);
                FramesWritten++;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!(_cts?.IsCancellationRequested ?? true))
            {
                LastError = $"Frame read error: {ex.Message}";
                OnError?.Invoke(LastError);
            }
        }
    }

    private void DrainStderr()
    {
        try
        {
            var stderr = _ffmpeg?.StandardError;
            if (stderr == null) return;

            while (!(_cts?.IsCancellationRequested ?? true))
            {
                string? line = stderr.ReadLine();
                if (line == null) break;
                if (!string.IsNullOrWhiteSpace(line))
                {
                    OnLog?.Invoke($"[ffmpeg] {line}");
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// Poll for the Global\ shared memory buffer created by the COM server.
    /// Returns null if cancelled.
    /// </summary>
    private SharedFrameBuffer? WaitForSharedBuffer(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var buf = SharedFrameBuffer.OpenWriter();
            if (buf != null) return buf;
            try { Task.Delay(250, ct).Wait(ct); } catch (OperationCanceledException) { break; }
        }
        return null;
    }

    private void ProbeStream(string ffprobePath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ffprobePath,
            Arguments = $"-v quiet -select_streams v:0 -show_entries stream=width,height,r_frame_rate -of csv=p=0 \"{_streamUrl}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var proc = Process.Start(psi)!;
        string output = proc.StandardOutput.ReadToEnd().Trim();
        proc.WaitForExit(10_000);

        // Output format: "width,height,num/den"  e.g. "1920,1080,25/1"
        var parts = output.Split(',');
        if (parts.Length >= 3
            && int.TryParse(parts[0], out int w) && w > 0
            && int.TryParse(parts[1], out int h) && h > 0)
        {
            _width = w;
            _height = h;

            var fpsParts = parts[2].Split('/');
            if (fpsParts.Length == 2
                && int.TryParse(fpsParts[0], out int num) && num > 0
                && int.TryParse(fpsParts[1], out int den) && den > 0)
            {
                _fpsNum = num;
                _fpsDen = den;
            }
            else
            {
                _fpsNum = 30; _fpsDen = 1;
            }
        }
        else
        {
            // Fallback defaults
            OnLog?.Invoke($"ffprobe could not detect stream (output: '{output}'), using 1920x1080@25fps");
            _width = 1920; _height = 1080; _fpsNum = 25; _fpsDen = 1;
        }
    }

    private static string FindTool(string toolName)
    {
        // Check PATH first
        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(';') ?? [];
        foreach (var dir in pathDirs)
        {
            var candidate = Path.Combine(dir.Trim(), toolName);
            if (File.Exists(candidate)) return candidate;
        }

        // Common install locations
        string baseName = Path.GetFileNameWithoutExtension(toolName);
        string[] commonPaths =
        [
            $@"C:\ffmpeg\bin\{toolName}",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin", toolName),
        ];

        foreach (var path in commonPaths)
        {
            if (File.Exists(path)) return path;
        }

        // Try winget location
        var wingetDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WinGet", "Packages");
        if (Directory.Exists(wingetDir))
        {
            foreach (var dir in Directory.GetDirectories(wingetDir, "Gyan.FFmpeg*"))
            {
                var candidates = Directory.GetFiles(dir, toolName, SearchOption.AllDirectories);
                if (candidates.Length > 0) return candidates[0];
            }
        }

        throw new FileNotFoundException($"{toolName} not found. Install via 'winget install Gyan.FFmpeg' or add to PATH.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
