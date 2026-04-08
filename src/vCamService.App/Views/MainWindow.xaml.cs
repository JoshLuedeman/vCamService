using vCamService.App.ViewModels;

namespace vCamService.App.Views;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();

        // Wire up the dialog delegate so MainViewModel can show AddStreamDialog
        // without taking a direct dependency on the View layer.
        ViewModel.ShowAddStreamDialog = vm =>
        {
            var dialog = new AddStreamDialog(vm) { Owner = this };
            return dialog.ShowDialog() == true;
        };
    }
}
