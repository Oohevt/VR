using System;
using System.IO;
using System.Linq;
using System.Reflection;
using SleepHealing.App;
using SleepHealing.Core;
using SleepHealing.EEG;
using Unity.XR.CoreUtils;
using Unity.XR.PXR;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.SpatialTracking;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Management;

namespace SleepHealing.Editor
{
    public static class SleepHealingBuild
    {
        private const string ScenePath = "Assets/SleepHealing.unity";
        private const string RootName = "睡眠疗愈仓";
        private const string RenderingFolder = "Assets/Rendering";
        private const string PipelineAssetPath = RenderingFolder + "/SleepHealingURP.asset";
        private const string RendererDataPath = RenderingFolder + "/SleepHealingRenderer.asset";
        private const string MaterialFolder = "Assets/Resources/SleepHealing";

        [MenuItem("Sleep Healing/Prepare Scene")]
        public static void PrepareScene()
        {
            var scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = GameObject.Find(RootName) ?? new GameObject(RootName);
            GetOrAdd<SimulatedEEGAdapter>(root);
            GetOrAdd<ExperienceDirector>(root);
            GetOrAdd<SafetyController>(root);
            GetOrAdd<SessionRecorder>(root);
            GetOrAdd<HealingEnvironment>(root);
            var interfaceView = GetOrAdd<SleepHealingUI>(root);
            var interfaceFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansCJKsc-Regular.otf");
            var interfaceObject = new SerializedObject(interfaceView);
            interfaceObject.FindProperty("interfaceFont").objectReferenceValue = interfaceFont;
            interfaceObject.ApplyModifiedPropertiesWithoutUndo();
            GetOrAdd<SleepHealingCapture>(root);
            GetOrAdd<SleepHealingBootstrap>(root);
            EnsureXrRig(root);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            ConfigureSharedSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("Sleep Healing scene prepared.");
        }

        [MenuItem("Sleep Healing/Build All")]
        public static void BuildAll()
        {
            EnsureScene();
            BuildMacPreview();
            BuildAndroid();
        }

        [MenuItem("Sleep Healing/Build macOS Preview")]
        public static void BuildMacPreview()
        {
            EnsureScene();
            SetPicoManagerEnabled(false);
            ConfigureSharedSettings();
            var output = ProjectPath("Builds/macOS/SleepHealingPreview.app");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildOrThrow(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development
            });
        }

        [MenuItem("Sleep Healing/Build Android APK")]
        public static void BuildAndroid()
        {
            EnsureScene();
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                throw new InvalidOperationException("Unable to switch the active build target to Android.");
            }
            ConfigureSharedSettings();
            ConfigureAndroid();
            SetPicoManagerEnabled(true);
            var output = ProjectPath("Builds/Android/SleepHealing-MVP.apk");
            Directory.CreateDirectory(Path.GetDirectoryName(output));

