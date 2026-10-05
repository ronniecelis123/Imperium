using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace GrandStrategy.Map.Rendering
{
    /// <summary>
    /// Construye la malla del mapa y las texturas que consume el shader "GrandStrategy/MapTerrain".
    /// <para>Texturas en GPU:</para>
    /// <list type="bullet">
    /// <item>_HeightTex: relieve (desplaza vértices y genera normales por píxel).</item>
    /// <item>_ColorMap: color pintado del terreno.</item>
    /// <item>_ProvinceIdTex: id de provincia por píxel (RG = 16 bits).</item>
    /// <item>_ProvinceDataTex (256×N): color del map mode + flags de interacción en alfa.</item>
    /// <item>_ProvinceOwnerTex (256×N): id del país dueño (RG) y si es agua (B).</item>
    /// </list>
    /// Cambiar el color de una provincia o resaltarla solo toca un píxel de las texturas 256×N.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapRenderer : MonoBehaviour
    {
        public const string ShaderName = "GrandStrategy/MapTerrain";
        public const int LookupWidth = 256;

        // Flags (canal alfa de _ProvinceDataTex). Deben coincidir con el shader.
        public const byte FlagHoverProvince = 1;
        public const byte FlagHoverCountry = 2;
        public const byte FlagSelectedCountry = 4;
        public const byte FlagSelectedProvince = 8;
        public const byte FlagPlayerCountry = 16;

        private const int ChunkQuads = 128;

        [Header("Dimensiones")]
        [Tooltip("Ancho del mapa en unidades de mundo.")]
        public float WorldWidth = 200f;
        [Tooltip("Altura máxima REAL de la malla (unidades de mundo). Baja a propósito: suelo apto para unidades.")]
        public float MaxTerrainHeight = 1.4f;
        [Tooltip("Altura 'virtual' usada solo para sombrear el relieve (las montañas se ven sin picos).")]
        public float ReliefShadingHeight = 4.5f;
        [Tooltip("Mip del heightmap que usa la malla: más alto = relieve geométrico más suave.")]
        public float VertexHeightMip = 2f;
        [Tooltip("Píxeles del mapa por cada cuadrado de la malla.")]
        [Range(1, 16)] public int VertexStep = 4;

        [Header("Apariencia")]
        public Color ShallowWater = new Color(0.16f, 0.40f, 0.46f);
        public Color DeepWater = new Color(0.03f, 0.10f, 0.19f);
        public Color HighlightColor = new Color(1f, 0.85f, 0.45f);
        [Range(0.5f, 6f)] public float CountryBorderWidth = 2.4f;
        [Range(0.25f, 4f)] public float ProvinceBorderWidth = 1f;

        public WorldData World { get; private set; }
        public Vector2 WorldSize { get; private set; }
        /// <summary>Escala vertical actual (se aplana al alejar la cámara, como el modo papel de HOI4).</summary>
        public float HeightScale { get; private set; }
        public float Zoom01 { get; private set; }
        public IMapMode MapMode { get; private set; }
        public Material Material => _material;

        public event Action<IMapMode> MapModeChanged;

        private Material _material;
        private Texture2D _heightTex, _colorTex, _idTex, _dataTex, _ownerTex, _lightsTex, _detailTex, _waterNormalTex;
        // Alturas a 1/4 de resolución (equivale al mip que usa la malla) para el raycast en CPU.
        private float[] _coarseHeights;
        private int _coarseW, _coarseH;
        private const int CoarseFactor = 4;
        private Color32[] _dataPixels;
        private Color32[] _ownerPixels;
        private byte[] _flags;
        private bool _dataDirty, _ownerDirty;
        private GameObject _chunkRoot;
        /// <summary>Copias del mapa a izquierda y derecha: el mundo da la vuelta de este a oeste.</summary>
        public bool WrapAround = true;
        private readonly System.Collections.Generic.List<Mesh> _meshes = new System.Collections.Generic.List<Mesh>();

        private int _hoverProvince = -1, _hoverCountry = -1, _selectedCountry = -1, _selectedProvince = -1, _playerCountry = -1;

        // ------------------------------------------------------------------ Construcción

        public void Build(WorldData world)
        {
            Clear();
            World = world;
            var map = world.Map;
            WorldSize = new Vector2(WorldWidth, WorldWidth * map.Height / map.Width);
            HeightScale = MaxTerrainHeight;
            MapMode ??= new PoliticalMapMode();

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[MapRenderer] No se encontró el shader '{ShaderName}'.");
                return;
            }
            _material = new Material(shader) { name = "MapTerrain (runtime)" };

            CreateTextures(world);
            ApplyMaterialProperties();
            BuildMeshes(map);
            RefreshMapModeColors();
            RefreshOwners();
        }

        private void CreateTextures(WorldData world)
        {
            var map = world.Map;
            int w = map.Width, h = map.Height, n = w * h;

            // Relieve en 16 bits normalizados (65536 niveles uniformes): con half float las alturas altas
            // pierden precisión y de cerca se ven escalones. Si la GPU no admite R16, se usa half float.
            bool r16 = SystemInfo.SupportsTextureFormat(TextureFormat.R16);
            _heightTex = new Texture2D(w, h, r16 ? TextureFormat.R16 : TextureFormat.RHalf, true, true)
            {
                name = "HeightTex", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
            };
            var heightBits = new ushort[n];
            for (int i = 0; i < n; i++)
                heightBits[i] = r16 ? (ushort)Mathf.Clamp(Mathf.RoundToInt(map.Heights[i] * 65535f), 0, 65535) : Mathf.FloatToHalf(map.Heights[i]);
            _heightTex.SetPixelData(heightBits, 0);
            _heightTex.Apply(true, true);

            _colorTex = new Texture2D(w, h, TextureFormat.RGBA32, true, false)
            {
                name = "ColorMap", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear, anisoLevel = 4,
            };
            _colorTex.SetPixels32(map.ColorMap);
            _colorTex.Apply(true, true);

            // Id de provincia: 16 bits en R (bajo) y G (alto). Sin filtrado ni mipmaps.
            _idTex = new Texture2D(w, h, TextureFormat.RG16, false, true)
            {
                name = "ProvinceIdTex", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point,
            };
            var idBytes = new byte[n * 2];
            for (int i = 0; i < n; i++)
            {
                int id = map.ProvinceIds[i];
                idBytes[i * 2] = (byte)(id & 0xFF);
                idBytes[i * 2 + 1] = (byte)((id >> 8) & 0xFF);
            }
            _idTex.SetPixelData(idBytes, 0);
            _idTex.Apply(false, true);

            // Luces de ciudad para la noche (negro si el mapa no las trae).
            if (map.CityLights != null)
            {
                _lightsTex = new Texture2D(w, h, TextureFormat.R8, true, true)
                {
                    name = "CityLights", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Trilinear,
                };
                _lightsTex.SetPixelData(map.CityLights, 0);
                _lightsTex.Apply(true, true);
            }
            else
            {
                _lightsTex = new Texture2D(1, 1, TextureFormat.R8, false, true) { name = "CityLights (vacío)" };
                _lightsTex.SetPixelData(new byte[] { 0 }, 0);
                _lightsTex.Apply(false, true);
            }

            _detailTex = ProceduralTextures.CreateDetailNoise();
            _waterNormalTex = ProceduralTextures.CreateWaterNormals();
            BuildCoarseHeights(map);

            int rows = Mathf.Max(1, Mathf.CeilToInt(world.Provinces.Count / (float)LookupWidth));
            _dataTex = new Texture2D(LookupWidth, rows, TextureFormat.RGBA32, false, false)
            {
                name = "ProvinceDataTex", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point,
            };
            _ownerTex = new Texture2D(LookupWidth, rows, TextureFormat.RGBA32, false, true)
            {
                name = "ProvinceOwnerTex", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point,
            };
            _dataPixels = new Color32[LookupWidth * rows];
            _ownerPixels = new Color32[LookupWidth * rows];
            _flags = new byte[world.Provinces.Count];
        }

        private void ApplyMaterialProperties()
        {
            _material.SetTexture("_HeightTex", _heightTex);
            _material.SetTexture("_ColorMap", _colorTex);
            _material.SetTexture("_ProvinceIdTex", _idTex);
            _material.SetTexture("_ProvinceDataTex", _dataTex);
            _material.SetTexture("_ProvinceOwnerTex", _ownerTex);
            _material.SetTexture("_CityLightsTex", _lightsTex);
            _material.SetTexture("_DetailTex", _detailTex);
            _material.SetTexture("_WaterNormalTex", _waterNormalTex);
            _material.SetFloat("_ReliefHeight", ReliefShadingHeight);
            // Variables globales: las usan ríos, árboles, edificios y nubes para apoyarse en el relieve.
            Shader.SetGlobalTexture("_GS_HeightTex", _heightTex);
            Shader.SetGlobalTexture("_GS_DetailTex", _detailTex);
            Shader.SetGlobalTexture("_GS_WaterNormalTex", _waterNormalTex);
            Shader.SetGlobalFloat("_GS_SeaLevel", World.Map.SeaLevel);
            Shader.SetGlobalFloat("_GS_VertexLod", VertexHeightMip);
            Shader.SetGlobalVector("_GS_MapWorldSize", new Vector4(WorldSize.x, WorldSize.y, 0, 0));
            _material.SetFloat("_VertexHeightLod", VertexHeightMip);
            var map = World.Map;
            float MillerY(float latDeg) => 1.25f * Mathf.Log(Mathf.Tan(Mathf.PI / 4f + 0.4f * latDeg * Mathf.Deg2Rad));
            var projection = map.MillerProjection
                ? new Vector4(MillerY(map.LatTop), MillerY(map.LatBottom), map.LatTop * Mathf.Deg2Rad, map.LatBottom * Mathf.Deg2Rad)
                : new Vector4(0, 0, map.LatTop * Mathf.Deg2Rad, map.LatBottom * Mathf.Deg2Rad);
            _material.SetFloat("_ProjectionMode", map.MillerProjection ? 1f : 0f);
            _material.SetVector("_Projection", projection);
            Shader.SetGlobalFloat("_GS_ProjectionMode", map.MillerProjection ? 1f : 0f);
            Shader.SetGlobalVector("_GS_Projection", projection);
            SetSun(0f, 0f, 0f);
            _material.SetFloat("_SeaLevel", World.Map.SeaLevel);
            _material.SetFloat("_NormalStrength", World.Map.NormalStrength);
            _material.SetVector("_MapWorldSize", new Vector4(WorldSize.x, WorldSize.y, 0, 0));
            _material.SetColor("_ShallowWater", ShallowWater);
            _material.SetColor("_DeepWater", DeepWater);
            _material.SetColor("_HighlightColor", HighlightColor);
            _material.SetFloat("_CountryBorderWidth", CountryBorderWidth);
            _material.SetFloat("_ProvinceBorderWidth", ProvinceBorderWidth);
            _material.SetFloat("_OverlayStrength", MapMode.OverlayStrength);
            SetZoom(Zoom01);
        }

        private void BuildMeshes(MapData map)
        {
            _chunkRoot = new GameObject("MapChunks");
            _chunkRoot.transform.SetParent(transform, false);
            var copies = new System.Collections.Generic.List<Transform>();
            foreach (float offset in WrapAround ? new[] { 0f, -WorldSize.x, WorldSize.x } : new[] { 0f })
            {
                var copy = new GameObject(offset == 0 ? "Centro" : offset < 0 ? "CopiaOeste" : "CopiaEste").transform;
                copy.SetParent(_chunkRoot.transform, false);
                copy.localPosition = new Vector3(offset, 0, 0);
                copies.Add(copy);
            }

            int quadsX = Mathf.CeilToInt(map.Width / (float)VertexStep);
            int quadsZ = Mathf.CeilToInt(map.Height / (float)VertexStep);

            for (int cz = 0; cz < quadsZ; cz += ChunkQuads)
            for (int cx = 0; cx < quadsX; cx += ChunkQuads)
            {
                int qx = Mathf.Min(ChunkQuads, quadsX - cx);
                int qz = Mathf.Min(ChunkQuads, quadsZ - cz);
                var mesh = BuildChunk(map, cx, cz, qx, qz);
                _meshes.Add(mesh);

                foreach (var copy in copies)
                {
                    var go = new GameObject($"Chunk_{cx / ChunkQuads}_{cz / ChunkQuads}");
                    go.transform.SetParent(copy, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = _material;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    mr.lightProbeUsage = LightProbeUsage.Off;
                    mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
            }
        }

        private Mesh BuildChunk(MapData map, int startX, int startZ, int quadsX, int quadsZ)
        {
            int vx = quadsX + 1, vz = quadsZ + 1;
            var vertices = new Vector3[vx * vz];
            var uvs = new Vector2[vx * vz];
            for (int z = 0; z < vz; z++)
            for (int x = 0; x < vx; x++)
            {
                float u = Mathf.Min(1f, (startX + x) * VertexStep / (float)map.Width);
                float v = Mathf.Min(1f, (startZ + z) * VertexStep / (float)map.Height);
                int i = z * vx + x;
                // La altura se aplica en el vertex shader (permite aplanar el relieve al alejar).
                vertices[i] = new Vector3(u * WorldSize.x, 0f, v * WorldSize.y);
                uvs[i] = new Vector2(u, v);
            }

            var tris = new int[quadsX * quadsZ * 6];
            int t = 0;
            for (int z = 0; z < quadsZ; z++)
            for (int x = 0; x < quadsX; x++)
            {
                int i = z * vx + x;
                tris[t++] = i;
                tris[t++] = i + vx;
                tris[t++] = i + 1;
                tris[t++] = i + 1;
                tris[t++] = i + vx;
                tris[t++] = i + vx + 1;
            }

            var mesh = new Mesh { name = $"MapChunk_{startX}_{startZ}" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = tris;
            var b = mesh.bounds;
            b.Encapsulate(b.center + Vector3.up * MaxTerrainHeight);
            mesh.bounds = b;
            mesh.UploadMeshData(true);
            return mesh;
        }

        private void Clear()
        {
            DestroyAsset(_chunkRoot);
            foreach (var m in _meshes) DestroyAsset(m);
            _meshes.Clear();
            DestroyAsset(_material);
            DestroyAsset(_heightTex);
            DestroyAsset(_colorTex);
            DestroyAsset(_idTex);
            DestroyAsset(_dataTex);
            DestroyAsset(_ownerTex);
            DestroyAsset(_lightsTex);
            DestroyAsset(_detailTex);
            DestroyAsset(_waterNormalTex);
        }

        private void BuildCoarseHeights(MapData map)
        {
            _coarseW = Mathf.Max(1, map.Width / CoarseFactor);
            _coarseH = Mathf.Max(1, map.Height / CoarseFactor);
            _coarseHeights = new float[_coarseW * _coarseH];
            for (int cy = 0; cy < _coarseH; cy++)
            for (int cx = 0; cx < _coarseW; cx++)
            {
                float sum = 0;
                for (int dy = 0; dy < CoarseFactor; dy++)
                {
                    int row = (cy * CoarseFactor + dy) * map.Width + cx * CoarseFactor;
                    for (int dx = 0; dx < CoarseFactor; dx++) sum += map.Heights[row + dx];
                }
                _coarseHeights[cy * _coarseW + cx] = sum / (CoarseFactor * CoarseFactor);
            }
        }

        private float SampleCoarse(float u, float v)
        {
            float fx = u * _coarseW - 0.5f;
            float fy = Mathf.Clamp(v * _coarseH - 0.5f, 0f, _coarseH - 1.001f);
            int x0 = Mathf.FloorToInt(fx), y0 = (int)fy;
            float tx = fx - x0, ty = fy - y0;
            x0 = ((x0 % _coarseW) + _coarseW) % _coarseW;
            int x1 = (x0 + 1) % _coarseW, y1 = Mathf.Min(y0 + 1, _coarseH - 1);
            float a = Mathf.Lerp(_coarseHeights[y0 * _coarseW + x0], _coarseHeights[y0 * _coarseW + x1], tx);
            float b = Mathf.Lerp(_coarseHeights[y1 * _coarseW + x0], _coarseHeights[y1 * _coarseW + x1], tx);
            return Mathf.Lerp(a, b, ty);
        }

        private static void DestroyAsset(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        private void OnDestroy() => Clear();

        // ------------------------------------------------------------------ Actualización

        private void LateUpdate() => FlushTextures();

        /// <summary>Sube a la GPU los cambios pendientes de color/flags/propietarios.</summary>
        public void FlushTextures()
        {
            if (_dataDirty && _dataTex != null)
            {
                _dataTex.SetPixels32(_dataPixels);
                _dataTex.Apply(false);
                _dataDirty = false;
            }
            if (_ownerDirty && _ownerTex != null)
            {
                _ownerTex.SetPixels32(_ownerPixels);
                _ownerTex.Apply(false);
                _ownerDirty = false;
            }
        }

        /// <summary>0 = cámara cerca, 1 = cámara lejos.</summary>
        public void SetZoom(float zoom01)
        {
            Zoom01 = Mathf.Clamp01(zoom01);
            HeightScale = MaxTerrainHeight * Mathf.Lerp(1f, 0.3f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, Zoom01)));
            Shader.SetGlobalFloat("_GS_Zoom", Zoom01);
            Shader.SetGlobalFloat("_GS_HeightScale", HeightScale);
            if (_material == null) return;
            _material.SetFloat("_Zoom", Zoom01);
            _material.SetFloat("_HeightScale", HeightScale);
        }

        /// <summary>
        /// Posición del sol: longitud y latitud (declinación) del punto subsolar, en grados.
        /// nightStrength 0 desactiva la noche.
        /// </summary>
        public void SetSun(float subsolarLongitude, float declination, float nightStrength = 1f)
        {
            var sun = new Vector4(subsolarLongitude * Mathf.Deg2Rad, declination * Mathf.Deg2Rad, nightStrength, 0);
            Shader.SetGlobalVector("_GS_SunInfo", sun);
            if (_material == null) return;
            _material.SetVector("_SunInfo", sun);
        }

        public void SetMapMode(IMapMode mode)
        {
            if (mode == null) return;
            MapMode = mode;
            if (_material != null) _material.SetFloat("_OverlayStrength", mode.OverlayStrength);
            RefreshMapModeColors();
            MapModeChanged?.Invoke(mode);
        }

        /// <summary>Recalcula el color de todas las provincias con el map mode actual.</summary>
        public void RefreshMapModeColors()
        {
            if (World == null || _dataPixels == null) return;
            foreach (var p in World.Provinces) WriteDataPixel(p);
            _dataDirty = true;
        }

        /// <summary>Llamar cuando cambie el propietario de provincias (conquista, anexión...).</summary>
        public void RefreshOwners()
        {
            if (World == null || _ownerPixels == null) return;
            foreach (var p in World.Provinces)
            {
                int owner = p.OwnerId + 1; // 0 = sin dueño
                _ownerPixels[p.Id] = new Color32((byte)(owner & 0xFF), (byte)((owner >> 8) & 0xFF), (byte)(p.IsWater ? 255 : 0), 255);
            }
            _ownerDirty = true;
            if (MapMode is PoliticalMapMode) RefreshMapModeColors();
        }

        public void RefreshProvince(Province p)
        {
            WriteDataPixel(p);
            _dataDirty = true;
        }

        private void WriteDataPixel(Province p)
        {
            var c = MapMode.GetProvinceColor(p, World);
            c.a = _flags[p.Id];
            _dataPixels[p.Id] = c;
        }

        // ------------------------------------------------------------------ Resaltado

        public void SetHoveredProvince(int provinceId) => SwapProvinceFlag(ref _hoverProvince, provinceId, FlagHoverProvince);
        public void SetSelectedProvince(int provinceId) => SwapProvinceFlag(ref _selectedProvince, provinceId, FlagSelectedProvince);
        public void SetHoveredCountry(int countryId) => SwapCountryFlag(ref _hoverCountry, countryId, FlagHoverCountry);
        public void SetSelectedCountry(int countryId) => SwapCountryFlag(ref _selectedCountry, countryId, FlagSelectedCountry);
        public void SetPlayerCountry(int countryId) => SwapCountryFlag(ref _playerCountry, countryId, FlagPlayerCountry);

        public void ClearHighlights()
        {
            SetHoveredProvince(-1);
            SetSelectedProvince(-1);
            SetHoveredCountry(-1);
            SetSelectedCountry(-1);
        }

        private void SwapProvinceFlag(ref int current, int next, byte flag)
        {
            if (current == next || World == null) return;
            if (current >= 0) SetFlag(current, flag, false);
            current = next;
            if (current >= 0) SetFlag(current, flag, true);
        }

        private void SwapCountryFlag(ref int current, int next, byte flag)
        {
            if (current == next || World == null) return;
            var old = World.GetCountry(current);
            if (old != null)
                foreach (int pid in old.Provinces) SetFlag(pid, flag, false);
            current = next;
            var c = World.GetCountry(current);
            if (c != null)
                foreach (int pid in c.Provinces) SetFlag(pid, flag, true);
        }

        private void SetFlag(int provinceId, byte flag, bool on)
        {
            if (provinceId < 0 || provinceId >= _flags.Length) return;
            byte f = on ? (byte)(_flags[provinceId] | flag) : (byte)(_flags[provinceId] & ~flag);
            if (f == _flags[provinceId]) return;
            _flags[provinceId] = f;
            _dataPixels[provinceId].a = f;
            _dataDirty = true;
        }

        // ------------------------------------------------------------------ Conversión de coordenadas

        /// <summary>Altura del terreno (en unidades de mundo) idéntica a la del vertex shader.</summary>
        public float GetTerrainHeight(float u, float v)
        {
            if (World == null) return 0f;
            float sea = World.Map.SeaLevel;
            u -= Mathf.Floor(u); // el mapa se repite de este a oeste
            float h = _coarseHeights != null ? SampleCoarse(u, v) : World.Map.SampleHeight(u, v);
            return Mathf.Clamp01((h - sea) / (1f - sea)) * HeightScale;
        }

        public Vector2 WorldToUV(Vector3 world) => new Vector2(world.x / WorldSize.x, world.z / WorldSize.y);

        public Vector3 UVToWorld(Vector2 uv) =>
            new Vector3(uv.x * WorldSize.x, GetTerrainHeight(uv.x, uv.y), uv.y * WorldSize.y);

        public Vector3 GetProvinceWorldPosition(Province p)
        {
            var map = World.Map;
            return UVToWorld(new Vector2((p.Center.x + 0.5f) / map.Width, (p.Center.y + 0.5f) / map.Height));
        }

        public Rect WorldBounds => new Rect(0, 0, WorldSize.x, WorldSize.y);
    }
}
