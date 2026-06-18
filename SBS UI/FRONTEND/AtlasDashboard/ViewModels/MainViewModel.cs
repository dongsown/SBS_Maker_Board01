using System.Collections.ObjectModel;
using AtlasDashboard.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AtlasDashboard.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private int telemetryDisplayValue;

    public ObservableCollection<SensorModel> Sensors { get; } = new();

    public MainViewModel()
    {
        Sensors.Add(new SensorModel { Name = "CẢM BIẾN NỒNG ĐỘ CHẤT 1", Description = "Nồng độ chất 1",    Icon = "🧪", IconBackground = "#FFF3F0", IconColor = "#000000", IsActive = false });
        Sensors.Add(new SensorModel { Name = "CẢM BIẾN NỒNG ĐỘ CHẤT 2", Description = "Nồng độ chất 2",    Icon = "🧪", IconBackground = "#F0F4FF", IconColor = "#3498DB", IsActive = false });
        Sensors.Add(new SensorModel { Name = "CẢM BIẾN ĐỘ CAO",          Description = "Đo lường độ cao",   Icon = "⛰️",  IconBackground = "#F5F5F5", IconColor = "#888888", IsActive = false });
        Sensors.Add(new SensorModel { Name = "CẢM BIẾN NHIỆT ĐỘ",        Description = "Nhiệt độ môi trường", Icon = "🌡️", IconBackground = "#F0FFF8", IconColor = "#10B981", IsActive = false });

        UpdateTelemetryValue();
    }

    /// <summary>
    /// Nhận chuỗi lệnh từ Middleware, set trạng thái cảm biến theo lệnh.
    /// Định dạng: "{key}:{ON|OFF}"  (ví dụ: "a:ON", "b:OFF")
    /// Mapping key → sensor index: a→0  b→1  c→2  d→3
    /// </summary>
    /// <returns>Index của sensor được thay đổi, hoặc -1 nếu không nhận dạng được.</returns>
    public int SetSensor(string command)
    {
        // Parse "x:ON" hoặc "x:OFF"
        var parts = command.Trim().Split(':');
        if (parts.Length != 2) return -1;

        string key   = parts[0].Trim().ToLower();
        string state = parts[1].Trim().ToUpper();

        if (state != "ON" && state != "OFF") return -1;

        int index = key switch
        {
            "a" => 0,
            "b" => 1,
            "c" => 2,
            "d" => 3,
            _   => -1
        };

        if (index < 0 || index >= Sensors.Count) return -1;

        Sensors[index].IsActive = state == "ON";
        UpdateTelemetryValue();
        return index;
    }

    /// <summary>
    /// Đặt tất cả cảm biến về trạng thái OFF (dùng khi reset về Idle).
    /// </summary>
    public void ResetAllSensors()
    {
        foreach (var sensor in Sensors)
            sensor.IsActive = false;
        UpdateTelemetryValue();
    }

    private void UpdateTelemetryValue()
    {
        if (Sensors.Count == 0) return;

        int activeCount = 0;
        foreach (var sensor in Sensors)
        {
            if (sensor.IsActive) activeCount++;
        }

        TelemetryDisplayValue = (int)((activeCount / (double)Sensors.Count) * 100);
    }
}
