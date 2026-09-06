using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ladeco.Diag.App.ViewModels;
using System.ComponentModel;

namespace Ladeco.Diag.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private ContentDialog? _actionDialog;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        Root.DataContext = viewModel;
        Title = "Ladeco IT Diagnostic Tool";
        Activated += (_, _) => viewModel.StartMonitoring();
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsActionOverlayVisible) && _viewModel.IsActionOverlayVisible)
        {
            _ = ShowActionDialogAsync();
        }
    }

    private async Task ShowActionDialogAsync()
    {
        if (_actionDialog is not null || Root.XamlRoot is null)
        {
            return;
        }

        var actionName = new TextBlock { Text = _viewModel.CurrentActionName, TextWrapping = TextWrapping.Wrap };
        var output = new TextBox
        {
            Text = _viewModel.ActionOutput,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 260
        };
        ScrollViewer.SetVerticalScrollBarVisibility(output, ScrollBarVisibility.Auto);
        var summary = new TextBlock { Text = _viewModel.ActionSummary, TextWrapping = TextWrapping.Wrap };
        _actionDialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = "Actie uitvoeren",
            Content = new StackPanel { Spacing = 12, Children = { actionName, output, summary } },
            PrimaryButtonText = "Stop",
            CloseButtonText = "Verbergen",
            DefaultButton = ContentDialogButton.None
        };

        PropertyChangedEventHandler updateDialog = (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.CurrentActionName)) actionName.Text = _viewModel.CurrentActionName;
            if (args.PropertyName == nameof(MainViewModel.ActionOutput)) output.Text = _viewModel.ActionOutput;
            if (args.PropertyName == nameof(MainViewModel.ActionSummary)) summary.Text = _viewModel.ActionSummary;
        };
        _viewModel.PropertyChanged += updateDialog;
        _actionDialog.PrimaryButtonClick += (_, _) => _viewModel.CancelActionCommand.Execute(null);

        try
        {
            await _actionDialog.ShowAsync();
        }
        finally
        {
            _viewModel.PropertyChanged -= updateDialog;
            _actionDialog = null;
            _viewModel.CloseActionOverlayCommand.Execute(null);
        }
    }
}
