using vCamService.App.ViewModels;

namespace vCamService.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>
    /// Updates the preview panel to show frames from the given FrameBuffer.
    /// Pass null to clear the preview.
    /// </summary>
    public void UpdatePreviewSource(FrameBuffer? buffer)
    {
        Preview.Source = buffer;
    }
}
