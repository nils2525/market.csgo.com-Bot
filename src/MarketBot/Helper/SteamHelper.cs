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
    public static class SteamHelper
    {
        private const long SteamIDConversionNumber = 76561197960265728;

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
