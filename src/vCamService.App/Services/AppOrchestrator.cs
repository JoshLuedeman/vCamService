using Microsoft.Extensions.Logging;
using vCamService.Core.Models;
using vCamService.Core.Services;
using vCamService.VCam;

namespace vCamService.App.Services;

public class AppOrchestrator : IDisposable
{
    private readonly Dictionary<string, IStreamReader> _readers = new();
    private readonly VirtualCameraManager _vcam;
    private readonly IConfigService _config;
    private readonly ILogger<AppOrchestrator> _logger;
    private AppConfig _appConfig;
    private string? _activeStreamId;
    private CancellationTokenSource? _cts;
    private Task? _feederTask;

    public AppOrchestrator(VirtualCameraManager vcam, IConfigService config, ILogger<AppOrchestrator> logger)
    {
        _vcam = vcam;
        _config = config;
        _logger = logger;
        _appConfig = config.Load();
    }

    public async Task StartAsync()
    {
        _logger.LogInformation("Starting AppOrchestrator, {StreamCount} streams configured", _appConfig.Streams.Count);
        _vcam.Start(_appConfig.VCamWidth, _appConfig.VCamHeight, _appConfig.VCamFps);
        _cts = new CancellationTokenSource();
        _feederTask = FeedVirtualCameraAsync(_cts.Token);

        foreach (var stream in _appConfig.Streams.Where(s => s.Enabled))
            await StartReaderAsync(stream);

        if (_appConfig.ActiveStreamId != null && _readers.ContainsKey(_appConfig.ActiveStreamId))
            _activeStreamId = _appConfig.ActiveStreamId;
        else if (_readers.Count > 0)
            _activeStreamId = _readers.Keys.First();
    }

    public async Task ShutdownAsync()
    {
        _logger.LogWarning("AppOrchestrator shutdown requested");
        _cts?.Cancel();
        if (_feederTask != null) await _feederTask.ConfigureAwait(false);
        foreach (var reader in _readers.Values) reader.Stop();
        _readers.Clear();
        _vcam.Stop();
        _config.Save(_appConfig with { ActiveStreamId = _activeStreamId });
        _logger.LogInformation("AppOrchestrator shutdown complete");
    }

    public async Task AddStreamAsync(StreamConfig config)
    {
        _logger.LogInformation("Adding stream {Name} ({Url})", config.Name, config.Url);
        var streams = _appConfig.Streams.ToList();
        streams.Add(config);
        _appConfig = _appConfig with { Streams = streams };
        _config.Save(_appConfig);
        if (config.Enabled)
            await StartReaderAsync(config);
    }

    public void RemoveStream(string id)
    {
        _logger.LogInformation("Removing stream {Id}", id);
        if (_readers.TryGetValue(id, out var reader))
        {
            reader.Stop();
            _readers.Remove(id);
        }
        var streams = _appConfig.Streams.Where(s => s.Id != id).ToList();
        _appConfig = _appConfig with { Streams = streams };
        _config.Save(_appConfig);
        if (_activeStreamId == id)
            _activeStreamId = _readers.Keys.FirstOrDefault();
    }

    public void SetActiveStream(string? id)
    {
        _logger.LogInformation("Active stream changed to {Id}", id);
        _activeStreamId = id;
        _appConfig = _appConfig with { ActiveStreamId = id };
        _config.Save(_appConfig);
    }

    public IStreamReader? GetReader(string id) =>
        _readers.TryGetValue(id, out var r) ? r : null;

    private async Task StartReaderAsync(StreamConfig config)
    {
        var reader = new StreamReader();
        _readers[config.Id] = reader;
        await reader.StartAsync(config, CancellationToken.None);
    }

    private async Task FeedVirtualCameraAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromMilliseconds(1000.0 / _appConfig.VCamFps);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_activeStreamId != null && _readers.TryGetValue(_activeStreamId, out var reader))
                {
                    var (frame, w, h) = reader.FrameBuffer.Get();
                    if (frame != null)
                        _vcam.SendFrame(frame, w, h);
                }
                await Task.Delay(interval, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in frame feeder loop");
            }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        foreach (var r in _readers.Values) r.Dispose();
        _vcam.Dispose();
    }
}
