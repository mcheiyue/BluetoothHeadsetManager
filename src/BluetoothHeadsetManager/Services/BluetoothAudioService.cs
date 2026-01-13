using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using BluetoothHeadsetManager.Interop;
using Windows.Devices.Enumeration;

namespace BluetoothHeadsetManager.Services
{
    /// <summary>
    /// 蓝牙音频服务 - 使用 IKsControl 接口控制蓝牙音频设备
    /// 参考: ToothTray 项目的 BluetoothAudioDevices 实现
    /// </summary>
    public class BluetoothAudioService : IDisposable
    {
        private readonly Dictionary<Guid, BluetoothAudioDeviceInfo> _audioDevices = new();
        private readonly Dictionary<Guid, string> _deviceContainers = new();
        private bool _disposed;

        public event EventHandler<string>? StatusChanged;

        /// <summary>
        /// 枚举所有蓝牙音频设备
        /// </summary>
        public async Task<List<BluetoothAudioDeviceInfo>> EnumerateAudioDevicesAsync()
        {
            return await Task.Run(() => EnumerateAudioDevices());
        }

        /// <summary>
        /// 枚举所有蓝牙音频设备 (同步版本)
        /// </summary>
        public List<BluetoothAudioDeviceInfo> EnumerateAudioDevices()
        {
            Logger.Log("BluetoothAudioService.EnumerateAudioDevices 开始");
            _audioDevices.Clear();
            
            try
            {
                // 首先枚举设备容器获取设备名称
                EnumerateDeviceContainers();

                // 创建设备枚举器
                var enumerator = CoreAudioInterop.CreateDeviceEnumerator();

                // 枚举所有渲染设备
                int hr = enumerator.EnumAudioEndpoints(
                    EDataFlow.eRender,
                    CoreAudioInterop.DEVICE_STATEMASK_ALL,
                    out var deviceCollection);

                if (hr < 0)
                {
                    Logger.LogError($"EnumAudioEndpoints 失败: 0x{hr:X8}");
                    return new List<BluetoothAudioDeviceInfo>();
                }

                deviceCollection.GetCount(out uint deviceCount);
                Logger.Log($"找到 {deviceCount} 个音频端点");

                for (uint i = 0; i < deviceCount; i++)
                {
                    try
                    {
                        ProcessAudioEndpoint(enumerator, deviceCollection, i);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"处理音频端点 {i} 时出错", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("枚举音频设备失败", ex);
            }

            Logger.Log($"枚举完成，找到 {_audioDevices.Count} 个蓝牙音频设备");
            return new List<BluetoothAudioDeviceInfo>(_audioDevices.Values);
        }

        /// <summary>
        /// 枚举设备容器以获取蓝牙设备的友好名称
        /// </summary>
        private void EnumerateDeviceContainers()
        {
            _deviceContainers.Clear();

            try
            {
                // 使用 Windows Runtime API 枚举设备容器
                var task = DeviceInformation.FindAllAsync(
                    string.Empty,
                    null,
                    DeviceInformationKind.DeviceContainer).AsTask();
                
                task.Wait();
                var containers = task.Result;

                foreach (var container in containers)
                {
                    if (Guid.TryParse(container.Id.Trim('{', '}'), out var containerId))
                    {
                        _deviceContainers[containerId] = container.Name;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"找到 {_deviceContainers.Count} 个设备容器");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"枚举设备容器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理单个音频端点
        /// </summary>
        private void ProcessAudioEndpoint(IMMDeviceEnumerator enumerator, IMMDeviceCollection collection, uint index)
        {
            collection.Item(index, out var device);

            // 获取设备状态
            device.GetState(out uint state);

            // 获取设备 ID
            device.GetId(out string deviceId);

            // 获取设备属性
            int hr = device.OpenPropertyStore(CoreAudioInterop.STGM_READ, out var propertyStore);
            if (hr < 0)
            {
                System.Diagnostics.Debug.WriteLine($"OpenPropertyStore 失败: 0x{hr:X8}");
                return;
            }

            // 获取容器 ID
            var containerIdKey = CoreAudioInterop.PKEY_Device_ContainerId;
            hr = propertyStore.GetValue(ref containerIdKey, out var containerIdProp);
            if (hr < 0)
            {
                System.Diagnostics.Debug.WriteLine($"GetValue ContainerId 失败: 0x{hr:X8}");
                containerIdProp.Dispose();
                return;
            }
            
            var containerId = containerIdProp.GetGuid();
            containerIdProp.Dispose();

            if (containerId == Guid.Empty)
                return;

            // 获取设备拓扑
            var topologyIid = CoreAudioInterop.IID_IDeviceTopology;
            hr = device.Activate(ref topologyIid, KsControlInterop.CLSCTX_ALL, IntPtr.Zero, out var topologyObj);
            if (hr < 0)
            {
                System.Diagnostics.Debug.WriteLine($"Activate IDeviceTopology 失败: 0x{hr:X8}");
                return;
            }

            var topology = (IDeviceTopology)topologyObj;

            // 获取连接器
            topology.GetConnectorCount(out uint connectorCount);

            for (uint c = 0; c < connectorCount; c++)
            {
                try
                {
                    ProcessConnector(enumerator, topology, c, containerId, state);
                }
                catch (COMException comEx)
                {
                    // 一些连接器可能不支持某些操作
                    System.Diagnostics.Debug.WriteLine($"处理连接器 {c} 时 COM 异常: 0x{comEx.HResult:X8}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"处理连接器 {c} 时异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 处理连接器，查找蓝牙设备
        /// 参考 ToothTray 的实现，简化 COM 调用
        /// </summary>
        private void ProcessConnector(IMMDeviceEnumerator enumerator, IDeviceTopology topology,
            uint connectorIndex, Guid containerId, uint deviceState)
        {
            topology.GetConnector(connectorIndex, out var connector);

            // 尝试获取连接的另一端连接器
            int hr = connector.GetConnectedTo(out var otherConnector);
            if (hr < 0 || otherConnector == null)
            {
                // 未连接，跳过
                return;
            }

            string? otherDeviceId = null;

            try
            {
                // 方法1: 直接作为 IPart 查询拓扑对象
                // 在 C# 中，COM 对象可以直接转换为其他接口（如果支持的话）
                var part = otherConnector as IPart;
                if (part != null)
                {
                    hr = part.GetTopologyObject(out var otherTopology);
                    if (hr >= 0 && otherTopology != null)
                    {
                        hr = otherTopology.GetDeviceId(out otherDeviceId);
                        if (hr < 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"GetDeviceId 失败: 0x{hr:X8}");
                        }
                    }
                }
                else
                {
                    // 方法2: 使用 GetDeviceIdConnectedTo
                    hr = connector.GetDeviceIdConnectedTo(out otherDeviceId);
                    if (hr < 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"GetDeviceIdConnectedTo 失败: 0x{hr:X8}");
                    }
                }
            }
            catch (InvalidCastException)
            {
                // 无法转换为 IPart，尝试直接获取设备 ID
                hr = connector.GetDeviceIdConnectedTo(out otherDeviceId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理连接器异常: {ex.Message}");
                return;
            }

            if (string.IsNullOrEmpty(otherDeviceId))
            {
                System.Diagnostics.Debug.WriteLine($"无法获取另一端设备 ID");
                return;
            }

            Logger.Log($"  连接器 {connectorIndex}: 连接到 {otherDeviceId}");

            // 检查是否是蓝牙设备
            // ToothTray 检查: starts_with "{2}.\\?\bth" (bthenum 或 bthhfenum)
            if (!otherDeviceId.StartsWith("{2}.\\\\?\\bth", StringComparison.OrdinalIgnoreCase) &&
                !otherDeviceId.Contains("bth", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Log($"    跳过非蓝牙设备");
                return;
            }

            Logger.Log($"    发现蓝牙音频设备!");

            // 获取蓝牙设备并激活 IKsControl
            hr = enumerator.GetDevice(otherDeviceId, out var otherDevice);
            if (hr < 0 || otherDevice == null)
            {
                Logger.LogError($"    GetDevice 失败: 0x{hr:X8}");
                return;
            }

            var ksControlIid = CoreAudioInterop.IID_IKsControl;
            hr = otherDevice.Activate(ref ksControlIid, KsControlInterop.CLSCTX_ALL, IntPtr.Zero, out var ksControlObj);
            if (hr < 0 || ksControlObj == null)
            {
                Logger.LogError($"    Activate IKsControl 失败: 0x{hr:X8}");
                return;
            }

            var ksControl = (IKsControl)ksControlObj;
            Logger.Log($"    成功获取 IKsControl 接口!");

            // 添加到设备列表
            if (!_audioDevices.TryGetValue(containerId, out var audioDevice))
            {
                // 获取设备名称
                string deviceName = _deviceContainers.TryGetValue(containerId, out var name)
                    ? name
                    : $"蓝牙设备 {containerId}";

                audioDevice = new BluetoothAudioDeviceInfo
                {
                    Name = deviceName,
                    ContainerId = containerId
                };
                _audioDevices[containerId] = audioDevice;
                
                Logger.Log($"    创建新蓝牙音频设备: '{deviceName}'");
            }

            audioDevice.AddKsControl(ksControl, deviceState);
            audioDevice.DeviceIds.Add(otherDeviceId);
            Logger.Log($"    添加 KsControl, 当前设备 KsControls 数量: {audioDevice.KsControls.Count}");
        }

        /// <summary>
        /// 连接蓝牙音频设备
        /// </summary>
        public async Task<bool> ConnectAsync(BluetoothAudioDeviceInfo device)
        {
            return await Task.Run(() =>
            {
                try
                {
                    StatusChanged?.Invoke(this, $"正在连接 {device.Name}...");
                    bool result = device.Connect();
                    
                    if (result)
                    {
                        StatusChanged?.Invoke(this, $"已连接 {device.Name}");
                    }
                    else
                    {
                        StatusChanged?.Invoke(this, $"连接 {device.Name} 失败");
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke(this, $"连接失败: {ex.Message}");
                    return false;
                }
            });
        }

        /// <summary>
        /// 断开蓝牙音频设备
        /// </summary>
        public async Task<bool> DisconnectAsync(BluetoothAudioDeviceInfo device)
        {
            return await Task.Run(() =>
            {
                try
                {
                    StatusChanged?.Invoke(this, $"正在断开 {device.Name}...");
                    bool result = device.Disconnect();
                    
                    if (result)
                    {
                        StatusChanged?.Invoke(this, $"已断开 {device.Name}");
                    }
                    else
                    {
                        StatusChanged?.Invoke(this, $"断开 {device.Name} 失败");
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke(this, $"断开失败: {ex.Message}");
                    return false;
                }
            });
        }

        /// <summary>
        /// 切换连接状态
        /// </summary>
        public async Task<bool> ToggleConnectionAsync(BluetoothAudioDeviceInfo device)
        {
            if (device.IsConnected)
            {
                return await DisconnectAsync(device);
            }
            else
            {
                return await ConnectAsync(device);
            }
        }

        /// <summary>
        /// 根据容器 ID 获取设备
        /// </summary>
        public BluetoothAudioDeviceInfo? GetDeviceByContainerId(Guid containerId)
        {
            return _audioDevices.TryGetValue(containerId, out var device) ? device : null;
        }

        /// <summary>
        /// 根据设备名称查找设备
        /// </summary>
        public BluetoothAudioDeviceInfo? FindDeviceByName(string name)
        {
            foreach (var device in _audioDevices.Values)
            {
                if (device.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                {
                    return device;
                }
            }
            return null;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _audioDevices.Clear();
                _deviceContainers.Clear();
                _disposed = true;
            }
        }
    }
}