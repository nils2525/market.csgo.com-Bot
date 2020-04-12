using System;
using System.Collections.Generic;
using System.Text;

namespace MarketBot.Extension
{
    public static class ListExtensions
    {
        public static T AddEntry<T>(this List<T> list, T entry)
        {
            list.Add(entry);
            return entry;
        }
    }
}
