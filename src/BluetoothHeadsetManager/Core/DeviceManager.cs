using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using BluetoothHeadsetManager.Bluetooth;
using BluetoothHeadsetManager.Models;
using BluetoothHeadsetManager.Utils;

namespace BluetoothHeadsetManager.Core
{
    /// <summary>
    /// 蓝牙设备管理器
    /// </summary>
    public class DeviceManager : IDisposable
    {
        private readonly BluetoothAdapter _adapter;
        private readonly Dictionary<string, BluetoothDeviceWrapper> _devices;
        private readonly object _devicesLock = new object();
        private bool _disposed = false;

        /// <summary>
        /// 设备列表变化事件
        /// </summary>
        public event EventHandler<DeviceInfo>? DeviceAdded;

        /// <summary>
        /// 设备移除事件
        /// </summary>
        public event EventHandler<string>? DeviceRemoved;

        /// <summary>
        /// 设备状态更新事件
        /// </summary>
        public event EventHandler<DeviceInfo>? DeviceUpdated;

        public DeviceManager()
        {
            _adapter = BluetoothAdapter.Instance;
            _devices = new Dictionary<string, BluetoothDeviceWrapper>();
            
            // 订阅适配器事件
            _adapter.DeviceDiscovered += OnDeviceDiscovered;
            _adapter.DeviceRemoved += OnDeviceRemoved;
            _adapter.DeviceUpdated += OnDeviceUpdated;

            Logger.Info("设备管理器已创建");
        }

        /// <summary>
        /// 初始化设备管理器
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                Logger.Info("正在初始化设备管理器...");

                // 初始化蓝牙适配器
                bool initialized = await _adapter.InitializeAsync();
                if (!initialized)
                {
                    Logger.Error("蓝牙适配器初始化失败");
                    return false;
                }

