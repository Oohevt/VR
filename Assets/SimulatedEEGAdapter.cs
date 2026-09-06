using System;
using UnityEngine;

namespace SleepHealing.EEG
{
    public sealed class SimulatedEEGAdapter : MonoBehaviour, IEEGStateProvider, IEEGSimulationControl
    {
        [Header("Simulation")]
        [SerializeField] private bool autoSimulate = true;
        [SerializeField] private bool signalAvailable = true;
        [SerializeField, Min(0.1f)] private float updatesPerSecond = 5f;
        [SerializeField] private EEGState manualState = new EEGState
        {
            signalQuality = 1f,
            relaxation = 0.35f,
            attention = 0.55f,
            fatigue = 0.2f
        };

        private float nextUpdateTime;
        private bool spikePending;

        public bool IsConnected => isActiveAndEnabled && signalAvailable;
        public bool AutoSimulate => autoSimulate;
        public EEGState CurrentState { get; private set; }
        public event Action<EEGState> StateChanged;

        private void OnEnable()
        {
            Publish(autoSimulate ? CreateAutomaticState(Time.time) : manualState);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextUpdateTime)
            {
                return;
            }

            nextUpdateTime = Time.unscaledTime + 1f / Mathf.Max(0.1f, updatesPerSecond);
            Publish(autoSimulate ? CreateAutomaticState(Time.time) : manualState);
        }

        public void SetManualState(EEGState state)
        {
            manualState = state.Sanitized();
            if (!autoSimulate)
            {
                Publish(manualState);
            }
        }

        public void SetAutoSimulation(bool enabled)
        {
            autoSimulate = enabled;
            Publish(autoSimulate ? CreateAutomaticState(Time.time) : manualState);
        }

        public void SetSignalAvailable(bool available)
        {
            signalAvailable = available;
            var state = CurrentState;
            state.signalQuality = available ? Mathf.Max(0.95f, state.signalQuality) : 0f;
            state.timestamp = Time.realtimeSinceStartupAsDouble;
            Publish(state);
        }

        public void TriggerSpike()
        {
            spikePending = true;
        }

        private static EEGState CreateAutomaticState(float time)
        {
            var slow = Mathf.PerlinNoise(time * 0.035f, 0.2f);
            var medium = Mathf.PerlinNoise(time * 0.08f, 0.7f);

            return new EEGState
            {
                signalQuality = 0.95f,
                relaxation = Mathf.Lerp(0.25f, 0.85f, slow),
                attention = Mathf.Lerp(0.35f, 0.65f, medium),
                fatigue = Mathf.Lerp(0.15f, 0.7f, 1f - slow),
                timestamp = Time.realtimeSinceStartupAsDouble
            };
        }

        private void Publish(EEGState state)
        {
            if (!signalAvailable)
            {
                state.signalQuality = 0f;
            }

            if (spikePending)
            {
                state.relaxation = 1f;
                state.attention = 0f;
                spikePending = false;
            }

            CurrentState = state.Sanitized();
            StateChanged?.Invoke(CurrentState);
        }
    }
}
