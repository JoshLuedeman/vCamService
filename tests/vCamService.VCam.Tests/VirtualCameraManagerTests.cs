using vCamService.VCam;

namespace vCamService.VCam.Tests;

/// <summary>
/// Unit tests for VirtualCameraManager.
///
/// Note: tests that actually create COM objects or call Media Foundation P/Invokes
/// require Windows 11 22H2+ and cannot run in a cross-platform CI environment.
/// This file covers the parts that are safe to test anywhere.
/// </summary>
public class VirtualCameraManagerTests
{
    [Fact]
    public void DeviceName_ReturnsExpectedString()
    {
        using var manager = new VirtualCameraManager();
        Assert.Equal("vCamService Camera", manager.DeviceName);
    }

    [Fact]
    public void IsRunning_FalseBeforeStart()
    {
        using var manager = new VirtualCameraManager();
        Assert.False(manager.IsRunning);
    }

    [Fact]
    public void FrameBuffer_IsInitialisedOnConstruction()
    {
        using var manager = new VirtualCameraManager();
        Assert.NotNull(manager.FrameBuffer);
    }

    [Fact]
    public void FrameBuffer_HasFrameAfterSendFrame()
    {
        using var manager = new VirtualCameraManager();

        const int w = 4, h = 4;
        byte[] bgra = new byte[w * h * 4]; // all zeros — valid BGRA data
        manager.SendFrame(bgra, w, h);

        Assert.True(manager.FrameBuffer.HasFrame);
    }

    [Fact]
    public void SendFrame_ThrowsForNullData()
    {
        using var manager = new VirtualCameraManager();
        Assert.Throws<ArgumentNullException>(() => manager.SendFrame(null!, 4, 4));
    }

    [Fact]
    public void Stop_IsIdempotentWhenNotRunning()
    {
        using var manager = new VirtualCameraManager();
        // Should not throw even when called before Start().
        manager.Stop();
        manager.Stop();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var manager = new VirtualCameraManager();
        manager.Dispose();
        manager.Dispose(); // second call must not throw
    }
}
