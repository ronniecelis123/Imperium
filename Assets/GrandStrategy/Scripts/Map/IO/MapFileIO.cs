#pragma warning disable CS0649 // los campos de las clases *Json los rellena JsonUtility
using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using GrandStrategy.Map.Generation;
using UnityEngine;

namespace GrandStrategy.Map.IO
{
    /// <summary>
    /// Lee y escribe mapas en un formato inspirado en HOI4, editable con cualquier editor de imágenes:
    /// <list type="bullet">
    /// <item><c>map.json</c> – nombre, tamaño y nivel del mar.</item>
    /// <item><c>provinces.png</c> – un color RGB único por provincia.</item>
    /// <item><c>definition.csv</c> – <c>id;r;g;b;type;coastal;terrain;continent</c> (igual que HOI4).</item>
    /// <item><c>heightmap.png</c> – relieve en escala de grises.</item>
    /// <item><c>colormap.png</c> – (opcional) color del terreno.</item>
    /// <item><c>provinces.json</c> – (opcional) nombre, población y desarrollo.</item>
    /// <item><c>countries.json</c> – países y sus provincias.</item>
    /// </list>
    /// Los ids en archivo empiezan en 1 (0 reservado); internamente se usa id - 1.
    /// </summary>
    public static class MapFileIO
    {
        public const string MapsFolderName = "Maps";

        [Serializable] private class MapInfoJson { public string name; public int width; public int height; public float seaLevel; public string projection; public float latTop = 90f; public float latBottom = -90f; public string startDate; public float normalStrength = 2.5f; }
        [Serializable] private class ProvinceJson { public int id; public string name; public int population; public int development; public float gdp; }
        [Serializable] private class ProvinceListJson { public List<ProvinceJson> provinces = new List<ProvinceJson>(); }
        [Serializable]
        private class CountryJson
        {
            public string tag; public string name; public string nameEn; public int r, g, b;
            public string government; public int capital; public int flagSeed; public List<int> provinces = new List<int>();
            public string leader; public string overlord;
            public EconomyJson economy;
        }
        [Serializable]
        private class EconomyJson
        {
            public int workshops, civilianFactories, commercialCenters, privateCompanies;
            public float privateCapital;
            public int militaryFactories, aircraftFactories, shipyards;
            public float exports, imports;
        }
        [Serializable] private class CountryListJson { public List<CountryJson> countries = new List<CountryJson>(); }
        [Serializable] private class CityJson { public string name; public float u; public float v; public int population; public bool capital; public string owner; }
        [Serializable] private class CityListJson { public List<CityJson> cities = new List<CityJson>(); }
        [Serializable] private class RiverJson { public string name; public float width = 1f; public List<float> points = new List<float>(); }
        [Serializable] private class RiverListJson { public List<RiverJson> rivers = new List<RiverJson>(); }

        public static string GetMapFolder(string mapName) =>
            Path.Combine(Application.streamingAssetsPath, MapsFolderName, mapName);

        public static bool MapExists(string folder) =>
            File.Exists(Path.Combine(folder, "provinces.png")) && File.Exists(Path.Combine(folder, "definition.csv"));

        // ================================================================ EXPORTAR

