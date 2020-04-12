using MarketAPI;
using MarketAPI.Models;
using MarketBot.Data;
using MarketBot.Helper;
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
using MarketBot.Extension;

namespace MarketBot
{
    public class MarketBuyService
    {
        private const int MaxInventorySize = 1000;
        private const int CSGOGameID = 730;
        private const double DefaultInventoryCountInterval = (1 * 60 * 1000);

        private Service _service;
        private WebClient _steamClient = new WebClient("https://steamcommunity.com/", true);

        private List<TimerHelper> _timers = new List<TimerHelper>();

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

                ConfigService.Instance.OnConfigUpdated += OnConfigUpdated;

                _service = new Service(ConfigService.Instance.Configuration.Key);
                if (!await _service.Init())
                {
                    Logger.LogToConsole(LogType.Error, "Failed initialising MarketAPI. Is the ApiKey correct?");
                    return false;
                }

                await _timers.AddEntry(new TimerHelper(DefaultInventoryCountInterval, CountCSGOInventoryAsync, true)).RunActionAsync(); // Check inventory size n milliseconds

                LogToConsole(LogType.Information, "Prefilling average item prices");
                await _timers.AddEntry(new TimerHelper(2 * 60 * 1000, UpdateAverageItemPriceListAsync, true)).RunActionAsync(); // Get average prices every 2 minutes

                _timers.Add(new TimerHelper(ConfigService.GetConfig().CheckInterval, BuyItemsAsync, true)); // Buy items every n milliseconds
                _timers.Add(new TimerHelper(1 * 60 * 1000, UpdateBalanceAsync, true)); // Get current balance every minute


                if (ConfigService.GetConfig().EnablePing)
                {
                    Logger.LogToConsole(LogType.Information, "Activating Selling/Autopurchase");
                    _timers.Add(new TimerHelper(3 * 61 * 1000, HandlePingAsync, true));
                }

                _serviceIsStarting = false;
                return _serviceIsStarted = true;
            }

            return false;
        }
        public async Task<bool> StopAsync()
        {
            if (_serviceIsStarted)
            {
                _serviceIsStarted = false;
                LogToConsole(LogType.Information, "Stopping Service");

                ConfigService.Instance.OnConfigUpdated -= OnConfigUpdated;

                var taskList = new List<Task>();
                foreach (var timer in _timers)
                {
                    if (timer.IsEnabled)
                    {
                        var stopTask = timer.StopAsync();
                        stopTask.Start();
                        taskList.Add(stopTask);
                    }
                }

                await Task.WhenAll(taskList.ToArray());
                return true;
            }

            return false;
        }

        private async void OnConfigUpdated(object sender, FileSystemEventArgs e)
        {
            if (sender is FileSystemWatcher watcher)
            {
                watcher.EnableRaisingEvents = false;

                await StopAsync();
                await StartAsync();

                watcher.EnableRaisingEvents = true;
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

                        LogToConsole(LogType.Information, "Bought '" + itemConfig.HashName + "' at " + (response?.Price ?? 0) + " " + _service.Currency + quantityLeftString);
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

            var timer = _timers.FirstOrDefault(a => a.Action == CountCSGOInventoryAsync);
            if (timer != null)
            {
                if (_currentInventorySize > MaxInventorySize * 0.95 && timer.Interval == DefaultInventoryCountInterval)
                {
                    LogToConsole(LogType.Warning, "Inventory is 95% filled!");
                    // Inventory 95% filled. Check size every 10 seconds from now
                    timer.Interval = 10 * 1000;
                }
                else if (timer.Interval != DefaultInventoryCountInterval && _currentInventorySize < MaxInventorySize * 0.95)
                {
                    LogToConsole(LogType.Warning, "Inventory was emptied.");
                    // Inventory was emptied. Reset interval to default value
                    timer.Interval = DefaultInventoryCountInterval;
                }
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
