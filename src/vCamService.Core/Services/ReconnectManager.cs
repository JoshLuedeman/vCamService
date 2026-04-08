namespace vCamService.Core.Services;

/// <summary>
/// Exponential backoff reconnection manager.
/// Delay = min(base * 2^attempt, maxDelay) + uniform jitter in [0, jitterFactor * delay].
/// </summary>
public sealed class ReconnectManager
{
    private readonly double _baseDelaySeconds;
    private readonly double _maxDelaySeconds;
    private readonly double _multiplier;
    private readonly double _jitterFactor;
    private int _attempt;
    private static readonly Random _rng = new();

    public ReconnectManager(
        double baseDelaySeconds = 1.0,
        double maxDelaySeconds = 30.0,
        double multiplier = 2.0,
        double jitterFactor = 0.5)
    {
        _baseDelaySeconds = baseDelaySeconds;
        _maxDelaySeconds = maxDelaySeconds;
        _multiplier = multiplier;
        _jitterFactor = jitterFactor;
    }

    public int Attempt => _attempt;

    public TimeSpan NextDelay()
    {
        double delay = Math.Min(_baseDelaySeconds * Math.Pow(_multiplier, _attempt), _maxDelaySeconds);
        double jitter = _rng.NextDouble() * _jitterFactor * delay;
        return TimeSpan.FromSeconds(delay + jitter);
    }

    /// <summary>
    /// Waits for the backoff duration, incrementing attempt count.
    /// Returns false if cancellation was requested before wait completed.
    /// </summary>
    public async Task<bool> WaitAsync(CancellationToken ct)
    {
        var delay = NextDelay();
        _attempt++;
        try
        {
            await Task.Delay(delay, ct).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public void Reset()
    {
        _attempt = 0;
    }
}
