namespace GrandStrategy.Map.Generation
{
    /// <summary>
    /// Ruido Perlin 2D con semilla. Implementación propia en C# puro (sin APIs de Unity) para poder
    /// usarla desde hilos de trabajo mientras se genera el mundo.
    /// </summary>
    public sealed class Noise
    {
        private readonly int[] _perm = new int[512];

        public Noise(int seed)
        {
            var rng = new System.Random(seed);
            var p = new int[256];
            for (int i = 0; i < 256; i++) p[i] = i;
            for (int i = 255; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = p[i]; p[i] = p[j]; p[j] = t;
            }
            for (int i = 0; i < 512; i++) _perm[i] = p[i & 255];
        }

        /// <summary>Perlin clásico, rango aproximado [-1, 1].</summary>
        public float Perlin(float x, float y)
        {
            int xi = FastFloor(x), yi = FastFloor(y);
            float xf = x - xi, yf = y - yi;
            int X = xi & 255, Y = yi & 255;
            float u = Fade(xf), v = Fade(yf);

            int aa = _perm[_perm[X] + Y];
            int ab = _perm[_perm[X] + Y + 1];
            int ba = _perm[_perm[X + 1] + Y];
            int bb = _perm[_perm[X + 1] + Y + 1];

            float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
            float x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);
            return Lerp(x1, x2, v) * 1.4f;
        }

        /// <summary>Fractal Brownian Motion: suma de octavas. Rango aproximado [-1, 1].</summary>
        public float Fbm(float x, float y, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0, amp = 1, norm = 0;
            for (int i = 0; i < octaves; i++)
            {
                sum += Perlin(x, y) * amp;
                norm += amp;
                amp *= gain;
                // Desplazamiento por octava para evitar artefactos alineados en el origen.
                x = x * lacunarity + 17.13f;
                y = y * lacunarity + 31.71f;
            }
            return sum / norm;
        }

        /// <summary>Ruido "ridged" (crestas), rango [0, 1]. Ideal para cordilleras.</summary>
        public float Ridged(float x, float y, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0, amp = 1, norm = 0;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - System.Math.Abs(Perlin(x, y));
                sum += n * n * amp;
                norm += amp;
                amp *= gain;
                x = x * lacunarity + 11.3f;
                y = y * lacunarity + 7.9f;
            }
            return sum / norm;
        }

        private static int FastFloor(float x) => x >= 0 ? (int)x : (int)x - 1;
        private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static float Grad(int hash, float x, float y)
        {
            switch (hash & 7)
            {
                case 0: return x + y;
                case 1: return -x + y;
                case 2: return x - y;
                case 3: return -x - y;
                case 4: return x;
                case 5: return -x;
                case 6: return y;
                default: return -y;
            }
        }
    }
}
