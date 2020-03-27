using MarketAPI;
using MarketAPI.Models;
using MarketBot.Data;
using MarketBot.Models;
using Newtonsoft.Json.Linq;
using SmartWebClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

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

        private bool _buyItemsAlreadyRunning = false;
        private bool _getAveragePriceAlreadyRunning = false;
        private bool _countInventoryAlreadyRunning = false;

        private Dictionary<string, double> _averageItemPrices = new Dictionary<string, double>();
        private int _currentInventorySize;

        private readonly Dictionary<BuyMode, Func<ItemConfiguration, List<ItemData>, List<ItemData>>> _buyModeFunctions = new Dictionary<BuyMode, Func<ItemConfiguration, List<ItemData>, List<ItemData>>>();

        public MarketBuyService()
        {
            _buyModeFunctions.Add(BuyMode.ConsiderAveragePrice, GetItemsToBuyConsiderAveragePrice);
            _buyModeFunctions.Add(BuyMode.IgnoreAveragePrice, GetItemsToBuyIgnoreAveragePrice);
            _buyModeFunctions.Add(BuyMode.UseAveragePrice, GetItemsToBuyUseAveragePrice);
        }

        public async Task<bool> Start()
        {
            if (_buyItemsTimer == null)
            {
                if (!ConfigService.Instance.ConfigIsInitialized)
                {
                    if (!ConfigService.Instance.LoadConfig())
                    {
                        Console.WriteLine("Warning: Failed loading config");
                        return false;
                    }
                }

                Console.WriteLine("Info: Starting Service");

                ConfigService.Instance.OnConfigUpdated += async (o, e) =>
                {
                    Stop();
                    await Start();
                };

                _service = new Service(ConfigService.Instance.Configuration.Key);

                _countInventoryTimer = new System.Timers.Timer(DefaultInventoryCountInterval); // Check inventory size n milliseconds
                _countInventoryTimer.Elapsed += CountInventoryTimer_Elapsed;
                _countInventoryTimer.Start();

                await CountCSGOInventoryAsync();

                Console.WriteLine("Info: Prefilling average item prices");
                await UpdateAverageItemPriceListAsync();

                _buyItemsTimer = new System.Timers.Timer(ConfigService.GetConfig().CheckInterval); // Buy items n milliseconds
                _buyItemsTimer.Elapsed += BuyItemsTimer_Elapsed;
                _buyItemsTimer.Start();

                _getAveragePriceTimer = new System.Timers.Timer(2 * 60 * 1000); // Get average prices every 2 minutes
                _getAveragePriceTimer.Elapsed += GetAveragePriceTimer_Elapsed;
                _getAveragePriceTimer.Start();

                return true;
            }

            return false;
        }

        public bool Stop()
        {
            if (_buyItemsTimer != null)
            {
                Console.WriteLine("Info: Stopping Service");

                _buyItemsTimer.Stop();
                _buyItemsTimer.Elapsed -= BuyItemsTimer_Elapsed;
                _buyItemsTimer = null;

                _getAveragePriceTimer.Stop();
                _getAveragePriceTimer.Elapsed -= GetAveragePriceTimer_Elapsed;
                _getAveragePriceTimer = null;

                _countInventoryTimer.Stop();
                _countInventoryTimer.Elapsed -= CountInventoryTimer_Elapsed;
                _countInventoryTimer = null;

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
                Console.WriteLine("Warning: Inventory is full!");
                return;
            }

            var activeItems = ConfigService.GetConfig().Entries.Where(c => c.IsActive).ToList();
            if (activeItems.Count > 0)
            {
                var currentItemInfos = await _service.GetItemsAsync(activeItems.Select(c => c.HashName).ToList());
                Console.Write(".");

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
                    var response = await _service.BuyItemAsync(itemToBuy.ID, itemToBuy.Price);
                    Console.WriteLine("Info: Bought '" + itemConfig.HashName + "' at " + (response?.Price ?? 0) + " " + ConfigService.GetConfig().Currency + ". Successfully = " + (response?.IsSuccessfully ?? false));
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
                    Console.WriteLine("Warning: Average price for item '" + itemConfig.HashName + "' not available");
                }
            }

            return itemsToBuy;
        }

        private List<ItemData> GetItemsToBuyIgnoreAveragePrice(ItemConfiguration itemConfig, List<ItemData> currentInfo)
        {
            if (itemConfig.MaxPrice <= 0)
            {
                Console.WriteLine("Warning: Max price for item '" + itemConfig.HashName + "' not set.");
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
                    Console.WriteLine("Warning: Average price for item '" + itemConfig.HashName + "' not available");
                }
            }

            return itemsToBuy;
        }

        private async Task CountCSGOInventoryAsync()
        {
            var steamID = await _service.GetMySteamIDAsync();
            JObject csgoInventory;

            try
            {
                csgoInventory = await _steamClient.GetObjectAsync<JObject>("profiles/" + steamID.SteamID64 + "/inventory/json/" + CSGOGameID + "/2");
            }
            catch (Exception)
            {
                csgoInventory = null;
            }

            var newInventorySize = csgoInventory?["rgInventory"]?.Count() ?? 0;
            if (newInventorySize == 0)
            {
                Console.WriteLine("Warning: Failed to load steam inventory size");
                return;
            }

            if (_currentInventorySize == 0)
            {
                // Display size only on startup
                Console.WriteLine("Info: Steam inventory size " + newInventorySize);
            }

            _currentInventorySize = newInventorySize;

            if (_currentInventorySize > MaxInventorySize * 0.95 && _countInventoryTimer.Interval == DefaultInventoryCountInterval)
            {
                Console.WriteLine("Warning: Inventory is 95% filled!");
                // Inventory 95% filled. Check size every 10 seconds from now
                _countInventoryTimer.Interval = 10 * 1000;
            }
            else if (_countInventoryTimer.Interval != DefaultInventoryCountInterval && _currentInventorySize < MaxInventorySize * 0.95)
            {
                Console.WriteLine("Info: Inventory was emptied.");
                // Inventory was emptied. Reset interval to default value
                _countInventoryTimer.Interval = DefaultInventoryCountInterval;
            }

        }
    }
}
