using Microsoft.UI.Xaml;
using Ladeco.Diag.App.ViewModels;

namespace Ladeco.Diag.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        Root.DataContext = viewModel;
        Title = "Ladeco IT Diagnostic Tool";
        Activated += (_, _) => viewModel.StartMonitoring();
    }
}
