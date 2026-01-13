# 蓝牙设备连接实现架构

## 一、整体设计

### 设计原则
1. **优先服务蓝牙音频设备** - 使用 IKsControl 方式
2. **兼容普通蓝牙设备** - 使用 BluetoothSetServiceState 方式
3. **分层架构** - 设备枚举层、连接控制层、UI 层分离

### 架构图

```
┌─────────────────────────────────────────────────────────────┐
│                      UI Layer                               │
│                    TrayViewModel                            │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                  Service Layer                              │
│  ┌─────────────────────┐  ┌────────────────────────────┐   │
│  │ BluetoothAudioService│  │ BluetoothGeneralService    │   │
│  │  - IKsControl        │  │  - BluetoothSetServiceState│   │
│  │  - 音频设备专用      │  │  - 通用蓝牙设备            │   │
│  └─────────────────────┘  └────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                  Interop Layer                              │
│  ┌───────────────┐ ┌───────────────┐ ┌─────────────────┐   │
│  │ CoreAudioInterop│ │ KsControlInterop│ │BluetoothApiInterop│ │
│  │ IMMDevice*     │ │ IKsControl    │ │ BluetoothSet*   │   │
│  └───────────────┘ └───────────────┘ └─────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                  Windows API                                │
│    mmdeviceapi.h  │  ks.h / ksmedia.h  │  bthprops.cpl    │
└─────────────────────────────────────────────────────────────┘
```

---

## 二、需要创建的文件

### 1. Interop 层

**`Interop/CoreAudioInterop.cs`**
```csharp
// COM 接口定义
- IMMDeviceEnumerator
- IMMDeviceCollection
- IMMDevice
- IMMEndpoint
- IDeviceTopology
- IConnector
- IPart
- IPropertyStore

// GUID 常量
- CLSID_MMDeviceEnumerator
- IID_IMMDeviceEnumerator
- PKEY_Device_FriendlyName
- PKEY_Device_ContainerId
```

**`Interop/KsControlInterop.cs`**
```csharp
// COM 接口
- IKsControl

// 结构体
- KSPROPERTY

// 常量
- KSPROPSETID_BtAudio = {7FA06C40-B8F6-4C7E-8556-E8C33A12E54D}
- KSPROPERTY_ONESHOT_RECONNECT = 1
- KSPROPERTY_ONESHOT_DISCONNECT = 2
- KSPROPERTY_TYPE_GET = 1
```

**`Interop/BluetoothApiInterop.cs`**
```csharp
// P/Invoke
- BluetoothSetServiceState
- BluetoothGetDeviceInfo
- BluetoothEnumerateInstalledServices

// 结构体
- BLUETOOTH_DEVICE_INFO

// 常量
- 音频服务 GUID
```

### 2. Service 层

**`Services/BluetoothAudioService.cs`** (新建)
```csharp
public class BluetoothAudioService : IDisposable
{
    // 枚举蓝牙音频设备
    public List<BluetoothAudioDevice> EnumerateAudioDevices();
    
    // 连接音频设备
    public bool Connect(BluetoothAudioDevice device);
    
    // 断开音频设备
    public bool Disconnect(BluetoothAudioDevice device);
}
```

**`Services/BluetoothGeneralService.cs`** (新建)
```csharp
public class BluetoothGeneralService : IDisposable
{
    // 连接普通蓝牙设备
    public bool Connect(BluetoothDeviceInfo device);
    
    // 断开普通蓝牙设备
    public bool Disconnect(BluetoothDeviceInfo device);
}
```

**`Services/BluetoothService.cs`** (重构)
```csharp
public class BluetoothService : IDisposable
{
    private readonly BluetoothAudioService _audioService;
    private readonly BluetoothGeneralService _generalService;
    
    // 统一接口
    public async Task<bool> ConnectAsync(BluetoothDeviceInfo device)
    {
        if (device.IsAudioDevice)
            return _audioService.Connect(device);
        else
            return _generalService.Connect(device);
    }
}
```

### 3. Model 层

**`Models/BluetoothAudioDevice.cs`** (新建)
```csharp
public class BluetoothAudioDevice
{
    public string Name { get; set; }
    public Guid ContainerId { get; set; }
    public bool IsConnected { get; set; }
    public List<IntPtr> KsControls { get; set; }
}
```

---

## 三、IKsControl 连接实现详解

### 步骤 1：枚举蓝牙音频设备

```csharp
// 1. 创建设备枚举器
var enumerator = new MMDeviceEnumerator();

// 2. 枚举所有渲染设备
var devices = enumerator.EnumAudioEndpoints(EDataFlow.eRender, DEVICE_STATEMASK_ALL);

// 3. 遍历每个设备
foreach (var device in devices)
{
    // 4. 获取设备属性
    var propertyStore = device.OpenPropertyStore(STGM_READ);
    var containerId = propertyStore.GetValue(PKEY_Device_ContainerId);
    
    // 5. 获取设备拓扑
    var topology = device.Activate<IDeviceTopology>();
    
    // 6. 遍历连接器
    foreach (var connector in topology.GetConnectors())
    {
        // 7. 获取连接的设备
        var connectedTo = connector.GetConnectedTo();
        var otherTopology = connectedTo.GetTopologyObject();
        var otherDeviceId = otherTopology.GetDeviceId();
        
        // 8. 检查是否是蓝牙设备
        if (otherDeviceId.StartsWith("{2}.\\\\?\\bth"))
        {
            // 9. 获取 IKsControl
            var otherDevice = enumerator.GetDevice(otherDeviceId);
            var ksControl = otherDevice.Activate<IKsControl>();
            
            // 10. 保存到集合
            audioDevices.Add(new BluetoothAudioDevice {
                ContainerId = containerId,
                KsControls = { ksControl }
            });
        }
    }
}
```

