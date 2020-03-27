﻿using MarketAPI;
using MarketAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace CSGOMarketBuyer
{
    public class MarketBuyerService
    {
        private Service _service;
        private System.Timers.Timer _timer;
        private bool _alreadyRunning = false;

        public Dictionary<string, double> _items { get; set; } = new Dictionary<string, double>();
        public MarketBuyerService(string apiKey)
        {
            _service = new Service(apiKey);
        }

        public bool Start(Dictionary<string, double> items)
        {
            if (items != null)
            {
                _items = items;
            }

            _timer = new System.Timers.Timer(500);
            _timer.Elapsed += Timer_Elapsed;
            _timer.Start();

            return true;
        }

        private async void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!_alreadyRunning)
            {
                _alreadyRunning = true;
                await BuyLowItemsAsync();
                //await BuyItemsAsync();
                _alreadyRunning = false;
            }
        }


        private async Task BuyLowItemsAsync()
        {
            var interesingItems = await _service.GetItemsAsync(_items.Select(c => c.Key).ToList());
            Console.Write(".");
            foreach (var item in _items)
            {
                var itemInPriceList = interesingItems?.Data?.Where(c => c.Key == item.Key)?.FirstOrDefault().Value;
                if (itemInPriceList?.Count > 0)
                {
                    foreach(var itemToBuy in itemInPriceList.Where(c => c.Price > 0 && c.Price <= item.Value))
                    {
                        var response = await _service.BuyItemAsync(itemToBuy.ID, itemToBuy.Price);
                        Console.WriteLine(Environment.NewLine+"BuyItem (" + item.Key + ")| Successfully: " + (response?.IsSuccessfully ?? false) + " - Price: " + (response?.Price ?? 0));
                    }
                }
                else
                {
                    Console.WriteLine(Environment.NewLine+"GetPriceList | Error while loading pricelist. | " + interesingItems?.ErrorMessage);
                    Thread.Sleep(10000);
                }
            }
        }

        private async Task BuyItemsAsync()
        {
            foreach (var item in _items)
            {
                await BuyItemAsync(item.Key, item.Value);
            }
        }

        private async Task BuyItemAsync(string name, double maxPrice)
        {
            GetItemResult item = await _service.GetItemAsync(name);
            var lastData = item?.Data?.FirstOrDefault();
            if (lastData == null)
            {
                Console.WriteLine("CheckItem | Name: " + name + " - Invalid result");
            }
            else
            {
                /*Console.WriteLine("CheckItem | Name: " + name + " - Current price: " + item.Data.First().Price);
                while (lastData?.Price <= maxPrice)
                {
                    var response = await _service.BuyItemAsync(name, lastData.Price);
                    Console.WriteLine("BuyItem | Successfully: " + response?.IsSuccessfully ?? false + " - Price: " + response?.Price);
                    lastData = (await _service.GetItemAsync(name))?.Data?.FirstOrDefault();
                }*/
            }                       
        }
    }
}
