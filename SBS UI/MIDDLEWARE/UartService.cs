// ============================================================
//  UartService.cs  —  POC UART via System.IO.Ports
//  Middleware layer (no DI, no MVVM, no Clean Architecture)
//  Target: COM3, 115200, 8N1
// ============================================================
using System;
using System.IO.Ports;
namespace Middleware
{
    /// <summary>
    /// Minimal UART service for POC testing.
    /// Flow: Button → COM3 → Virtual Serial → COM4 → Terminal / STM32
    /// </summary>
    public class UartService
    {
        // ── Configuration ────────────────────────────────────
        private const string PortName  = "COM10";
        private const int    BaudRate  = 115200;
        private const int    DataBits  = 8;
        private const Parity Parity    = System.IO.Ports.Parity.None;
        private const StopBits StopBits = System.IO.Ports.StopBits.One;
        // ── State ────────────────────────────────────────────
        private SerialPort? _port;

        // Buffer tích lũy dữ liệu nhận được — xử lý partial reads
        private string _receiveBuffer = string.Empty;
        private readonly object _bufferLock = new();
        // ── Events ───────────────────────────────────────────
        /// <summary>Raised (on a background thread) whenever a line is received from the port.</summary>
        public event Action<string>? DataReceived;
        /// <summary>Raised when a log message is produced (send/receive/error).</summary>
        public event Action<string>? LogMessage;
        // ── Public API ────────────────────────────────────────
        /// <summary>
        /// Returns true when COM3 is currently open.
        /// </summary>
        public bool IsOpen => _port?.IsOpen == true;
        /// <summary>
        /// Opens COM3 if not already open.
        /// Throws nothing — errors are surfaced via <see cref="LogMessage"/>.
        /// </summary>
        /// <returns>True if the port is open (was already open, or just opened successfully).</returns>
        public bool EnsureOpen()
        {
            if (_port?.IsOpen == true)
                return true;
            try
            {
                _port = new SerialPort(PortName, BaudRate, Parity, DataBits, StopBits)
                {
                    ReadTimeout  = SerialPort.InfiniteTimeout,
                    WriteTimeout = 500,
                    NewLine      = "\r\n"
                };
                _port.DataReceived += OnDataReceived;
                _port.Open();
                Log($"[UART] Opened {PortName} @ {BaudRate} baud.");
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                Log($"[UART] ERROR — {PortName} is busy (used by another process): {ex.Message}");
                return false;
            }
            catch (System.IO.IOException ex)
            {
                Log($"[UART] ERROR — {PortName} does not exist or is unavailable: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Log($"[UART] ERROR — Failed to open {PortName}: {ex.Message}");
                return false;
            }
        }
        /// <summary>
        /// Sends the string "LED_ON\r\n" to COM3.
        /// Opens the port first if it is not yet open.
        /// </summary>
        public void SendLedOn()
        {
            if (!EnsureOpen())
                return;
            const string payload = "z\r\n";
            try
            {
                _port!.Write(payload);
                Log($"[UART] SENT → \"{payload.TrimEnd()}\"");
            }
            catch (TimeoutException)
            {
                Log("[UART] ERROR — Write timeout. Device may be disconnected.");
            }
            catch (InvalidOperationException ex)
            {
                Log($"[UART] ERROR — Port closed unexpectedly: {ex.Message}");
            }
            catch (Exception ex)
            {
                Log($"[UART] ERROR — Send failed: {ex.Message}");
            }
        }


        /// <summary>
        /// Gửi chuỗi bất kỳ qua COM3 (mở port nếu chưa mở).
        /// \r\n được tự động thêm vào cuối.
        /// </summary>
        public void SendRaw(string message)
        {
            if (!EnsureOpen()) return;

            string payload = message.TrimEnd('\r', '\n') + "\r\n";
            try
            {
                _port!.Write(payload);
                Log($"[UART] SENT → \"{message.TrimEnd()}\"");
            }
            catch (TimeoutException)
            {
                Log("[UART] ERROR — Write timeout. Device may be disconnected.");
            }
            catch (InvalidOperationException ex)
            {
                Log($"[UART] ERROR — Port closed unexpectedly: {ex.Message}");
            }
            catch (Exception ex)
            {
                Log($"[UART] ERROR — Send failed: {ex.Message}");
            }
        }
        /// <summary>
        /// Closes the serial port and releases resources.
        /// </summary>
        public void Close()
        {
            if (_port == null) return;
            try
            {
                if (_port.IsOpen)
                    _port.Close();
                _port.DataReceived -= OnDataReceived;
                _port.Dispose();
                Log($"[UART] {PortName} closed.");
            }
            catch (Exception ex)
            {
                Log($"[UART] ERROR — Close failed: {ex.Message}");
            }
            finally
            {
                _port = null;
            }
        }
        // ── Private ───────────────────────────────────────────
        /// <summary>
        /// Fired by SerialPort on a background thread when data arrives.
        /// Reads all available bytes, splits on newlines and raises DataReceived.
        /// </summary>
        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_port == null || !_port.IsOpen) return;
            try
            {
                string raw = _port.ReadExisting();
                if (string.IsNullOrEmpty(raw)) return;

                lock (_bufferLock)
                {
                    // Gộp vào buffer tích lũy
                    _receiveBuffer += raw;

                    // Xử lý từng dòng hoàn chỉnh (kết thúc bằng \n)
                    int newlineIndex;
                    while ((newlineIndex = _receiveBuffer.IndexOf('\n')) >= 0)
                    {
                        // Lấy dòng hoàn chỉnh, bỏ \r
                        string line = _receiveBuffer[..newlineIndex].TrimEnd('\r').Trim();

                        // Cắt buffer, giữ lại phần chưa xử lý
                        _receiveBuffer = _receiveBuffer[(newlineIndex + 1)..];

                        if (string.IsNullOrEmpty(line)) continue;

                        Log($"[UART] RECV ← \"{line}\"");
                        DataReceived?.Invoke(line);
                    }
                }
            }
            catch (InvalidOperationException)
            {
                Log("[UART] ERROR — Connection lost (port closed during read).");
            }
            catch (Exception ex)
            {
                Log($"[UART] ERROR — Read failed: {ex.Message}");
            }
        }
        private void Log(string message)
        {
            // Prefix with timestamp for readability
            string stamped = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            System.Diagnostics.Debug.WriteLine(stamped);
            LogMessage?.Invoke(stamped);
        }
    }
}
