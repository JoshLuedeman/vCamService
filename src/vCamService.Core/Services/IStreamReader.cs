using vCamService.Core.Models;

namespace vCamService.Core.Services;

public interface IStreamReader : IDisposable
{
    StreamStatus Status { get; }
    FrameBuffer FrameBuffer { get; }
    event Action<StreamStatus>? StatusChanged;
    Task StartAsync(StreamConfig config, CancellationToken ct);
    void Stop();
}
