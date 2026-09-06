using SleepHealing.EEG;
using UnityEngine;

namespace SleepHealing.Core
{
    public sealed class SafetyController : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float minimumSignalQuality = 0.5f;

        public bool SignalIsSafe { get; private set; }
        public string StatusMessage { get; private set; } = "等待信号";

        public bool Evaluate(bool providerConnected, EEGState state)
        {
            SignalIsSafe = providerConnected && state.IsUsable(minimumSignalQuality);
            StatusMessage = SignalIsSafe ? "信号稳定" : "信号无效，自适应已冻结";
            return SignalIsSafe;
        }
    }
}
