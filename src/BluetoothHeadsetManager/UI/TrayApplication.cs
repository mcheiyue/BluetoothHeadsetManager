using System;
using System.Drawing;
using System.Windows.Forms;
using BluetoothHeadsetManager.Utils;

namespace BluetoothHeadsetManager.UI
{
    /// <summary>
    /// 系统托盘应用程序主类
    /// </summary>
    public class TrayApplication : IDisposable
    {
        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _contextMenu;
        private bool _disposed = false;

        public TrayApplication()
        {
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
                
                // 添加菜单项
                var connectMenuItem = new ToolStripMenuItem("连接耳机(&C)");
                connectMenuItem.Click += OnConnectClick;
                _contextMenu.Items.Add(connectMenuItem);

                var disconnectMenuItem = new ToolStripMenuItem("断开连接(&D)");
                disconnectMenuItem.Click += OnDisconnectClick;
                _contextMenu.Items.Add(disconnectMenuItem);

                _contextMenu.Items.Add(new ToolStripSeparator());

                var settingsMenuItem = new ToolStripMenuItem("设置(&S)");
                settingsMenuItem.Click += OnSettingsClick;
                _contextMenu.Items.Add(settingsMenuItem);

                _contextMenu.Items.Add(new ToolStripSeparator());

                var exitMenuItem = new ToolStripMenuItem("退出(&X)");
                exitMenuItem.Click += OnExitClick;
                _contextMenu.Items.Add(exitMenuItem);

                // 创建托盘图标
                _notifyIcon = new NotifyIcon
                {
                    Icon = SystemIcons.Application, // 临时使用系统图标
                    Text = "蓝牙耳机管理器",
                    ContextMenuStrip = _contextMenu,
                    Visible = false
                };

                // 绑定双击事件
                _notifyIcon.DoubleClick += OnTrayIconDoubleClick;

                Logger.Info("托盘应用初始化完成");
            }
            catch (Exception ex)
            {
                Logger.Error("托盘应用初始化失败", ex);
                throw;
            }
        }

        /// <summary>
        /// 启动应用
        /// </summary>
        public void Start()
        {
            try
            {
                _notifyIcon.Visible = true;
                Logger.Info("托盘应用已启动");
                
                // 显示启动通知
                ShowNotification("蓝牙耳机管理器", "应用已启动，运行在系统托盘中", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                Logger.Error("启动托盘应用失败", ex);
                throw;
            }
        }

        /// <summary>
        /// 停止应用
        /// </summary>
        public void Stop()
        {
            try
            {
                _notifyIcon.Visible = false;
                Logger.Info("托盘应用已停止");
            }
            catch (Exception ex)
            {
                Logger.Error("停止托盘应用失败", ex);
            }
        }

        /// <summary>
        /// 显示气泡通知
        /// </summary>
        public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
        {
            try
            {
                _notifyIcon.ShowBalloonTip(3000, title, message, icon);
            }
            catch (Exception ex)
            {
                Logger.Error($"显示通知失败: {title} - {message}", ex);
            }
        }

        /// <summary>
        /// 更新托盘图标文本
        /// </summary>
        public void UpdateTrayText(string text)
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

        #region 事件处理

        private void OnConnectClick(object sender, EventArgs e)
        {
            try
            {
                Logger.Info("用户点击了连接耳机菜单");
                ShowNotification("连接耳机", "连接功能开发中...", ToolTipIcon.Info);
                // TODO: 实现连接逻辑
            }
            catch (Exception ex)
            {
                Logger.Error("连接耳机失败", ex);
                ShowNotification("错误", "连接耳机失败", ToolTipIcon.Error);
            }
        }

        private void OnDisconnectClick(object sender, EventArgs e)
        {
            try
            {
                Logger.Info("用户点击了断开连接菜单");
                ShowNotification("断开连接", "断开功能开发中...", ToolTipIcon.Info);
                // TODO: 实现断开逻辑
            }
            catch (Exception ex)
            {
                Logger.Error("断开连接失败", ex);
                ShowNotification("错误", "断开连接失败", ToolTipIcon.Error);
            }
        }

        private void OnSettingsClick(object sender, EventArgs e)
        {
            try
            {
                Logger.Info("用户点击了设置菜单");
                ShowNotification("设置", "设置功能开发中...", ToolTipIcon.Info);
                // TODO: 打开设置窗口
            }
            catch (Exception ex)
            {
                Logger.Error("打开设置失败", ex);
            }
        }

        private void OnExitClick(object sender, EventArgs e)
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

        private void OnTrayIconDoubleClick(object sender, EventArgs e)
        {
            try
            {
                Logger.Info("用户双击了托盘图标");
                ShowNotification("蓝牙耳机管理器", "快捷操作开发中...", ToolTipIcon.Info);
                // TODO: 实现快速连接/断开
            }
            catch (Exception ex)
            {
                Logger.Error("处理双击事件失败", ex);
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
                        // 释放托盘图标
                        if (_notifyIcon != null)
                        {
                            _notifyIcon.Visible = false;
                            _notifyIcon.Dispose();
                            _notifyIcon = null;
                        }

                        // 释放上下文菜单
                        if (_contextMenu != null)
                        {
                            _contextMenu.Dispose();
                            _contextMenu = null;
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