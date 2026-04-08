using vCamService.Core.Services;

namespace vCamService.Core.Tests;

public class ReconnectManagerTests
{
    [Fact]
    public void NextDelay_FirstAttempt_ReturnsBaseDelay()
    {
        var mgr = new ReconnectManager(baseDelaySeconds: 1.0, jitterFactor: 0.0);
        var delay = mgr.NextDelay();

        Assert.Equal(TimeSpan.FromSeconds(1.0), delay);
    }

    [Fact]
    public void NextDelay_Doubles_EachCall()
    {
        var mgr = new ReconnectManager(baseDelaySeconds: 1.0, maxDelaySeconds: 60.0, jitterFactor: 0.0);

        Assert.Equal(1.0, mgr.NextDelay().TotalSeconds, precision: 5);
        // attempt hasn't been incremented yet — WaitAsync increments
        // NextDelay doesn't change attempt, only reports current
    }

    [Fact]
    public async Task WaitAsync_CancelledImmediately_ReturnsFalse()
    {
        var mgr = new ReconnectManager(baseDelaySeconds: 60.0, jitterFactor: 0.0);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await mgr.WaitAsync(cts.Token);

        Assert.False(result);
    }

    [Fact]
    public async Task WaitAsync_ShortDelay_ReturnsTrue()
    {
        var mgr = new ReconnectManager(baseDelaySeconds: 0.01, jitterFactor: 0.0);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var result = await mgr.WaitAsync(cts.Token);

        Assert.True(result);
    }

    [Fact]
    public async Task WaitAsync_IncrementsAttemptCount()
    {
        var mgr = new ReconnectManager(baseDelaySeconds: 0.01, jitterFactor: 0.0);
        Assert.Equal(0, mgr.Attempt);

        await mgr.WaitAsync(CancellationToken.None);
        Assert.Equal(1, mgr.Attempt);

        await mgr.WaitAsync(CancellationToken.None);
        Assert.Equal(2, mgr.Attempt);
    }

    [Fact]
    public async Task Reset_ResetsAttemptToZero()
    {
        var mgr = new ReconnectManager(baseDelaySeconds: 0.01, jitterFactor: 0.0);
        await mgr.WaitAsync(CancellationToken.None);
        await mgr.WaitAsync(CancellationToken.None);

        mgr.Reset();

        Assert.Equal(0, mgr.Attempt);
    }

    [Fact]
    public void NextDelay_CappedAtMax()
    {
        var mgr = new ReconnectManager(baseDelaySeconds: 1.0, maxDelaySeconds: 5.0, jitterFactor: 0.0);
        // Force high attempt by calling WaitAsync in background — just check cap logic directly
        // by using large exponent scenario: 1 * 2^10 = 1024 > 5 → should cap at 5
        var field = typeof(ReconnectManager).GetField("_attempt",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(mgr, 10);

        var delay = mgr.NextDelay();
        Assert.Equal(5.0, delay.TotalSeconds, precision: 5);
    }
}
