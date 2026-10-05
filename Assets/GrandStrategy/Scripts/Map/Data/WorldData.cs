using System.Collections.Generic;
using UnityEngine;

namespace GrandStrategy.Map
{
    /// <summary>
    /// Estado completo del mundo: mapa de píxeles, provincias y países.
    /// Es la fuente de verdad que leen el render, la UI y (en el futuro) la simulación.
    /// </summary>
    public sealed class WorldData
    {
        public string Name = "Mundo";
        /// <summary>Carpeta de la que se cargó el mapa (null si es procedural). Contiene p. ej. flags/.</summary>
        public string SourceFolder;
        /// <summary>Fecha de inicio de la partida.</summary>
        public System.DateTime StartDate = new System.DateTime(1936, 1, 1, 12, 0, 0);
        public MapData Map;
        public readonly List<Province> Provinces = new List<Province>();
        public readonly List<Country> Countries = new List<Country>();
        /// <summary>Ciudades principales (decoración: edificios).</summary>
        public readonly List<CityInfo> Cities = new List<CityInfo>();
        /// <summary>Ríos principales (decoración).</summary>
        public readonly List<RiverPath> Rivers = new List<RiverPath>();

        public WorldData(MapData map)
        {
            Map = map;
        }

        public Province GetProvince(int id) => id >= 0 && id < Provinces.Count ? Provinces[id] : null;

        public Country GetCountry(int id) => id >= 0 && id < Countries.Count ? Countries[id] : null;

        public Country GetOwner(Province province) => province == null ? null : GetCountry(province.OwnerId);

        public Province GetProvinceAtPixel(int x, int y) => GetProvince(Map.GetProvinceIdAt(x, y));

        /// <summary>Asigna (o cambia) el propietario de una provincia manteniendo las listas coherentes.</summary>
        public void SetOwner(Province province, Country country)
        {
            var old = GetOwner(province);
            old?.Provinces.Remove(province.Id);
            province.OwnerId = country != null ? country.Id : -1;
            country?.Provinces.Add(province.Id);
            OwnerChanged?.Invoke(province);
        }

        /// <summary>Una provincia ha cambiado de propietario (p. ej. los nombres del mapa deben recolocarse).</summary>
        public event System.Action<Province> OwnerChanged;

        /// <summary>
        /// Calcula a partir del mapa de píxeles: tamaño, centro, límites, vecinos y si la provincia es costera.
        /// Lo usan tanto el generador procedural como el cargador de archivos.
        /// </summary>
        public void BuildDerivedData()
        {
            int w = Map.Width, h = Map.Height;
            int n = Provinces.Count;
            var ids = Map.ProvinceIds;

            var sumX = new double[n];
            var sumY = new double[n];
            var count = new int[n];
            var minX = new int[n];
            var minY = new int[n];
            var maxX = new int[n];
            var maxY = new int[n];
            for (int i = 0; i < n; i++)
            {
                minX[i] = minY[i] = int.MaxValue;
                maxX[i] = maxY[i] = int.MinValue;
            }

            var adjacency = new HashSet<long>();
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int id = ids[row + x];
                    if (id < 0) continue;
                    sumX[id] += x;
                    sumY[id] += y;
                    count[id]++;
                    if (x < minX[id]) minX[id] = x;
                    if (x > maxX[id]) maxX[id] = x;
                    if (y < minY[id]) minY[id] = y;
                    if (y > maxY[id]) maxY[id] = y;

                    if (x + 1 < w)
                    {
                        int r = ids[row + x + 1];
                        if (r != id && r >= 0) adjacency.Add(PairKey(id, r));
                    }
                    if (y + 1 < h)
                    {
                        int u = ids[row + w + x];
                        if (u != id && u >= 0) adjacency.Add(PairKey(id, u));
                    }
                }
            }

            for (int i = 0; i < n; i++)
            {
                var p = Provinces[i];
                p.Neighbors.Clear();
                p.Coastal = false;
                p.PixelCount = count[i];
                if (count[i] > 0)
                {
                    p.Center = new Vector2((float)(sumX[i] / count[i]), (float)(sumY[i] / count[i]));
                    p.Bounds = new RectInt(minX[i], minY[i], maxX[i] - minX[i] + 1, maxY[i] - minY[i] + 1);
                }
            }

            foreach (long key in adjacency)
            {
                int a = (int)(key >> 32);
                int b = (int)(key & 0xFFFFFFFF);
                var pa = Provinces[a];
                var pb = Provinces[b];
                pa.Neighbors.Add(b);
                pb.Neighbors.Add(a);
                if (pa.IsWater != pb.IsWater)
                {
                    if (!pa.IsWater && pb.Type == ProvinceType.Sea) pa.Coastal = true;
                    if (!pb.IsWater && pa.Type == ProvinceType.Sea) pb.Coastal = true;
                }
            }
        }

        /// <summary>Recalcula población, PIB, tropas y vecinos de cada país.</summary>
        public void RecalculateCountryStats()
        {
            foreach (var c in Countries)
            {
                c.Population = 0;
                c.Gdp = 0;
                c.Neighbors.Clear();
                float devSum = 0;
                foreach (int pid in c.Provinces)
                {
                    var p = Provinces[pid];
                    c.Population += p.Population;
                    devSum += p.Development;
                    // PIB: población * productividad según desarrollo (en millones).
                    c.Gdp += p.Gdp > 0 ? p.Gdp : p.Population * (0.6f + p.Development * 0.35f) / 1000f;
                    foreach (int nid in p.Neighbors)
                    {
                        int owner = Provinces[nid].OwnerId;
                        if (owner >= 0 && owner != c.Id) c.Neighbors.Add(owner);
                    }
                }
                c.AverageDevelopment = c.Provinces.Count > 0 ? devSum / c.Provinces.Count : 0;
                c.Manpower = (int)(c.Population * 0.012f);
            }
        }

        private static long PairKey(int a, int b)
        {
            if (a > b) { int t = a; a = b; b = t; }
            return ((long)a << 32) | (uint)b;
        }
    }
}
