namespace SleepHealing.EEG
{
    public interface IEEGSimulationControl
    {
        bool AutoSimulate { get; }
        void SetAutoSimulation(bool enabled);
        void SetManualState(EEGState state);
        void SetSignalAvailable(bool available);
        void TriggerSpike();
    }
}
