using System;
using System.IO;

namespace BluetoothHeadsetManager.Utils
{
    /// <summary>
    /// 日志工具类
    /// </summary>
    public static class Logger
    {
        private static string _logFilePath;
        private static readonly object _lockObject = new object();

        /// <summary>
        /// 初始化日志系统
        /// </summary>
        public static void Initialize()
        {
            try
            {
                // 设置日志文件路径
                string appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BluetoothHeadsetManager"
                );

                // 确保目录存在
                if (!Directory.Exists(appDataPath))
                {
                    Directory.CreateDirectory(appDataPath);
                }

                // 日志文件名包含日期
                string logFileName = $"app_{DateTime.Now:yyyyMMdd}.log";
                _logFilePath = Path.Combine(appDataPath, logFileName);

                // 写入启动标记
                Info("========== 日志系统已初始化 ==========");
            }
            catch (Exception ex)
            {
                // 如果初始化失败，至少输出到控制台
                Console.WriteLine($"日志系统初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        public static void Info(string message)
        {
            WriteLog("INFO", message);
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        public static void Warning(string message)
        {
            WriteLog("WARN", message);
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        public static void Error(string message, Exception ex = null)
        {
            string fullMessage = ex != null 
                ? $"{message}\n异常信息: {ex.Message}\n堆栈跟踪:\n{ex.StackTrace}"
                : message;
            
            WriteLog("ERROR", fullMessage);
        }

        /// <summary>
        /// 记录调试日志
        /// </summary>
        public static void Debug(string message)
        {
#if DEBUG
            WriteLog("DEBUG", message);
#endif
        }

        /// <summary>
        /// 写入日志
        /// </summary>
        private static void WriteLog(string level, string message)
        {
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";

                // 输出到控制台
                Console.WriteLine(logEntry);

                // 写入文件
                if (!string.IsNullOrEmpty(_logFilePath))
                {
                    lock (_lockObject)
                    {
                        File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"写入日志失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理旧日志文件（保留最近7天）
        /// </summary>
        public static void CleanOldLogs()
        {
            try
            {
                string appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BluetoothHeadsetManager"
                );

                if (!Directory.Exists(appDataPath))
                    return;

                var logFiles = Directory.GetFiles(appDataPath, "app_*.log");
                var cutoffDate = DateTime.Now.AddDays(-7);

                foreach (var logFile in logFiles)
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.LastWriteTime < cutoffDate)
                    {
                        File.Delete(logFile);
                        Info($"已删除旧日志文件: {fileInfo.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Warning($"清理旧日志失败: {ex.Message}");
            }
        }
    }
}