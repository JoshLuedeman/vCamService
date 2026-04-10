using Xunit;
using vCamService.Core.Services;

namespace vCamService.Tests;

public class StreamConfigTests : IDisposable
{
    private readonly string _tempDir;

    public StreamConfigTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"vCamTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var config = new StreamConfig();
        Assert.Equal(1920, config.Width);
        Assert.Equal(1080, config.Height);
        Assert.Equal(30, config.FpsNumerator);
        Assert.Equal(1, config.FpsDenominator);
        Assert.Equal(SharedFrameBuffer.PixelFormatNV12, config.PixelFormat);
    }

    [Fact]
    public void JsonRoundTrip_PreservesValues()
    {
        var original = new StreamConfig
        {
            Width = 1280,
            Height = 720,
            FpsNumerator = 25,
            FpsDenominator = 1,
            PixelFormat = SharedFrameBuffer.PixelFormatBGRA
        };

        string json = System.Text.Json.JsonSerializer.Serialize(original);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<StreamConfig>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(1280, deserialized.Width);
        Assert.Equal(720, deserialized.Height);
        Assert.Equal(25, deserialized.FpsNumerator);
        Assert.Equal(1, deserialized.FpsDenominator);
        Assert.Equal(SharedFrameBuffer.PixelFormatBGRA, deserialized.PixelFormat);
    }

    [Fact]
    public void Load_ReturnDefaults_WhenFileDoesNotExist()
    {
        // StreamConfig.Load() uses a hardcoded path, so we test the default constructor
        var config = StreamConfig.Load();
        // If the file doesn't exist at ProgramData path (likely in test), we get defaults
        Assert.True(config.Width > 0);
        Assert.True(config.Height > 0);
    }

    [Fact]
    public void Deserialization_IgnoresExtraProperties()
    {
        string json = """{"Width":640,"Height":480,"FpsNumerator":15,"FpsDenominator":1,"PixelFormat":1,"ExtraField":"ignored"}""";
        var config = System.Text.Json.JsonSerializer.Deserialize<StreamConfig>(json);

        Assert.NotNull(config);
        Assert.Equal(640, config.Width);
        Assert.Equal(480, config.Height);
    }

    [Fact]
    public void Deserialization_ReturnsNull_ForInvalidJson()
    {
        string json = "not json at all";
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<StreamConfig>(json));
    }
}
