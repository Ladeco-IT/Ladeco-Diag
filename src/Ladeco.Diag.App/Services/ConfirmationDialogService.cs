using Ladeco.Diag.Application.Abstractions;
using Microsoft.UI.Xaml.Controls;

namespace Ladeco.Diag.App.Services;

public sealed class ConfirmationDialogService : IConfirmationDialogService
{
    private readonly ILocalizationService _localization;

    public ConfirmationDialogService(ILocalizationService localization)
    {
        _localization = localization;
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var xamlRoot = App.MainWindow?.Content.XamlRoot;
        if (xamlRoot is null)
        {
            return false;
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = _localization["Dialog.Yes"],
            CloseButtonText = _localization["Dialog.No"],
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
