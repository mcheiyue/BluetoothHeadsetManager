using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BluetoothHeadsetManager.Interop;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth;

namespace BluetoothHeadsetManager.Services
{
    /// <summary>
    /// 统一的蓝牙服务 - 整合音频设备和通用蓝牙设备的连接管理
    /// 使用 Windows Runtime API 获取所有蓝牙设备（参考 BlueGauge）
    /// 使用 IKsControl 方式连接音频设备
    /// </summary>
    public class BluetoothService : IDisposable
    {
        private BluetoothClient? _client;
        private readonly BluetoothAudioService _audioService;
        private bool _disposed;

        // 缓存的音频设备信息 - 按多种键索引以确保匹配
        private readonly Dictionary<string, BluetoothAudioDeviceInfo> _nameToAudioDevice = new();
        // 按 MAC 地址索引
        private readonly Dictionary<string, BluetoothAudioDeviceInfo> _macToAudioDevice = new();
        
        // 设备监视器
        private DeviceWatcher? _deviceWatcher;

        public event EventHandler<string>? ConnectionStatusChanged;
        
        // 设备列表变更事件
        public event EventHandler? DeviceListChanged;

        public BluetoothService()
        {
            _audioService = new BluetoothAudioService();
            _audioService.StatusChanged += (s, msg) => ConnectionStatusChanged?.Invoke(this, msg);

            try
            {
                _client = new BluetoothClient();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"蓝牙初始化失败: {ex.Message}");
            }
            
            InitializeDeviceWatcher();
        }
        
        /// <summary>
        /// 初始化设备监视器
        /// </summary>
        private void InitializeDeviceWatcher()
        {
            try
            {
                // 仅监听已配对的蓝牙设备
                string selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
                
                // 请求额外的属性，如连接状态
                string[] requestedProperties = {
                    "System.Devices.Aep.IsConnected",
                    "System.Devices.Aep.Bluetooth.Le.IsConnectable"
                };

                _deviceWatcher = DeviceInformation.CreateWatcher(
                    selector,
                    requestedProperties,
                    DeviceInformationKind.AssociationEndpoint);

                _deviceWatcher.Added += DeviceWatcher_Added;
                _deviceWatcher.Updated += DeviceWatcher_Updated;
                _deviceWatcher.Removed += DeviceWatcher_Removed;
                
                Logger.Log("启动设备状态监视器...");
                _deviceWatcher.Start();
            }
            catch (Exception ex)
            {
                Logger.LogError("初始化设备监视器失败", ex);
            }
        }

