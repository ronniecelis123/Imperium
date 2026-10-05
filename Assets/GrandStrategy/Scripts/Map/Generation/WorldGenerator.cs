using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace GrandStrategy.Map.Generation
{
    [Serializable]
    public sealed class WorldGenSettings
    {
        public int Seed = 1936;
        [Tooltip("Resolución del mapa de provincias. Más alto = fronteras más finas pero carga más lenta.")]
        public int Width = 3072;
        public int Height = 1536;
        [Range(0.2f, 0.6f)] public float SeaLevel = 0.4f;
        [Range(0.15f, 0.6f)] public float LandFraction = 0.36f;
        [Tooltip("Separación media (en píxeles) entre centros de provincias terrestres.")]
        public int LandProvinceSpacing = 32;
        public int SeaProvinceSpacing = 110;
        public int CountryCount = 70;
        [Tooltip("Número de 'ciclos' de ruido continental a lo ancho del mapa. Menos = continentes más grandes.")]
        public float ContinentFrequency = 2.3f;
    }

    /// <summary>Progreso compartido entre el hilo de generación y la pantalla de carga.</summary>
    public sealed class GenerationProgress
    {
        private volatile string _status = "";
        private float _value;

        public string Status { get => _status; set => _status = value; }
        public float Value
        {
            get { lock (this) return _value; }
            set { lock (this) _value = value; }
        }

        public void Report(float value, string status)
        {
            Value = value;
            Status = status;
        }
    }

    /// <summary>
    /// Generador procedural de mundos. No usa ninguna API de Unity que requiera el hilo principal,
    /// así que puede ejecutarse en un Task mientras se muestra la pantalla de carga.
    /// Pasos: relieve → limpieza de islas/lagos → biomas y colormap → provincias (Dijkstra multi-fuente
    /// con coste ruidoso para fronteras orgánicas) → países (crecimiento sobre el grafo de provincias).
    /// </summary>
    public sealed class WorldGenerator
    {
        private const int MinIslandPixels = 40;
        private const int MinLakePixels = 80;

        private readonly WorldGenSettings _s;
        private readonly System.Random _rng;
        private int _w, _h, _n;
        private MapData _map;
        private byte[] _isLand;
        private byte[] _terrain;
        private int[] _component;
        private List<ComponentInfo> _components;

        private struct ComponentInfo
        {
            public bool IsLand;
            public int Size;
            public int FirstPixel;
            public bool IsLake;
        }

        public WorldGenerator(WorldGenSettings settings)
        {
            _s = settings;
            _rng = new System.Random(settings.Seed);
        }

        public WorldData Generate(GenerationProgress progress = null)
        {
            progress ??= new GenerationProgress();
            _w = _s.Width;
            _h = _s.Height;
            _n = _w * _h;
            _map = new MapData(_w, _h) { SeaLevel = _s.SeaLevel };

            progress.Report(0.02f, "Formando continentes...");
            GenerateHeights();

            progress.Report(0.25f, "Esculpiendo costas, islas y lagos...");
            CleanupComponents();

            progress.Report(0.35f, "Calculando climas y biomas...");
            GenerateBiomesAndColors();

            progress.Report(0.5f, "Trazando provincias...");
            var world = new WorldData(_map) { Name = "Mundo procedural #" + _s.Seed };
            GenerateProvinces(world);

            progress.Report(0.75f, "Analizando fronteras...");
            world.BuildDerivedData();
            AssignProvinceData(world);

            progress.Report(0.85f, "Fundando naciones...");
            GenerateCountries(world);
            world.RecalculateCountryStats();

            progress.Report(1f, "Listo");
            return world;
        }

        // ------------------------------------------------------------------ Relieve

        private void GenerateHeights()
        {
            var raw = new float[_n];
            var continent = new Noise(_s.Seed);
            var warp = new Noise(_s.Seed + 101);
            float freq = _s.ContinentFrequency;
            float aspect = _w / (float)_h;

            Parallel.For(0, _h, y =>
            {
                float v = y / (float)_h;
                for (int x = 0; x < _w; x++)
                {
                    float u = x / (float)_w;
                    float px = u * freq;
                    float py = v * freq / aspect;

                    // Domain warping: deforma las coordenadas para costas más naturales.
                    float wx = warp.Fbm(px + 5.2f, py + 1.3f, 4);
                    float wy = warp.Fbm(px + 9.1f, py + 2.8f, 4);
                    float c = continent.Fbm(px + wx * 1.1f, py + wy * 1.1f, 7);

                    // Hundir los bordes del mapa para que los continentes no queden cortados.
                    // El margen se modula con ruido para que las costas cercanas al borde no queden rectas.
                    float ex = SmoothStep(0f, 0.1f, Math.Min(u, 1 - u));
                    float ey = SmoothStep(0f, 0.2f, Math.Min(v, 1 - v));
                    c -= (1f - ex * ey) * (0.5f + 0.25f * wx);
                    // Franja final siempre de agua.
                    float edge = Math.Min(Math.Min(u, 1 - u) * 2f, Math.Min(v, 1 - v));
                    c -= (1f - SmoothStep(0f, 0.025f, edge)) * 2f;
                    raw[y * _w + x] = c;
                }
            });

            // Umbral por percentil para conseguir la fracción de tierra deseada.
            int step = 7;
            var samples = new List<float>(_n / step + 1);
            for (int i = 0; i < _n; i += step) samples.Add(raw[i]);
            samples.Sort();
            float threshold = samples[(int)((1f - _s.LandFraction) * (samples.Count - 1))];
            float cmin = samples[0];
            float cmax = samples[samples.Count - 1];

            var mountainMask = new Noise(_s.Seed + 202);
            var ridges = new Noise(_s.Seed + 303);
            var hills = new Noise(_s.Seed + 404);
            float sea = _s.SeaLevel;
            var heights = _map.Heights;

            Parallel.For(0, _h, y =>
            {
                float v = y / (float)_h;
                for (int x = 0; x < _w; x++)
                {
                    int i = y * _w + x;
                    float u = x / (float)_w;
                    float px = u * freq;
                    float py = v * freq / aspect;
                    float c = raw[i];

                    if (c >= threshold)
                    {
                        float e = (c - threshold) / Math.Max(1e-4f, cmax - threshold);
                        float m = SmoothStep(-0.08f, 0.3f, mountainMask.Fbm(px * 1.6f + 3f, py * 1.6f + 7f, 3));
                        float r = ridges.Ridged(px * 3.5f, py * 3.5f, 5);
                        float hl = hills.Fbm(px * 9f, py * 9f, 4) * 0.5f + 0.5f;
                        float land = 0.03f + 0.26f * (float)Math.Pow(e, 0.9) + 0.08f * hl
                                     + m * r * r * 0.95f * SmoothStep(0f, 0.1f, e);
                        heights[i] = sea + (1f - sea) * Clamp01(land);
                    }
                    else
                    {
                        float d = (threshold - c) / Math.Max(1e-4f, threshold - cmin);
                        heights[i] = sea * (1f - 0.9f * (float)Math.Pow(Clamp01(d * 1.6f), 0.6));
                    }
                }
            });
        }

        // ------------------------------------------------------------------ Islas y lagos

        private void CleanupComponents()
        {
            float sea = _s.SeaLevel;
            _isLand = new byte[_n];
            for (int i = 0; i < _n; i++) _isLand[i] = (byte)(_map.Heights[i] >= sea ? 1 : 0);

            LabelComponents();

            // Islas diminutas → mar; charcos diminutos → tierra.
            bool changed = false;
            for (int i = 0; i < _n; i++)
            {
                var info = _components[_component[i]];
                if (info.IsLand && info.Size < MinIslandPixels)
                {
                    _isLand[i] = 0;
                    _map.Heights[i] = sea - 0.01f;
                    changed = true;
                }
                else if (!info.IsLand && info.Size < MinLakePixels && !TouchesEdge(info.FirstPixel))
                {
                    _isLand[i] = 1;
                    _map.Heights[i] = sea + 0.005f;
                    changed = true;
                }
            }
            if (changed) LabelComponents();

            // El océano es la masa de agua más grande; el resto de masas de agua pequeñas son lagos.
            int ocean = -1, oceanSize = 0;
            for (int c = 0; c < _components.Count; c++)
                if (!_components[c].IsLand && _components[c].Size > oceanSize)
                {
                    ocean = c;
                    oceanSize = _components[c].Size;
                }
            for (int c = 0; c < _components.Count; c++)
            {
                var info = _components[c];
                if (!info.IsLand && c != ocean && info.Size < 25000)
                {
                    info.IsLake = true;
                    _components[c] = info;
                }
            }
        }

        private bool TouchesEdge(int pixel)
        {
            // Aproximación: solo comprueba el primer píxel (escaneado en orden, el más al sur-oeste).
            int x = pixel % _w, y = pixel / _w;
            return x == 0 || y == 0 || x == _w - 1 || y == _h - 1;
        }

        private void LabelComponents()
        {
            _component ??= new int[_n];
            for (int i = 0; i < _n; i++) _component[i] = -1;
            _components = new List<ComponentInfo>();
            var stack = new Stack<int>();

            for (int start = 0; start < _n; start++)
            {
                if (_component[start] >= 0) continue;
                int id = _components.Count;
                byte cls = _isLand[start];
                int size = 0;
                _component[start] = id;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    int p = stack.Pop();
                    size++;
                    int x = p % _w, y = p / _w;
                    if (x > 0) Visit(p - 1);
                    if (x < _w - 1) Visit(p + 1);
                    if (y > 0) Visit(p - _w);
                    if (y < _h - 1) Visit(p + _w);
                }
                _components.Add(new ComponentInfo { IsLand = cls == 1, Size = size, FirstPixel = start });

                void Visit(int q)
                {
                    if (_component[q] < 0 && _isLand[q] == cls)
                    {
                        _component[q] = id;
                        stack.Push(q);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ Biomas

        private void GenerateBiomesAndColors()
        {
            _terrain = new byte[_n];
            var tempNoise = new Noise(_s.Seed + 505);
            var moistNoise = new Noise(_s.Seed + 606);
            var grain = new Noise(_s.Seed + 707);
            var lakeFlags = new bool[_components.Count];
            for (int c = 0; c < _components.Count; c++) lakeFlags[c] = _components[c].IsLake;

            float sea = _s.SeaLevel;
            float freq = _s.ContinentFrequency;
            float aspect = _w / (float)_h;
            var heights = _map.Heights;
            var colors = _map.ColorMap;

            // Paleta "pintada" y desaturada, al estilo del colormap de HOI4.
            var desert = new Vector3(196, 172, 122);
            var savanna = new Vector3(162, 152, 88);
            var jungle = new Vector3(58, 92, 42);
            var steppe = new Vector3(168, 160, 108);
            var grass = new Vector3(118, 134, 74);
            var forest = new Vector3(66, 92, 50);
            var tundraDry = new Vector3(142, 138, 116);
            var tundra = new Vector3(122, 124, 102);
            var taiga = new Vector3(70, 86, 66);
            var rock = new Vector3(118, 108, 96);
            var snow = new Vector3(236, 238, 242);
            var marsh = new Vector3(86, 100, 70);
            var seabedShallow = new Vector3(150, 160, 140);
            var seabedDeep = new Vector3(40, 60, 80);

            Parallel.For(0, _h, y =>
            {
                float v = y / (float)_h;
                float lat = Math.Abs(v - 0.5f) * 2f; // 0 ecuador, 1 polo
                for (int x = 0; x < _w; x++)
                {
                    int i = y * _w + x;
                    float u = x / (float)_w;
                    float px = u * freq;
                    float py = v * freq / aspect;
                    float h = heights[i];

                    if (_isLand[i] == 0)
                    {
                        bool lake = lakeFlags[_component[i]];
                        _terrain[i] = (byte)(lake ? TerrainType.Lake : TerrainType.Ocean);
                        float depth = Clamp01((sea - h) / sea);
                        colors[i] = ToColor(Vector3.Lerp(seabedShallow, seabedDeep, Mathf.Sqrt(depth)));
                        continue;
                    }

                    float landE = (h - sea) / (1f - sea);
                    float temp = 1f - (float)Math.Pow(lat, 1.5) * 1.05f + tempNoise.Fbm(px * 2f, py * 2f, 3) * 0.2f - landE * 0.8f;
                    float moist = moistNoise.Fbm(px * 1.8f + 3f, py * 1.8f + 7f, 5) * 1.25f + 0.52f;
                    // Cinturón desértico subtropical (como el Sahara / Australia).
                    float belt = (lat - 0.33f) / 0.12f;
                    moist -= 0.28f * (float)Math.Exp(-belt * belt);
                    moist = Clamp01(moist);

                    TerrainType t;
                    if (temp < 0.06f) t = TerrainType.Arctic;
                    else if (landE > 0.38f) t = TerrainType.Mountains;
                    else if (temp < 0.2f) t = TerrainType.Tundra;
                    else if (landE > 0.21f) t = TerrainType.Hills;
                    else if (temp > 0.72f)
                        t = moist < 0.32f ? TerrainType.Desert : moist > 0.62f ? TerrainType.Jungle : TerrainType.Plains;
                    else if (moist < 0.22f) t = TerrainType.Desert;
                    else if (moist > 0.68f && landE < 0.06f) t = TerrainType.Marsh;
                    else if (moist > 0.52f) t = TerrainType.Forest;
                    else t = TerrainType.Plains;
                    _terrain[i] = (byte)t;

                    // Color continuo (sin cortes duros entre biomas).
                    Vector3 hot = Tri(desert, savanna, jungle, moist);
                    Vector3 temperate = Tri(steppe, grass, forest, moist);
                    Vector3 cold = Tri(tundraDry, tundra, taiga, moist);
                    Vector3 col = temp > 0.55f
                        ? Vector3.Lerp(temperate, hot, SmoothStep(0.55f, 0.82f, temp))
                        : Vector3.Lerp(cold, temperate, SmoothStep(0.15f, 0.45f, temp));
                    if (t == TerrainType.Marsh) col = Vector3.Lerp(col, marsh, 0.6f);
                    col = Vector3.Lerp(col, rock, SmoothStep(0.22f, 0.45f, landE));
                    float snowAmount = Math.Max(SmoothStep(0.55f, 0.75f, landE), SmoothStep(0.1f, 0.0f, temp));
                    col = Vector3.Lerp(col, snow, snowAmount);
                    col *= 0.93f + 0.14f * (grain.Fbm(px * 60f, py * 60f, 2) * 0.5f + 0.5f);
                    colors[i] = ToColor(col);
                }
            });
        }

        // ------------------------------------------------------------------ Provincias

        private void GenerateProvinces(WorldData world)
        {
            var seeds = new List<int>();
            var seedTypes = new List<ProvinceType>();
            var compHasSeed = new bool[_components.Count];
            var density = new Noise(_s.Seed + 808);

            void AddSeed(int pixel, ProvinceType type)
            {
                seeds.Add(pixel);
                seedTypes.Add(type);
                compHasSeed[_component[pixel]] = true;
            }

            // Semillas terrestres en rejilla con jitter; algunas celdas se saltan para variar el tamaño.
            int ls = Math.Max(8, _s.LandProvinceSpacing);
            for (int gy = 0; gy * ls < _h; gy++)
            for (int gx = 0; gx * ls < _w; gx++)
            {
                int x = Math.Min(_w - 1, gx * ls + _rng.Next(ls));
                int y = Math.Min(_h - 1, gy * ls + _rng.Next(ls));
                int p = y * _w + x;
                if (_isLand[p] == 0) continue;
                float d = density.Fbm(x * 3f / _w, y * 3f / _w, 3) * 0.5f + 0.5f;
                var terrain = (TerrainType)_terrain[p];
                float skip = SmoothStep(0.55f, 0.85f, d) * 0.55f;
                if (terrain == TerrainType.Desert || terrain == TerrainType.Arctic || terrain == TerrainType.Tundra)
                    skip += 0.45f;
                if (_rng.NextDouble() < skip) continue;
                AddSeed(p, ProvinceType.Land);
            }

            // Semillas marítimas (zonas marinas más grandes, como en HOI4).
            int ss = Math.Max(16, _s.SeaProvinceSpacing);
            for (int gy = 0; gy * ss < _h; gy++)
            for (int gx = 0; gx * ss < _w; gx++)
            {
                int x = Math.Min(_w - 1, gx * ss + _rng.Next(ss));
                int y = Math.Min(_h - 1, gy * ss + _rng.Next(ss));
                int p = y * _w + x;
                if (_isLand[p] == 1) continue;
                AddSeed(p, _components[_component[p]].IsLake ? ProvinceType.Lake : ProvinceType.Sea);
            }

            // Toda isla / lago / mar cerrado necesita al menos una provincia.
            for (int c = 0; c < _components.Count; c++)
            {
                if (compHasSeed[c]) continue;
                var info = _components[c];
                var type = info.IsLand ? ProvinceType.Land : info.IsLake ? ProvinceType.Lake : ProvinceType.Sea;
                AddSeed(info.FirstPixel, type);
            }

            // Coste ruidoso por píxel → fronteras irregulares y orgánicas.
            var cost = new byte[_n];
            var costNoise = new Noise(_s.Seed + 909);
            Parallel.For(0, _h, y =>
            {
                for (int x = 0; x < _w; x++)
                {
                    float nv = costNoise.Fbm(x * 24f / _w, y * 24f / _w, 3) * 0.5f + 0.5f;
                    int c = 1 + (int)(Clamp01(nv) * 6f);
                    // Las crestas montañosas son caras de cruzar: las fronteras tienden a seguirlas.
                    if (_terrain[y * _w + x] == (byte)TerrainType.Mountains) c += 2;
                    cost[y * _w + x] = (byte)c;
                }
            });

            // Dijkstra multi-fuente con cola de cubos (algoritmo de Dial): O(N).
            const int bucketCount = 16; // > coste máximo
            var buckets = new List<int>[bucketCount];
            for (int b = 0; b < bucketCount; b++) buckets[b] = new List<int>(4096);
            var dist = new int[_n];
            var ids = _map.ProvinceIds;
            for (int i = 0; i < _n; i++)
            {
                dist[i] = int.MaxValue;
                ids[i] = -1;
            }

            int pending = 0;
            for (int s = 0; s < seeds.Count; s++)
            {
                int p = seeds[s];
                if (ids[p] >= 0) continue; // dos semillas en el mismo píxel
                ids[p] = s;
                dist[p] = 0;
                buckets[0].Add(p);
                pending++;
            }

            int current = 0;
            while (pending > 0)
            {
                var bucket = buckets[current % bucketCount];
                for (int k = 0; k < bucket.Count; k++)
                {
                    int p = bucket[k];
                    pending--;
                    if (dist[p] != current) continue;
                    int x = p % _w, y = p / _w;
                    byte cls = _isLand[p];
                    int owner = ids[p];
                    if (x > 0) Relax(p - 1);
                    if (x < _w - 1) Relax(p + 1);
                    if (y > 0) Relax(p - _w);
                    if (y < _h - 1) Relax(p + _w);

                    void Relax(int q)
                    {
                        if (_isLand[q] != cls) return;
                        int nd = current + cost[q];
                        if (nd >= dist[q]) return;
                        dist[q] = nd;
                        ids[q] = owner;
                        buckets[nd % bucketCount].Add(q);
                        pending++;
                    }
                }
                bucket.Clear();
                current++;
            }

            // Compactar ids (descarta semillas duplicadas que no recibieron píxeles).
            var used = new bool[seeds.Count];
            for (int i = 0; i < _n; i++)
                if (ids[i] >= 0) used[ids[i]] = true;
            var remap = new int[seeds.Count];
            var nameGen = new NameGenerator(_s.Seed + 1);
            var usedColors = new HashSet<int>();
            for (int s = 0; s < seeds.Count; s++)
            {
                if (!used[s])
                {
                    remap[s] = -1;
                    continue;
                }
                remap[s] = world.Provinces.Count;
                var province = new Province
                {
                    Id = world.Provinces.Count,
                    Type = seedTypes[s],
                    MapColor = UniqueColor(usedColors),
                    Continent = _component[seeds[s]],
                };
                province.Name = province.Type == ProvinceType.Land ? nameGen.ProvinceName()
                    : province.Type == ProvinceType.Lake ? "Lago " + nameGen.ProvinceName()
                    : "Mar de " + nameGen.ProvinceName();
                world.Provinces.Add(province);
            }
            for (int i = 0; i < _n; i++) ids[i] = ids[i] >= 0 ? remap[ids[i]] : 0;
        }

        private void AssignProvinceData(WorldData world)
        {
            // Terreno dominante por provincia.
            int count = world.Provinces.Count;
            var votes = new int[count * MapEnumExtensions.TerrainCount];
            var ids = _map.ProvinceIds;
            for (int i = 0; i < _n; i++) votes[ids[i] * MapEnumExtensions.TerrainCount + _terrain[i]]++;

            foreach (var p in world.Provinces)
            {
                int best = 0, bestVotes = -1;
                for (int t = 0; t < MapEnumExtensions.TerrainCount; t++)
                {
                    int vv = votes[p.Id * MapEnumExtensions.TerrainCount + t];
                    if (vv > bestVotes)
                    {
                        bestVotes = vv;
                        best = t;
                    }
                }
                p.Terrain = (TerrainType)best;
                if (p.IsWater) continue;

                double pop = p.PixelCount * p.Terrain.PopulationDensity() * 1500.0 * (0.5 + _rng.NextDouble());
                if (p.Coastal) pop *= 1.35;
                p.Population = (int)Math.Max(800, pop);
                int dev = 1 + (int)(_rng.NextDouble() * 4) + (p.Coastal ? 1 : 0)
                          + (p.Terrain == TerrainType.Plains ? 2 : 0);
                p.Development = Mathf.Clamp(dev, 1, 10);
            }
        }

        // ------------------------------------------------------------------ Países

        private void GenerateCountries(WorldData world)
        {
            var land = new List<Province>();
            foreach (var p in world.Provinces)
                if (!p.IsWater) land.Add(p);
            if (land.Count == 0) return;

            long landPixels = 0;
            foreach (var p in land) landPixels += p.PixelCount;
            float minCapitalDist = (float)Math.Sqrt(landPixels / (double)Math.Max(1, _s.CountryCount)) * 0.85f;

            var names = new NameGenerator(_s.Seed + 2);
            var strength = new List<float>();

            // Capitales separadas entre sí.
            var shuffled = new List<Province>(land);
            Shuffle(shuffled);
            var capitals = new List<Province>();
            foreach (var p in shuffled)
            {
                if (capitals.Count >= _s.CountryCount) break;
                if (p.PixelCount < 150) continue;
                bool farEnough = true;
                foreach (var c in capitals)
                    if ((c.Center - p.Center).sqrMagnitude < minCapitalDist * minCapitalDist)
                    {
                        farEnough = false;
                        break;
                    }
                if (farEnough) capitals.Add(p);
            }

            foreach (var cap in capitals)
            {
                CreateCountry(world, names, cap);
                // Unas pocas potencias grandes y muchas naciones menores.
                strength.Add(0.75f + (float)Math.Pow(_rng.NextDouble(), 2.0) * 1.5f);
            }
            GrowCountries(world, capitals, strength);

            // Masas de tierra sin capital: se anexan al vecino cercano o forman nuevas naciones.
            var visited = new bool[world.Provinces.Count];
            foreach (var start in land)
            {
                if (start.HasOwner || visited[start.Id]) continue;
                var group = new List<Province>();
                var queue = new Queue<Province>();
                queue.Enqueue(start);
                visited[start.Id] = true;
                while (queue.Count > 0)
                {
                    var p = queue.Dequeue();
                    group.Add(p);
                    foreach (int nid in p.Neighbors)
                    {
                        var q = world.Provinces[nid];
                        if (!q.IsWater && !q.HasOwner && !visited[nid])
                        {
                            visited[nid] = true;
                            queue.Enqueue(q);
                        }
                    }
                }

                Province nearest = null;
                float bestD = float.MaxValue;
                foreach (var p in land)
                {
                    if (!p.HasOwner) continue;
                    float d = (p.Center - group[0].Center).sqrMagnitude;
                    if (d < bestD)
                    {
                        bestD = d;
                        nearest = p;
                    }
                }

                if (nearest != null && group.Count <= 6 && Math.Sqrt(bestD) < _w * 0.06f)
                {
                    var owner = world.GetCountry(nearest.OwnerId);
                    foreach (var p in group) world.SetOwner(p, owner);
                    continue;
                }

                int newCount = Math.Max(1, group.Count / 25);
                Shuffle(group);
                var newCaps = new List<Province>();
                var newStrength = new List<float>();
                group.Sort((a, b) => b.PixelCount.CompareTo(a.PixelCount));
                for (int k = 0; k < newCount && k < group.Count; k++)
                {
                    newCaps.Add(group[k * (group.Count / newCount)]);
                    newStrength.Add(1f);
                }
                foreach (var cap in newCaps) CreateCountry(world, names, cap);
                GrowCountries(world, newCaps, newStrength);
            }

            AssignCountryColors(world);
        }

        private void CreateCountry(WorldData world, NameGenerator names, Province capital)
        {
            string root = names.CountryRoot();
            var gov = PickGovernment();
            var country = new Country
            {
                Id = world.Countries.Count,
                Tag = names.MakeTag(root),
                Name = names.OfficialName(root, gov),
                Government = gov,
                CapitalProvinceId = capital.Id,
                FlagSeed = _rng.Next(),
            };
            world.Countries.Add(country);
            world.SetOwner(capital, country);
            capital.Development = Math.Min(10, capital.Development + 3);
            capital.Population = (int)(capital.Population * 2.5f);
            capital.Name = names.ProvinceName();
        }

        private GovernmentType PickGovernment()
        {
            double r = _rng.NextDouble();
            if (r < 0.28) return GovernmentType.Monarchy;
            if (r < 0.52) return GovernmentType.Democracy;
            if (r < 0.66) return GovernmentType.Communism;
            if (r < 0.78) return GovernmentType.Fascism;
            return GovernmentType.NonAligned;
        }

        /// <summary>Dijkstra sobre el grafo de provincias: cada país crece desde su capital.</summary>
        private void GrowCountries(WorldData world, List<Province> capitals, List<float> strength)
        {
            var best = new Dictionary<int, float>();
            var heap = new MinHeap();
            for (int i = 0; i < capitals.Count; i++)
            {
                best[capitals[i].Id] = 0;
                heap.Push(0, capitals[i].Id, capitals[i].OwnerId, strength[i]);
            }

            while (heap.Count > 0)
            {
                var item = heap.Pop();
                if (best.TryGetValue(item.Province, out float known) && item.Cost > known) continue;
                var p = world.Provinces[item.Province];
                if (!p.HasOwner) world.SetOwner(p, world.Countries[item.Country]);
                else if (p.OwnerId != item.Country) continue;

                foreach (int nid in p.Neighbors)
                {
                    var q = world.Provinces[nid];
                    if (q.IsWater || q.HasOwner) continue;
                    float step = (q.Center - p.Center).magnitude * q.Terrain.MovementCost() / item.Strength;
                    float nc = item.Cost + step;
                    if (best.TryGetValue(nid, out float old) && old <= nc) continue;
                    best[nid] = nc;
                    heap.Push(nc, nid, item.Country, item.Strength);
                }
            }
        }

        private void AssignCountryColors(WorldData world)
        {
            world.RecalculateCountryStats(); // necesario para conocer vecinos
            var assigned = new bool[world.Countries.Count];
            foreach (var c in world.Countries)
            {
                Color32 bestColor = default;
                float bestScore = -1;
                for (int attempt = 0; attempt < 16; attempt++)
                {
                    float hue = (float)_rng.NextDouble();
                    float sat = 0.38f + (float)_rng.NextDouble() * 0.4f;
                    float val = 0.55f + (float)_rng.NextDouble() * 0.38f;
                    Color32 candidate = Color.HSVToRGB(hue, sat, val);
                    float minDist = 999f;
                    foreach (int nid in c.Neighbors)
                    {
                        if (!assigned[nid]) continue;
                        minDist = Math.Min(minDist, ColorDistance(candidate, world.Countries[nid].Color));
                    }
                    if (minDist > bestScore)
                    {
                        bestScore = minDist;
                        bestColor = candidate;
                    }
                    if (minDist > 90f) break;
                }
                c.Color = bestColor;
                assigned[c.Id] = true;
            }
        }

        // ------------------------------------------------------------------ Utilidades

        private Color32 UniqueColor(HashSet<int> used)
        {
            while (true)
            {
                int r = _rng.Next(1, 256), g = _rng.Next(1, 256), b = _rng.Next(1, 256);
                int key = (r << 16) | (g << 8) | b;
                if (used.Add(key)) return new Color32((byte)r, (byte)g, (byte)b, 255);
            }
        }

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static float ColorDistance(Color32 a, Color32 b)
        {
            int dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return (float)Math.Sqrt(dr * dr + dg * dg + db * db);
        }

        private static Vector3 Tri(Vector3 dry, Vector3 mid, Vector3 wet, float m) =>
            m < 0.5f ? Vector3.Lerp(dry, mid, m * 2f) : Vector3.Lerp(mid, wet, (m - 0.5f) * 2f);

        private static Color32 ToColor(Vector3 c) =>
            new Color32((byte)Math.Min(255f, Math.Max(0f, c.x)), (byte)Math.Min(255f, Math.Max(0f, c.y)),
                (byte)Math.Min(255f, Math.Max(0f, c.z)), 255);

        private static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;

        private static float SmoothStep(float e0, float e1, float x)
        {
            float t = Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Montículo binario mínimo (netstandard2.1 no trae PriorityQueue).</summary>
        private sealed class MinHeap
        {
            public struct Item
            {
                public float Cost;
                public int Province;
                public int Country;
                public float Strength;
            }

            private readonly List<Item> _items = new List<Item>();
            public int Count => _items.Count;

            public void Push(float cost, int province, int country, float strength)
            {
                _items.Add(new Item { Cost = cost, Province = province, Country = country, Strength = strength });
                int i = _items.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (_items[parent].Cost <= _items[i].Cost) break;
                    (_items[parent], _items[i]) = (_items[i], _items[parent]);
                    i = parent;
                }
            }

            public Item Pop()
            {
                var top = _items[0];
                int last = _items.Count - 1;
                _items[0] = _items[last];
                _items.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, smallest = i;
                    if (l < _items.Count && _items[l].Cost < _items[smallest].Cost) smallest = l;
                    if (r < _items.Count && _items[r].Cost < _items[smallest].Cost) smallest = r;
                    if (smallest == i) break;
                    (_items[smallest], _items[i]) = (_items[i], _items[smallest]);
                    i = smallest;
                }
                return top;
            }
        }
    }
}
