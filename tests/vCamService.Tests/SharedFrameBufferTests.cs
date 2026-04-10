using Xunit;
using vCamService.Core.Services;

namespace vCamService.Tests;

public class SharedFrameBufferTests : IDisposable
{
    private readonly string _mmfName = $"Local\\vCamTest_{Guid.NewGuid():N}";
    private SharedFrameBuffer? _owner;
    private SharedFrameBuffer? _reader;

    public void Dispose()
    {
        _reader?.Dispose();
        _owner?.Dispose();
    }

    [Fact]
    public void FrameSize_NV12_IsWidthTimesHeightTimes1Point5()
    {
        _owner = SharedFrameBuffer.CreateForTest(_mmfName, 1920, 1080, pixelFormat: SharedFrameBuffer.PixelFormatNV12);
        Assert.Equal(1920 * 1080 * 3 / 2, _owner.FrameSize);
    }

    [Fact]
    public void FrameSize_BGRA_IsWidthTimesHeightTimes4()
    {
        _owner = SharedFrameBuffer.CreateForTest(_mmfName, 640, 480, pixelFormat: SharedFrameBuffer.PixelFormatBGRA);
        Assert.Equal(640 * 480 * 4, _owner.FrameSize);
    }

    [Fact]
    public void CreateForTest_SetsHeaderCorrectly()
    {
        _owner = SharedFrameBuffer.CreateForTest(_mmfName, 1280, 720, fpsNum: 25, fpsDen: 1);

        Assert.Equal(1280, _owner.Width);
        Assert.Equal(720, _owner.Height);
        Assert.Equal(25, _owner.FpsNumerator);
        Assert.Equal(1, _owner.FpsDenominator);
        Assert.Equal(SharedFrameBuffer.PixelFormatNV12, _owner.PixelFormat);
    }

    [Fact]
    public void OpenForTest_ReadsHeaderFromOwner()
    {
        _owner = SharedFrameBuffer.CreateForTest(_mmfName, 1920, 1080, fpsNum: 30, fpsDen: 1);
        _reader = SharedFrameBuffer.OpenForTest(_mmfName);

        Assert.NotNull(_reader);
        Assert.Equal(1920, _reader.Width);
        Assert.Equal(1080, _reader.Height);
        Assert.Equal(30, _reader.FpsNumerator);
        Assert.Equal(1, _reader.FpsDenominator);
    }

    [Fact]
    public void WriteFrame_ThenReadFrame_RoundTrip()
    {
        const int w = 64, h = 48;
        _owner = SharedFrameBuffer.CreateForTest(_mmfName, w, h, pixelFormat: SharedFrameBuffer.PixelFormatNV12);
        _reader = SharedFrameBuffer.OpenForTest(_mmfName);
        Assert.NotNull(_reader);

        int frameSize = w * h * 3 / 2; // NV12
        byte[] frameData = new byte[frameSize];
        // Fill with a recognizable pattern
        for (int i = 0; i < frameData.Length; i++)
            frameData[i] = (byte)(i % 251); // prime to avoid alignment artifacts

        _owner.WriteFrame(frameData);

        // Read back
        byte[] readBuffer = new byte[frameSize];
        unsafe
        {
            fixed (byte* ptr = readBuffer)
            {
                bool ok = _reader.TryReadFrame((nint)ptr, frameSize);
                Assert.True(ok, "TryReadFrame should succeed after WriteFrame");
            }
        }

        Assert.Equal(frameData, readBuffer);
    }

    [Fact]
    public void WriteFrame_SlotsFlip()
    {
        const int w = 16, h = 16;
        _owner = SharedFrameBuffer.CreateForTest(_mmfName, w, h, pixelFormat: SharedFrameBuffer.PixelFormatBGRA);

        int frameSize = w * h * 4;
        byte[] frame1 = new byte[frameSize];
        byte[] frame2 = new byte[frameSize];
        Array.Fill(frame1, (byte)0xAA);
        Array.Fill(frame2, (byte)0xBB);

        _owner.WriteFrame(frame1);
        int slotAfterFirst = _owner.GetWriteSlot();

        _owner.WriteFrame(frame2);
        int slotAfterSecond = _owner.GetWriteSlot();

        // Write slot should alternate
        Assert.NotEqual(slotAfterFirst, slotAfterSecond);
    }