        private void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            // 当有新设备添加（或首次扫描发现）时触发
            // 注意：首次启动时会为每个已配对设备触发一次 Added
            Logger.Log($"设备监视器: 发现设备 {deviceInfo.Name} (ID: {deviceInfo.Id})");
            // 我们不在这里立即刷新，因为启动时会大量触发，最好由 UI 决定何时初次加载
            // 但如果是后续添加的新设备，可以考虑触发
        }

        private void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            // 当设备属性变更（如连接状态改变）时触发
            // 这是一个非常频繁的事件，需要过滤
            
            // 检查是否有连接状态变化
            if (deviceInfoUpdate.Properties.ContainsKey("System.Devices.Aep.IsConnected"))
            {
                var isConnected = (bool)deviceInfoUpdate.Properties["System.Devices.Aep.IsConnected"];
                Logger.Log($"设备监视器: 设备更新 {deviceInfoUpdate.Id} - 连接状态: {isConnected}");
                
                // 触发刷新事件
                // 为了避免过于频繁的刷新，可以在 ViewModel 层做防抖处理，或者在这里做一个简单的节流
                DeviceListChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            Logger.Log($"设备监视器: 设备移除 {deviceInfoUpdate.Id}");
            DeviceListChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 获取已配对的蓝牙设备列表
        /// 使用 Windows Runtime API (参考 BlueGauge 和 ToothTray 的实现)
        /// </summary>
        public async Task<List<Models.BluetoothDeviceInfo>> GetPairedDevicesAsync()
        {
            var devices = new List<Models.BluetoothDeviceInfo>();

            Logger.LogSection("开始获取已配对蓝牙设备");

            // 首先枚举音频设备，获取 IKsControl 接口
            Logger.Log("步骤1: 枚举音频设备...");
            var audioDevices = await Task.Run(() => _audioService.EnumerateAudioDevices());
            _nameToAudioDevice.Clear();
            _macToAudioDevice.Clear();

            Logger.Log($"找到 {audioDevices.Count} 个蓝牙音频设备 (带 IKsControl):");
            
            foreach (var audioDevice in audioDevices)
            {
                // 按名称索引
                _nameToAudioDevice[audioDevice.Name] = audioDevice;
                // 同时按小写名称索引，方便匹配
                _nameToAudioDevice[audioDevice.Name.ToLowerInvariant()] = audioDevice;
                Logger.Log($"  音频设备: '{audioDevice.Name}' (已连接: {audioDevice.IsConnected}, KsControls: {audioDevice.KsControls.Count})");
            }

            try
            {
                // 使用 Windows Runtime API 获取所有已配对的蓝牙设备
                Logger.Log("步骤2: 使用 Windows Runtime API 获取蓝牙设备...");
                string selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
                Logger.Log($"  选择器: {selector}");
                
                var deviceInfos = await DeviceInformation.FindAllAsync(selector);
                Logger.Log($"  Windows Runtime API 找到 {deviceInfos.Count} 个已配对蓝牙设备:");

                foreach (var deviceInfo in deviceInfos)
                {
                    try
                    {
                        Logger.Log($"  处理设备: {deviceInfo.Name} (ID: {deviceInfo.Id})");
                        var btDevice = await BluetoothDevice.FromIdAsync(deviceInfo.Id);
                        if (btDevice == null)
                        {
                            Logger.Log($"    跳过: FromIdAsync 返回 null");
                            continue;
                        }

                        // 格式化 MAC 地址
                        ulong address = btDevice.BluetoothAddress;
                        string formattedMac = FormatMacAddress(address);
                        string deviceName = btDevice.Name ?? "未知设备";
                        
                        Logger.Log($"    设备名: '{deviceName}', MAC: {formattedMac}");

                        // 检查是否在音频设备列表中 - 使用多种匹配策略
                        BluetoothAudioDeviceInfo? matchingAudioDevice = FindMatchingAudioDevice(deviceName);
                        
                        // 如果找到匹配的音频设备，建立 MAC 地址映射
                        if (matchingAudioDevice != null)
                        {
                            _macToAudioDevice[formattedMac] = matchingAudioDevice;
                            _nameToAudioDevice[deviceName] = matchingAudioDevice;
                            Logger.Log($"    匹配到音频设备: '{matchingAudioDevice.Name}', KsControls: {matchingAudioDevice.KsControls.Count}");
                        }
                        else
                        {
                            Logger.Log($"    未匹配到音频设备");
                        }

                        // 获取连接状态
                        bool isConnected = btDevice.ConnectionStatus == BluetoothConnectionStatus.Connected;
                        if (matchingAudioDevice != null)
                        {
                            isConnected = matchingAudioDevice.IsConnected;
                        }

                        // 获取设备类型
                        uint classOfDevice = (uint)btDevice.ClassOfDevice.RawValue;

                        var info = new Models.BluetoothDeviceInfo
                        {
                            Name = deviceName,
                            MacAddress = formattedMac,
                            IsConnected = isConnected,
                            IsPaired = true,
                            ClassOfDevice = classOfDevice,
                            SupportsKsControl = matchingAudioDevice != null
                        };

                        devices.Add(info);
                        Logger.Log($"    添加设备: '{deviceName}' 已连接={isConnected}, 支持KsControl={info.SupportsKsControl}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"  处理设备 {deviceInfo.Name} 失败", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Windows Runtime API 获取设备失败，回退到 32feet 库", ex);
                await FallbackGetPairedDevicesAsync(devices);
            }

            Logger.Log($"蓝牙设备扫描完成: 共 {devices.Count} 个设备");
            Logger.Log($"_nameToAudioDevice 包含 {_nameToAudioDevice.Count} 个条目");
            Logger.Log($"_macToAudioDevice 包含 {_macToAudioDevice.Count} 个条目");
            
            return devices;
        }
        
        /// <summary>
        /// 查找匹配的音频设备 - 使用多种匹配策略
        /// </summary>
        private BluetoothAudioDeviceInfo? FindMatchingAudioDevice(string deviceName)
        {
            // 策略1: 精确匹配
            if (_nameToAudioDevice.TryGetValue(deviceName, out var audioDevice))
            {
                return audioDevice;
            }
            
            // 策略2: 忽略大小写匹配
            if (_nameToAudioDevice.TryGetValue(deviceName.ToLowerInvariant(), out audioDevice))
            {
                return audioDevice;
            }
            
            // 策略3: 包含匹配
            foreach (var kvp in _nameToAudioDevice)
            {
                // 跳过小写版本的重复条目
                if (kvp.Key != kvp.Value.Name) continue;
                
                if (kvp.Key.Contains(deviceName, StringComparison.OrdinalIgnoreCase) ||
                    deviceName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }
            
            return null;
        }

        /// <summary>
        /// 备选方案：使用 32feet 库获取设备
        /// </summary>
        private async Task FallbackGetPairedDevicesAsync(List<Models.BluetoothDeviceInfo> devices)
        {
            await Task.Run(() =>
            {
                if (_client == null)
                {
                    try
                    {
                        _client = new BluetoothClient();
                    }
                    catch
                    {
                        return;
                    }
                }

                try
                {
                    var pairedDevices = _client.PairedDevices;
                    System.Diagnostics.Debug.WriteLine($"32feet 库找到 {pairedDevices.Count()} 个已配对设备:");

                    foreach (var dev in pairedDevices)
                    {
                        var formattedMac = Regex.Replace(
                            dev.DeviceAddress.ToString(),
                            ".{2}",
                            "$0:").TrimEnd(':');

                        // 检查是否已经添加
                        if (devices.Exists(d => d.MacAddress.Equals(formattedMac, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        string deviceName = dev.DeviceName ?? "未知设备";
                        
                        // 检查是否有匹配的音频设备
                        bool hasKsControl = _nameToAudioDevice.ContainsKey(deviceName);

                        var deviceInfo = new Models.BluetoothDeviceInfo
                        {
                            Name = deviceName,
                            MacAddress = formattedMac,
                            IsConnected = dev.Connected,
                            IsPaired = dev.Authenticated,
                            ClassOfDevice = dev.ClassOfDevice.Value,
                            SupportsKsControl = hasKsControl
                        };

                        devices.Add(deviceInfo);
                        System.Diagnostics.Debug.WriteLine($"  - {deviceName} ({formattedMac}) 已连接: {dev.Connected}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"32feet 获取设备失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 格式化 MAC 地址
        /// </summary>
        private static string FormatMacAddress(ulong address)
        {
            byte[] bytes = BitConverter.GetBytes(address);
            return $"{bytes[5]:X2}:{bytes[4]:X2}:{bytes[3]:X2}:{bytes[2]:X2}:{bytes[1]:X2}:{bytes[0]:X2}";
        }

        /// <summary>
        /// 连接到蓝牙设备
        /// 使用 IKsControl 方式（音频设备）
        /// </summary>
        public async Task<bool> ConnectAsync(string macAddress)
        {
            // 先查找设备名称
            string? deviceName = null;
            
            // 尝试通过 Windows Runtime API 获取设备名称
            try
            {
                string selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
                var deviceInfos = await DeviceInformation.FindAllAsync(selector);
                
                foreach (var deviceInfo in deviceInfos)
                {
                    var btDevice = await BluetoothDevice.FromIdAsync(deviceInfo.Id);
                    if (btDevice != null)
                    {
                        string formattedMac = FormatMacAddress(btDevice.BluetoothAddress);
                        if (formattedMac.Equals(macAddress, StringComparison.OrdinalIgnoreCase))
                        {
                            deviceName = btDevice.Name;
                            break;
                        }
                    }
                }
            }
            catch { }

            // 使用 IKsControl 方式（适用于音频设备）
            BluetoothAudioDeviceInfo? audioDevice = null;
            
            if (deviceName != null && _nameToAudioDevice.TryGetValue(deviceName, out audioDevice))
            {
                // 找到了
            }
            else
            {
                // 尝试模糊匹配
                foreach (var kvp in _nameToAudioDevice)
                {
                    audioDevice = kvp.Value;
                    break; // 这里有问题，应该更精确匹配
                }
            }

            if (audioDevice != null)
            {
                ConnectionStatusChanged?.Invoke(this, $"正在使用 IKsControl 连接 {audioDevice.Name}...");
                System.Diagnostics.Debug.WriteLine($"连接设备: {audioDevice.Name}, KsControls 数量: {audioDevice.KsControls.Count}");
                
                var result = await _audioService.ConnectAsync(audioDevice);
                if (result)
                {
                    ConnectionStatusChanged?.Invoke(this, $"已连接 {audioDevice.Name}");
                    return true;
                }
                else
                {
                    ConnectionStatusChanged?.Invoke(this, $"连接 {audioDevice.Name} 失败");
                    return false;
                }
            }

            ConnectionStatusChanged?.Invoke(this, $"设备 {macAddress} 不支持通过此应用连接（非音频设备或未找到 IKsControl）");
            return false;
        }

        /// <summary>
        /// 断开蓝牙设备连接
        /// 使用 IKsControl 方式
        /// </summary>
        public async Task<bool> DisconnectAsync(string macAddress)
        {
            // 先查找设备名称
            string? deviceName = null;
            
            try
            {
                string selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
                var deviceInfos = await DeviceInformation.FindAllAsync(selector);
                
                foreach (var deviceInfo in deviceInfos)
                {
                    var btDevice = await BluetoothDevice.FromIdAsync(deviceInfo.Id);
                    if (btDevice != null)
                    {
                        string formattedMac = FormatMacAddress(btDevice.BluetoothAddress);
                        if (formattedMac.Equals(macAddress, StringComparison.OrdinalIgnoreCase))
                        {
                            deviceName = btDevice.Name;
                            break;
                        }
                    }
                }
            }
            catch { }

            // 使用 IKsControl 方式
            BluetoothAudioDeviceInfo? audioDevice = null;
            
            if (deviceName != null && _nameToAudioDevice.TryGetValue(deviceName, out audioDevice))
            {
                // 找到了
            }
            else
            {
                // 尝试模糊匹配
                foreach (var kvp in _nameToAudioDevice)
                {
                    audioDevice = kvp.Value;
                    break;
                }
            }

            if (audioDevice != null)
            {
                ConnectionStatusChanged?.Invoke(this, $"正在使用 IKsControl 断开 {audioDevice.Name}...");
                System.Diagnostics.Debug.WriteLine($"断开设备: {audioDevice.Name}, KsControls 数量: {audioDevice.KsControls.Count}");
                
                var result = await _audioService.DisconnectAsync(audioDevice);
                if (result)
                {
                    ConnectionStatusChanged?.Invoke(this, $"已断开 {audioDevice.Name}");
                    return true;
                }
                else
                {
                    ConnectionStatusChanged?.Invoke(this, $"断开 {audioDevice.Name} 失败");
                    return false;
                }
            }

            ConnectionStatusChanged?.Invoke(this, $"设备 {macAddress} 不支持通过此应用断开");
            return false;
        }

        /// <summary>
        /// 切换设备连接状态
        /// </summary>
        public async Task<bool> ToggleConnectionAsync(Models.BluetoothDeviceInfo device)
        {
            Logger.LogSection($"ToggleConnectionAsync: {device.Name}");
            Logger.Log($"设备信息: MAC={device.MacAddress}, 已连接={device.IsConnected}, 支持KsControl={device.SupportsKsControl}");
            Logger.Log($"_nameToAudioDevice 包含 {_nameToAudioDevice.Count} 个条目");
            Logger.Log($"_macToAudioDevice 包含 {_macToAudioDevice.Count} 个条目");
            
            // 列出所有已知的音频设备映射
            Logger.Log("已知的 MAC 映射:");
            foreach (var kvp in _macToAudioDevice)
            {
                Logger.Log($"  {kvp.Key} -> {kvp.Value.Name} (KsControls: {kvp.Value.KsControls.Count})");
            }
            
            // 策略1: 通过 MAC 地址查找（最可靠）
            if (_macToAudioDevice.TryGetValue(device.MacAddress, out var audioDevice))
            {
                Logger.Log($"策略1成功: 通过 MAC 地址找到音频设备: {audioDevice.Name}");
                return await PerformToggle(audioDevice, device.IsConnected, device.MacAddress);
            }
            Logger.Log($"策略1失败: MAC 地址 {device.MacAddress} 未找到");
            
            // 策略2: 通过设备名称查找
            if (_nameToAudioDevice.TryGetValue(device.Name, out audioDevice))
            {
                Logger.Log($"策略2成功: 通过名称找到音频设备: {audioDevice.Name}");
                return await PerformToggle(audioDevice, device.IsConnected, device.MacAddress);
            }
            Logger.Log($"策略2失败: 名称 '{device.Name}' 未找到");
            
            // 策略3: 模糊匹配
            audioDevice = FindMatchingAudioDevice(device.Name);
            if (audioDevice != null)
            {
                Logger.Log($"策略3成功: 通过模糊匹配找到音频设备: {audioDevice.Name}");
                return await PerformToggle(audioDevice, device.IsConnected, device.MacAddress);
            }
            Logger.Log($"策略3失败: 模糊匹配也未找到");
            
            Logger.Log($"未找到匹配的音频设备: {device.Name}，尝试直接使用标准 API 回退...");
            return await ToggleWithStandardApi(device.MacAddress, !device.IsConnected);
        }
        
        /// <summary>
        /// 执行连接/断开操作
        /// 注意：每次操作前都需要重新获取最新的 IKsControl 接口，因为 COM 对象可能已经失效
        /// </summary>
        private async Task<bool> PerformToggle(BluetoothAudioDeviceInfo audioDevice, bool isCurrentlyConnected, string macAddress)
        {
            Logger.Log($"PerformToggle: {audioDevice.Name}, KsControls 数量: {audioDevice.KsControls.Count}");
            
            // 关键：重新枚举音频设备，获取最新的 IKsControl 接口
            // 因为之前缓存的 COM 对象可能已经失效
            Logger.Log($"重新枚举音频设备以获取最新的 IKsControl...");
            var freshAudioDevices = await Task.Run(() => _audioService.EnumerateAudioDevices());
            
            // 查找匹配的最新设备（通过名称或 ContainerId）
            BluetoothAudioDeviceInfo? freshDevice = null;
            foreach (var device in freshAudioDevices)
            {
                // [DIAGNOSIS LOG] 记录匹配过程
                // Logger.Log($"[DIAGNOSIS] 比较: '{device.Name}' vs '{audioDevice.Name}' | ID: {device.ContainerId} vs {audioDevice.ContainerId}");
                
                if (device.Name == audioDevice.Name || device.ContainerId == audioDevice.ContainerId)
                {
                    freshDevice = device;
                    Logger.Log($"找到最新的音频设备: '{device.Name}', KsControls: {device.KsControls.Count}");
                    break;
                }
            }
            
            if (freshDevice == null)
            {
                Logger.LogError($"无法找到最新的音频设备: {audioDevice.Name}");
                Logger.Log($"[DIAGNOSIS] 重新枚举未找到设备 {audioDevice.Name}，尝试回退到标准 API。");
                // 即使重新枚举失败，也尝试使用标准 API，因为标准 API 基于 MAC 地址，不依赖 IKsControl 枚举
                return await ToggleWithStandardApi(macAddress, !isCurrentlyConnected);
            }
            
            if (freshDevice.KsControls.Count == 0)
            {
                Logger.LogError($"没有 KsControl 接口: {freshDevice.Name}");
                Logger.Log($"[DIAGNOSIS] 设备 {freshDevice.Name} 存在，但 KsControls 列表为空。尝试回退到标准 API。");
                return await ToggleWithStandardApi(macAddress, !isCurrentlyConnected);
            }
            
            string action = isCurrentlyConnected ? "断开" : "连接";
            Logger.Log($"执行{action}操作...");
            
            bool result = false;
            if (isCurrentlyConnected)
            {
                ConnectionStatusChanged?.Invoke(this, $"正在使用 IKsControl 断开 {freshDevice.Name}...");
                result = await _audioService.DisconnectAsync(freshDevice);
            }
            else
            {
                ConnectionStatusChanged?.Invoke(this, $"正在使用 IKsControl 连接 {freshDevice.Name}...");
                result = await _audioService.ConnectAsync(freshDevice);
            }

            Logger.Log($"{action}结果: {result}");

            // 如果 IKsControl 失败，尝试回退
            if (!result)
            {
                Logger.Log($"IKsControl {action}失败，尝试使用标准 API 回退...");
                return await ToggleWithStandardApi(macAddress, !isCurrentlyConnected);
            }

            return true;
        }

        /// <summary>
        /// 使用标准 Windows Bluetooth API (BluetoothSetServiceState) 进行连接/断开
        /// </summary>
        private async Task<bool> ToggleWithStandardApi(string macAddress, bool connect)
        {
            return await Task.Run(async () =>
            {
                string action = connect ? "连接" : "断开";
                ConnectionStatusChanged?.Invoke(this, $"正在使用标准 API {action} (MAC: {macAddress})...");
                
                try
                {
                    bool success;
                    if (connect)
                    {
                        success = BluetoothServiceHelper.EnableAudioServices(macAddress);
                    }
                    else
                    {
                        // 断开连接：禁用服务
                        success = BluetoothServiceHelper.DisableAudioServices(macAddress);
                        
                        // 如果断开成功，需要重新启用服务以恢复设备在 Windows 中的"音频"状态
                        // 否则设备会变成"其他设备"且无法通过 Windows 设置连接
                        if (success)
                        {
                            Logger.Log("标准 API 断开成功，正在恢复服务状态...");
                            // 等待 2 秒让系统处理断开
                            await Task.Delay(2000);
                            
                            // 重新启用服务
                            // 注意：这通常不会触发自动重连，但会使设备重新出现在音频列表中
                            bool restoreResult = BluetoothServiceHelper.EnableAudioServices(macAddress);
                            Logger.Log($"服务状态恢复{(restoreResult ? "成功" : "失败")}");
                        }
                    }

                    if (success)
                    {
                        ConnectionStatusChanged?.Invoke(this, $"标准 API {action}成功");
                        Logger.Log($"标准 API {action}成功");
                        return true;
                    }
                    else
                    {
                        ConnectionStatusChanged?.Invoke(this, $"标准 API {action}失败");
                        Logger.LogError($"标准 API {action}失败");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"标准 API 操作异常: {ex.Message}", ex);
                    ConnectionStatusChanged?.Invoke(this, $"标准 API 操作出错: {ex.Message}");
                    return false;
                }
            });
        }

        /// <summary>
        /// 获取仅音频设备
        /// </summary>
        public async Task<List<Models.BluetoothDeviceInfo>> GetAudioDevicesAsync()
        {
            var allDevices = await GetPairedDevicesAsync();
            return allDevices.FindAll(d => d.IsAudioDevice);
        }

        /// <summary>
        /// 刷新音频设备缓存
        /// </summary>
        public async Task RefreshAudioDeviceCacheAsync()
        {
            await Task.Run(() =>
            {
                var audioDevices = _audioService.EnumerateAudioDevices();
                _nameToAudioDevice.Clear();
                foreach (var device in audioDevices)
                {
                    _nameToAudioDevice[device.Name] = device;
                }
                System.Diagnostics.Debug.WriteLine($"刷新音频设备缓存: 找到 {audioDevices.Count} 个设备");
            });
        }

        /// <summary>
        /// 直接获取蓝牙音频设备列表
        /// </summary>
        public async Task<List<BluetoothAudioDeviceInfo>> GetBluetoothAudioDevicesAsync()
        {
            return await _audioService.EnumerateAudioDevicesAsync();
        }

        /// <summary>
        /// 通过音频服务直接连接
        /// </summary>
        public async Task<bool> ConnectAudioDeviceAsync(BluetoothAudioDeviceInfo device)
        {
            return await _audioService.ConnectAsync(device);
        }

        /// <summary>
        /// 通过音频服务直接断开
        /// </summary>
        public async Task<bool> DisconnectAudioDeviceAsync(BluetoothAudioDeviceInfo device)
        {
            return await _audioService.DisconnectAsync(device);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_deviceWatcher != null)
                {
                    _deviceWatcher.Stop();
                    _deviceWatcher.Added -= DeviceWatcher_Added;
                    _deviceWatcher.Updated -= DeviceWatcher_Updated;
                    _deviceWatcher.Removed -= DeviceWatcher_Removed;
                    _deviceWatcher = null;
                }
                
                _client?.Close();
                _client = null;
                _audioService.Dispose();
                _nameToAudioDevice.Clear();
                _disposed = true;
            }
        }
    }
}