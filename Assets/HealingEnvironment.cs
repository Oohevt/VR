using SleepHealing.EEG;
using UnityEngine;

namespace SleepHealing.Core
{
    public sealed class HealingEnvironment : MonoBehaviour
    {
        private Camera viewingCamera;
        private Light ambientKey;
        private Transform breathingOrb;
        private Transform starField;
        private ParticleSystem stars;
        private Material orbMaterial;
        private AudioSource ambientAudio;
        private float targetIntensity = 0.35f;
        private float currentIntensity = 0.35f;
        private float targetMotion = 0.05f;
        private float currentMotion = 0.05f;
        private float targetAudio = 0.025f;
        private bool initialized;
        private bool safeState;

        public float VisualIntensity => currentIntensity;
        public float MotionSpeed => currentMotion;
        public float AudioLevel => ambientAudio != null ? ambientAudio.volume : 0f;

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.008f;
            RenderSettings.fogColor = new Color(0.012f, 0.018f, 0.055f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.018f, 0.025f, 0.07f);

            CreateCamera();
            CreateCabin();
            CreateStarField();
            CreateBreathingOrb();
            CreateAmbientSound();
        }

        public void Apply(ExperienceStage stage, EEGState state, bool adaptive, float deltaSeconds)
        {
            if (!initialized)
            {
                Initialize();
            }

            if (safeState)
            {
                return;
            }

            var stageBase = StageBaseIntensity(stage);
            var relaxation = adaptive ? state.relaxation : 0.45f;
            targetIntensity = Mathf.Clamp01(stageBase + (relaxation - 0.5f) * 0.28f);
            targetMotion = Mathf.Lerp(0.025f, 0.12f, 1f - relaxation);
            targetAudio = Mathf.Lerp(0.018f, 0.065f, targetIntensity);

            var response = 1f - Mathf.Exp(-Mathf.Max(0f, deltaSeconds) * 0.7f);
            currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity, response);
            currentMotion = Mathf.Lerp(currentMotion, targetMotion, response);
            ambientAudio.volume = Mathf.Lerp(ambientAudio.volume, targetAudio, response);
            ambientAudio.pitch = Mathf.Lerp(0.92f, 1.02f, relaxation);

            var midnight = new Color(0.035f, 0.075f, 0.19f);
            var warmGold = new Color(0.95f, 0.60f, 0.22f);
            var glow = Color.Lerp(midnight, warmGold, currentIntensity * 0.72f);
            ambientKey.color = glow;
            ambientKey.intensity = Mathf.Lerp(0.35f, 1.25f, currentIntensity);
            orbMaterial.SetColor("_Color", glow);
            if (orbMaterial.HasProperty("_EmissionColor"))
            {
                orbMaterial.EnableKeyword("_EMISSION");
                orbMaterial.SetColor("_EmissionColor", glow * Mathf.Lerp(0.8f, 2.4f, currentIntensity));
            }

            var starMain = stars.main;
            starMain.startColor = Color.Lerp(new Color(0.38f, 0.48f, 0.82f, 0.55f), new Color(1f, 0.78f, 0.38f, 0.95f), currentIntensity);
            starMain.startSize = Mathf.Lerp(0.16f, 0.42f, currentIntensity);
            var emission = stars.emission;
            emission.rateOverTime = Mathf.Lerp(8f, 26f, currentIntensity);

