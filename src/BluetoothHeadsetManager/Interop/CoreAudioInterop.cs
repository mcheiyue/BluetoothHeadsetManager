using System;
using System.Runtime.InteropServices;

namespace BluetoothHeadsetManager.Interop
{
    /// <summary>
    /// Core Audio API COM 接口和常量定义
    /// 参考: https://docs.microsoft.com/en-us/windows/win32/coreaudio/core-audio-apis-in-windows-vista
    /// </summary>
    public static class CoreAudioInterop
    {
        // CLSID 和 IID
        public static readonly Guid CLSID_MMDeviceEnumerator = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
        public static readonly Guid IID_IMMDeviceEnumerator = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");
        public static readonly Guid IID_IDeviceTopology = new Guid("2A07407E-6497-4A18-9787-32F79BD0D98F");
        public static readonly Guid IID_IKsControl = new Guid("28F54685-06FD-11D2-B27A-00A0C9223196");
        public static readonly Guid IID_IPart = new Guid("AE2DE0E4-5BCA-4F2D-AA46-5D13F8FDB3A9");

        // Property Keys
        public static readonly PropertyKey PKEY_Device_FriendlyName = new PropertyKey(
            new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);
        public static readonly PropertyKey PKEY_Device_ContainerId = new PropertyKey(
            new Guid("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C"), 2);

        // 设备状态常量
        public const uint DEVICE_STATE_ACTIVE = 0x00000001;
        public const uint DEVICE_STATE_DISABLED = 0x00000002;
        public const uint DEVICE_STATE_NOTPRESENT = 0x00000004;
        public const uint DEVICE_STATE_UNPLUGGED = 0x00000008;
        public const uint DEVICE_STATEMASK_ALL = 0x0000000F;

        // Storage access mode
        public const uint STGM_READ = 0x00000000;

        /// <summary>
        /// 创建 MMDeviceEnumerator 实例
        /// </summary>
        public static IMMDeviceEnumerator CreateDeviceEnumerator()
        {
            var enumeratorType = Type.GetTypeFromCLSID(CLSID_MMDeviceEnumerator);
            if (enumeratorType == null)
                throw new InvalidOperationException("无法获取 MMDeviceEnumerator 类型");
            
            var instance = Activator.CreateInstance(enumeratorType);
            if (instance == null)
                throw new InvalidOperationException("无法创建 MMDeviceEnumerator 实例");
                
            return (IMMDeviceEnumerator)instance;
        }
    }

    /// <summary>
    /// Property Key 结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PropertyKey
    {
        public Guid fmtid;
        public uint pid;

        public PropertyKey(Guid formatId, uint propertyId)
        {
            fmtid = formatId;
            pid = propertyId;
        }
    }

