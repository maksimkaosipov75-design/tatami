using System.ComponentModel;
using System.Windows.Media;

namespace OmarchyDock.Models;

public class DockItem : INotifyPropertyChanged
{
    public required string Key { get; init; } // exe path, lowercase - identity for merging pinned + running
    public required string DisplayName { get; set; }
    public required ImageSource Icon { get; set; }
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

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
