using System.Windows;
using Ladeco.Diag.App.ViewModels;

namespace Ladeco.Diag.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
