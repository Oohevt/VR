using System;
using UnityEngine;

namespace SleepHealing.EEG
{
    [Serializable]
    public struct EEGState
    {
        [Range(0f, 1f)] public float signalQuality;
        [Range(0f, 1f)] public float relaxation;
        [Range(0f, 1f)] public float attention;
        [Range(0f, 1f)] public float fatigue;
        public double timestamp;

        public bool IsUsable(float minimumSignalQuality = 0.5f)
        {
            return signalQuality >= Mathf.Clamp01(minimumSignalQuality);
        }

        public EEGState Sanitized()
        {
            return new EEGState
            {
                signalQuality = Mathf.Clamp01(signalQuality),
                relaxation = Mathf.Clamp01(relaxation),
                attention = Mathf.Clamp01(attention),
                fatigue = Mathf.Clamp01(fatigue),
                timestamp = Math.Max(0d, timestamp)
            };
        }
    }
}
