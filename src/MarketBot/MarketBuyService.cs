﻿using MarketAPI;
using MarketAPI.Models;
using MarketBot.Data;
using MarketBot.Models;
using Newtonsoft.Json.Linq;
using SmartWebClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using static SmartWebClient.Logger;

namespace MarketBot
{
    public class MarketBuyService
    {
        private const int MaxInventorySize = 1000;
        private const int CSGOGameID = 730;
        private const double DefaultInventoryCountInterval = (1 * 60 * 1000);

        private Service _service;
        private WebClient _steamClient = new WebClient("https://steamcommunity.com/", true);

        private System.Timers.Timer _buyItemsTimer;
        private System.Timers.Timer _getAveragePriceTimer;
        private System.Timers.Timer _countInventoryTimer;
        private System.Timers.Timer _getBalanceTimer;
        private System.Timers.Timer _pingTimer;

        private bool _buyItemsAlreadyRunning = false;
        private bool _getAveragePriceAlreadyRunning = false;
        private bool _countInventoryAlreadyRunning = false;
        private bool _getBalanceAlreadyRunning = false;
        private bool _isAlreadyPinging = false;

        private bool _serviceIsStarted;
        private bool _serviceIsStarting;

        private Dictionary<string, double> _averageItemPrices = new Dictionary<string, double>();
        private int _currentInventorySize;
        private double _marketBalance = 0;

        private readonly Dictionary<BuyMode, Func<ItemConfiguration, List<ItemData>, List<ItemData>>> _buyModeFunctions = new Dictionary<BuyMode, Func<ItemConfiguration, List<ItemData>, List<ItemData>>>();

        public MarketBuyService()
        {
            _buyModeFunctions.Add(BuyMode.ConsiderAveragePrice, GetItemsToBuyConsiderAveragePrice);
            _buyModeFunctions.Add(BuyMode.IgnoreAveragePrice, GetItemsToBuyIgnoreAveragePrice);
            _buyModeFunctions.Add(BuyMode.UseAveragePrice, GetItemsToBuyUseAveragePrice);
        }

        public async Task<bool> StartAsync()
        {
            if (!_serviceIsStarted && !_serviceIsStarting)
            {
                _serviceIsStarting = true;

                if (!ConfigService.Instance.ConfigIsInitialized)
                {
                    if (!ConfigService.Instance.LoadConfig())
                    {
                        LogToConsole(LogType.Warning, "Failed loading config");
                        return false;
                    }
                }

                LogToConsole(LogType.Information, "Starting Service");

                ConfigService.Instance.OnConfigUpdated += async (o, e) =>
                {
                    if (o is FileSystemWatcher watcher)
                    {
                        watcher.EnableRaisingEvents = false;

                        await StopAsync();
                        await StartAsync();

                        watcher.EnableRaisingEvents = true;
                    }
                };

                _service = new Service(ConfigService.Instance.Configuration.Key);

                _countInventoryTimer = new System.Timers.Timer(DefaultInventoryCountInterval); // Check inventory size n milliseconds
                _countInventoryTimer.Elapsed += CountInventoryTimer_Elapsed;
                _countInventoryTimer.Start();

                await CountCSGOInventoryAsync();

                LogToConsole(LogType.Information, "Prefilling average item prices");
                await UpdateAverageItemPriceListAsync();

                _buyItemsTimer = new System.Timers.Timer(ConfigService.GetConfig().CheckInterval); // Buy items n milliseconds
                _buyItemsTimer.Elapsed += BuyItemsTimer_Elapsed;
                _buyItemsTimer.Start();

                _getAveragePriceTimer = new System.Timers.Timer(2 * 60 * 1000); // Get average prices every 2 minutes
                _getAveragePriceTimer.Elapsed += GetAveragePriceTimer_Elapsed;
                _getAveragePriceTimer.Start();

                _getBalanceTimer = new System.Timers.Timer(1 * 60 * 1000); // Get current balance every minute
                _getBalanceTimer.Elapsed += GetBalanceTimer_Elapsed;
                _getBalanceTimer.Start();

                if (ConfigService.GetConfig().EnablePing)
                {
                    Logger.LogToConsole(LogType.Information, "Activating Selling/Autopurchase");
                    _pingTimer = new System.Timers.Timer(3 * 61 * 1000); // Ping every 3min 3sec
                    _pingTimer.Elapsed += PingTimer_Elapsed;
                    _pingTimer.Start();
                }

                _serviceIsStarting = false;
                return _serviceIsStarted = true;
            }

            return false;
        }

