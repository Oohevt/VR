using UnityEngine;

namespace SleepHealing.EEG
{
    public sealed class EEGSignalProcessor
    {
        private readonly float minimumSignalQuality;
        private readonly float requiredStableSeconds;
        private readonly float maximumChangePerSecond;
        private bool initialized;
        private float stableSeconds;

        public EEGState Current { get; private set; }
        public bool AdaptationEnabled { get; private set; }
        public float StableSeconds => stableSeconds;

        public EEGSignalProcessor(
            float minimumSignalQuality = 0.5f,
            float requiredStableSeconds = 15f,
            float maximumChangePerSecond = 0.12f)
        {
            this.minimumSignalQuality = Mathf.Clamp01(minimumSignalQuality);
            this.requiredStableSeconds = Mathf.Max(0f, requiredStableSeconds);
            this.maximumChangePerSecond = Mathf.Max(0.001f, maximumChangePerSecond);
        }

        public EEGState Process(EEGState input, float deltaSeconds)
        {
            input = input.Sanitized();
            var safeDelta = Mathf.Max(0f, deltaSeconds);

            if (!initialized)
            {
                Current = input;
                initialized = true;
            }

            if (!input.IsUsable(minimumSignalQuality))
            {
                stableSeconds = 0f;
                AdaptationEnabled = false;
                var frozen = Current;
                frozen.signalQuality = input.signalQuality;
                frozen.timestamp = input.timestamp;
                Current = frozen;
                return Current;
            }

            stableSeconds += safeDelta;
            AdaptationEnabled = stableSeconds >= requiredStableSeconds;
            var maxChange = maximumChangePerSecond * safeDelta;
            Current = new EEGState
            {
                signalQuality = input.signalQuality,
                relaxation = Mathf.MoveTowards(Current.relaxation, input.relaxation, maxChange),
                attention = Mathf.MoveTowards(Current.attention, input.attention, maxChange),
                fatigue = Mathf.MoveTowards(Current.fatigue, input.fatigue, maxChange),
                timestamp = input.timestamp
            };
            return Current;
        }
    }
}
