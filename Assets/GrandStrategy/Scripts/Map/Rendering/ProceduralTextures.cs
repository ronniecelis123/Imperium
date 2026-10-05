using UnityEngine;

namespace GrandStrategy.Map.Rendering
{
    /// <summary>
    /// Texturas pequeñas y cíclicas (sin costuras) generadas una vez al arrancar, para no depender de
    /// archivos externos: normales del oleaje y ruido de detalle del terreno/espuma.
    /// Muestrear una textura es mucho más barato que calcular ruido en cada píxel.
    /// </summary>
    public static class ProceduralTextures
    {
        /// <summary>
        /// Normales de agua (RG = XZ codificado en 0..1), calculadas a partir de un ruido fractal cíclico:
        /// isótropo (sin franjas repetitivas) y sin costuras.
        /// </summary>
        public static Texture2D CreateWaterNormals(int size = 256, int seed = 7)
        {
            var h = PeriodicFbm(size, seed, 6, 5, 0.5f);
            var px = new Color32[size * size];
            const float strength = 9f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int xl = (x - 1 + size) % size, xr = (x + 1) % size;
                int yd = (y - 1 + size) % size, yu = (y + 1) % size;
                float dx = (h[y * size + xr] - h[y * size + xl]) * strength;
                float dy = (h[yu * size + x] - h[yd * size + x]) * strength;
                var n = new Vector3(-dx, 1f, -dy).normalized;
                px[y * size + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255), (byte)((n.z * 0.5f + 0.5f) * 255), 255, 255);
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = "WaterNormals", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4,
            };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        /// <summary>
        /// Ruido de detalle cíclico: R = grano grueso, G = grano fino, B = manchas (espuma).
        /// </summary>
        public static Texture2D CreateDetailNoise(int size = 512, int seed = 11)
        {
            var r = PeriodicFbm(size, seed, 8, 5, 0.55f);
            var g = PeriodicFbm(size, seed + 1, 32, 4, 0.5f);
            var b = PeriodicFbm(size, seed + 2, 16, 3, 0.6f);
            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++)
                px[i] = new Color32(ToByte(r[i]), ToByte(g[i]), ToByte(b[i]), 255);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = "DetailNoise", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4,
            };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        private static byte ToByte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        /// <summary>FBM de ruido de valor sobre una rejilla que se repite (sin costuras). Resultado ~0..1.</summary>
        internal static float[] PeriodicFbm(int size, int seed, int baseCells, int octaves, float gain)
        {
            var result = new float[size * size];
            var rng = new System.Random(seed);
            float amp = 1f, norm = 0f;
            int cells = baseCells;
            for (int o = 0; o < octaves; o++)
            {
                var lattice = new float[cells * cells];
                for (int i = 0; i < lattice.Length; i++) lattice[i] = (float)rng.NextDouble();
                for (int y = 0; y < size; y++)
                {
                    float fy = y * cells / (float)size;
                    int y0 = (int)fy;
                    float ty = Smooth(fy - y0);
                    int y1 = (y0 + 1) % cells;
                    for (int x = 0; x < size; x++)
                    {
                        float fx = x * cells / (float)size;
                        int x0 = (int)fx;
                        float tx = Smooth(fx - x0);
                        int x1 = (x0 + 1) % cells;
                        float a = Mathf.Lerp(lattice[y0 * cells + x0], lattice[y0 * cells + x1], tx);
                        float c = Mathf.Lerp(lattice[y1 * cells + x0], lattice[y1 * cells + x1], tx);
                        result[y * size + x] += Mathf.Lerp(a, c, ty) * amp;
                    }
                }
                norm += amp;
                amp *= gain;
                cells = Mathf.Min(cells * 2, size);
            }
            for (int i = 0; i < result.Length; i++) result[i] /= norm;
            return result;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
