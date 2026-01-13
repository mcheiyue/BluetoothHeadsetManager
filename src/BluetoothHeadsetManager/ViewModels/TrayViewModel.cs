using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using BluetoothHeadsetManager.Models;
using BluetoothHeadsetManager.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BluetoothHeadsetManager.ViewModels
{
    public partial class TrayViewModel : ObservableObject
    {
        private readonly BluetoothService _bluetoothService;
        private readonly BatteryService _batteryService;
        private readonly AudioSwitchService _audioSwitchService;

        [ObservableProperty]
        private ObservableCollection<BluetoothDeviceInfo> _devices = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _autoSwitchAudio = true;

        /// <summary>
        /// 已连接的设备数量
        /// </summary>
        public int ConnectedDevicesCount => Devices.Count(d => d.IsConnected);

        /// <summary>
        /// 托盘图标提示文本
        /// </summary>
        public string TrayToolTip
        {
            get
            {
                var connected = Devices.Where(d => d.IsConnected).ToList();
                if (connected.Count == 0)
                    return "蓝牙耳机管理器 - 无设备连接";
                else if (connected.Count == 1)
                    return $"已连接: {connected[0].DisplayName}";
                else
                    return $"已连接 {connected.Count} 个设备";
            }
        }

        // 用于防抖的计时器
        private System.Threading.Timer? _debounceTimer;
        
        // 记录上次已连接的设备MAC地址，用于判断新增连接
        private HashSet<string> _previousConnectedMacs = new();
        private bool _isFirstLoad = true;

        public TrayViewModel()
        {
            _bluetoothService = new BluetoothService();
            _batteryService = new BatteryService();
            _audioSwitchService = new AudioSwitchService();
            
            _bluetoothService.ConnectionStatusChanged += (s, msg) =>
            {
                StatusMessage = msg;
            };

            // 订阅设备列表变更事件
            _bluetoothService.DeviceListChanged += (s, e) =>
            {
                // 使用防抖机制，避免短时间内多次刷新
                _debounceTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                _debounceTimer = new System.Threading.Timer(_ =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        System.Diagnostics.Debug.WriteLine("检测到设备状态变化，自动刷新列表...");
                        _ = RefreshDevicesAsync();
                    });
                }, null, 1500, System.Threading.Timeout.Infinite); // 延迟 1.5 秒刷新，给系统一点反应时间
            };
            
            // 初始化时加载设备
            _ = RefreshDevicesAsync();
        }

        [RelayCommand]
        private async Task RefreshDevicesAsync()
        {
            IsLoading = true;
            StatusMessage = "正在扫描设备...";
            
            try
            {
                // GetPairedDevicesAsync 内部会自动枚举音频设备，不需要单独调用 RefreshAudioDeviceCacheAsync
                var devices = await _bluetoothService.GetPairedDevicesAsync();
                
                // 获取每个设备的电量
                foreach (var device in devices)
                {
                    int batteryLevel = await _batteryService.GetBatteryLevelAsync(device.MacAddress);
                    device.BatteryLevel = batteryLevel;
                }
                
                // 处理自动连接音频逻辑
                if (!_isFirstLoad && AutoSwitchAudio)
                {
                    var currentConnected = devices
                        .Where(d => d.IsConnected && d.IsAudioDevice)
                        .ToList();
                        
                    foreach (var device in currentConnected)
                    {
                        // 如果是新连接的设备（不在上次列表中）
                        if (!_previousConnectedMacs.Contains(device.MacAddress))
                        {
                            System.Diagnostics.Debug.WriteLine($"检测到新连接的音频设备: {device.Name}，尝试自动切换...");
                            StatusMessage = $"检测到 {device.Name} 已连接，正在切换音频...";
                            
                            // 异步执行切换，不阻塞UI
                            _ = Task.Run(async () =>
                            {
                                // 等待系统音频端点就绪
                                await Task.Delay(1000);
                                bool switched = _audioSwitchService.SwitchToBluetoothDevice(device.Name);
                                
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    if (switched)
                                        StatusMessage = $"已自动切换音频到 {device.Name}";
                                    else
                                        StatusMessage = $"未能自动切换音频到 {device.Name}";
                                });
                            });
                        }
                    }
                }
                
                // 更新上次连接列表
                _previousConnectedMacs.Clear();
                foreach (var device in devices.Where(d => d.IsConnected))
                {
                    _previousConnectedMacs.Add(device.MacAddress);
                }
                _isFirstLoad = false;
                
                // 按连接状态和设备类型排序
                var sortedDevices = devices
                    .OrderByDescending(d => d.IsConnected)
                    .ThenByDescending(d => d.IsAudioDevice)
                    .ThenByDescending(d => d.SupportsKsControl)
                    .ThenBy(d => d.Name)
                    .ToList();
                
                Devices.Clear();
                foreach (var device in sortedDevices)
                {
                    Devices.Add(device);
                }
                
                var connectedCount = devices.Count(d => d.IsConnected);
                var ksControlCount = devices.Count(d => d.SupportsKsControl);
                
                StatusMessage = $"找到 {devices.Count} 个设备 ({connectedCount} 已连接, {ksControlCount} 支持快速连接)";
                
                // 通知属性变化
                OnPropertyChanged(nameof(ConnectedDevicesCount));
                OnPropertyChanged(nameof(TrayToolTip));
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"刷新失败: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"刷新设备列表失败: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ToggleConnectionAsync(BluetoothDeviceInfo? device)
        {
            if (device == null) return;

            IsLoading = true;
            bool wasConnected = device.IsConnected;
            
            var action = wasConnected ? "断开" : "连接";
            var method = device.SupportsKsControl ? "IKsControl" : "BluetoothSetServiceState";
            StatusMessage = $"正在{action} {device.Name} (使用 {method})...";
            
            try
            {
                // 先记录这是一个手动操作，RefreshDevicesAsync 里面就不会重复触发自动切换（因为我们会手动更新 _previousConnectedMacs）
                // 但实际上 RefreshDevicesAsync 会在操作后调用，所以我们需要一种机制来避免重复
                // 简单的做法是：手动操作成功后，我们立即添加到 _previousConnectedMacs
                
                bool success = await _bluetoothService.ToggleConnectionAsync(device);
                
                // 如果连接成功且启用了自动切换音频
                if (success && !wasConnected && AutoSwitchAudio && device.IsAudioDevice)
                {
                    // 将其添加到已连接列表，防止 RefreshDevicesAsync 再次触发通知
                    _previousConnectedMacs.Add(device.MacAddress);
                    
                    StatusMessage = $"已连接 {device.Name}，正在切换音频...";
                    
                    // 等待一小段时间让系统识别音频设备
                    await Task.Delay(1500);
                    
                    // 尝试切换音频输出
                    bool audioSwitched = _audioSwitchService.SwitchToBluetoothDevice(device.Name);
                    if (audioSwitched)
                    {
                        StatusMessage = $"已连接并切换音频到 {device.Name}";
                    }
                    else
                    {
                        StatusMessage = $"已连接 {device.Name}，但音频切换失败";
                    }
                }
                else if (success && wasConnected)
                {
                    StatusMessage = $"已断开 {device.Name}";
                    _previousConnectedMacs.Remove(device.MacAddress);
                }
                else if (!success)
                {
                    StatusMessage = $"{action} {device.Name} 失败";
                }
                
                // 刷新设备列表以更新连接状态
                await RefreshDevicesAsync();
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"{action}失败: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"切换连接状态失败: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 快速连接第一个可用的音频设备
        /// </summary>
        [RelayCommand]
        private async Task QuickConnectAsync()
        {
            var audioDevices = Devices
                .Where(d => d.IsAudioDevice && !d.IsConnected && d.SupportsKsControl)
                .ToList();

            if (audioDevices.Count == 0)
            {
                // 尝试找任何未连接的音频设备
                audioDevices = Devices
                    .Where(d => d.IsAudioDevice && !d.IsConnected)
                    .ToList();
            }

            if (audioDevices.Count == 0)
            {
                StatusMessage = "没有可连接的音频设备";
                return;
            }

            // 连接第一个可用的设备
            await ToggleConnectionAsync(audioDevices.First());
        }

        /// <summary>
        /// 断开所有已连接的设备
        /// </summary>
        [RelayCommand]
        private async Task DisconnectAllAsync()
        {
            var connectedDevices = Devices.Where(d => d.IsConnected).ToList();
            
            if (connectedDevices.Count == 0)
            {
                StatusMessage = "没有已连接的设备";
                return;
            }

            IsLoading = true;
            StatusMessage = $"正在断开 {connectedDevices.Count} 个设备...";

            int successCount = 0;
            foreach (var device in connectedDevices)
            {
                try
                {
                    bool success = await _bluetoothService.DisconnectAsync(device.MacAddress);
                    if (success) successCount++;
                }
                catch
                {
                    // 继续处理其他设备
                }
            }

            StatusMessage = $"已断开 {successCount}/{connectedDevices.Count} 个设备";
            await RefreshDevicesAsync();
            IsLoading = false;
        }

        [RelayCommand]
        private void Exit()
        {
            _debounceTimer?.Dispose();
            _bluetoothService.Dispose();
            _batteryService.Dispose();
            _audioSwitchService.Dispose();
            Application.Current.Shutdown();
        }
    }
}