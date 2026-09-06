using System;
using UnityEngine;

namespace SleepHealing.EEG
{
    public sealed class PicoEEGAdapter : MonoBehaviour, IEEGStateProvider
    {
        public bool IsConnected => false;
        public EEGState CurrentState { get; private set; }
        public string IntegrationStatus => "待 SDK 确认";

        public event Action<EEGState> StateChanged;

        private void OnEnable()
        {
            CurrentState = default;
            Debug.Log("PicoEEGAdapter：真实字段、协议、采样率和算法含义待 SDK 确认。", this);
        }

        // 真实 SDK 到位后仅在此适配原始回调，并通过本事件发布统一 EEGState。
        private void PublishFromVerifiedSdk(EEGState verifiedState)
        {
            CurrentState = verifiedState.Sanitized();
            StateChanged?.Invoke(CurrentState);
        }
    }
}