        public static void Export(WorldData world, string folder)
        {
            Directory.CreateDirectory(folder);
            var map = world.Map;
            int w = map.Width, h = map.Height, n = w * h;

            File.WriteAllText(Path.Combine(folder, "map.json"), JsonUtility.ToJson(new MapInfoJson
            {
                name = world.Name, width = w, height = h, seaLevel = map.SeaLevel,
                projection = map.MillerProjection ? "miller" : "linear", latTop = map.LatTop, latBottom = map.LatBottom,
                startDate = world.StartDate.ToString("yyyy-MM-dd"), normalStrength = map.NormalStrength,
            }, true));

            var provincePixels = new Color32[n];
            var heightPixels = new Color32[n];
            for (int i = 0; i < n; i++)
            {
                provincePixels[i] = world.Provinces[map.ProvinceIds[i]].MapColor;
                byte hv = (byte)Mathf.Clamp(Mathf.RoundToInt(map.Heights[i] * 255f), 0, 255);
                heightPixels[i] = new Color32(hv, hv, hv, 255);
            }
            WritePng(Path.Combine(folder, "provinces.png"), w, h, provincePixels);
            WritePng(Path.Combine(folder, "heightmap.png"), w, h, heightPixels);
            WritePng(Path.Combine(folder, "colormap.png"), w, h, map.ColorMap);

            var csv = new StringBuilder();
            csv.AppendLine("0;0;0;0;land;false;unknown;0");
            foreach (var p in world.Provinces)
            {
                string type = p.Type == ProvinceType.Land ? "land" : p.Type == ProvinceType.Sea ? "sea" : "lake";
                csv.Append(p.Id + 1).Append(';').Append(p.MapColor.r).Append(';').Append(p.MapColor.g).Append(';')
                    .Append(p.MapColor.b).Append(';').Append(type).Append(';').Append(p.Coastal ? "true" : "false")
                    .Append(';').Append(p.Terrain.ToString().ToLowerInvariant()).Append(';').Append(p.Continent).AppendLine();
            }
            File.WriteAllText(Path.Combine(folder, "definition.csv"), csv.ToString());

            var provinces = new ProvinceListJson();
            foreach (var p in world.Provinces)
                provinces.provinces.Add(new ProvinceJson { id = p.Id + 1, name = p.Name, population = p.Population, development = p.Development, gdp = p.Gdp });
            File.WriteAllText(Path.Combine(folder, "provinces.json"), JsonUtility.ToJson(provinces, true));

            var countries = new CountryListJson();
            foreach (var c in world.Countries)
            {
                var cj = new CountryJson
                {
                    tag = c.Tag, name = c.Name, r = c.Color.r, g = c.Color.g, b = c.Color.b,
                    government = c.Government.ToString(), capital = c.CapitalProvinceId + 1, flagSeed = c.FlagSeed,
                    leader = c.Leader, overlord = world.GetCountry(c.OverlordId)?.Tag,
                };
                foreach (int pid in c.Provinces) cj.provinces.Add(pid + 1);
                countries.countries.Add(cj);
            }
            File.WriteAllText(Path.Combine(folder, "countries.json"), JsonUtility.ToJson(countries, true));
        }

        /// <summary>Base económica del país; null si el archivo no la trae (se estimará al empezar).</summary>
        private static Economy.CountryEconomy ToEconomy(EconomyJson e)
        {
            // JsonUtility crea el objeto aunque falte en el archivo: sin edificios ni comercio se considera vacío.
            if (e == null || e.workshops + e.civilianFactories + e.commercialCenters + e.exports <= 0) return null;
            return new Economy.CountryEconomy
            {
                Workshops = e.workshops, CivilianFactories = e.civilianFactories, CommercialCenters = e.commercialCenters,
                PrivateCompanies = e.privateCompanies, PrivateCapital = e.privateCapital,
                MilitaryFactories = e.militaryFactories, AircraftFactories = e.aircraftFactories, Shipyards = e.shipyards,
                Exports = e.exports, Imports = e.imports,
            };
        }

        private static void WritePng(string path, int w, int h, Color32[] pixels)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        // ================================================================ CARGAR

        /// <summary>Imágenes ya decodificadas (la decodificación PNG/JPG debe hacerse en el hilo principal).</summary>
        private sealed class DecodedImages
        {
            public int Width, Height;
            public Color32[] Provinces;
            public Color32[] ColorMap;
            public Color32[] Height8;
            public byte[] Height16;
            public Color32[] Lights;
        }

        /// <summary>
        /// Carga asíncrona: decodifica las imágenes en el hilo principal (con pausas entre ellas) y hace
        /// todo el procesado pesado en un hilo de trabajo, para que la pantalla de carga no se congele.
        /// </summary>
        public static IEnumerator LoadAsync(string folder, GenerationProgress progress, Action<WorldData> onLoaded, Action<Exception> onError)
        {
            var images = new DecodedImages();
            Exception error = null;

            progress.Report(0.05f, "Leyendo provincias...");
            yield return null;
            try { DecodeProvinces(folder, images); } catch (Exception e) { error = e; }
            if (error != null) { onError(error); yield break; }

            progress.Report(0.15f, "Leyendo el relieve y los colores del terreno...");
            yield return null;
            try { DecodeTerrain(folder, images); } catch (Exception e) { error = e; }
            if (error != null) { onError(error); yield break; }

            var task = Task.Run(() => BuildWorld(folder, images, progress));
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) onError(task.Exception.GetBaseException());
            else onLoaded(task.Result);
        }

