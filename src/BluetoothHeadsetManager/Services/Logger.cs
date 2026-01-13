using System;
using System.IO;

namespace BluetoothHeadsetManager.Services
{
    /// <summary>
    /// 简单的文件日志记录器
    /// </summary>
    public static class Logger
    {
        private static readonly string LogFilePath;
        private static readonly object LockObj = new object();

        static Logger()
        {
            // 日志文件放在程序目录下
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            LogFilePath = Path.Combine(exeDir, "bluetooth_manager.log");
            
            // 清空旧日志
            try
            {
                File.WriteAllText(LogFilePath, $"=== 蓝牙耳机管理器日志 - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\r\n");
            }
            catch { }
        }

        public static void Log(string message)
        {
            try
            {
                string logLine = $"[{DateTime.Now:HH:mm:ss.fff}] {message}\r\n";
                lock (LockObj)
                {
                    File.AppendAllText(LogFilePath, logLine);
                }
                System.Diagnostics.Debug.WriteLine(message);
            }
            catch { }
        }

        public static void LogError(string message, Exception? ex = null)
        {
            string errorMsg = ex != null ? $"{message}: {ex.Message}\r\n{ex.StackTrace}" : message;
            Log($"[ERROR] {errorMsg}");
        }

        public static void LogSection(string title)
        {
            Log($"\r\n{'='.ToString().PadRight(50, '=')}\r\n{title}\r\n{'='.ToString().PadRight(50, '=')}");
        }
    }
}