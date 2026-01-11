# VS Code 开发 C# .NET 项目指南

## 完全可行！使用 VS Code 开发 C# 项目的优势

使用 VS Code 开发 C# + .NET 项目**完全可行**，而且有以下优势：

### ✅ 优势
1. **轻量级**：VS Code 启动快，占用资源少
2. **跨平台**：Windows/Mac/Linux 都能用
3. **现代化**：界面简洁，扩展丰富
4. **免费开源**：完全免费
5. **集成终端**：命令行操作方便

### ⚠️ 与 Visual Studio 的区别
- VS Code 需要手动配置一些东西（但配置一次即可）
- 可视化设计器支持较弱（WinForms 设计器）
- 调试功能完善，但稍逊于 VS

### 💡 解决方案
我们采用**代码优先**的方式开发 UI，不依赖可视化设计器，这样 VS Code 完全够用！

---

## 一、环境准备

### 1.1 安装 .NET SDK

1. 下载 .NET 7 SDK：https://dotnet.microsoft.com/download/dotnet/7.0
2. 安装完成后，验证安装：
   ```powershell
   dotnet --version
   # 应该显示类似：7.0.x
   ```

### 1.2 安装 VS Code 扩展

在 VS Code 中安装以下扩展：

1. **C# (ms-dotnettools.csharp)**
   - 提供 C# 语法高亮、智能感知、调试等功能
   - 必装！

2. **C# Dev Kit (ms-dotnettools.csdevkit)** [可选但推荐]
   - 提供项目管理、测试资源管理器等
   - 增强开发体验

3. **NuGet Package Manager (jmrog.vscode-nuget-package-manager)** [可选]
   - 图形化管理 NuGet 包
   - 方便添加依赖

### 1.3 配置 VS Code

创建 `.vscode/settings.json`：
```json
{
  "omnisharp.useModernNet": true,
  "dotnet.defaultSolution": "BluetoothHeadsetManager.sln",
  "files.exclude": {
    "**/bin": true,
    "**/obj": true
  }
}
```

---

## 二、项目创建流程（VS Code 方式）

### 2.1 使用 .NET CLI 创建项目

```powershell
# 1. 创建项目目录
mkdir BluetoothHeadsetManager
cd BluetoothHeadsetManager

# 2. 创建 WinForms 项目
dotnet new winforms -n BluetoothHeadsetManager -f net7.0-windows

# 3. 创建解决方案并添加项目
dotnet new sln
dotnet sln add BluetoothHeadsetManager/BluetoothHeadsetManager.csproj

# 4. 在 VS Code 中打开
code .
```

### 2.2 项目文件配置

编辑 `BluetoothHeadsetManager.csproj`：
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net7.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <ApplicationIcon>Resources\icon.ico</ApplicationIcon>
    
    <!-- 单文件发布配置 -->
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PublishTrimmed>true</PublishTrimmed>
    <TrimMode>link</TrimMode>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
  </PropertyGroup>

  <ItemGroup>
    <!-- Windows Runtime API -->
    <PackageReference Include="Microsoft.Windows.SDK.Contracts" Version="10.0.22621.38" />
    
    <!-- 蓝牙库（如果需要） -->
    <PackageReference Include="InTheHand.Net.Bluetooth" Version="4.0.40" />
    
    <!-- JSON 配置 -->
    <PackageReference Include="System.Text.Json" Version="7.0.0" />
  </ItemGroup>

</Project>
```

---

## 三、VS Code 开发工作流

### 3.1 编译和运行

#### 方式1：使用集成终端
```powershell
# 编译
dotnet build

# 运行
dotnet run

# 发布（生成单文件可执行文件）
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

#### 方式2：使用 F5 调试
1. 按 `F5` 或点击"运行和调试"
2. 首次运行会自动生成 `.vscode/launch.json`
3. 之后可以设置断点、查看变量等

### 3.2 调试配置

`.vscode/launch.json`：
```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": ".NET Core Launch (console)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/BluetoothHeadsetManager/bin/Debug/net7.0-windows/BluetoothHeadsetManager.exe",
      "args": [],
      "cwd": "${workspaceFolder}/BluetoothHeadsetManager",
      "console": "internalConsole",
      "stopAtEntry": false
    }
  ]
}
```

