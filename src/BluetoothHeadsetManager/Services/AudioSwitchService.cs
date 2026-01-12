using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace BluetoothHeadsetManager.Services
{
    /// <summary>
    /// 音频切换服务 - 自动将音频输出切换到蓝牙设备
    /// 参考 SoundSwitch 项目的实现
    /// </summary>
    public class AudioSwitchService : IDisposable
    {
        private bool _disposed;
        private MMDeviceEnumerator? _enumerator;

        #region COM Interfaces for Setting Default Audio Device

        // Policy Config GUID
        private const string POLICY_CONFIG_CLIENT_IID = "870AF99C-171D-4F9E-AF0D-E63DF40C2BC9";

        [ComImport, Guid(POLICY_CONFIG_CLIENT_IID)]
        private class PolicyConfigClient
        {
        }

        // IPolicyConfig interface (Windows 7+)
        [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPolicyConfig
        {
            [PreserveSig]
            int GetMixFormat(
                [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
                [In] IntPtr ppFormat);

            [PreserveSig]
            int GetDeviceFormat(
                [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
                [In][MarshalAs(UnmanagedType.Bool)] bool bDefault,
                [In] IntPtr ppFormat);

            [PreserveSig]
            int ResetDeviceFormat([In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);

            [PreserveSig]
            int SetDeviceFormat(
                [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
                [In] IntPtr pEndpointFormat,
                [In] IntPtr mixFormat);

            [PreserveSig]
            int GetProcessingPeriod(
                [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
                [In][MarshalAs(UnmanagedType.Bool)] bool bDefault,
                [In] IntPtr pmftDefaultPeriod,
                [In] IntPtr pmftMinimumPeriod);

            [PreserveSig]
            int SetProcessingPeriod(
                [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
                [In] IntPtr pmftPeriod);

            [PreserveSig]
            int GetShareMode(
                [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
                [In] IntPtr pMode);

            [PreserveSig]
            int SetShareMode(
                [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
                [In] IntPtr mode);

            [PreserveSig]
            int GetPropertyValue(
                [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
                [In][MarshalAs(UnmanagedType.Bool)] bool bFxStore,
                [In] IntPtr key,
                [In] IntPtr pv);

            [PreserveSig]
            int SetPropertyValue(
                [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
                [In][MarshalAs(UnmanagedType.Bool)] bool bFxStore,
                [In] IntPtr key,
                [In] IntPtr pv);

            [PreserveSig]
            int SetDefaultEndpoint(
                [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
                [In][MarshalAs(UnmanagedType.U4)] ERole role);

            [PreserveSig]
            int SetEndpointVisibility(
                [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
                [In][MarshalAs(UnmanagedType.Bool)] bool bVisible);
        }

        // IPolicyConfigX interface (Windows 10+)
        [Guid("8F9FB2AA-1C0B-4D54-B6BB-B2F2A10CE03C")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPolicyConfigX
        {
            [PreserveSig]
            int GetMixFormat(
                [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
                [In] IntPtr ppFormat);

            [PreserveSig]
            int GetDeviceFormat(
                [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
                [In][MarshalAs(UnmanagedType.Bool)] bool bDefault,
                [In] IntPtr ppFormat);

            [PreserveSig]
            int ResetDeviceFormat([In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);

            [PreserveSig]
            int SetDeviceFormat(
                [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
                [In] IntPtr pEndpointFormat,
                [In] IntPtr mixFormat);

            [PreserveSig]
            int GetProcessingPeriod(
                [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
                [In][MarshalAs(UnmanagedType.Bool)] bool bDefault,
                [In] IntPtr pmftDefaultPeriod,
                [In] IntPtr pmftMinimumPeriod);

            [PreserveSig]
            int SetProcessingPeriod(
                [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
                [In] IntPtr pmftPeriod);

            [PreserveSig]
            int GetShareMode(
                [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
                [In] IntPtr pMode);

            [PreserveSig]
            int SetShareMode(
                [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
                [In] IntPtr mode);

            [PreserveSig]
            int GetPropertyValue(
                [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
                [In][MarshalAs(UnmanagedType.Bool)] bool bFxStore,
                [In] IntPtr key,
                [In] IntPtr pv);

            [PreserveSig]
            int SetPropertyValue(
                [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
                [In][MarshalAs(UnmanagedType.Bool)] bool bFxStore,
                [In] IntPtr key,
                [In] IntPtr pv);

            [PreserveSig]
            int SetDefaultEndpoint(
                [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
                [In][MarshalAs(UnmanagedType.U4)] ERole role);

            [PreserveSig]
            int SetEndpointVisibility(
                [In][MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName,
                [In][MarshalAs(UnmanagedType.Bool)] bool bVisible);
        }

        private enum ERole : uint
        {
            eConsole = 0,
            eMultimedia = 1,
            eCommunications = 2
        }

        #endregion

        public AudioSwitchService()
        {
            try
            {
                _enumerator = new MMDeviceEnumerator();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"音频服务初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取所有音频播放设备
        /// </summary>
        public List<AudioDeviceInfo> GetPlaybackDevices()
        {
            var devices = new List<AudioDeviceInfo>();
            
            if (_enumerator == null) return devices;

            try
            {
                var collection = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                foreach (var device in collection)
                {
                    devices.Add(new AudioDeviceInfo
                    {
                        Id = device.ID,
                        Name = device.FriendlyName,
                        IsDefault = IsDefaultDevice(device.ID, DataFlow.Render)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取音频设备列表失败: {ex.Message}");
            }

            return devices;
        }

        /// <summary>
        /// 获取当前默认播放设备
        /// </summary>
        public AudioDeviceInfo? GetDefaultPlaybackDevice()
        {
            if (_enumerator == null) return null;

            try
            {
                var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                return new AudioDeviceInfo
                {
                    Id = device.ID,
                    Name = device.FriendlyName,
                    IsDefault = true
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取默认音频设备失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 检查设备是否是默认设备
        /// </summary>
        private bool IsDefaultDevice(string deviceId, DataFlow flow)
        {
            if (_enumerator == null) return false;

            try
            {
                var defaultDevice = _enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
                return defaultDevice.ID == deviceId;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 设置默认播放设备
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>是否成功</returns>
        public bool SetDefaultPlaybackDevice(string deviceId)
        {
            try
            {
                var policyConfig = new PolicyConfigClient();
                
                // 尝试使用 IPolicyConfigX (Windows 10+)
                var policyConfigX = policyConfig as IPolicyConfigX;
                if (policyConfigX != null)
                {
                    // 设置所有角色的默认设备
                    Marshal.ThrowExceptionForHR(policyConfigX.SetDefaultEndpoint(deviceId, ERole.eConsole));
                    Marshal.ThrowExceptionForHR(policyConfigX.SetDefaultEndpoint(deviceId, ERole.eMultimedia));
                    Marshal.ThrowExceptionForHR(policyConfigX.SetDefaultEndpoint(deviceId, ERole.eCommunications));
                    return true;
                }

                // 回退到 IPolicyConfig (Windows 7+)
                var policyConfigOld = policyConfig as IPolicyConfig;
                if (policyConfigOld != null)
                {
                    Marshal.ThrowExceptionForHR(policyConfigOld.SetDefaultEndpoint(deviceId, ERole.eConsole));
                    Marshal.ThrowExceptionForHR(policyConfigOld.SetDefaultEndpoint(deviceId, ERole.eMultimedia));
                    Marshal.ThrowExceptionForHR(policyConfigOld.SetDefaultEndpoint(deviceId, ERole.eCommunications));
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置默认音频设备失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 根据蓝牙设备名称查找并切换到对应的音频设备
        /// </summary>
        /// <param name="bluetoothDeviceName">蓝牙设备名称</param>
        /// <returns>是否成功</returns>
        public bool SwitchToBluetoothDevice(string bluetoothDeviceName)
        {
            var devices = GetPlaybackDevices();
            
            // 查找包含蓝牙设备名称的音频设备
            var audioDevice = devices.FirstOrDefault(d => 
                d.Name.Contains(bluetoothDeviceName, StringComparison.OrdinalIgnoreCase));

            if (audioDevice != null)
            {
                return SetDefaultPlaybackDevice(audioDevice.Id);
            }

            System.Diagnostics.Debug.WriteLine($"未找到蓝牙音频设备: {bluetoothDeviceName}");
            return false;
        }

        /// <summary>
        /// 根据蓝牙MAC地址查找并切换到对应的音频设备
        /// </summary>
        /// <param name="macAddress">MAC地址</param>
        /// <returns>是否成功</returns>
        public bool SwitchToBluetoothDeviceByMac(string macAddress)
        {
            var devices = GetPlaybackDevices();
            
            // 音频设备ID通常包含蓝牙MAC地址（格式可能不同）
            string cleanMac = macAddress.Replace(":", "").Replace("-", "").ToLowerInvariant();
            
            var audioDevice = devices.FirstOrDefault(d => 
                d.Id.ToLowerInvariant().Contains(cleanMac));

            if (audioDevice != null)
            {
                return SetDefaultPlaybackDevice(audioDevice.Id);
            }

            return false;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _enumerator?.Dispose();
                _enumerator = null;
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 音频设备信息
    /// </summary>
    public class AudioDeviceInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
    }
}