        private async void PingTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!_isAlreadyPinging)
            {
                _isAlreadyPinging = true;
                await HandlePingAsync();
                _isAlreadyPinging = false;
            }
        }

        private async void GetBalanceTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!_getBalanceAlreadyRunning)
            {
                _getBalanceAlreadyRunning = true;
                await UpdateBalanceAsync();
                _getBalanceAlreadyRunning = false;
            }
        }

        public async Task<bool> StopAsync()
        {
            if (_serviceIsStarted)
            {
                _serviceIsStarted = false;
                LogToConsole(LogType.Information, "Stopping Service");

                _buyItemsTimer.Stop();
                _buyItemsTimer.Elapsed -= BuyItemsTimer_Elapsed;
                _buyItemsTimer = null;

                _getAveragePriceTimer.Stop();
                _getAveragePriceTimer.Elapsed -= GetAveragePriceTimer_Elapsed;
                _getAveragePriceTimer = null;

                _countInventoryTimer.Stop();
                _countInventoryTimer.Elapsed -= CountInventoryTimer_Elapsed;
                _countInventoryTimer = null;

                _getBalanceTimer.Stop();
                _getBalanceTimer.Elapsed -= GetBalanceTimer_Elapsed;
                _getBalanceTimer = null;

                if(_pingTimer?.Enabled ?? false)
                {
                    _pingTimer.Stop();
                    _pingTimer.Elapsed -= PingTimer_Elapsed;
                    _pingTimer = null;
                }

                while(_buyItemsAlreadyRunning || _countInventoryAlreadyRunning || _getAveragePriceAlreadyRunning || _getBalanceAlreadyRunning || _isAlreadyPinging)
                {
                    await Task.Delay(250);
                }

                return true;
            }

            return false;
        }

        private async void CountInventoryTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!_countInventoryAlreadyRunning)
            {
                _countInventoryAlreadyRunning = true;
                await CountCSGOInventoryAsync();
                _countInventoryAlreadyRunning = false;
            }
        }

        private async void GetAveragePriceTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!_getAveragePriceAlreadyRunning)
            {
                _getAveragePriceAlreadyRunning = true;
                await UpdateAverageItemPriceListAsync();
                _getAveragePriceAlreadyRunning = false;
            }
        }

        private async Task UpdateAverageItemPriceListAsync()
        {
            var itemList = ConfigService.GetConfig().Entries.Select(c => c.HashName).ToList();
            var newPrices = await _service.GetItemHistoryAsync(itemList);

            if (newPrices?.Data?.Count > 0)
            {
                lock (_averageItemPrices)
                {
                    _averageItemPrices = new Dictionary<string, double>();
                    foreach (var newPrice in newPrices.Data)
                    {
                        _averageItemPrices.Add(newPrice.Key, newPrice.Value.AveragePrice);
                    }
                }
            }
        }

        private async void BuyItemsTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!_buyItemsAlreadyRunning)
            {
                _buyItemsAlreadyRunning = true;
                await BuyItemsAsync();
                //await BuyItemsAsync();
                _buyItemsAlreadyRunning = false;
            }
        }

        private async Task BuyItemsAsync()
        {
            if (_currentInventorySize >= MaxInventorySize)
            {
                LogToConsole(LogType.Warning, "Inventory is full!");
                return;
            }

            var activeItems = ConfigService.GetConfig().Entries.Where(c => c.IsActive).ToList();
            if (activeItems.Count > 0)
            {
                var currentItemInfos = await _service.GetItemsAsync(activeItems.Select(c => c.HashName).ToList());
                LogToConsole(LogType.None, ".", false);

                if (currentItemInfos?.Data?.Count < 1)
                {
                    Thread.Sleep(10000);
                }

                foreach (var item in activeItems)
                {
                    var currentIntemInfo = currentItemInfos?.Data?.Where(c => c.Key == item.HashName)?.FirstOrDefault().Value;
                    await BuyItemAsync(item, currentIntemInfo);
                }
            }
        }

        private async Task BuyItemAsync(ItemConfiguration itemConfig, List<ItemData> currentInfo)
        {
            if (currentInfo?.Count > 0)
            {
                var buyFunction = _buyModeFunctions[itemConfig.Mode];
                var itemsToBuy = buyFunction(itemConfig, currentInfo);

                foreach (var itemToBuy in itemsToBuy)
                {
                    if (_marketBalance < itemToBuy.Price)
                    {
                        LogToConsole(LogType.Warning, "Skipping purchase, as the balance is not sufficient.");
                        continue;
                    }

                    var response = await _service.BuyItemAsync(itemToBuy.ID, itemToBuy.Price);
                    if (response?.IsSuccessfully ?? false)
                    {
                        string quantityLeftString = "";
                        if (itemConfig?.MaxQuantity > 0)
                        {
                            itemConfig.MaxQuantity = itemConfig.MaxQuantity - 1;
                            quantityLeftString = ". (" + itemConfig.MaxQuantity + " left)";
                            if (itemConfig.MaxQuantity == 0)
                            {
                                itemConfig.IsActive = false;
                            }

                            // Update new Quantity/IsActive settings to config file
                            ConfigService.Instance.SaveConfig();
                        }

                        LogToConsole(LogType.Information, "Bought '" + itemConfig.HashName + "' at " + (response?.Price ?? 0) + " " + ConfigService.GetConfig().Currency + quantityLeftString);
                    }
                }
            }
        }

        private List<ItemData> GetItemsToBuyUseAveragePrice(ItemConfiguration itemConfig, List<ItemData> currentInfo)
        {
            var itemsToBuy = new List<ItemData>();

            if (_averageItemPrices?.Count > 0)
            {
                var averagePrice = _averageItemPrices[itemConfig.HashName];
                if (averagePrice != 0)
                {
                    itemsToBuy = currentInfo.Where(c => c.Price <= averagePrice).ToList();
                }
                else
                {
                    LogToConsole(LogType.Warning, "Average price for item '" + itemConfig.HashName + "' not available");
                }
            }

            return itemsToBuy;
        }

        private List<ItemData> GetItemsToBuyIgnoreAveragePrice(ItemConfiguration itemConfig, List<ItemData> currentInfo)
        {
            if (itemConfig.MaxPrice <= 0)
            {
                LogToConsole(LogType.Warning, "Max price for item '" + itemConfig.HashName + "' not set");
                return new List<ItemData>();
            }

            return currentInfo.Where(c => c.Price <= itemConfig.MaxPrice).ToList();
        }

        private List<ItemData> GetItemsToBuyConsiderAveragePrice(ItemConfiguration itemConfig, List<ItemData> currentInfo)
        {
            var itemsToBuy = new List<ItemData>();

            if (_averageItemPrices?.Count > 0)
            {
                var averagePrice = _averageItemPrices.Where(c => c.Key == itemConfig.HashName).FirstOrDefault().Value;
                if (averagePrice != 0)
                {
                    var maxPrice = averagePrice < itemConfig.MaxPrice ? averagePrice : itemConfig.MaxPrice;
                    itemsToBuy = currentInfo.Where(c => c.Price <= maxPrice).ToList();
                }
                else
                {
                    LogToConsole(LogType.Warning, "Average price for item '" + itemConfig.HashName + "' not available");
                }
            }

            return itemsToBuy;
        }

        private async Task CountCSGOInventoryAsync()
        {
            var steamID = await _service.GetMySteamIDAsync();
            int newInventorySize = 0;

            try
            {
                var csgoInventory = await _steamClient.GetObjectAsync<JObject>("profiles/" + steamID.SteamID64 + "/inventory/json/" + CSGOGameID + "/2");
                newInventorySize = csgoInventory?["rgInventory"]?.Count() ?? 0;
            }
            catch (Exception)
            {

            }

            if (newInventorySize == 0)
            {
                LogToConsole(LogType.Warning, "Failed to load steam inventory size");
                return;
            }

            if (_currentInventorySize == 0)
            {
                // Display size only on startup
                LogToConsole(LogType.Information, "Steam inventory size " + newInventorySize);
            }

            _currentInventorySize = newInventorySize;

            if (_currentInventorySize > MaxInventorySize * 0.95 && _countInventoryTimer.Interval == DefaultInventoryCountInterval)
            {
                LogToConsole(LogType.Warning, "Inventory is 95% filled!");
                // Inventory 95% filled. Check size every 10 seconds from now
                _countInventoryTimer.Interval = 10 * 1000;
            }
            else if (_countInventoryTimer.Interval != DefaultInventoryCountInterval && _currentInventorySize < MaxInventorySize * 0.95)
            {
                LogToConsole(LogType.Warning, "Inventory was emptied.");
                // Inventory was emptied. Reset interval to default value
                _countInventoryTimer.Interval = DefaultInventoryCountInterval;
            }

        }

        private async Task UpdateBalanceAsync()
        {
            var newBalance = await _service.GetBalanceAsync();
            if (newBalance != null)
            {
                _marketBalance = newBalance.Balance;
                if (_marketBalance < 1)
                {
                    LogToConsole(LogType.Warning, "Balance is at " + newBalance.Balance + newBalance.Currency);
                }
            }
            else
            {
                LogToConsole(LogType.Error, "GetBalance failed.");
            }
        }

        private async Task HandlePingAsync()
        {
            var pingResult = await _service.PingAsync();
        }
    }
}
