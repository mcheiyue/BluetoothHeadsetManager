using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;
using BluetoothHeadsetManager.Utils;

namespace BluetoothHeadsetManager.Bluetooth
{
    /// <summary>
    /// 蓝牙适配器封装类
    /// </summary>
    public class BluetoothAdapter
    {
        private static BluetoothAdapter? _instance;
        private static readonly object _lock = new object();

        private Radio? _bluetoothRadio;
        private DeviceWatcher? _deviceWatcher;

        /// <summary>
        /// 设备发现事件
        /// </summary>
        public event EventHandler<DeviceInformation>? DeviceDiscovered;

        /// <summary>
        /// 设备移除事件
        /// </summary>
        public event EventHandler<DeviceInformationUpdate>? DeviceRemoved;

        /// <summary>
        /// 设备更新事件
        /// </summary>
        public event EventHandler<DeviceInformationUpdate>? DeviceUpdated;

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static BluetoothAdapter Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new BluetoothAdapter();
                        }
                    }
                }
                return _instance;
            }
        }

        private BluetoothAdapter()
        {
            Logger.Info("蓝牙适配器已创建");
        }

        /// <summary>
        /// 初始化蓝牙适配器
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                Logger.Info("正在初始化蓝牙适配器...");

                // 获取蓝牙Radio
                var radios = await Radio.GetRadiosAsync();
                _bluetoothRadio = radios.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);

                if (_bluetoothRadio == null)
                {
                    Logger.Error("未找到蓝牙适配器");
                    return false;
                }

                Logger.Info($"蓝牙适配器已找到: {_bluetoothRadio.Name}, 状态: {_bluetoothRadio.State}");

                // 检查蓝牙是否已启用
                if (_bluetoothRadio.State != RadioState.On)
                {
                    Logger.Warning($"蓝牙未启用，当前状态: {_bluetoothRadio.State}");
                    
                    // 尝试启用蓝牙
                    var accessStatus = await _bluetoothRadio.SetStateAsync(RadioState.On);
                    if (accessStatus == RadioAccessStatus.Allowed)
                    {
                        Logger.Info("蓝牙已启用");
                    }
                    else
                    {
                        Logger.Error($"无法启用蓝牙: {accessStatus}");
                        return false;
                    }
                }

                Logger.Info("蓝牙适配器初始化成功");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("初始化蓝牙适配器失败", ex);
                return false;
            }
        }

        /// <summary>
        /// 检查蓝牙是否可用
        /// </summary>
        public bool IsBluetoothAvailable()
        {
            return _bluetoothRadio != null && _bluetoothRadio.State == RadioState.On;
        }

        /// <summary>
        /// 获取蓝牙状态
        /// </summary>
        public RadioState GetBluetoothState()
        {
            return _bluetoothRadio?.State ?? RadioState.Unknown;
        }

        /// <summary>
        /// 开始扫描蓝牙设备
        /// </summary>
        public void StartDeviceWatcher()
        {
            try
            {
                if (_deviceWatcher != null)
                {
                    Logger.Warning("设备监视器已在运行");
                    return;
                }

                Logger.Info("开始扫描蓝牙设备...");

                // 创建设备选择器
                string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.IsPaired" };
                string deviceSelector = BluetoothDevice.GetDeviceSelector();

                _deviceWatcher = DeviceInformation.CreateWatcher(
                    deviceSelector,
                    requestedProperties,
                    DeviceInformationKind.AssociationEndpoint);

                // 订阅事件
                _deviceWatcher.Added += OnDeviceAdded;
                _deviceWatcher.Updated += OnDeviceUpdated;
                _deviceWatcher.Removed += OnDeviceRemoved;
                _deviceWatcher.EnumerationCompleted += OnEnumerationCompleted;
                _deviceWatcher.Stopped += OnWatcherStopped;

                // 开始监视
                _deviceWatcher.Start();

                Logger.Info("设备监视器已启动");
            }
            catch (Exception ex)
            {
                Logger.Error("启动设备监视器失败", ex);
            }
        }

        /// <summary>
        /// 停止扫描蓝牙设备
        /// </summary>
        public void StopDeviceWatcher()
        {
            try
            {
                if (_deviceWatcher == null)
                {
                    return;
                }

                Logger.Info("停止设备监视器...");

                // 取消订阅事件
                _deviceWatcher.Added -= OnDeviceAdded;
                _deviceWatcher.Updated -= OnDeviceUpdated;
                _deviceWatcher.Removed -= OnDeviceRemoved;
                _deviceWatcher.EnumerationCompleted -= OnEnumerationCompleted;
                _deviceWatcher.Stopped -= OnWatcherStopped;

                // 停止并释放
                if (_deviceWatcher.Status == DeviceWatcherStatus.Started ||
                    _deviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted)
                {
                    _deviceWatcher.Stop();
                }

                _deviceWatcher = null;
                Logger.Info("设备监视器已停止");
            }
            catch (Exception ex)
            {
                Logger.Error("停止设备监视器失败", ex);
            }
        }

        /// <summary>
        /// 获取已配对的蓝牙设备列表
        /// </summary>
        public async Task<List<DeviceInformation>> GetPairedDevicesAsync()
        {
            try
            {
                Logger.Info("正在获取已配对的蓝牙设备...");

                string deviceSelector = BluetoothDevice.GetDeviceSelector();
                var devices = await DeviceInformation.FindAllAsync(deviceSelector);

                var pairedDevices = devices.Where(d => 
                {
                    var isPaired = d.Pairing?.IsPaired ?? false;
                    return isPaired;
                }).ToList();

                Logger.Info($"找到 {pairedDevices.Count} 个已配对设备");
                return pairedDevices;
            }
            catch (Exception ex)
            {
                Logger.Error("获取已配对设备失败", ex);
                return new List<DeviceInformation>();
            }
        }

        /// <summary>
        /// 根据设备ID查找设备
        /// </summary>
        public async Task<DeviceInformation?> FindDeviceByIdAsync(string deviceId)
        {
            try
            {
                Logger.Debug($"查找设备: {deviceId}");
                
                var deviceInfo = await DeviceInformation.CreateFromIdAsync(deviceId);
                return deviceInfo;
            }
            catch (Exception ex)
            {
                Logger.Error($"查找设备失败: {deviceId}", ex);
                return null;
            }
        }

        #region 事件处理

        private void OnDeviceAdded(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            try
            {
                Logger.Debug($"发现设备: {deviceInfo.Name} ({deviceInfo.Id})");
                DeviceDiscovered?.Invoke(this, deviceInfo);
            }
            catch (Exception ex)
            {
                Logger.Error($"处理设备添加事件失败: {deviceInfo?.Name}", ex);
            }
        }

        private void OnDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            try
            {
                Logger.Debug($"设备更新: {deviceInfoUpdate.Id}");
                DeviceUpdated?.Invoke(this, deviceInfoUpdate);
            }
            catch (Exception ex)
            {
                Logger.Error($"处理设备更新事件失败: {deviceInfoUpdate?.Id}", ex);
            }
        }

        private void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            try
            {
                Logger.Debug($"设备移除: {deviceInfoUpdate.Id}");
                DeviceRemoved?.Invoke(this, deviceInfoUpdate);
            }
            catch (Exception ex)
            {
                Logger.Error($"处理设备移除事件失败: {deviceInfoUpdate?.Id}", ex);
            }
        }

        private void OnEnumerationCompleted(DeviceWatcher sender, object args)
        {
            Logger.Info("设备枚举完成");
        }

        private void OnWatcherStopped(DeviceWatcher sender, object args)
        {
            Logger.Info("设备监视器已停止");
        }

        #endregion
    }
}