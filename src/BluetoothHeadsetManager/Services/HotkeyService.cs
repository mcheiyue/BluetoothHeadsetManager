using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace BluetoothHeadsetManager.Services
{
    /// <summary>
    /// 全局热键服务
    /// </summary>
    public class HotkeyService : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private readonly Dictionary<int, Action> _hotkeyActions = new();
        private HwndSource? _hwndSource;
        private IntPtr _windowHandle;
        private int _currentId = 0;
        private bool _disposed;

        #region Native Methods

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        #endregion

        /// <summary>
        /// 修饰键
        /// </summary>
        [Flags]
        public enum ModifierKeys : uint
        {
            None = 0,
            Alt = 1,
            Control = 2,
            Shift = 4,
            Win = 8
        }

        /// <summary>
        /// 初始化热键服务
        /// </summary>
        /// <param name="windowHandle">窗口句柄</param>
        public void Initialize(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
            _hwndSource = HwndSource.FromHwnd(windowHandle);
            _hwndSource?.AddHook(WndProc);
        }

        /// <summary>
        /// 为无窗口应用程序初始化热键服务
        /// </summary>
        public void InitializeForWindowless()
        {
            // 创建一个隐藏的消息窗口
            var parameters = new HwndSourceParameters("HotkeyMessageWindow")
            {
                Width = 0,
                Height = 0,
                PositionX = 0,
                PositionY = 0,
                WindowStyle = 0, // 无样式
                ExtendedWindowStyle = 0,
                ParentWindow = IntPtr.Zero
            };

            _hwndSource = new HwndSource(parameters);
            _windowHandle = _hwndSource.Handle;
            _hwndSource.AddHook(WndProc);
        }

        /// <summary>
        /// 注册热键
        /// </summary>
        /// <param name="modifiers">修饰键</param>
        /// <param name="key">虚拟键码</param>
        /// <param name="action">回调动作</param>
        /// <returns>热键ID，失败返回-1</returns>
        public int RegisterHotkey(ModifierKeys modifiers, uint key, Action action)
        {
            if (_windowHandle == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("热键服务未初始化");
                return -1;
            }

            int id = ++_currentId;
            
            if (RegisterHotKey(_windowHandle, id, (uint)modifiers, key))
            {
                _hotkeyActions[id] = action;
                System.Diagnostics.Debug.WriteLine($"注册热键成功: ID={id}");
                return id;
            }
            else
            {
                int error = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"注册热键失败: 错误代码={error}");
                return -1;
            }
        }

        /// <summary>
        /// 注销热键
        /// </summary>
        /// <param name="hotkeyId">热键ID</param>
        public void UnregisterHotkey(int hotkeyId)
        {
            if (_windowHandle != IntPtr.Zero && _hotkeyActions.ContainsKey(hotkeyId))
            {
                UnregisterHotKey(_windowHandle, hotkeyId);
                _hotkeyActions.Remove(hotkeyId);
            }
        }

        /// <summary>
        /// 消息处理
        /// </summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();
                if (_hotkeyActions.TryGetValue(hotkeyId, out var action))
                {
                    action?.Invoke();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // 注销所有热键
                foreach (var id in _hotkeyActions.Keys)
                {
                    if (_windowHandle != IntPtr.Zero)
                    {
                        UnregisterHotKey(_windowHandle, id);
                    }
                }
                _hotkeyActions.Clear();

                _hwndSource?.RemoveHook(WndProc);
                _hwndSource?.Dispose();
                _hwndSource = null;

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 常用虚拟键码
    /// </summary>
    public static class VirtualKeys
    {
        public const uint VK_F1 = 0x70;
        public const uint VK_F2 = 0x71;
        public const uint VK_F3 = 0x72;
        public const uint VK_F4 = 0x73;
        public const uint VK_F5 = 0x74;
        public const uint VK_F6 = 0x75;
        public const uint VK_F7 = 0x76;
        public const uint VK_F8 = 0x77;
        public const uint VK_F9 = 0x78;
        public const uint VK_F10 = 0x79;
        public const uint VK_F11 = 0x7A;
        public const uint VK_F12 = 0x7B;

        // 字母键 (A-Z)
        public const uint VK_A = 0x41;
        public const uint VK_B = 0x42;
        public const uint VK_C = 0x43;
        public const uint VK_D = 0x44;
        public const uint VK_E = 0x45;
        public const uint VK_F = 0x46;
        public const uint VK_G = 0x47;
        public const uint VK_H = 0x48;
        public const uint VK_I = 0x49;
        public const uint VK_J = 0x4A;
        public const uint VK_K = 0x4B;
        public const uint VK_L = 0x4C;
        public const uint VK_M = 0x4D;
        public const uint VK_N = 0x4E;
        public const uint VK_O = 0x4F;
        public const uint VK_P = 0x50;
        public const uint VK_Q = 0x51;
        public const uint VK_R = 0x52;
        public const uint VK_S = 0x53;
        public const uint VK_T = 0x54;
        public const uint VK_U = 0x55;
        public const uint VK_V = 0x56;
        public const uint VK_W = 0x57;
        public const uint VK_X = 0x58;
        public const uint VK_Y = 0x59;
        public const uint VK_Z = 0x5A;

        // 数字键 (0-9)
        public const uint VK_0 = 0x30;
        public const uint VK_1 = 0x31;
        public const uint VK_2 = 0x32;
        public const uint VK_3 = 0x33;
        public const uint VK_4 = 0x34;
        public const uint VK_5 = 0x35;
        public const uint VK_6 = 0x36;
        public const uint VK_7 = 0x37;
        public const uint VK_8 = 0x38;
        public const uint VK_9 = 0x39;
    }
}