using System;
using System.Threading.Tasks;
using UnityEngine;

namespace SleepHealing.Core
{
    /// <summary>
    /// 程序化 HDR 星空：深靛蓝渐变、银河带与幂律亮度分布的恒星，启动时在 CPU 上生成，不依赖任何外部素材。
    /// 亮星的数值超过 1.0，交给后处理 Bloom 产生辉光。
    /// </summary>
    public static class StarSky
    {
        private static readonly Color Zenith = new Color(0.0008f, 0.0013f, 0.0050f);
        private static readonly Color Horizon = new Color(0.0065f, 0.0110f, 0.0320f);
        private static readonly Color Below = new Color(0.0005f, 0.0008f, 0.0025f);
        private static readonly Color BandBlue = new Color(0.34f, 0.46f, 0.92f);
        private static readonly Color BandDust = new Color(0.90f, 0.64f, 0.48f);
        private static readonly Color Nebula = new Color(0.12f, 0.16f, 0.40f);
        private static readonly Vector3 BandNormal = new Vector3(0.38f, 0.58f, -0.72f).normalized;

        public static Cubemap CreateCubemap(int size, int seed)
        {
            var faces = new Color[6][];
            for (var face = 0; face < 6; face++)
            {
                faces[face] = new Color[size * size];
            }

            Parallel.For(0, 6 * size, row =>
            {
                var face = row / size;
                var y = row % size;
                var pixels = faces[face];
                var v = (y + 0.5f) / size * 2f - 1f;
                for (var x = 0; x < size; x++)
                {
                    var u = (x + 0.5f) / size * 2f - 1f;
                    pixels[y * size + x] = ShadeSky(FaceDirection(face, u, v).normalized);
                }
            });

            SplatStars(faces, size, seed);

            var cubemap = new Cubemap(size, TextureFormat.RGBAHalf, false)
            {
                name = "程序化星穹",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            for (var face = 0; face < 6; face++)
            {
                cubemap.SetPixels(faces[face], (CubemapFace)face);
            }
            cubemap.Apply(false, true);
            return cubemap;
        }

        public static Texture2D CreateSoftSprite(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "柔光粒子",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            var half = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x + 0.5f - half) / half;
                    var dy = (y + 0.5f - half) / half;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var edge = Mathf.Clamp01(1f - distance);
                    var smooth = edge * edge * (3f - 2f * edge);
                    var alpha = Mathf.Pow(smooth, 1.6f) * 0.7f + Mathf.Exp(-distance * distance * 22f) * 0.5f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static Color ShadeSky(Vector3 direction)
        {
            var elevation = direction.y;
            var sky = elevation >= 0f
                ? Color.Lerp(Zenith, Horizon, Mathf.Pow(1f - elevation, 2.4f))
                : Color.Lerp(Horizon, Below, Mathf.Sqrt(Mathf.Clamp01(-elevation * 3f)));

            var bandDistance = Vector3.Dot(direction, BandNormal);
            var bandCore = Mathf.Exp(-bandDistance * bandDistance / (2f * 0.13f * 0.13f));
            var bandWide = Mathf.Exp(-bandDistance * bandDistance / (2f * 0.34f * 0.34f));
            var cloud = Fbm(direction * 3.4f + new Vector3(11.7f, 3.1f, 27.9f), 4);
            var lane = Fbm(direction * 7.1f + new Vector3(41.3f, 17.7f, 5.2f), 3);
            var lanes = 1f - 0.7f * lane * lane;
            var band = (bandCore * 0.035f + bandWide * 0.009f) * (0.25f + 1.6f * cloud * cloud) * lanes;
            sky += BandBlue * band + BandDust * (band * 0.6f * cloud);

            var haze = Fbm(direction * 1.7f + new Vector3(3.3f, 71.1f, 9.4f), 3);
            sky += Nebula * (Mathf.Pow(haze, 2.6f) * 0.018f);
            sky.a = 1f;
            return sky;
        }

        private static void SplatStars(Color[][] faces, int size, int seed)
        {
            var random = new System.Random(seed);
            var placed = 0;
            while (placed < 9000)
            {
                var z = random.NextDouble() * 2.0 - 1.0;
                var angle = random.NextDouble() * Math.PI * 2.0;
                var radial = Math.Sqrt(1.0 - z * z);
                var direction = new Vector3((float)(radial * Math.Cos(angle)), (float)z, (float)(radial * Math.Sin(angle)));
                var bandDistance = Vector3.Dot(direction, BandNormal);
                var acceptance = 0.28f + 0.72f * Mathf.Exp(-bandDistance * bandDistance / (2f * 0.24f * 0.24f));
                if (random.NextDouble() > acceptance)
                {
                    continue;
                }
                placed++;

                var magnitude = Mathf.Max((float)random.NextDouble(), 0.0015f);
                var brightness = Mathf.Min(7f, 0.05f * Mathf.Pow(magnitude, -0.78f));
                var warmth = Mathf.Pow((float)random.NextDouble(), 1.6f);
                var tint = Color.Lerp(new Color(0.70f, 0.80f, 1f), new Color(1f, 0.88f, 0.70f), warmth);
                if (random.NextDouble() < 0.04)
                {
                    tint = new Color(1f, 0.62f, 0.45f);
                }
                var sigma = 0.55f + 0.30f * Mathf.Log10(1f + brightness * 5f);
                var radius = Mathf.CeilToInt(sigma * 2.8f);
                var color = tint * brightness;

                for (var face = 0; face < 6; face++)
                {
                    if (!ProjectToFace(face, direction, out var u, out var v))
                    {
                        continue;
                    }
                    var margin = (radius + 1f) * 2f / size;
                    if (Mathf.Abs(u) > 1f + margin || Mathf.Abs(v) > 1f + margin)
                    {
                        continue;
                    }
                    var px = (u + 1f) * 0.5f * size - 0.5f;
                    var py = (v + 1f) * 0.5f * size - 0.5f;
                    var cx = Mathf.RoundToInt(px);
                    var cy = Mathf.RoundToInt(py);
                    var pixels = faces[face];
                    for (var y = cy - radius; y <= cy + radius; y++)
                    {
                        if (y < 0 || y >= size)
                        {
                            continue;
                        }
                        for (var x = cx - radius; x <= cx + radius; x++)
                        {
                            if (x < 0 || x >= size)
                            {
                                continue;
                            }
                            var dx = x - px;
                            var dy = y - py;
                            var weight = Mathf.Exp(-(dx * dx + dy * dy) / (2f * sigma * sigma));
                            if (weight < 0.01f)
                            {
                                continue;
                            }
                            var index = y * size + x;
                            var existing = pixels[index];
                            pixels[index] = new Color(
                                existing.r + color.r * weight,
                                existing.g + color.g * weight,
                                existing.b + color.b * weight,
                                1f);
                        }
                    }
                }
            }
        }

        // Unity 立方体贴图约定：每个面 (u, v) ∈ [-1, 1]，v 向下为正（第 0 行是图像顶部）。
        private static Vector3 FaceDirection(int face, float u, float v)
        {
            switch (face)
            {
                case 0: return new Vector3(1f, -v, -u);
                case 1: return new Vector3(-1f, -v, u);
                case 2: return new Vector3(u, 1f, v);
                case 3: return new Vector3(u, -1f, -v);
                case 4: return new Vector3(u, -v, 1f);
                default: return new Vector3(-u, -v, -1f);
            }
        }

        private static bool ProjectToFace(int face, Vector3 d, out float u, out float v)
        {
            float major;
            switch (face)
            {
                case 0: major = d.x; u = -d.z; v = -d.y; break;
                case 1: major = -d.x; u = d.z; v = -d.y; break;
                case 2: major = d.y; u = d.x; v = d.z; break;
                case 3: major = -d.y; u = d.x; v = -d.z; break;
                case 4: major = d.z; u = d.x; v = -d.y; break;
                default: major = -d.z; u = -d.x; v = -d.y; break;
            }
            if (major <= 1e-5f)
            {
                return false;
            }
            u /= major;
            v /= major;
            return true;
        }

        private static float Hash(int x, int y, int z)
        {
            unchecked
            {
                var h = x * 374761393 + y * 668265263 + z * 1274126177;
                h = (h ^ (h >> 13)) * 1103515245;
                h ^= h >> 16;
                return (h & 0x7fffffff) * (1f / 0x7fffffff);
            }
        }

        private static float Noise(Vector3 p)
        {
            var x0 = Mathf.FloorToInt(p.x);
            var y0 = Mathf.FloorToInt(p.y);
            var z0 = Mathf.FloorToInt(p.z);
            var fx = p.x - x0;
            var fy = p.y - y0;
            var fz = p.z - z0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            fz = fz * fz * (3f - 2f * fz);

            var x00 = Mathf.Lerp(Hash(x0, y0, z0), Hash(x0 + 1, y0, z0), fx);
            var x10 = Mathf.Lerp(Hash(x0, y0 + 1, z0), Hash(x0 + 1, y0 + 1, z0), fx);
            var x01 = Mathf.Lerp(Hash(x0, y0, z0 + 1), Hash(x0 + 1, y0, z0 + 1), fx);
            var x11 = Mathf.Lerp(Hash(x0, y0 + 1, z0 + 1), Hash(x0 + 1, y0 + 1, z0 + 1), fx);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, fy), Mathf.Lerp(x01, x11, fy), fz);
        }

        private static float Fbm(Vector3 p, int octaves)
        {
            var sum = 0f;
            var amplitude = 0.5f;
            var total = 0f;
            for (var octave = 0; octave < octaves; octave++)
            {
                sum += Noise(p) * amplitude;
                total += amplitude;
                p = p * 2.07f + new Vector3(19.1f, 7.3f, 3.7f);
                amplitude *= 0.5f;
            }
            return sum / total;
        }
    }
}