                Logger.Info("设备管理器初始化成功");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("初始化设备管理器失败", ex);
                return false;
            }
        }

        /// <summary>
        /// 开始扫描设备
        /// </summary>
        public void StartScan()
        {
            try
            {
                Logger.Info("开始扫描蓝牙设备");
                _adapter.StartDeviceWatcher();
            }
            catch (Exception ex)
            {
                Logger.Error("开始扫描设备失败", ex);
            }
        }

        /// <summary>
        /// 停止扫描设备
        /// </summary>
        public void StopScan()
        {
            try
            {
                Logger.Info("停止扫描蓝牙设备");
                _adapter.StopDeviceWatcher();
            }
            catch (Exception ex)
            {
                Logger.Error("停止扫描设备失败", ex);
            }
        }

        /// <summary>
        /// 获取所有已配对的设备
        /// </summary>
        public async Task<List<DeviceInfo>> GetPairedDevicesAsync()
        {
            try
            {
                Logger.Info("获取已配对的设备列表");
                
                var deviceInfoList = await _adapter.GetPairedDevicesAsync();
                var result = new List<DeviceInfo>();

                foreach (var deviceInfo in deviceInfoList)
                {
                    try
                    {
                        var wrapper = await BluetoothDeviceWrapper.FromDeviceInformationAsync(deviceInfo);
                        if (wrapper != null)
                        {
                            result.Add(wrapper.DeviceInfo);
                            
                            // 缓存设备
                            lock (_devicesLock)
                            {
                                if (!_devices.ContainsKey(wrapper.DeviceInfo.Id))
                                {
                                    _devices[wrapper.DeviceInfo.Id] = wrapper;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"处理设备信息失败: {deviceInfo.Name}", ex);
                    }
                }

                Logger.Info($"找到 {result.Count} 个已配对设备");
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error("获取已配对设备失败", ex);
                return new List<DeviceInfo>();
            }
        }

        /// <summary>
        /// 根据设备ID查找设备
        /// </summary>
        public async Task<BluetoothDeviceWrapper?> FindDeviceAsync(string deviceId)
        {
            try
            {
                // 先从缓存查找
                lock (_devicesLock)
                {
                    if (_devices.TryGetValue(deviceId, out var cachedDevice))
                    {
                        Logger.Debug($"从缓存获取设备: {cachedDevice.DeviceInfo.Name}");
                        return cachedDevice;
                    }
                }

                // 缓存中没有，从系统查找
                Logger.Info($"从系统查找设备: {deviceId}");
                var device = await BluetoothDeviceWrapper.FromIdAsync(deviceId);
                
                if (device != null)
                {
                    // 添加到缓存
                    lock (_devicesLock)
                    {
                        _devices[deviceId] = device;
                    }
                }

                return device;
            }
            catch (Exception ex)
            {
                Logger.Error($"查找设备失败: {deviceId}", ex);
                return null;
            }
        }

        /// <summary>
        /// 获取所有缓存的设备
        /// </summary>
        public List<DeviceInfo> GetCachedDevices()
        {
            lock (_devicesLock)
            {
                return _devices.Values.Select(d => d.DeviceInfo).ToList();
            }
        }

        /// <summary>
        /// 清除设备缓存
        /// </summary>
        public void ClearCache()
        {
            lock (_devicesLock)
            {
                foreach (var device in _devices.Values)
                {
                    try
                    {
                        device.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"释放设备失败: {device.DeviceInfo.Name}", ex);
                    }
                }
                _devices.Clear();
                Logger.Info("设备缓存已清除");
            }
        }

        /// <summary>
        /// 移除指定设备
        /// </summary>
        public void RemoveDevice(string deviceId)
        {
            lock (_devicesLock)
            {
                if (_devices.TryGetValue(deviceId, out var device))
                {
                    try
                    {
                        device.Dispose();
                        _devices.Remove(deviceId);
                        Logger.Info($"设备已移除: {device.DeviceInfo.Name}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"移除设备失败: {device.DeviceInfo.Name}", ex);
                    }
                }
            }
        }

        #region 事件处理

        private async void OnDeviceDiscovered(object? sender, DeviceInformation deviceInfo)
        {
            try
            {
                Logger.Debug($"发现新设备: {deviceInfo.Name}");

                var wrapper = await BluetoothDeviceWrapper.FromDeviceInformationAsync(deviceInfo);
                if (wrapper != null)
                {
                    lock (_devicesLock)
                    {
                        if (!_devices.ContainsKey(wrapper.DeviceInfo.Id))
                        {
                            _devices[wrapper.DeviceInfo.Id] = wrapper;
                            DeviceAdded?.Invoke(this, wrapper.DeviceInfo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"处理设备发现事件失败: {deviceInfo.Name}", ex);
            }
        }

        private void OnDeviceRemoved(object? sender, DeviceInformationUpdate update)
        {
            try
            {
                Logger.Debug($"设备移除: {update.Id}");

                lock (_devicesLock)
                {
                    if (_devices.TryGetValue(update.Id, out var device))
                    {
                        device.Dispose();
                        _devices.Remove(update.Id);
                        DeviceRemoved?.Invoke(this, update.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"处理设备移除事件失败: {update.Id}", ex);
            }
        }

        private void OnDeviceUpdated(object? sender, DeviceInformationUpdate update)
        {
            try
            {
                Logger.Debug($"设备更新: {update.Id}");

                lock (_devicesLock)
                {
                    if (_devices.TryGetValue(update.Id, out var device))
                    {
                        device.UpdateConnectionStatus();
                        DeviceUpdated?.Invoke(this, device.DeviceInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"处理设备更新事件失败: {update.Id}", ex);
            }
        }

        #endregion

        #region IDisposable实现

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        // 停止扫描
                        StopScan();

                        // 取消订阅事件
                        _adapter.DeviceDiscovered -= OnDeviceDiscovered;
                        _adapter.DeviceRemoved -= OnDeviceRemoved;
                        _adapter.DeviceUpdated -= OnDeviceUpdated;

                        // 清除缓存
                        ClearCache();

                        Logger.Info("设备管理器资源已释放");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("释放设备管理器资源失败", ex);
                    }
                }

                _disposed = true;
            }
        }

        ~DeviceManager()
        {
            Dispose(false);
        }

        #endregion
    }
}