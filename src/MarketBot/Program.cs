using MarketBot.Data;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static SmartWebClient.Logger;

namespace MarketBot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var buyService = new MarketBuyService();
            if (await buyService.StartAsync())
            {
                Thread.Sleep(-1);
            }
            else
            {
                LogToConsole(LogType.Error, "Starting the service failed.");
            }
        }
    }
}
