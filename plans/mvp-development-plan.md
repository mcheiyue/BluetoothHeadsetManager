# 蓝牙耳机管理器 MVP 开发规划

## 一、可直接复用的开源代码分析

### 1. GalaxyBudsClient - 蓝牙服务 (C# / 32feet)
**文件**: [`code/GalaxyBudsClient/GalaxyBudsClient.Platform.Windows/Bluetooth/BluetoothService.cs`](code/GalaxyBudsClient/GalaxyBudsClient.Platform.Windows/Bluetooth/BluetoothService.cs)

可复用功能：
- ✅ 使用 `InTheHand.Net.Bluetooth` (32feet) 连接蓝牙设备
- ✅ 设备发现和枚举 `GetDevicesAsync()` 方法
- ✅ RFCOMM 连接管理
- ✅ 设备进入范围自动重连检测

关键代码段：
```csharp
// 第262-290行：设备枚举
public async Task<BluetoothDevice[]> GetDevicesAsync()
{
    var devs = await Task.Factory.FromAsync(
        (callback, stateObject) => _client.BeginDiscoverDevices(20, true, true, false, false, callback, stateObject),
        _client.EndDiscoverDevices, null);
    // ...
}

// 第147-225行：连接逻辑
public async Task ConnectAsync(string macAddress, string uuid, CancellationToken cancelToken)
{
    _client.Connect(addr, new Guid(uuid));
    // ...
}
```

### 2. GalaxyBudsClient - 热键注册 (C# / Win32)
**文件**: [`code/GalaxyBudsClient/GalaxyBudsClient.Platform.Windows/Impl/HotkeyReceiver.cs`](code/GalaxyBudsClient/GalaxyBudsClient.Platform.Windows/Impl/HotkeyReceiver.cs)

可复用功能：
- ✅ 全局热键注册 (`RegisterHotKey` Win32 API)
- ✅ 热键事件处理
- ✅ 热键冲突检测

### 3. SoundSwitch - 音频切换 (C# / NAudio)
**文件**: [`code/SoundSwitch/SoundSwitch.Audio.Manager/AudioSwitcher.cs`](code/SoundSwitch/SoundSwitch.Audio.Manager/AudioSwitcher.cs)

可复用功能：
- ✅ 切换默认音频设备 `SwitchTo()` 方法
- ✅ 获取音频设备列表 `GetAudioEndpoints()` 方法
- ✅ 音量同步 `SetVolumeFromDefaultDevice()` 方法
- ✅ COM 线程管理

关键代码段：
```csharp
// 第81-102行：切换默认设备
public void SwitchTo(string deviceId, ERole role)
{
    ComThread.Invoke(() => {
        PolicyClient.SetDefaultEndpoint(deviceId, role);
    });
}
```

### 4. BlueGauge - 电量读取 (Rust → 需翻译为 C#)
**文件**: 
- [`code/BlueGauge/src/bluetooth/ble.rs`](code/BlueGauge/src/bluetooth/ble.rs) - BLE GATT 电量
- [`code/BlueGauge/src/bluetooth/btc.rs`](code/BlueGauge/src/bluetooth/btc.rs) - 经典蓝牙 PnP 电量

需要翻译为 C# 的逻辑：
- BLE: 读取 Battery Service (UUID: 0x180F) 的 Battery Level Characteristic
- Classic: 通过 CM_Get_DevNode_PropertyW 读取 DEVPKEY_Bluetooth_Battery

### 5. EarTrumpet - WPF 应用架构
**文件**: [`code/EarTrumpet/EarTrumpet/App.xaml.cs`](code/EarTrumpet/EarTrumpet/App.xaml.cs)

可参考架构：
- ✅ WPF 应用启动流程
- ✅ 托盘图标管理
- ✅ Flyout 窗口设计
- ✅ 单实例应用

---

## 二、需要删除的项目文件

```
删除目录/文件列表：
├── src/                          # 全部删除 (WinForms 代码)
├── plans/                        # 保留本文件，删除其他
│   ├── implementation-plan.md    # 删除
│   ├── ui-design.md              # 删除
│   ├── ui-technology-comparison.md # 删除
│   └── winforms-extensibility-analysis.md # 删除
├── docs/                         # 全部删除
│   └── setup-guide.md            # 删除
├── clone_repos.bat               # 保留 (用于更新开源代码)
└── .vscode/                      # 保留
```

---

## 三、MVP 开发阶段

### 阶段 1: 项目搭建 + 基础托盘 (MVP-1)
**目标**: 一个可运行的 WPF 托盘应用，右键菜单可退出

**创建文件**:
```
src/
└── BluetoothHeadsetManager/
    ├── BluetoothHeadsetManager.csproj
    ├── App.xaml
    ├── App.xaml.cs
    ├── MainWindow.xaml          # 隐藏的主窗口
    ├── MainWindow.xaml.cs
    └── Views/
        └── TrayIconView.xaml    # 托盘图标
```

