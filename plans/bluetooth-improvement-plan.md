# 蓝牙耳机管理器改进计划

## 一、当前问题诊断

### 1. 连接/断开功能问题 🔴

**当前实现（错误）：**
```csharp
// BluetoothService.cs - 使用 RFCOMM Socket 连接
client.Connect(address, AudioSinkServiceClass);
```

**问题分析：**
- RFCOMM 是建立串口数据通道，不是让 Windows 系统"连接"蓝牙耳机
- 即使连接成功，耳机也不会出现在 Windows 音频设备中
- 断开操作更是无效 - 只是关闭了 BluetoothClient 对象

### 2. 设备枚举方式可优化

当前使用 `BluetoothClient.PairedDevices`，只能获取已配对设备的基本信息，无法：
- 区分经典蓝牙和 BLE 设备
- 获取连接状态变化事件
- 获取设备容器 ID（用于关联音频设备）

---

## 二、正确的实现方案

### 方案 A：ToothTray 方式（推荐）

使用 Windows Kernel Streaming API (`IKsControl`) 来控制蓝牙音频设备连接：

**核心技术：**
- `IMMDeviceEnumerator` - 枚举音频设备
- `IDeviceTopology` - 获取设备拓扑结构
- `IKsControl` - 控制蓝牙音频连接
- `KSPROPSETID_BtAudio` - 蓝牙音频属性集

**连接/断开命令：**
```
KSPROPERTY_ONESHOT_RECONNECT = 1  // 连接
KSPROPERTY_ONESHOT_DISCONNECT = 2 // 断开
```

**KSPROPSETID_BtAudio GUID：**
```
{7FA06C40-B8F6-4C7E-8556-E8C33A12E54D}
```

### 方案 B：BluetoothSetServiceState 方式（备选）

使用 Windows Bluetooth API 启用/禁用服务：

```csharp
[DllImport("bthprops.cpl")]
static extern int BluetoothSetServiceState(
    IntPtr hRadio,
    ref BLUETOOTH_DEVICE_INFO pbtdi,
    ref Guid pGuidService,
    uint dwServiceFlags  // BLUETOOTH_SERVICE_ENABLE = 1, BLUETOOTH_SERVICE_DISABLE = 0
);
```

**常用服务 GUID：**
- `AudioSink (A2DP)`: 0000110B-0000-1000-8000-00805F9B34FB
- `HandsFree (HFP)`: 0000111E-0000-1000-8000-00805F9B34FB
- `Headset`: 00001108-0000-1000-8000-00805F9B34FB

---

## 三、实现步骤

### 阶段 1：重构设备枚举

**目标**：使用 Windows Runtime API + Core Audio API 获取更详细的设备信息

**步骤：**
1. 创建 `BluetoothAudioEnumerator` 类
2. 使用 `IMMDeviceEnumerator` 枚举音频设备
3. 通过 `PKEY_Device_ContainerId` 关联蓝牙设备
4. 使用 `Windows.Devices.Bluetooth` 获取蓝牙设备详细信息

**关键代码参考**：
- ToothTray: `BluetoothAudioDevices.cpp` - 枚举蓝牙音频设备
- BlueGauge: `btc.rs` - 使用 `BluetoothDevice.GetDeviceSelectorFromPairingState`

### 阶段 2：实现正确的连接/断开

**方法 A - IKsControl（推荐）：**

```csharp
// P/Invoke 定义
[ComImport]
[Guid("28F54685-06FD-11D2-B27A-00A0C9223196")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IKsControl
{
    int KsProperty(
        ref KSPROPERTY Property,
        int PropertyLength,
        IntPtr PropertyData,
        int DataLength,
        out int BytesReturned);
    // ... 其他方法
}

[StructLayout(LayoutKind.Sequential)]
public struct KSPROPERTY
{
    public Guid Set;
    public uint Id;
    public uint Flags;
}

public static readonly Guid KSPROPSETID_BtAudio = 
    new Guid("7FA06C40-B8F6-4C7E-8556-E8C33A12E54D");

public const uint KSPROPERTY_ONESHOT_RECONNECT = 1;
public const uint KSPROPERTY_ONESHOT_DISCONNECT = 2;
public const uint KSPROPERTY_TYPE_GET = 1;
```

**方法 B - BluetoothSetServiceState（备选）：**

