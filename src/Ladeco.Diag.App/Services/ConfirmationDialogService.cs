using System.Windows;

namespace Ladeco.Diag.App.Services;

public sealed class ConfirmationDialogService : IConfirmationDialogService
{
    public bool Confirm(string title, string message)
    {
        return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }
}
