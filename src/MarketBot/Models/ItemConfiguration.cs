using MarketBot.Helper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace MarketBot.Models
{
    public class ItemConfiguration
    {
        /// <summary>
        /// Hash name of the item
        /// </summary>
        public string HashName { get; set; }

        /// <summary>
        /// Maximum price to buy
        /// </summary>
        public double? MaxPrice { get; set; }

        /// <summary>
        /// Buy mode
        /// </summary>
        public BuyMode Mode { get; set; }

        /// <summary>
        /// True = Config for this item is active
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Maximum quantity to buy; Disable this configuration when the quantity is reached
        /// </summary>
        public int? MaxQuantity { get; set; }

        /// <summary>
        /// Steam account that should be used instead of the main account linked to market.csgo.com
        /// </summary>
        public List<SteamAccount> AltAccounts { get; set; } = new List<SteamAccount>();
    }

    public class SteamAccount
    {
        /// <summary>
        /// Steam 64 ID
        /// </summary>
        public long SteamID64 { get; set; }

        /// <summary>
        /// Steam 32 ID
        /// </summary>
        [JsonIgnore]
        public int SteamID32 => SteamHelper.GetSteamID32(SteamID64);

        /// <summary>
        /// Trade token
        /// </summary>
        public string Token { get; set; }
    }

    public enum BuyMode
    {
        /// <summary>
        /// Ignore the average price, always buy when Price <= MaxPrice
        /// </summary>
        IgnoreAveragePrice,

        /// <summary>
        /// Use the average price when it is smaller than the configured MaxPrice
        /// </summary>
        ConsiderAveragePrice,

        /// <summary>
        /// Ignore the configured MaxPrice, always buy when price is <= Average Price
        /// </summary>
        UseAveragePrice,
    }
}
