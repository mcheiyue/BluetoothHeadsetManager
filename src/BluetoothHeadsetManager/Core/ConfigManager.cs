using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BluetoothHeadsetManager.Models;
using BluetoothHeadsetManager.Utils;

namespace BluetoothHeadsetManager.Core
{
    /// <summary>
    /// 配置管理器
    /// 负责应用配置的保存、加载和管理
    /// </summary>
    public class ConfigManager
    {
        private static ConfigManager? _instance;
        private static readonly object _lock = new object();
        
        private readonly string _configDirectory;
        private readonly string _configFilePath;
        private AppConfig _config;
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// 获取ConfigManager单例
        /// </summary>
        public static ConfigManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ConfigManager();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 获取当前配置
        /// </summary>
        public AppConfig Config => _config;

        /// <summary>
        /// 私有构造函数（单例模式）
        /// </summary>
        private ConfigManager()
        {
            // 设置配置目录路径
            _configDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BluetoothHeadsetManager"
            );

            _configFilePath = Path.Combine(_configDirectory, "config.json");

            // JSON序列化选项
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            // 确保配置目录存在
            EnsureConfigDirectory();

            // 加载或创建配置
            _config = LoadConfig();

            Logger.Info($"ConfigManager initialized, config file: {_configFilePath}");
        }

        /// <summary>
        /// 确保配置目录存在
        /// </summary>
        private void EnsureConfigDirectory()
        {
            try
            {
                if (!Directory.Exists(_configDirectory))
                {
                    Directory.CreateDirectory(_configDirectory);
                    Logger.Info($"Created config directory: {_configDirectory}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to create config directory: {_configDirectory}", ex);
            }
        }

        /// <summary>
        /// 加载配置文件
        /// </summary>
        private AppConfig LoadConfig()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    Logger.Info("Loading configuration from file");
                    
                    string json = File.ReadAllText(_configFilePath);
                    var config = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions);
                    
