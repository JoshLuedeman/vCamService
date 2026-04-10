using Xunit;
using vCamService.Core.Services;

namespace vCamService.Tests;

public class VideoStreamReaderParsingTests
{
    [Fact]
    public void ParseProbeOutput_StandardFormat()
    {
        var (w, h, fpsNum, fpsDen) = VideoStreamReader.ParseProbeOutput("1920,1080,25/1");
        Assert.Equal(1920, w);
        Assert.Equal(1080, h);
        Assert.Equal(25, fpsNum);
        Assert.Equal(1, fpsDen);
    }

    [Fact]
    public void ParseProbeOutput_NtscFrameRate()
    {
        var (w, h, fpsNum, fpsDen) = VideoStreamReader.ParseProbeOutput("1920,1080,30000/1001");
        Assert.Equal(1920, w);
        Assert.Equal(1080, h);
        Assert.Equal(30000, fpsNum);
        Assert.Equal(1001, fpsDen);
    }

    [Fact]
    public void ParseProbeOutput_720p()
    {
        var (w, h, fpsNum, fpsDen) = VideoStreamReader.ParseProbeOutput("1280,720,30/1");
        Assert.Equal(1280, w);
        Assert.Equal(720, h);
        Assert.Equal(30, fpsNum);
        Assert.Equal(1, fpsDen);
    }

    [Fact]
    public void ParseProbeOutput_4K()
    {
        var (w, h, fpsNum, fpsDen) = VideoStreamReader.ParseProbeOutput("3840,2160,60/1");
        Assert.Equal(3840, w);
        Assert.Equal(2160, h);
        Assert.Equal(60, fpsNum);
        Assert.Equal(1, fpsDen);
    }

    [Fact]
    public void ParseProbeOutput_EmptyString_ReturnsFallback()
    {
        var (w, h, fpsNum, fpsDen) = VideoStreamReader.ParseProbeOutput("");
        Assert.Equal(1920, w);
        Assert.Equal(1080, h);
        Assert.Equal(25, fpsNum);
        Assert.Equal(1, fpsDen);
    }

    [Fact]
    public void ParseProbeOutput_Null_ReturnsFallback()
    {
        var (w, h, fpsNum, fpsDen) = VideoStreamReader.ParseProbeOutput(null!);
        Assert.Equal(1920, w);
        Assert.Equal(1080, h);
        Assert.Equal(25, fpsNum);
        Assert.Equal(1, fpsDen);
    }

    [Fact]
    public void ParseProbeOutput_GarbageInput_ReturnsFallback()
    {
        var (w, h, fpsNum, fpsDen) = VideoStreamReader.ParseProbeOutput("no video streams found");
        Assert.Equal(1920, w);
        Assert.Equal(1080, h);
        Assert.Equal(25, fpsNum);
        Assert.Equal(1, fpsDen);
    }

    [Fact]
    public void ParseProbeOutput_InvalidFps_DefaultsTo30()
    {
        var (w, h, fpsNum, fpsDen) = VideoStreamReader.ParseProbeOutput("1920,1080,invalid");
        Assert.Equal(1920, w);
        Assert.Equal(1080, h);
        Assert.Equal(30, fpsNum); // fallback for unparseable fps
        Assert.Equal(1, fpsDen);
    }

    [Fact]
    public void ParseProbeOutput_ZeroWidth_ReturnsFallback()
    {
        var (w, h, fpsNum, fpsDen) = VideoStreamReader.ParseProbeOutput("0,1080,25/1");
        Assert.Equal(1920, w);
        Assert.Equal(1080, h);
        Assert.Equal(25, fpsNum);
        Assert.Equal(1, fpsDen);
    }

    [Fact]
    public void ParseProbeOutput_NegativeHeight_ReturnsFallback()
    {
        var (w, h, fpsNum, fpsDen) = VideoStreamReader.ParseProbeOutput("1920,-1,25/1");
        Assert.Equal(1920, w);
        Assert.Equal(1080, h);
        Assert.Equal(25, fpsNum);
        Assert.Equal(1, fpsDen);
    }

    [Fact]
    public void ParseProbeOutput_ExtraCommas_StillParses()
    {
        // Some ffprobe versions may output extra fields
        var (w, h, fpsNum, fpsDen) = VideoStreamReader.ParseProbeOutput("1920,1080,25/1,extra");
        Assert.Equal(1920, w);
        Assert.Equal(1080, h);
        Assert.Equal(25, fpsNum);
        Assert.Equal(1, fpsDen);
    }

    [Fact]
    public void ParseProbeOutput_ZeroDenominator_DefaultsTo30()
    {
        var (w, h, fpsNum, fpsDen) = VideoStreamReader.ParseProbeOutput("1920,1080,25/0");
        Assert.Equal(1920, w);
        Assert.Equal(1080, h);
        Assert.Equal(30, fpsNum); // fallback
        Assert.Equal(1, fpsDen);
    }
}
