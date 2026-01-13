using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace BluetoothHeadsetManager.Services
{
    /// <summary>
    /// 电量服务 - 读取蓝牙设备电量
    /// 参考 BlueGauge 项目的实现
    /// </summary>
    public class BatteryService : IDisposable
    {
        private bool _disposed;

        // DEVPKEY_Bluetooth_Battery - 用于从PnP设备读取电量
        // 参考 BlueGauge: GUID 0x104EA319_6EE2_4701_BD47_8DDBF425BBE5, PID = 2
        private static readonly Guid DEVPKEY_BLUETOOTH_BATTERY_FMTID =
            new Guid(0x104EA319, 0x6EE2, 0x4701, 0xBD, 0x47, 0x8D, 0xDB, 0xF4, 0x25, 0xBB, 0xE5);
        private const uint DEVPKEY_BLUETOOTH_BATTERY_PID = 2;

        // DEVPKEY_Bluetooth_DeviceAddress - 用于从PnP设备读取蓝牙地址
        // 参考 BlueGauge: windows_sys::Wdk::Devices::Bluetooth::DEVPKEY_Bluetooth_DeviceAddress
        private static readonly Guid DEVPKEY_BLUETOOTH_DEVICEADDRESS_FMTID =
            new Guid(0x2BD67D8B, 0x8BEB, 0x48D5, 0x87, 0xE0, 0x6C, 0xDA, 0x34, 0x28, 0x04, 0x0A);
        private const uint DEVPKEY_BLUETOOTH_DEVICEADDRESS_PID = 1;

        // GATT Battery Service UUID
        private static readonly Guid BATTERY_SERVICE_UUID =
            new Guid("0000180F-0000-1000-8000-00805F9B34FB");
        // GATT Battery Level Characteristic UUID
        private static readonly Guid BATTERY_LEVEL_CHARACTERISTIC_UUID =
            new Guid("00002A19-0000-1000-8000-00805F9B34FB");

        // 蓝牙设备实例ID前缀
        private const string BT_INSTANCE_PREFIX = "BTHENUM\\";

        #region Native Methods for PnP Device Battery Reading

        // Configuration Manager error codes
        private const int CR_SUCCESS = 0;

        // Device property types
        private const uint DEVPROP_TYPE_BYTE = 0x00000003;
        private const uint DEVPROP_TYPE_STRING = 0x00000012;

        // CM_LOCATE_DEVNODE flags
        private const uint CM_LOCATE_DEVNODE_NORMAL = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct DEVPROPKEY
        {
            public Guid fmtid;
            public uint pid;
        }

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern int CM_Locate_DevNodeW(
            out uint pdnDevInst,
            string pDeviceID,
            uint ulFlags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern int CM_Get_DevNode_PropertyW(
            uint dnDevInst,
            ref DEVPROPKEY propertyKey,
            out uint propertyType,
            out byte propertyBuffer,
            ref uint propertyBufferSize,
            uint ulFlags);
        
        // 重载版本 - 用于读取字符串属性
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern int CM_Get_DevNode_PropertyW(
            uint dnDevInst,
            ref DEVPROPKEY propertyKey,
            out uint propertyType,
            IntPtr propertyBuffer,
            ref uint propertyBufferSize,
            uint ulFlags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern int CM_Get_Device_ID_ListW(
            string? pszFilter,
            IntPtr buffer,
            uint bufferLen,
            uint ulFlags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern int CM_Get_Device_ID_List_SizeW(
            out uint pulLen,
            string? pszFilter,
            uint ulFlags);

        // CM_GET_DEVICE_ID_LIST flags
        private const uint CM_GETIDLIST_FILTER_PRESENT = 0x00000100;
        private const uint CM_GETIDLIST_FILTER_CLASS = 0x00000200;
        private const uint CM_GETIDLIST_FILTER_ENUMERATOR = 0x00000004;

        #endregion

        /// <summary>
        /// 获取蓝牙设备的电量
        /// </summary>
        /// <param name="macAddress">设备MAC地址</param>
        /// <returns>电量百分比 (0-100)，失败返回 -1</returns>
        public async Task<int> GetBatteryLevelAsync(string macAddress)
        {
            Logger.Log($"[电量] 开始获取电量: {macAddress}");
            
            // 首先尝试通过 PnP 设备属性读取（经典蓝牙）
            int batteryLevel = await GetBatteryFromPnPAsync(macAddress);
            if (batteryLevel >= 0)
            {
                Logger.Log($"[电量] PnP 方式成功: {batteryLevel}%");
                return batteryLevel;
            }

            // 如果失败，尝试通过 BLE GATT 读取
            Logger.Log($"[电量] PnP 方式失败，尝试 BLE GATT...");
            batteryLevel = await GetBatteryFromBleGattAsync(macAddress);
            if (batteryLevel >= 0)
            {
                Logger.Log($"[电量] BLE GATT 方式成功: {batteryLevel}%");
            }
            else
            {
                Logger.Log($"[电量] 所有方式均失败");
            }
            return batteryLevel;
        }

        /// <summary>
        /// 通过 PnP 设备属性读取电量（经典蓝牙）
        /// 参考 BlueGauge 的 btc.rs 实现
        /// </summary>
        private async Task<int> GetBatteryFromPnPAsync(string macAddress)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 将MAC地址转换为蓝牙地址格式
                    string cleanMac = macAddress.Replace(":", "").Replace("-", "").ToUpperInvariant();
                    Logger.Log($"[电量] 查找 PnP 设备, 地址: {cleanMac}");
                    
                    // 查找匹配的蓝牙PnP设备实例ID
                    string? instanceId = FindBluetoothPnPDeviceInstanceId(cleanMac);
                    if (instanceId == null)
                    {
                        Logger.Log($"[电量] 未找到匹配的 PnP 设备: {macAddress}");
                        return -1;
                    }

                    Logger.Log($"[电量] 找到 PnP 设备: {instanceId}");
                    
                    // 读取电量属性
                    return ReadBatteryFromDeviceNode(instanceId);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[电量] PnP 电量读取异常: {ex.Message}", ex);
                    return -1;
                }
            });
        }

        /// <summary>
        /// 查找蓝牙设备的PnP实例ID
        /// 参考 BlueGauge 的实现: 枚举 GUID_DEVCLASS_SYSTEM 类下的所有设备
        /// 筛选包含 "BTHENUM\" 和蓝牙地址的设备
        /// </summary>
        private string? FindBluetoothPnPDeviceInstanceId(string btAddress)
        {
            try
            {
                // 获取所有系统类设备列表大小
                // GUID_DEVCLASS_SYSTEM = {4D36E97D-E325-11CE-BFC1-08002BE10318}
                string filter = "{4D36E97D-E325-11CE-BFC1-08002BE10318}";
                
                int result = CM_Get_Device_ID_List_SizeW(out uint listSize, filter,
                    CM_GETIDLIST_FILTER_PRESENT | CM_GETIDLIST_FILTER_CLASS);
                
                if (result != CR_SUCCESS || listSize == 0)
                {
                    Logger.Log($"[电量] CM_Get_Device_ID_List_SizeW 失败: result={result}, listSize={listSize}");
                    return null;
                }

                Logger.Log($"[电量] 设备列表大小: {listSize} 字符");

                // 分配缓冲区并获取设备列表
                IntPtr buffer = Marshal.AllocHGlobal((int)(listSize * 2)); // Unicode characters
                try
                {
                    result = CM_Get_Device_ID_ListW(filter, buffer, listSize,
                        CM_GETIDLIST_FILTER_PRESENT | CM_GETIDLIST_FILTER_CLASS);
                    
                    if (result != CR_SUCCESS)
                    {
                        Logger.Log($"[电量] CM_Get_Device_ID_ListW 失败: {result}");
                        return null;
                    }

                    // 解析设备ID列表（以null分隔，以双null结尾）
                    List<string> deviceIds = new List<string>();
                    int offset = 0;
                    while (true)
                    {
                        string? deviceId = Marshal.PtrToStringUni(buffer + offset);
                        if (string.IsNullOrEmpty(deviceId))
                        {
                            break;
                        }
                        deviceIds.Add(deviceId);
                        offset += (deviceId.Length + 1) * 2;
                    }

                    Logger.Log($"[电量] 找到 {deviceIds.Count} 个系统类设备");
                    
                    // 查找匹配蓝牙地址的设备
                    // BlueGauge 使用两个条件: 包含 "BTHENUM\" 和 蓝牙地址
                    foreach (string deviceId in deviceIds)
                    {
                        if (deviceId.StartsWith(BT_INSTANCE_PREFIX, StringComparison.OrdinalIgnoreCase) &&
                            deviceId.Contains(btAddress, StringComparison.OrdinalIgnoreCase))
                        {
                            return deviceId;
                        }
                    }

                    // 如果没找到，记录一些候选设备用于调试
                    var btDevices = deviceIds.FindAll(d => d.StartsWith(BT_INSTANCE_PREFIX, StringComparison.OrdinalIgnoreCase));
                    if (btDevices.Count > 0)
                    {
                        Logger.Log($"[电量] 找到 {btDevices.Count} 个蓝牙设备，但没有匹配地址 {btAddress}");
                        foreach (var dev in btDevices.Take(3))
                        {
                            Logger.Log($"[电量]   候选: {dev}");
                        }
                    }

                    return null;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[电量] 查找 PnP 设备异常: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// 从设备节点读取电量属性
        /// 参考 BlueGauge 的 read_pnp_device_battery_from_instance_id 函数
        /// </summary>
        private int ReadBatteryFromDeviceNode(string instanceId)
        {
            try
            {
                // 获取设备节点句柄
                int result = CM_Locate_DevNodeW(out uint devNode, instanceId, CM_LOCATE_DEVNODE_NORMAL);
                if (result != CR_SUCCESS)
                {
                    Logger.Log($"[电量] CM_Locate_DevNodeW 失败: {result}");
                    return -1;
                }

                // 准备DEVPROPKEY
                DEVPROPKEY propKey = new DEVPROPKEY
                {
                    fmtid = DEVPKEY_BLUETOOTH_BATTERY_FMTID,
                    pid = DEVPKEY_BLUETOOTH_BATTERY_PID
                };

                uint propertyType;
                byte batteryLevel;
                uint bufferSize = 1;

                // 读取电量属性
                result = CM_Get_DevNode_PropertyW(
                    devNode,
                    ref propKey,
                    out propertyType,
                    out batteryLevel,
                    ref bufferSize,
                    0);

                if (result != CR_SUCCESS)
                {
                    // CR_NO_SUCH_VALUE (0x00000025) = 属性不存在，这是正常的（设备可能不支持电量上报）
                    if (result == 0x25)
                    {
                        Logger.Log($"[电量] 设备不支持电量上报 (CR_NO_SUCH_VALUE)");
                    }
                    else
                    {
                        Logger.Log($"[电量] CM_Get_DevNode_PropertyW 失败: 0x{result:X8}");
                    }
                    return -1;
                }

                if (propertyType != DEVPROP_TYPE_BYTE)
                {
                    Logger.Log($"[电量] 电量属性类型错误: {propertyType}");
                    return -1;
                }

                return batteryLevel;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[电量] 读取设备节点电量异常: {ex.Message}", ex);
                return -1;
            }
        }

        /// <summary>
        /// 通过 BLE GATT Battery Service 读取电量
        /// 参考 BlueGauge 的 ble.rs 实现
        /// </summary>
        private async Task<int> GetBatteryFromBleGattAsync(string macAddress)
        {
            try
            {
                // 将MAC地址转换为蓝牙地址 (ulong)
                string cleanMac = macAddress.Replace(":", "").Replace("-", "");
                if (!ulong.TryParse(cleanMac, System.Globalization.NumberStyles.HexNumber, null, out ulong bluetoothAddress))
                {
                    Logger.Log($"[电量] 无法解析 MAC 地址: {macAddress}");
                    return -1;
                }

                Logger.Log($"[电量] 尝试 BLE GATT 连接: 0x{bluetoothAddress:X12}");

                // 使用 Windows.Devices.Bluetooth API
                var device = await Windows.Devices.Bluetooth.BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress);
                if (device == null)
                {
                    Logger.Log($"[电量] BLE 设备不存在或不可用");
                    return -1;
                }

                Logger.Log($"[电量] BLE 设备已连接: {device.Name}");

                // 获取 Battery Service
                var servicesResult = await device.GetGattServicesForUuidAsync(BATTERY_SERVICE_UUID);
                if (servicesResult.Status != Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success ||
                    servicesResult.Services.Count == 0)
                {
                    Logger.Log($"[电量] 未找到 Battery Service: Status={servicesResult.Status}");
                    device.Dispose();
                    return -1;
                }

                var batteryService = servicesResult.Services[0];
                Logger.Log($"[电量] 找到 Battery Service");
                
                // 获取 Battery Level Characteristic
                var charsResult = await batteryService.GetCharacteristicsForUuidAsync(BATTERY_LEVEL_CHARACTERISTIC_UUID);
                if (charsResult.Status != Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success ||
                    charsResult.Characteristics.Count == 0)
                {
                    Logger.Log($"[电量] 未找到 Battery Level Characteristic: Status={charsResult.Status}");
                    device.Dispose();
                    return -1;
                }

                var batteryChar = charsResult.Characteristics[0];
                Logger.Log($"[电量] 找到 Battery Level Characteristic");

                // 读取电量值
                var readResult = await batteryChar.ReadValueAsync();
                if (readResult.Status != Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success)
                {
                    Logger.Log($"[电量] 读取电量值失败: Status={readResult.Status}");
                    device.Dispose();
                    return -1;
                }

                var reader = Windows.Storage.Streams.DataReader.FromBuffer(readResult.Value);
                byte batteryLevel = reader.ReadByte();

                device.Dispose();
                return batteryLevel;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[电量] BLE GATT 电量读取异常: {ex.Message}", ex);
                return -1;
            }
        }

        /// <summary>
        /// 批量获取设备电量
        /// </summary>
        public async Task<Dictionary<string, int>> GetBatteryLevelsAsync(IEnumerable<string> macAddresses)
        {
            var results = new Dictionary<string, int>();
            
            foreach (var mac in macAddresses)
            {
                int level = await GetBatteryLevelAsync(mac);
                results[mac] = level;
            }

            return results;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}