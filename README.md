# 蓝牙耳机管理器 (Bluetooth Headset Manager)

一个轻量级的 Windows 蓝牙耳机管理工具，支持快速连接/断开、电量显示、音频自动切换等功能。

## ✨ 功能特性

- 🎧 **快速连接/断开** - 托盘右键菜单一键操作，比系统自带快3倍
- 🔋 **电量显示** - 实时显示蓝牙设备电量（支持经典蓝牙和BLE设备）
- 🔊 **音频自动切换** - 连接后自动将音频输出切换到蓝牙设备
- ⌨️ **全局热键** - Ctrl+Shift+B 快速连接/断开，Ctrl+Shift+R 刷新设备列表
- 🖥️ **托盘常驻** - 最小化到系统托盘，不占用任务栏空间

## 🛠️ 技术栈

- **框架**: .NET 7 + WPF
- **UI**: Windows 原生托盘图标 (Hardcodet.NotifyIcon.Wpf)
- **MVVM**: CommunityToolkit.Mvvm
- **蓝牙**: InTheHand.Net.Bluetooth (32feet)
- **音频**: NAudio.CoreAudioApi

## 📦 安装

### 从源码编译

1. 确保已安装 [.NET 7 SDK](https://dotnet.microsoft.com/download/dotnet/7.0)
2. 克隆仓库：
   ```bash
   git clone https://github.com/yourusername/BluetoothHeadsetManager.git
   cd BluetoothHeadsetManager
   ```
3. 编译运行：
   ```bash
   cd src/BluetoothHeadsetManager
   dotnet run
   ```

## 🚀 使用方法

1. 启动程序后，图标会出现在系统托盘
2. 右键点击托盘图标，查看已配对的蓝牙设备列表
3. 点击设备名称即可连接/断开
4. 使用热键 `Ctrl+Shift+B` 快速操作第一个音频设备

## ⌨️ 快捷键

| 快捷键 | 功能 |
|--------|------|
| Ctrl+Shift+B | 连接/断开第一个音频设备 |
| Ctrl+Shift+R | 刷新设备列表 |

## 📁 项目结构

```
src/BluetoothHeadsetManager/
├── App.xaml(.cs)           # 应用程序入口
├── MainWindow.xaml(.cs)    # 主窗口（隐藏）
├── Models/
│   └── BluetoothDeviceInfo.cs   # 蓝牙设备信息模型
├── ViewModels/
│   └── TrayViewModel.cs         # 托盘视图模型
├── Services/
│   ├── BluetoothService.cs      # 蓝牙连接服务
│   ├── BatteryService.cs        # 电量读取服务
│   ├── AudioSwitchService.cs    # 音频切换服务
│   └── HotkeyService.cs         # 全局热键服务
└── Resources/
    └── app.ico                   # 应用程序图标
```

## 🔧 开发参考

本项目参考了以下开源项目：

- [32feet](https://github.com/inthehand/32feet) - 蓝牙通信库
- [BlueGauge](https://github.com/iKineticate/BlueGauge) - 蓝牙电量读取
- [SoundSwitch](https://github.com/Belphemur/SoundSwitch) - 音频设备切换
- [ToothTray](https://github.com/m2jean/ToothTray) - 托盘蓝牙管理
- [EarTrumpet](https://github.com/File-New-Project/EarTrumpet) - WPF UI 参考

## 📄 许可证

MIT License - 详见 [LICENSE](LICENSE) 文件

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

## 📋 待办事项

- [ ] 添加 WPF-UI 皮肤库实现 Win11 Fluent 风格
- [ ] 添加 LiveCharts 图表显示电量历史
- [ ] 添加设置页面配置热键
- [ ] 添加开机自启动选项
- [ ] 支持特定耳机的高级功能（如降噪模式）