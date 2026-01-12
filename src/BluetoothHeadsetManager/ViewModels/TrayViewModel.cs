using System.Collections.ObjectModel;
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

        public TrayViewModel()
        {
            _bluetoothService = new BluetoothService();
            _batteryService = new BatteryService();
            _audioSwitchService = new AudioSwitchService();
            _bluetoothService.ConnectionStatusChanged += (s, msg) =>
            {
                StatusMessage = msg;
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
                var devices = await _bluetoothService.GetPairedDevicesAsync();
                
                // 获取每个设备的电量
                foreach (var device in devices)
                {
                    int batteryLevel = await _batteryService.GetBatteryLevelAsync(device.MacAddress);
                    device.BatteryLevel = batteryLevel;
                }
                
                Devices.Clear();
                foreach (var device in devices)
                {
                    Devices.Add(device);
                }
                StatusMessage = $"找到 {devices.Count} 个设备";
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
            StatusMessage = wasConnected ? $"正在断开 {device.Name}..." : $"正在连接 {device.Name}...";
            
            try
            {
                bool success = await _bluetoothService.ToggleConnectionAsync(device);
                
                // 如果连接成功且启用了自动切换音频
                if (success && !wasConnected && AutoSwitchAudio && device.IsAudioDevice)
                {
                    // 等待一小段时间让系统识别音频设备
                    await Task.Delay(1000);
                    
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
                
                // 刷新设备列表以更新连接状态
                await RefreshDevicesAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void Exit()
        {
            _bluetoothService.Dispose();
            _batteryService.Dispose();
            _audioSwitchService.Dispose();
            Application.Current.Shutdown();
        }
    }
}