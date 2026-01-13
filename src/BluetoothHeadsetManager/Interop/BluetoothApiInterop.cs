using System;
using System.Runtime.InteropServices;

namespace BluetoothHeadsetManager.Interop
{
    /// <summary>
    /// Windows Bluetooth API P/Invoke 定义
    /// 用于普通蓝牙设备（非音频设备）的连接和断开
    /// 参考: 32feet 库和 Windows Bluetooth API
    /// </summary>
    public static class BluetoothApiInterop
    {
        private const string BthPropsDll = "bthprops.cpl";

        /// <summary>
        /// 蓝牙服务启用标志
        /// </summary>
        public const uint BLUETOOTH_SERVICE_DISABLE = 0;
        public const uint BLUETOOTH_SERVICE_ENABLE = 1;

        /// <summary>
        /// 最大设备名称长度
        /// </summary>
        public const int BTH_MAX_NAME_SIZE = 248;

        #region 蓝牙服务 GUID

        /// <summary>
        /// A2DP Audio Sink (高质量音频)
        /// </summary>
        public static readonly Guid AudioSinkServiceClass = new Guid("0000110B-0000-1000-8000-00805F9B34FB");

        /// <summary>
        /// Hands-Free (免提)
        /// </summary>
        public static readonly Guid HandsFreeServiceClass = new Guid("0000111E-0000-1000-8000-00805F9B34FB");

        /// <summary>
        /// Headset (耳机)
        /// </summary>
        public static readonly Guid HeadsetServiceClass = new Guid("00001108-0000-1000-8000-00805F9B34FB");

        /// <summary>
        /// AVRCP (音频/视频远程控制)
        /// </summary>
        public static readonly Guid AVRemoteControlServiceClass = new Guid("0000110E-0000-1000-8000-00805F9B34FB");

        /// <summary>
        /// HID (人机接口设备，如键盘鼠标)
        /// </summary>
        public static readonly Guid HumanInterfaceDeviceServiceClass = new Guid("00001124-0000-1000-8000-00805F9B34FB");

        /// <summary>
        /// Serial Port (串口)
        /// </summary>
        public static readonly Guid SerialPortServiceClass = new Guid("00001101-0000-1000-8000-00805F9B34FB");

        #endregion

        #region P/Invoke

        /// <summary>
        /// 设置蓝牙服务状态（启用或禁用）
        /// </summary>
        /// <param name="hRadio">蓝牙适配器句柄，可以为 IntPtr.Zero</param>
        /// <param name="pbtdi">蓝牙设备信息</param>
        /// <param name="pGuidService">服务 GUID</param>
        /// <param name="dwServiceFlags">服务标志（启用/禁用）</param>
        /// <returns>错误代码，0 表示成功</returns>
        [DllImport(BthPropsDll, ExactSpelling = true, SetLastError = true)]
        public static extern int BluetoothSetServiceState(
            IntPtr hRadio,
            ref BLUETOOTH_DEVICE_INFO pbtdi,
            ref Guid pGuidService,
            uint dwServiceFlags);