        /// <summary>Carga síncrona (bloquea). Debe llamarse en el hilo principal.</summary>
        public static WorldData Load(string folder)
        {
            var images = new DecodedImages();
            DecodeProvinces(folder, images);
            DecodeTerrain(folder, images);
            return BuildWorld(folder, images, new GenerationProgress());
        }

        private static void DecodeProvinces(string folder, DecodedImages images)
        {
            var tex = ReadImage(Path.Combine(folder, "provinces.png"));
            images.Width = tex.width;
            images.Height = tex.height;
            images.Provinces = tex.GetPixels32();
            UnityEngine.Object.Destroy(tex);
        }

        private static void DecodeTerrain(string folder, DecodedImages images)
        {
            int w = images.Width, h = images.Height;
            string r16 = Path.Combine(folder, "heightmap.r16");
            string png = Path.Combine(folder, "heightmap.png");
            if (File.Exists(r16)) images.Height16 = File.ReadAllBytes(r16);
            else if (File.Exists(png))
            {
                var tex = ReadImage(png);
                images.Height8 = tex.width == w && tex.height == h ? tex.GetPixels32() : ResizeNearest(tex, w, h);
                UnityEngine.Object.Destroy(tex);
            }

            string lightsPath = Path.Combine(folder, "citylights.png");
            if (File.Exists(lightsPath))
            {
                var tex = ReadImage(lightsPath);
                images.Lights = tex.width == w && tex.height == h ? tex.GetPixels32() : ResizeNearest(tex, w, h);
                UnityEngine.Object.Destroy(tex);
            }

            foreach (var name in new[] { "colormap.png", "colormap.jpg" })
            {
                string path = Path.Combine(folder, name);
                if (!File.Exists(path)) continue;
                var tex = ReadImage(path);
                images.ColorMap = tex.width == w && tex.height == h ? tex.GetPixels32() : ResizeNearest(tex, w, h);
                UnityEngine.Object.Destroy(tex);
                break;
            }
        }

        /// <summary>Construye el mundo a partir de las imágenes decodificadas. Seguro en hilos de trabajo.</summary>
        private static WorldData BuildWorld(string folder, DecodedImages images, GenerationProgress progress)
        {
            progress.Report(0.3f, "Construyendo provincias...");
            var info = new MapInfoJson { name = Path.GetFileName(folder), seaLevel = 0.4f };
            string infoPath = Path.Combine(folder, "map.json");
            if (File.Exists(infoPath)) JsonUtility.FromJsonOverwrite(File.ReadAllText(infoPath), info);

            int w = images.Width, h = images.Height, n = w * h;
            var map = new MapData(w, h)
            {
                SeaLevel = info.seaLevel,
                MillerProjection = string.Equals(info.projection, "miller", StringComparison.OrdinalIgnoreCase),
                LatTop = info.latTop,
                LatBottom = info.latBottom,
                NormalStrength = info.normalStrength,
            };
            var world = new WorldData(map) { Name = string.IsNullOrEmpty(info.name) ? "Mapa" : info.name, SourceFolder = folder };
            if (DateTime.TryParse(info.startDate, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var start))
                world.StartDate = start.Date.AddHours(12);
            if (images.Lights != null)
            {
                map.CityLights = new byte[n];
                for (int i = 0; i < n; i++) map.CityLights[i] = images.Lights[i].r;
                images.Lights = null;
            }

            // definition.csv → provincias y diccionario color → id interno.
            var colorToId = new Dictionary<int, int>();
            var fileIdToInternal = new Dictionary<int, int>();
            foreach (var rawLine in File.ReadAllLines(Path.Combine(folder, "definition.csv")))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var parts = line.Split(';');
                if (parts.Length < 5 || !int.TryParse(parts[0], out int fileId) || fileId == 0) continue;
                byte r = byte.Parse(parts[1]), g = byte.Parse(parts[2]), b = byte.Parse(parts[3]);
                var province = new Province
                {
                    Id = world.Provinces.Count,
                    MapColor = new Color32(r, g, b, 255),
                    Type = ParseType(parts[4]),
                    Terrain = parts.Length > 6 ? ParseTerrain(parts[6]) : TerrainType.Plains,
                    Continent = parts.Length > 7 && int.TryParse(parts[7], out int cont) ? cont : 0,
                    Name = "Provincia " + fileId,
                };
                if (province.Type == ProvinceType.Sea) province.Terrain = TerrainType.Ocean;
                if (province.Type == ProvinceType.Lake) province.Terrain = TerrainType.Lake;
                colorToId[ColorKey(province.MapColor)] = province.Id;
                fileIdToInternal[fileId] = province.Id;
                world.Provinces.Add(province);
            }

