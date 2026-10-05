using UnityEngine;

namespace GrandStrategy.Map
{
    /// <summary>
    /// Datos por píxel del mapa. Todos los arrays van fila a fila empezando por ABAJO (convención de
    /// Texture2D de Unity): índice = y * Width + x, con y = 0 en el borde sur.
    /// </summary>
    public sealed class MapData
    {
        public int Width;
        public int Height;
        /// <summary>Altura normalizada 0..1 en la que empieza la tierra.</summary>
        public float SeaLevel = 0.4f;

        /// <summary>Relieve 0..1.</summary>
        public float[] Heights;
        /// <summary>Índice de provincia de cada píxel.</summary>
        public int[] ProvinceIds;
        /// <summary>Color pintado del terreno (equivalente al colormap de HOI4).</summary>
        public Color32[] ColorMap;
        /// <summary>Intensidad de las luces de ciudad (0..255) para la noche; null si no hay.</summary>
        public byte[] CityLights;

        // --- Proyección (para calcular latitud/longitud: día y noche) ---
        /// <summary>true = Miller (mapa mundial), false = latitud lineal.</summary>
        public bool MillerProjection;
        public float LatTop = 90f;
        public float LatBottom = -90f;
        /// <summary>Intensidad del sombreado del relieve (los mapas reales necesitan menos que el procedural).</summary>
        public float NormalStrength = 2.5f;

        public MapData(int width, int height)
        {
            Width = width;
            Height = height;
            Heights = new float[width * height];
            ProvinceIds = new int[width * height];
            ColorMap = new Color32[width * height];
        }

        public int PixelCount => Width * Height;

        /// <summary>Latitud en grados para una coordenada v (0 = borde sur) según la proyección.</summary>
        public float LatitudeAt(float v)
        {
            if (!MillerProjection) return Mathf.Lerp(LatBottom, LatTop, v);
            float Miller(float lat) => 1.25f * Mathf.Log(Mathf.Tan(Mathf.PI / 4f + 0.4f * lat * Mathf.Deg2Rad));
            float y = Mathf.Lerp(Miller(LatBottom), Miller(LatTop), v);
            return (2.5f * Mathf.Atan(Mathf.Exp(0.8f * y)) - 0.625f * Mathf.PI) * Mathf.Rad2Deg;
        }

        public int GetProvinceIdAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return -1;
            return ProvinceIds[y * Width + x];
        }

        /// <summary>Altura con interpolación bilineal; u, v en 0..1.</summary>
        public float SampleHeight(float u, float v)
        {
            float fx = Mathf.Clamp(u * Width - 0.5f, 0f, Width - 1.001f);
            float fy = Mathf.Clamp(v * Height - 0.5f, 0f, Height - 1.001f);
            int x0 = (int)fx, y0 = (int)fy;
            int x1 = Mathf.Min(x0 + 1, Width - 1), y1 = Mathf.Min(y0 + 1, Height - 1);
            float tx = fx - x0, ty = fy - y0;
            float a = Mathf.Lerp(Heights[y0 * Width + x0], Heights[y0 * Width + x1], tx);
            float b = Mathf.Lerp(Heights[y1 * Width + x0], Heights[y1 * Width + x1], tx);
            return Mathf.Lerp(a, b, ty);
        }
    }
}
