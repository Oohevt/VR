using System;
using SleepHealing.EEG;
using UnityEngine;

namespace SleepHealing.Core
{
    public sealed class ExperienceDirector : MonoBehaviour
    {
        [SerializeField] private ExperienceMode mode = ExperienceMode.Demo;

        private IEEGStateProvider provider;
        private EEGSignalProcessor processor;
        private SafetyController safety;
        private SessionRecorder recorder;
        private HealingEnvironment environment;

        public ExperienceTimeline Timeline { get; private set; }
        public EEGState ProcessedEEG { get; private set; }
        public bool AdaptationEnabled => processor != null && processor.AdaptationEnabled && safety.SignalIsSafe;
        public ExperienceMode Mode => mode;

        public event Action<ExperienceStage> StageChanged;

        public void Initialize(
            IEEGStateProvider eegProvider,
            SafetyController safetyController,
            SessionRecorder sessionRecorder,
            HealingEnvironment healingEnvironment)
        {
            provider = eegProvider;
            safety = safetyController;
            recorder = sessionRecorder;
            environment = healingEnvironment;
            ResetTimeline();
            ApplyCurrentState(0f);
        }

        private void Update()
        {
            if (Timeline == null || provider == null)
            {
                return;
            }

            if (Timeline.WasExited)
            {
                return;
            }

            var delta = Time.unscaledDeltaTime;
            Timeline.Tick(delta);
            ApplyCurrentState(delta);
        }

        public void StartSession(ExperienceMode selectedMode)
        {
            mode = selectedMode;
            ResetTimeline();
            recorder.Begin();
            environment.ResumeSession();
            Timeline.Start();
        }

        public void TogglePause()
        {
            if (Timeline == null || !Timeline.IsRunning)
            {
                return;
            }

            if (Timeline.IsPaused)
            {
                Timeline.Resume();
            }
            else
            {
                Timeline.Pause();
            }
        }

        public void SafeExit()
        {
            Timeline?.Exit();
            environment?.EnterSafeState();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void ForceStageForCapture(ExperienceStage stage)
        {
            Timeline?.ForceStage(stage);
            ApplyCurrentState(16f);
        }
#endif

        private void ResetTimeline()
        {
            if (Timeline != null)
            {
                Timeline.StageChanged -= HandleStageChanged;
            }

            Timeline = new ExperienceTimeline(mode);
            Timeline.StageChanged += HandleStageChanged;
            processor = new EEGSignalProcessor();
        }

        private void ApplyCurrentState(float delta)
        {
            var raw = provider?.CurrentState ?? default;
            ProcessedEEG = processor.Process(raw, delta);
            safety.Evaluate(provider?.IsConnected == true, raw);
            environment.Apply(Timeline.Stage, ProcessedEEG, AdaptationEnabled, delta);

            if (Timeline.IsRunning)
            {
                recorder.Record(Timeline.TotalElapsed, Timeline.Stage, ProcessedEEG, AdaptationEnabled);
            }
        }

        private void HandleStageChanged(ExperienceStage stage)
        {
            StageChanged?.Invoke(stage);
        }
    }
}