            var pixels = images.Provinces;
            int unknown = 0;
            int lastKey = -1, lastId = -1;
            for (int i = 0; i < n; i++)
            {
                int key = ColorKey(pixels[i]);
                if (key == lastKey) { map.ProvinceIds[i] = lastId; continue; }
                if (!colorToId.TryGetValue(key, out int id)) { id = -1; unknown++; }
                map.ProvinceIds[i] = id;
                lastKey = key;
                lastId = id;
            }
            images.Provinces = null;
            if (unknown > 0)
            {
                Debug.LogWarning($"[MapFileIO] {unknown} píxeles con colores que no están en definition.csv; se rellenan con el vecino.");
                FillUnknownPixels(map);
            }

            // Relieve.
            progress.Report(0.5f, "Procesando el relieve...");
            if (images.Height16 != null && images.Height16.Length >= n * 2)
            {
                var bytes = images.Height16;
                for (int i = 0; i < n; i++) map.Heights[i] = (bytes[i * 2] | (bytes[i * 2 + 1] << 8)) / 65535f;
            }
            else if (images.Height8 != null)
            {
                for (int i = 0; i < n; i++) map.Heights[i] = images.Height8[i].r / 255f;
                SmoothHeights(map, 2); // suaviza el escalonado de 8 bits
            }
            else
            {
                for (int i = 0; i < n; i++) map.Heights[i] = map.SeaLevel + 0.05f;
            }
            // La costa la define el mapa de provincias: la tierra siempre queda sobre el nivel del mar.
            for (int i = 0; i < n; i++)
            {
                bool water = world.Provinces[map.ProvinceIds[i]].IsWater;
                if (water && map.Heights[i] >= map.SeaLevel) map.Heights[i] = map.SeaLevel - 0.003f;
                else if (!water && map.Heights[i] < map.SeaLevel) map.Heights[i] = map.SeaLevel + 0.003f;
            }

            progress.Report(0.65f, "Analizando fronteras...");
            world.BuildDerivedData();

            // Colormap opcional (si no existe se colorea por terreno).
            if (images.ColorMap != null) map.ColorMap = images.ColorMap;
            else
                for (int i = 0; i < n; i++) map.ColorMap[i] = TerrainColor(world.Provinces[map.ProvinceIds[i]].Terrain);

            // Datos extra de provincias.
            progress.Report(0.8f, "Cargando provincias y naciones...");
            string provPath = Path.Combine(folder, "provinces.json");
            if (File.Exists(provPath))
            {
                var list = JsonUtility.FromJson<ProvinceListJson>(File.ReadAllText(provPath));
                foreach (var pj in list.provinces)
                {
                    if (!fileIdToInternal.TryGetValue(pj.id, out int pid)) continue;
                    var p = world.Provinces[pid];
                    if (!string.IsNullOrEmpty(pj.name)) p.Name = pj.name;
                    p.Population = pj.population;
                    p.Development = Mathf.Clamp(pj.development, 1, 10);
                    p.Gdp = pj.gdp;
                }
            }
            else
            {
                foreach (var p in world.Provinces)
                    if (!p.IsWater) p.Population = Mathf.Max(800, (int)(p.PixelCount * p.Terrain.PopulationDensity() * 1500));
            }

