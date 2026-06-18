using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AtlasDashboard.Models
{
    public class SensorEvent
    {
        public string Timestamp { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public bool IsOn { get; set; }
    }

    public static class SensorDataStore
    {
        // Danh sách lưu trữ log cho từng cảm biến
        public static ObservableCollection<SensorEvent> LogSensorA { get; } = new ObservableCollection<SensorEvent>();
        public static ObservableCollection<SensorEvent> LogSensorB { get; } = new ObservableCollection<SensorEvent>();
        public static ObservableCollection<SensorEvent> LogSensorC { get; } = new ObservableCollection<SensorEvent>();
        public static ObservableCollection<SensorEvent> LogSensorD { get; } = new ObservableCollection<SensorEvent>();

        // Trạng thái hiện tại
        public static bool StateSensorA { get; private set; } = false;
        public static bool StateSensorB { get; private set; } = false;
        public static bool StateSensorC { get; private set; } = false;
        public static bool StateSensorD { get; private set; } = false;

        public static void RecordEvent(string sensorId, bool isOn)
        {
            var evt = new SensorEvent
            {
                Timestamp = DateTime.Now.ToString("HH:mm:ss.fff"),
                State = isOn ? "ON" : "OFF",
                IsOn = isOn
            };

            switch (sensorId.ToLower())
            {
                case "a":
                    StateSensorA = isOn;
                    App.Current.Dispatcher.Invoke(() => LogSensorA.Add(evt));
                    break;
                case "b":
                    StateSensorB = isOn;
                    App.Current.Dispatcher.Invoke(() => LogSensorB.Add(evt));
                    break;
                case "c":
                    StateSensorC = isOn;
                    App.Current.Dispatcher.Invoke(() => LogSensorC.Add(evt));
                    break;
                case "d":
                    StateSensorD = isOn;
                    App.Current.Dispatcher.Invoke(() => LogSensorD.Add(evt));
                    break;
            }
        }
    }
}
