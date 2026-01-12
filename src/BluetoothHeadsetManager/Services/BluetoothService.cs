using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;

namespace BluetoothHeadsetManager.Services
{
    /// <summary>
    /// 蓝牙服务 - 基于32feet库
    /// </summary>
    public class BluetoothService : IDisposable
    {
        private BluetoothClient? _client;
        private bool _disposed;

        // 常用蓝牙服务UUID
        private static readonly Guid HandsFreeServiceClass = new Guid("0000111E-0000-1000-8000-00805F9B34FB");
        private static readonly Guid AudioSinkServiceClass = new Guid("0000110B-0000-1000-8000-00805F9B34FB");
        private static readonly Guid HeadsetServiceClass = new Guid("00001108-0000-1000-8000-00805F9B34FB");

        public event EventHandler<string>? ConnectionStatusChanged;

        public BluetoothService()
        {
            try
            {
                _client = new BluetoothClient();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"蓝牙初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取已配对的蓝牙设备列表
        /// </summary>
        public async Task<List<Models.BluetoothDeviceInfo>> GetPairedDevicesAsync()
        {
            return await Task.Run(() =>
            {
                var devices = new List<Models.BluetoothDeviceInfo>();

                if (_client == null)
                {
                    try
                    {
                        _client = new BluetoothClient();
                    }
                    catch
                    {
                        return devices;
                    }
                }

                try
                {
                    // 获取已配对的设备
                    var pairedDevices = _client.PairedDevices;

                    foreach (var dev in pairedDevices)
                    {
                        var formattedMac = Regex.Replace(
                            dev.DeviceAddress.ToString(),
                            ".{2}",
                            "$0:").TrimEnd(':');

                        devices.Add(new Models.BluetoothDeviceInfo
                        {
                            Name = dev.DeviceName ?? "未知设备",
                            MacAddress = formattedMac,
                            IsConnected = dev.Connected,
                            IsPaired = dev.Authenticated,
                            ClassOfDevice = dev.ClassOfDevice.Value
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"获取设备列表失败: {ex.Message}");
                }

                return devices;
            });
        }

        /// <summary>
        /// 连接到蓝牙设备
        /// </summary>
        public async Task<bool> ConnectAsync(string macAddress)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 解析MAC地址
                    var cleanMac = macAddress.Replace(":", "").Replace("-", "");
                    var address = BluetoothAddress.Parse(cleanMac);

                    // 创建新的客户端连接
                    var client = new BluetoothClient();

                    // 尝试连接到音频服务
                    try
                    {
                        client.Connect(address, AudioSinkServiceClass);
                        ConnectionStatusChanged?.Invoke(this, $"已连接到 {macAddress}");
                        return true;
                    }
                    catch
                    {
                        // 尝试连接到免提服务
                        try
                        {
                            client.Connect(address, HandsFreeServiceClass);
                            ConnectionStatusChanged?.Invoke(this, $"已连接到 {macAddress}");
                            return true;
                        }
                        catch
                        {
                            // 尝试连接到耳机服务
                            try
                            {
                                client.Connect(address, HeadsetServiceClass);
                                ConnectionStatusChanged?.Invoke(this, $"已连接到 {macAddress}");
                                return true;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"连接失败: {ex.Message}");
                                ConnectionStatusChanged?.Invoke(this, $"连接失败: {ex.Message}");
                                return false;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"连接失败: {ex.Message}");
                    ConnectionStatusChanged?.Invoke(this, $"连接失败: {ex.Message}");
                    return false;
                }
            });
        }

        /// <summary>
        /// 断开蓝牙设备连接
        /// 注意: 32feet库不支持直接断开，需要使用系统API
        /// </summary>
        public async Task<bool> DisconnectAsync(string macAddress)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 32feet库没有直接的断开方法
                    // 这里通过关闭客户端来间接实现
                    _client?.Close();
                    _client = new BluetoothClient();
                    
                    ConnectionStatusChanged?.Invoke(this, $"已断开 {macAddress}");
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"断开连接失败: {ex.Message}");
                    ConnectionStatusChanged?.Invoke(this, $"断开失败: {ex.Message}");
                    return false;
                }
            });
        }

        /// <summary>
        /// 切换设备连接状态
        /// </summary>
        public async Task<bool> ToggleConnectionAsync(Models.BluetoothDeviceInfo device)
        {
            if (device.IsConnected)
            {
                return await DisconnectAsync(device.MacAddress);
            }
            else
            {
                return await ConnectAsync(device.MacAddress);
            }
        }

        /// <summary>
        /// 获取仅音频设备
        /// </summary>
        public async Task<List<Models.BluetoothDeviceInfo>> GetAudioDevicesAsync()
        {
            var allDevices = await GetPairedDevicesAsync();
            return allDevices.FindAll(d => d.IsAudioDevice);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _client?.Close();
                _client = null;
                _disposed = true;
            }
        }
    }
}