`.vscode/tasks.json`：
```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "build",
      "command": "dotnet",
      "type": "process",
      "args": [
        "build",
        "${workspaceFolder}/BluetoothHeadsetManager/BluetoothHeadsetManager.csproj",
        "/property:GenerateFullPaths=true",
        "/consoleloggerparameters:NoSummary"
      ],
      "problemMatcher": "$msCompile"
    },
    {
      "label": "publish",
      "command": "dotnet",
      "type": "process",
      "args": [
        "publish",
        "${workspaceFolder}/BluetoothHeadsetManager/BluetoothHeadsetManager.csproj",
        "-c",
        "Release",
        "-r",
        "win-x64",
        "--self-contained",
        "true",
        "/p:PublishSingleFile=true"
      ],
      "problemMatcher": "$msCompile"
    }
  ]
}
```

### 3.3 包管理

#### 添加 NuGet 包
```powershell
# 进入项目目录
cd BluetoothHeadsetManager

# 添加包
dotnet add package Microsoft.Windows.SDK.Contracts
dotnet add package InTheHand.Net.Bluetooth

# 查看已安装的包
dotnet list package

# 更新包
dotnet add package PackageName --version x.x.x
```

---

## 四、代码优先的 UI 开发

### 4.1 为什么不用设计器？

VS Code 没有 WinForms 可视化设计器，但这反而是**优势**：

1. **代码更清晰**：所有 UI 代码在一个地方，易于维护
2. **版本控制友好**：不会产生大量自动生成的代码
3. **更灵活**：动态创建 UI 更方便
4. **适合托盘应用**：我们的应用主要是托盘图标，不需要复杂窗体

### 4.2 系统托盘 UI 示例

```csharp
using System;
using System.Drawing;
using System.Windows.Forms;

namespace BluetoothHeadsetManager.UI
{
    public class TrayApplication : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private ContextMenuStrip contextMenu;

        public TrayApplication()
        {
            InitializeTrayIcon();
        }

        private void InitializeTrayIcon()
        {
            // 创建右键菜单
            contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("连接耳机", null, OnConnect);
            contextMenu.Items.Add("断开连接", null, OnDisconnect);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("设置", null, OnSettings);
            contextMenu.Items.Add("退出", null, OnExit);

            // 创建托盘图标
            trayIcon = new NotifyIcon()
            {
                Icon = SystemIcons.Application, // 临时使用系统图标
                ContextMenuStrip = contextMenu,
                Visible = true,
                Text = "蓝牙耳机管理器 - 未连接"
            };

            // 左键单击事件
            trayIcon.Click += (s, e) =>
            {
                if (((MouseEventArgs)e).Button == MouseButtons.Left)
                {
                    OnToggleConnection(s, e);
                }
            };
        }

        private void OnConnect(object? sender, EventArgs e)
        {
            MessageBox.Show("连接耳机功能");
        }

        private void OnDisconnect(object? sender, EventArgs e)
        {
            MessageBox.Show("断开连接功能");
        }

        private void OnSettings(object? sender, EventArgs e)
        {
            MessageBox.Show("设置功能");
        }

        private void OnToggleConnection(object? sender, EventArgs e)
        {
            // 切换连接状态
        }

        private void OnExit(object? sender, EventArgs e)
        {
            trayIcon.Visible = false;
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                trayIcon?.Dispose();
                contextMenu?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
```

### 4.3 程序入口

```csharp
// Program.cs
using System;
using System.Windows.Forms;
using BluetoothHeadsetManager.UI;

namespace BluetoothHeadsetManager
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplication());
        }
    }
}
```

---

## 五、常用 VS Code 快捷键

### 5.1 编码相关
- `Ctrl + .` - 快速修复（Quick Fix）
- `F12` - 跳转到定义
- `Shift + F12` - 查找所有引用
- `Ctrl + Space` - 触发智能感知
- `Ctrl + Shift + O` - 跳转到符号

