using Newtonsoft.Json.Linq;
using SmartWebClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SmartWebClient.Logger;

namespace MarketBot.Helper
{
    public class SteamHelper
    {
        public const int MaxCSGOInventorySize = 1000;
        private const int CSGOGameID = 730;
        private const long SteamIDConversionNumber = 76561197960265728;

        private WebClient _steamClient = new WebClient("https://steamcommunity.com/", true);

        public async Task<int> GetCSGOInventorySizeAsync(int steamID64)
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

        public static long GetSteamID64(int steamID32)
        {
            return steamID32 + SteamIDConversionNumber;

        }

        public static int GetSteamID32(long steamID64)
        {
            return (int)(steamID64 - SteamIDConversionNumber);
        }
    }
}
