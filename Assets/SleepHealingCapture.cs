using System.Collections;
using System.IO;
using SleepHealing.Core;
using SleepHealing.EEG;
using UnityEngine;

namespace SleepHealing.App
{
    public sealed class SleepHealingCapture : MonoBehaviour
    {
        private ExperienceDirector director;
        private IEEGSimulationControl simulation;
        private string outputDirectory;
        private int requestedStage = -1;

        public void Initialize(ExperienceDirector experienceDirector, IEEGSimulationControl simulationControl)
        {
            director = experienceDirector;
            simulation = simulationControl;
            var arguments = System.Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length; index++)
            {
                if (arguments[index] == "--capture-artifacts" && index + 1 < arguments.Length)
                {
                    var expectedDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../Artifacts"));
                    var requestedDirectory = Path.GetFullPath(arguments[index + 1]);
                    if (!string.Equals(expectedDirectory, requestedDirectory, System.StringComparison.Ordinal))
                    {
                        Debug.LogError("Capture output must be the project Artifacts directory.");
                        Application.Quit(2);
                        return;
                    }
                    outputDirectory = expectedDirectory;
                }
                else if (arguments[index] == "--capture-stage" && index + 1 < arguments.Length)
                {
                    int.TryParse(arguments[index + 1], out requestedStage);
                }
            }

            if (!string.IsNullOrEmpty(outputDirectory))
            {
                StartCoroutine(CaptureStages());
            }
        }

        private IEnumerator CaptureStages()
        {
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
            yield break;
#else
            if (simulation == null)
            {
                Debug.LogError("Capture mode requires an EEG simulation control.");
                Application.Quit(2);
                yield break;
            }
            Directory.CreateDirectory(outputDirectory);
            if ((new DirectoryInfo(outputDirectory).Attributes & FileAttributes.ReparsePoint) != 0)
            {
                Debug.LogError("Capture output cannot be a symbolic link.");
                Application.Quit(2);
                yield break;
            }
            simulation.SetSignalAvailable(true);
            simulation.SetAutoSimulation(false);
            simulation.SetManualState(new EEGState
            {
                signalQuality = 1f,
                relaxation = 0.78f,
                attention = 0.42f,
                fatigue = 0.36f
            });
            director.StartSession(ExperienceMode.Demo);
            yield return null;

            var stages = requestedStage >= 0
                ? new[] { (ExperienceStage)Mathf.Clamp(requestedStage, 0, 4) }
                : (ExperienceStage[])System.Enum.GetValues(typeof(ExperienceStage));

            foreach (var stage in stages)
            {
                director.ForceStageForCapture(stage);
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                yield return new WaitForSecondsRealtime(0.3f);
                var fileName = $"{(int)stage + 1:00}-{stage}.png";
                SaveCameraFrame(Path.Combine(outputDirectory, fileName));
                yield return new WaitForSecondsRealtime(0.5f);
            }

            yield return new WaitForSecondsRealtime(0.75f);
            Application.Quit(0);
#endif
        }

        private static void SaveCameraFrame(string path)
        {
            const int width = 1440;
            const int height = 900;
            var camera = Camera.main;
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;

            camera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            camera.Render();
            screenshot.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            screenshot.Apply();
            var encoded = screenshot.EncodeToPNG();
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(encoded, 0, encoded.Length);
            }

            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Destroy(renderTexture);
            Destroy(screenshot);
        }
    }
}
