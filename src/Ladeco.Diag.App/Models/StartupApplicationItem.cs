using System.Windows.Input;

namespace Ladeco.Diag.App.Models;

public sealed record StartupApplicationItem(string Name, string Command, bool IsEnabled, ICommand ToggleCommand);