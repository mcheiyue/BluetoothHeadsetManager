using System;
using System.Threading.Tasks;
using BluetoothHeadsetManager.Bluetooth;
using BluetoothHeadsetManager.Utils;
using Windows.Devices.Bluetooth;
using ThreadingTimer = System.Threading.Timer;

namespace BluetoothHeadsetManager.Core
{
    /// <summary>
    /// 连接状态变化事件参数
    /// </summary>
    public class ConnectionStatusChangedEventArgs : EventArgs
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public BluetoothConnectionStatus Status { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// 连接管理器
    /// 负责蓝牙设备的连接、断开、状态监控和自动重连
    /// </summary>
    public class ConnectionManager : IDisposable
    {
        private readonly DeviceManager _deviceManager;
        private BluetoothDeviceWrapper? _currentDevice;
        private ThreadingTimer? _reconnectTimer;
        private ThreadingTimer? _connectionMonitorTimer;
        private bool _autoReconnectEnabled = true;
        private int _reconnectAttempts = 0;
        private const int MaxReconnectAttempts = 5;
        private const int ReconnectDelayMs = 5000; // 5秒
        private const int ConnectionMonitorIntervalMs = 10000; // 10秒
        private bool _isConnecting = false;
        private bool _isDisconnecting = false;
        private bool _disposed = false;

        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        public event EventHandler<ConnectionStatusChangedEventArgs>? StatusChanged;

        /// <summary>
        /// 构造函数
        /// </summary>
        public ConnectionManager(DeviceManager deviceManager)
        {
            _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
            Logger.Info("ConnectionManager initialized");
        }

        /// <summary>
        /// 获取当前连接的设备
        /// </summary>
        public BluetoothDeviceWrapper? CurrentDevice => _currentDevice;

        /// <summary>
        /// 获取或设置自动重连是否启用
        /// </summary>
        public bool AutoReconnectEnabled
        {
            get => _autoReconnectEnabled;
            set
            {
                _autoReconnectEnabled = value;
                Logger.Info($"Auto-reconnect {(value ? "enabled" : "disabled")}");
            }
        }

        /// <summary>
        /// 连接到指定设备
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>连接是否成功</returns>
        public async Task<bool> ConnectAsync(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
            {
                Logger.Error("ConnectAsync: deviceId is null or empty");
                return false;
            }

            if (_isConnecting)
            {
                Logger.Warning("Already connecting to a device");
                return false;
            }

            try
            {
                _isConnecting = true;
                Logger.Info($"Connecting to device: {deviceId}");

                // 如果已连接到其他设备，先断开
                if (_currentDevice != null && _currentDevice.Id != deviceId)
                {
                    Logger.Info("Disconnecting from current device before connecting to new one");
                    await DisconnectAsync();
                }

                // 查找设备
                var device = await _deviceManager.FindDeviceAsync(deviceId);
                if (device == null)
                {
                    Logger.Error($"Device not found: {deviceId}");
                    OnStatusChanged(deviceId, string.Empty, BluetoothConnectionStatus.Disconnected, 
                        "Device not found");
                    return false;
                }

                // 执行连接
                bool success = await device.ConnectAsync();
                if (success)
                {
                    _currentDevice = device;
                    _reconnectAttempts = 0;
                    
                    // 订阅连接状态变化事件
                    device.ConnectionStatusChanged += OnDeviceConnectionStatusChanged;
                    
                    // 启动连接监控
                    StartConnectionMonitor();
                    
                    Logger.Info($"Successfully connected to {device.Name}");
                    OnStatusChanged(device.Id, device.Name, BluetoothConnectionStatus.Connected);
                }
                else
                {
                    Logger.Error($"Failed to connect to {device.Name}");
                    OnStatusChanged(device.Id, device.Name, BluetoothConnectionStatus.Disconnected, 
                        "Connection failed");
                }

                return success;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error connecting to device: {ex.Message}", ex);
                OnStatusChanged(deviceId, string.Empty, BluetoothConnectionStatus.Disconnected, 
                    ex.Message);
                return false;
            }
            finally
            {
                _isConnecting = false;
            }
        }

        /// <summary>
        /// 断开当前连接的设备
        /// </summary>
        /// <returns>断开是否成功</returns>
        public async Task<bool> DisconnectAsync()
        {
            if (_currentDevice == null)
            {
                Logger.Warning("No device connected");
                return true;
            }

            if (_isDisconnecting)
            {
                Logger.Warning("Already disconnecting");
                return false;
            }

            try
            {
                _isDisconnecting = true;
                var deviceId = _currentDevice.Id;
                var deviceName = _currentDevice.Name;

                Logger.Info($"Disconnecting from device: {deviceName}");

                // 停止监控
                StopConnectionMonitor();
                StopReconnectTimer();

                // 取消订阅事件
                _currentDevice.ConnectionStatusChanged -= OnDeviceConnectionStatusChanged;

                // 执行断开
                _currentDevice.Disconnect();
                
                // 等待一小段时间确保断开完成
                await Task.Delay(500);

                _currentDevice = null;
                _reconnectAttempts = 0;

                Logger.Info($"Successfully disconnected from {deviceName}");
                OnStatusChanged(deviceId, deviceName, BluetoothConnectionStatus.Disconnected);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error disconnecting device: {ex.Message}", ex);
                return false;
            }
            finally
            {
                _isDisconnecting = false;
            }
        }

        /// <summary>
        /// 检查设备是否已连接
        /// </summary>
        /// <param name="deviceId">设备ID（可选，不提供则检查当前设备）</param>
        /// <returns>是否已连接</returns>
        public bool IsConnected(string? deviceId = null)
        {
            if (_currentDevice == null)
                return false;

            if (deviceId != null && _currentDevice.Id != deviceId)
                return false;

            return _currentDevice.IsConnected;
        }

        /// <summary>
        /// 获取当前连接状态
        /// </summary>
        public BluetoothConnectionStatus GetConnectionStatus()
        {
            return _currentDevice?.ConnectionStatus ?? BluetoothConnectionStatus.Disconnected;
        }

        /// <summary>
        /// 设备连接状态变化事件处理
        /// </summary>
        private void OnDeviceConnectionStatusChanged(object? sender, BluetoothConnectionStatus status)
        {
            if (_currentDevice == null)
                return;

            Logger.Info($"Device {_currentDevice.Name} connection status changed to: {status}");

            // 触发状态变化事件
            OnStatusChanged(_currentDevice.Id, _currentDevice.Name, status);

            // 如果连接断开且启用了自动重连
            if (status == BluetoothConnectionStatus.Disconnected && 
                _autoReconnectEnabled && 
                !_isDisconnecting &&
                _reconnectAttempts < MaxReconnectAttempts)
            {
                Logger.Info($"Connection lost, starting auto-reconnect (attempt {_reconnectAttempts + 1}/{MaxReconnectAttempts})");
                StartReconnectTimer();
            }
        }

        /// <summary>
        /// 启动重连定时器
        /// </summary>
        private void StartReconnectTimer()
        {
            StopReconnectTimer();

            _reconnectTimer = new ThreadingTimer(async _ =>
            {
                if (_currentDevice == null || _disposed)
                    return;

                _reconnectAttempts++;
                Logger.Info($"Auto-reconnect attempt {_reconnectAttempts}/{MaxReconnectAttempts}");

                var deviceId = _currentDevice.Id;
                bool success = await ConnectAsync(deviceId);

                if (!success && _reconnectAttempts >= MaxReconnectAttempts)
                {
                    Logger.Warning("Max reconnection attempts reached, giving up");
                    StopReconnectTimer();
                    OnStatusChanged(deviceId, _currentDevice?.Name ?? string.Empty,
                        BluetoothConnectionStatus.Disconnected,
                        "Auto-reconnect failed after maximum attempts");
                }
            }, null, ReconnectDelayMs, System.Threading.Timeout.Infinite);
        }

        /// <summary>
        /// 停止重连定时器
        /// </summary>
        private void StopReconnectTimer()
        {
            if (_reconnectTimer != null)
            {
                _reconnectTimer.Dispose();
                _reconnectTimer = null;
            }
        }

        /// <summary>
        /// 启动连接监控定时器
        /// </summary>
        private void StartConnectionMonitor()
        {
            StopConnectionMonitor();

            _connectionMonitorTimer = new ThreadingTimer(_ =>
            {
                if (_currentDevice == null || _disposed)
                    return;

                try
                {
                    // 更新连接状态
                    _currentDevice.UpdateConnectionStatus();
                    
                    Logger.Debug($"Connection monitor: {_currentDevice.Name} - {_currentDevice.ConnectionStatus}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error in connection monitor: {ex.Message}", ex);
                }
            }, null, ConnectionMonitorIntervalMs, ConnectionMonitorIntervalMs);
        }

        /// <summary>
        /// 停止连接监控定时器
        /// </summary>
        private void StopConnectionMonitor()
        {
            if (_connectionMonitorTimer != null)
            {
                _connectionMonitorTimer.Dispose();
                _connectionMonitorTimer = null;
            }
        }

        /// <summary>
        /// 触发状态变化事件
        /// </summary>
        private void OnStatusChanged(string deviceId, string deviceName, 
            BluetoothConnectionStatus status, string? errorMessage = null)
        {
            StatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs
            {
                DeviceId = deviceId,
                DeviceName = deviceName,
                Status = status,
                ErrorMessage = errorMessage
            });
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            Logger.Info("Disposing ConnectionManager");

            StopConnectionMonitor();
            StopReconnectTimer();

            if (_currentDevice != null)
            {
                _currentDevice.ConnectionStatusChanged -= OnDeviceConnectionStatusChanged;
                _currentDevice.Disconnect();
                _currentDevice = null;
            }

            _disposed = true;
            Logger.Info("ConnectionManager disposed");
        }
    }
}