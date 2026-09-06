using System.Collections.Generic;
using SleepHealing.EEG;
using UnityEngine;

namespace SleepHealing.Core
{
    public sealed class SessionRecorder : MonoBehaviour
    {
        private readonly List<SessionSample> samples = new List<SessionSample>();
        private float nextRecordTime;

        public int SampleCount => samples.Count;
        public IReadOnlyList<SessionSample> Samples => samples;

        public void Begin()
        {
            samples.Clear();
            nextRecordTime = 0f;
        }

        public void Record(float totalElapsed, ExperienceStage stage, EEGState state, bool adaptive)
        {
            if (totalElapsed + 0.0001f < nextRecordTime)
            {
                return;
            }

            samples.Add(new SessionSample(totalElapsed, stage, state, adaptive));
            nextRecordTime = totalElapsed + 0.5f;
        }
    }

    public readonly struct SessionSample
    {
        public readonly float elapsed;
        public readonly ExperienceStage stage;
        public readonly EEGState eeg;
        public readonly bool adaptive;

        public SessionSample(float elapsed, ExperienceStage stage, EEGState eeg, bool adaptive)
        {
            this.elapsed = elapsed;
            this.stage = stage;
            this.eeg = eeg;
            this.adaptive = adaptive;
        }
    }
}
