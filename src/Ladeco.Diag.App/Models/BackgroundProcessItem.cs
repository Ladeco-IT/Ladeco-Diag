using System.Windows.Input;

namespace Ladeco.Diag.App.Models;

public sealed record BackgroundProcessItem(int ProcessId, string Name, string MemoryUsage, ICommand StopCommand);