**NuGet 包**:
```xml
<PackageReference Include="Hardcodet.NotifyIcon.Wpf" Version="1.1.0" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
```

---

### 阶段 2: 蓝牙设备扫描 (MVP-2)
**目标**: 托盘菜单显示已配对的蓝牙设备列表

**创建文件**:
```
src/BluetoothHeadsetManager/
├── Services/
│   └── BluetoothService.cs      # 复用 GalaxyBudsClient 代码
├── Models/
│   └── BluetoothDeviceInfo.cs
└── ViewModels/
    └── TrayViewModel.cs
```

**NuGet 包**:
```xml
<PackageReference Include="InTheHand.Net.Bluetooth" Version="4.1.1" />
```

**复用代码**: GalaxyBudsClient 的 `GetDevicesAsync()` 方法

---

### 阶段 3: 设备连接/断开 (MVP-3)
**目标**: 点击设备名即可连接或断开

**修改文件**:
- `Services/BluetoothService.cs` - 添加连接/断开方法
- `ViewModels/TrayViewModel.cs` - 添加连接命令

**复用代码**: GalaxyBudsClient 的 `ConnectAsync()` 和 `DisconnectAsync()` 方法

---

### 阶段 4: 电量显示 (MVP-4)
**目标**: 在设备名旁边显示电量百分比

**创建文件**:
```
src/BluetoothHeadsetManager/
├── Services/
│   └── BatteryService.cs        # 翻译自 BlueGauge
└── Interop/
    └── Cfgmgr32.cs              # PnP API 封装
```

**实现逻辑**:
1. BLE 设备: 通过 GATT Battery Service 读取
2. 经典蓝牙: 通过 CM_Get_DevNode_PropertyW 读取

---

### 阶段 5: 音频自动切换 (MVP-5)
**目标**: 连接耳机后自动切换 Windows 音频输出

**创建文件**:
```
src/BluetoothHeadsetManager/
└── Services/
    └── AudioSwitchService.cs    # 复用 SoundSwitch 代码
```

**NuGet 包**:
```xml
<PackageReference Include="NAudio" Version="2.2.1" />
```

**复用代码**: SoundSwitch 的 `AudioSwitcher` 类

---

## 四、最终项目结构

```
BluetoothHeadsetManager/
├── BluetoothHeadsetManager.sln
├── README.md
├── LICENSE
├── .gitignore
├── code/                         # 开源项目参考代码 (保留)
├── plans/
│   └── mvp-development-plan.md   # 本文件
└── src/
    └── BluetoothHeadsetManager/
        ├── BluetoothHeadsetManager.csproj
        ├── App.xaml
        ├── App.xaml.cs
        ├── MainWindow.xaml
        ├── MainWindow.xaml.cs
        ├── Models/
        │   ├── BluetoothDeviceInfo.cs
        │   └── AppConfig.cs
        ├── ViewModels/
        │   ├── TrayViewModel.cs
        │   └── MainViewModel.cs
        ├── Views/
        │   ├── TrayIconView.xaml
        │   └── DeviceFlyout.xaml
        ├── Services/
        │   ├── BluetoothService.cs
        │   ├── BatteryService.cs
        │   ├── AudioSwitchService.cs
        │   └── ConfigService.cs
        └── Interop/
            ├── Cfgmgr32.cs
            └── User32.cs
```

---

## 五、代码复用映射表

| 功能 | 源项目 | 源文件 | 复用方式 |
|------|--------|--------|----------|
| 蓝牙设备枚举 | GalaxyBudsClient | `BluetoothService.cs:262-290` | 直接复制并适配 |
| 蓝牙连接 | GalaxyBudsClient | `BluetoothService.cs:147-225` | 直接复制并适配 |
| 设备断开 | GalaxyBudsClient | `BluetoothService.cs:229-247` | 直接复制并适配 |
| 热键注册 | GalaxyBudsClient | `HotkeyReceiver.cs` | 直接复制并适配 |
| 音频切换 | SoundSwitch | `AudioSwitcher.cs:81-102` | 直接复制并适配 |
| 获取音频设备 | SoundSwitch | `AudioSwitcher.cs:329-346` | 直接复制并适配 |
| BLE 电量读取 | BlueGauge | `ble.rs:107-153` | 翻译为 C# |
| 经典蓝牙电量 | BlueGauge | `btc.rs:246-278` | 翻译为 C# |
| WPF 应用架构 | EarTrumpet | `App.xaml.cs` | 参考架构设计 |

---

## 六、下一步行动

确认此规划后，切换到 **Code 模式** 执行以下操作：

1. **删除旧文件**
   - 删除 `src/` 目录
   - 删除 `plans/` 中除本文件外的其他文件
   - 删除 `docs/` 目录

2. **创建新 WPF 项目**
   - 使用 `dotnet new wpf` 创建项目
   - 配置 NuGet 包

3. **实现 MVP-1**
   - 托盘图标
   - 右键菜单（退出）

然后逐步迭代完成后续 MVP 阶段。