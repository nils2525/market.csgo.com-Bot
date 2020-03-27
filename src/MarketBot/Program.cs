using MarketBot.Data;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MarketBot
{
    class Program
    {
        static object lo = new object();
        static async Task Main(string[] args)
        {
            var buyService = new MarketBuyService();
            if (await buyService.Start())
            {
                Thread.Sleep(-1);
            }
            else
            {
                Console.WriteLine("Starting the service failed.");
            }
        }
    }
}
