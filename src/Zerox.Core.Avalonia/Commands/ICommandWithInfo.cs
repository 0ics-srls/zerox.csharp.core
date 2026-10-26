using System.Windows.Input;

namespace Zerox.Core.Avalonia.Commands;

// What menus and toolbars see of a command.
public interface ICommandWithInfo : ICommand
{
    string? Label { get; }

    // An icon resource key.
    string? Icon { get; }
}
