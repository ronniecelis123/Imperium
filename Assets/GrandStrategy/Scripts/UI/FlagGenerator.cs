using System.Collections.Generic;
using System.IO;
using GrandStrategy.Map;
using UnityEngine;

namespace GrandStrategy.UI
{
    /// <summary>Banderas procedurales (tricolores, cruces nórdicas, cantones...) a partir de la semilla del país.</summary>
    public static class FlagGenerator
    {
        private const int Width = 60;
        private const int Height = 40;

        private static readonly Color32[] Palette =
        {
            new Color32(240, 240, 235, 255), new Color32(190, 30, 40, 255), new Color32(25, 60, 140, 255),
            new Color32(245, 200, 40, 255), new Color32(25, 25, 25, 255), new Color32(20, 110, 60, 255),
            new Color32(110, 30, 60, 255), new Color32(90, 160, 210, 255),
        };

        private static readonly Dictionary<int, Texture2D> Cache = new Dictionary<int, Texture2D>();

        /// <summary>Carpeta con banderas reales (&lt;TAG&gt;.png). Si falta una, se genera una procedural.</summary>
        public static string FlagFolder;

        public static Texture2D Get(Country country)
        {
            if (country == null) return null;
            if (Cache.TryGetValue(country.Id, out var cached) && cached != null) return cached;

            var file = TryLoadFlagFile(country);
            if (file != null)
            {
                Cache[country.Id] = file;
                return file;
            }

            var rng = new System.Random(country.FlagSeed);
            Color32 main = country.Color;
            Color32 a = Palette[rng.Next(Palette.Length)];
            Color32 b = Palette[rng.Next(Palette.Length)];
            if (Same(a, main)) a = Palette[(System.Array.IndexOf(Palette, a) + 1) % Palette.Length];
            if (Same(b, a)) b = Palette[(System.Array.IndexOf(Palette, b) + 3) % Palette.Length];

            var pixels = new Color32[Width * Height];
            int pattern = rng.Next(6);
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                float u = x / (float)Width, v = 1f - y / (float)Height;
                Color32 c;
                switch (pattern)
                {
                    case 0: c = v < 1f / 3 ? main : v < 2f / 3 ? a : b; break;                 // tricolor horizontal
                    case 1: c = u < 1f / 3 ? main : u < 2f / 3 ? a : b; break;                 // tricolor vertical
                    case 2: c = v < 0.5f ? main : a; break;                                     // bicolor
                    case 3:                                                                      // cruz nórdica
                        c = (Mathf.Abs(u - 0.36f) < 0.07f || Mathf.Abs(v - 0.5f) < 0.1f) ? a : main;
                        if (Mathf.Abs(u - 0.36f) < 0.035f || Mathf.Abs(v - 0.5f) < 0.05f) c = b;
                        break;
                    case 4: c = (u < 0.42f && v < 0.5f) ? a : (v < 0.5f || ((int)(v * 6) % 2 == 0) ? main : b); break; // cantón
                    default:                                                                     // triángulo
                        c = u < (0.5f - Mathf.Abs(v - 0.5f)) * 0.9f ? a : v < 0.5f ? main : b;
                        break;
                }
                pixels[y * Width + x] = c;
            }

            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = "Flag_" + country.Tag, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            Cache[country.Id] = tex;
            return tex;
        }

        private static Texture2D TryLoadFlagFile(Country country)
        {
            if (string.IsNullOrEmpty(FlagFolder) || string.IsNullOrEmpty(country.Tag)) return null;
            string path = Path.Combine(FlagFolder, country.Tag + ".png");
            if (!File.Exists(path)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "Flag_" + country.Tag, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
            };
            return tex.LoadImage(File.ReadAllBytes(path)) ? tex : null;
        }

        private static bool Same(Color32 x, Color32 y) =>
            Mathf.Abs(x.r - y.r) + Mathf.Abs(x.g - y.g) + Mathf.Abs(x.b - y.b) < 60;
    }
}
