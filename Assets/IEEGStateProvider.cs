using System;

namespace SleepHealing.EEG
{
    public interface IEEGStateProvider
    {
        bool IsConnected { get; }
        EEGState CurrentState { get; }
        event Action<EEGState> StateChanged;
    }
}
