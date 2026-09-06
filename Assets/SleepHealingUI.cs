using System;
using SleepHealing.Core;
using SleepHealing.EEG;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace SleepHealing.App
{
    public sealed class SleepHealingUI : MonoBehaviour
    {
        [SerializeField] private Font interfaceFont;

        private ExperienceDirector director;
        private IEEGStateProvider provider;
        private IEEGSimulationControl simulation;
        private SafetyController safety;
        private SessionRecorder recorder;
        private Text stageText;
        private Text descriptionText;
        private Text timeText;
        private Text statusText;
        private Text sampleText;
        private Text autoButtonText;
        private Text modeButtonText;
        private Text pauseButtonText;
        private Image progressFill;
        private Slider relaxationSlider;
        private Slider attentionSlider;
        private Slider fatigueSlider;
        private Slider signalSlider;
        private bool updatingSliders;
        private bool previousAutoMode;

        private static readonly Color CanvasTint = new Color(0.008f, 0.012f, 0.038f, 0f);
        private static readonly Color Panel = new Color(0.075f, 0.105f, 0.22f, 0.94f);
        private static readonly Color PanelRaised = new Color(0.13f, 0.16f, 0.29f, 0.98f);
        private static readonly Color Gold = new Color(0.96f, 0.66f, 0.27f, 1f);
        private static readonly Color Cream = new Color(0.96f, 0.90f, 0.73f, 1f);
        private static readonly Color PrimaryText = new Color(0.88f, 0.91f, 1f, 1f);
        private static readonly Color SecondaryText = new Color(0.57f, 0.64f, 0.79f, 1f);

        public void Initialize(
            ExperienceDirector experienceDirector,
            IEEGStateProvider eegProvider,
            IEEGSimulationControl simulationControl,
            SafetyController safetyController,
            SessionRecorder sessionRecorder)
        {
            director = experienceDirector;
            provider = eegProvider;
            simulation = simulationControl;
            safety = safetyController;
            recorder = sessionRecorder;
            previousAutoMode = simulation?.AutoSimulate ?? true;
            BuildCanvas();
            UpdateView();
        }

        private void Update()
        {
            if (director?.Timeline == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Space)) director.TogglePause();
            if (Input.GetKeyDown(KeyCode.Escape)) director.SafeExit();

            if (simulation != null && previousAutoMode != simulation.AutoSimulate)
            {
                SyncSliders(provider.CurrentState);
                previousAutoMode = simulation.AutoSimulate;
            }

            if (simulation != null && !simulation.AutoSimulate && !updatingSliders)
            {
                simulation.SetManualState(new EEGState
                {
                    relaxation = relaxationSlider.value,
                    attention = attentionSlider.value,
                    fatigue = fatigueSlider.value,
                    signalQuality = signalSlider.value,
                    timestamp = Time.realtimeSinceStartupAsDouble
                });
            }

            UpdateView();
        }

        private void BuildCanvas()
        {
            if (interfaceFont == null)
            {
                interfaceFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            var canvasObject = new GameObject("疗愈交互界面", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 0.25f;
            canvas.sortingOrder = 20;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.GetComponent<GraphicRaycaster>().ignoreReversedGraphics = true;
            canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            canvasObject.AddComponent<Image>().color = CanvasTint;

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject("交互事件系统", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            var left = CreatePanel(canvas.transform, "阶段面板", new Vector2(0f, 1f), new Vector2(34f, -34f), new Vector2(420f, 280f));
            CreateText(left, "星空深睡", 28, Cream, new Vector2(28f, -18f), new Vector2(360f, 56f), FontStyle.Bold);
            stageText = CreateText(left, string.Empty, 21, PrimaryText, new Vector2(28f, -82f), new Vector2(360f, 34f), FontStyle.Bold);
            descriptionText = CreateText(left, string.Empty, 15, SecondaryText, new Vector2(28f, -124f), new Vector2(360f, 44f));
            progressFill = CreateProgress(left, new Vector2(28f, -176f), new Vector2(364f, 5f));
            timeText = CreateText(left, string.Empty, 14, SecondaryText, new Vector2(28f, -197f), new Vector2(180f, 28f));
            statusText = CreateText(left, string.Empty, 14, SecondaryText, new Vector2(28f, -232f), new Vector2(360f, 28f));

            var right = CreatePanel(canvas.transform, "脑电面板", new Vector2(1f, 0f), new Vector2(-34f, 34f), new Vector2(350f, 425f));
            CreateText(right, "脑电模拟器", 21, PrimaryText, new Vector2(22f, -20f), new Vector2(300f, 32f), FontStyle.Bold);
            CreateText(right, "模拟数据 · 非医疗结论", 14, SecondaryText, new Vector2(22f, -56f), new Vector2(300f, 26f));
            autoButtonText = CreateButton(right, "切换到手动调节", new Vector2(22f, -96f), new Vector2(306f, 44f), ToggleSimulation).GetComponentInChildren<Text>();
            relaxationSlider = CreateMetric(right, "放松", new Vector2(22f, -154f));
            attentionSlider = CreateMetric(right, "专注", new Vector2(22f, -207f));
            fatigueSlider = CreateMetric(right, "疲劳", new Vector2(22f, -260f));
            signalSlider = CreateMetric(right, "信号", new Vector2(22f, -313f));
            sampleText = CreateText(right, string.Empty, 12, SecondaryText, new Vector2(22f, -343f), new Vector2(300f, 20f));
            var disconnectButton = CreateButton(right, "模拟断联", new Vector2(22f, -374f), new Vector2(148f, 40f), ToggleSignal);
            var spikeButton = CreateButton(right, "注入尖峰", new Vector2(180f, -374f), new Vector2(148f, 40f), () => simulation?.TriggerSpike());
            disconnectButton.gameObject.SetActive(simulation != null);
            spikeButton.gameObject.SetActive(simulation != null);

            var footer = CreateRect(canvas.transform, "流程控制", new Vector2(0f, 0f), new Vector2(34f, 34f), new Vector2(730f, 52f));
            CreateButton(footer, "重新开始", Vector2.zero, new Vector2(150f, 48f), () => director.StartSession(director.Mode));
            pauseButtonText = CreateButton(footer, "暂停", new Vector2(158f, 0f), new Vector2(110f, 48f), director.TogglePause).GetComponentInChildren<Text>();
            CreateButton(footer, "安全退出", new Vector2(276f, 0f), new Vector2(130f, 48f), director.SafeExit);
            modeButtonText = CreateButton(footer, "切换到 8 分钟", new Vector2(414f, 0f), new Vector2(168f, 48f), ToggleMode).GetComponentInChildren<Text>();
        }

        private void UpdateView()
        {
            var timeline = director.Timeline;
            stageText.text = StageName(timeline.Stage);
            descriptionText.text = StageDescription(timeline.Stage);
            timeText.text = $"{FormatTime(timeline.StageElapsed)}  /  {FormatTime(timeline.StageDuration)}";
            statusText.text = timeline.IsPaused ? "体验已暂停" : safety.StatusMessage;
            progressFill.fillAmount = timeline.StageProgress;
            sampleText.text = $"记录 {recorder.SampleCount} · 自适应 {(director.AdaptationEnabled ? "运行" : "冻结")}";
            autoButtonText.transform.parent.gameObject.SetActive(simulation != null);
            autoButtonText.text = simulation?.AutoSimulate == true ? "切换到手动调节" : "切换到自动曲线";
            modeButtonText.text = director.Mode == ExperienceMode.Demo ? "切换到 8 分钟" : "切换到 90 秒";
            pauseButtonText.text = timeline.IsPaused ? "继续" : "暂停";

            updatingSliders = true;
            var state = simulation?.AutoSimulate == false ? provider.CurrentState : director.ProcessedEEG;
            SyncSliders(state);
            var manual = simulation != null && !simulation.AutoSimulate;
            relaxationSlider.interactable = manual;
            attentionSlider.interactable = manual;
            fatigueSlider.interactable = manual;
            signalSlider.interactable = manual;
            updatingSliders = false;
        }

        private void SyncSliders(EEGState state)
        {
            relaxationSlider.SetValueWithoutNotify(state.relaxation);
            attentionSlider.SetValueWithoutNotify(state.attention);
            fatigueSlider.SetValueWithoutNotify(state.fatigue);
            signalSlider.SetValueWithoutNotify(state.signalQuality);
        }

        private void ToggleSimulation()
        {
            if (simulation != null) simulation.SetAutoSimulation(!simulation.AutoSimulate);
        }

        private void ToggleSignal()
        {
            if (simulation != null) simulation.SetSignalAvailable(!provider.IsConnected);
        }
        private void ToggleMode() => director.StartSession(director.Mode == ExperienceMode.Demo ? ExperienceMode.Full : ExperienceMode.Demo);

        private Slider CreateMetric(Transform parent, string label, Vector2 position)
        {
            CreateText(parent, label, 13, SecondaryText, position, new Vector2(50f, 22f));
            var sliderRect = CreateRect(parent, label + "滑杆", new Vector2(0f, 1f), position + new Vector2(54f, 1f), new Vector2(250f, 22f));
            sliderRect.gameObject.AddComponent<Image>().color = new Color(0.035f, 0.05f, 0.11f, 1f);
            var slider = sliderRect.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;

            var fillArea = CreateRect(sliderRect, "Fill Area", new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(220f, 6f));
            var fill = CreateRect(fillArea, "Fill", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(220f, 6f));
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            fill.gameObject.AddComponent<Image>().color = Gold;
            slider.fillRect = fill;

            var handleArea = CreateRect(sliderRect, "Handle Slide Area", new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(228f, 22f));
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(4f, 0f);
            handleArea.offsetMax = new Vector2(-4f, 0f);
            var handle = CreateRect(handleArea, "Handle", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(16f, 16f));
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = Cream;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            return slider;
        }

        private Image CreateProgress(Transform parent, Vector2 position, Vector2 size)
        {
            var track = CreateRect(parent, "阶段进度轨道", new Vector2(0f, 1f), position, size);
            track.gameObject.AddComponent<Image>().color = new Color(0.26f, 0.30f, 0.44f, 1f);
            var fill = CreateRect(track, "阶段进度", new Vector2(0f, 0.5f), Vector2.zero, size);
            var image = fill.gameObject.AddComponent<Image>();
            image.color = Gold;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            return image;
        }

        private Button CreateButton(Transform parent, string label, Vector2 position, Vector2 size, Action action)
        {
            var rect = CreateRect(parent, label, new Vector2(0f, 1f), position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = PanelRaised;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = new ColorBlock
            {
                normalColor = PanelRaised,
                highlightedColor = new Color(0.22f, 0.25f, 0.38f, 1f),
                pressedColor = new Color(0.40f, 0.28f, 0.13f, 1f),
                selectedColor = PanelRaised,
                disabledColor = new Color(0.09f, 0.10f, 0.15f, 0.65f),
                colorMultiplier = 1f,
                fadeDuration = 0.18f
            };
            button.onClick.AddListener(() => action());
            var text = CreateText(rect, label, 14, Cream, Vector2.zero, size, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private RectTransform CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = CreateRect(parent, name, anchor, position, size);
            rect.gameObject.AddComponent<Image>().color = Panel;
            return rect;
        }

        private Text CreateText(Transform parent, string value, int size, Color color, Vector2 position, Vector2 dimensions, FontStyle style = FontStyle.Normal)
        {
            var rect = CreateRect(parent, value, new Vector2(0f, 1f), position, dimensions);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = interfaceFont;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.text = value;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static RectTransform CreateRect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x, anchor.y);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static string StageName(ExperienceStage stage) => stage switch
        {
            ExperienceStage.Preparation => "01  静心准备",
            ExperienceStage.Baseline => "02  基线采集",
            ExperienceStage.Breathing => "03  呼吸放松",
            ExperienceStage.DeepHealing => "04  深度疗愈",
            ExperienceStage.Awakening => "05  自然唤醒",
            _ => "睡眠疗愈"
        };

        private static string StageDescription(ExperienceStage stage) => stage switch
        {
            ExperienceStage.Preparation => "保持舒适躺姿，确认随时可以安全退出。",
            ExperienceStage.Baseline => "观察当前状态，不改变环境节奏。",
            ExperienceStage.Breathing => "跟随暖金光核，缓慢吸气与呼气。",
            ExperienceStage.DeepHealing => "星穹根据稳定后的模拟状态缓慢响应。",
            ExperienceStage.Awakening => "环境逐步恢复，轻轻活动手指与肩颈。",
            _ => string.Empty
        };

        private static string FormatTime(float seconds)
        {
            var value = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{value / 60:00}:{value % 60:00}";
        }
    }
}
