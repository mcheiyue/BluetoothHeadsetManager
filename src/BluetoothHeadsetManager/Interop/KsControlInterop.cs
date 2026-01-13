using System;
using System.Runtime.InteropServices;

namespace BluetoothHeadsetManager.Interop
{
    /// <summary>
    /// Kernel Streaming Control 接口定义
    /// 用于控制蓝牙音频设备的连接和断开
    /// 参考: ToothTray 项目和 Windows WDM 音频驱动
    /// </summary>
    public static class KsControlInterop
    {
        /// <summary>
        /// KSPROPSETID_BtAudio - 蓝牙音频属性集 GUID
        /// 用于控制蓝牙音频设备的连接状态
        /// </summary>
        public static readonly Guid KSPROPSETID_BtAudio = new Guid("7FA06C40-B8F6-4C7E-8556-E8C33A12E54D");

        /// <summary>
        /// 重新连接蓝牙音频设备
        /// </summary>
        public const uint KSPROPERTY_ONESHOT_RECONNECT = 1;

        /// <summary>
        /// 断开蓝牙音频设备连接
        /// </summary>
        public const uint KSPROPERTY_ONESHOT_DISCONNECT = 2;

        /// <summary>
        /// KSPROPERTY Flags
        /// </summary>
        public const uint KSPROPERTY_TYPE_GET = 0x00000001;
        public const uint KSPROPERTY_TYPE_SET = 0x00000002;

        /// <summary>
        /// COM 激活上下文
        /// </summary>
        public const uint CLSCTX_ALL = 0x17; // CLSCTX_INPROC_SERVER | CLSCTX_INPROC_HANDLER | CLSCTX_LOCAL_SERVER | CLSCTX_REMOTE_SERVER
        public const uint CLSCTX_INPROC_SERVER = 0x1;
    }

    /// <summary>
    /// KSPROPERTY 结构
    /// 必须与 Windows ks.h 中的定义完全匹配
    /// Pack=1 确保没有填充字节
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct KSPROPERTY
    {
        /// <summary>
        /// 属性集 GUID (16 bytes)
        /// </summary>
        public Guid Set;

        /// <summary>
        /// 属性 ID (4 bytes - ULONG)
        /// </summary>
        public uint Id;

        /// <summary>
        /// 属性标志 (4 bytes - ULONG)
        /// </summary>
        public uint Flags;
    }

