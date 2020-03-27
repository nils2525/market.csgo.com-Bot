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

        /// <summary>
        /// Interval in milliseconds to check all items if a good price is available
        /// </summary>
        public int CheckInterval { get; set; } = 2000;

        public List<ItemConfiguration> Entries { get; set; } = new List<ItemConfiguration>();
    }
}
