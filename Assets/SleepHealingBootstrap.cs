using SleepHealing.EEG;
using SleepHealing.Core;
using UnityEngine;

namespace SleepHealing.App
{
    [DefaultExecutionOrder(-100)]
    public sealed class SleepHealingBootstrap : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour eegProviderBehaviour;

        public IEEGStateProvider EEGProvider { get; private set; }

        private void Awake()
        {
            EEGProvider = eegProviderBehaviour as IEEGStateProvider;

            if (EEGProvider == null)
            {
                foreach (var behaviour in GetComponents<MonoBehaviour>())
                {
                    if (behaviour is IEEGStateProvider provider)
                    {
                        EEGProvider = provider;
                        break;
                    }
                }
            }

            if (EEGProvider == null)
            {
                Debug.LogWarning("SleepHealingBootstrap requires an EEG provider.", this);
                return;
            }

            var director = GetComponent<ExperienceDirector>();
            var safety = GetComponent<SafetyController>();
            var recorder = GetComponent<SessionRecorder>();
            var environment = GetComponent<HealingEnvironment>();
            var interfaceView = GetComponent<SleepHealingUI>();
            var capture = GetComponent<SleepHealingCapture>();
            var simulation = EEGProvider as IEEGSimulationControl;

            if (director == null || safety == null || recorder == null || environment == null || interfaceView == null)
            {
                Debug.LogError("SleepHealingBootstrap scene components are incomplete.", this);
                return;
            }

            environment.Initialize();
            director.Initialize(EEGProvider, safety, recorder, environment);
            interfaceView.Initialize(director, EEGProvider, simulation, safety, recorder);
            capture?.Initialize(director, simulation);
            director.StartSession(ExperienceMode.Demo);
        }
    }
}
