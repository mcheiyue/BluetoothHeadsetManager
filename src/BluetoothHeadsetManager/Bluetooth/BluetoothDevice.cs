using System;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using BluetoothHeadsetManager.Models;
using BluetoothHeadsetManager.Utils;

namespace BluetoothHeadsetManager.Bluetooth
{
    /// <summary>
    /// 蓝牙设备封装类
    /// </summary>
    public class BluetoothDeviceWrapper : IDisposable
    {
        private Windows.Devices.Bluetooth.BluetoothDevice? _device;
        private bool _disposed = false;

        /// <summary>
        /// 设备信息
        /// </summary>
        public DeviceInfo DeviceInfo { get; private set; }

        /// <summary>
        /// Windows 蓝牙设备对象
        /// </summary>
        public Windows.Devices.Bluetooth.BluetoothDevice? NativeDevice => _device;

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected => _device?.ConnectionStatus == BluetoothConnectionStatus.Connected;

        public BluetoothDeviceWrapper(DeviceInfo deviceInfo)
        {
            DeviceInfo = deviceInfo ?? throw new ArgumentNullException(nameof(deviceInfo));
        }

        /// <summary>
        /// 从设备ID创建蓝牙设备
        /// </summary>
        public static async Task<BluetoothDeviceWrapper?> FromIdAsync(string deviceId)
        {
            try
            {
                Logger.Info($"正在创建蓝牙设备: {deviceId}");
                
                var device = await Windows.Devices.Bluetooth.BluetoothDevice.FromIdAsync(deviceId);
                if (device == null)
                {
                    Logger.Warning($"无法创建蓝牙设备: {deviceId}");
                    return null;
                }

                var deviceInfo = new DeviceInfo
                {
                    Id = device.DeviceId,
                    Name = device.Name,
                    Address = FormatBluetoothAddress(device.BluetoothAddress),
                    IsPaired = true, // FromId只能获取已配对的设备
                    IsConnected = device.ConnectionStatus == BluetoothConnectionStatus.Connected
                };

                var wrapper = new BluetoothDeviceWrapper(deviceInfo)
                {
                    _device = device
                };

                Logger.Info($"蓝牙设备创建成功: {device.Name} ({deviceInfo.Address})");
                return wrapper;
            }
            catch (Exception ex)
            {
                Logger.Error($"创建蓝牙设备失败: {deviceId}", ex);
                return null;
            }
        }

        /// <summary>
        /// 从DeviceInformation创建蓝牙设备
        /// </summary>
        public static async Task<BluetoothDeviceWrapper?> FromDeviceInformationAsync(DeviceInformation deviceInformation)
        {
            try
            {
                if (deviceInformation == null)
                    return null;

                Logger.Debug($"从DeviceInformation创建设备: {deviceInformation.Name}");

                var device = await Windows.Devices.Bluetooth.BluetoothDevice.FromIdAsync(deviceInformation.Id);
                if (device == null)
                {
                    Logger.Warning($"无法从DeviceInformation创建设备: {deviceInformation.Id}");
                    return null;
                }

                var deviceInfo = new DeviceInfo
                {
                    Id = device.DeviceId,
                    Name = device.Name,
                    Address = FormatBluetoothAddress(device.BluetoothAddress),
                    IsPaired = (bool)(deviceInformation.Pairing?.IsPaired ?? false),
                    IsConnected = device.ConnectionStatus == BluetoothConnectionStatus.Connected
                };

                var wrapper = new BluetoothDeviceWrapper(deviceInfo)
                {
                    _device = device
                };

                return wrapper;
            }
            catch (Exception ex)
            {
                Logger.Error($"从DeviceInformation创建设备失败: {deviceInformation?.Id}", ex);
                return null;
            }
        }

        /// <summary>
        /// 连接到设备（在Windows中，蓝牙连接通常由系统自动管理）
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            try
            {
                if (_device == null)
                {
                    Logger.Warning("设备未初始化，无法连接");
                    return false;
                }

                Logger.Info($"请求连接设备: {DeviceInfo.Name}");

                // 注意：Windows.Devices.Bluetooth.BluetoothDevice 不提供显式的连接方法
                // 连接是在访问设备服务时自动建立的
                // 这里我们只检查当前连接状态
                
                // 等待一小段时间让系统建立连接
                await Task.Delay(1000);
                
                UpdateConnectionStatus();
                
                if (IsConnected)
                {
                    Logger.Info($"设备已连接: {DeviceInfo.Name}");
                    return true;
                }
                else
                {
                    Logger.Warning($"设备未连接: {DeviceInfo.Name} - 请在Windows蓝牙设置中手动连接设备");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"检查设备连接状态失败: {DeviceInfo.Name}", ex);
                return false;
            }
        }

        /// <summary>
        /// 断开设备连接
        /// </summary>
        public void Disconnect()
        {
            try
            {
                Logger.Info($"断开设备连接: {DeviceInfo.Name}");
                
                // 释放设备对象会触发断开
                _device?.Dispose();
                _device = null;
                
                DeviceInfo.IsConnected = false;
                Logger.Info($"设备已断开: {DeviceInfo.Name}");
            }
            catch (Exception ex)
            {
                Logger.Error($"断开设备失败: {DeviceInfo.Name}", ex);
            }
        }

        /// <summary>
        /// 更新连接状态
        /// </summary>
        public void UpdateConnectionStatus()
        {
            if (_device != null)
            {
                DeviceInfo.IsConnected = IsConnected;
            }
        }

        /// <summary>
        /// 格式化蓝牙地址
        /// </summary>
        private static string FormatBluetoothAddress(ulong address)
        {
            byte[] bytes = BitConverter.GetBytes(address);
            Array.Reverse(bytes);
            
            // 只取后6个字节（蓝牙地址是48位）
            return string.Format("{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}",
                bytes[2], bytes[3], bytes[4], bytes[5], bytes[6], bytes[7]);
        }

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
                        _device?.Dispose();
                        _device = null;
                        Logger.Debug($"BluetoothDevice资源已释放: {DeviceInfo.Name}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"释放BluetoothDevice资源失败: {DeviceInfo.Name}", ex);
                    }
                }

                _disposed = true;
            }
        }

        ~BluetoothDeviceWrapper()
        {
            Dispose(false);
        }

        #endregion
    }
}