            // Países.
            string countriesPath = Path.Combine(folder, "countries.json");
            if (File.Exists(countriesPath))
            {
                var list = JsonUtility.FromJson<CountryListJson>(File.ReadAllText(countriesPath));
                var byTag = new Dictionary<string, Country>();
                foreach (var cj in list.countries)
                {
                    var country = new Country
                    {
                        Id = world.Countries.Count,
                        Tag = cj.tag,
                        Name = cj.name,
                        NameEn = cj.nameEn,
                        Color = new Color32((byte)cj.r, (byte)cj.g, (byte)cj.b, 255),
                        Government = Enum.TryParse(cj.government, true, out GovernmentType gov) ? gov : GovernmentType.NonAligned,
                        FlagSeed = cj.flagSeed != 0 ? cj.flagSeed : (cj.tag ?? "").GetHashCode(),
                        Leader = cj.leader,
                        Economy = ToEconomy(cj.economy),
                    };
                    world.Countries.Add(country);
                    if (!string.IsNullOrEmpty(cj.tag)) byTag[cj.tag] = country;
                    foreach (int fid in cj.provinces)
                        if (fileIdToInternal.TryGetValue(fid, out int pid) && !world.Provinces[pid].IsWater)
                            world.SetOwner(world.Provinces[pid], country);
                    country.CapitalProvinceId = fileIdToInternal.TryGetValue(cj.capital, out int cap) ? cap
                        : country.Provinces.Count > 0 ? country.Provinces[0] : -1;
                }
                // Soberanos (se resuelven al final porque pueden aparecer después en la lista).
                for (int i = 0; i < list.countries.Count; i++)
                {
                    var tag = list.countries[i].overlord;
                    if (!string.IsNullOrEmpty(tag) && byTag.TryGetValue(tag, out var lord))
                        world.Countries[i].OverlordId = lord.Id;
                }
            }