### 5.2 调试相关
- `F5` - 开始调试
- `Ctrl + F5` - 运行（不调试）
- `F9` - 设置/取消断点
- `F10` - 单步跳过
- `F11` - 单步进入

### 5.3 终端相关
- `Ctrl + `` ` `` - 打开/关闭集成终端
- `Ctrl + Shift + `` ` `` - 新建终端

---

## 六、开发建议

### 6.1 项目结构建议

```
BluetoothHeadsetManager/
├── Program.cs                      # 入口
├── UI/
│   ├── TrayApplication.cs         # 托盘应用
│   └── SettingsForm.cs            # 设置窗体（需要时手写）
├── Core/
│   ├── DeviceManager.cs
│   ├── ConnectionManager.cs
│   └── BatteryMonitor.cs
├── Models/
│   ├── DeviceInfo.cs
│   └── AppConfig.cs
└── Resources/
    ├── connected.ico
    └── disconnected.ico
```

### 6.2 开发流程

1. **VS Code 编写代码**
   - 利用智能感知和代码补全
   - 快速修复错误

2. **终端编译运行**
   ```powershell
   dotnet build
   dotnet run
   ```

3. **F5 调试**
   - 设置断点
   - 检查变量
   - 单步执行

4. **发布**
   ```powershell
   dotnet publish -c Release -r win-x64 --self-contained
   ```

### 6.3 VS Code 扩展推荐

#### 必装
- **C#** - C# 语言支持
- **C# Dev Kit** - 项目管理增强

#### 推荐
- **GitLens** - Git 增强
- **Error Lens** - 行内显示错误
- **Better Comments** - 注释高亮
- **Todo Tree** - TODO 管理
- **Code Spell Checker** - 拼写检查

---

## 七、常见问题解决

### 7.1 智能感知不工作

**解决方案：**
```powershell
# 重启 OmniSharp 服务
Ctrl + Shift + P -> "OmniSharp: Restart OmniSharp"

# 或者重新加载窗口
Ctrl + Shift + P -> "Developer: Reload Window"
```

### 7.2 无法调试

**检查清单：**
1. 是否安装了 C# 扩展？
2. `.vscode/launch.json` 配置是否正确？
3. 项目是否成功编译？

### 7.3 发布后文件太大

**优化方案：**
```xml
<!-- 在 .csproj 中添加 -->
<PropertyGroup>
  <PublishTrimmed>true</PublishTrimmed>
  <TrimMode>link</TrimMode>
  <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
</PropertyGroup>
```

---

## 八、VS Code vs Visual Studio 对比

| 功能 | VS Code | Visual Studio |
|------|---------|---------------|
| 代码编辑 | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| 智能感知 | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| 调试功能 | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| 可视化设计器 | ❌ | ⭐⭐⭐⭐⭐ |
| 启动速度 | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ |
| 资源占用 | ⭐⭐⭐⭐⭐ | ⭐⭐ |
| 跨平台 | ⭐⭐⭐⭐⭐ | ⭐⭐ |
| 免费 | ✅ | 社区版免费 |

**结论：** 对于我们的项目（托盘应用，代码优先），VS Code **完全够用**！

---

## 九、快速开始清单

- [ ] 安装 .NET 7 SDK
- [ ] 安装 VS Code
- [ ] 安装 C# 扩展
- [ ] 创建项目：`dotnet new winforms`
- [ ] 打开 VS Code：`code .`
- [ ] 配置 `.vscode/launch.json` 和 `tasks.json`
- [ ] 开始编码！

---

## 十、总结

### ✅ VS Code 开发 C# 的优势
1. 轻量快速
2. 现代化界面
3. 强大的扩展生态
4. 免费开源
5. 完全满足我们的项目需求

### 💡 关键点
- 使用 .NET CLI 创建和管理项目
- 代码优先开发 UI（不依赖设计器）
- 利用 VS Code 的调试功能
- 熟悉常用快捷键提高效率

### 🎯 最佳实践
- 代码结构清晰
- 充分利用智能感知
- 善用集成终端
- 定期提交代码到 Git

**现在可以放心使用 VS Code 开发了！**