    /// <summary>
    /// IKsControl 接口
    /// 用于向驱动程序发送 Kernel Streaming 控制命令
    /// 注意：使用指针传递 KSPROPERTY 以匹配 C++ 签名
    /// </summary>
    [ComImport]
    [Guid("28F54685-06FD-11D2-B27A-00A0C9223196")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IKsControl
    {
        /// <summary>
        /// 获取或设置属性
        /// </summary>
        /// <param name="Property">指向 KSPROPERTY 结构的指针</param>
        /// <param name="PropertyLength">KSPROPERTY 结构大小 (ULONG)</param>
        /// <param name="PropertyData">属性数据缓冲区</param>
        /// <param name="DataLength">数据长度 (ULONG)</param>
        /// <param name="BytesReturned">返回的字节数</param>
        /// <returns>HRESULT</returns>
        [PreserveSig]
        int KsProperty(
            IntPtr Property,
            uint PropertyLength,
            IntPtr PropertyData,
            uint DataLength,
            out uint BytesReturned);

        /// <summary>
        /// 获取或设置方法
        /// </summary>
        [PreserveSig]
        int KsMethod(
            IntPtr Method,
            uint MethodLength,
            IntPtr MethodData,
            uint DataLength,
            out uint BytesReturned);

        /// <summary>
        /// 获取或设置事件
        /// </summary>
        [PreserveSig]
        int KsEvent(
            IntPtr Event,
            uint EventLength,
            IntPtr EventData,
            uint DataLength,
            out uint BytesReturned);
    }

    /// <summary>
    /// 蓝牙音频设备信息
    /// </summary>
    public class BluetoothAudioDeviceInfo
    {
        /// <summary>
        /// 设备名称 (来自 Container)
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 设备容器 ID
        /// </summary>
        public Guid ContainerId { get; set; }

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// 关联的 KsControl 接口列表
        /// 一个蓝牙设备可能有多个音频端点（如 A2DP 和 HFP）
        /// </summary>
        public System.Collections.Generic.List<IKsControl> KsControls { get; } = new();

        /// <summary>
        /// 设备 ID 列表
        /// </summary>
        public System.Collections.Generic.List<string> DeviceIds { get; } = new();

        /// <summary>
        /// 添加 KsControl 并更新连接状态
        /// </summary>
        public void AddKsControl(IKsControl ksControl, uint deviceState)
        {
            KsControls.Add(ksControl);
            // 只要有一个端点是活动的，就认为设备已连接
            if (deviceState == CoreAudioInterop.DEVICE_STATE_ACTIVE)
            {
                IsConnected = true;
            }
        }

        /// <summary>
        /// 连接设备
        /// </summary>
        public bool Connect()
        {
            return SendKsProperty(KsControlInterop.KSPROPERTY_ONESHOT_RECONNECT);
        }

        /// <summary>
        /// 断开设备
        /// </summary>
        public bool Disconnect()
        {
            return SendKsProperty(KsControlInterop.KSPROPERTY_ONESHOT_DISCONNECT);
        }

        /// <summary>
        /// 向所有关联的 KsControl 发送属性请求
        /// 参考 ToothTray 的实现：使用 KSPROPERTY_TYPE_GET 触发连接/断开操作
        /// </summary>
        private bool SendKsProperty(uint propertyId)
        {
            string actionName = propertyId == KsControlInterop.KSPROPERTY_ONESHOT_RECONNECT ? "RECONNECT" : "DISCONNECT";
            Services.Logger.Log($"SendKsProperty: {actionName} (propertyId={propertyId}) 到 {KsControls.Count} 个 KsControl");
            
            if (KsControls.Count == 0)
            {
                Services.Logger.LogError($"SendKsProperty: 没有 KsControl 接口");
                return false;
            }

            var property = new KSPROPERTY
            {
                Set = KsControlInterop.KSPROPSETID_BtAudio,
                Id = propertyId,
                // ToothTray 使用 GET 标志 - 这是一个触发操作
                Flags = KsControlInterop.KSPROPERTY_TYPE_GET
            };
            
            // 验证结构体大小 - 应该是 24 字节 (16 + 4 + 4)
            int propertySize = Marshal.SizeOf<KSPROPERTY>();
            Services.Logger.Log($"  KSPROPERTY 结构体大小: {propertySize} 字节 (期望: 24)");
            Services.Logger.Log($"  KSPROPERTY: Set={property.Set}, Id={property.Id}, Flags={property.Flags}");

            bool anySuccess = false;
            int successCount = 0;
            int index = 0;
            
            // 分配非托管内存来存储 KSPROPERTY 结构
            IntPtr propertyPtr = Marshal.AllocHGlobal(propertySize);
            
            try
            {
                // 将结构复制到非托管内存
                Marshal.StructureToPtr(property, propertyPtr, false);
                
                foreach (var ksControl in KsControls)
                {
                    try
                    {
                        Services.Logger.Log($"  调用 KsControl[{index}].KsProperty...");
                        uint bytesReturned;
                        int hr = ksControl.KsProperty(
                            propertyPtr,
                            (uint)propertySize,
                            IntPtr.Zero,
                            0,
                            out bytesReturned);

                        Services.Logger.Log($"    结果: HRESULT=0x{hr:X8} ({hr}), bytesReturned={bytesReturned}");

                        // 对于 ONESHOT 属性，即使返回 S_FALSE (1) 也算成功
                        // 有些驱动可能返回 0x80070057 (E_INVALIDARG) 但仍然执行了操作
                        if (hr >= 0)
                        {
                            anySuccess = true;
                            successCount++;
                            Services.Logger.Log($"    成功!");
                        }
                        else
                        {
                            Services.Logger.Log($"    失败: HRESULT=0x{hr:X8}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Services.Logger.LogError($"    KsProperty 异常", ex);
                    }
                    index++;
                }
            }
            finally
            {
                // 释放非托管内存
                Marshal.FreeHGlobal(propertyPtr);
            }

            Services.Logger.Log($"SendKsProperty 完成: {successCount}/{KsControls.Count} 成功, anySuccess={anySuccess}");
            return anySuccess;
        }

        public override string ToString()
        {
            var status = IsConnected ? "已连接" : "未连接";
            return $"{Name} ({status})";
        }
    }
}