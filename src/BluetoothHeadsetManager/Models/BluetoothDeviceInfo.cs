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
        /// 是否支持 IKsControl 连接方式
        /// 仅音频设备支持此方式
        /// </summary>
        public bool SupportsKsControl { get; set; }

        /// <summary>
        /// 是否是音频设备
        /// 基于 ClassOfDevice 判断:
        /// - 0x200400: 音频设备
        /// - 0x240400: 带麦克风的耳机
        /// - 0x200404: 可穿戴耳机设备
        /// </summary>
        public bool IsAudioDevice
        {
            get
            {
                // Major Device Class: Audio/Video (0x04)
                // Minor Device Class 检查
                uint majorClass = (ClassOfDevice >> 8) & 0x1F;
                return majorClass == 0x04 || // Audio/Video
                       (ClassOfDevice & 0x200400) != 0 || // 音频渲染器
                       (ClassOfDevice & 0x240400) != 0;   // 音频/话筒
            }
        }

        /// <summary>
        /// 是否是耳机设备
        /// </summary>
        public bool IsHeadset
        {
            get
            {
                uint majorClass = (ClassOfDevice >> 8) & 0x1F;
                uint minorClass = (ClassOfDevice >> 2) & 0x3F;
                
                // Major: Audio/Video (0x04)
                // Minor: 0x01 = Headset, 0x02 = Hands-free, 0x06 = Headphones
                return majorClass == 0x04 && 
                       (minorClass == 0x01 || minorClass == 0x02 || minorClass == 0x06);
            }
        }

        /// <summary>
        /// 连接类型描述
        /// </summary>
        public string ConnectionMethod
        {
            get
            {
                if (SupportsKsControl)
                    return "IKsControl (音频驱动)";
                else if (IsAudioDevice)
                    return "BluetoothSetServiceState";
                else
                    return "标准蓝牙";
            }
        }

        /// <summary>
        /// 获取设备图标名称
        /// </summary>
        public string IconName
        {
            get
            {
                if (IsHeadset)
                    return "headphones";
                else if (IsAudioDevice)
                    return "speaker";
                else
                    return "bluetooth";
            }
        }

        public override string ToString()
        {
            var batteryStr = BatteryLevel >= 0 ? $" [{BatteryLevel}%]" : "";
            var connStr = IsConnected ? " (已连接)" : "";
            var ksStr = SupportsKsControl ? " ★" : "";
            return $"{Name}{batteryStr}{connStr}{ksStr}";
        }

        /// <summary>
        /// 获取显示名称（用于菜单）
        /// </summary>
        public string DisplayName
        {
            get
            {
                var parts = new System.Collections.Generic.List<string> { Name };
                
                if (BatteryLevel >= 0)
                    parts.Add($"[{BatteryLevel}%]");
                
                if (IsConnected)
                    parts.Add("✓");
                
                return string.Join(" ", parts);
            }
        }

        /// <summary>
        /// 获取状态提示
        /// </summary>
        public string StatusTip
        {
            get
            {
                var status = IsConnected ? "已连接" : "未连接";
                var method = ConnectionMethod;
                var battery = BatteryLevel >= 0 ? $", 电量: {BatteryLevel}%" : "";
                
                return $"状态: {status}, 连接方式: {method}{battery}";
            }
        }
    }
}