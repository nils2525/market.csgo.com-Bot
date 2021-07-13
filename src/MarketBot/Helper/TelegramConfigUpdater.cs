using MarketBot.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace MarketBot.Helper
{
    internal class TelegramConfigUpdater
    {
        private TelegramHelper _telegram;
        private bool _isStarted;

        private Models.ItemConfiguration _selectedItemToEdit;
        private ItemEditType? _selectedItemEditType;

        internal TelegramConfigUpdater(TelegramHelper telegram)
        {
            _telegram = telegram;

        }


        internal void Start()
        {
            if (!_isStarted)
            {
                _telegram.OnReceivedMessage += OnReceivedMessageAsync;
                _isStarted = true;
            }
        }

        internal void Stop()
        {
            if (_isStarted)
            {
                _telegram.OnReceivedMessage -= OnReceivedMessageAsync;
                _isStarted = false;
            }
        }


        private async void OnReceivedMessageAsync(object sender, string message)
        {
            if (message.StartsWith("/edititem"))
            {
                var messageID = await _telegram.SendRepliableMessageAsync(
                    "Which Item should be edited?",
                    new Dictionary<string, string>(ConfigService.Instance.Configuration.Entries.Select(c =>
                        new KeyValuePair<string, string>(c.HashName, c.HashName))),
                    EditItemCallbackSelectedItem);
            }
            else if (message.StartsWith("x"))
            {

            }
        }

        private async Task EditItemCallbackSelectedItem(CallbackQuery callback)
        {
            _selectedItemToEdit = ConfigService.Instance.Configuration.Entries.FirstOrDefault(c => c.HashName == callback.Data);
            if (_selectedItemToEdit != null)
            {
                var isActiveCommand = _selectedItemToEdit.IsActive ? "Deactivate" : "Activate";
                var messageID = await _telegram.SendRepliableMessageAsync("Which info should be edited?", new Dictionary<string, string>()
                {
                    { isActiveCommand, isActiveCommand },
                    { "Price", "Update Price" },
                    { "Mode", "Update Mode" }
                }, EditItemCallbackSelectType);
            }
        }

        private async Task EditItemCallbackSelectType(CallbackQuery callback)
        {
            if (_selectedItemToEdit != null)
            {
                var data = callback.Data;
                if (data == "Activate")
                {
                    _selectedItemToEdit.IsActive = true;
                    await _telegram.SendSimpleMessageAsync("Activated " + _selectedItemToEdit.HashName);
                    SaveSettings();                    
                }
                else if (data == "Deactivate")
                {
                    _selectedItemToEdit.IsActive = false;
                    await _telegram.SendSimpleMessageAsync("Deactivated " + _selectedItemToEdit.HashName);
                    SaveSettings();
                }
                else if (data == "Price")
                {
                    _selectedItemEditType = ItemEditType.Price;
                    await _telegram.SendRepliableMessageAsync("Enter new target price", EditItemCallbackUpdatePrice);
                }
                else if (data == "Mode")
                {
                    _selectedItemEditType = ItemEditType.BuyMode;

                }
            }
        }

        private async Task EditItemCallbackUpdatePrice(CallbackQuery callback)
        {
            callback.Data = callback.Data.Replace(".", ",");
            if (_selectedItemToEdit != null && double.TryParse(callback.Data, out double result))
            {
                _selectedItemToEdit.MaxPrice = result;
                SaveSettings();
            }
        }

        private void SaveSettings()
        {
            ConfigService.Instance.SaveConfig();
            _selectedItemToEdit = null;
            _selectedItemEditType = null;
        }

        enum ItemEditType
        {
            Price,
            BuyMode,
            MaxQuantity
        }
    }
}
