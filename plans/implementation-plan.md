# 蓝牙耳机管理器 - 详细实施计划

## 项目概览

**项目名称**: BluetoothHeadsetManager  
**技术栈**: C# + .NET 7 + WinForms  
**开发工具**: VS Code + .NET CLI  
**目标**: 轻量级 Windows 系统托盘工具，实现一键连接蓝牙耳机和电量显示

---

## 任务分解

### 1️⃣ 项目环境准备与初始化

#### 1.1 开发环境配置
- 验证 .NET 7 SDK 安装
- 安装 VS Code C# 扩展
- 配置 VS Code 工作区设置

#### 1.2 项目创建
- 使用 .NET CLI 创建 WinForms 项目
- 创建解决方案文件
- 配置项目文件 (`.csproj`)
- 添加必要的 NuGet 包依赖

#### 1.3 版本控制设置
- 初始化 Git 仓库
- 创建 `.gitignore` 文件
- 创建初始提交

#### 1.4 VS Code 调试配置
- 创建 `.vscode/launch.json`
- 创建 `.vscode/tasks.json`
- 创建 `.vscode/settings.json`
- 测试 F5 调试功能

**输出**: 可编译运行的空白 WinForms 项目

---

### 2️⃣ 核心项目结构搭建

#### 2.1 创建目录结构
```
BluetoothHeadsetManager/
├── UI/                    # 用户界面层
├── Core/                  # 业务逻辑层
├── Bluetooth/             # 蓝牙API封装
├── Models/                # 数据模型
├── Utils/                 # 工具类
└── Resources/             # 资源文件
```

#### 2.2 创建基础类文件
- `Program.cs` - 程序入口
- `UI/TrayApplication.cs` - 托盘应用主类
- `Models/DeviceInfo.cs` - 设备信息模型
- `Models/AppConfig.cs` - 应用配置模型
- `Utils/Logger.cs` - 日志工具类

#### 2.3 资源文件准备
- 创建临时托盘图标（可使用系统图标）
- 准备配置文件模板 `config.json`

**输出**: 完整的项目目录结构和基础类框架

---

### 3️⃣ 蓝牙API封装与设备管理

#### 3.1 蓝牙适配器封装
- 创建 `Bluetooth/BluetoothAdapter.cs`
- 实现蓝牙适配器初始化
- 实现设备扫描功能
- 实现获取已配对设备列表

#### 3.2 设备管理器实现
- 创建 `Core/DeviceManager.cs`
- 实现设备扫描方法 `ScanDevicesAsync()`
- 实现获取已配对设备 `GetPairedDevicesAsync()`
- 实现设备查找 `FindDeviceAsync()`
- 实现设备缓存机制

#### 3.3 蓝牙设备模型
- 创建 `Bluetooth/BluetoothDevice.cs`
- 定义设备属性：名称、地址、连接状态等
- 实现设备信息序列化/反序列化

#### 3.4 Windows蓝牙API集成
- 引入 `Windows.Devices.Bluetooth` 命名空间
- 引入 `Windows.Devices.Enumeration` 命名空间
- 实现蓝牙权限检查
- 测试设备扫描功能

**输出**: 可扫描和识别蓝牙设备的功能模块

---

### 4️⃣ 系统托盘UI实现

#### 4.1 基础托盘图标
- 实现 `UI/TrayApplication.cs` 基础框架
- 创建 `NotifyIcon` 实例
- 设置托盘图标和提示文本
- 实现托盘图标显示/隐藏

#### 4.2 右键菜单构建
- 创建 `UI/ContextMenuBuilder.cs`
- 实现菜单项：连接耳机、断开连接、设置、退出
- 添加菜单分隔符
- 绑定菜单项事件处理器

#### 4.3 托盘图标状态管理
- 创建多个图标状态：未连接、连接中、已连接
- 实现图标状态切换逻辑
- 实现提示文本动态更新

#### 4.4 气泡通知
- 创建 `Utils/NotificationHelper.cs`
- 实现连接成功通知
- 实现断开连接通知
- 实现错误提示通知
- 实现低电量警告通知

#### 4.5 左键单击事件
- 实现左键单击快速切换连接状态
- 添加防抖动逻辑

**输出**: 功能完整的系统托盘界面

---

### 5️⃣ 连接管理功能实现

#### 5.1 连接管理器基础
- 创建 `Core/ConnectionManager.cs`
- 定义连接状态枚举
- 实现连接状态变化事件

#### 5.2 连接功能实现
- 实现 `ConnectAsync()` 方法
- 处理连接超时
- 处理连接失败
- 实现连接状态监控

#### 5.3 断开功能实现
- 实现 `DisconnectAsync()` 方法
- 确保资源正确释放
- 更新连接状态

#### 5.4 连接状态查询
- 实现 `IsConnected()` 方法
- 实现连接状态轮询
- 实现状态缓存机制

#### 5.5 自动重连机制
- 实现意外断开检测
- 实现自动重连逻辑
- 设置重连次数限制
- 设置重连间隔时间

**输出**: 稳定的蓝牙连接/断开功能

