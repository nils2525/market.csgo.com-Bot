using MarketBot.Data;
using System;
using System.Collections.Generic;
using System.Threading;

namespace MarketBot
{
    class Program
    {
        static void Main(string[] args)
        {
            var buyService = new MarketBuyService();
            if (buyService.Start())
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
