using System;

namespace SleepHealing.Core
{
    public enum ExperienceStage
    {
        Preparation,
        Baseline,
        Breathing,
        DeepHealing,
        Awakening
    }

    public enum ExperienceMode
    {
        Demo,
        Full
    }

    public sealed class ExperienceTimeline
    {
        private static readonly float[] DemoDurations = { 10f, 15f, 20f, 35f, 10f };
        private static readonly float[] FullDurations = { 45f, 60f, 90f, 255f, 30f };

        private readonly float[] durations;

        public ExperienceStage Stage { get; private set; } = ExperienceStage.Preparation;
        public float StageElapsed { get; private set; }
        public float TotalElapsed { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsCompleted { get; private set; }
        public bool WasExited { get; private set; }
        public float StageDuration => durations[(int)Stage];
        public float StageProgress => StageDuration <= 0f ? 1f : Math.Clamp(StageElapsed / StageDuration, 0f, 1f);

        public event Action<ExperienceStage> StageChanged;

        public ExperienceTimeline(ExperienceMode mode)
        {
            var source = mode == ExperienceMode.Demo ? DemoDurations : FullDurations;
            durations = (float[])source.Clone();
        }

        public void Start()
        {
            Stage = ExperienceStage.Preparation;
            StageElapsed = 0f;
            TotalElapsed = 0f;
            IsRunning = true;
            IsPaused = false;
            IsCompleted = false;
            WasExited = false;
            StageChanged?.Invoke(Stage);
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsRunning || IsPaused || deltaSeconds <= 0f)
            {
                return;
            }

            var remaining = deltaSeconds;
            while (remaining > 0f && IsRunning)
            {
                var available = StageDuration - StageElapsed;
                var consumed = Math.Min(remaining, available);
                StageElapsed += consumed;
                TotalElapsed += consumed;
                remaining -= consumed;

                if (StageElapsed + 0.0001f >= StageDuration)
                {
                    Advance();
                }
            }
        }

        public void Pause()
        {
            if (IsRunning)
            {
                IsPaused = true;
            }
        }

        public void Resume()
        {
            if (IsRunning)
            {
                IsPaused = false;
            }
        }

        public void Exit()
        {
            IsRunning = false;
            IsPaused = false;
            IsCompleted = false;
            WasExited = true;
            Stage = ExperienceStage.Preparation;
            StageElapsed = 0f;
            StageChanged?.Invoke(Stage);
        }

        public void ForceStage(ExperienceStage stage)
        {
            Stage = stage;
            StageElapsed = 0f;
            StageChanged?.Invoke(Stage);
        }

        private void Advance()
        {
            if (Stage == ExperienceStage.Awakening)
            {
                IsRunning = false;
                IsCompleted = true;
                StageElapsed = StageDuration;
                return;
            }

            Stage++;
            StageElapsed = 0f;
            StageChanged?.Invoke(Stage);
        }
    }
}
