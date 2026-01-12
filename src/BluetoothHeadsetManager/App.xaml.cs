using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BluetoothHeadsetManager.Models;
using BluetoothHeadsetManager.Services;
using BluetoothHeadsetManager.ViewModels;
using Hardcodet.Wpf.TaskbarNotification;

namespace BluetoothHeadsetManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private TaskbarIcon? _taskbarIcon;
        private TrayViewModel? _viewModel;
        private HotkeyService? _hotkeyService;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _viewModel = new TrayViewModel();

            // 创建托盘图标
            _taskbarIcon = new TaskbarIcon
            {
                Icon = new System.Drawing.Icon("Resources/app.ico"),
                ToolTipText = "蓝牙耳机管理器",
                DataContext = _viewModel
            };

            // 创建右键菜单
            _taskbarIcon.ContextMenu = CreateContextMenu();

            // 监听设备列表变化，更新菜单
            _viewModel.Devices.CollectionChanged += (s, args) =>
            {
                Dispatcher.Invoke(() =>
                {
                    _taskbarIcon.ContextMenu = CreateContextMenu();
                });
            };

            // 初始化热键服务
            InitializeHotkeys();
        }

        private void InitializeHotkeys()
        {
            _hotkeyService = new HotkeyService();
            _hotkeyService.InitializeForWindowless();

            // 注册 Ctrl+Shift+B 热键来连接/断开第一个音频设备
            _hotkeyService.RegisterHotkey(
                HotkeyService.ModifierKeys.Control | HotkeyService.ModifierKeys.Shift,
                VirtualKeys.VK_B,
                () =>
                {
                    Dispatcher.Invoke(async () =>
                    {
                        if (_viewModel != null && _viewModel.Devices.Count > 0)
                        {
                            // 找到第一个音频设备
                            var audioDevice = _viewModel.Devices.FirstOrDefault(d => d.IsAudioDevice);
                            if (audioDevice != null)
                            {
                                await _viewModel.ToggleConnectionCommand.ExecuteAsync(audioDevice);
                            }
                        }
                    });
                });

            // 注册 Ctrl+Shift+R 热键来刷新设备列表
            _hotkeyService.RegisterHotkey(
                HotkeyService.ModifierKeys.Control | HotkeyService.ModifierKeys.Shift,
                VirtualKeys.VK_R,
                () =>
                {
                    Dispatcher.Invoke(async () =>
                    {
                        if (_viewModel != null)
                        {
                            await _viewModel.RefreshDevicesCommand.ExecuteAsync(null);
                        }
                    });
                });
        }

        private ContextMenu CreateContextMenu()
        {
            var contextMenu = new ContextMenu();

            // 添加设备列表
            if (_viewModel?.Devices.Count > 0)
            {
                foreach (var device in _viewModel.Devices)
                {
                    var deviceItem = new MenuItem
                    {
                        Header = device.ToString(),
                        IsCheckable = false,
                        Tag = device
                    };
                    
                    if (device.IsConnected)
                    {
                        deviceItem.FontWeight = FontWeights.Bold;
                        deviceItem.Header = "✓ " + device.ToString();
                    }

                    // 点击设备项时切换连接状态
                    deviceItem.Click += async (s, args) =>
                    {
                        if (s is MenuItem menuItem && menuItem.Tag is BluetoothDeviceInfo dev)
                        {
                            await _viewModel.ToggleConnectionCommand.ExecuteAsync(dev);
                        }
                    };

                    contextMenu.Items.Add(deviceItem);
                }

                contextMenu.Items.Add(new Separator());
            }
            else
            {
                var noDeviceItem = new MenuItem
                {
                    Header = "未发现蓝牙设备",
                    IsEnabled = false
                };
                contextMenu.Items.Add(noDeviceItem);
                contextMenu.Items.Add(new Separator());
            }

            // 刷新按钮
            var refreshItem = new MenuItem { Header = "🔄 刷新设备列表 (Ctrl+Shift+R)" };
            refreshItem.Click += async (s, args) =>
            {
                if (_viewModel != null)
                {
                    await _viewModel.RefreshDevicesCommand.ExecuteAsync(null);
                }
            };
            contextMenu.Items.Add(refreshItem);

            contextMenu.Items.Add(new Separator());

            // 自动切换音频选项
            var autoSwitchItem = new MenuItem
            {
                Header = "🔊 自动切换音频输出",
                IsCheckable = true,
                IsChecked = _viewModel?.AutoSwitchAudio ?? true
            };
            autoSwitchItem.Click += (s, args) =>
            {
                if (_viewModel != null)
                {
                    _viewModel.AutoSwitchAudio = autoSwitchItem.IsChecked;
                }
            };
            contextMenu.Items.Add(autoSwitchItem);

            contextMenu.Items.Add(new Separator());

            // 热键提示
            var hotkeyInfoItem = new MenuItem
            {
                Header = "⌨️ 热键: Ctrl+Shift+B 连接/断开",
                IsEnabled = false
            };
            contextMenu.Items.Add(hotkeyInfoItem);

            contextMenu.Items.Add(new Separator());

            // 退出按钮
            var exitItem = new MenuItem { Header = "❌ 退出" };
            exitItem.Click += (s, args) => Shutdown();
            contextMenu.Items.Add(exitItem);

            return contextMenu;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _hotkeyService?.Dispose();
            _viewModel?.ExitCommand.Execute(null);
            _taskbarIcon?.Dispose();
            base.OnExit(e);
        }
    }
}
