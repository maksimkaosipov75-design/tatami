using System.ComponentModel;
using System.Windows.Media;

namespace Pier.Models;

public class DockItem : INotifyPropertyChanged
{
    public required string Key { get; init; } // exe path, lowercase - identity for merging pinned + running
    public required ImageSource Icon { get; set; }

    private string _displayName = "";
    public required string DisplayName
    {
        get => _displayName;
        set { _displayName = value; OnPropertyChanged(nameof(DisplayName)); }
    }
    public string? LaunchPath { get; set; } // set for pinned items - what to run if not already running
    public nint WindowHandle { get; set; }
    public bool IsPinned { get; set; }
    public bool IsLaunchpad { get; init; }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set { _isRunning = value; OnPropertyChanged(nameof(IsRunning)); }
    }

    private bool _isForeground;
    public bool IsForeground
    {
        get => _isForeground;
        set { _isForeground = value; OnPropertyChanged(nameof(IsForeground)); }
    }

    /// <summary>
    /// Re-raises the properties whose bindings run through a converter that
    /// reads application resources. Changing such a resource does not by itself
    /// invalidate the binding, so the dock calls this after restyling.
    /// </summary>
    public void RefreshBrushes() => OnPropertyChanged(nameof(IsForeground));

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
