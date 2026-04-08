using System.Runtime.InteropServices;
using vCamService.Core.Services;

namespace vCamService.App.Views;

public partial class PreviewControl : UserControl
{
    // -------------------------------------------------------------------------
    // Dependency property
    // -------------------------------------------------------------------------
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(
            nameof(Source),
            typeof(FrameBuffer),
            typeof(PreviewControl),
            new PropertyMetadata(null));

    public FrameBuffer? Source
    {
        get => (FrameBuffer?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    // -------------------------------------------------------------------------
    // Fields
    // -------------------------------------------------------------------------
    private readonly DispatcherTimer _timer;
    private WriteableBitmap? _bitmap;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------
    public PreviewControl()
    {
        InitializeComponent();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100) // ~10 fps
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    // -------------------------------------------------------------------------
    // Timer callback — runs on UI thread
    // -------------------------------------------------------------------------
    private void OnTick(object? sender, EventArgs e)
    {
        var source = Source;
        if (source is null || !source.HasFrame)
        {
            ShowNoStream();
            return;
        }

        var (data, width, height) = source.Get();
        if (data is null || width <= 0 || height <= 0)
        {
            ShowNoStream();
            return;
        }

        // Re-create the bitmap if dimensions changed
        if (_bitmap is null || _bitmap.PixelWidth != width || _bitmap.PixelHeight != height)
        {
            _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            FrameImage.Source = _bitmap;
        }

        _bitmap.Lock();
        try
        {
            Marshal.Copy(data, 0, _bitmap.BackBuffer, data.Length);
            _bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
        }
        finally
        {
            _bitmap.Unlock();
        }

        FrameImage.Visibility  = Visibility.Visible;
        NoStreamText.Visibility = Visibility.Collapsed;
    }

    private void ShowNoStream()
    {
        FrameImage.Visibility  = Visibility.Collapsed;
        NoStreamText.Visibility = Visibility.Visible;
    }
}