            try
            {
                BuildOrThrow(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                });
            }
            finally
            {
                SetPicoManagerEnabled(false);
            }
        }

        private static void ConfigureSharedSettings()
        {
            EnsureRenderPipeline();
            EnsurePicoDebuggerConfig();
            PlayerSettings.companyName = "GKXN";
            PlayerSettings.productName = "星空深睡";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.gkxn.sleephealing");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.gkxn.sleephealing.preview");
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
        }

        [MenuItem("Sleep Healing/Setup Render Pipeline")]
        public static void SetupRenderPipeline()
        {
            Directory.CreateDirectory(RenderingFolder);
            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererDataPath);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, PipelineAssetPath);
            }

            // 移动 XR 取向：4x MSAA、HDR 中间缓冲给 Bloom 用、不要深度/不透明拷贝、不要阴影。
            pipeline.msaaSampleCount = 4;
            pipeline.supportsHDR = true;
            pipeline.renderScale = 1f;
            pipeline.useSRPBatcher = true;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.colorGradingMode = ColorGradingMode.HighDynamicRange;
            pipeline.colorGradingLutSize = 32;
            var serialized = new SerializedObject(pipeline);
            serialized.FindProperty("m_MainLightShadowsSupported").boolValue = false;
            serialized.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            var activeLevel = QualitySettings.GetQualityLevel();
            for (var index = 0; index < QualitySettings.names.Length; index++)
            {
                QualitySettings.SetQualityLevel(index, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(activeLevel, false);

            // URP 17 的全局设置类是 internal，只能反射调用 Ensure 生成 UniversalRenderPipelineGlobalSettings.asset。
            var globalSettingsType = typeof(UniversalRenderPipelineAsset).Assembly.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings");
            var ensure = globalSettingsType?.GetMethod("Ensure", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (ensure == null)
            {
                throw new InvalidOperationException("UniversalRenderPipelineGlobalSettings.Ensure not found.");
            }
            ensure.Invoke(null, new object[] { true });

            CreateMaterialTemplates();
            AssetDatabase.SaveAssets();
            Debug.Log("Sleep Healing render pipeline configured.");
        }

        // PICO SDK 的调试器在开发构建启动时读取 Resources/PXR_PicoDebuggerSO，缺失会抛 NullReference 并弹出开发控制台。
        private static void EnsurePicoDebuggerConfig()
        {
            const string path = "Assets/Resources/PXR_PicoDebuggerSO.asset";
            if (AssetDatabase.LoadAssetAtPath<Unity.XR.PXR.Debugger.PXR_PicoDebuggerSO>(path) != null)
            {
                return;
            }

            var config = ScriptableObject.CreateInstance<Unity.XR.PXR.Debugger.PXR_PicoDebuggerSO>();
            config.isOpen = false;
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureRenderPipeline()
        {
            if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath) == null
                || GraphicsSettings.defaultRenderPipeline == null
                || AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Skybox.mat") == null)
            {
                SetupRenderPipeline();
            }
        }

        // 运行时材质全部由代码生成，这些模板只为把 URP 着色器和所需关键字变体带进打包。
        private static void CreateMaterialTemplates()
        {
            var lit = EnsureMaterial("Lit", "Universal Render Pipeline/Lit");
            lit.EnableKeyword("_EMISSION");
            lit.SetColor("_EmissionColor", Color.black);

            var additive = EnsureMaterial("Additive", "Universal Render Pipeline/Particles/Unlit");
            additive.SetFloat("_Surface", 1f);
            additive.SetFloat("_Blend", 2f);
            additive.SetFloat("_ZWrite", 0f);
            additive.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            additive.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            additive.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            additive.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            additive.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            additive.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            additive.SetOverrideTag("RenderType", "Transparent");
            additive.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            EnsureMaterial("Skybox", "Skybox/Cubemap");
        }

        private static Material EnsureMaterial(string materialName, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new InvalidOperationException($"Shader missing: {shaderName}");
            }

            var path = $"{MaterialFolder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureAndroid()
        {
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            EnsurePicoSettings();

            var buildTargetSettings = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget")
                .Select(guid => AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault();
            if (buildTargetSettings == null)
            {
                buildTargetSettings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(buildTargetSettings, "Assets/XRGeneralSettingsPerBuildTarget.asset");
            }

            var generalSettings = buildTargetSettings.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (generalSettings == null)
            {
                generalSettings = ScriptableObject.CreateInstance<XRGeneralSettings>();
                AssetDatabase.AddObjectToAsset(generalSettings, buildTargetSettings);
                buildTargetSettings.SetSettingsForBuildTarget(BuildTargetGroup.Android, generalSettings);
                var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                AssetDatabase.AddObjectToAsset(manager, buildTargetSettings);
                generalSettings.Manager = manager;
            }

            var loaders = generalSettings.Manager.activeLoaders.ToArray();
            foreach (var loader in loaders)
            {
                XRPackageMetadataStore.RemoveLoader(generalSettings.Manager, loader.GetType().FullName, BuildTargetGroup.Android);
            }

            if (!XRPackageMetadataStore.AssignLoader(generalSettings.Manager, "PXR_Loader", BuildTargetGroup.Android))
            {
                throw new InvalidOperationException("Unable to assign PXR_Loader for Android.");
            }

            EditorUtility.SetDirty(buildTargetSettings);
            AssetDatabase.SaveAssets();
        }

        private static void EnsurePicoSettings()
        {
            if (PXR_Settings.GetSettings() != null)
            {
                return;
            }

            const string settingsPath = "Assets/XR/PXR_Settings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<PXR_Settings>(settingsPath);
            if (settings == null)
            {
                Directory.CreateDirectory("Assets/XR");
                settings = ScriptableObject.CreateInstance<PXR_Settings>();
                AssetDatabase.CreateAsset(settings, settingsPath);
            }

            EditorBuildSettings.AddConfigObject("Unity.XR.PXR.Settings", settings, true);
            EditorUtility.SetDirty(settings);
        }

        private static void EnsureXrRig(GameObject root)
        {
            var origin = GetOrAdd<XROrigin>(root);
            var offset = root.transform.Find("XR Camera Offset");
            if (offset == null)
            {
                offset = new GameObject("XR Camera Offset").transform;
                offset.SetParent(root.transform, false);
            }

            var cameraTransform = offset.Find("Main Camera");
            if (cameraTransform == null)
            {
                cameraTransform = new GameObject("Main Camera").transform;
                cameraTransform.SetParent(offset, false);
            }

            cameraTransform.gameObject.tag = "MainCamera";
            var camera = GetOrAdd<Camera>(cameraTransform.gameObject);
            GetOrAdd<AudioListener>(cameraTransform.gameObject);
            var trackedPose = GetOrAdd<TrackedPoseDriver>(cameraTransform.gameObject);
            trackedPose.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.Center);
            trackedPose.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            trackedPose.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            origin.CameraFloorOffsetObject = offset.gameObject;
            origin.Camera = camera;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;

            var picoManager = GetOrAdd<PXR_Manager>(root);
            picoManager.enabled = false;

            var eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var eventObject = new GameObject("XR 交互事件系统");
                eventSystem = eventObject.AddComponent<EventSystem>();
            }
            GetOrAdd<XRUIInputModule>(eventSystem.gameObject);
        }

        private static void SetPicoManagerEnabled(bool enabled)
        {
            var root = GameObject.Find(RootName);
            if (root == null)
            {
                throw new InvalidOperationException("Sleep Healing scene root is missing.");
            }

            var manager = GetOrAdd<PXR_Manager>(root);
            manager.enabled = enabled;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        private static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                PrepareScene();
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            return target.GetComponent<T>() ?? target.AddComponent<T>();
        }

        private static string ProjectPath(string relativePath)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var fullPath = Path.GetFullPath(Path.Combine(projectRoot, relativePath));
            if (!fullPath.StartsWith(projectRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Build output escaped the Unity project root.");
            }
            return fullPath;
        }

        private static void BuildOrThrow(BuildPlayerOptions options)
        {
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"Build failed: {report.summary.result}, errors={report.summary.totalErrors}");
            }

            Debug.Log($"Build succeeded: {options.locationPathName}, bytes={report.summary.totalSize}");
        }
    }
}
