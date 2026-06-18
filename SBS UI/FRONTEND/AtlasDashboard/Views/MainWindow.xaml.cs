using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Middleware;

namespace AtlasDashboard.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const string LcdPlaceholder = "TYPE ANYTHING YOU WANT";

    // ── UART (Middleware) ─────────────────────────────────────
    private readonly UartService _uart = new();

    // ══════════════════════════════════════════════════════════
    //  State Machine
    //
    //  Idle       → Auto/Manual enabled  |  Start/Stop DISABLED  |  Cycles OFF
    //  ModeManual → Auto DISABLED        |  Start/Stop enabled   |  Display+Input ON
    //  ModeAuto   → Manual DISABLED      |  Start/Stop enabled   |  Display ON, Input OFF
    //  Running    → Auto/Manual DISABLED |  Start DISABLED       |  Stop enabled
    // ══════════════════════════════════════════════════════════

    private enum AppState { Idle, ModeManual, ModeAuto, Running }
    private AppState _state          = AppState.Idle;
    private AppState _modeBeforeRun  = AppState.Idle;

    private string _pendingCycles = string.Empty;

    // Sensor selection sync
    private bool _isSyncingSensor = false;
    private string _currentSensorId = "a";

    // Real-time Chart Timer
    private System.Windows.Threading.DispatcherTimer _chartTimer;
    private bool _lastChartState = false;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new ViewModels.MainViewModel();

        // Initialize Real-time Chart Timer (~20 fps)
        _chartTimer = new System.Windows.Threading.DispatcherTimer();
        _chartTimer.Interval = TimeSpan.FromMilliseconds(50);
        _chartTimer.Tick += ChartTimer_Tick;
        _chartTimer.Start();

        // Khởi tạo Database DataGrid source ban đầu
        DatabaseEventLog.ItemsSource = AtlasDashboard.Models.SensorDataStore.LogSensorA;

        InitializeWebView();

        // Subscribe Middleware events
        _uart.LogMessage   += OnUartLog;
        _uart.DataReceived += OnUartDataReceived;

        // Wire buttons
        BtnNewSession.Click += BtnNewSession_Click;
        BtnAuto.Click       += BtnAuto_Click;
        BtnManual.Click     += BtnManual_Click;
        BtnStart.Click      += BtnStart_Click;
        BtnStop.Click       += BtnStop_Click;

        // Wire motor buttons (Manual mode → send UART and play animations)
        BtnMotor1On.Click  += (_, _) => { _uart.SendRaw("1:on"); StartWaterAnimation(WaterFlow1); };
        BtnMotor1Off.Click += (_, _) => { _uart.SendRaw("1:off"); StopWaterAnimation(WaterFlow1); };
        
        BtnMotor2On.Click  += (_, _) => { _uart.SendRaw("2:on"); StartWaterAnimation(WaterFlow2); };
        BtnMotor2Off.Click += (_, _) => { _uart.SendRaw("2:off"); StopWaterAnimation(WaterFlow2); };
        
        BtnMotor3On.Click  += (_, _) => { _uart.SendRaw("3:on"); StartFanAnimation(FanIcon3); };
        BtnMotor3Off.Click += (_, _) => { _uart.SendRaw("3:off"); StopFanAnimation(FanIcon3); };
        
        BtnMotor4On.Click  += (_, _) => { _uart.SendRaw("4:on"); StartFanAnimation(FanIcon4); };
        BtnMotor4Off.Click += (_, _) => { _uart.SendRaw("4:off"); StopFanAnimation(FanIcon4); };

        // Mở COM3 sẵn sàng nhận ngay khi khởi động
        _uart.EnsureOpen();

        // Đóng port khi cửa sổ đóng
        Closed += (_, _) => _uart.Close();

        // Khởi tạo UI về trạng thái Idle
        ApplyState(AppState.Idle);
    }

    private async void InitializeWebView()
    {
        await WebView3D.EnsureCoreWebView2Async();
        string webAssetFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Web");
        WebView3D.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "appassets.local",
            webAssetFolder,
            Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow
        );
        WebView3D.CoreWebView2.Navigate("https://appassets.local/index.html");
    }

    // ══════════════════════════════════════════════════════════
    //  State Machine — core
    // ══════════════════════════════════════════════════════════

    private void ApplyState(AppState newState)
    {
        _state = newState;

        // Stop all animations when transitioning states
        StopWaterAnimation(WaterFlow1);
        StopWaterAnimation(WaterFlow2);
        StopFanAnimation(FanIcon3);
        StopFanAnimation(FanIcon4);

        switch (_state)
        {
            // ─── IDLE ─────────────────────────────────────────
            case AppState.Idle:
                SetButton(BtnAuto,   enabled: true,  opacity: 1.0);
                SetButton(BtnManual, enabled: true,  opacity: 1.0);
                SetButton(BtnStart,  enabled: false, opacity: 0.35);
                SetButton(BtnStop,   enabled: false, opacity: 0.35);
                SetCyclesArea(displayEnabled: false, inputEnabled: false);
                SetMotorControlsMode(MotorMode.Hidden);
                break;

            // ─── MANUAL MODE ──────────────────────────────────
            case AppState.ModeManual:
                SetButton(BtnAuto,   enabled: false, opacity: 0.35);
                SetButton(BtnManual, enabled: true,  opacity: 1.0);
                SetButton(BtnStart,  enabled: true,  opacity: 1.0);
                SetButton(BtnStop,   enabled: true,  opacity: 1.0);
                SetCyclesArea(displayEnabled: true, inputEnabled: true);
                SetMotorControlsMode(MotorMode.StatusDisplay);
                break;

            // ─── AUTO MODE ────────────────────────────────────
            case AppState.ModeAuto:
                SetButton(BtnAuto,   enabled: true,  opacity: 1.0);
                SetButton(BtnManual, enabled: false, opacity: 0.35);
                SetButton(BtnStart,  enabled: true,  opacity: 1.0);
                SetButton(BtnStop,   enabled: true,  opacity: 1.0);
                SetCyclesArea(displayEnabled: true, inputEnabled: false);
                SetMotorControlsMode(MotorMode.ButtonsDisabled);
                break;

            // ─── RUNNING ──────────────────────────────────────
            case AppState.Running:
                SetButton(BtnAuto,   enabled: false, opacity: 0.35);
                SetButton(BtnManual, enabled: false, opacity: 0.35);
                SetButton(BtnStart,  enabled: false, opacity: 0.35);
                SetButton(BtnStop,   enabled: true,  opacity: 1.0);
                // Cycles: giữ nguyên như mode trước Running
                bool wasManual = _modeBeforeRun == AppState.ModeManual;
                SetCyclesArea(displayEnabled: true, inputEnabled: wasManual);
                
                if (_modeBeforeRun == AppState.ModeAuto)
                {
                    SetMotorControlsMode(MotorMode.Buttons);
                }
                break;
        }
    }

    // ── Helpers ───────────────────────────────────────────────

    private static void SetButton(Button btn, bool enabled, double opacity)
    {
        btn.IsEnabled = enabled;
        btn.Opacity   = opacity;
    }

    private void SetCyclesArea(bool displayEnabled, bool inputEnabled)
    {
        DisplayCyclesBorder.Opacity = displayEnabled ? 1.0 : 0.35;
        InputCyclesBorder.Opacity   = inputEnabled   ? 1.0 : 0.35;
        InputCycles.IsEnabled       = inputEnabled;
    }

    // ── Motor Controls ───────────────────────────────────────

    private enum MotorMode { Hidden, Buttons, ButtonsDisabled, StatusDisplay }

    private void SetMotorControlsMode(MotorMode mode)
    {
        var panels     = new[] { Motor1Controls, Motor2Controls, Motor3Controls, Motor4Controls };
        var statusBdgs = new[] { Motor1Status,   Motor2Status,   Motor3Status,   Motor4Status   };
        var onBtns     = new[] { BtnMotor1On,    BtnMotor2On,    BtnMotor3On,    BtnMotor4On    };
        var offBtns    = new[] { BtnMotor1Off,   BtnMotor2Off,   BtnMotor3Off,   BtnMotor4Off   };

        for (int i = 0; i < panels.Length; i++)
        {
            switch (mode)
            {
                case MotorMode.Hidden:
                    panels[i].Visibility     = Visibility.Collapsed;
                    statusBdgs[i].Visibility = Visibility.Collapsed;
                    break;

                case MotorMode.Buttons:
                    panels[i].Visibility     = Visibility.Visible;
                    statusBdgs[i].Visibility = Visibility.Collapsed;
                    onBtns[i].IsEnabled  = true;
                    offBtns[i].IsEnabled = true;
                    onBtns[i].Cursor     = System.Windows.Input.Cursors.Hand;
                    offBtns[i].Cursor    = System.Windows.Input.Cursors.Hand;
                    onBtns[i].Opacity    = 1.0;
                    offBtns[i].Opacity   = 1.0;
                    break;

                case MotorMode.ButtonsDisabled:
                    panels[i].Visibility     = Visibility.Visible;
                    statusBdgs[i].Visibility = Visibility.Collapsed;
                    onBtns[i].IsEnabled  = false;
                    offBtns[i].IsEnabled = false;
                    onBtns[i].Cursor     = System.Windows.Input.Cursors.Arrow;
                    offBtns[i].Cursor    = System.Windows.Input.Cursors.Arrow;
                    onBtns[i].Opacity    = 0.4;
                    offBtns[i].Opacity   = 0.4;
                    break;

                case MotorMode.StatusDisplay:
                    panels[i].Visibility     = Visibility.Collapsed;
                    statusBdgs[i].Visibility = Visibility.Visible;
                    break;
            }
        }
    }

    // ── Cached brushes for motor status (avoid per-call allocations) ───
    private static readonly Brush _motorOnFg  = new SolidColorBrush(Color.FromRgb(0x04, 0x7D, 0x3C));
    private static readonly Brush _motorOnBg  = new SolidColorBrush(Color.FromRgb(0xEC, 0xFD, 0xF5));
    private static readonly Brush _motorOffFg = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
    private static readonly Brush _motorOffBg = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));

    private void UpdateMotorStatusDisplay(int motorIndex, bool isOn)
    {
        var statusBdgs = new[] { Motor1Status,   Motor2Status,   Motor3Status,   Motor4Status   };
        var statusTxts = new[] { Motor1StatusText, Motor2StatusText, Motor3StatusText, Motor4StatusText };
        
        if (motorIndex < 1 || motorIndex > 4) return;
        
        int idx = motorIndex - 1;
        var bdg = statusBdgs[idx];
        var txt = statusTxts[idx];

        if (isOn)
        {
            txt.Text       = "ON";
            txt.Foreground = _motorOnFg;
            bdg.Background = _motorOnBg;
        }
        else
        {
            txt.Text       = "OFF";
            txt.Foreground = _motorOffFg;
            bdg.Background = _motorOffBg;
        }

        // Sync animations
        if (motorIndex == 1) { if (isOn) StartWaterAnimation(WaterFlow1); else StopWaterAnimation(WaterFlow1); }
        else if (motorIndex == 2) { if (isOn) StartWaterAnimation(WaterFlow2); else StopWaterAnimation(WaterFlow2); }
        else if (motorIndex == 3) { if (isOn) StartFanAnimation(FanIcon3); else StopFanAnimation(FanIcon3); }
        else if (motorIndex == 4) { if (isOn) StartFanAnimation(FanIcon4); else StopFanAnimation(FanIcon4); }
    }

    // ══════════════════════════════════════════════════════════
    //  Button Handlers
    // ══════════════════════════════════════════════════════════

    private void BtnNewSession_Click(object sender, RoutedEventArgs e)
    {
        _uart.SendLedOn();
    }

    private void BtnAuto_Click(object sender, RoutedEventArgs e)
    {
        ApplyState(AppState.ModeAuto);
    }

    private void BtnManual_Click(object sender, RoutedEventArgs e)
    {
        ApplyState(AppState.ModeManual);
        _uart.SendRaw("m");
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        _modeBeforeRun = _state;
        ApplyState(AppState.Running);

        if (_modeBeforeRun == AppState.ModeManual)
        {
            // Manual: gửi số cycles đã nhập
            if (!string.IsNullOrEmpty(_pendingCycles))
                _uart.SendRaw(_pendingCycles);
        }
        else if (_modeBeforeRun == AppState.ModeAuto)
        {
            // Auto: gửi lệnh start
            _uart.SendRaw("m:start");
        }

        if (WebView3D.CoreWebView2 != null)
            await WebView3D.ExecuteScriptAsync("startMotorRotation();");
    }

    private async void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        ApplyState(AppState.Idle);
        _uart.SendRaw("s");

        if (WebView3D.CoreWebView2 != null)
            await WebView3D.ExecuteScriptAsync("stopMotorRotation();");

        // Reset cycles UI và giá trị đã lưu
        _pendingCycles         = string.Empty;
        DisplayCyclesText.Text = string.Empty;
        InputCycles.Text       = string.Empty;
    }

    // ══════════════════════════════════════════════════════════
    //  UART event handlers
    // ══════════════════════════════════════════════════════════

    private void OnUartLog(string message)
    {
        Dispatcher.Invoke(() => AppendToLcd(message));
    }

    private void OnUartDataReceived(string line)
    {
        Dispatcher.Invoke(() =>
        {
            string cleanLine = line.Trim();
            // 1. Log lên LCD
            AppendToLcd($"[RECV] {cleanLine}");

            // Kiểm tra lệnh Reset tối cao
            if (cleanLine.ToLower() == "reset")
            {
                ShowResetAlert();
                return;
            }

            var parts = cleanLine.Split(':');
            if (parts.Length == 2)
            {
                string id = parts[0].ToLower();
                bool isOn = parts[1].ToUpper() == "ON";

                // Đồng bộ trạng thái Relay (Motor controls) nếu có lệnh 1-4
                switch (id)
                {
                    case "1": 
                        if (isOn) BtnMotor1On.IsChecked = true; else BtnMotor1Off.IsChecked = true;
                        UpdateMotorStatusDisplay(1, isOn);
                        break;
                    case "2": 
                        if (isOn) BtnMotor2On.IsChecked = true; else BtnMotor2Off.IsChecked = true; 
                        UpdateMotorStatusDisplay(2, isOn);
                        break;
                    case "3": 
                        if (isOn) BtnMotor3On.IsChecked = true; else BtnMotor3Off.IsChecked = true; 
                        UpdateMotorStatusDisplay(3, isOn);
                        break;
                    case "4": 
                        if (isOn) BtnMotor4On.IsChecked = true; else BtnMotor4Off.IsChecked = true; 
                        UpdateMotorStatusDisplay(4, isOn);
                        break;
                }

                // Cập nhật trạng thái Sensor (cảm biến a-d)
                int changedIndex = -1;
                if (DataContext is ViewModels.MainViewModel vm)
                {
                    changedIndex = vm.SetSensor(cleanLine);
                }

                // Đóng băng hệ thống nếu cảm biến độ cao (index 2) hoặc nhiệt độ (index 3) bật (ON), bất kể trạng thái
                if (isOn && (changedIndex == 2 || changedIndex == 3))
                {
                    ShowFreezeAlert();
                }

                // Ghi nhận sự kiện vào Database Log
                AtlasDashboard.Models.SensorDataStore.RecordEvent(id, isOn);

                // Hiệu ứng flash nếu là sensor
                if (changedIndex >= 0)
                {
                    PlaySensorFlashEffect(changedIndex);
                }
            }
        });
    }

    // ══════════════════════════════════════════════════════════
    //  Overlay Alerts (Freeze & Reset)
    // ══════════════════════════════════════════════════════════

    private void ShowFreezeAlert()
    {
        OverlayGrid.Visibility = Visibility.Visible;
        FreezeAlertBox.Visibility = Visibility.Visible;
        ResetAlertBox.Visibility = Visibility.Collapsed;
        
        // Vô hiệu hóa root grid bằng cách set IsEnabled trên UI chính
        // Ta không set trên toàn bộ Window vì Overlay nằm trong đó
        // Tuy nhiên, có thể dựa vào OverlayGrid với Background bán trong suốt để block click.
    }

    private void ShowResetAlert()
    {
        OverlayGrid.Visibility = Visibility.Visible;
        ResetAlertBox.Visibility = Visibility.Visible;
        FreezeAlertBox.Visibility = Visibility.Collapsed;
    }

    private void HideAlerts()
    {
        OverlayGrid.Visibility = Visibility.Collapsed;
        FreezeAlertBox.Visibility = Visibility.Collapsed;
        ResetAlertBox.Visibility = Visibility.Collapsed;
    }

    private void BtnConfirmReset_Click(object sender, RoutedEventArgs e)
    {
        HideAlerts();
        if (DataContext is ViewModels.MainViewModel vm)
        {
            vm.ResetAllSensors();
        }

        // Đặt lại dữ liệu chu kỳ (Cycles)
        _pendingCycles         = string.Empty;
        DisplayCyclesText.Text = string.Empty;
        InputCycles.Text       = string.Empty;

        ApplyState(AppState.Idle);
    }

    // ══════════════════════════════════════════════════════════
    //  UI Effect — flash animation khi sensor toggle
    // ══════════════════════════════════════════════════════════

    private void PlaySensorFlashEffect(int index)
    {
        if (SensorList.ItemContainerGenerator.ContainerFromIndex(index)
                is not FrameworkElement container) return;

        var anim = new DoubleAnimation
        {
            From           = 0.3,
            To             = 1.0,
            Duration       = TimeSpan.FromMilliseconds(400),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        container.BeginAnimation(OpacityProperty, anim);
    }

    // ══════════════════════════════════════════════════════════
    //  Motor 2D Animations (Water Flow & Fan Rotation)
    // ══════════════════════════════════════════════════════════

    // ── Cached brushes for animations ───────────────────────────────────
    private static readonly Brush _waterActiveBrush   = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0EA5E9"));
    private static readonly Brush _waterInactiveBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));
    private static readonly Brush _fanActiveBrush     = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
    private static readonly Brush _fanInactiveBrush   = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));

    private void StartWaterAnimation(Path path)
    {
        var anim = new DoubleAnimation
        {
            From           = 24,
            To             = 0,
            Duration       = TimeSpan.FromSeconds(0.4),
            RepeatBehavior = RepeatBehavior.Forever
        };
        path.BeginAnimation(Shape.StrokeDashOffsetProperty, anim);
        path.Stroke = _waterActiveBrush;
    }

    private void StopWaterAnimation(Path path)
    {
        path.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
        path.Stroke = _waterInactiveBrush;
    }

    private void StartFanAnimation(TextBlock textBlock)
    {
        if (textBlock.RenderTransform is RotateTransform transform)
        {
            var anim = new DoubleAnimation
            {
                From           = 0,
                To             = 360,
                Duration       = TimeSpan.FromSeconds(0.6),
                RepeatBehavior = RepeatBehavior.Forever
            };
            transform.BeginAnimation(RotateTransform.AngleProperty, anim);
        }
        textBlock.Foreground = _fanActiveBrush;
    }

    private void StopFanAnimation(TextBlock textBlock)
    {
        if (textBlock.RenderTransform is RotateTransform transform)
            transform.BeginAnimation(RotateTransform.AngleProperty, null);
        textBlock.Foreground = _fanInactiveBrush;
    }

    // ══════════════════════════════════════════════════════════
    //  LCD Display helper
    // ══════════════════════════════════════════════════════════

    private void AppendToLcd(string line)
    {
        const int maxLines = 10;
        string current = LcdDisplay.Text ?? string.Empty;

        var lines = current.Split('\n');
        int start = lines.Length >= maxLines ? 1 : 0;

        var sb = new StringBuilder();
        for (int i = start; i < lines.Length; i++)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(lines[i]);
        }
        if (sb.Length > 0) sb.Append('\n');
        sb.Append(line);

        LcdDisplay.Text = sb.ToString();
    }

    // ══════════════════════════════════════════════════════════
    //  LCD Input placeholder
    // ══════════════════════════════════════════════════════════

    private void LcdInput_GotFocus(object sender, RoutedEventArgs e)
    {
        if (LcdCommandInput.Text == LcdPlaceholder)
        {
            LcdCommandInput.Text       = string.Empty;
            LcdCommandInput.Foreground = (Brush)FindResource("TextWhiteBrush");
        }
    }

    private void LcdInput_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LcdCommandInput.Text))
        {
            LcdCommandInput.Text       = LcdPlaceholder;
            LcdCommandInput.Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));
        }
    }

    // ══════════════════════════════════════════════════════════
    //  Cycles Input — chỉ cập nhật khi nhấn Enter
    // ══════════════════════════════════════════════════════════

    private void InputCycles_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            string input = InputCycles.Text.Trim();
            if (string.IsNullOrEmpty(input)) return;

            // Chỉ lưu + hiển thị — chưa gửi UART
            // UART sẽ được gửi khi nhấn Start
            _pendingCycles         = input;
            DisplayCyclesText.Text = input;
        }
    }

    private void NavMenu_Checked(object sender, RoutedEventArgs e)
    {
        if (DashboardTopWidgets == null || DashboardBottomWidgets == null || DatabaseView == null)
            return;

        if (NavDashboard.IsChecked == true)
        {
            DashboardTopWidgets.Visibility = Visibility.Visible;
            DashboardBottomWidgets.Visibility = Visibility.Visible;
            DatabaseView.Visibility = Visibility.Collapsed;
        }
        else if (NavDatabases.IsChecked == true)
        {
            DashboardTopWidgets.Visibility = Visibility.Collapsed;
            DashboardBottomWidgets.Visibility = Visibility.Collapsed;
            DatabaseView.Visibility = Visibility.Visible;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  Real-time Digital Chart Logic & Database Selectors
    // ══════════════════════════════════════════════════════════

    private void ChartTimer_Tick(object? sender, EventArgs e)
    {
        if (ChartCanvas == null || DigitalChartLine == null) return;
        if (!IsVisible) return; // Skip rendering when window is minimized/hidden
        
        double width = ChartCanvas.ActualWidth;
        if (width == 0) width = 800; // fallback if not rendered yet
        
        // 1. Determine which sensor is selected
        string selectedSensorId = _currentSensorId;

        bool currentState = false;
        switch (selectedSensorId)
        {
            case "a": currentState = AtlasDashboard.Models.SensorDataStore.StateSensorA; break;
            case "b": currentState = AtlasDashboard.Models.SensorDataStore.StateSensorB; break;
            case "c": currentState = AtlasDashboard.Models.SensorDataStore.StateSensorC; break;
            case "d": currentState = AtlasDashboard.Models.SensorDataStore.StateSensorD; break;
        }

        // Y = 30 (ON/High), Y = 130 (OFF/Low)
        double currentY = currentState ? 30.0 : 130.0;
        double step = 3.0; // scroll speed

        var points = DigitalChartLine.Points;
        
        // Shift existing points right
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            p.X += step;
            points[i] = p; // struct copy back
        }

        // Remove out of bounds points (X > width + 10)
        while (points.Count > 0 && points[points.Count - 1].X > width + 10)
        {
            points.RemoveAt(points.Count - 1);
        }

        // Add new points at X = 0
        if (points.Count > 0 && currentState != _lastChartState)
        {
            // State changed -> vertical step line
            double lastY = _lastChartState ? 30.0 : 130.0;
            points.Insert(0, new System.Windows.Point(0, lastY));
            points.Insert(0, new System.Windows.Point(0, currentY));
        }
        else if (points.Count == 0)
        {
            points.Add(new System.Windows.Point(0, currentY));
        }
        else
        {
            // Continuous state
            points.Insert(0, new System.Windows.Point(0, currentY));
        }

        _lastChartState = currentState;
    }

    private void GlobalSensor_Checked(object sender, RoutedEventArgs e)
    {
        if (_isSyncingSensor) return;
        if (sender is RadioButton rb && rb.Tag != null)
        {
            _isSyncingSensor = true;
            _currentSensorId = rb.Tag.ToString() ?? "a";
            
            // Sync Chart Buttons
            if (ChartBtnSensorA != null) ChartBtnSensorA.IsChecked = (_currentSensorId == "a");
            if (ChartBtnSensorB != null) ChartBtnSensorB.IsChecked = (_currentSensorId == "b");
            if (ChartBtnSensorC != null) ChartBtnSensorC.IsChecked = (_currentSensorId == "c");
            if (ChartBtnSensorD != null) ChartBtnSensorD.IsChecked = (_currentSensorId == "d");

            // Sync Db Buttons
            if (DbBtnSensorA != null) DbBtnSensorA.IsChecked = (_currentSensorId == "a");
            if (DbBtnSensorB != null) DbBtnSensorB.IsChecked = (_currentSensorId == "b");
            if (DbBtnSensorC != null) DbBtnSensorC.IsChecked = (_currentSensorId == "c");
            if (DbBtnSensorD != null) DbBtnSensorD.IsChecked = (_currentSensorId == "d");

            // Update Chart
            if (DigitalChartLine != null)
            {
                DigitalChartLine.Points.Clear();
            }

            // Update Database
            if (DatabaseEventLog != null)
            {
                switch (_currentSensorId)
                {
                    case "a": DatabaseEventLog.ItemsSource = AtlasDashboard.Models.SensorDataStore.LogSensorA; break;
                    case "b": DatabaseEventLog.ItemsSource = AtlasDashboard.Models.SensorDataStore.LogSensorB; break;
                    case "c": DatabaseEventLog.ItemsSource = AtlasDashboard.Models.SensorDataStore.LogSensorC; break;
                    case "d": DatabaseEventLog.ItemsSource = AtlasDashboard.Models.SensorDataStore.LogSensorD; break;
                }
            }

            _isSyncingSensor = false;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  UPGRADE 2 & 3 — Button Hover Glow + Ripple Effects
    // ══════════════════════════════════════════════════════════

    /// <summary>Animate a DropShadowEffect onto/off a button with a given color.</summary>
    private static void AnimateButtonGlow(Button btn, Color glowColor, bool show, int blurRadius = 14, int durationMs = 180)
    {
        var effect = btn.Effect as DropShadowEffect;
        if (effect == null)
        {
            effect = new DropShadowEffect
            {
                Color       = glowColor,
                ShadowDepth = 0,
                BlurRadius  = 0,
                Opacity     = 0
            };
            btn.Effect = effect;
        }

        var duration = TimeSpan.FromMilliseconds(durationMs);

        // Animate BlurRadius
        var blurAnim = new DoubleAnimation
        {
            To       = show ? blurRadius : 0,
            Duration = duration,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        effect.BeginAnimation(DropShadowEffect.BlurRadiusProperty, blurAnim);

        // Animate Opacity
        var opacAnim = new DoubleAnimation
        {
            To       = show ? 0.85 : 0,
            Duration = duration,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        effect.BeginAnimation(DropShadowEffect.OpacityProperty, opacAnim);

        // Update color when showing
        if (show)
        {
            var colorAnim = new ColorAnimation
            {
                To       = glowColor,
                Duration = duration
            };
            effect.BeginAnimation(DropShadowEffect.ColorProperty, colorAnim);
        }
    }

    // ── UPGRADE 2: START button ───────────────────────────────
    private static readonly Color _tealGlow = (Color)ColorConverter.ConvertFromString("#00B4D8");
    private static readonly Color _redGlow  = (Color)ColorConverter.ConvertFromString("#FF4455");
    private static readonly Color _manualGlow = (Color)ColorConverter.ConvertFromString("#00B4D8");
    private static readonly Color _autoGlow   = (Color)ColorConverter.ConvertFromString("#90CAF9");

    private void BtnStart_MouseEnter(object sender, MouseEventArgs e)
        => AnimateButtonGlow(BtnStart, _tealGlow, show: true);

    private void BtnStart_MouseLeave(object sender, MouseEventArgs e)
        => AnimateButtonGlow(BtnStart, _tealGlow, show: false);

    // ── UPGRADE 2: STOP button ────────────────────────────────
    private void BtnStop_MouseEnter(object sender, MouseEventArgs e)
        => AnimateButtonGlow(BtnStop, _redGlow, show: true);

    private void BtnStop_MouseLeave(object sender, MouseEventArgs e)
        => AnimateButtonGlow(BtnStop, _redGlow, show: false);

    // ── UPGRADE 3: Manual (BtnAuto) button ───────────────────
    private void BtnManualMode_MouseEnter(object sender, MouseEventArgs e)
        => AnimateButtonGlow(BtnAuto, _manualGlow, show: true);

    private void BtnManualMode_MouseLeave(object sender, MouseEventArgs e)
        => AnimateButtonGlow(BtnAuto, _manualGlow, show: false);

    // ── UPGRADE 3: Auto (BtnManual) button ───────────────────
    private void BtnAutoMode_MouseEnter(object sender, MouseEventArgs e)
        => AnimateButtonGlow(BtnManual, _autoGlow, show: true);

    private void BtnAutoMode_MouseLeave(object sender, MouseEventArgs e)
        => AnimateButtonGlow(BtnManual, _autoGlow, show: false);

    // ── Shared Ripple (Upgrades 2 & 3) ───────────────────────
    private void RippleButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button btn) return;

        var point = e.GetPosition(btn);
        var adornerLayer = AdornerLayer.GetAdornerLayer(btn);
        if (adornerLayer == null) return;

        var adorner = new RippleAdorner(btn, point);
        adornerLayer.Add(adorner);
        adorner.BeginAnimation(() => adornerLayer.Remove(adorner));
    }
}

// ══════════════════════════════════════════════════════════
//  Ripple Adorner helper class (Upgrades 2 & 3)
// ══════════════════════════════════════════════════════════
internal class RippleAdorner : System.Windows.Documents.Adorner
{
    private readonly Ellipse _ellipse;
    private readonly Canvas  _canvas;
    private readonly Point   _origin;

    public RippleAdorner(UIElement adornedElement, Point clickPoint) : base(adornedElement)
    {
        IsHitTestVisible = false;
        _origin = clickPoint;

        _ellipse = new Ellipse
        {
            Fill             = Brushes.White,
            Opacity          = 0.4,
            IsHitTestVisible = false,
            Width            = 0,
            Height           = 0,
            RenderTransformOrigin = new Point(0.5, 0.5)
        };

        _canvas = new Canvas { IsHitTestVisible = false };
        _canvas.Children.Add(_ellipse);
        AddVisualChild(_canvas);
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _canvas;

    protected override Size MeasureOverride(Size constraint)
    {
        _canvas.Measure(constraint);
        return constraint;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _canvas.Arrange(new Rect(finalSize));
        return finalSize;
    }

    public void BeginAnimation(Action onComplete)
    {
        const double targetSize = 120;
        Canvas.SetLeft(_ellipse, _origin.X - targetSize / 2);
        Canvas.SetTop(_ellipse,  _origin.Y - targetSize / 2);

        var duration = TimeSpan.FromMilliseconds(400);

        var widthAnim = new DoubleAnimation(0, targetSize, duration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        var heightAnim = new DoubleAnimation(0, targetSize, duration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        var opacAnim = new DoubleAnimation(0.4, 0, duration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        opacAnim.Completed += (_, _) => onComplete?.Invoke();

        _ellipse.BeginAnimation(FrameworkElement.WidthProperty,  widthAnim);
        _ellipse.BeginAnimation(FrameworkElement.HeightProperty, heightAnim);
        _ellipse.BeginAnimation(UIElement.OpacityProperty,       opacAnim);
    }
}