            LoadFeatures(folder, world);
            world.RecalculateCountryStats();
            progress.Report(1f, "Listo");
            return world;
        }

        /// <summary>Ciudades y ríos (opcionales): se usan solo para decorar el mapa.</summary>
        private static void LoadFeatures(string folder, WorldData world)
        {
            var byTag = new Dictionary<string, int>();
            foreach (var c in world.Countries)
                if (!string.IsNullOrEmpty(c.Tag)) byTag[c.Tag] = c.Id;

            string citiesPath = Path.Combine(folder, "cities.json");
            if (File.Exists(citiesPath))
            {
                foreach (var cj in JsonUtility.FromJson<CityListJson>(File.ReadAllText(citiesPath)).cities)
                {
                    world.Cities.Add(new CityInfo
                    {
                        Name = cj.name, UV = new Vector2(cj.u, cj.v), Population = cj.population, IsCapital = cj.capital,
                        OwnerId = cj.owner != null && byTag.TryGetValue(cj.owner, out int id) ? id : -1,
                    });
                }
            }
            CompleteCities(world);

            string riversPath = Path.Combine(folder, "rivers.json");
            if (File.Exists(riversPath))
            {
                foreach (var rj in JsonUtility.FromJson<RiverListJson>(File.ReadAllText(riversPath)).rivers)
                {
                    var river = new RiverPath { Name = rj.name, Width = rj.width };
                    for (int i = 0; i + 1 < rj.points.Count; i += 2) river.Points.Add(new Vector2(rj.points[i], rj.points[i + 1]));
                    if (river.Points.Count >= 2) world.Rivers.Add(river);
                }
            }
        }

        /// <summary>
        /// Asigna provincia y puntos de victoria a cada ciudad, y añade la capital de los países que no la tengan
        /// en cities.json (se usa la provincia capital del país).
        /// </summary>
        private static void CompleteCities(WorldData world)
        {
            var map = world.Map;
            var hasCapital = new HashSet<int>();
            foreach (var city in world.Cities)
                if (city.IsCapital && city.OwnerId >= 0) hasCapital.Add(city.OwnerId);

            foreach (var country in world.Countries)
            {
                if (hasCapital.Contains(country.Id)) continue;
                var capital = world.GetProvince(country.CapitalProvinceId);
                if (capital == null || capital.IsWater) continue;
                world.Cities.Add(new CityInfo
                {
                    Name = capital.Name, OwnerId = country.Id, IsCapital = true, Population = Mathf.Max(capital.Population, 100000),
                    UV = new Vector2((capital.Center.x + 0.5f) / map.Width, (capital.Center.y + 0.5f) / map.Height),
                });
            }

            foreach (var city in world.Cities)
            {
                int px = Mathf.Clamp((int)(Mathf.Repeat(city.UV.x, 1f) * map.Width), 0, map.Width - 1);
                int py = Mathf.Clamp((int)(city.UV.y * map.Height), 0, map.Height - 1);
                city.ProvinceId = map.ProvinceIds[py * map.Width + px];
                city.VictoryPoints = CityInfo.ComputeVictoryPoints(city.Population, city.IsCapital);
            }
        }

        private static Texture2D ReadImage(string path)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!tex.LoadImage(File.ReadAllBytes(path)))
                throw new IOException("No se pudo leer la imagen " + path);
            return tex;
        }

        private static Color32[] ResizeNearest(Texture2D tex, int w, int h)
        {
            var src = tex.GetPixels32();
            var dst = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                int sy = y * tex.height / h;
                for (int x = 0; x < w; x++) dst[y * w + x] = src[sy * tex.width + x * tex.width / w];
            }
            return dst;
        }

        private static void FillUnknownPixels(MapData map)
        {
            int w = map.Width, h = map.Height;
            var ids = map.ProvinceIds;
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (ids[i] >= 0) continue;
                    int v = x > 0 && ids[i - 1] >= 0 ? ids[i - 1]
                        : x < w - 1 && ids[i + 1] >= 0 ? ids[i + 1]
                        : y > 0 && ids[i - w] >= 0 ? ids[i - w]
                        : y < h - 1 && ids[i + w] >= 0 ? ids[i + w] : -1;
                    if (v >= 0)
                    {
                        ids[i] = v;
                        changed = true;
                    }
                }
            }
            for (int i = 0; i < ids.Length; i++)
                if (ids[i] < 0) ids[i] = 0;
        }

        private static void SmoothHeights(MapData map, int passes)
        {
            int w = map.Width, h = map.Height;
            var tmp = new float[map.Heights.Length];
            for (int pass = 0; pass < passes; pass++)
            {
                var src = map.Heights;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float sum = 0;
                    int count = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                        sum += src[yy * w + xx];
                        count++;
                    }
                    tmp[y * w + x] = sum / count;
                }
                Array.Copy(tmp, src, src.Length);
            }
        }

        private static int ColorKey(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;

        private static ProvinceType ParseType(string s)
        {
            s = s.Trim().ToLowerInvariant();
            return s == "sea" ? ProvinceType.Sea : s == "lake" ? ProvinceType.Lake : ProvinceType.Land;
        }

        private static TerrainType ParseTerrain(string s)
        {
            s = s.Trim().ToLowerInvariant();
            // Nombres de HOI4 + los nuestros.
            switch (s)
            {
                case "ocean": return TerrainType.Ocean;
                case "lakes":
                case "lake": return TerrainType.Lake;
                case "forest": return TerrainType.Forest;
                case "hills": return TerrainType.Hills;
                case "mountain":
                case "mountains": return TerrainType.Mountains;
                case "desert": return TerrainType.Desert;
                case "jungle": return TerrainType.Jungle;
                case "marsh": return TerrainType.Marsh;
                case "tundra": return TerrainType.Tundra;
                case "arctic": return TerrainType.Arctic;
                default: return TerrainType.Plains;
            }
        }

        private static Color32 TerrainColor(TerrainType t)
        {
            switch (t)
            {
                case TerrainType.Ocean: return new Color32(40, 60, 80, 255);
                case TerrainType.Lake: return new Color32(60, 80, 90, 255);
                case TerrainType.Forest: return new Color32(66, 92, 50, 255);
                case TerrainType.Hills: return new Color32(128, 122, 84, 255);
                case TerrainType.Mountains: return new Color32(118, 108, 96, 255);
                case TerrainType.Desert: return new Color32(196, 172, 122, 255);
                case TerrainType.Jungle: return new Color32(58, 92, 42, 255);
                case TerrainType.Marsh: return new Color32(86, 100, 70, 255);
                case TerrainType.Tundra: return new Color32(122, 124, 102, 255);
                case TerrainType.Arctic: return new Color32(236, 238, 242, 255);
                default: return new Color32(118, 134, 74, 255);
            }
        }
    }
}
