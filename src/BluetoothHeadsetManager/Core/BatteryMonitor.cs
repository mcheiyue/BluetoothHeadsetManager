using System;
using System.Linq;
using System.Threading.Tasks;
using BluetoothHeadsetManager.Bluetooth;
using BluetoothHeadsetManager.Utils;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using ThreadingTimer = System.Threading.Timer;

namespace BluetoothHeadsetManager.Core
{
    /// <summary>
    /// 电量变化事件参数
    /// </summary>
    public class BatteryLevelChangedEventArgs : EventArgs
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public int BatteryLevel { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsLowBattery { get; set; }
    }

    /// <summary>
    /// 电量监控器
    /// 负责监控蓝牙设备的电量状态
    /// </summary>
    public class BatteryMonitor : IDisposable
    {
        private BluetoothDeviceWrapper? _monitoredDevice;
        private ThreadingTimer? _monitorTimer;
        private int _lastBatteryLevel = -1;
        private bool _disposed = false;
        private TimeSpan _monitorInterval = TimeSpan.FromMinutes(1); // 默认1分钟检查一次
        private int _lowBatteryThreshold = 20; // 低电量阈值：20%
        private bool _isMonitoring = false;

        /// <summary>
        /// Battery Service UUID (标准蓝牙电池服务)
        /// </summary>
        private static readonly Guid BatteryServiceUuid = new Guid("0000180F-0000-1000-8000-00805F9B34FB");

        /// <summary>
        /// Battery Level Characteristic UUID (电池电量特征)
        /// </summary>
        private static readonly Guid BatteryLevelCharacteristicUuid = new Guid("00002A19-0000-1000-8000-00805F9B34FB");

        /// <summary>
        /// 电量变化事件
        /// </summary>
        public event EventHandler<BatteryLevelChangedEventArgs>? BatteryLevelChanged;

        /// <summary>
        /// 获取或设置监控间隔
        /// </summary>
        public TimeSpan MonitorInterval
        {
            get => _monitorInterval;
            set
            {
                if (value.TotalSeconds < 10)
                {
                    Logger.Warning("Monitor interval too short, using minimum 10 seconds");
                    _monitorInterval = TimeSpan.FromSeconds(10);
                }
                else
                {
                    _monitorInterval = value;
                    Logger.Info($"Monitor interval set to {value.TotalSeconds} seconds");
                }

                // 如果正在监控，重启定时器
                if (_isMonitoring && _monitoredDevice != null)
                {
                    StartMonitoring(_monitoredDevice, _monitorInterval);
                }
            }
        }

        /// <summary>
        /// 获取或设置低电量阈值（百分比）
        /// </summary>
        public int LowBatteryThreshold
        {
            get => _lowBatteryThreshold;
            set
            {
                if (value < 0 || value > 100)
                {
                    Logger.Warning($"Invalid threshold {value}, must be 0-100");
                    return;
                }
                _lowBatteryThreshold = value;
                Logger.Info($"Low battery threshold set to {value}%");
            }
        }

        /// <summary>
        /// 获取上次读取的电量值
        /// </summary>
        public int LastBatteryLevel => _lastBatteryLevel;

        /// <summary>
        /// 获取是否正在监控
        /// </summary>
        public bool IsMonitoring => _isMonitoring;

        /// <summary>
        /// 构造函数
        /// </summary>
        public BatteryMonitor()
        {
            Logger.Info("BatteryMonitor initialized");
        }

        /// <summary>
        /// 获取设备电量（一次性读取）
        /// </summary>
        /// <param name="device">蓝牙设备</param>
        /// <returns>电量百分比（0-100），失败返回null</returns>
        public async Task<int?> GetBatteryLevelAsync(BluetoothDeviceWrapper device)
        {
            if (device?.NativeDevice == null)
            {
                Logger.Warning("Device is null or not initialized");
                return null;
            }

            try
            {
                Logger.Debug($"Reading battery level for {device.Name}");

                // 注意：Windows.Devices.Bluetooth.BluetoothDevice 在某些SDK版本中
                // 可能不直接支持 GetGattServicesAsync 方法
                // 这里提供一个替代实现：通过 BluetoothLEDevice 访问
                
                // 尝试获取GATT服务（使用更兼容的方式）
                GattDeviceServicesResult? gattResult = null;
                
                try
                {
                    // 某些设备可能需要先建立GATT连接
                    // 这里使用反射或者直接访问来兼容不同的SDK版本
                    var deviceType = device.NativeDevice.GetType();
                    var method = deviceType.GetMethod("GetGattServicesAsync");
                    
                    if (method != null)
                    {
                        var task = method.Invoke(device.NativeDevice, new object[] { BluetoothCacheMode.Uncached }) as Task<GattDeviceServicesResult>;
                        if (task != null)
                        {
                            gattResult = await task;
                        }
                    }
                }
                catch
                {
                    // 如果反射失败，说明此API不可用
                }

                if (gattResult == null || gattResult.Status != GattCommunicationStatus.Success)
                {
                    Logger.Warning($"GATT services not available for {device.Name} - device may not support battery reporting");
                    Logger.Info($"Tip: Some Bluetooth devices do not expose battery level via standard GATT service");
                    return null;
                }

                // 查找电池服务
                var batteryService = gattResult.Services
                    .FirstOrDefault(s => s.Uuid == BatteryServiceUuid);

                if (batteryService == null)
                {
                    Logger.Warning($"Battery service not found on device {device.Name}");
                    return null;
                }

                // 获取电池电量特征
                var characteristicsResult = await batteryService.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                
                if (characteristicsResult.Status != GattCommunicationStatus.Success)
                {
                    Logger.Warning($"Failed to get characteristics: {characteristicsResult.Status}");
                    return null;
                }

                var batteryCharacteristic = characteristicsResult.Characteristics
                    .FirstOrDefault(c => c.Uuid == BatteryLevelCharacteristicUuid);

                if (batteryCharacteristic == null)
                {
                    Logger.Warning("Battery level characteristic not found");
                    return null;
                }

                // 读取电量值
                var readResult = await batteryCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
                
                if (readResult.Status != GattCommunicationStatus.Success)
                {
                    Logger.Warning($"Failed to read battery value: {readResult.Status}");
                    return null;
                }

                // 解析电量值（单字节，0-100）
                var reader = DataReader.FromBuffer(readResult.Value);
                int batteryLevel = reader.ReadByte();

                Logger.Info($"Battery level for {device.Name}: {batteryLevel}%");
                return batteryLevel;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error reading battery level for {device.Name}", ex);
                return null;
            }
        }

