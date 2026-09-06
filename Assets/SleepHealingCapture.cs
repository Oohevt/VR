using System.Collections;
using System.IO;
using SleepHealing.Core;
using SleepHealing.EEG;
using UnityEngine;

namespace SleepHealing.App
{
    /// <summary>
    /// 开发构建下按命令行参数逐阶段截图：--capture-artifacts <绝对目录> [--capture-stage N]。
    /// 用 ScreenCapture 抓最终画面，后处理与界面都包含在内。
    /// </summary>
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
                    var requested = arguments[index + 1];
                    if (!Path.IsPathRooted(requested))
                    {
                        Debug.LogError("Capture output must be an absolute directory.");
                        Application.Quit(2);
                        return;
                    }
                    outputDirectory = Path.GetFullPath(requested);
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
            // 让反射探针、粒子预热和天空盒环境光先稳定几帧。
            yield return new WaitForSecondsRealtime(1.5f);

            var stages = requestedStage >= 0
                ? new[] { (ExperienceStage)Mathf.Clamp(requestedStage, 0, 4) }
                : (ExperienceStage[])System.Enum.GetValues(typeof(ExperienceStage));

            foreach (var stage in stages)
            {
                director.ForceStageForCapture(stage);
                yield return new WaitForSecondsRealtime(3f);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, $"{(int)stage + 1:00}-{stage}.png"));
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                yield return new WaitForSecondsRealtime(0.5f);
            }

            yield return new WaitForSecondsRealtime(0.75f);
            Application.Quit(0);
#endif
        }
    }
}
