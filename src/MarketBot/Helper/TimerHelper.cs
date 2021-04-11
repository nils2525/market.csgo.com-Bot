using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using static SmartWebClient.Logger;

namespace MarketBot.Helper
{
    public class TimerHelper
    {
        private Timer _timer;
        private double _interval;
        public Func<Task> Action { get; private set; }
        public bool ActionIsRunning { get; private set; }
        public bool IsEnabled => _timer?.Enabled ?? false;

        public double Interval
        {
            get
            {
                return _interval;
            }
            set
            {
                _interval = value;
                if (_timer != null)
                {
                    _timer.Interval = value;
                }
            }
        }

        public TimerHelper(double interval, Func<Task> action, bool startTimer = false)
        {
            _interval = interval;
            Action = action;

            if (startTimer)
            {
                Start();
            }
        }

        public bool Start()
        {
            if (!IsEnabled)
            {
                _timer = new Timer(_interval);
                _timer.Elapsed += Timer_Elapsed;
                _timer.Start();
                return true;
            }
            return false;
        }

        public async Task<bool> StopAsync()
        {
            if (IsEnabled)
            {
                _timer.Stop();
                _timer.Elapsed -= Timer_Elapsed;
                _timer = null;

                await WaitForActionEndedAsync();

                return true;
            }
            return false;
        }

        private async void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            await RunActionAsync();
        }

        public async Task WaitForActionEndedAsync()
        {
            while (ActionIsRunning)
            {
                // Wait until the current action is done
                await Task.Delay(5);
            }
        }

        public async Task RunActionAsync()
        {
            if (!ActionIsRunning)
            {
                ActionIsRunning = true;

                try
                {
                    await Action();
                }
                catch (Exception ex)
                {
                    LogToConsole(ex);
                }

                ActionIsRunning = false;
            }
        }
    }
}
