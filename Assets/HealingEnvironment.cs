using SleepHealing.EEG;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SleepHealing.Core
{
    /// <summary>
    /// 星空疗愈舱视觉：程序化 HDR 星穹、镜面舱台、发光穹顶光弧、呼吸光核与近景星尘，全部运行时生成。
    /// 依赖 URP 后处理（Bloom / ACES）把 HDR 发光变成柔和辉光。
    /// </summary>
    public sealed class HealingEnvironment : MonoBehaviour
    {
        private static readonly Color CoolGuide = new Color(0.30f, 0.48f, 0.95f);
        private static readonly Color WarmGold = new Color(1.00f, 0.66f, 0.30f);
        private static readonly Vector3 DomeCenter = new Vector3(0f, -1.3f, 2.0f);
        private const float DomeRadius = 3.4f;
        private const float ProbeRefreshSeconds = 2.5f;
        private const int GlowLayer = 1; // TransparentFX：光晕与星尘不进反射探针

        private Camera viewingCamera;
        private Light orbLight;
        private Transform breathingOrb;
        private Transform halo;
        private Transform starField;
        private ParticleSystem stars;
        private ReflectionProbe reflectionProbe;
        private Material orbCoreMaterial;
        private Material haloMaterial;
        private Material lineMaterial;
        private Material starMaterial;
        private Material skyMaterial;
        private AudioSource ambientAudio;
        private float targetIntensity = 0.35f;
        private float currentIntensity = 0.35f;
        private float targetMotion = 0.05f;
        private float currentMotion = 0.05f;
        private float targetAudio = 0.025f;
        private float skyRotation;
        private float probeTimer;
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
            CreateCamera();
            CreateSky();
            CreateLighting();
            CreatePostProcessing();
            CreatePlatform();
            CreateDome();
            CreateStarDust();
            CreateBreathingOrb();
            CreateReflectionProbe();
            CreateAmbientSound();
            ApplyVisuals(0f);
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

            var delta = Mathf.Max(0f, deltaSeconds);
            var response = 1f - Mathf.Exp(-delta * 0.7f);
            currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity, response);
            currentMotion = Mathf.Lerp(currentMotion, targetMotion, response);
            ambientAudio.volume = Mathf.Lerp(ambientAudio.volume, targetAudio, response);
            ambientAudio.pitch = Mathf.Lerp(0.92f, 1.02f, relaxation);

            skyRotation = (skyRotation + currentMotion * delta) % 360f;
            starField.Rotate(Vector3.up, currentMotion * delta, Space.World);
            ApplyVisuals(delta);
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
            if (initialized)
            {
                ApplyVisuals(0f);
            }
        }

        public void ResumeSession()
        {
            safeState = false;
            if (stars != null && !stars.isPlaying) stars.Play(true);
            if (ambientAudio != null && !ambientAudio.isPlaying) ambientAudio.Play();
        }

        private void ApplyVisuals(float delta)
        {
            var intensity = currentIntensity;
            var glow = Color.Lerp(CoolGuide, WarmGold, intensity * 0.85f);
            var breath = safeState ? 1f : 1f + Mathf.Sin(Time.unscaledTime * Mathf.PI / 3f) * 0.05f;

            orbCoreMaterial.SetColor("_EmissionColor", glow * Mathf.Lerp(0.9f, 2.4f, intensity));
            var haloTint = glow * Mathf.Lerp(1.0f, 1.6f, intensity);
            haloTint.a = Mathf.Lerp(0.15f, 0.45f, intensity);
            haloMaterial.SetColor("_BaseColor", haloTint);
            orbLight.color = glow;
            orbLight.intensity = Mathf.Lerp(0.4f, 2.0f, intensity);
            breathingOrb.localScale = Vector3.one * (0.6f * breath);
            halo.localScale = Vector3.one * (2.0f * breath);
            halo.rotation = Quaternion.LookRotation(halo.position - viewingCamera.transform.position);

            var lineTint = glow * Mathf.Lerp(1.0f, 2.2f, intensity);
            lineTint.a = 1f;
            lineMaterial.SetColor("_BaseColor", lineTint);

            var starTint = Color.Lerp(new Color(0.55f, 0.70f, 1f), new Color(1f, 0.85f, 0.60f), intensity) * Mathf.Lerp(0.9f, 1.8f, intensity);
            starTint.a = 1f;
            starMaterial.SetColor("_BaseColor", starTint);
            var emission = stars.emission;
            emission.rateOverTime = Mathf.Lerp(3f, 10f, intensity);
            var main = stars.main;
            main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Lerp(0.008f, 0.014f, intensity), Mathf.Lerp(0.02f, 0.035f, intensity));

            skyMaterial.SetFloat("_Exposure", Mathf.Lerp(0.7f, 1.15f, intensity));
            skyMaterial.SetFloat("_Rotation", skyRotation);

            probeTimer -= delta;
            if (probeTimer <= 0f)
            {
                probeTimer = ProbeRefreshSeconds;
                reflectionProbe.RenderProbe();
            }
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
            viewingCamera.clearFlags = CameraClearFlags.Skybox;
            viewingCamera.allowHDR = true;
            viewingCamera.allowMSAA = true;

            var cameraData = viewingCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.dithering = true;
            cameraData.renderShadows = false;
        }

        private void CreateSky()
        {
            skyMaterial = InstantiateTemplate("Skybox", "Skybox/Cubemap");
            skyMaterial.SetTexture("_Tex", StarSky.CreateCubemap(512, 20260906));
            skyMaterial.SetFloat("_Exposure", 1f);
            RenderSettings.skybox = skyMaterial;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.5f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.defaultReflectionResolution = 128;
            RenderSettings.reflectionIntensity = 1f;
            DynamicGI.UpdateEnvironment();
        }

        private void CreateLighting()
        {
            var moon = new GameObject("月光").AddComponent<Light>();
            moon.transform.SetParent(transform, false);
            moon.transform.rotation = Quaternion.Euler(52f, 205f, 0f);
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.66f, 1f);
            moon.intensity = 0.2f;
            moon.shadows = LightShadows.None;
        }

        private void CreatePostProcessing()
        {
            var volumeObject = new GameObject("画面后处理");
            volumeObject.transform.SetParent(transform, false);
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "星空深睡画面";
            volume.sharedProfile = profile;

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 1.2f;
            bloom.intensity.value = 0.55f;
            bloom.scatter.value = 0.6f;
            bloom.maxIterations.value = 5;
            bloom.highQualityFiltering.value = false;

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.value = TonemappingMode.ACES;

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.value = 0f;
            color.contrast.value = 10f;
            color.saturation.value = 12f;

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.25f;
            vignette.smoothness.value = 0.5f;
        }

        private void CreatePlatform()
        {
            var platformMaterial = CreateLit("镜面舱台材质", new Color(0.05f, 0.06f, 0.10f), 0.35f, 0.9f);
            var platform = CreatePrimitive(PrimitiveType.Cylinder, "镜面舱台", transform, platformMaterial);
            platform.transform.position = DomeCenter + Vector3.down * 0.02f;
            platform.transform.localScale = new Vector3(DomeRadius * 2f + 0.1f, 0.02f, DomeRadius * 2f + 0.1f);

            lineMaterial = InstantiateTemplate("Additive", "Universal Render Pipeline/Particles/Unlit");
            lineMaterial.name = "线光材质";

            const int segments = 96;
            var ring = new Vector3[segments];
            for (var index = 0; index < segments; index++)
            {
                var angle = index / (float)segments * Mathf.PI * 2f;
                ring[index] = DomeCenter + new Vector3(Mathf.Sin(angle) * (DomeRadius + 0.02f), 0.015f, Mathf.Cos(angle) * (DomeRadius + 0.02f));
            }
            var ringGradient = MakeGradient(
                new[] { (0f, new Color(1f, 0.86f, 0.62f)), (1f, new Color(1f, 0.86f, 0.62f)) },
                new[] { (0f, 0.8f), (1f, 0.8f) });
            CreateLine("舱台光环", ring, true, 0.02f, 0.02f, ringGradient);
        }

        private void CreateDome()
        {
            var arcGradient = MakeGradient(
                new[] { (0f, new Color(1f, 0.92f, 0.80f)), (0.5f, new Color(0.92f, 0.94f, 1f)), (1f, new Color(0.85f, 0.90f, 1f)) },
                new[] { (0f, 0f), (0.12f, 0.85f), (0.55f, 0.45f), (1f, 0f) });

            const int arcs = 12;
            const int points = 36;
            for (var index = 0; index < arcs; index++)
            {
                var azimuth = (index + 0.5f) * (360f / arcs) * Mathf.Deg2Rad;
                var arc = new Vector3[points];
                for (var point = 0; point < points; point++)
                {
                    var elevation = Mathf.Lerp(1f, 80f, point / (float)(points - 1)) * Mathf.Deg2Rad;
                    arc[point] = DomeCenter + new Vector3(
                        Mathf.Cos(elevation) * Mathf.Sin(azimuth),
                        Mathf.Sin(elevation),
                        Mathf.Cos(elevation) * Mathf.Cos(azimuth)) * DomeRadius;
                }
                CreateLine($"穹顶光弧-{index + 1:00}", arc, false, 0.03f, 0.01f, arcGradient);
            }
        }

        private void CreateStarDust()
        {
            starField = new GameObject("星尘").transform;
            starField.SetParent(transform, false);
            starField.position = DomeCenter + Vector3.up * 1.4f;
            starField.gameObject.layer = GlowLayer;
            stars = starField.gameObject.AddComponent<ParticleSystem>();
            stars.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = stars.main;
            main.loop = true;
            main.duration = 30f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(18f, 40f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.04f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.04f);
            main.startColor = Color.white;
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = 0f;

            var emission = stars.emission;
            emission.rateOverTime = 6f;

            var shape = stars.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 3.0f;
            shape.radiusThickness = 1f;

            var colorOverLifetime = stars.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(MakeGradient(
                new[] { (0f, Color.white), (1f, Color.white) },
                new[] { (0f, 0f), (0.25f, 1f), (0.75f, 1f), (1f, 0f) }));

            var noise = stars.noise;
            noise.enabled = true;
            noise.strength = 0.05f;
            noise.frequency = 0.15f;
            noise.scrollSpeed = 0.05f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            starMaterial = InstantiateTemplate("Additive", "Universal Render Pipeline/Particles/Unlit");
            starMaterial.name = "星尘材质";
            starMaterial.SetTexture("_BaseMap", StarSky.CreateSoftSprite(64));
            var renderer = stars.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.None;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.material = starMaterial;

            stars.Simulate(30f, true, true, true);
            stars.Play();
        }

        private void CreateBreathingOrb()
        {
            breathingOrb = new GameObject("呼吸光核").transform;
            breathingOrb.SetParent(transform, false);
            breathingOrb.position = new Vector3(0f, 0.35f, 3.6f);

            orbCoreMaterial = CreateLit("光核材质", new Color(0.12f, 0.06f, 0.02f), 0f, 0.4f);
            var core = CreatePrimitive(PrimitiveType.Sphere, "光核", breathingOrb, orbCoreMaterial);
            core.transform.localScale = Vector3.one * 0.7f;

            orbLight = breathingOrb.gameObject.AddComponent<Light>();
            orbLight.type = LightType.Point;
            orbLight.range = 12f;
            orbLight.shadows = LightShadows.None;
            orbLight.intensity = 1f;

            haloMaterial = InstantiateTemplate("Additive", "Universal Render Pipeline/Particles/Unlit");
            haloMaterial.name = "光晕材质";
            haloMaterial.SetTexture("_BaseMap", StarSky.CreateSoftSprite(128));
            halo = CreatePrimitive(PrimitiveType.Quad, "光晕", transform, haloMaterial).transform;
            halo.position = breathingOrb.position;
            halo.gameObject.layer = GlowLayer;
        }

        private void CreateReflectionProbe()
        {
            var probeObject = new GameObject("环境反射");
            probeObject.transform.SetParent(transform, false);
            probeObject.transform.position = DomeCenter + Vector3.up * 1.8f;
            reflectionProbe = probeObject.AddComponent<ReflectionProbe>();
            reflectionProbe.mode = ReflectionProbeMode.Realtime;
            reflectionProbe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            reflectionProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            reflectionProbe.resolution = 256;
            reflectionProbe.hdr = true;
            reflectionProbe.size = new Vector3(DomeRadius * 2f + 0.2f, 3.6f, DomeRadius * 2f + 0.2f);
            reflectionProbe.boxProjection = true;
            reflectionProbe.cullingMask = ~(1 << GlowLayer);
            reflectionProbe.nearClipPlane = 0.1f;
            reflectionProbe.farClipPlane = 60f;
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

        private LineRenderer CreateLine(string objectName, Vector3[] points, bool loop, float widthStart, float widthEnd, Gradient gradient)
        {
            var lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = loop;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.widthCurve = AnimationCurve.Linear(0f, widthStart, 1f, widthEnd);
            line.colorGradient = gradient;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCornerVertices = 2;
            line.numCapVertices = 3;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = lineMaterial;
            return line;
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

        private static Gradient MakeGradient((float time, Color color)[] colors, (float time, float alpha)[] alphas)
        {
            var colorKeys = new GradientColorKey[colors.Length];
            for (var index = 0; index < colors.Length; index++)
            {
                colorKeys[index] = new GradientColorKey(colors[index].color, colors[index].time);
            }
            var alphaKeys = new GradientAlphaKey[alphas.Length];
            for (var index = 0; index < alphas.Length; index++)
            {
                alphaKeys[index] = new GradientAlphaKey(alphas[index].alpha, alphas[index].time);
            }
            var gradient = new Gradient();
            gradient.SetKeys(colorKeys, alphaKeys);
            return gradient;
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

            var renderer = instance.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (material != null)
            {
                renderer.material = material;
            }

            return instance;
        }

        private static Material CreateLit(string materialName, Color baseColor, float metallic, float smoothness)
        {
            var material = InstantiateTemplate("Lit", "Universal Render Pipeline/Lit");
            material.name = materialName;
            material.SetColor("_BaseColor", baseColor);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.SetColor("_EmissionColor", Color.black);
            return material;
        }

        // 材质模板由编辑器脚本生成到 Resources，保证 URP 着色器与关键字变体进入打包；缺失时退回 Shader.Find。
        private static Material InstantiateTemplate(string templateName, string fallbackShader)
        {
            var template = Resources.Load<Material>("SleepHealing/" + templateName);
            if (template != null)
            {
                return new Material(template);
            }

            var shader = Shader.Find(fallbackShader) ?? Shader.Find("Unlit/Color");
            return new Material(shader);
        }
    }
}
