using vCamService.App.ViewModels;

namespace vCamService.App.Views;

public partial class AddStreamDialog : Window
{
    public AddStreamDialog(AddStreamViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