            var breath = 1f + Mathf.Sin(Time.unscaledTime * Mathf.PI / 3f) * 0.055f;
            breathingOrb.localScale = Vector3.one * breath;
            starField.Rotate(Vector3.up, currentMotion * Mathf.Max(0f, deltaSeconds), Space.World);
        }

        public void EnterSafeState()
        {
            safeState = true;
            targetIntensity = 0.22f;
            targetMotion = 0f;
            targetAudio = 0.005f;
            currentIntensity = targetIntensity;
            currentMotion = 0f;
            if (ambientAudio != null)
            {
                ambientAudio.volume = targetAudio;
            }
            if (stars != null)
            {
                stars.Pause(true);
            }
            if (breathingOrb != null)
            {
                breathingOrb.localScale = Vector3.one;
            }
        }

        public void ResumeSession()
        {
            safeState = false;
            if (stars != null && !stars.isPlaying) stars.Play(true);
            if (ambientAudio != null && !ambientAudio.isPlaying) ambientAudio.Play();
        }

        private void CreateCamera()
        {
            var existing = Camera.main;
            if (existing != null)
            {
                viewingCamera = existing;
            }
            else
            {
                var cameraObject = new GameObject("疗愈视点");
                cameraObject.transform.SetParent(transform, false);
                viewingCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                cameraObject.tag = "MainCamera";
            }

            viewingCamera.transform.position = new Vector3(0f, 0.1f, -0.4f);
            viewingCamera.transform.rotation = Quaternion.identity;
            viewingCamera.fieldOfView = 72f;
            viewingCamera.nearClipPlane = 0.05f;
            viewingCamera.farClipPlane = 120f;
            viewingCamera.clearFlags = CameraClearFlags.SolidColor;
            viewingCamera.backgroundColor = new Color(0.004f, 0.006f, 0.025f);
            viewingCamera.allowHDR = true;
        }

        private void CreateCabin()
        {
            var shell = new GameObject("疗愈仓结构");
            shell.transform.SetParent(transform, false);
            var shellMaterial = CreateMaterial("仓体深靛", new Color(0.025f, 0.035f, 0.085f), 0.42f, 0.78f);
            var trimMaterial = CreateMaterial("暖金导光", new Color(0.42f, 0.24f, 0.075f), 0.65f, 0.55f);

            var floor = CreatePrimitive(PrimitiveType.Cylinder, "悬浮舱台", shell.transform, shellMaterial);
            floor.transform.position = new Vector3(0f, -1.35f, 3.8f);
            floor.transform.localScale = new Vector3(3.7f, 0.12f, 5.3f);

            for (var index = 0; index < 14; index++)
            {
                var angle = Mathf.Lerp(-68f, 68f, index / 13f) * Mathf.Deg2Rad;
                var rib = CreatePrimitive(PrimitiveType.Cube, $"舱壁导光-{index + 1:00}", shell.transform, index % 3 == 0 ? trimMaterial : shellMaterial);
                rib.transform.position = new Vector3(Mathf.Sin(angle) * 4.2f, 1.1f + Mathf.Cos(angle) * 2.7f, 4.7f);
                rib.transform.localScale = new Vector3(0.055f, 5.4f, 0.12f);
                rib.transform.rotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
            }

            for (var index = 0; index < 7; index++)
            {
                var rail = CreatePrimitive(PrimitiveType.Cube, $"纵向星轨-{index + 1:00}", shell.transform, trimMaterial);
                rail.transform.position = new Vector3(Mathf.Lerp(-3.1f, 3.1f, index / 6f), 2.9f, 6.8f);
                rail.transform.localScale = new Vector3(0.025f, 0.025f, 5.5f);
            }
        }

        private void CreateStarField()
        {
            starField = new GameObject("星穹").transform;
            starField.SetParent(transform, false);
            stars = starField.gameObject.AddComponent<ParticleSystem>();
            var main = stars.main;
            main.loop = true;
            main.duration = 20f;
            main.startLifetime = 80f;
            main.startSpeed = 0.012f;
            main.startSize = 0.28f;
            main.maxParticles = 1800;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = 0f;

            var shape = stars.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 34f;
            shape.radiusThickness = 0.28f;

            var renderer = stars.GetComponent<ParticleSystemRenderer>();
            renderer.material = CreateMaterial("星尘", new Color(0.65f, 0.74f, 1f), 0f, 0f, true);
            stars.Simulate(60f, true, true, true);
            stars.Play();
        }

        private void CreateBreathingOrb()
        {
            breathingOrb = CreatePrimitive(PrimitiveType.Sphere, "呼吸光核", transform, null).transform;
            breathingOrb.position = new Vector3(0.72f, -0.12f, 4.4f);
            breathingOrb.localScale = Vector3.one;
            orbMaterial = CreateMaterial("呼吸光材质", new Color(0.88f, 0.53f, 0.18f), 0.15f, 0.5f);
            breathingOrb.GetComponent<Renderer>().material = orbMaterial;

            ambientKey = breathingOrb.gameObject.AddComponent<Light>();
            ambientKey.type = LightType.Point;
            ambientKey.range = 14f;
            ambientKey.shadows = LightShadows.Soft;
            ambientKey.intensity = 0.75f;
        }

        private void CreateAmbientSound()
        {
            ambientAudio = gameObject.AddComponent<AudioSource>();
            ambientAudio.playOnAwake = false;
            ambientAudio.loop = true;
            ambientAudio.spatialBlend = 0.18f;
            ambientAudio.volume = 0.025f;

            const int sampleRate = 22050;
            const int seconds = 12;
            var samples = new float[sampleRate * seconds];
            for (var index = 0; index < samples.Length; index++)
            {
                var t = index / (float)sampleRate;
                var pulse = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI / 3f);
                samples[index] = (Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.22f + Mathf.Sin(2f * Mathf.PI * 164.81f * t) * 0.1f) * pulse;
            }

            var clip = AudioClip.Create("程序化低频星声", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            ambientAudio.clip = clip;
            ambientAudio.Play();
        }

        private static float StageBaseIntensity(ExperienceStage stage)
        {
            return stage switch
            {
                ExperienceStage.Preparation => 0.22f,
                ExperienceStage.Baseline => 0.32f,
                ExperienceStage.Breathing => 0.48f,
                ExperienceStage.DeepHealing => 0.68f,
                ExperienceStage.Awakening => 0.42f,
                _ => 0.3f
            };
        }

        private static GameObject CreatePrimitive(PrimitiveType type, string objectName, Transform parent, Material material)
        {
            var instance = GameObject.CreatePrimitive(type);
            instance.name = objectName;
            instance.transform.SetParent(parent, false);
            var collider = instance.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            if (material != null)
            {
                instance.GetComponent<Renderer>().material = material;
            }

            return instance;
        }

        private static Material CreateMaterial(string materialName, Color color, float metallic, float smoothness, bool particle = false)
        {
            var shader = Shader.Find(particle ? "Particles/Standard Unlit" : "Standard") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = materialName, color = color };
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            return material;
        }
    }
}
