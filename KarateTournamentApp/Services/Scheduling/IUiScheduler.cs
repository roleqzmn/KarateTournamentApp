using System;

namespace KarateTournamentApp.Services.Scheduling
{
    public interface IUiScheduler
    {
        void Schedule(TimeSpan delay, Action action);
    }
}
