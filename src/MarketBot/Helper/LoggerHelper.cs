using System;
using System.Collections.Generic;
using System.Text;
using static SmartWebClient.Logger;

namespace MarketBot.Helper
{
    internal class LoggerHelper
    {
        internal static TelegramHelper TelegramHelper { get; set; }
        internal static void WriteConsoleLog(LogType type, string message, bool newLine = true)
        {
            LogToConsole(type, message, newLine);
        }

        internal static void WriteLog(LogType type, string message, bool sendToTelegram = true)
        {
            WriteConsoleLog(type, message);
            if (sendToTelegram && TelegramHelper != null)
            {
                TelegramHelper.SendSimpleMessage(type.ToString() + ": " + message);
            }
        }

        internal static void WriteLog(Exception ex)
        {
            WriteLog(LogType.Error, ex.ToString(), true);
        }

    }
}
