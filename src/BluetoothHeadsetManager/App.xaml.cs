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
                Icon = new System.Drawing.Icon(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Resources/app.ico")),
                ToolTipText = "蓝牙耳机管理器",
                DataContext = _viewModel
            };

            // 创建右键菜单
            _taskbarIcon.ContextMenu = CreateContextMenu();

            // 订阅设备列表变化事件，刷新菜单
            _viewModel.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(TrayViewModel.Devices))
                {
                    Dispatcher.Invoke(() => _taskbarIcon.ContextMenu = CreateContextMenu());
                }
            };

            // 初始化热键服务
            InitializeHotkeys();
        }

        private ContextMenu CreateContextMenu()
        {
            var menu = new ContextMenu();

            // 刷新按钮
            var refreshItem = new MenuItem { Header = "🔄 刷新设备列表" };
            refreshItem.Click += async (s, args) =>
            {
                if (_viewModel != null)
                {
                    await _viewModel.RefreshDevicesCommand.ExecuteAsync(null);
                    _taskbarIcon!.ContextMenu = CreateContextMenu();
                }
            };
            menu.Items.Add(refreshItem);
            menu.Items.Add(new Separator());

            // 设备列表
            if (_viewModel != null && _viewModel.Devices.Count > 0)
            {
                foreach (var device in _viewModel.Devices)
                {
                    var deviceItem = new MenuItem
                    {
                        Header = FormatDeviceHeader(device)
                    };
                    deviceItem.Click += async (s, args) =>
                    {
                        await _viewModel.ToggleConnectionCommand.ExecuteAsync(device);
                        _taskbarIcon!.ContextMenu = CreateContextMenu();
                    };
                    menu.Items.Add(deviceItem);
                }
            }
            else
            {
                var noDeviceItem = new MenuItem { Header = "没有找到蓝牙设备", IsEnabled = false };
                menu.Items.Add(noDeviceItem);
            }

            menu.Items.Add(new Separator());

            // 退出按钮
            var exitItem = new MenuItem { Header = "退出" };
            exitItem.Click += (s, args) => Shutdown();
            menu.Items.Add(exitItem);

            return menu;
        }

        private string FormatDeviceHeader(BluetoothDeviceInfo device)
        {
            var status = device.IsConnected ? "🟢" : "⚪";
            var battery = device.BatteryLevel > 0 ? $" 🔋{device.BatteryLevel}%" : "";
            var audioTag = device.IsAudioDevice ? " 🎧" : "";
            return $"{status} {device.Name}{battery}{audioTag}";
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


        protected override void OnExit(ExitEventArgs e)
        {
            _hotkeyService?.Dispose();
            _viewModel?.ExitCommand.Execute(null);
            _taskbarIcon?.Dispose();
            base.OnExit(e);
        }
    }
}
