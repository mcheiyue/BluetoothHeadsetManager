namespace BluetoothHeadsetManager.Models
{
    /// <summary>
    /// 蓝牙设备信息
    /// </summary>
    public class BluetoothDeviceInfo
    {
        /// <summary>
        /// 设备名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// MAC地址
        /// </summary>
        public string MacAddress { get; set; } = string.Empty;

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// 是否已配对
        /// </summary>
        public bool IsPaired { get; set; }

        /// <summary>
        /// 设备类型
        /// </summary>
        public uint ClassOfDevice { get; set; }

        /// <summary>
        /// 电量百分比 (0-100, -1表示未知)
        /// </summary>
        public int BatteryLevel { get; set; } = -1;

        /// <summary>
        /// 是否是音频设备
        /// </summary>
        public bool IsAudioDevice => (ClassOfDevice & 0x200400) != 0;

        public override string ToString()
        {
            var batteryStr = BatteryLevel >= 0 ? $" [{BatteryLevel}%]" : "";
            var connStr = IsConnected ? " (已连接)" : "";
            return $"{Name}{batteryStr}{connStr}";
        }
    }
}