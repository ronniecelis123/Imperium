using System.Collections.Generic;
using System.IO;
using GrandStrategy.Map;
using UnityEngine;

namespace GrandStrategy.UI
{
    /// <summary>
    /// Retratos de los dirigentes (leaders/&lt;TAG&gt;.png del mapa). Si un país no tiene retrato se usa
    /// una silueta genérica, como hace HOI4 con los líderes sin imagen.
    /// </summary>
    public static class PortraitLoader
    {
        private static readonly Dictionary<int, Texture2D> Cache = new Dictionary<int, Texture2D>();
        private static Texture2D _silhouette;

        /// <summary>Carpeta con los retratos; null = solo siluetas.</summary>
        public static string LeaderFolder;

        public static Texture2D Get(Country country)
        {
            if (country == null) return Silhouette;
            if (Cache.TryGetValue(country.Id, out var cached) && cached != null) return cached;

            Texture2D tex = null;
            if (!string.IsNullOrEmpty(LeaderFolder) && !string.IsNullOrEmpty(country.Tag))
            {
                string path = Path.Combine(LeaderFolder, country.Tag + ".png");
                if (File.Exists(path))
                {
                    tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                    {
                        name = "Leader_" + country.Tag, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                    };
                    if (!tex.LoadImage(File.ReadAllBytes(path))) tex = null;
                }
            }
            tex ??= Silhouette;
            Cache[country.Id] = tex;
            return tex;
        }

        /// <summary>Silueta de busto sobre fondo sepia.</summary>
        public static Texture2D Silhouette
        {
            get
            {
                if (_silhouette != null) return _silhouette;
                const int w = 84, h = 112;
                var px = new Color32[w * h];
                var top = new Color(0.36f, 0.31f, 0.25f);
                var bottom = new Color(0.16f, 0.13f, 0.11f);
                var figure = new Color(0.09f, 0.08f, 0.07f);
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h; // v = 0 abajo
                    var c = Color.Lerp(bottom, top, v);
                    float head = Mathf.Pow((u - 0.5f) / 0.17f, 2) + Mathf.Pow((v - 0.62f) / 0.15f, 2);
                    float shoulders = Mathf.Pow((u - 0.5f) / 0.42f, 2) + Mathf.Pow((v - 0.08f) / 0.36f, 2);
                    float neck = Mathf.Abs(u - 0.5f) < 0.07f && v > 0.38f && v < 0.5f ? 0f : 2f;
                    if (head < 1f || (shoulders < 1f && v < 0.42f) || neck < 1f) c = figure;
                    px[y * w + x] = c;
                }
                _silhouette = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "LeaderSilhouette", filterMode = FilterMode.Bilinear };
                _silhouette.SetPixels32(px);
                _silhouette.Apply(false, true);
                return _silhouette;
            }
        }
    }
}
