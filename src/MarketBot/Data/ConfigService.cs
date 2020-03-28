using MarketBot.Models;
using Newtonsoft.Json;
using SmartWebClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace MarketBot.Data
{
    internal class ConfigService
    {
        #region Statics

        private const string ConfigFile = "config.json";

        #endregion

        #region Fields

        internal event FileSystemEventHandler OnConfigUpdated;

        private static ConfigService _instance;

        private FileSystemWatcher _fileWatcher;

        private Configuration _configuration;

        #endregion

        #region Properties

        internal Configuration Configuration
        {
            get
            {
                if (ConfigIsInitialized && _configuration != null)
                {
                    return _configuration;
                }
                else
                {
                    if (LoadConfig())
                    {
                        return _configuration;
                    }
                    else
                    {
                        return default;
                    }
                }
            }
        }

        internal static ConfigService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ConfigService();
                }

                return _instance;
            }
        }

        internal bool ConfigIsInitialized { get; private set; }

        #endregion

        #region Constructors

        private ConfigService()
        {

        }

        #endregion

        #region Methods

        internal static Configuration GetConfig()
        {
            return Instance.Configuration ?? new Configuration();
        }

        internal bool LoadConfig()
        {
            if (File.Exists(ConfigFile))
            {
                var configString = File.ReadAllText(ConfigFile);
                var config = JsonConvert.DeserializeObject<Configuration>(configString);
                if (config?.Entries?.Count > 0)
                {
                    _configuration = config;

                    // Init FileWatcher only the first time
                    if (_fileWatcher == null)
                    {
                        InitFileWatcher();
                    }

                    return ConfigIsInitialized = true;
                }
                return ConfigIsInitialized = false;
            }
            else
            {
                CreateDummyConfig();
                return ConfigIsInitialized = false;
            }
        }

        internal void CreateDummyConfig()
        {
            var dummyConfig = new Configuration()
            {
                Key = "YourApiKeyHere",
                CheckInterval = 500,
                Entries = new List<ItemConfiguration>()
                {
                    new ItemConfiguration()
                    {
                        HashName = "Chroma 3 Case",
                        MaxPrice = 0.015,
                        MaxQuantity = null,
                        Mode = BuyMode.ConsiderAveragePrice,
                        IsActive = false
                    }
                }
            };

            Logger.LogToConsole(Logger.LogType.Information, "Creating dummy config file.");
            var jsonString = JsonConvert.SerializeObject(dummyConfig, Formatting.Indented);
            File.WriteAllText(ConfigFile, jsonString);
        }

        private void InitFileWatcher()
        {
            var currentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _fileWatcher = new FileSystemWatcher(currentDirectory, ConfigFile);
            _fileWatcher.Changed += (o, e) =>
            {
                if (ConfigIsInitialized)
                {
                    ConfigIsInitialized = false;
                    OnConfigUpdated(o, e);
                }
            };
            _fileWatcher.EnableRaisingEvents = true;
        }

        #endregion
    }
}
