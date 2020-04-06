using System;
using System.Collections.Generic;
using System.Text;

namespace MarketBot.Models
{
    public class Configuration
    {
        /// <summary>
        /// API Key
        /// </summary>
        public string Key { get; set; }

        public string Currency { get; set; } = "USD";

        /// <summary>
        /// Interval in milliseconds to check all items if a good price is available
        /// </summary>
        public double CheckInterval { get; set; } = 2000;

        public bool EnablePing { get; set; }

        public List<ItemConfiguration> Entries { get; set; } = new List<ItemConfiguration>();
    }
}
