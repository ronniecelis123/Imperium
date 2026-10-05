using UnityEngine;

namespace GrandStrategy.Map.Rendering
{
    /// <summary>
    /// Texturas generadas por código para la calidad alta:
    /// <list type="bullet">
    /// <item>Atlas de follaje 2×2 con transparencia: racimo de hojas, rama de conífera, mata de hierba y hoja de palmera.
    /// Se aplica sobre "tarjetas" (quads) como hacen los juegos con los árboles (leaf cards).</item>
    /// <item>Detalle del suelo (canales = patrones en gris): R hierba, G roca, B arena, A tierra con piedras.</item>
    /// </list>
    /// </summary>
    public static class VegetationTextures
    {
        public static readonly Rect LeavesRect = new Rect(0f, 0f, 0.5f, 0.5f);
        public static readonly Rect NeedlesRect = new Rect(0.5f, 0f, 0.5f, 0.5f);
        public static readonly Rect GrassRect = new Rect(0f, 0.5f, 0.5f, 0.5f);
        public static readonly Rect FrondRect = new Rect(0.5f, 0.5f, 0.5f, 0.5f);

        private const int Tile = 512;

        // ================================================================== Atlas de follaje

        public static Texture2D CreateFoliageAtlas()
        {
            int size = Tile * 2;
            var px = new Color[size * size];
            var rng = new System.Random(42);
            DrawLeaves(px, size, 0, 0, rng);
            DrawNeedles(px, size, Tile, 0, rng);
            DrawGrass(px, size, 0, Tile, rng);
            DrawFrond(px, size, Tile, Tile, rng);
            // Los píxeles transparentes toman un verde medio: evita bordes oscuros al filtrar/mipmaps.
            for (int i = 0; i < px.Length; i++)
                if (px[i].a <= 0f) px[i] = new Color(0.2f, 0.32f, 0.14f, 0f);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            {
                name = "FoliageAtlas", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4,
            };
            tex.SetPixels(px);
            tex.Apply(true, true);
            return tex;
        }

        private static void Blend(Color[] px, int size, int x, int y, Color c, float coverage)
        {
            if (x < 0 || y < 0 || x >= size || y >= size || coverage <= 0f) return;
            int i = y * size + x;
            var dst = px[i];
            float a = Mathf.Clamp01(coverage) * c.a;
            px[i] = new Color(Mathf.Lerp(dst.r, c.r, a), Mathf.Lerp(dst.g, c.g, a), Mathf.Lerp(dst.b, c.b, a), Mathf.Max(dst.a, a));
        }

        /// <summary>Elipse rotada con borde suave y nervio central más oscuro (una hoja).</summary>
        private static void Leaf(Color[] px, int size, Vector2 c, float len, float width, float angle, Color col, int ox, int oy)
        {
            float ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);
            int r = Mathf.CeilToInt(len) + 2;
            for (int y = (int)c.y - r; y <= (int)c.y + r; y++)
            for (int x = (int)c.x - r; x <= (int)c.x + r; x++)
            {
                if (x < 0 || y < 0 || x >= Tile || y >= Tile) continue;
                float dx = x - c.x, dy = y - c.y;
                float u = (dx * ca + dy * sa) / len, v = (-dx * sa + dy * ca) / width;
                // Forma de hoja: más ancha cerca de la base, puntiaguda al final.
                float shape = u * u + v * v * (1f + 0.6f * u);
                if (shape > 1.05f) continue;
                float edge = Mathf.Clamp01((1.05f - shape) * 8f);
                float shade = 0.85f + 0.25f * (1f - Mathf.Abs(v)) - 0.12f * u;
                var leafCol = col * shade;
                if (Mathf.Abs(v) < 0.08f && u > -0.9f) leafCol *= 0.75f; // nervio
                leafCol.a = 1f;
                Blend(px, size, ox + x, oy + y, leafCol, edge);
            }
        }