                    if (config != null)
                    {
                        Logger.Info("Configuration loaded successfully");
                        return config;
                    }
                    else
                    {
                        Logger.Warning("Failed to deserialize config, creating new one");
                    }
                }
                else
                {
                    Logger.Info("Config file not found, creating default configuration");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error loading configuration, creating default", ex);
            }

            // 创建默认配置
            var defaultConfig = CreateDefaultConfig();
            SaveConfig(defaultConfig);
            return defaultConfig;
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        private AppConfig CreateDefaultConfig()
        {
            Logger.Info("Creating default configuration");
            
            return new AppConfig
            {
                AutoStart = false,
                EnableAutoReconnect = true,
                MaxReconnectAttempts = 3,
                BatteryCheckInterval = 60,
                LowBatteryThreshold = 20,
                EnableNotifications = true,
                EnableDebugLog = false,
                ConfigVersion = "1.0.0",
                LastConnectedDeviceId = string.Empty
            };
        }

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        public bool SaveConfig()
        {
            return SaveConfig(_config);
        }

        /// <summary>
        /// 保存指定配置到文件
        /// </summary>
        private bool SaveConfig(AppConfig config)
        {
            try
            {
                Logger.Info("Saving configuration");

                EnsureConfigDirectory();

                string json = JsonSerializer.Serialize(config, _jsonOptions);
                File.WriteAllText(_configFilePath, json);

                Logger.Info("Configuration saved successfully");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save configuration", ex);
                return false;
            }
        }

        /// <summary>
        /// 重新加载配置
        /// </summary>
        public void ReloadConfig()
        {
            Logger.Info("Reloading configuration");
            _config = LoadConfig();
        }

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        public void ResetToDefault()
        {
            Logger.Info("Resetting configuration to default");
            
            _config = CreateDefaultConfig();
            SaveConfig();
            
            Logger.Info("Configuration reset complete");
        }

        /// <summary>
        /// 更新最后连接的设备
        /// </summary>
        public void UpdateLastConnectedDevice(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
                return;

            try
            {
                _config.LastConnectedDeviceId = deviceId;
                SaveConfig();
                
                Logger.Info($"Updated last connected device: {deviceId}");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to update last connected device", ex);
            }
        }

        /// <summary>
        /// 添加收藏设备
        /// </summary>
        public void AddFavoriteDevice(DeviceInfo device)
        {
            if (device == null)
                return;

            try
            {
                // 检查是否已存在
                var existing = _config.FavoriteDevices
                    .Find(d => d.Id == device.Id);

                if (existing != null)
                {
                    Logger.Info($"Device {device.Name} already in favorites, updating");
                    _config.FavoriteDevices.Remove(existing);
                }

                _config.FavoriteDevices.Add(device);
                SaveConfig();
                
                Logger.Info($"Added device to favorites: {device.Name}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to add favorite device: {device.Name}", ex);
            }
        }

        /// <summary>
        /// 移除收藏设备
        /// </summary>
        public void RemoveFavoriteDevice(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
                return;

            try
            {
                var device = _config.FavoriteDevices
                    .Find(d => d.Id == deviceId);

                if (device != null)
                {
                    _config.FavoriteDevices.Remove(device);
                    SaveConfig();
                    
                    Logger.Info($"Removed device from favorites: {device.Name}");
                }
                else
                {
                    Logger.Warning($"Device not found in favorites: {deviceId}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to remove favorite device: {deviceId}", ex);
            }
        }

        /// <summary>
        /// 获取配置文件路径
        /// </summary>
        public string GetConfigFilePath()
        {
            return _configFilePath;
        }

        /// <summary>
        /// 获取配置目录路径
        /// </summary>
        public string GetConfigDirectory()
        {
            return _configDirectory;
        }

        /// <summary>
        /// 导出配置到指定文件
        /// </summary>
        public bool ExportConfig(string filePath)
        {
            try
            {
                Logger.Info($"Exporting configuration to: {filePath}");

                string json = JsonSerializer.Serialize(_config, _jsonOptions);
                File.WriteAllText(filePath, json);

                Logger.Info("Configuration exported successfully");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to export configuration to: {filePath}", ex);
                return false;
            }
        }

        /// <summary>
        /// 从指定文件导入配置
        /// </summary>
        public bool ImportConfig(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Logger.Error($"Import file not found: {filePath}");
                    return false;
                }

                Logger.Info($"Importing configuration from: {filePath}");

                string json = File.ReadAllText(filePath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions);

                if (config != null)
                {
                    _config = config;
                    SaveConfig();
                    
                    Logger.Info("Configuration imported successfully");
                    return true;
                }
                else
                {
                    Logger.Error("Failed to deserialize imported configuration");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to import configuration from: {filePath}", ex);
                return false;
            }
        }

        /// <summary>
        /// 验证配置有效性
        /// </summary>
        public bool ValidateConfig()
        {
            try
            {
                bool isValid = true;

                // 验证电量检查间隔
                if (_config.BatteryCheckInterval < 10)
                {
                    Logger.Warning("BatteryCheckInterval too small, adjusting to 10 seconds");
                    _config.BatteryCheckInterval = 10;
                    isValid = false;
                }
                else if (_config.BatteryCheckInterval > 3600)
                {
                    Logger.Warning("BatteryCheckInterval too large, adjusting to 3600 seconds");
                    _config.BatteryCheckInterval = 3600;
                    isValid = false;
                }

                // 验证低电量阈值
                if (_config.LowBatteryThreshold < 0 || _config.LowBatteryThreshold > 100)
                {
                    Logger.Warning("Invalid LowBatteryThreshold, adjusting to 20%");
                    _config.LowBatteryThreshold = 20;
                    isValid = false;
                }

                // 验证重连次数
                if (_config.MaxReconnectAttempts < 0)
                {
                    Logger.Warning("Invalid MaxReconnectAttempts, adjusting to 3");
                    _config.MaxReconnectAttempts = 3;
                    isValid = false;
                }
                else if (_config.MaxReconnectAttempts > 10)
                {
                    Logger.Warning("MaxReconnectAttempts too large, adjusting to 10");
                    _config.MaxReconnectAttempts = 10;
                    isValid = false;
                }

                if (!isValid)
                {
                    SaveConfig();
                }

                return isValid;
            }
            catch (Exception ex)
            {
                Logger.Error("Error validating configuration", ex);
                return false;
            }
        }
    }
}