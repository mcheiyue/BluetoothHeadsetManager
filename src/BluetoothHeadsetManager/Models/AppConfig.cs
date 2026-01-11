using System;
using System.Collections.Generic;

namespace BluetoothHeadsetManager.Models
{
    /// <summary>
    /// 应用程序配置模型
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// 常用设备列表
        /// </summary>
        public List<DeviceInfo> FavoriteDevices { get; set; } = new List<DeviceInfo>();

        /// <summary>
        /// 最后连接的设备ID
        /// </summary>
        public string LastConnectedDeviceId { get; set; } = string.Empty;

        /// <summary>
        /// 是否开机自动启动
        /// </summary>
        public bool AutoStart { get; set; } = false;

        /// <summary>
        /// 是否启用自动重连
        /// </summary>
        public bool EnableAutoReconnect { get; set; } = true;

        /// <summary>
        /// 自动重连最大尝试次数
        /// </summary>
        public int MaxReconnectAttempts { get; set; } = 3;

        /// <summary>
        /// 电量查询间隔（秒）
        /// </summary>
        public int BatteryCheckInterval { get; set; } = 60;

        /// <summary>
        /// 低电量阈值（百分比）
        /// </summary>
        public int LowBatteryThreshold { get; set; } = 20;

        /// <summary>
        /// 是否启用通知
        /// </summary>
        public bool EnableNotifications { get; set; } = true;

        /// <summary>
        /// 是否启用调试日志
        /// </summary>
        public bool EnableDebugLog { get; set; } = false;

        /// <summary>
        /// 配置文件版本
        /// </summary>
        public string ConfigVersion { get; set; } = "1.0.0";
    }
}