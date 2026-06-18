using CommunityToolkit.Mvvm.ComponentModel;

namespace AtlasDashboard.Models;

public partial class SensorModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusBadgeColor))]
    [NotifyPropertyChangedFor(nameof(StatusForegroundColor))]
    [NotifyPropertyChangedFor(nameof(Opacity))]
    private bool isActive;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string IconColor { get; set; } = "#3498DB";
    public string IconBackground { get; set; } = "#F0F4FF";

    public string StatusText => IsActive ? "ON" : "OFF";
    public string StatusBadgeColor => IsActive ? "#10B981" : "#CCCCCC";
    public string StatusForegroundColor => IsActive ? "#FFFFFF" : "#555555";
    public double Opacity => IsActive ? 1.0 : 0.5;
}
