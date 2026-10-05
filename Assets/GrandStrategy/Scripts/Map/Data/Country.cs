using System.Collections.Generic;
using UnityEngine;

namespace GrandStrategy.Map
{
    /// <summary>
    /// Nación jugable. Equivale a un "TAG" de HOI4 o una civilización de AoH2.
    /// </summary>
    public sealed class Country
    {
        public int Id;
        /// <summary>Etiqueta de 3 letras, estilo HOI4 (GER, ENG...).</summary>
        public string Tag;
        /// <summary>Nombre en español (fichas, ventanas y, por defecto, el mapa).</summary>
        public string Name;
        /// <summary>Nombre en inglés (para otros idiomas; ver <see cref="CountryNames"/>).</summary>
        public string NameEn;
        /// <summary>Nombre que se escribe sobre el mapa en el idioma actual.</summary>
        public string MapName => CountryNames.MapLabel(this);
        public Color32 Color;
        public GovernmentType Government;
        public int CapitalProvinceId = -1;
        /// <summary>Dirigente del país (el retrato se carga de leaders/&lt;TAG&gt;.png).</summary>
        public string Leader;
        /// <summary>País soberano si es un dominio, títere o protectorado; -1 si es independiente.</summary>
        public int OverlordId = -1;
        /// <summary>Semilla para generar la bandera procedural.</summary>
        public int FlagSeed;

        public readonly List<int> Provinces = new List<int>();
        public readonly HashSet<int> Neighbors = new HashSet<int>();

        // --- Estadísticas calculadas (WorldData.RecalculateCountryStats) ---
        public long Population;
        /// <summary>PIB estimado en millones.</summary>
        public float Gdp;
        /// <summary>Tropas disponibles (placeholder hasta implementar el sistema militar).</summary>
        public int Manpower;
        public float AverageDevelopment;

        /// <summary>Edificios, comercio, capital privado y tesoro (null hasta que lo rellena la carga o EconomySystem).</summary>
        public Economy.CountryEconomy Economy;

        public bool IsAlive => Provinces.Count > 0;

        public override string ToString() => $"{Name} [{Tag}]";
    }
}