```csharp
[DllImport("bthprops.cpl")]
public static extern int BluetoothSetServiceState(
    IntPtr hRadio,
    ref BLUETOOTH_DEVICE_INFO pbtdi,
    ref Guid pGuidService,
    uint dwServiceFlags);

// 使用
public void Connect(BluetoothDeviceInfo device)
{
    Guid a2dpSink = new Guid("0000110B-0000-1000-8000-00805F9B34FB");
    BluetoothSetServiceState(IntPtr.Zero, ref device._info, ref a2dpSink, 1);
}

public void Disconnect(BluetoothDeviceInfo device)
{
    Guid a2dpSink = new Guid("0000110B-0000-1000-8000-00805F9B34FB");
    BluetoothSetServiceState(IntPtr.Zero, ref device._info, ref a2dpSink, 0);
}
```

### 阶段 3：设备状态监控

**使用 Windows Runtime API：**

```csharp
// 监控经典蓝牙设备连接状态
var btcDevice = await BluetoothDevice.FromBluetoothAddressAsync(address);
btcDevice.ConnectionStatusChanged += (sender, args) => {
    bool connected = sender.ConnectionStatus == BluetoothConnectionStatus.Connected;
};

// 监控 BLE 设备连接状态
var bleDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(address);
bleDevice.ConnectionStatusChanged += (sender, args) => { ... };
```

### 阶段 4：改进电量读取

**参考 BlueGauge 的实现：**

1. **经典蓝牙电量** - 通过 PnP API：
```csharp
// DEVPKEY_Bluetooth_Battery = {104EA319-6EE2-4701-BD47-8DDBF425BBE5}, 2
CM_Locate_DevNodeW(&devnode, instanceId, CM_LOCATE_DEVNODE_NORMAL);
CM_Get_DevNode_PropertyW(devnode, &DEVPKEY_BLUETOOTH_BATTERY, &propType, &battery, &size, 0);
```

2. **BLE 电量** - 通过 GATT Battery Service：
```csharp
var batteryService = await device.GetGattServicesForUuidAsync(GattServiceUuids.Battery);
var batteryLevel = await service.GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel);
var result = await characteristic.ReadValueAsync();
```

---

## 四、文件修改清单

### 需要修改的文件：

1. **`Services/BluetoothService.cs`** - 完全重写
   - 替换 RFCOMM 连接为 IKsControl 或 BluetoothSetServiceState
   - 使用 IMMDeviceEnumerator 枚举蓝牙音频设备
   
2. **`Services/BatteryService.cs`** - 优化
   - 增加周期性轮询和事件订阅
   
3. **`Models/BluetoothDeviceInfo.cs`** - 扩展
   - 添加 ContainerId 属性
   - 添加 DeviceType (Classic/BLE) 属性
   
4. **`ViewModels/TrayViewModel.cs`** - 更新
   - 支持实时状态更新

### 需要新增的文件：

1. **`Interop/KsControl.cs`** - IKsControl COM 接口定义
2. **`Interop/BluetoothApi.cs`** - BluetoothSetServiceState 等 P/Invoke
3. **`Interop/CoreAudioInterop.cs`** - IMMDeviceEnumerator 等 COM 接口
4. **`Services/DeviceWatcherService.cs`** - 设备状态监控服务

---

## 五、技术风险

1. **IKsControl 方式的兼容性**
   - 需要音频设备已在系统中注册
   - 可能需要管理员权限

2. **BluetoothSetServiceState 的限制**
   - 已被标记为过时 API
   - 在某些 Windows 版本上可能不工作

3. **Windows Runtime API 的依赖**
   - 需要 Windows 10 1703 以上版本
   - 需要在项目中启用 Windows Runtime 支持

---

## 六、优先级排序

### 第一优先级（必须修复）
- [x] ~~设备列表显示~~ 
- [ ] **连接/断开功能** ← 当前焦点

### 第二优先级（重要改进）
- [ ] 电量读取优化
- [ ] 设备状态实时监控

### 第三优先级（体验增强）
- [ ] 音频自动切换优化
- [ ] UI 皮肤美化
- [ ] 设置页面

---

## 七、参考资料

### 开源项目
- ToothTray: `BluetoothAudioDevices.cpp` - IKsControl 连接实现
- BlueGauge: `btc.rs` / `ble.rs` - Windows Runtime API 使用
- 32feet: `BluetoothDeviceInfo.win32.cs` - BluetoothSetServiceState

### 官方文档
- [IMMDeviceEnumerator](https://docs.microsoft.com/en-us/windows/win32/api/mmdeviceapi/nn-mmdeviceapi-immdeviceenumerator)
- [IKsControl](https://docs.microsoft.com/en-us/windows-hardware/drivers/stream/ksproperty-structure)
- [BluetoothSetServiceState](https://docs.microsoft.com/en-us/windows/win32/api/bluetoothapis/nf-bluetoothapis-bluetoothsetservicestate)