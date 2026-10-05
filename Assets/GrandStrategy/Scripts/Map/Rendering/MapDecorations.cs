using System;
using System.Collections.Generic;
using GrandStrategy.CameraControl;
using GrandStrategy.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace GrandStrategy.Map.Rendering
{
    /// <summary>
    /// Elementos del mapa con calidad configurable (Ajustes → desactivado / bajo / medio / alto):
    /// <list type="bullet">
    /// <item>Bosques: árboles donde la imagen de satélite muestra bosque. Alto = tarjetas de hojas con LOD
    /// (de lejos se usa el árbol medio) + hierba y arbustos alrededor de la cámara.</item>
    /// <item>Ciudades: Alto = estilos arquitectónicos por región (como Victoria 3), manzanas con patio, parques,
    /// calles, puertos con muelles, grúas y barcos; además de los monumentos.</item>
    /// <item>Caminos y ferrocarriles entre ciudades.</item>
    /// <item>Ríos y nubes (que se mueven con el reloj del juego).</item>
    /// <item>Terreno: publica la calidad y la máscara de uso del suelo (campos de cultivo, suelo urbano).</item>
    /// </list>
    /// </summary>
    public sealed class MapDecorations : MonoBehaviour
    {
        [Header("Bosques")]
        public int ForestStep = 6;
        public float TreeHeight = 0.036f;
        [Tooltip("Distancia (unidades de mundo) a partir de la cual los árboles de calidad alta pasan al modelo medio.")]
        public float TreeLodDistance = 2.6f;

        [Header("Nubes")]
        public float CloudHeight = 9f;

        /// <summary>Multiplicador de velocidad de las nubes (lo da el reloj del juego; 0 = quietas).</summary>
        public Func<float> CloudSpeed = () => 1f;
        /// <summary>Cámara para la que se dibuja (null = Camera.main).</summary>
        public Camera TargetCamera;

        private const float ForestHideZoom = 0.32f;
        private const float CityHideZoom = 0.38f;
        private const float RiverHideZoom = 0.8f;
        private const float GroundCoverZoom = 0.09f;
        private const float GroundCoverCell = 0.35f;
        private const int GroundCoverRadius = 3;

        private MapRenderer _map;
        private WorldData _world;
        private StrategyCamera _cameraRig;
        private Material _treeMat, _foliageMat, _groundCoverMat, _cityMat, _riverMat, _roadMat, _railMat, _streetMat;
        private readonly List<Material> _cloudMats = new List<Material>();
        private Texture2D _foliageAtlas, _groundDetail, _landUse;
        private InstancedLayer _trees, _parkTrees, _buildings, _groundCover;
        private GameObject _rivers, _roads, _streets, _clouds;
        private DetailLevel? _builtForests, _builtCities, _builtRivers, _builtClouds, _builtRoads;
        private readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
        private readonly List<UnityEngine.Object> _transient = new List<UnityEngine.Object>();
        private readonly Plane[] _frustum = new Plane[6];
        private readonly Dictionary<Vector2Int, (List<Matrix4x4> grass, List<Matrix4x4> bush)> _coverCells =
            new Dictionary<Vector2Int, (List<Matrix4x4>, List<Matrix4x4>)>();
        private Vector2Int _coverCenter = new Vector2Int(int.MinValue, 0);
        private float _cloudTime;
        private bool _dirty = true;

        private List<CityInfo> Cities => _world.Cities.Count > 0 ? _world.Cities : (_capitals ??= CapitalsAsCities());
        private List<CityInfo> _capitals;

        // ================================================================== Construcción y ajustes

        public void Build(MapRenderer map, WorldData world, StrategyCamera cameraRig)
        {
            _map = map;
            _world = world;
            _cameraRig = cameraRig;

            var props = Shader.Find("GrandStrategy/MapProps");
            var foliage = Shader.Find("GrandStrategy/MapFoliage");
            var river = Shader.Find("GrandStrategy/MapRiver");
            var road = Shader.Find("GrandStrategy/MapRoad");
            if (props == null || foliage == null || river == null || road == null || Shader.Find("GrandStrategy/MapClouds") == null)
            {
                Debug.LogError("[MapDecorations] Faltan shaders de decoración; se desactivan.");
                enabled = false;
                return;
            }

            _foliageAtlas = Own(VegetationTextures.CreateFoliageAtlas());
            _groundDetail = Own(VegetationTextures.CreateGroundDetail());
            Shader.SetGlobalTexture("_GS_GroundDetailTex", _groundDetail);
            BuildLandUse();

            _treeMat = Own(new Material(props) { name = "Trees", enableInstancing = true });
            SetFade(_treeMat, ForestHideZoom);
            _treeMat.SetFloat("_ColorVariation", 0.16f);
            _foliageMat = Own(new Material(foliage) { name = "Foliage", enableInstancing = true, mainTexture = _foliageAtlas });
            SetFade(_foliageMat, ForestHideZoom);
            _groundCoverMat = Own(new Material(foliage) { name = "GroundCover", enableInstancing = true, mainTexture = _foliageAtlas });
            SetFade(_groundCoverMat, GroundCoverZoom);
            _groundCoverMat.SetFloat("_Wind", 0.15f);
            _cityMat = Own(new Material(props) { name = "Buildings", enableInstancing = true });
            SetFade(_cityMat, CityHideZoom);
            _cityMat.SetFloat("_WindowDensity", 3.5f);
            _cityMat.SetFloat("_ColorVariation", 0.1f);
            _riverMat = Own(new Material(river) { name = "Rivers" });
            _roadMat = Own(new Material(road) { name = "Roads" });
            _roadMat.SetFloat("_Kind", 0f);
            _roadMat.SetFloat("_FadeStart", 0.35f);
            _roadMat.SetFloat("_FadeEnd", 0.55f);
            _railMat = Own(new Material(road) { name = "Railways" });
            _railMat.SetFloat("_Kind", 1f);
            _railMat.SetFloat("_FadeStart", 0.3f);
            _railMat.SetFloat("_FadeEnd", 0.5f);
            _streetMat = Own(new Material(road) { name = "Streets" });
            _streetMat.SetFloat("_Kind", 2f);
            _streetMat.SetFloat("_FadeStart", CityHideZoom - 0.08f);
            _streetMat.SetFloat("_FadeEnd", CityHideZoom);
            _streetMat.SetFloat("_Widen", 1.5f);

            GameSettings.Changed += MarkDirty;
            ApplySettings();
        }

        private static void SetFade(Material m, float hideZoom)
        {
            m.SetFloat("_FadeStart", hideZoom - 0.1f);
            m.SetFloat("_FadeEnd", hideZoom);
        }

        private void MarkDirty() => _dirty = true;

        private T Own<T>(T obj) where T : UnityEngine.Object
        {
            _owned.Add(obj);
            return obj;
        }

        private void OnDestroy()
        {
            GameSettings.Changed -= MarkDirty;
            foreach (var o in _owned)
                if (o != null) Destroy(o);
            foreach (var o in _transient)
                if (o != null) Destroy(o);
            foreach (var m in _cloudMats)
                if (m != null) Destroy(m);
            Shader.SetGlobalVector("_GS_Clouds", Vector4.zero);
        }

        private static void DestroyNow(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        /// <summary>Reconstruye solo las capas cuya calidad ha cambiado.</summary>
        public void ApplySettings()
        {
            _dirty = false;
            if (_map == null) return;

            Shader.SetGlobalFloat("_GS_TerrainQuality", (float)GameSettings.Terrain);

            var forests = GameSettings.Forests;
            if (_builtForests != forests)
            {
                _trees = forests == DetailLevel.Off ? null : BuildForests(forests);
                _treeMat.SetFloat("_Wind", forests >= DetailLevel.Medium ? 0.05f : 0f);
                _groundCover = null;
                _coverCenter = new Vector2Int(int.MinValue, 0);
                _builtForests = forests;
            }

            var cities = GameSettings.Cities;
            if (_builtCities != cities)
            {
                if (_streets != null) DestroyNow(_streets);
                _streets = null;
                _parkTrees = null;
                _buildings = cities == DetailLevel.Off ? null : BuildCities(cities);
                _cityMat.SetFloat("_FacadeDetail", cities == DetailLevel.High ? 1f : 0f);
                _builtCities = cities;
            }

            var roads = GameSettings.Roads;
            if (_builtRoads != roads)
            {
                if (_roads != null) DestroyNow(_roads);
                _roads = roads == DetailLevel.Off ? null : BuildRoads(roads);
                _builtRoads = roads;
            }

            var rivers = GameSettings.Rivers;
            if (_builtRivers != rivers)
            {
                if (_rivers != null) DestroyNow(_rivers);
                _rivers = rivers == DetailLevel.Off ? null : BuildRivers(rivers);
                _riverMat.SetFloat("_Detail", rivers >= DetailLevel.Medium ? 1f : 0f);
                _builtRivers = rivers;
            }

            var clouds = GameSettings.Clouds;
            if (_builtClouds != clouds)
            {
                if (_clouds != null) DestroyNow(_clouds);
                _clouds = clouds == DetailLevel.Off ? null : BuildClouds(clouds);
                _builtClouds = clouds;
            }
        }

        // ================================================================== Cada fotograma

        private void LateUpdate()
        {
            if (_dirty) ApplySettings();
            _cloudTime += Time.unscaledDeltaTime * Mathf.Max(0f, CloudSpeed());
            Shader.SetGlobalFloat("_GS_CloudTime", _cloudTime);
            ApplyVisibility();
            RenderInstanced(TargetCamera != null ? TargetCamera : Camera.main);
        }

        /// <summary>Muestra u oculta las capas según el zoom. forcedCloudVisibility: solo para pruebas.</summary>
        public void ApplyVisibility(float? forcedCloudVisibility = null)
        {
            if (_map == null) return;
            float zoom = _map.Zoom01;
            SetActive(_rivers, zoom < RiverHideZoom);
            SetActive(_roads, zoom < 0.56f);
            SetActive(_streets, zoom < CityHideZoom);

            float vis = 0f;
            if (_clouds != null && _cameraRig != null)
                vis = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(14f, 34f, _cameraRig.Height)) * (1f - 0.35f * Mathf.InverseLerp(0.85f, 1f, zoom));
            if (forcedCloudVisibility.HasValue && _clouds != null) vis = forcedCloudVisibility.Value;
            SetActive(_clouds, vis > 0.001f);
            float shadows = _builtClouds >= DetailLevel.Medium ? 0.22f : 0f;
            Shader.SetGlobalVector("_GS_Clouds", new Vector4(vis, CloudHeight, shadows, 0));
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }

        /// <summary>Envía a la GPU los árboles, edificios y vegetación visibles para esta cámara.</summary>
        public void RenderInstanced(Camera cam)
        {
            if (cam == null || _map == null) return;
            float zoom = _map.Zoom01;
            bool trees = _trees != null && zoom < ForestHideZoom;
            bool buildings = _buildings != null && zoom < CityHideZoom;
            UpdateGroundCover(cam, zoom);
            bool cover = _groundCover != null && zoom < GroundCoverZoom;
            if (!trees && !buildings && !cover) return;

            GeometryUtility.CalculateFrustumPlanes(cam, _frustum);
            float w = _map.WorldSize.x;
            bool wrap = _map.WrapAround;
            if (trees)
            {
                if (_builtForests == DetailLevel.High) _trees.Render(_foliageMat, cam, w, _frustum, wrap, _treeMat, TreeLodDistance);
                else _trees.Render(_treeMat, cam, w, _frustum, wrap);
            }
            if (buildings)
            {
                _buildings.Render(_cityMat, cam, w, _frustum, wrap);
                _parkTrees?.Render(_foliageMat, cam, w, _frustum, wrap);
            }
            if (cover) _groundCover.Render(_groundCoverMat, cam, w, _frustum, wrap);
        }

        // ================================================================== Bosques

        private InstancedLayer BuildForests(DetailLevel level)
        {
            var map = _world.Map;
            int w = map.Width, h = map.Height;
            bool high = level == DetailLevel.High;
            int chunksX = high ? 64 : 24, chunksZ = high ? 32 : 12; // zonas más pequeñas = LOD más fino
            var lists = new List<Matrix4x4>[chunksX * chunksZ * 3];
            var rng = new System.Random(1936);
            float density = level == DetailLevel.Low ? 0.4f : 0.75f;
            int maxTrees = level == DetailLevel.Low ? 30000 : high ? 120000 : 75000;
            int count = 0;
            int step = Mathf.Max(2, ForestStep);

            for (int y = step / 2; y < h && count < maxTrees; y += step)
            {
                float lat = map.LatitudeAt((y + 0.5f) / h);
                for (int x = step / 2; x < w && count < maxTrees; x += step)
                {
                    int i = y * w + x;
                    var province = _world.GetProvince(map.ProvinceIds[i]);
                    if (province == null || province.IsWater) continue;
                    float landE = (map.Heights[i] - map.SeaLevel) / (1f - map.SeaLevel);
                    if (landE > 0.6f) continue;
                    var c = map.ColorMap[i];
                    float bright = (c.r + c.g + c.b) / 765f;
                    bool forest = c.g > c.r * 1.06f && c.g > c.b * 1.08f && bright < 0.36f && bright > 0.05f;
                    if (!forest || rng.NextDouble() > density) continue;

                    // Calidad alta: bosquetes más tupidos.
                    int n = high ? 2 + rng.Next(4) : 1 + rng.Next(level == DetailLevel.Low ? 2 : 3);
                    for (int k = 0; k < n; k++)
                    {
                        float px = x + (float)(rng.NextDouble() - 0.5) * step;
                        float py = y + (float)(rng.NextDouble() - 0.5) * step;
                        var uv = new Vector2(Mathf.Repeat(px / w, 1f), Mathf.Clamp01(py / h));
                        var kind = Mathf.Abs(lat) > 47f || landE > 0.35f ? TreeKind.Conifer
                            : Mathf.Abs(lat) < 16f ? TreeKind.Tropical : TreeKind.Broadleaf;
                        float size = TreeHeight * (0.7f + (float)rng.NextDouble() * 0.6f) * (kind == TreeKind.Tropical ? 1.15f : 1f);
                        var m = Matrix4x4.TRS(new Vector3(uv.x * _map.WorldSize.x, 0, uv.y * _map.WorldSize.y),
                            Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), Vector3.one * size);
                        int chunk = Mathf.Min(chunksX - 1, (int)(uv.x * chunksX)) + Mathf.Min(chunksZ - 1, (int)(uv.y * chunksZ)) * chunksX;
                        (lists[chunk * 3 + (int)kind] ??= new List<Matrix4x4>()).Add(m);
                        count++;
                    }
                }
            }

            var layer = new InstancedLayer();
            for (int i = 0; i < lists.Length; i++)
            {
                if (lists[i] == null) continue;
                var kind = (TreeKind)(i % 3);
                if (high) layer.Add(PropMeshes.Tree(kind, DetailLevel.High), lists[i], PropMeshes.Tree(kind, DetailLevel.Medium));
                else layer.Add(PropMeshes.Tree(kind, level), lists[i]);
            }
            Debug.Log($"[MapDecorations] {count} árboles (calidad {level})");
            return layer;
        }

        /// <summary>Hierba y arbustos (calidad alta): se generan por celdas solo alrededor de la cámara.</summary>
        private void UpdateGroundCover(Camera cam, float zoom)
        {
            if (_builtForests != DetailLevel.High || zoom >= GroundCoverZoom)
            {
                _groundCover = null;
                _coverCenter = new Vector2Int(int.MinValue, 0);
                return;
            }
            var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.45f, 0));
            if (ray.direction.y >= -1e-4f) return;
            var focus = ray.GetPoint(-ray.origin.y / ray.direction.y);
            var center = new Vector2Int(Mathf.FloorToInt(focus.x / GroundCoverCell), Mathf.FloorToInt(focus.z / GroundCoverCell));
            if (center == _coverCenter && _groundCover != null) return;
            _coverCenter = center;

            var layer = new InstancedLayer();
            for (int dy = -GroundCoverRadius; dy <= GroundCoverRadius; dy++)
            for (int dx = -GroundCoverRadius; dx <= GroundCoverRadius; dx++)
            {
                var key = new Vector2Int(center.x + dx, center.y + dy);
                if (!_coverCells.TryGetValue(key, out var cell))
                {
                    cell = GenerateCoverCell(key);
                    if (_coverCells.Count > 400) _coverCells.Clear();
                    _coverCells[key] = cell;
                }
                layer.Add(PropMeshes.Grass(), cell.grass);
                layer.Add(PropMeshes.Bush(), cell.bush);
            }
            _groundCover = layer;
        }

        private (List<Matrix4x4>, List<Matrix4x4>) GenerateCoverCell(Vector2Int key)
        {
            var grass = new List<Matrix4x4>();
            var bush = new List<Matrix4x4>();
            var map = _world.Map;
            var rng = new System.Random(key.x * 73856093 ^ key.y * 19349663);
            for (int s = 0; s < 170; s++)
            {
                var pos = new Vector2((key.x + (float)rng.NextDouble()) * GroundCoverCell, (key.y + (float)rng.NextDouble()) * GroundCoverCell);
                float u = Mathf.Repeat(pos.x / _map.WorldSize.x, 1f), v = pos.y / _map.WorldSize.y;
                if (v <= 0f || v >= 1f) continue;
                // Manchas de pradera: la hierba se agrupa en matas en lugar de salpicar todo el suelo.
                float clump = Mathf.PerlinNoise(pos.x * 9f + 31.7f, pos.y * 9f + 11.3f);
                if (clump < 0.5f) continue;
                int px = Mathf.Clamp((int)(u * map.Width), 0, map.Width - 1), py = Mathf.Clamp((int)(v * map.Height), 0, map.Height - 1);
                int i = py * map.Width + px;
                var province = _world.GetProvince(map.ProvinceIds[i]);
                if (province == null || province.IsWater) continue;
                float landE = (map.Heights[i] - map.SeaLevel) / (1f - map.SeaLevel);
                if (landE > 0.55f) continue;
                var c = map.ColorMap[i];
                float bright = (c.r + c.g + c.b) / 765f;
                bool desert = c.r > c.g * 1.12f && bright > 0.42f;
                if (desert || bright > 0.72f) continue;
                bool greenish = c.g >= c.r * 0.95f;
                if (!greenish && rng.NextDouble() < 0.6) continue;
                bool forestish = c.g > c.r * 1.06f && bright < 0.36f;
                bool isBush = rng.NextDouble() < (forestish ? 0.35 : 0.08);
                float scale = isBush ? 0.009f + (float)rng.NextDouble() * 0.006f : 0.005f + (float)rng.NextDouble() * 0.004f;
                var m = Matrix4x4.TRS(new Vector3(pos.x, 0, pos.y), Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), Vector3.one * scale);
                (isBush ? bush : grass).Add(m);
            }
            return (grass, bush);
        }

        // ================================================================== Ciudades

        private InstancedLayer BuildCities(DetailLevel level)
        {
            var layer = new InstancedLayer();
            var parks = new List<Matrix4x4>();
            var streets = level == DetailLevel.High ? new RibbonBuilder(_map.WorldSize) : null;
            int total = 0, landmarks = 0, harbors = 0;
            var perMesh = new Dictionary<Mesh, List<Matrix4x4>>();

            foreach (var city in Cities)
            {
                perMesh.Clear();
                var rng = new System.Random(city.Name != null ? city.Name.GetHashCode() : total);
                float size = Mathf.Clamp01(Mathf.Sqrt(city.Population / 4e6f));
                var center = new Vector2(city.UV.x * _map.WorldSize.x, city.UV.y * _map.WorldSize.y);
                float reserved = 0f;
                Vector2 landmarkPos = center;

                if (PropMeshes.TryGetLandmark(city.Name, out var landmark, out float lScale, out var lOffset))
                {
                    landmarkPos = center + lOffset;
                    if (IsLand(landmarkPos))
                    {
                        Add(perMesh, landmark, Matrix4x4.TRS(new Vector3(landmarkPos.x, 0, landmarkPos.y), Quaternion.Euler(0, 20f, 0), Vector3.one * lScale * 1.2f));
                        reserved = lScale * 3.6f;
                        landmarks++;
                    }
                }

                switch (level)
                {
                    case DetailLevel.High:
                        total += LayoutCityHigh(city, center, size, rng, perMesh, landmarkPos, reserved, parks, streets);
                        if (BuildHarbor(center, size, rng, perMesh)) harbors++;
                        break;
                    case DetailLevel.Medium:
                        total += LayoutDistrict(city, center, size, rng, perMesh, landmarkPos, reserved);
                        break;
                    default:
                        total += ScatterSimple(city, center, size, rng, perMesh, landmarkPos, reserved);
                        break;
                }
                foreach (var kv in perMesh) layer.Add(kv.Key, kv.Value);
            }

            if (level >= DetailLevel.Medium && HamletsEnabled) total += BuildHamlets(layer);

            if (parks.Count > 0)
            {
                _parkTrees = new InstancedLayer();
                _parkTrees.Add(PropMeshes.Tree(TreeKind.Broadleaf, DetailLevel.High), parks);
            }
            if (streets != null && streets.Count > 0)
                _streets = RibbonObject("Calles", streets.ToMesh("Streets"), _streetMat);
            Debug.Log($"[MapDecorations] {Cities.Count} ciudades, {total} edificios, {landmarks} monumentos, {harbors} puertos (calidad {level})");
            return layer;
        }

        /// <summary>
        /// Estilo arquitectónico según la nación (estilos nacionales de los años 30 para potencias y actores
        /// principales) y, para el resto y las colonias, según la región.
        /// </summary>
        private ArchStyle StyleFor(CityInfo city) => StyleAt(_world.GetCountry(city.OwnerId), city.UV);

        /// <summary>Estilo arquitectónico de un punto del mapa según su país y, si no tiene estilo propio, su región.</summary>
        private ArchStyle StyleAt(Country owner, Vector2 uv)
        {
            string tag = owner != null ? owner.Tag : "";
            switch (tag)
            {
                case "ENG": case "IRE": case "AST": case "NZL":
                    return ArchStyle.British;
                case "FRA": case "BEL": case "LUX":
                    return ArchStyle.French;
                case "GER": case "AUT": case "SWI": case "CZE": case "HUN": case "POL": case "DNZ": case "LIT": case "LAT": case "EST":
                    return ArchStyle.Germanic;
                case "HOL":
                    return ArchStyle.LowCountries;
                case "ITA": case "SPR": case "POR": case "GRE": case "ALB":
                    return ArchStyle.Mediterranean;
                case "YUG": case "ROM": case "BUL":
                    return ArchStyle.Balkan;
                case "SOV":
                    return ArchStyle.Russian;
                case "SWE": case "NOR": case "DEN": case "FIN": case "ICE":
                    return ArchStyle.Nordic;
                case "JAP":
                    return ArchStyle.Japanese;
                case "CHI": case "MAN": case "GXC": case "YUN": case "SHX": case "PRC": case "XSM":
                    return ArchStyle.Chinese;
                case "BRA": case "MEX": case "ARG": case "CHL": case "COL": case "PRU": case "VEN": case "CUB": case "ECU": case "GTM":
                case "BOL": case "HTI": case "DOM": case "HND": case "PRY": case "NIC": case "SLV": case "CRI": case "PAN": case "URY": case "PHI":
                    return ArchStyle.LatinColonial;
                case "SAF":
                    return ArchStyle.CapeDutch;
                case "TUR":
                    return ArchStyle.Ottoman;
                case "TIB": case "BHU":
                    return ArchStyle.Himalayan;
                case "SIA": case "MON":
                    return ArchStyle.EastAsian;
                case "PER": case "IRQ": case "SAU": case "YEM": case "OMA": case "EGY": case "AFG": case "SIK":
                    return ArchStyle.MiddleEastern;
                case "RAJ": case "NEP":
                    return ArchStyle.SouthAsian;
                case "USA": case "CAN":
                    return ArchStyle.American;
            }
            // Colonias: según la región donde está la ciudad.
            float lat = _world.Map.LatitudeAt(uv.y);
            float lon = uv.x * 360f - 180f;
            if (lat > 8f && lat < 38f && lon > -18f && lon < 62f && !(lat > 35f && lon < 28f)) return ArchStyle.MiddleEastern;
            if (lat > 5f && lat < 35f && lon >= 62f && lon < 95f) return ArchStyle.SouthAsian;
            if (lon >= 95f && lon < 150f && lat > -10f) return ArchStyle.EastAsian;
            return ArchStyle.European;
        }

        /// <summary>
        /// Calidad alta: cuadrícula de calles, manzanas con patio en el centro, parques, edificios del estilo regional.
        /// </summary>
        private int LayoutCityHigh(CityInfo city, Vector2 center, float size, System.Random rng, Dictionary<Mesh, List<Matrix4x4>> perMesh,
            Vector2 landmarkPos, float reserved, List<Matrix4x4> parks, RibbonBuilder streets)
        {
            var style = StyleFor(city);
            float radius = 0.055f + size * 0.13f;
            const float block = 0.036f; // edificios y manzanas algo mayores (antes 0.03)
            float angle = (float)rng.NextDouble() * 90f;
            var rot = Quaternion.Euler(0, angle, 0);
            int n = Mathf.CeilToInt(radius / block);
            int count = 0;

            // Calles en los bordes de las manzanas, recortadas al círculo de la ciudad y a tierra firme.
            var run = new List<Vector2>();
            for (int axis = 0; axis < 2; axis++)
            for (int k = -n - 1; k <= n; k++)
            {
                float off = (k + 0.5f) * block;
                float r2 = radius * radius * 0.95f - off * off;
                if (r2 <= 0f) continue;
                float half = Mathf.Sqrt(r2);
                float width = k % 3 == 0 ? 0.0024f : 0.0016f; // avenidas cada tres calles
                run.Clear();
                for (float t = -half; t <= half; t += 0.008f)
                {
                    var local = axis == 0 ? new Vector3(off, 0, t) : new Vector3(t, 0, off);
                    var w3 = rot * local;
                    var p = center + new Vector2(w3.x, w3.z);
                    if (IsLand(p)) run.Add(p);
                    else
                    {
                        if (run.Count > 1) streets.Add(run.ToArray(), width, false, 0.01f, 0);
                        run.Clear();
                    }
                }
                if (run.Count > 1) streets.Add(run.ToArray(), width, false, 0.01f, 0);
            }

            Vector2[] lots = { new Vector2(-0.25f, -0.25f), new Vector2(0.25f, -0.25f), new Vector2(-0.25f, 0.25f), new Vector2(0.25f, 0.25f) };
            for (int gx = -n; gx <= n; gx++)
            for (int gy = -n; gy <= n; gy++)
            {
                var cell = new Vector2(gx, gy) * block;
                float d = cell.magnitude;
                if (d > radius * (0.82f + 0.35f * (float)rng.NextDouble())) continue;
                float r = d / radius;
                var c3 = rot * new Vector3(cell.x, 0, cell.y);
                var cellPos = center + new Vector2(c3.x, c3.z);
                if (reserved > 0 && Vector2.Distance(cellPos, landmarkPos) < reserved) continue;
                if (!IsLand(cellPos)) continue;

                // Parque con árboles.
                if (r > 0.2f && r < 0.75f && rng.NextDouble() < 0.07)
                {
                    for (int t = 0; t < 6; t++)
                    {
                        var lp = rot * new Vector3((float)(rng.NextDouble() - 0.5) * block * 0.75f, 0, (float)(rng.NextDouble() - 0.5) * block * 0.75f);
                        parks.Add(Matrix4x4.TRS(new Vector3(cellPos.x + lp.x, 0, cellPos.y + lp.z), Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0),
                            Vector3.one * (0.022f + (float)rng.NextDouble() * 0.008f)));
                    }
                    continue;
                }

                if (UsesCityBlocks(style) && r < 0.55f && size > 0.2f && rng.NextDouble() < 0.72)
                {
                    // Manzana completa con patio interior; más alta cuanto más céntrica.
                    float s = block * 0.8f;
                    float hScale = 1f + (1f - r) * 0.45f * size;
                    Add(perMesh, PropMeshes.StyledBuilding(BuildingKind.PerimeterBlock, rng.Next(4), style),
                        Matrix4x4.TRS(new Vector3(cellPos.x, 0, cellPos.y), Quaternion.Euler(0, angle, 0), new Vector3(s, s * hScale, s)));
                    count++;
                    continue;
                }

                float fill = Mathf.Lerp(1f, 0.55f, r);
                foreach (var lot in lots)
                {
                    if (rng.NextDouble() > fill) continue;
                    var l3 = rot * new Vector3(cell.x + lot.x * block, 0, cell.y + lot.y * block);
                    var pos = center + new Vector2(l3.x, l3.z);
                    if (!IsLand(pos)) continue;
                    var kind = PickKind(style, r, size, city.Population, rng.NextDouble());
                    float scale = block * 0.4f * (0.85f + (float)rng.NextDouble() * 0.15f) * KindScale(kind);
                    var q = Quaternion.Euler(0, angle + (rng.NextDouble() < 0.5 ? 0f : 180f), 0);
                    Add(perMesh, PropMeshes.StyledBuilding(kind, rng.Next(4), style), Matrix4x4.TRS(new Vector3(pos.x, 0, pos.y), q, Vector3.one * scale));
                    count++;
                }
            }
            return count;
        }

        /// <summary>Estilos cuyos centros urbanos se forman con manzanas cerradas con patio (ensanches, bulevares).</summary>
        private static bool UsesCityBlocks(ArchStyle style) =>
            style == ArchStyle.European || style == ArchStyle.American || style == ArchStyle.French || style == ArchStyle.Germanic ||
            style == ArchStyle.Mediterranean || style == ArchStyle.Russian || style == ArchStyle.LatinColonial || style == ArchStyle.Nordic;

        /// <summary>Tamaño relativo de cada arquetipo en su parcela (templos y fábricas algo mayores, torii menor).</summary>
        private static float KindScale(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.Factory: case BuildingKind.Church: case BuildingKind.Mosque:
                case BuildingKind.GothicChurch: case BuildingKind.TowerChurch: case BuildingKind.DomedChurch: case BuildingKind.OnionChurch:
                case BuildingKind.NordicChurch: case BuildingKind.BaroqueChurch: case BuildingKind.OttomanMosque: case BuildingKind.ChineseTemple:
                case BuildingKind.ByzantineChurch: case BuildingKind.HinduTemple: case BuildingKind.Campanile:
                    return 1.15f;
                case BuildingKind.FrenchCathedral: case BuildingKind.IndoSaracenic:
                    return 1.3f;
                case BuildingKind.Torii: case BuildingKind.Stupa:
                    return 0.8f;
                default:
                    return 1f;
            }
        }

        /// <summary>
        /// Arquetipo de cada parcela según el estilo, la distancia al centro (r: 0 centro, 1 borde), el tamaño de la
        /// ciudad y una tirada aleatoria. Monumentos religiosos escasos, fábricas en la periferia de las grandes
        /// ciudades, edificios altos en los centros de las metrópolis y vivienda típica en el resto.
        /// </summary>
        private static BuildingKind PickKind(ArchStyle style, float r, float size, int population, double roll)
        {
            bool periphery = r > 0.6f && population > 400000;
            bool core = r < 0.3f && size > 0.5f;
            switch (style)
            {
                case ArchStyle.British:
                    if (roll < 0.03) return BuildingKind.TowerChurch;
                    if (periphery && roll < 0.22) return BuildingKind.Factory; // fábricas y chimeneas de ladrillo
                    if (core && roll < 0.2) return roll < 0.1 ? BuildingKind.Civic : BuildingKind.Apartment;
                    if (r < 0.75f) return roll < 0.8 ? BuildingKind.BrickTerrace : BuildingKind.House;
                    return roll < 0.45 ? BuildingKind.BrickTerrace : BuildingKind.House;
                case ArchStyle.French:
                    if (roll < 0.012) return BuildingKind.FrenchCathedral;
                    if (roll < 0.035) return BuildingKind.Church;
                    if (periphery && roll < 0.2) return BuildingKind.Factory;
                    if (r < 0.6f) return roll < 0.75 ? BuildingKind.FrenchHouse : BuildingKind.Townhouses;
                    return roll < 0.4 ? BuildingKind.FrenchHouse : BuildingKind.House;
                case ArchStyle.Germanic:
                    if (roll < 0.035) return BuildingKind.GothicChurch;
                    if (periphery && roll < 0.22) return BuildingKind.Factory;
                    if (core && roll < 0.3) return BuildingKind.Apartment;
                    return roll < 0.7 ? BuildingKind.HalfTimber : BuildingKind.Townhouses;
                case ArchStyle.LowCountries:
                    if (roll < 0.03) return BuildingKind.GothicChurch;
                    if (periphery && roll < 0.18) return BuildingKind.Factory;
                    if (r < 0.7f) return roll < 0.82 ? BuildingKind.DutchGable : BuildingKind.Townhouses;
                    return roll < 0.5 ? BuildingKind.DutchGable : BuildingKind.House;
                case ArchStyle.Mediterranean:
                    if (roll < 0.02) return BuildingKind.DomedChurch;
                    if (roll < 0.04) return BuildingKind.Campanile;
                    if (periphery && roll < 0.16) return BuildingKind.Factory;
                    if (core && roll < 0.2) return BuildingKind.Apartment;
                    return BuildingKind.MedHouse;
                case ArchStyle.Balkan:
                    if (roll < 0.025) return BuildingKind.ByzantineChurch;
                    if (roll < 0.04) return BuildingKind.Mosque;
                    if (periphery && roll < 0.14) return BuildingKind.Factory;
                    return roll < 0.45 ? BuildingKind.OttomanHouse : BuildingKind.MedHouse;
                case ArchStyle.Russian:
                    if (roll < 0.03) return BuildingKind.OnionChurch;
                    if (periphery && roll < 0.24) return BuildingKind.Factory; // industrialización de los planes quinquenales
                    if (r < 0.5f && size > 0.35f && roll < 0.5) return roll < 0.3 ? BuildingKind.SovietBlock : BuildingKind.Apartment;
                    return BuildingKind.Izba;
                case ArchStyle.Nordic:
                    if (roll < 0.03) return BuildingKind.NordicChurch;
                    if (periphery && roll < 0.14) return BuildingKind.Factory;
                    if (core && roll < 0.35) return BuildingKind.Apartment;
                    return BuildingKind.NordicHouse;
                case ArchStyle.Japanese:
                    if (roll < 0.025) return BuildingKind.AsianTemple; // pagoda
                    if (roll < 0.04) return BuildingKind.Torii;
                    if (periphery && roll < 0.2) return BuildingKind.Factory;
                    if (r < 0.25f && size > 0.6f && roll < 0.22) return BuildingKind.Apartment; // edificios modernos de Tokio y Osaka
                    return BuildingKind.Machiya;
                case ArchStyle.Chinese:
                    if (roll < 0.02) return BuildingKind.ChineseTemple;
                    if (roll < 0.03) return BuildingKind.BrickPagoda;
                    if (periphery && roll < 0.16) return BuildingKind.Factory;
                    if (r < 0.25f && size > 0.7f && roll < 0.25) return BuildingKind.Apartment; // concesiones de Shanghái
                    return roll < 0.4 ? BuildingKind.Siheyuan : BuildingKind.AsianHouse;
                case ArchStyle.LatinColonial:
                    if (roll < 0.03) return BuildingKind.BaroqueChurch;
                    if (periphery && roll < 0.14) return BuildingKind.Factory;
                    if (r < 0.25f && size > 0.6f && roll < 0.25) return roll < 0.1 ? BuildingKind.DecoTower : BuildingKind.Apartment;
                    return BuildingKind.ColonialHouse;
                case ArchStyle.CapeDutch:
                    if (roll < 0.025) return BuildingKind.NordicChurch; // iglesia reformada blanca
                    if (periphery && roll < 0.18) return BuildingKind.Factory; // minería e industria del Rand
                    if (core && roll < 0.25) return roll < 0.1 ? BuildingKind.Civic : BuildingKind.Apartment;
                    return roll < 0.35 ? BuildingKind.CapeDutch : BuildingKind.Clapboard;
                case ArchStyle.Ottoman:
                    if (roll < 0.045) return BuildingKind.OttomanMosque;
                    if (periphery && roll < 0.14) return BuildingKind.Factory;
                    return roll < 0.55 ? BuildingKind.OttomanHouse : BuildingKind.FlatHouse;
                case ArchStyle.Himalayan:
                    if (roll < 0.05) return BuildingKind.Stupa;
                    return BuildingKind.HimalayanHouse;
                case ArchStyle.American:
                    if (roll < 0.03) return BuildingKind.Church;
                    if (periphery && roll < 0.2) return BuildingKind.Factory;
                    if (r < 0.3f && size > 0.55f && roll < 0.5) return roll < 0.3 ? BuildingKind.DecoTower : BuildingKind.Tower;
                    if (r < 0.45f) return roll < 0.6 ? BuildingKind.Apartment : BuildingKind.Townhouses;
                    return BuildingKind.Clapboard; // barrios de casas de madera
                case ArchStyle.SouthAsian:
                    if (roll < 0.03) return BuildingKind.Mosque;
                    if (roll < 0.055) return BuildingKind.HinduTemple;
                    if (r < 0.3f && size > 0.45f && roll < 0.12) return BuildingKind.IndoSaracenic; // Bombay, Calcuta, Nueva Delhi
                    if (r > 0.65f && roll < 0.14) return BuildingKind.Factory;
                    return BuildingKind.FlatHouse;
            }
            switch (style)
            {
                case ArchStyle.EastAsian:
                    if (roll < 0.04) return BuildingKind.AsianTemple;
                    if (r > 0.6f && population > 400000 && roll < 0.18) return BuildingKind.Factory;
                    if (r < 0.3f && size > 0.5f && roll < 0.22) return BuildingKind.Apartment;
                    return BuildingKind.AsianHouse;
                case ArchStyle.MiddleEastern:
                    if (roll < 0.05) return BuildingKind.Mosque;
                    if (roll < 0.3) return BuildingKind.CourtyardHouse;
                    if (r > 0.65f && roll < 0.38) return BuildingKind.Factory;
                    return BuildingKind.FlatHouse;
                case ArchStyle.SouthAsian:
                    if (roll < 0.05) return BuildingKind.Mosque;
                    if (r > 0.65f && roll < 0.14) return BuildingKind.Factory;
                    return BuildingKind.FlatHouse;
                default:
                    if (roll < 0.035) return BuildingKind.Church;
                    if (r > 0.6f && population > 400000 && roll < 0.2) return BuildingKind.Factory;
                    if (style == ArchStyle.American && r < 0.3f && size > 0.55f && roll < 0.5) return BuildingKind.Tower;
                    if (r < 0.22f && size > 0.7f && roll < 0.3) return BuildingKind.Tower;
                    if (r < 0.3f && roll < 0.08) return BuildingKind.Civic;
                    if (r < 0.5f) return roll < 0.6 ? BuildingKind.Apartment : BuildingKind.Townhouses;
                    if (r < 0.75f) return roll < 0.5 ? BuildingKind.Townhouses : BuildingKind.House;
                    return BuildingKind.House;
            }
        }

        // ================================================================== Aldeas bajo las luces nocturnas

        /// <summary>Aldeas activadas (se puede desactivar para comparar rendimiento).</summary>
        private static bool HamletsEnabled = true;

        /// <summary>Lado de la celda (píxeles del mapa) en la que se busca un punto de luz para poner una aldea.</summary>
        private const int HamletCell = 6;
        /// <summary>Brillo mínimo (0..255) de las luces nocturnas para que haya aldea (los puntos claramente visibles).</summary>
        private const byte HamletMinLight = 77;   // 0.30
        /// <summary>A partir de este brillo la aldea tiene 3 casas en vez de 2.</summary>
        private const byte HamletBigLight = 128;  // 0.50
        /// <summary>Tamaño de las zonas en que se agrupan las aldeas para dibujarlas (unidades de mundo).</summary>
        private const float HamletTile = 8f;

        /// <summary>
        /// Pequeñas aldeas (2–3 casas del estilo del país) donde las luces nocturnas marcan población pero no hay
        /// ninguna ciudad del juego, para que de noche esas luces correspondan a edificios. Se agrupan por zonas del
        /// mapa (una tanda de dibujo por zona y tipo de casa), no por aldea, para no multiplicar las llamadas de
        /// dibujo; se calculan una sola vez, al construir las decoraciones.
        /// </summary>
        private int BuildHamlets(InstancedLayer layer)
        {
            var map = _world.Map;
            if (map.CityLights == null) return 0;
            int w = map.Width, h = map.Height;
            int cw = w / HamletCell, ch = h / HamletCell;
            float worldPerPixel = _map.WorldSize.x / w;

            // Celdas ocupadas por las ciudades del juego (su radio más un margen): ahí ya hay edificios.
            var taken = new bool[cw * ch];
            foreach (var city in Cities)
            {
                float size = Mathf.Clamp01(Mathf.Sqrt(city.Population / 4e6f));
                float r = (0.055f + size * 0.13f + 0.06f) / worldPerPixel / HamletCell;
                float cx = Mathf.Repeat(city.UV.x, 1f) * cw, cy = city.UV.y * ch;
                for (int y = Mathf.Max(0, (int)(cy - r)); y <= Mathf.Min(ch - 1, (int)(cy + r)); y++)
                for (int x = (int)(cx - r); x <= (int)(cx + r); x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    if (dx * dx + dy * dy <= r * r) taken[y * cw + ((x % cw) + cw) % cw] = true;
                }
            }

            var perTile = new Dictionary<(int, int, Mesh), List<Matrix4x4>>();
            var rng = new System.Random(1936);
            int houses = 0;
            for (int cy = 0; cy < ch; cy++)
            for (int cx = 0; cx < cw; cx++)
            {
                if (taken[cy * cw + cx]) continue;
                // Píxel más brillante de la celda.
                byte best = 0;
                int bx = 0, by = 0;
                for (int y = cy * HamletCell; y < (cy + 1) * HamletCell; y++)
                for (int x = cx * HamletCell; x < (cx + 1) * HamletCell; x++)
                {
                    byte l = map.CityLights[y * w + x];
                    if (l > best) { best = l; bx = x; by = y; }
                }
                if (best < HamletMinLight) continue;
                var province = _world.GetProvince(map.ProvinceIds[by * w + bx]);
                if (province == null || province.IsWater) continue;

                var uv = new Vector2((bx + 0.5f) / w, (by + 0.5f) / h);
                var style = StyleAt(_world.GetOwner(province), uv);
                var center = new Vector2(uv.x * _map.WorldSize.x, uv.y * _map.WorldSize.y);
                int count = best >= HamletBigLight ? 3 : 2;
                float angle = (float)rng.NextDouble() * 90f;
                var tile = ((int)(center.x / HamletTile), (int)(center.y / HamletTile));
                for (int i = 0; i < count; i++)
                {
                    // Casas juntas, como un caserío: alrededor del punto de luz, con la misma orientación aproximada.
                    float a = (i / (float)count + (float)rng.NextDouble() * 0.2f) * Mathf.PI * 2f;
                    float d = 0.008f + (float)rng.NextDouble() * 0.012f;
                    var pos = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                    if (!IsLand(pos)) continue;
                    var mesh = PropMeshes.StyledBuilding(HamletKind(style, rng.NextDouble()), rng.Next(4), style);
                    float scale = 0.0125f * (0.85f + (float)rng.NextDouble() * 0.2f);
                    var q = Quaternion.Euler(0, angle + (float)rng.NextDouble() * 30f - 15f + (rng.NextDouble() < 0.5 ? 0f : 90f), 0);
                    var key = (tile.Item1, tile.Item2, mesh);
                    if (!perTile.TryGetValue(key, out var list)) perTile[key] = list = new List<Matrix4x4>();
                    list.Add(Matrix4x4.TRS(new Vector3(pos.x, 0, pos.y), q, Vector3.one * scale));
                    houses++;
                }
            }
            foreach (var kv in perTile) layer.Add(kv.Key.Item3, kv.Value);
            Debug.Log($"[MapDecorations] {houses} casas de aldea en {perTile.Count} grupos de dibujo");
            return houses;
        }

        /// <summary>Vivienda rural típica de cada estilo (la más reconocible de cada país).</summary>
        private static BuildingKind HamletKind(ArchStyle style, double roll)
        {
            switch (style)
            {
                case ArchStyle.British: return roll < 0.7 ? BuildingKind.House : BuildingKind.BrickTerrace;
                case ArchStyle.French: return roll < 0.6 ? BuildingKind.House : BuildingKind.FrenchHouse;
                case ArchStyle.Germanic: return roll < 0.75 ? BuildingKind.HalfTimber : BuildingKind.House;
                case ArchStyle.LowCountries: return roll < 0.6 ? BuildingKind.House : BuildingKind.DutchGable;
                case ArchStyle.Mediterranean: return BuildingKind.MedHouse;
                case ArchStyle.Balkan: return roll < 0.5 ? BuildingKind.OttomanHouse : BuildingKind.MedHouse;
                case ArchStyle.Russian: return BuildingKind.Izba;
                case ArchStyle.Nordic: return BuildingKind.NordicHouse;
                case ArchStyle.Japanese: return BuildingKind.Machiya;
                case ArchStyle.Chinese: return roll < 0.75 ? BuildingKind.AsianHouse : BuildingKind.Siheyuan;
                case ArchStyle.EastAsian: return BuildingKind.AsianHouse;
                case ArchStyle.LatinColonial: return BuildingKind.ColonialHouse;
                case ArchStyle.CapeDutch: return roll < 0.5 ? BuildingKind.CapeDutch : BuildingKind.Clapboard;
                case ArchStyle.Ottoman: return roll < 0.6 ? BuildingKind.OttomanHouse : BuildingKind.FlatHouse;
                case ArchStyle.Himalayan: return BuildingKind.HimalayanHouse;
                case ArchStyle.American: return BuildingKind.Clapboard;
                case ArchStyle.MiddleEastern: return roll < 0.7 ? BuildingKind.FlatHouse : BuildingKind.CourtyardHouse;
                case ArchStyle.SouthAsian: return BuildingKind.FlatHouse;
                default: return BuildingKind.House;
            }
        }

        /// <summary>Puerto para ciudades costeras: muelles, almacenes, grúas y barcos fondeados.</summary>
        private bool BuildHarbor(Vector2 center, float size, System.Random rng, Dictionary<Mesh, List<Matrix4x4>> perMesh)
        {
            float radius = 0.055f + size * 0.13f;
            float best = float.MaxValue;
            Vector2 dir = Vector2.zero;
            for (int a = 0; a < 24; a++)
            {
                float ang = a * Mathf.PI * 2f / 24f;
                var d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                for (float r = 0.01f; r < radius + 0.05f; r += 0.006f)
                {
                    if (IsLand(center + d * r)) continue;
                    if (r < best) { best = r; dir = d; }
                    break;
                }
            }
            if (best == float.MaxValue) return false;

            var perp = new Vector2(-dir.y, dir.x);
            var look = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.y));
            var along = Quaternion.LookRotation(new Vector3(perp.x, 0, perp.y));
            int piers = 2 + (int)(size * 3);
            for (int i = 0; i < piers; i++)
            {
                float off = (i - (piers - 1) * 0.5f) * 0.022f;
                var p = center + dir * (best + 0.018f) + perp * off;
                Add(perMesh, PropMeshes.StyledBuilding(BuildingKind.Pier, 0, ArchStyle.European), Matrix4x4.TRS(new Vector3(p.x, 0, p.y), look, Vector3.one * 0.02f));
                var wpos = center + dir * (best - 0.012f) + perp * off;
                if (IsLand(wpos) && i % 2 == 0)
                    Add(perMesh, PropMeshes.StyledBuilding(BuildingKind.Warehouse, i % 4, ArchStyle.European), Matrix4x4.TRS(new Vector3(wpos.x, 0, wpos.y), along, Vector3.one * 0.016f));
                if (i % 2 == 1)
                {
                    var cpos = center + dir * (best + 0.004f) + perp * off;
                    Add(perMesh, PropMeshes.StyledBuilding(BuildingKind.Crane, 0, ArchStyle.European), Matrix4x4.TRS(new Vector3(cpos.x, 0, cpos.y), look, Vector3.one * 0.018f));
                }
            }
            int ships = 1 + (int)(size * 3);
            for (int i = 0; i < ships; i++)
            {
                var p = center + dir * (best + 0.06f + (float)rng.NextDouble() * 0.06f) + perp * ((float)rng.NextDouble() - 0.5f) * 0.16f;
                if (IsLand(p)) continue;
                var q = Quaternion.LookRotation(new Vector3(perp.x, 0, perp.y)) * Quaternion.Euler(0, (float)rng.NextDouble() * 40f - 20f, 0);
                Add(perMesh, PropMeshes.StyledBuilding(BuildingKind.Ship, rng.Next(4), ArchStyle.European), Matrix4x4.TRS(new Vector3(p.x, 0, p.y), q, Vector3.one * 0.03f));
            }
            return true;
        }

        /// <summary>Calidad media: manzanas en cuadrícula con los edificios del estilo de cada país.</summary>
        private int LayoutDistrict(CityInfo city, Vector2 center, float size, System.Random rng,
            Dictionary<Mesh, List<Matrix4x4>> perMesh, Vector2 landmarkPos, float reserved)
        {
            var style = StyleFor(city);
            float radius = 0.05f + size * 0.12f;
            const float block = 0.036f; // edificios y manzanas algo mayores (antes 0.03)
            float angle = (float)rng.NextDouble() * 90f;
            var rot = Quaternion.Euler(0, angle, 0);
            int n = Mathf.CeilToInt(radius / block);
            int count = 0;
            Vector2[] lots = { new Vector2(-0.25f, -0.25f), new Vector2(0.25f, -0.25f), new Vector2(-0.25f, 0.25f), new Vector2(0.25f, 0.25f) };

            for (int gx = -n; gx <= n; gx++)
            for (int gy = -n; gy <= n; gy++)
            {
                var cell = new Vector2(gx, gy) * block;
                float d = cell.magnitude;
                if (d > radius * (0.8f + 0.4f * (float)rng.NextDouble())) continue;
                float r = d / radius;
                float fill = Mathf.Lerp(1f, 0.55f, r);
                foreach (var lot in lots)
                {
                    if (rng.NextDouble() > fill) continue;
                    var off3 = rot * new Vector3(cell.x + lot.x * block, 0, cell.y + lot.y * block);
                    var pos = center + new Vector2(off3.x, off3.z);
                    if (reserved > 0 && Vector2.Distance(pos, landmarkPos) < reserved) continue;
                    if (!IsLand(pos)) continue;
                    var kind = PickKind(style, r, size, city.Population, rng.NextDouble());
                    float scale = block * 0.42f * (0.85f + (float)rng.NextDouble() * 0.15f) * KindScale(kind);
                    var q = Quaternion.Euler(0, angle + (rng.NextDouble() < 0.5 ? 0f : 180f), 0);
                    Add(perMesh, PropMeshes.StyledBuilding(kind, rng.Next(4), style), Matrix4x4.TRS(new Vector3(pos.x, 0, pos.y), q, Vector3.one * scale));
                    count++;
                }
            }
            return count;
        }

        /// <summary>Calidad baja: pocas casas sencillas alrededor del centro.</summary>
        private int ScatterSimple(CityInfo city, Vector2 center, float size, System.Random rng,
            Dictionary<Mesh, List<Matrix4x4>> perMesh, Vector2 landmarkPos, float reserved)
        {
            int count = 6 + (int)(size * 40) + (city.IsCapital ? 6 : 0);
            float radius = 0.05f + size * 0.11f;
            var mesh = PropMeshes.SimpleBuilding();
            int placed = 0;
            for (int k = 0; k < count; k++)
            {
                float r = radius * Mathf.Pow((float)rng.NextDouble(), 0.75f);
                float ang = (float)(rng.NextDouble() * Math.PI * 2);
                var pos = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                if (reserved > 0 && Vector2.Distance(pos, landmarkPos) < reserved) continue;
                if (!IsLand(pos)) continue;
                float central = 1f - r / radius;
                float s = 0.0156f + (float)rng.NextDouble() * 0.0096f;
                var scale = new Vector3(s, s * (0.8f + central * 1.6f), s);
                Add(perMesh, mesh, Matrix4x4.TRS(new Vector3(pos.x, 0, pos.y), Quaternion.Euler(0, (float)rng.NextDouble() * 90f, 0), scale));
                placed++;
            }
            return placed;
        }

        private static void Add(Dictionary<Mesh, List<Matrix4x4>> perMesh, Mesh mesh, Matrix4x4 m)
        {
            if (!perMesh.TryGetValue(mesh, out var list)) perMesh[mesh] = list = new List<Matrix4x4>();
            list.Add(m);
        }

        private bool IsLand(Vector2 worldXZ)
        {
            var map = _world.Map;
            float u = Mathf.Repeat(worldXZ.x / _map.WorldSize.x, 1f), v = worldXZ.y / _map.WorldSize.y;
            if (v < 0f || v >= 1f) return false;
            int px = Mathf.Clamp((int)(u * map.Width), 0, map.Width - 1);
            int py = Mathf.Clamp((int)(v * map.Height), 0, map.Height - 1);
            var province = _world.GetProvince(map.ProvinceIds[py * map.Width + px]);
            return province != null && !province.IsWater;
        }

        /// <summary>Para mapas sin cities.json (procedurales): una ciudad en cada capital.</summary>
        private List<CityInfo> CapitalsAsCities()
        {
            var list = new List<CityInfo>();
            var map = _world.Map;
            foreach (var c in _world.Countries)
            {
                var capital = _world.GetProvince(c.CapitalProvinceId);
                if (capital == null) continue;
                list.Add(new CityInfo
                {
                    Name = capital.Name, OwnerId = c.Id, IsCapital = true, Population = Mathf.Max(capital.Population, 200000),
                    UV = new Vector2((capital.Center.x + 0.5f) / map.Width, (capital.Center.y + 0.5f) / map.Height),
                });
            }
            return list;
        }

        // ================================================================== Uso del suelo (para el terreno)

        /// <summary>
        /// Textura de baja resolución: R = influencia agrícola (campos alrededor de las ciudades y en las llanuras
        /// templadas), G = suelo urbano. El shader del terreno la usa en calidad alta.
        /// </summary>
        private void BuildLandUse()
        {
            const int w = 1024;
            int h = Mathf.Max(1, Mathf.RoundToInt(w * _map.WorldSize.y / _map.WorldSize.x));
            var farm = new float[w * h];
            var urban = new float[w * h];
            for (int y = 0; y < h; y++)
            {
                float lat = Mathf.Abs(_world.Map.LatitudeAt((y + 0.5f) / h));
                float baseFarm = lat > 30f && lat < 54f ? 0.6f : lat > 22f && lat < 57f ? 0.35f : lat < 22f ? 0.15f : 0f;
                for (int x = 0; x < w; x++) farm[y * w + x] = baseFarm;
            }
            float pxPerUnit = w / _map.WorldSize.x;
            foreach (var city in Cities)
            {
                float size = Mathf.Clamp01(Mathf.Sqrt(city.Population / 4e6f));
                float farmR = (0.3f + Mathf.Sqrt(city.Population / 1e6f) * 0.45f) * pxPerUnit;
                float urbanR = (0.055f + size * 0.13f) * 1.1f * pxPerUnit;
                float cx = city.UV.x * w, cy = city.UV.y * h;
                int r = Mathf.CeilToInt(farmR) + 1;
                for (int y = Mathf.Max(0, (int)cy - r); y <= Mathf.Min(h - 1, (int)cy + r); y++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = ((int)cx + dx + w) % w;
                    float d = Vector2.Distance(new Vector2((int)cx + dx + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    int i = y * w + x;
                    farm[i] = Mathf.Max(farm[i], 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(farmR * 0.35f, farmR, d)));
                    urban[i] = Mathf.Max(urban[i], 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(urbanR * 0.6f, urbanR * 1.2f, d)));
                }
            }
            var bytes = new byte[w * h * 2];
            for (int i = 0; i < w * h; i++)
            {
                bytes[i * 2] = (byte)Mathf.Clamp(Mathf.RoundToInt(farm[i] * 255f), 0, 255);
                bytes[i * 2 + 1] = (byte)Mathf.Clamp(Mathf.RoundToInt(urban[i] * 255f), 0, 255);
            }
            _landUse = Own(new Texture2D(w, h, TextureFormat.RG16, false, true)
            {
                name = "LandUse", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            });
            _landUse.SetPixelData(bytes, 0);
            _landUse.Apply(false, true);
            Shader.SetGlobalTexture("_GS_LandUseTex", _landUse);
        }

        // ================================================================== Caminos y ferrocarriles

        private GameObject BuildRoads(DetailLevel level)
        {
            var root = new GameObject("Caminos");
            root.transform.SetParent(transform, false);
            var cities = Cities;
            var roads = new RibbonBuilder(_map.WorldSize);
            var rails = level == DetailLevel.High ? new RibbonBuilder(_map.WorldSize) : null;
            var edges = new HashSet<long>();
            var railEdges = new HashSet<long>();
            var pts = new List<Vector2>();
            var pos = new Vector2[cities.Count];
            for (int i = 0; i < cities.Count; i++) pos[i] = new Vector2(cities[i].UV.x * _map.WorldSize.x, cities[i].UV.y * _map.WorldSize.y);

            void Connect(int a, int b, RibbonBuilder target, HashSet<long> seen, float halfWidth, float meander, float sideOffset)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (!seen.Add(key)) return;
                if (!MeanderPath(pos[a], pos[b], meander, sideOffset, a * 31 + b, pts)) return;
                target.Add(pts.ToArray(), halfWidth, true, 0.02f, 2);
            }

            for (int i = 0; i < cities.Count; i++)
            {
                bool big = cities[i].Population > 500000;
                if (level == DetailLevel.Low && !big) continue;
                int neighbors = level == DetailLevel.Low ? 2 : 3;
                float maxDist = level == DetailLevel.Low ? 5f : 3.5f;
                foreach (int j in Nearest(pos, i, neighbors, maxDist, level == DetailLevel.Low ? 500000 : 0))
                    Connect(i, j, roads, edges, big && cities[j].Population > 500000 ? 0.0034f : 0.0026f, 0.05f, 0f);
                if (rails != null && cities[i].Population > 300000)
                    foreach (int j in Nearest(pos, i, 2, 6f, 300000))
                        Connect(i, j, rails, railEdges, 0.0024f, 0.02f, 0.006f);
            }

            if (roads.Count > 0) RibbonObject("Caminos", roads.ToMesh("Roads"), _roadMat).transform.SetParent(root.transform, false);
            if (rails != null && rails.Count > 0) RibbonObject("Ferrocarriles", rails.ToMesh("Railways"), _railMat).transform.SetParent(root.transform, false);
            Debug.Log($"[MapDecorations] {roads.Count} caminos, {rails?.Count ?? 0} vías de tren (calidad {level})");
            return root;
        }

        private List<int> Nearest(Vector2[] pos, int i, int count, float maxDist, int minPopulation)
        {
            var best = new List<(float d, int j)>();
            var cities = Cities;
            for (int j = 0; j < pos.Length; j++)
            {
                if (j == i || cities[j].Population < minPopulation) continue;
                float dx = Mathf.Abs(pos[j].x - pos[i].x);
                dx = Mathf.Min(dx, _map.WorldSize.x - dx);
                float d = Mathf.Sqrt(dx * dx + (pos[j].y - pos[i].y) * (pos[j].y - pos[i].y));
                if (d <= maxDist) best.Add((d, j));
            }
            best.Sort((a, b) => a.d.CompareTo(b.d));
            var result = new List<int>();
            for (int k = 0; k < best.Count && k < count; k++) result.Add(best[k].j);
            return result;
        }

        /// <summary>Trazado sinuoso entre dos ciudades; false si cruza demasiada agua.</summary>
        private bool MeanderPath(Vector2 a, Vector2 b, float meander, float sideOffset, int seed, List<Vector2> pts)
        {
            pts.Clear();
            var delta = b - a;
            if (Mathf.Abs(delta.x) > _map.WorldSize.x * 0.5f) return false; // no cruzar el antimeridiano
            float len = delta.magnitude;
            if (len < 1e-4f) return false;
            var perp = new Vector2(-delta.y, delta.x) / len;
            int n = Mathf.Max(2, Mathf.CeilToInt(len / 0.05f));
            int water = 0;
            float amp = Mathf.Min(meander, len * 0.08f);
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float wiggle = (Mathf.PerlinNoise(seed * 0.137f, t * len * 2.5f) - 0.5f) * 2f * amp * Mathf.Sin(t * Mathf.PI);
                var p = a + delta * t + perp * (wiggle + sideOffset * Mathf.Sin(t * Mathf.PI));
                if (i > 0 && i < n && !IsLand(p)) water++;
                pts.Add(p);
            }
            return water <= n * 0.06f;
        }

        private GameObject RibbonObject(string name, Mesh mesh, Material material)
        {
            _transient.Add(mesh);
            var root = new GameObject(name);
            root.transform.SetParent(transform, false);
            foreach (float offset in new[] { 0f, -_map.WorldSize.x, _map.WorldSize.x })
            {
                var go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(offset, 0, 0);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            return root;
        }

        // ================================================================== Ríos

        private GameObject BuildRivers(DetailLevel level)
        {
            var builder = new RibbonBuilder(_map.WorldSize);
            var size = _map.WorldSize;
            var pts = new List<Vector2>();
            bool smooth = level >= DetailLevel.Medium;
            foreach (var river in _world.Rivers)
            {
                if (level == DetailLevel.Low && river.Width < 0.5f) continue;
                pts.Clear();
                foreach (var p in river.Points) pts.Add(new Vector2(p.x * size.x, p.y * size.y));
                builder.Add(pts, 0.0075f * (0.6f + river.Width * 0.8f), smooth, smooth ? 0.02f : 0.06f, smooth ? 10 : 4);
            }
            Debug.Log($"[MapDecorations] {builder.Count} tramos de río (calidad {level})");
            return RibbonObject("Ríos", builder.ToMesh("Rivers"), _riverMat);
        }

        // ================================================================== Nubes

        private GameObject BuildClouds(DetailLevel level)
        {
            var root = new GameObject("Nubes");
            root.transform.SetParent(transform, false);
            var shader = Shader.Find("GrandStrategy/MapClouds");
            foreach (var m in _cloudMats) DestroyNow(m);
            _cloudMats.Clear();

            bool rich = level >= DetailLevel.Medium;
            var layers = rich
                ? new[] { (height: CloudHeight, opacity: 0.5f, scale: 1f, coverage: 0f), (height: CloudHeight + 4f, opacity: 0.28f, scale: 2f, coverage: -0.06f) }
                : new[] { (height: CloudHeight, opacity: 0.4f, scale: 1f, coverage: 0f) };
            var size = _map.WorldSize;
            foreach (var l in layers)
            {
                var mat = new Material(shader) { name = "Clouds" };
                mat.SetFloat("_Opacity", l.opacity);
                mat.SetFloat("_Scale", l.scale);
                mat.SetFloat("_Coverage", l.coverage);
                mat.SetFloat("_Detail", rich ? 1f : 0f);
                _cloudMats.Add(mat);

                var mesh = new Mesh { name = "CloudPlane" };
                float x0 = -size.x, x1 = size.x * 2f, z0 = -size.y * 0.1f, z1 = size.y * 1.1f;
                mesh.vertices = new[] { new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), new Vector3(x1, 0, z0) };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                mesh.RecalculateBounds();
                _owned.Add(mesh);

                var go = new GameObject("CapaNubes");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(0, l.height, 0);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            return root;
        }
    }
}