        private static void Line(Color[] px, int size, Vector2 a, Vector2 b, float w0, float w1, Color col, int ox, int oy)
        {
            int minX = (int)Mathf.Min(a.x, b.x) - (int)Mathf.Max(w0, w1) - 2, maxX = (int)Mathf.Max(a.x, b.x) + (int)Mathf.Max(w0, w1) + 2;
            int minY = (int)Mathf.Min(a.y, b.y) - (int)Mathf.Max(w0, w1) - 2, maxY = (int)Mathf.Max(a.y, b.y) + (int)Mathf.Max(w0, w1) + 2;
            var ab = b - a;
            float len2 = Mathf.Max(ab.sqrMagnitude, 1e-4f);
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                if (x < 0 || y < 0 || x >= Tile || y >= Tile) continue;
                var p = new Vector2(x, y);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                float d = Vector2.Distance(p, a + ab * t);
                float w = Mathf.Lerp(w0, w1, t);
                float cover = Mathf.Clamp01(w - d + 0.5f);
                var c = col; c.a = 1f;
                Blend(px, size, ox + x, oy + y, c, cover);
            }
        }

        private static void DrawLeaves(Color[] px, int size, int ox, int oy, System.Random rng)
        {
            var center = new Vector2(Tile * 0.5f, Tile * 0.52f);
            var bark = new Color(0.3f, 0.22f, 0.14f);
            // Ramitas desde abajo hacia el interior del racimo.
            for (int i = 0; i < 6; i++)
            {
                var end = center + Random2(rng) * Tile * 0.3f;
                Line(px, size, new Vector2(Tile * 0.5f, Tile * 0.04f), end, 5f, 1.5f, bark, ox, oy);
            }
            for (int i = 0; i < 150; i++)
            {
                var dir = Random2(rng);
                float rr = Mathf.Sqrt((float)rng.NextDouble()) * Tile * 0.4f;
                var p = center + dir * rr;
                float g = (float)rng.NextDouble();
                var col = Color.Lerp(new Color(0.16f, 0.3f, 0.1f), new Color(0.42f, 0.56f, 0.2f), g * 0.8f + (p.y / Tile) * 0.3f);
                float angle = Mathf.Atan2(dir.y, dir.x) + ((float)rng.NextDouble() - 0.5f) * 1.6f;
                Leaf(px, size, p, Tile * (0.05f + (float)rng.NextDouble() * 0.025f), Tile * (0.022f + (float)rng.NextDouble() * 0.01f), angle, col, ox, oy);
            }
        }

        private static void DrawNeedles(Color[] px, int size, int ox, int oy, System.Random rng)
        {
            var stemA = new Vector2(Tile * 0.03f, Tile * 0.5f);
            var stemB = new Vector2(Tile * 0.97f, Tile * 0.52f);
            Line(px, size, stemA, stemB, 4f, 1.2f, new Color(0.28f, 0.2f, 0.13f), ox, oy);
            // Ramillas secundarias
            for (int b = 0; b < 7; b++)
            {
                float t = 0.12f + b * 0.11f;
                var basePt = Vector2.Lerp(stemA, stemB, t);
                float side = b % 2 == 0 ? 1f : -1f;
                float len = Tile * 0.28f * (1f - t * 0.7f);
                var tip = basePt + new Vector2(len * 0.8f, side * len * 0.55f);
                Line(px, size, basePt, tip, 2.2f, 0.8f, new Color(0.28f, 0.2f, 0.13f), ox, oy);
                for (float s = 0.1f; s < 1f; s += 0.06f)
                {
                    var q = Vector2.Lerp(basePt, tip, s);
                    for (int k = -1; k <= 1; k += 2)
                    {
                        float nl = Tile * 0.05f * (1f - s * 0.6f);
                        var dir = new Vector2(0.6f, k * 0.8f).normalized;
                        var col = Color.Lerp(new Color(0.07f, 0.17f, 0.1f), new Color(0.2f, 0.36f, 0.2f), (float)rng.NextDouble());
                        Line(px, size, q, q + dir * nl, 1.4f, 0.5f, col, ox, oy);
                    }
                }
            }
            for (float t = 0.02f; t < 1f; t += 0.012f)
            {
                var q = Vector2.Lerp(stemA, stemB, t);
                float nl = Tile * (0.11f - 0.07f * t);
                for (int k = -1; k <= 1; k += 2)
                {
                    float ang = k * (0.7f + (float)rng.NextDouble() * 0.4f);
                    var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    var col = Color.Lerp(new Color(0.06f, 0.16f, 0.09f), new Color(0.22f, 0.38f, 0.22f), (float)rng.NextDouble());
                    Line(px, size, q, q + dir * nl, 1.6f, 0.5f, col, ox, oy);
                }
            }
        }

        private static void DrawGrass(Color[] px, int size, int ox, int oy, System.Random rng)
        {
            for (int i = 0; i < 46; i++)
            {
                var basePt = new Vector2(Tile * (0.25f + (float)rng.NextDouble() * 0.5f), Tile * 0.02f);
                float h = Tile * (0.45f + (float)rng.NextDouble() * 0.5f);
                float lean = ((float)rng.NextDouble() - 0.5f) * Tile * 0.5f;
                var col = Color.Lerp(new Color(0.24f, 0.36f, 0.12f), new Color(0.6f, 0.62f, 0.28f), (float)rng.NextDouble());
                // Brizna curva: tres tramos.
                var p0 = basePt;
                var p1 = basePt + new Vector2(lean * 0.2f, h * 0.45f);
                var p2 = basePt + new Vector2(lean * 0.6f, h * 0.8f);
                var p3 = basePt + new Vector2(lean, h);
                Line(px, size, p0, p1, 5f, 4f, col * 0.85f, ox, oy);
                Line(px, size, p1, p2, 4f, 2.5f, col, ox, oy);
                Line(px, size, p2, p3, 2.5f, 0.6f, col * 1.12f, ox, oy);
            }
            for (int i = 0; i < 7; i++)
            {
                var p = new Vector2(Tile * (0.3f + (float)rng.NextDouble() * 0.4f), Tile * (0.5f + (float)rng.NextDouble() * 0.4f));
                var flower = rng.NextDouble() < 0.5 ? new Color(0.95f, 0.9f, 0.6f) : new Color(0.85f, 0.35f, 0.3f);
                Leaf(px, size, p, 9f, 9f, 0f, flower, ox, oy);
            }
        }

        private static void DrawFrond(Color[] px, int size, int ox, int oy, System.Random rng)
        {
            Vector2 Rib(float t) => new Vector2(Tile * (0.04f + 0.92f * t), Tile * (0.62f - 0.3f * t * t));
            for (int i = 0; i < 20; i++)
                Line(px, size, Rib(i / 20f), Rib((i + 1) / 20f), 5f - i * 0.2f, 4.8f - i * 0.2f, new Color(0.35f, 0.4f, 0.16f), ox, oy);
            for (float t = 0.05f; t < 0.97f; t += 0.03f)
            {
                var p = Rib(t);
                float len = Tile * 0.22f * (1f - Mathf.Abs(t - 0.4f));
                for (int k = -1; k <= 1; k += 2)
                {
                    var dir = new Vector2(0.55f, k * 0.83f - 0.25f).normalized;
                    var col = Color.Lerp(new Color(0.12f, 0.3f, 0.1f), new Color(0.32f, 0.5f, 0.16f), (float)rng.NextDouble());
                    Line(px, size, p, p + dir * len, 4f, 0.8f, col, ox, oy);
                }
            }
        }

        private static Vector2 Random2(System.Random rng)
        {
            float a = (float)(rng.NextDouble() * Mathf.PI * 2);
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        // ================================================================== Detalle del suelo

        public static Texture2D CreateGroundDetail(int size = 512)
        {
            var coarse = ProceduralTextures.PeriodicFbm(size, 101, 16, 5, 0.55f);
            var fine = ProceduralTextures.PeriodicFbm(size, 202, 64, 3, 0.5f);
            var rock = ProceduralTextures.PeriodicFbm(size, 303, 12, 5, 0.55f);
            var rng = new System.Random(5);
            var pebbles = new float[size * size];
            for (int i = 0; i < 900; i++)
            {
                int cx = rng.Next(size), cy = rng.Next(size), r = 1 + rng.Next(4);
                float v = 0.4f + (float)rng.NextDouble() * 0.6f;
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx * dx + dy * dy > r * r) continue;
                    int x = (cx + dx + size) % size, y = (cy + dy + size) % size;
                    pebbles[y * size + x] = Mathf.Max(pebbles[y * size + x], v * (1f - (dx * dx + dy * dy) / (float)(r * r + 1)));
                }
            }

            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                // Hierba: grano fino con manchas (matas).
                float grass = 0.5f + (fine[i] - 0.5f) * 1.4f + (coarse[i] - 0.5f) * 0.6f;
                // Roca: grietas (ruido "ridged") sobre una base irregular.
                float ridge = 1f - Mathf.Abs(rock[i] * 2f - 1f);
                float rockV = 0.35f + coarse[i] * 0.4f - Mathf.Pow(ridge, 6f) * 0.5f + (fine[i] - 0.5f) * 0.3f;
                // Arena: ondulaciones cíclicas (frecuencia entera) deformadas por ruido.
                float ripple = 0.5f + 0.35f * Mathf.Sin(2f * Mathf.PI * (x * 18f / size + coarse[i] * 1.6f)) + (fine[i] - 0.5f) * 0.2f;
                // Tierra: grumos y piedras.
                float soil = 0.4f + (coarse[i] - 0.5f) * 0.6f + (fine[i] - 0.5f) * 0.4f + pebbles[i] * 0.5f;
                px[i] = new Color32(B(grass), B(rockV), B(ripple), B(soil));
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = "GroundDetail", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8,
            };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        private static byte B(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
    }
}