---

### 6️⃣ 电量监控功能实现

#### 6.1 电量监控器基础
- 创建 `Core/BatteryMonitor.cs`
- 创建 `Models/BatteryInfo.cs`
- 定义电量变化事件

#### 6.2 电量获取实现
- 实现通过 GATT 服务获取电量
- 查找 Battery Service (UUID: 0x180F)
- 读取 Battery Level Characteristic
- 处理不支持电量的设备

#### 6.3 定时监控机制
- 实现 `StartMonitoring()` 方法
- 使用 `System.Timers.Timer` 定时查询
- 配置查询间隔（默认60秒）
- 实现 `StopMonitoring()` 方法

#### 6.4 电量显示更新
- 更新托盘图标提示文本显示电量
- 实现电量百分比格式化
- 处理电量获取失败情况

#### 6.5 低电量提醒
- 设置低电量阈值（默认20%）
- 实现低电量气泡通知
- 防止重复提醒

**输出**: 实时电量监控和显示功能

---

### 7️⃣ 配置管理与持久化

#### 7.1 配置管理器
- 创建 `Core/ConfigManager.cs`
- 定义配置文件路径
- 实现配置加载方法
- 实现配置保存方法

#### 7.2 配置模型设计
- 扩展 `Models/AppConfig.cs`
- 定义常用设备配置
- 定义应用设置（自动启动、通知等）
- 定义UI设置（语言、主题等）

#### 7.3 JSON序列化
- 使用 `System.Text.Json`
- 实现配置序列化
- 实现配置反序列化
- 处理配置文件损坏情况

#### 7.4 默认配置
- 创建默认配置模板
- 实现首次运行配置初始化
- 实现配置迁移机制

#### 7.5 常用设备保存
- 实现保存常用设备方法
- 实现加载常用设备方法
- 记录最后连接时间

**输出**: 完整的配置管理系统

---

### 8️⃣ 日志系统实现

#### 8.1 日志工具类
- 完善 `Utils/Logger.cs`
- 定义日志级别（Debug, Info, Warning, Error）
- 实现日志写入文件
- 实现日志控制台输出

#### 8.2 日志格式设计
- 时间戳格式化
- 日志级别标识
- 消息内容格式
- 异常堆栈跟踪

#### 8.3 日志文件管理
- 设置日志文件路径
- 实现日志文件滚动
- 设置最大日志文件大小
- 实现旧日志清理

#### 8.4 关键操作日志
- 记录应用启动/退出
- 记录设备连接/断开
- 记录配置加载/保存
- 记录错误和异常

**输出**: 完善的日志记录系统

---

### 9️⃣ 错误处理与重连机制

#### 9.1 异常处理策略
- 定义自定义异常类
- 实现全局异常捕获
- 实现关键操作的 try-catch
- 记录异常到日志

#### 9.2 连接失败处理
- 处理蓝牙适配器未启用
- 处理设备不可用
- 处理连接超时
- 友好的错误提示

#### 9.3 重连机制优化
- 实现指数退避算法
- 设置最大重连次数
- 实现用户手动取消重连
- 重连状态UI反馈

#### 9.4 资源清理
- 实现 `Dispose` 模式
- 确保蓝牙连接正确释放
- 确保定时器正确停止
- 确保托盘图标正确移除

**输出**: 健壮的错误处理机制

---

### 🔟 性能优化与资源管理

#### 10.1 启动优化
- 延迟加载非关键模块
- 异步初始化蓝牙适配器
- 优化配置加载速度
- 减少启动时的UI阻塞

#### 10.2 内存优化
- 及时释放未使用的蓝牙连接
- 使用弱引用缓存设备列表
- 限制日志文件大小
- 定期触发垃圾回收

#### 10.3 CPU优化
- 优化电量查询频率
- 使用异步操作避免阻塞
- 降低定时器触发频率
- 避免不必要的设备扫描

#### 10.4 编译优化
- 配置单文件发布
- 启用 Trimming
- 启用压缩
- 测试发布后文件大小

**输出**: 体积小、内存占用低的应用

---

### 1️⃣1️⃣ 单元测试编写

#### 11.1 测试项目创建
- 创建 `BluetoothHeadsetManager.Tests` 项目
- 添加 xUnit 测试框架
- 配置测试项目引用

#### 11.2 核心功能测试
- 测试设备管理器功能
- 测试连接管理器功能
- 测试配置管理器功能
- 测试日志工具类

#### 11.3 Mock对象
- Mock 蓝牙设备
- Mock 配置文件
- Mock 系统API

#### 11.4 测试覆盖率
- 运行测试覆盖率分析
- 补充关键路径测试
- 确保核心功能测试覆盖

**输出**: 完整的单元测试套件

---

### 1️⃣2️⃣ 打包发布配置

#### 12.1 发布配置
- 创建 `build/publish.ps1` 脚本
- 配置发布参数
- 设置输出路径
- 测试发布流程

#### 12.2 应用图标
- 设计或获取应用图标
- 创建多尺寸图标文件
- 配置应用图标

