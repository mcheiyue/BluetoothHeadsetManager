using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using BluetoothHeadsetManager.Core;
using BluetoothHeadsetManager.Models;
using BluetoothHeadsetManager.Utils;
using Windows.Devices.Bluetooth;

namespace BluetoothHeadsetManager.UI
{
    /// <summary>
    /// 系统托盘应用程序主类
    /// </summary>
    public class TrayApplication : IDisposable
    {
        private NotifyIcon _notifyIcon = null!;
        private ContextMenuStrip _contextMenu = null!;
        private ToolStripMenuItem _connectMenuItem = null!;
        private ToolStripMenuItem _disconnectMenuItem = null!;
        private ToolStripMenuItem _devicesMenuItem = null!;
        private ToolStripMenuItem _refreshMenuItem = null!;
        
        private readonly DeviceManager _deviceManager;
        private readonly ConnectionManager _connectionManager;
        private readonly BatteryMonitor _batteryMonitor;
        private readonly ConfigManager _configManager;
        
        private bool _disposed = false;
        private bool _isInitialized = false;

        public TrayApplication()
        {
            // 初始化管理器
            _configManager = ConfigManager.Instance;
            _deviceManager = new DeviceManager();
            _connectionManager = new ConnectionManager(_deviceManager);
            _batteryMonitor = new BatteryMonitor();
            
            // 订阅事件
            _connectionManager.StatusChanged += OnConnectionStatusChanged;
            _batteryMonitor.BatteryLevelChanged += OnBatteryLevelChanged;
            _deviceManager.DeviceAdded += OnDeviceAdded;
            _deviceManager.DeviceRemoved += OnDeviceRemoved;
            
            InitializeComponents();
        }

        /// <summary>
        /// 初始化UI组件
        /// </summary>
        private void InitializeComponents()
        {
            try
            {
                // 创建上下文菜单
                _contextMenu = new ContextMenuStrip();
                
                // 连接菜单项
                _connectMenuItem = new ToolStripMenuItem("连接设备(&C)");
                _connectMenuItem.Click += OnConnectClick;
                _contextMenu.Items.Add(_connectMenuItem);

                // 断开连接菜单项
                _disconnectMenuItem = new ToolStripMenuItem("断开连接(&D)");
                _disconnectMenuItem.Click += OnDisconnectClick;
                _disconnectMenuItem.Enabled = false;
                _contextMenu.Items.Add(_disconnectMenuItem);

                _contextMenu.Items.Add(new ToolStripSeparator());

                // 设备列表子菜单
                _devicesMenuItem = new ToolStripMenuItem("选择设备(&V)");
                _contextMenu.Items.Add(_devicesMenuItem);

                // 刷新设备列表
                _refreshMenuItem = new ToolStripMenuItem("刷新设备列表(&R)");
                _refreshMenuItem.Click += OnRefreshClick;
                _contextMenu.Items.Add(_refreshMenuItem);

                _contextMenu.Items.Add(new ToolStripSeparator());

                // 设置菜单项
                var settingsMenuItem = new ToolStripMenuItem("设置(&S)");
                settingsMenuItem.Click += OnSettingsClick;
                _contextMenu.Items.Add(settingsMenuItem);

                // 关于菜单项
                var aboutMenuItem = new ToolStripMenuItem("关于(&A)");
                aboutMenuItem.Click += OnAboutClick;
                _contextMenu.Items.Add(aboutMenuItem);

                _contextMenu.Items.Add(new ToolStripSeparator());

                // 退出菜单项
                var exitMenuItem = new ToolStripMenuItem("退出(&X)");
                exitMenuItem.Click += OnExitClick;
                _contextMenu.Items.Add(exitMenuItem);

                // 创建托盘图标
                _notifyIcon = new NotifyIcon
                {
                    Icon = SystemIcons.Application,
                    Text = "蓝牙耳机管理器 - 未连接",
                    ContextMenuStrip = _contextMenu,
                    Visible = false
                };

                // 绑定双击事件 - 快速连接/断开
                _notifyIcon.DoubleClick += OnTrayIconDoubleClick;
                
                // 菜单打开前更新设备列表
                _contextMenu.Opening += OnContextMenuOpening;

                Logger.Info("托盘应用UI初始化完成");
            }
            catch (Exception ex)
            {
                Logger.Error("托盘应用UI初始化失败", ex);
                throw;
            }
        }

