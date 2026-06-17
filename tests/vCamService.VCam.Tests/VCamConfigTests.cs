using vCamService.VCam;

namespace vCamService.VCam.Tests;

public class VCamConfigTests
{
    [Fact]
    public void Defaults_Are1280x720At30Fps()
    {
        var cfg = new VCamConfig();

        Assert.Equal(1280, cfg.Width);
        Assert.Equal(720, cfg.Height);
        Assert.Equal(30, cfg.Fps);
    }

    [Fact]
    public void FrameDuration100Ns_At30Fps_Returns333333()
    {
        var cfg = new VCamConfig(Fps: 30);
        Assert.Equal(333_333L, cfg.FrameDuration100Ns);
    }

    [Fact]
    public void FrameDuration100Ns_At60Fps_Returns166666()
    {
        var cfg = new VCamConfig(Fps: 60);
        Assert.Equal(166_666L, cfg.FrameDuration100Ns);
    }

    [Theory]
    [InlineData(0, 720, 30)]
    [InlineData(1280, 0, 30)]
    [InlineData(1280, 720, 0)]
    [InlineData(-1, 720, 30)]
    [InlineData(1280, -1, 30)]
    [InlineData(1280, 720, -1)]
    public void Constructor_ThrowsForInvalidValues(int width, int height, int fps)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new VCamConfig(width, height, fps));
    }

    [Fact]
    public void CustomValues_ArePreserved()
    {
        var cfg = new VCamConfig(1920, 1080, 60);

        Assert.Equal(1920, cfg.Width);
        Assert.Equal(1080, cfg.Height);
        Assert.Equal(60, cfg.Fps);
    }

    [Fact]
    public void RecordEquality_WorksForSameValues()
    {
        var a = new VCamConfig(1280, 720, 30);
        var b = new VCamConfig(1280, 720, 30);

        Assert.Equal(a, b);
    }

    [Fact]
    public void RecordEquality_DiffersForDifferentValues()
    {
        var a = new VCamConfig(1280, 720, 30);
        var b = new VCamConfig(1920, 1080, 60);

        Assert.NotEqual(a, b);
    }
}
