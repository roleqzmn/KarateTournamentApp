using System;
using System.Windows;
using System.Windows.Threading;

namespace KarateTournamentApp.Services.Scheduling
{
    public class WpfUiScheduler : IUiScheduler
    {
        public void Schedule(TimeSpan delay, Action action)
        {
            ArgumentNullException.ThrowIfNull(action);

            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var timer = new DispatcherTimer
                {
                    Interval = delay
                };

                EventHandler? onTick = null;
                onTick = (_, _) =>
                {
                    timer.Stop();
                    timer.Tick -= onTick;
                    action();
                };

                timer.Tick += onTick;
                timer.Start();
            });
        }
    }
}
