using Newtonsoft.Json.Linq;
using SmartWebClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SmartWebClient.Logger;
using static MarketBot.Helper.LoggerHelper;

namespace MarketBot.Helper
{
    public class InventoryMonitor
    {
        private const int CSGOGameID = 730;
        public const int MaxCSGOInventorySize = 1000;
        private const double DefaultInventoryCountInterval = (2 * 60 * 1000);

        private WebClient _steamClient = new WebClient("https://steamcommunity.com/", true);
        private TimerHelper _timer;

        public long SteamID64 { get; }
        public int InventorySize { get; set; }

        private InventoryMonitor(long steamID64)
        {
            SteamID64 = steamID64;
            _timer = new TimerHelper(DefaultInventoryCountInterval, HandleInventoryUpdateAsync, true);
        }

        public static InventoryMonitor StartNewService(long steamID64)
        {
            return new InventoryMonitor(steamID64);
        }

        public async Task<bool> StopAsync()
        {
            return await _timer.StopAsync();
        }

        private async Task HandleInventoryUpdateAsync()
        {
            var newSize = await GetInventorySizeAsync(SteamID64);

            if (SendInventorySizeMessage(InventorySize, newSize))
            {                
                WriteLog(LogType.Information, "Steam inventory size " + newSize);
            }

            InventorySize = newSize;

            if (InventorySize > MaxCSGOInventorySize * 0.95 && _timer.Interval == DefaultInventoryCountInterval)
            {
                WriteLog(LogType.Warning, "Inventory is 95% filled!");
                // Inventory 95% filled. Check size every 10 seconds from now
                _timer.Interval = 10 * 1000;
            }
            else if (_timer.Interval != DefaultInventoryCountInterval && InventorySize < MaxCSGOInventorySize * 0.95)
            {
                WriteLog(LogType.Warning, "Inventory was emptied.");
                // Inventory was emptied. Reset interval to default value
                _timer.Interval = DefaultInventoryCountInterval;
            }
        }

        public async Task<int> GetInventorySizeAsync()
        {
            await _timer.RunActionAsync();
            return InventorySize;
        }

        private async Task<int> GetInventorySizeAsync(long steamID64)
        {
            int inventorySize = 0;

            try
            {
                var csgoInventory = await _steamClient.GetObjectAsync<JObject>("profiles/" + steamID64 + "/inventory/json/" + CSGOGameID + "/2");
                inventorySize = csgoInventory?["rgInventory"]?.Count() ?? 0;
            }
            catch (Exception)
            {

            }

            return inventorySize;

        }

        private bool SendInventorySizeMessage(int oldSize, int newSize)
        {
            if (InventorySize == 0)
            {
                // Display size on startup
                return true;
            }

            for (int i = 100; i < 1000; i += 100)
            {
                if (oldSize < i && newSize >= i)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