### 步骤 2：连接/断开

```csharp
public bool Connect(BluetoothAudioDevice device)
{
    var property = new KSPROPERTY {
        Set = KSPROPSETID_BtAudio,
        Id = KSPROPERTY_ONESHOT_RECONNECT,
        Flags = KSPROPERTY_TYPE_GET
    };
    
    foreach (var ksControl in device.KsControls)
    {
        int bytesReturned;
        var hr = ksControl.KsProperty(ref property, Marshal.SizeOf(property), 
                                       IntPtr.Zero, 0, out bytesReturned);
        if (hr < 0) return false;
    }
    return true;
}

public bool Disconnect(BluetoothAudioDevice device)
{
    var property = new KSPROPERTY {
        Set = KSPROPSETID_BtAudio,
        Id = KSPROPERTY_ONESHOT_DISCONNECT,
        Flags = KSPROPERTY_TYPE_GET
    };
    
    // ... 同上
}
```

---

## 四、BluetoothSetServiceState 实现详解

### 步骤 1：获取设备信息

```csharp
// 从 32feet 获取的设备信息
var bluetoothClient = new BluetoothClient();
var pairedDevices = bluetoothClient.PairedDevices;

foreach (var dev in pairedDevices)
{
    var info = new BLUETOOTH_DEVICE_INFO {
        dwSize = Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>(),
        Address = dev.DeviceAddress
    };
    
    // 刷新设备信息
    BluetoothGetDeviceInfo(IntPtr.Zero, ref info);
}
```

### 步骤 2：连接/断开

```csharp
public bool Connect(BluetoothDeviceInfo device)
{
    var services = new[] {
        BluetoothService.AudioSink,      // A2DP
        BluetoothService.Handsfree,      // HFP
        BluetoothService.Headset         // HSP
    };
    
    foreach (var serviceGuid in services)
    {
        var result = BluetoothSetServiceState(
            IntPtr.Zero,
            ref device._info,
            ref serviceGuid,
            BLUETOOTH_SERVICE_ENABLE  // 1
        );
        
        if (result == 0) return true;
    }
    return false;
}

public bool Disconnect(BluetoothDeviceInfo device)
{
    // 同上，使用 BLUETOOTH_SERVICE_DISABLE (0)
}
```

---

## 五、整合到 UI

### TrayViewModel 修改

```csharp
public async Task ToggleDeviceConnectionAsync(BluetoothDeviceInfo device)
{
    bool success;
    
    if (device.IsConnected)
    {
        success = await _bluetoothService.DisconnectAsync(device);
        if (success && AutoSwitchAudio)
        {
            // 断开后可能需要切换回默认音频设备
        }
    }
    else
    {
        success = await _bluetoothService.ConnectAsync(device);
        if (success && AutoSwitchAudio)
        {
            // 连接成功后自动切换音频
            await _audioService.SwitchToBluetoothDevice(device.Name);
        }
    }
    
    // 刷新设备列表
    await RefreshDevicesAsync();
}
```

---

## 六、注意事项

### 1. COM 初始化
```csharp
// 在应用启动时
CoInitializeEx(IntPtr.Zero, COINIT_MULTITHREADED);
```

### 2. 异步处理
- IKsControl 操作可能需要几秒钟才能完成
- 应在后台线程执行，避免阻塞 UI

### 3. 错误处理
- 设备可能不在范围内
- 设备可能被其他应用占用
- 驱动程序可能不支持 IKsControl

### 4. 设备状态同步
- 连接/断开后需要等待系统完成状态更新
- 可能需要轮询或使用事件通知

---

## 七、测试计划

### 测试用例

1. **蓝牙耳机连接测试**
   - 已配对耳机，点击连接
   - 验证耳机出现在 Windows 音频设备中

2. **蓝牙耳机断开测试**
   - 已连接耳机，点击断开
   - 验证耳机从 Windows 音频设备中消失

3. **普通蓝牙设备连接测试**
   - 蓝牙键盘/鼠标等
   - 使用 BluetoothSetServiceState 方式

4. **边界条件测试**
   - 设备不在范围内
   - 设备已被其他应用连接
   - 快速点击连接/断开

---

## 八、实现顺序

1. ✅ 创建 `Interop/CoreAudioInterop.cs`
2. ✅ 创建 `Interop/KsControlInterop.cs`
3. ✅ 创建 `Interop/BluetoothApiInterop.cs`
4. ✅ 创建 `Models/BluetoothAudioDevice.cs`
5. ✅ 创建 `Services/BluetoothAudioService.cs`
6. ✅ 创建 `Services/BluetoothGeneralService.cs`
7. ✅ 重构 `Services/BluetoothService.cs`
8. ✅ 更新 `ViewModels/TrayViewModel.cs`
9. ✅ 测试和调试