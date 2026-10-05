using System.Collections.Generic;
using UnityEngine;

namespace GrandStrategy.Map
{
    /// <summary>
    /// Unidad básica del mapa. El Id es el índice interno (0..N-1) que se usa en las texturas de la GPU;
    /// en los archivos se escribe Id + 1 (el 0 queda reservado, igual que en HOI4).
    /// </summary>
    public sealed class Province
    {
        public int Id;
        public string Name;
        /// <summary>Color único de la provincia en provinces.png.</summary>
        public Color32 MapColor;
        public ProvinceType Type;
        public TerrainType Terrain;
        public int Continent;

        /// <summary>Id del país propietario, -1 si no tiene.</summary>
        public int OwnerId = -1;

        // --- Economía / población (se ampliará en fases futuras) ---
        public int Population;
        /// <summary>Nivel de desarrollo 1..10 (infraestructura, industria...).</summary>
        public int Development = 1;
        /// <summary>PIB de la provincia en millones (0 = se estima a partir de población y desarrollo).</summary>
        public float Gdp;
        /// <summary>Edificios de la provincia: los de 1936 (repartidos al empezar, no ocupan huecos) y los construidos en la partida.</summary>
        public readonly List<Economy.BuildingInstance> Buildings = new List<Economy.BuildingInstance>();

        // --- Datos derivados del mapa de píxeles (los calcula WorldData) ---
        public int PixelCount;
        /// <summary>Centro aproximado en coordenadas de píxel.</summary>
        public Vector2 Center;
        public RectInt Bounds;
        public bool Coastal;
        public readonly List<int> Neighbors = new List<int>();

        public bool IsWater => Type != ProvinceType.Land;
        public bool HasOwner => OwnerId >= 0;

        public override string ToString() => $"{Name} (#{Id})";
    }
}
