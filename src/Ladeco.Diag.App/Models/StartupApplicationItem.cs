using System.Windows.Input;

namespace Ladeco.Diag.App.Models;

public enum StartupSource
{
	CurrentUserRegistry,
	LocalMachineRegistry64,
	LocalMachineRegistry32,
	UserStartupFolder,
	CommonStartupFolder
}

public sealed record StartupApplicationItem(
	string Name,
	string Command,
	bool IsEnabled,
	ICommand ToggleCommand,
	StartupSource Source,
	string SourceDescription);