#### 12.3 版本信息
- 设置应用版本号
- 设置公司信息
- 设置版权信息
- 设置产品描述

#### 12.4 自动启动配置
- 实现添加到启动项功能
- 实现从启动项移除功能
- 在设置界面添加开关

#### 12.5 安装程序（可选）
- 考虑使用 WiX 或 Inno Setup
- 创建安装脚本
- 测试安装/卸载流程

**输出**: 可分发的应用程序

---

### 1️⃣3️⃣ 文档完善

#### 13.1 用户文档
- 创建 `docs/user-guide.md`
- 编写安装说明
- 编写使用教程
- 添加常见问题解答
- 添加截图和示例

#### 13.2 开发文档
- 创建 `docs/architecture.md`
- 描述系统架构
- 说明模块职责
- 绘制类图和流程图

#### 13.3 API文档
- 创建 `docs/api.md`
- 文档化公共API
- 添加代码示例
- 说明参数和返回值

#### 13.4 README更新
- 更新项目说明
- 添加功能列表
- 添加构建说明
- 添加贡献指南

#### 13.5 变更日志
- 创建 `CHANGELOG.md`
- 记录版本变更
- 记录功能新增
- 记录问题修复

**输出**: 完整的项目文档

---

## 技术要点参考

### 关键技术栈

```xml
<!-- .csproj 配置 -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net7.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <ApplicationIcon>Resources\icon.ico</ApplicationIcon>
    
    <!-- 单文件发布 -->
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PublishTrimmed>true</PublishTrimmed>
    <TrimMode>link</TrimMode>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Windows.SDK.Contracts" Version="10.0.22621.38" />
    <PackageReference Include="System.Text.Json" Version="7.0.0" />
  </ItemGroup>
</Project>
```

### 核心API使用

```csharp
// 蓝牙设备扫描
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

var selector = BluetoothDevice.GetDeviceSelector();
var devices = await DeviceInformation.FindAllAsync(selector);

// 连接设备
var device = await BluetoothDevice.FromIdAsync(deviceId);

// 获取电量
var gattServices = await device.GetGattServicesAsync();
var batteryService = gattServices.Services
    .FirstOrDefault(s => s.Uuid == GattServiceUuids.Battery);
```

---

## 开发流程建议

### 迭代开发策略

**第1轮迭代 - MVP（最小可用产品）**
- 任务 1-4：环境搭建 + 基础UI
- 任务 5：基础连接功能
- 目标：能够连接/断开蓝牙设备

**第2轮迭代 - 核心功能**
- 任务 6：电量监控
- 任务 7：配置管理
- 任务 8：日志系统
- 目标：完整功能实现

**第3轮迭代 - 优化完善**
- 任务 9-10：错误处理和性能优化
- 任务 11：测试
- 目标：稳定可靠

**第4轮迭代 - 发布准备**
- 任务 12-13：打包和文档
- 目标：正式发布

### Git提交建议

- 每完成一个子任务提交一次
- 使用语义化提交信息
- 示例：`feat: 实现蓝牙设备扫描功能`
- 示例：`fix: 修复连接超时问题`

---

## 质量指标

### 性能目标
- ✅ 可执行文件 < 10MB
- ✅ 运行时内存 < 30MB
- ✅ 连接响应 < 3秒
- ✅ 启动时间 < 2秒

### 稳定性目标
- ✅ 7x24小时稳定运行
- ✅ 无内存泄漏
- ✅ 异常恢复能力
- ✅ 连接成功率 > 95%

### 代码质量
- ✅ 核心功能测试覆盖率 > 70%
- ✅ 无严重代码警告
- ✅ 符合C#编码规范
- ✅ 完整的错误处理

---

## 风险与应对

### 主要风险

1. **蓝牙API兼容性**
   - 风险：不同设备支持程度不同
   - 应对：多设备测试，提供降级方案

2. **电量获取失败**
   - 风险：部分设备不支持标准Battery Service
   - 应对：优雅降级，显示"电量不可用"

3. **连接稳定性**
   - 风险：蓝牙连接可能意外断开
   - 应对：实现自动重连机制

4. **Windows版本兼容**
   - 风险：不同Windows版本API差异
   - 应对：最低支持Win10，充分测试

---

## 后续扩展计划

### 短期扩展（v2.0）
- 多设备管理（切换不同耳机）
- 音频路由控制
- 蓝牙编码格式显示（SBC/AAC/LDAC）

### 长期扩展（v3.0）
- 均衡器调节
- 环境音控制
- 设备固件更新
- 云端配置同步

---

## 总结

这个项目采用**敏捷迭代**的方式开发，先实现MVP，然后逐步完善功能。整个开发过程分为13个主要任务，每个任务都有明确的输出目标。

**关键成功因素**：
1. 稳定的蓝牙连接机制
2. 准确的电量获取
3. 良好的用户体验
4. 控制资源占用

**开发周期预估**：
- MVP版本：2-3周
- 完整版本：4-6周
- 优化发布：6-8周

现在可以开始第一个任务：**项目环境准备与初始化**！