using System;

namespace BluetoothHeadsetManager.Models
{
    /// <summary>
    /// 蓝牙设备信息模型
    /// </summary>
    public class DeviceInfo
    {
        /// <summary>
        /// 设备ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 设备名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 设备地址
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// 是否已配对
        /// </summary>
        public bool IsPaired { get; set; }

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// 电量百分比（0-100，-1表示不支持）
        /// </summary>
        public int BatteryLevel { get; set; } = -1;

        /// <summary>
        /// 最后连接时间
        /// </summary>
        public DateTime? LastConnectedTime { get; set; }

        /// <summary>
        /// 设备类型
        /// </summary>
        public string DeviceType { get; set; }

        public override string ToString()
        {
            return $"{Name} ({Address}) - {(IsConnected ? "已连接" : "未连接")}";
        }
    }
}