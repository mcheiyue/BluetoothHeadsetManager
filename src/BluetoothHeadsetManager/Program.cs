using System;
using System.Windows.Forms;
using BluetoothHeadsetManager.UI;
using BluetoothHeadsetManager.Utils;

namespace BluetoothHeadsetManager
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                // 初始化日志系统
                Logger.Initialize();
                Logger.Info("应用程序启动");

                // 设置应用程序视觉样式
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // 启动托盘应用
                using (var trayApp = new TrayApplication())
                {
                    trayApp.Start();
                    Application.Run();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("应用程序发生致命错误", ex);
                MessageBox.Show(
                    $"应用程序启动失败：{ex.Message}",
                    "错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                Logger.Info("应用程序退出");
            }
        }
    }
}