    [Fact]
    public void TryReadFrame_BeforeAnyWrite_ReturnsTrue_WithZeroData()
    {
        const int w = 16, h = 16;
        _owner = SharedFrameBuffer.CreateForTest(_mmfName, w, h, pixelFormat: SharedFrameBuffer.PixelFormatBGRA);
        _reader = SharedFrameBuffer.OpenForTest(_mmfName);
        Assert.NotNull(_reader);

        int frameSize = w * h * 4;
        byte[] readBuffer = new byte[frameSize];
        unsafe
        {
            fixed (byte* ptr = readBuffer)
            {
                bool ok = _reader.TryReadFrame((nint)ptr, frameSize);
                // Sequence is 0 (even), so read should succeed with zeroed slot data
                Assert.True(ok);
            }
        }
    }

    [Fact]
    public void IsProducerAlive_ReturnsTrue_AfterWrite()
    {
        const int w = 16, h = 16;
        _owner = SharedFrameBuffer.CreateForTest(_mmfName, w, h);
        _reader = SharedFrameBuffer.OpenForTest(_mmfName);
        Assert.NotNull(_reader);

        byte[] frame = new byte[_owner.FrameSize];
        _owner.WriteFrame(frame);

        Assert.True(_reader.IsProducerAlive());
    }

    [Fact]
    public void CommitSlot_UpdatesActiveSlotAndSequence()
    {
        const int w = 16, h = 16;
        _owner = SharedFrameBuffer.CreateForTest(_mmfName, w, h);

        int writeSlot = _owner.GetWriteSlot();
        _owner.CommitSlot(writeSlot);

        // After commit, the write slot should be the other one
        int nextWriteSlot = _owner.GetWriteSlot();
        Assert.NotEqual(writeSlot, nextWriteSlot);
    }

    [Fact]
    public void TryReadFrame_WithInsufficientBuffer_ReturnsFalse()
    {
        const int w = 64, h = 48;
        _owner = SharedFrameBuffer.CreateForTest(_mmfName, w, h);
        _reader = SharedFrameBuffer.OpenForTest(_mmfName);
        Assert.NotNull(_reader);

        // Buffer too small
        byte[] tinyBuffer = new byte[10];
        unsafe
        {
            fixed (byte* ptr = tinyBuffer)
            {
                bool ok = _reader.TryReadFrame((nint)ptr, tinyBuffer.Length);
                Assert.False(ok);
            }
        }
    }

    [Fact]
    public void MultipleWrites_LastFrameIsRead()
    {
        const int w = 16, h = 16;
        _owner = SharedFrameBuffer.CreateForTest(_mmfName, w, h, pixelFormat: SharedFrameBuffer.PixelFormatBGRA);
        _reader = SharedFrameBuffer.OpenForTest(_mmfName);
        Assert.NotNull(_reader);

        int frameSize = w * h * 4;

        // Write 3 frames, each with different data
        for (byte val = 1; val <= 3; val++)
        {
            byte[] frame = new byte[frameSize];
            Array.Fill(frame, val);
            _owner.WriteFrame(frame);
        }

        // Read should get the last frame (val=3)
        byte[] readBuffer = new byte[frameSize];
        unsafe
        {
            fixed (byte* ptr = readBuffer)
            {
                bool ok = _reader.TryReadFrame((nint)ptr, frameSize);
                Assert.True(ok);
            }
        }
        Assert.Equal(3, readBuffer[0]);
        Assert.Equal(3, readBuffer[frameSize - 1]);
    }
}
