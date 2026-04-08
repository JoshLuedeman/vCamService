using vCamService.Core.Services;

namespace vCamService.Core.Tests;

public class FrameBufferTests
{
    [Fact]
    public void Put_And_Get_ReturnsLatestFrame()
    {
        var buf = new FrameBuffer();
        var data = new byte[] { 1, 2, 3, 4 };

        buf.Put(data, 1, 1);
        var (frame, w, h) = buf.Get();

        Assert.Equal(data, frame);
        Assert.Equal(1, w);
        Assert.Equal(1, h);
    }

    [Fact]
    public void Get_WhenEmpty_ReturnsNull()
    {
        var buf = new FrameBuffer();
        var (frame, w, h) = buf.Get();

        Assert.Null(frame);
        Assert.Equal(0, w);
        Assert.Equal(0, h);
    }

    [Fact]
    public void Put_OverwritesPreviousFrame()
    {
        var buf = new FrameBuffer();
        buf.Put(new byte[] { 1, 2, 3, 4 }, 1, 1);
        buf.Put(new byte[] { 9, 8, 7, 6 }, 2, 2);

        var (frame, w, h) = buf.Get();
        Assert.Equal(new byte[] { 9, 8, 7, 6 }, frame);
        Assert.Equal(2, w);
        Assert.Equal(2, h);
    }

    [Fact]
    public void Clear_RemovesFrame()
    {
        var buf = new FrameBuffer();
        buf.Put(new byte[] { 1, 2, 3, 4 }, 1, 1);
        buf.Clear();

        var (frame, _, _) = buf.Get();
        Assert.Null(frame);
        Assert.False(buf.HasFrame);
    }

    [Fact]
    public void HasFrame_ReturnsTrueAfterPut()
    {
        var buf = new FrameBuffer();
        Assert.False(buf.HasFrame);

        buf.Put(new byte[] { 1, 2, 3, 4 }, 1, 1);
        Assert.True(buf.HasFrame);
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentPutAndGet()
    {
        var buf = new FrameBuffer();
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        var writers = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < 1000; i++)
                    buf.Put(new byte[4], 1, 1);
            }
            catch (Exception ex) { exceptions.Add(ex); }
        }));

        var readers = Enumerable.Range(0, 5).Select(_ => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < 1000; i++)
                    buf.Get();
            }
            catch (Exception ex) { exceptions.Add(ex); }
        }));

        await Task.WhenAll(writers.Concat(readers));
        Assert.Empty(exceptions);
    }
}