        /// <summary>
        /// 启动应用
        /// </summary>
        public async void Start()
        {
            try
            {
                Logger.Info("启动托盘应用...");
                
                // 显示托盘图标
                _notifyIcon.Visible = true;
                
                // 初始化设备管理器
                bool initialized = await _deviceManager.InitializeAsync();
                if (!initialized)
                {
                    ShowNotification("初始化失败", "无法初始化蓝牙适配器，请检查蓝牙是否已启用", ToolTipIcon.Error);
                    Logger.Error("设备管理器初始化失败");
                    return;
                }
                
                _isInitialized = true;
                
                // 应用配置
                ApplyConfiguration();
                
                // 启动设备扫描
                _deviceManager.StartScan();
                
                // 显示启动通知
                ShowNotification("蓝牙耳机管理器", "应用已启动，双击图标快速连接", ToolTipIcon.Info);
                
                // 如果配置了自动连接，尝试连接到上次设备
                if (!string.IsNullOrEmpty(_configManager.Config.LastConnectedDeviceId))
                {
                    Logger.Info($"尝试自动连接到上次设备: {_configManager.Config.LastConnectedDeviceId}");
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(2000); // 等待设备扫描
                        await TryConnectToLastDeviceAsync();
                    });
                }
                
                Logger.Info("托盘应用已启动");
            }
            catch (Exception ex)
            {
                Logger.Error("启动托盘应用失败", ex);
                ShowNotification("启动失败", $"应用启动失败: {ex.Message}", ToolTipIcon.Error);
            }
        }

        /// <summary>
        /// 停止应用
        /// </summary>
        public void Stop()
        {
            try
            {
                Logger.Info("停止托盘应用...");
                
                // 停止电量监控
                _batteryMonitor.StopMonitoring();
                
                // 断开设备连接
                _ = _connectionManager.DisconnectAsync();
                
                // 停止设备扫描
                _deviceManager.StopScan();
                
                _notifyIcon.Visible = false;
                Logger.Info("托盘应用已停止");
            }
            catch (Exception ex)
            {
                Logger.Error("停止托盘应用失败", ex);
            }
        }

        /// <summary>
        /// 应用配置
        /// </summary>
        private void ApplyConfiguration()
        {
            try
            {
                var config = _configManager.Config;
                
                // 应用自动重连设置
                _connectionManager.AutoReconnectEnabled = config.EnableAutoReconnect;
                
                // 应用电量监控设置
                _batteryMonitor.MonitorInterval = TimeSpan.FromSeconds(config.BatteryCheckInterval);
                _batteryMonitor.LowBatteryThreshold = config.LowBatteryThreshold;
                
                Logger.Info("配置已应用");
            }
            catch (Exception ex)
            {
                Logger.Error("应用配置失败", ex);
            }
        }

        /// <summary>
        /// 尝试连接到上次连接的设备
        /// </summary>
        private async Task TryConnectToLastDeviceAsync()
        {
            try
            {
                var lastDeviceId = _configManager.Config.LastConnectedDeviceId;
                if (string.IsNullOrEmpty(lastDeviceId))
                    return;
                
                var device = await _deviceManager.FindDeviceAsync(lastDeviceId);
                if (device != null && !device.IsConnected)
                {
                    Logger.Info($"找到上次设备，尝试自动连接: {device.Name}");
                    await ConnectToDeviceAsync(lastDeviceId);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("自动连接到上次设备失败", ex);
            }
        }

        /// <summary>
        /// 显示气泡通知
        /// </summary>
        public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
        {
            try
            {
                if (_configManager.Config.EnableNotifications)
                {
                    _notifyIcon.ShowBalloonTip(3000, title, message, icon);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"显示通知失败: {title} - {message}", ex);
            }
        }

        /// <summary>
        /// 更新托盘图标文本
        /// </summary>
        private void UpdateTrayText(string text)
        {
            try
            {
                // NotifyIcon.Text 最大长度为63个字符
                _notifyIcon.Text = text.Length > 63 ? text.Substring(0, 63) : text;
            }
            catch (Exception ex)
            {
                Logger.Error("更新托盘文本失败", ex);
            }
        }

        /// <summary>
        /// 更新菜单状态
        /// </summary>
        private void UpdateMenuState()
        {
            try
            {
                bool isConnected = _connectionManager.IsConnected();
                _connectMenuItem.Enabled = !isConnected;
                _disconnectMenuItem.Enabled = isConnected;
            }
            catch (Exception ex)
            {
                Logger.Error("更新菜单状态失败", ex);
            }
        }

        /// <summary>
        /// 连接到指定设备
        /// </summary>
        private async Task ConnectToDeviceAsync(string deviceId)
        {
            try
            {
                Logger.Info($"连接到设备: {deviceId}");
                
                bool success = await _connectionManager.ConnectAsync(deviceId);
                
                if (success)
                {
                    // 保存最后连接的设备
                    _configManager.UpdateLastConnectedDevice(deviceId);
                    
                    // 启动电量监控
                    if (_connectionManager.CurrentDevice != null)
                    {
                        _batteryMonitor.StartMonitoring(_connectionManager.CurrentDevice);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"连接设备失败: {deviceId}", ex);
                ShowNotification("连接失败", $"连接设备失败: {ex.Message}", ToolTipIcon.Error);
            }
        }

        #region 事件处理

        private void OnContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // 更新菜单状态
            UpdateMenuState();
            
            // 更新设备列表
            UpdateDeviceList();
        }

        private async void OnConnectClick(object? sender, EventArgs e)
        {
            try
            {
                Logger.Info("用户点击了连接设备菜单");
                
                if (!_isInitialized)
                {
                    ShowNotification("未就绪", "应用尚未初始化完成", ToolTipIcon.Warning);
                    return;
                }
                
                // 获取收藏设备或上次连接的设备
                var lastDeviceId = _configManager.Config.LastConnectedDeviceId;
                if (!string.IsNullOrEmpty(lastDeviceId))
                {
                    await ConnectToDeviceAsync(lastDeviceId);
                }
                else
                {
                    ShowNotification("提示", "请在设备列表中选择要连接的设备", ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("连接设备失败", ex);
                ShowNotification("错误", $"连接设备失败: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private async void OnDisconnectClick(object? sender, EventArgs e)
        {
            try
            {
                Logger.Info("用户点击了断开连接菜单");
                
                await _connectionManager.DisconnectAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("断开连接失败", ex);
                ShowNotification("错误", $"断开连接失败: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private async void OnRefreshClick(object? sender, EventArgs e)
        {
            try
            {
                Logger.Info("用户点击了刷新设备列表");
                
                ShowNotification("刷新中", "正在刷新设备列表...", ToolTipIcon.Info);
                
                // 重新扫描设备
                _deviceManager.StopScan();
                await Task.Delay(500);
                _deviceManager.StartScan();
                
                await Task.Delay(1000); // 等待扫描
                UpdateDeviceList();
            }
            catch (Exception ex)
            {
                Logger.Error("刷新设备列表失败", ex);
            }
        }

        private void UpdateDeviceList()
        {
            try
            {
                _devicesMenuItem.DropDownItems.Clear();
                
                var devices = _deviceManager.GetCachedDevices();
                if (devices.Count == 0)
                {
                    var noDeviceItem = new ToolStripMenuItem("(未发现设备)")
                    {
                        Enabled = false
                    };
                    _devicesMenuItem.DropDownItems.Add(noDeviceItem);
                }
                else
                {
                    foreach (var device in devices.OrderBy(d => d.Name))
                    {
                        var deviceItem = new ToolStripMenuItem(
                            $"{device.Name} ({(device.IsConnected ? "已连接" : device.IsPaired ? "已配对" : "未配对")})"
                        );
                        
                        deviceItem.Tag = device.Id;
                        deviceItem.Click += async (s, e) =>
                        {
                            var deviceId = (s as ToolStripMenuItem)?.Tag as string;
                            if (!string.IsNullOrEmpty(deviceId))
                            {
                                if (_connectionManager.IsConnected(deviceId))
                                {
                                    await _connectionManager.DisconnectAsync();
                                }
                                else
                                {
                                    await ConnectToDeviceAsync(deviceId);
                                }
                            }
                        };
                        
                        _devicesMenuItem.DropDownItems.Add(deviceItem);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("更新设备列表失败", ex);
            }
        }

        private void OnSettingsClick(object? sender, EventArgs e)
        {
            try
            {
                Logger.Info("用户点击了设置菜单");
                
                var message = $"配置文件位置:\n{_configManager.GetConfigFilePath()}\n\n" +
                             $"自动重连: {(_configManager.Config.EnableAutoReconnect ? "启用" : "禁用")}\n" +
                             $"电量检查间隔: {_configManager.Config.BatteryCheckInterval}秒\n" +
                             $"低电量阈值: {_configManager.Config.LowBatteryThreshold}%\n" +
                             $"通知: {(_configManager.Config.EnableNotifications ? "启用" : "禁用")}";
                
                MessageBox.Show(message, "当前设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger.Error("显示设置失败", ex);
            }
        }

        private void OnAboutClick(object? sender, EventArgs e)
        {
            try
            {
                var message = "蓝牙耳机管理器 v1.0\n\n" +
                             "快速连接和管理蓝牙耳机\n" +
                             "实时显示设备电量\n\n" +
                             "使用 .NET 7 + WinForms 开发";
                
                MessageBox.Show(message, "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger.Error("显示关于对话框失败", ex);
            }
        }

        private void OnExitClick(object? sender, EventArgs e)
        {
            try
            {
                Logger.Info("用户点击了退出菜单");
                
                var result = MessageBox.Show(
                    "确定要退出蓝牙耳机管理器吗？",
                    "确认退出",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (result == DialogResult.Yes)
                {
                    Stop();
                    Application.Exit();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("退出应用失败", ex);
            }
        }

        private async void OnTrayIconDoubleClick(object? sender, EventArgs e)
        {
            try
            {
                Logger.Info("用户双击了托盘图标");
                
                if (!_isInitialized)
                    return;
                
                // 切换连接状态
                if (_connectionManager.IsConnected())
                {
                    await _connectionManager.DisconnectAsync();
                }
                else
                {
                    // 连接到上次设备或收藏设备
                    var lastDeviceId = _configManager.Config.LastConnectedDeviceId;
                    if (!string.IsNullOrEmpty(lastDeviceId))
                    {
                        await ConnectToDeviceAsync(lastDeviceId);
                    }
                    else
                    {
                        ShowNotification("提示", "请在右键菜单中选择要连接的设备", ToolTipIcon.Info);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("处理双击事件失败", ex);
            }
        }

        private void OnConnectionStatusChanged(object? sender, ConnectionStatusChangedEventArgs e)
        {
            try
            {
                // 在UI线程上更新
                _notifyIcon.ContextMenuStrip?.Invoke((MethodInvoker)delegate
                {
                    UpdateMenuState();
                    
                    if (e.Status == BluetoothConnectionStatus.Connected)
                    {
                        UpdateTrayText($"蓝牙耳机管理器 - 已连接: {e.DeviceName}");
                        ShowNotification("已连接", $"成功连接到 {e.DeviceName}", ToolTipIcon.Info);
                    }
                    else
                    {
                        UpdateTrayText("蓝牙耳机管理器 - 未连接");
                        ShowNotification("已断开", $"已断开与 {e.DeviceName} 的连接", ToolTipIcon.Warning);
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Error("处理连接状态变化失败", ex);
            }
        }

        private void OnBatteryLevelChanged(object? sender, BatteryLevelChangedEventArgs e)
        {
            try
            {
                // 在UI线程上更新
                _notifyIcon.ContextMenuStrip?.Invoke((MethodInvoker)delegate
                {
                    var statusText = $"蓝牙耳机管理器 - {e.DeviceName} 电量: {e.BatteryLevel}%";
                    UpdateTrayText(statusText);
                    
                    // 低电量提醒
                    if (e.IsLowBattery)
                    {
                        ShowNotification("低电量提醒",
                            $"{e.DeviceName} 电量仅剩 {e.BatteryLevel}%，请及时充电",
                            ToolTipIcon.Warning);
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Error("处理电量变化失败", ex);
            }
        }

        private void OnDeviceAdded(object? sender, DeviceInfo device)
        {
            Logger.Debug($"设备已添加: {device.Name}");
        }

        private void OnDeviceRemoved(object? sender, string deviceId)
        {
            Logger.Debug($"设备已移除: {deviceId}");
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
                        Logger.Info("释放托盘应用资源...");
                        
                        // 取消事件订阅
                        _connectionManager.StatusChanged -= OnConnectionStatusChanged;
                        _batteryMonitor.BatteryLevelChanged -= OnBatteryLevelChanged;
                        _deviceManager.DeviceAdded -= OnDeviceAdded;
                        _deviceManager.DeviceRemoved -= OnDeviceRemoved;
                        
                        // 释放管理器
                        _batteryMonitor?.Dispose();
                        _connectionManager?.Dispose();
                        _deviceManager?.Dispose();
                        
                        // 释放托盘图标
                        if(_notifyIcon != null)
                        {
                            _notifyIcon.Visible = false;
                            _notifyIcon.Dispose();
                            _notifyIcon = null!;
                        }

                        // 释放上下文菜单
                        if (_contextMenu != null)
                        {
                            _contextMenu.Dispose();
                            _contextMenu = null!;
                        }

                        Logger.Info("托盘应用资源已释放");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("释放托盘应用资源失败", ex);
                    }
                }

                _disposed = true;
            }
        }

        ~TrayApplication()
        {
            Dispose(false);
        }

        #endregion
    }
}