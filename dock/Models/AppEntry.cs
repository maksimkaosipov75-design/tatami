using System.Windows.Media;

namespace Pier.Models;

public class AppEntry
{
    public required string Name { get; init; }
    public required string TargetPath { get; init; }
    public required ImageSource Icon { get; init; }
}
