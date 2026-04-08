using System.Text.Json;
using vCamService.Core.Models;
using vCamService.Core.Services;

namespace vCamService.Core.Tests;

public class ConfigServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigServiceTestable _service;

    public ConfigServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"vCamService_tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _service = new ConfigServiceTestable(_tempDir);
    }

    [Fact]
    public void Load_WhenFileNotExists_ReturnsDefault()
    {
        var config = _service.Load();

        Assert.NotNull(config);
        Assert.Equal(1, config.ConfigVersion);
        Assert.Empty(config.Streams);
        Assert.Null(config.ActiveStreamId);
    }

    [Fact]
    public void Save_And_Load_RoundTrip()
    {
        var original = new AppConfig
        {
            ActiveStreamId = "test-id",
            VCamWidth = 1920,
            VCamHeight = 1080,
            VCamFps = 25,
            MinimizeToTray = false,
            Streams = new List<StreamConfig>
            {
                new StreamConfig
                {
                    Id = "test-id",
                    Name = "Test Stream",
                    Url = "rtsp://192.168.1.1:554/stream",
                    Protocol = "rtsp",
                    Width = 1920,
                    Height = 1080,
                    Fps = 25,
                    RtspTransport = "tcp",
                    Enabled = true
                }
            }
        };

        _service.Save(original);
        var loaded = _service.Load();

        Assert.Equal(original.ActiveStreamId, loaded.ActiveStreamId);
        Assert.Equal(original.VCamWidth, loaded.VCamWidth);
        Assert.Equal(original.VCamHeight, loaded.VCamHeight);
        Assert.Equal(original.VCamFps, loaded.VCamFps);
        Assert.Equal(original.MinimizeToTray, loaded.MinimizeToTray);
        Assert.Single(loaded.Streams);
        Assert.Equal("Test Stream", loaded.Streams[0].Name);
        Assert.Equal("rtsp://192.168.1.1:554/stream", loaded.Streams[0].Url);
    }

    [Fact]
    public void Save_IsAtomic_NoCorruptFile()
    {
        // After save, config file should be valid JSON
        var config = new AppConfig { ActiveStreamId = "x" };
        _service.Save(config);

        var path = Path.Combine(_tempDir, "config.json");
        Assert.True(File.Exists(path));

        var json = File.ReadAllText(path);
        var parsed = JsonSerializer.Deserialize<AppConfig>(json,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.NotNull(parsed);
        Assert.Equal("x", parsed!.ActiveStreamId);
    }

    [Fact]
    public void Save_TempFileNotLeft_OnSuccess()
    {
        _service.Save(new AppConfig());
        var tmpPath = Path.Combine(_tempDir, "config.json.tmp");
        Assert.False(File.Exists(tmpPath));
    }

    [Fact]
    public void Load_WithCorruptJson_ReturnsDefault()
    {
        var path = Path.Combine(_tempDir, "config.json");
        File.WriteAllText(path, "{ this is not valid json }");

        var config = _service.Load();
        Assert.NotNull(config);
        Assert.Empty(config.Streams);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}

/// <summary>Testable subclass that uses a custom config directory.</summary>
internal sealed class ConfigServiceTestable : ConfigService
{
    public ConfigServiceTestable(string dir) : base(dir) { }
}
