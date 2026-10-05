using UnityEngine;

namespace GrandStrategy.Map.Rendering
{
    /// <summary>
    /// Un "map mode" (como en HOI4) decide qué color recibe cada provincia en la capa superpuesta.
    /// Añadir uno nuevo (recursos, ideología, diplomacia...) es solo implementar esta interfaz.
    /// </summary>
    public interface IMapMode
    {
        string Name { get; }
        /// <summary>0 = solo terreno, 1 = color del modo totalmente visible (según zoom).</summary>
        float OverlayStrength { get; }
        Color32 GetProvinceColor(Province province, WorldData world);
    }

    public sealed class PoliticalMapMode : IMapMode
    {
        private static readonly Color32 Unowned = new Color32(130, 126, 118, 255);

        public string Name => "Político";
        public float OverlayStrength => 1f;

        public Color32 GetProvinceColor(Province province, WorldData world)
        {
            var owner = world.GetOwner(province);
            return owner != null ? owner.Color : Unowned;
        }
    }

    public sealed class TerrainMapMode : IMapMode
    {
        public string Name => "Terreno";
        public float OverlayStrength => 0f;
        public Color32 GetProvinceColor(Province province, WorldData world) => new Color32(0, 0, 0, 255);
    }

    /// <summary>
    /// Vista de edificios: colores políticos apagados para que destaquen los símbolos de los edificios
    /// (los dibuja UI.BuildingMarkers mientras este modo está activo).
    /// </summary>
    public sealed class BuildingsMapMode : IMapMode
    {
        private static readonly Color Unowned = new Color(0.51f, 0.49f, 0.46f);

        public string Name => "Edificios";
        public float OverlayStrength => 1f;

        public Color32 GetProvinceColor(Province province, WorldData world)
        {
            if (province.IsWater) return new Color32(0, 0, 0, 255);
            var owner = world.GetOwner(province);
            Color c = owner != null ? (Color)owner.Color : Unowned;
            float grey = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            c = Color.Lerp(c, new Color(grey, grey, grey), 0.55f) * 0.78f;
            c.a = 1f;
            return c;
        }
    }

    public sealed class PopulationMapMode : IMapMode
    {
        private static readonly Color Low = new Color(0.95f, 0.92f, 0.7f);
        private static readonly Color Mid = new Color(0.93f, 0.6f, 0.25f);
        private static readonly Color High = new Color(0.6f, 0.08f, 0.08f);

        public string Name => "Población";
        public float OverlayStrength => 1f;

        public Color32 GetProvinceColor(Province province, WorldData world)
        {
            if (province.IsWater || province.PixelCount == 0) return new Color32(0, 0, 0, 255);
            // Densidad en escala logarítmica.
            float density = province.Population / (float)province.PixelCount;
            float t = Mathf.InverseLerp(0f, 3.6f, Mathf.Log10(1f + density));
            return t < 0.5f ? Color.Lerp(Low, Mid, t * 2f) : Color.Lerp(Mid, High, (t - 0.5f) * 2f);
        }
    }
}
