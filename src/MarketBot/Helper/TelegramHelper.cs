using MarketBot.Data;
using SmartWebClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using static MarketBot.Helper.LoggerHelper;

namespace MarketBot.Helper
{
    internal class TelegramHelper
    {
        private int _allowedUser;
        private bool _isActive;
        private TelegramBotClient _client;
        private List<Task> _sendTasks = new List<Task>();

        private Dictionary<int, Func<Telegram.Bot.Types.CallbackQuery, Task>> _callbackActions = new Dictionary<int, Func<Telegram.Bot.Types.CallbackQuery, Task>>();

        public event EventHandler<string> OnReceivedMessage;

        public void Start()
        {
            if (!_isActive)
            {
                var config = ConfigService.Instance?.Configuration;
                _allowedUser = config.TelegramUser;


                _client = new TelegramBotClient(config.TelegramToken);
                var me = _client.GetMeAsync().Result;
                WriteLog(Logger.LogType.Information, $"Telegram bot {me.Username} connected.", false);

                _client.OnMessage += BotClient_OnMessage;
                _client.OnCallbackQuery += BotClient_OnCallbackQuery;
                _client.OnInlineQuery += BotClient_OnInlineQuery;
                _client.OnInlineResultChosen += BotClient_OnInlineResultChosen;

                _client.StartReceiving();

                _isActive = true;
            }
        }

        public void Stop()
        {
            if (_isActive)
            {
                _isActive = false;

                _client.StopReceiving();
                _client.OnMessage -= BotClient_OnMessage;
                _client.OnCallbackQuery -= BotClient_OnCallbackQuery;
                _client.OnInlineQuery -= BotClient_OnInlineQuery;
                _client.OnInlineResultChosen -= BotClient_OnInlineResultChosen;
                _client = null;
            }
        }

        public void AddCallbackAction(int messageID, Func<Telegram.Bot.Types.CallbackQuery, Task> action)
        {
            _callbackActions[messageID] = action;
        }

        public void SendSimpleMessage(string message)
        {
            _sendTasks.Add(Task.Factory.StartNew(async () => await SendSimpleMessageAsync(message)));

            _sendTasks.RemoveAll(c => c.IsCompleted);
        }


        public async Task<int> SendRepliableMessageAsync(string message)
        {
            var sendMessage = await _client.SendTextMessageAsync(new ChatId(_allowedUser), message, replyMarkup: InlineKeyboardMarkup.Empty());
            return sendMessage.MessageId + 1;
        }
        public async Task<int> SendRepliableMessageAsync(string message, Func<Telegram.Bot.Types.CallbackQuery, Task> callback)
        {
            var result = await SendRepliableMessageAsync(message);
            if (callback != null)
            {
                AddCallbackAction(result, callback);
            }

            return result;
        }

        public async Task<int> SendRepliableMessageAsync(string message, Dictionary<string, string> replies)
        {
            var buttons = new List<List<InlineKeyboardButton>>();
            foreach (var reply in replies)
            {
                buttons.Add(new List<InlineKeyboardButton>() { InlineKeyboardButton.WithCallbackData(reply.Value, reply.Key) });
            }

            var markup = new InlineKeyboardMarkup(buttons);
            var sendMessage = await _client.SendTextMessageAsync(new Telegram.Bot.Types.ChatId(_allowedUser), message, replyMarkup: markup);
            return sendMessage.MessageId;
        }
        public async Task<int> SendRepliableMessageAsync(string message, Dictionary<string, string> replies, Func<Telegram.Bot.Types.CallbackQuery, Task> callback)
        {
            var result = await SendRepliableMessageAsync(message, replies);
            if (callback != null)
            {
                AddCallbackAction(result, callback);
            }

            return result;
        }

        public async Task SendSimpleMessageAsync(string message)
        {
            await _client.SendTextMessageAsync(new Telegram.Bot.Types.ChatId(_allowedUser), message);
        }


        private bool AnswerMessage(int userID)
        {
            return _allowedUser > 0 && _isActive && userID == _allowedUser && (ConfigService.Instance?.Configuration?.TelegramActive ?? false);
        }

        #region EventHandlers

        private void BotClient_OnInlineResultChosen(object sender, Telegram.Bot.Args.ChosenInlineResultEventArgs e)
        {
            if (AnswerMessage(e.ChosenInlineResult.From.Id))
            {

            }
        }

        private void BotClient_OnInlineQuery(object sender, Telegram.Bot.Args.InlineQueryEventArgs e)
        {
            if (AnswerMessage(e.InlineQuery.From.Id))
            {

            }
        }

        private async void BotClient_OnCallbackQuery(object sender, Telegram.Bot.Args.CallbackQueryEventArgs e)
        {
            var messageID = e.CallbackQuery.Message.MessageId;
            if (_callbackActions.ContainsKey(messageID))
            {
                await _callbackActions[messageID](e.CallbackQuery);
                _callbackActions.Remove(messageID);
            }
        }

        private async void BotClient_OnMessage(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            await HandleReceivedMessageAsync(e.Message);
        }

        private async Task HandleReceivedMessageAsync(Telegram.Bot.Types.Message message)
        {
            var messageID = message.MessageId;
            if (_callbackActions.ContainsKey(messageID))
            {
                await _callbackActions[messageID](new CallbackQuery()
                {
                    Data = message.Text,
                    From = message.From,
                    Message = message
                });
                _callbackActions.Remove(messageID);
            }
            else if (message.Type == Telegram.Bot.Types.Enums.MessageType.Text)
            {
                await HandleUserInfoCommand(message);

                if (AnswerMessage(message.From.Id) && OnReceivedMessage != null)
                {
                    OnReceivedMessage.Invoke(this, message.Text);

                }
            }
        }

        private async Task HandleUserInfoCommand(Message message)
        {
            if (message.Text.StartsWith("/userinfo"))
            {
                var user = message.From;

                await _client.SendTextMessageAsync(new Telegram.Bot.Types.ChatId(user.Id), $@"Name: {user.FirstName} {user.LastName}
Username: {user.Username}
ID: {user.Id}");
            }
        }

        #endregion
    }
}