    /// <summary>
    /// PROPVARIANT 结构 (简化版本)
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct PropVariant : IDisposable
    {
        [FieldOffset(0)]
        public ushort vt;
        [FieldOffset(8)]
        public IntPtr pwszVal;
        [FieldOffset(8)]
        public IntPtr punkVal;
        [FieldOffset(8)]
        public Guid guidVal;

        public const ushort VT_EMPTY = 0;
        public const ushort VT_LPWSTR = 31;
        public const ushort VT_CLSID = 72;

        public string? GetString()
        {
            if (vt == VT_LPWSTR && pwszVal != IntPtr.Zero)
                return Marshal.PtrToStringUni(pwszVal);
            return null;
        }

        public Guid GetGuid()
        {
            if (vt == VT_CLSID && pwszVal != IntPtr.Zero)
                return Marshal.PtrToStructure<Guid>(pwszVal);
            return Guid.Empty;
        }

        public void Dispose()
        {
            PropVariantClear(ref this);
        }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PropVariant pvar);
    }

    /// <summary>
    /// 数据流方向
    /// </summary>
    public enum EDataFlow
    {
        eRender = 0,
        eCapture = 1,
        eAll = 2
    }

    /// <summary>
    /// 设备角色
    /// </summary>
    public enum ERole
    {
        eConsole = 0,
        eMultimedia = 1,
        eCommunications = 2
    }

    /// <summary>
    /// 连接器类型
    /// </summary>
    public enum ConnectorType
    {
        Unknown_Connector = 0,
        Physical_Internal = 1,
        Physical_External = 2,
        Software_IO = 3,
        Software_Fixed = 4,
        Network = 5
    }

    #region COM Interfaces

    /// <summary>
    /// IMMDeviceEnumerator 接口
    /// </summary>
    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(
            EDataFlow dataFlow,
            uint dwStateMask,
            out IMMDeviceCollection ppDevices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(
            EDataFlow dataFlow,
            ERole role,
            out IMMDevice ppEndpoint);

        [PreserveSig]
        int GetDevice(
            [MarshalAs(UnmanagedType.LPWStr)] string pwstrId,
            out IMMDevice ppDevice);

        [PreserveSig]
        int RegisterEndpointNotificationCallback(IntPtr pClient);

        [PreserveSig]
        int UnregisterEndpointNotificationCallback(IntPtr pClient);
    }

    /// <summary>
    /// IMMDeviceCollection 接口
    /// </summary>
    [ComImport]
    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDeviceCollection
    {
        [PreserveSig]
        int GetCount(out uint pcDevices);

        [PreserveSig]
        int Item(uint nDevice, out IMMDevice ppDevice);
    }

    /// <summary>
    /// IMMDevice 接口
    /// </summary>
    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDevice
    {
        [PreserveSig]
        int Activate(
            ref Guid iid,
            uint dwClsCtx,
            IntPtr pActivationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);

        [PreserveSig]
        int OpenPropertyStore(
            uint stgmAccess,
            out IPropertyStore ppProperties);

        [PreserveSig]
        int GetId(
            [MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);

        [PreserveSig]
        int GetState(out uint pdwState);
    }

    /// <summary>
    /// IPropertyStore 接口
    /// </summary>
    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyStore
    {
        [PreserveSig]
        int GetCount(out uint cProps);

        [PreserveSig]
        int GetAt(uint iProp, out PropertyKey pkey);

        [PreserveSig]
        int GetValue(ref PropertyKey key, out PropVariant pv);

        [PreserveSig]
        int SetValue(ref PropertyKey key, ref PropVariant propvar);

        [PreserveSig]
        int Commit();
    }

    /// <summary>
    /// IDeviceTopology 接口
    /// </summary>
    [ComImport]
    [Guid("2A07407E-6497-4A18-9787-32F79BD0D98F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDeviceTopology
    {
        [PreserveSig]
        int GetConnectorCount(out uint pCount);

        [PreserveSig]
        int GetConnector(uint nIndex, out IConnector ppConnector);

        [PreserveSig]
        int GetSubunitCount(out uint pCount);

        [PreserveSig]
        int GetSubunit(uint nIndex, out IntPtr ppSubunit);

        [PreserveSig]
        int GetPartById(uint nId, out IntPtr ppPart);

        [PreserveSig]
        int GetDeviceId([MarshalAs(UnmanagedType.LPWStr)] out string ppwstrDeviceId);

        [PreserveSig]
        int GetSignalPath(
            IntPtr pIPartFrom,
            IntPtr pIPartTo,
            [MarshalAs(UnmanagedType.Bool)] bool bRejectMixedPaths,
            out IntPtr ppParts);
    }

    /// <summary>
    /// IConnector 接口
    /// 注意：不要在接口中定义 QueryInterface，COM 互操作会自动处理
    /// </summary>
    [ComImport]
    [Guid("9C2C4058-23F5-41DE-877A-DF3AF236A09E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IConnector
    {
        [PreserveSig]
        int QueryConnectorType(out ConnectorType pType);

        [PreserveSig]
        int GetDataFlow(out int pFlow);

        [PreserveSig]
        int ConnectTo([MarshalAs(UnmanagedType.Interface)] IConnector pConnectTo);

        [PreserveSig]
        int Disconnect();

        [PreserveSig]
        int IsConnected([MarshalAs(UnmanagedType.Bool)] out bool pbConnected);

        [PreserveSig]
        int GetConnectedTo([MarshalAs(UnmanagedType.Interface)] out IConnector ppConTo);

        [PreserveSig]
        int GetConnectorIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string ppwstrConnectorId);

        [PreserveSig]
        int GetDeviceIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string ppwstrDeviceId);
    }

    /// <summary>
    /// IPart 接口
    /// </summary>
    [ComImport]
    [Guid("AE2DE0E4-5BCA-4F2D-AA46-5D13F8FDB3A9")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPart
    {
        [PreserveSig]
        int GetName([MarshalAs(UnmanagedType.LPWStr)] out string ppwstrName);

        [PreserveSig]
        int GetLocalId(out uint pnId);

        [PreserveSig]
        int GetGlobalId([MarshalAs(UnmanagedType.LPWStr)] out string ppwstrGlobalId);

        [PreserveSig]
        int GetPartType(out int pPartType);

        [PreserveSig]
        int GetSubType(out Guid pSubType);

        [PreserveSig]
        int GetControlInterfaceCount(out uint pCount);

        [PreserveSig]
        int GetControlInterface(uint nIndex, out IntPtr ppInterfaceDesc);

        [PreserveSig]
        int EnumPartsIncoming(out IntPtr ppParts);

        [PreserveSig]
        int EnumPartsOutgoing(out IntPtr ppParts);

        [PreserveSig]
        int GetTopologyObject([MarshalAs(UnmanagedType.Interface)] out IDeviceTopology ppTopology);

        [PreserveSig]
        int Activate(uint dwClsContext, ref Guid refiid, out IntPtr ppvObject);

        [PreserveSig]
        int RegisterControlChangeCallback(ref Guid riid, IntPtr pNotify);

        [PreserveSig]
        int UnregisterControlChangeCallback(IntPtr pNotify);
    }

    #endregion
}