        /// <summary>
        /// 启动电量监控
        /// </summary>
        /// <param name="device">要监控的设备</param>
        /// <param name="interval">监控间隔（可选）</param>
        public void StartMonitoring(BluetoothDeviceWrapper device, TimeSpan? interval = null)
        {
            if (device == null)
            {
                Logger.Warning("Cannot start monitoring: device is null");
                return;
            }

            try
            {
                // 停止之前的监控
                StopMonitoring();

                if (interval.HasValue)
                {
                    _monitorInterval = interval.Value;
                }

                _monitoredDevice = device;
                _isMonitoring = true;
                _lastBatteryLevel = -1;

                Logger.Info($"Starting battery monitoring for {device.Name} (interval: {_monitorInterval.TotalSeconds}s)");

                // 立即读取一次电量
                _ = Task.Run(async () => await CheckBatteryAsync());

                // 启动定时器
                _monitorTimer = new ThreadingTimer(
                    async _ => await CheckBatteryAsync(),
                    null,
                    _monitorInterval,
                    _monitorInterval
                );

                Logger.Info($"Battery monitoring started for {device.Name}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to start battery monitoring", ex);
                _isMonitoring = false;
            }
        }

        /// <summary>
        /// 停止电量监控
        /// </summary>
        public void StopMonitoring()
        {
            if (!_isMonitoring)
                return;

            try
            {
                Logger.Info($"Stopping battery monitoring{(_monitoredDevice != null ? $" for {_monitoredDevice.Name}" : "")}");

                _monitorTimer?.Dispose();
                _monitorTimer = null;
                _isMonitoring = false;
                _lastBatteryLevel = -1;
                _monitoredDevice = null;

                Logger.Info("Battery monitoring stopped");
            }
            catch (Exception ex)
            {
                Logger.Error("Error stopping battery monitoring", ex);
            }
        }

        /// <summary>
        /// 检查电量（定时器回调）
        /// </summary>
        private async Task CheckBatteryAsync()
        {
            if (_monitoredDevice == null || _disposed)
                return;

            try
            {
                var batteryLevel = await GetBatteryLevelAsync(_monitoredDevice);

                if (batteryLevel.HasValue)
                {
                    // 检查电量是否变化
                    if (batteryLevel.Value != _lastBatteryLevel)
                    {
                        bool isLowBattery = batteryLevel.Value <= _lowBatteryThreshold;
                        
                        // 如果是低电量且之前不是低电量，额外记录
                        if (isLowBattery && _lastBatteryLevel > _lowBatteryThreshold)
                        {
                            Logger.Warning($"Low battery alert: {_monitoredDevice.Name} - {batteryLevel.Value}%");
                        }

                        _lastBatteryLevel = batteryLevel.Value;

                        // 触发事件
                        OnBatteryLevelChanged(new BatteryLevelChangedEventArgs
                        {
                            DeviceId = _monitoredDevice.Id,
                            DeviceName = _monitoredDevice.Name,
                            BatteryLevel = batteryLevel.Value,
                            IsLowBattery = isLowBattery
                        });
                    }
                }
                else
                {
                    Logger.Debug($"Failed to read battery level for {_monitoredDevice.Name}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error checking battery level", ex);
            }
        }

        /// <summary>
        /// 触发电量变化事件
        /// </summary>
        private void OnBatteryLevelChanged(BatteryLevelChangedEventArgs args)
        {
            try
            {
                BatteryLevelChanged?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                Logger.Error("Error in BatteryLevelChanged event handler", ex);
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            Logger.Info("Disposing BatteryMonitor");

            StopMonitoring();
            _disposed = true;

            Logger.Info("BatteryMonitor disposed");
        }
    }
}