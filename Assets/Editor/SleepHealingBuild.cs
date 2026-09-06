using System;
using System.IO;
using System.Linq;
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
