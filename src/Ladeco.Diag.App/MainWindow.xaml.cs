using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ladeco.Diag.App.Services;
using Ladeco.Diag.App.ViewModels;
using Ladeco.Diag.Application.Abstractions;
using System.ComponentModel;

namespace Ladeco.Diag.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ILocalUserAccountService _localUserAccountService;
    private readonly IConfirmationDialogService _confirmationDialog;
    private readonly ILocalizationService _localization;
    private ContentDialog? _actionDialog;

    public MainWindow(MainViewModel viewModel, ILocalUserAccountService localUserAccountService, IConfirmationDialogService confirmationDialog, ILocalizationService localization)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _localUserAccountService = localUserAccountService;
        _confirmationDialog = confirmationDialog;
        _localization = localization;
        Root.DataContext = viewModel;
        Title = "Ladeco IT Diagnostic Tool";
        Activated += (_, _) => viewModel.StartMonitoring();
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private async void CreateAdministratorButton_Click(object sender, RoutedEventArgs e)
    {
        var userName = new TextBox { Header = _localization["Account.UserName"], PlaceholderText = _localization["Account.UserNamePlaceholder"] };
        var password = new PasswordBox { Header = _localization["Account.Password"] };
        var passwordConfirmation = new PasswordBox { Header = _localization["Account.PasswordConfirmation"] };
        var content = new StackPanel { Spacing = 12, Children = { userName, password, passwordConfirmation } };
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = _localization["Account.CreateTitle"],
            Content = content,
            PrimaryButtonText = _localization["Account.Create"],
            CloseButtonText = _localization["Dialog.No"],
            DefaultButton = ContentDialogButton.Primary
        };

        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(userName.Text) || string.IsNullOrEmpty(password.Password))
            {
                args.Cancel = true;
            }
            else if (!string.Equals(password.Password, passwordConfirmation.Password, StringComparison.Ordinal))
            {
                args.Cancel = true;
            }
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var result = await _localUserAccountService.CreateAdministratorAsync(userName.Text.Trim(), password.Password);
        if (!result.Success)
        {
            await ShowMessageAsync(_localization["Account.Failed"], result.Message);
            return;
        }

        var logoff = await _confirmationDialog.ConfirmAsync(_localization["Account.Created"], _localization["Account.LogoffPrompt"]);
        if (logoff)
        {
            _localUserAccountService.LogoffCurrentSession();
        }
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = _localization["Dialog.No"]
        };
        await dialog.ShowAsync();
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