        /// <summary>
        /// 获取蓝牙设备信息
        /// </summary>
        [DllImport(BthPropsDll, ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int BluetoothGetDeviceInfo(
            IntPtr hRadio,
            ref BLUETOOTH_DEVICE_INFO pbtdi);

        /// <summary>
        /// 枚举设备安装的蓝牙服务
        /// </summary>
        [DllImport(BthPropsDll, ExactSpelling = true, SetLastError = true)]
        public static extern int BluetoothEnumerateInstalledServices(
            IntPtr hRadio,
            ref BLUETOOTH_DEVICE_INFO pbtdi,
            ref int pcServices,
            [In, Out] Guid[] pGuidServices);

        /// <summary>
        /// 查找第一个蓝牙适配器
        /// </summary>
        [DllImport(BthPropsDll, ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr BluetoothFindFirstRadio(
            ref BLUETOOTH_FIND_RADIO_PARAMS pbtfrp,
            out IntPtr phRadio);

        /// <summary>
        /// 查找下一个蓝牙适配器
        /// </summary>
        [DllImport(BthPropsDll, ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool BluetoothFindNextRadio(
            IntPtr hFind,
            out IntPtr phRadio);

        /// <summary>
        /// 关闭适配器查找句柄
        /// </summary>
        [DllImport(BthPropsDll, ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool BluetoothFindRadioClose(IntPtr hFind);

        /// <summary>
        /// 关闭句柄
        /// </summary>
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr handle);

        #endregion
    }

    /// <summary>
    /// 蓝牙设备信息结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct BLUETOOTH_DEVICE_INFO
    {
        /// <summary>
        /// 结构大小
        /// </summary>
        public int dwSize;

        /// <summary>
        /// 设备地址
        /// </summary>
        public ulong Address;

        /// <summary>
        /// 设备类型
        /// </summary>
        public uint ulClassofDevice;

        /// <summary>
        /// 是否已连接
        /// </summary>
        [MarshalAs(UnmanagedType.Bool)]
        public bool fConnected;

        /// <summary>
        /// 是否已记住（配对）
        /// </summary>
        [MarshalAs(UnmanagedType.Bool)]
        public bool fRemembered;

        /// <summary>
        /// 是否已认证
        /// </summary>
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAuthenticated;

        /// <summary>
        /// 最后一次看到的时间
        /// </summary>
        public SYSTEMTIME stLastSeen;

        /// <summary>
        /// 最后一次使用的时间
        /// </summary>
        public SYSTEMTIME stLastUsed;

        /// <summary>
        /// 设备名称
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = BluetoothApiInterop.BTH_MAX_NAME_SIZE)]
        public string szName;

        /// <summary>
        /// 创建一个新的 BLUETOOTH_DEVICE_INFO 实例
        /// </summary>
        public static BLUETOOTH_DEVICE_INFO Create()
        {
            return new BLUETOOTH_DEVICE_INFO
            {
                dwSize = Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>()
            };
        }

        /// <summary>
        /// 从 MAC 地址创建 BLUETOOTH_DEVICE_INFO
        /// </summary>
        public static BLUETOOTH_DEVICE_INFO FromAddress(ulong address)
        {
            return new BLUETOOTH_DEVICE_INFO
            {
                dwSize = Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>(),
                Address = address
            };
        }

        /// <summary>
        /// 从 MAC 地址字符串创建 BLUETOOTH_DEVICE_INFO
        /// </summary>
        public static BLUETOOTH_DEVICE_INFO FromAddress(string macAddress)
        {
            var cleanMac = macAddress.Replace(":", "").Replace("-", "");
            ulong address = Convert.ToUInt64(cleanMac, 16);
            return FromAddress(address);
        }
    }

    /// <summary>
    /// 系统时间结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEMTIME
    {
        public ushort wYear;
        public ushort wMonth;
        public ushort wDayOfWeek;
        public ushort wDay;
        public ushort wHour;
        public ushort wMinute;
        public ushort wSecond;
        public ushort wMilliseconds;
    }

    /// <summary>
    /// 蓝牙适配器查找参数
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct BLUETOOTH_FIND_RADIO_PARAMS
    {
        public int dwSize;

        public static BLUETOOTH_FIND_RADIO_PARAMS Create()
        {
            return new BLUETOOTH_FIND_RADIO_PARAMS
            {
                dwSize = Marshal.SizeOf<BLUETOOTH_FIND_RADIO_PARAMS>()
            };
        }
    }

    /// <summary>
    /// 蓝牙服务连接辅助类
    /// </summary>
    public static class BluetoothServiceHelper
    {
        /// <summary>
        /// 音频设备服务 GUID 列表
        /// </summary>
        public static readonly Guid[] AudioServiceGuids = new[]
        {
            BluetoothApiInterop.AudioSinkServiceClass,
            BluetoothApiInterop.HandsFreeServiceClass,
            BluetoothApiInterop.HeadsetServiceClass,
            BluetoothApiInterop.AVRemoteControlServiceClass
        };

        /// <summary>
        /// 启用蓝牙设备的音频服务
        /// </summary>
        /// <param name="macAddress">设备 MAC 地址</param>
        /// <returns>是否成功启用至少一个服务</returns>
        public static bool EnableAudioServices(string macAddress)
        {
            var deviceInfo = BLUETOOTH_DEVICE_INFO.FromAddress(macAddress);
            bool anySuccess = false;

            // 刷新设备信息
            BluetoothApiInterop.BluetoothGetDeviceInfo(IntPtr.Zero, ref deviceInfo);

            foreach (var serviceGuid in AudioServiceGuids)
            {
                var guid = serviceGuid;
                int result = BluetoothApiInterop.BluetoothSetServiceState(
                    IntPtr.Zero,
                    ref deviceInfo,
                    ref guid,
                    BluetoothApiInterop.BLUETOOTH_SERVICE_ENABLE);

                if (result == 0)
                {
                    anySuccess = true;
                    System.Diagnostics.Debug.WriteLine($"成功启用服务: {guid}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"启用服务失败: {guid}, 错误码: {result}");
                }
            }

            return anySuccess;
        }

        /// <summary>
        /// 禁用蓝牙设备的音频服务
        /// </summary>
        /// <param name="macAddress">设备 MAC 地址</param>
        /// <returns>是否成功禁用至少一个服务</returns>
        public static bool DisableAudioServices(string macAddress)
        {
            var deviceInfo = BLUETOOTH_DEVICE_INFO.FromAddress(macAddress);
            bool anySuccess = false;

            // 刷新设备信息
            BluetoothApiInterop.BluetoothGetDeviceInfo(IntPtr.Zero, ref deviceInfo);

            foreach (var serviceGuid in AudioServiceGuids)
            {
                var guid = serviceGuid;
                int result = BluetoothApiInterop.BluetoothSetServiceState(
                    IntPtr.Zero,
                    ref deviceInfo,
                    ref guid,
                    BluetoothApiInterop.BLUETOOTH_SERVICE_DISABLE);

                if (result == 0)
                {
                    anySuccess = true;
                    System.Diagnostics.Debug.WriteLine($"成功禁用服务: {guid}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"禁用服务失败: {guid}, 错误码: {result}");
                }
            }

            return anySuccess;
        }

        /// <summary>
        /// 获取设备已安装的服务列表
        /// </summary>
        public static Guid[] GetInstalledServices(string macAddress)
        {
            var deviceInfo = BLUETOOTH_DEVICE_INFO.FromAddress(macAddress);
            
            // 刷新设备信息
            BluetoothApiInterop.BluetoothGetDeviceInfo(IntPtr.Zero, ref deviceInfo);

            // 首先获取服务数量
            int serviceCount = 0;
            BluetoothApiInterop.BluetoothEnumerateInstalledServices(
                IntPtr.Zero,
                ref deviceInfo,
                ref serviceCount,
                null!);

            if (serviceCount == 0)
                return Array.Empty<Guid>();

            // 然后获取服务列表
            var services = new Guid[serviceCount];
            int result = BluetoothApiInterop.BluetoothEnumerateInstalledServices(
                IntPtr.Zero,
                ref deviceInfo,
                ref serviceCount,
                services);

            if (result != 0)
            {
                System.Diagnostics.Debug.WriteLine($"枚举服务失败, 错误码: {result}");
                return Array.Empty<Guid>();
            }

            return services;
        }
    }
}