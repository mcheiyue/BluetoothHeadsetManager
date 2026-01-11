# 蓝牙耳机管理器 (Bluetooth Headset Manager)

一个轻量级的 Windows 系统托盘工具，用于快速连接/断开蓝牙耳机并实时显示电量。

## 项目特点

- ✅ **轻量级**：可执行文件 < 10MB，内存占用 < 30MB
- ⚡ **快速响应**：连接操作 < 3秒
- 🔋 **电量显示**：实时显示耳机电量
- 🎯 **一键操作**：托盘图标一键连接/断开
- 🔧 **易于扩展**：模块化设计，便于功能扩展

## 技术栈

- **框架**: .NET 7 + Windows Forms
- **语言**: C#
- **开发工具**: Visual Studio Code
- **目标平台**: Windows 10/11

## 系统要求

- Windows 10 1809 或更高版本
- .NET 7.0 Runtime（或 SDK）
- 蓝牙适配器

## 开发环境配置

请参阅 [`docs/setup-guide.md`](docs/setup-guide.md) 获取详细的环境配置说明。

### 快速开始

1. 克隆仓库
```bash
git clone <repository-url>
cd BluetoothHeadsetManager
```

2. 安装依赖
```bash
dotnet restore
```

3. 编译项目
```bash
dotnet build
```

4. 运行项目
```bash
dotnet run --project src/BluetoothHeadsetManager/BluetoothHeadsetManager.csproj
```

5. 调试（在 VS Code 中）
- 按 `F5` 启动调试
- 或使用 `Ctrl + F5` 运行（不调试）

## 项目结构

```
BluetoothHeadsetManager/
├── src/
│   └── BluetoothHeadsetManager/      # 主项目
│       ├── UI/                       # 用户界面层
│       ├── Core/                     # 业务逻辑层
│       ├── Bluetooth/                # 蓝牙API封装
│       ├── Models/                   # 数据模型
│       └── Utils/                    # 工具类
├── docs/                             # 文档
├── plans/                            # 规划文档
├── .vscode/                          # VS Code 配置
└── README.md
```

## 构建发布版本

```bash
# 发布单文件可执行程序
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true

# 输出位置
# src/BluetoothHeadsetManager/bin/Release/net7.0-windows/win-x64/publish/
```

## 功能路线图

### v1.0 (当前开发中)
- [x] 项目环境准备
- [ ] 系统托盘UI
- [ ] 蓝牙设备扫描
- [ ] 设备连接/断开
- [ ] 电量监控
- [ ] 配置管理

### v2.0 (计划中)
- [ ] 多设备管理
- [ ] 音频路由控制
- [ ] 蓝牙编码格式显示

## 贡献

欢迎贡献代码和提出建议！

## 许可证

[MIT License](LICENSE)

## 相关文档

- [技术方案](plans/bluetooth-headset-manager.md)
- [实施计划](plans/implementation-plan.md)
- [VS Code 开发指南](plans/vscode-development-guide.md)
- [环境配置指南](docs/setup-guide.md)