# 蓝牙耳机管理器 - 环境配置指南

## 第一步：安装 .NET 7 SDK

### 1. 下载 .NET 7 SDK

访问官方下载页面：
- **官方地址**: https://dotnet.microsoft.com/download/dotnet/7.0
- **选择版本**: .NET 7.0 SDK（不是 Runtime）
- **选择平台**: Windows x64 Installer

### 2. 安装步骤

1. 下载完成后，双击运行安装程序
2. 按照向导提示完成安装（默认选项即可）
3. 安装完成后，**重启命令行窗口**（或重启 VS Code）

### 3. 验证安装

打开新的命令行窗口，执行：

```powershell
dotnet --version
```

应该显示类似：`7.0.x`

如果仍然提示找不到命令，请：
1. 检查环境变量 `PATH` 是否包含：`C:\Program Files\dotnet\`
2. 重启计算机

---

## 第二步：安装 VS Code 扩展

在 VS Code 中按 `Ctrl + Shift + X` 打开扩展面板，搜索并安装：

### 必装扩展

1. **C# (ms-dotnettools.csharp)**
   - 提供 C# 语法高亮、智能感知、调试功能
   - 发布者：Microsoft

2. **C# Dev Kit (ms-dotnettools.csdevkit)** [推荐]
   - 提供项目管理、测试资源管理器
   - 发布者：Microsoft

### 可选扩展

3. **NuGet Package Manager (jmrog.vscode-nuget-package-manager)**
   - 图形化管理 NuGet 包
   
4. **GitLens (eamodio.gitlens)**
   - Git 增强功能

5. **Error Lens (usernamehw.errorlens)**
   - 行内显示错误信息

---

## 第三步：验证环境

安装完成后，在项目根目录执行以下命令验证：

```powershell
# 验证 .NET SDK
dotnet --version

# 验证 .NET 运行时
dotnet --list-runtimes

# 验证 SDK 列表
dotnet --list-sdks
```

预期输出示例：
```
7.0.15
Microsoft.NETCore.App 7.0.15 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
7.0.15 [C:\Program Files\dotnet\sdk]
```

---

## 常见问题

### Q1: dotnet 命令找不到
**解决方案**：
1. 确认已安装 .NET 7 SDK（不是 Runtime）
2. 重启命令行或 VS Code
3. 检查环境变量 PATH
4. 重启计算机

### Q2: VS Code 智能感知不工作
**解决方案**：
1. 确认已安装 C# 扩展
2. 打开项目后等待 OmniSharp 加载完成（查看右下角状态栏）
3. 按 `Ctrl + Shift + P`，搜索 "OmniSharp: Restart OmniSharp"
4. 重新加载 VS Code 窗口

### Q3: 需要安装哪个版本的 .NET？
**答案**：
- **推荐**: .NET 7.0（项目基于此版本）
- **也可用**: .NET 8.0（向下兼容）
- **不推荐**: .NET 6.0 或更早版本

---

## 下一步

环境配置完成后，请执行以下命令确认：

```powershell
dotnet --version
```

如果显示版本号（如 `7.0.15`），说明环境准备就绪，可以继续创建项目！

---

## 快速检查清单

- [ ] .NET 7 SDK 已安装
- [ ] `dotnet --version` 命令可用
- [ ] VS Code 已安装 C# 扩展
- [ ] VS Code 已安装 C# Dev Kit 扩展（推荐）
- [ ] 已重启 VS Code 或命令行窗口

全部完成后，即可开始创建项目！