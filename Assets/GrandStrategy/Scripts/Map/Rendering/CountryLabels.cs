using System.Collections.Generic;
using GrandStrategy.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace GrandStrategy.Map.Rendering
{
    /// <summary>
    /// Nombres de los países escritos sobre el mapa, al estilo de los juegos de Paradox, con una jerarquía por
    /// escala: <b>mundo → continente → región → local</b>. Se separan dos decisiones:
    /// <list type="number">
    /// <item><b>Visibilidad</b> (¿debe existir el nombre a esta escala?):
    /// <list type="bullet">
    /// <item>Aparece cuando el territorio, en pantalla, es lo bastante grande para su nombre compacto
    /// (<see cref="AppearMaxOverflow"/>). Como al acercar el territorio solo crece, una vez visible se mantiene en
    /// las escalas continental y regional: el nombre no desaparece por cambiar de representación.</item>
    /// <item>Todos los nombres nacionales se desvanecen juntos y a propósito solo al entrar en la escala local
    /// (<see cref="LocalFadeStart"/> → <see cref="LocalFadeEnd"/>).</item>
    /// </list></item>
    /// <item><b>Representación</b> (si existe, ¿cómo se dibuja?):
    /// <list type="bullet">
    /// <item><b>Normal</b>: curva sobre el eje principal del territorio (análisis de componentes principales). Se
    /// usa si, ajustada a su país, se lee (≥ <see cref="NormalMinPixels"/>). La geometría marca cuánto podría crecer;
    /// por encima de <see cref="SizeKneePixels"/> crece cada vez menos (<see cref="SizeCompression"/>), con un tope
    /// (<see cref="MaxPixels"/>): los grandes siguen siendo mayores, sin diferencias extremas con sus vecinos.</item>
    /// <item><b>Compacta</b>: recta, más apretada y algo más discreta, a tamaño fijo de pantalla
    /// (<see cref="CompactPixels"/>); estable al mover la cámara.</item>
    /// </list></item>
    /// </list>
    /// Las decisiones usan histéresis y los cambios son fundidos. Si dos nombres se pisan, las normales tienen
    /// preferencia; entre compactas se respetan primero las que ya estaban visibles y después el país mayor. No hay
    /// listas de países: todo depende de la geometría actual, que solo se recalcula cuando cambian las fronteras
    /// (<see cref="WorldData.OwnerChanged"/>) o la fuente.
    /// </summary>
    public sealed class CountryLabels : MonoBehaviour
    {
        // ------------------------------------------------------------------ Parámetros de ajuste
        // Visibilidad (mundo → continente → región)
        /// <summary>
        /// Aparición: el nombre existe cuando su versión compacta desbordaría su país como mucho este número de veces
        /// (CompactPixels / tamaño del territorio en pantalla). Más alto = los países pequeños aparecen antes al
        /// acercar desde la vista mundial. Vale igual para países pequeños y microestados: estos, al ser menores,
        /// llegan al umbral más tarde (típicamente en escala regional).
        /// </summary>
        public const float AppearMaxOverflow = 2.0f;
        /// <summary>Histéresis: para salir de un estado hay que pasar el umbral un 15 % (evita parpadeos en el límite).</summary>
        public const float Hysteresis = 0.15f;
        // Desvanecimiento intencionado en escala local (zoom de cámara: 0 cerca, 1 lejos). Con 0.16 la pantalla
        // abarca unos 14° de longitud (pocas provincias); con 0.10, unos 8°. Antes era 0.36 → 0.27 (Europa entera
        // en pantalla), lo que ocultaba los nombres en escala continental.
        /// <summary>Zoom en que los nombres nacionales empiezan a desvanecerse al entrar en escala local.</summary>
        public const float LocalFadeStart = 0.16f;
        /// <summary>Zoom en que los nombres nacionales han desaparecido del todo (escala local/provincial).</summary>
        public const float LocalFadeEnd = 0.10f;

        // Representación
        /// <summary>Altura mínima (px de pantalla) de las mayúsculas de una etiqueta normal; por debajo pasa a compacta.</summary>
        public const float NormalMinPixels = 11f;
        /// <summary>Altura (px de pantalla) de las mayúsculas de una etiqueta compacta.</summary>
        public const float CompactPixels = 10f;
        /// <summary>Opacidad de las etiquetas compactas respecto a las normales (menos protagonismo).</summary>
        public const float CompactAlpha = 0.88f;
        /// <summary>Espacio extra entre letras (en "em"): normal (estilo cartográfico) y compacta.</summary>
        public const float LetterSpacing = 0.2f, CompactLetterSpacing = 0.06f;
        /// <summary>Tamaño (px de pantalla) a partir del cual una etiqueta normal crece cada vez menos.</summary>
        public const float SizeKneePixels = 24f;
        /// <summary>Exponente del crecimiento por encima de <see cref="SizeKneePixels"/> (1 = sin compresión).</summary>
        public const float SizeCompression = 0.6f;
        /// <summary>Altura máxima (px de pantalla) de las mayúsculas: los países enormes no llenan la pantalla.</summary>
        public const float MaxPixels = 120f;
        /// <summary>Velocidad de los fundidos al aparecer o cambiar de representación (1/s): 4 ≈ 0.25 s.</summary>
        public const float FadeSpeed = 4f;

        // Legibilidad día/noche (el texto mantiene su tono crema; el contraste viene del halo oscuro)
        /// <summary>Radio del halo (texels del atlas de la fuente).</summary>
        public const float HaloRadius = 1.8f;
        /// <summary>Opacidad del halo oscuro de día (más presencia sobre colores claros, nieve y nubes) y de noche.</summary>
        public const float HaloDayAlpha = 0.68f, HaloNightAlpha = 0.45f;
        /// <summary>Sombra suave desplazada (solo de día), opacidad relativa al halo.</summary>
        public const float DayShadow = 0.55f;
        /// <summary>Color del texto (crema cálido) de día y de noche.</summary>
        public static readonly Color DayTextColor = new Color(0.95f, 0.92f, 0.84f, 0.95f);
        public static readonly Color NightTextColor = new Color(0.92f, 0.88f, 0.78f, 0.8f);
        /// <summary>Color del halo (oscuro, apenas cálido).</summary>
        public static readonly Color HaloColor = new Color(0.05f, 0.045f, 0.04f, 1f);

        private const int FontSize = 64;            // tamaño al que se rasteriza la fuente en el atlas
        private const float CapHeight = 0.7f;       // altura de las mayúsculas respecto al "em" (aprox.)
        private const float MaxEmPixels = 230f;     // tope de tamaño (en píxeles del mapa) para los países enormes
        private const int Bins = 8;
        private const float RebuildDelay = 0.5f;    // agrupa varios cambios de frontera seguidos en una reconstrucción

        private MapRenderer _map;
        private WorldData _world;
        private Font _font;
        private Material _material;
        private RenderTexture _atlas;
        private Mesh _mesh;
        private readonly List<GameObject> _copies = new List<GameObject>();
        private bool _dirty;
        private float _rebuildAt = -1f;

        /// <summary>Cámara para la que se resuelven los choques entre nombres (null = Camera.main).</summary>
        public Camera TargetCamera;

        private enum Lod : byte { Hidden, Compact, Normal }

        /// <summary>Un conjunto de letras (versión normal o compacta de un nombre).</summary>
        private struct GlyphSet
        {
            public int FirstVertex, VertexCount;
            public int FirstGlyph, GlyphCount;
            public float CapWorld;     // altura de las letras en el mundo (tal como se construyeron)
        }

        private struct Label
        {
            public GlyphSet Normal, Compact;
            public Vector2 Anchor;     // centro del nombre (x, z)
            public float Height;       // altura del terreno en el centro
            public float Priority;     // superficie del texto: los grandes ganan
        }

        private const float CellPixels = 64f;
        private readonly List<Label> _labels = new List<Label>();
        private readonly List<Vector2> _glyphCenters = new List<Vector2>();
        private readonly List<Rect> _candidate = new List<Rect>();
        private int[] _order;
        private float[] _alphaNormal, _alphaCompact;
        private bool[] _visible; // visibilidad por escala (con histéresis), independiente de la representación
        private Lod[] _lod;      // representación decidida (normal / compacta), si es visible
        private Lod[] _shown;    // estado final tras los choques
        private Lod[] _previous; // estado final del fotograma anterior (las compactas ya visibles tienen preferencia)
        private Color32[] _colors;
        private List<Rect>[] _grid;
        private int _gridCols, _gridRows;
        private bool _snapAlpha;

        public void Build(MapRenderer map, WorldData world)
        {
            _map = map;
            _world = world;
            var shader = Shader.Find("GrandStrategy/MapLabel");
            if (shader == null)
            {
                Debug.LogError("[CountryLabels] No se encontró el shader GrandStrategy/MapLabel.");
                enabled = false;
                return;
            }
            _font = Resources.Load<Font>("Fonts/MarcellusSC-Regular");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _material = new Material(shader) { name = "CountryLabels" };
            ApplyMaterialParameters();
            _mesh = new Mesh { name = "CountryLabels", indexFormat = IndexFormat.UInt32 };
            Font.textureRebuilt += OnFontTextureRebuilt;
            world.OwnerChanged += OnOwnerChanged;

            // Tres copias (centro, oeste y este) para que el mapa dé la vuelta al mundo.
            float w = map.WorldSize.x;
            foreach (float offset in new[] { 0f, -w, w })
            {
                var go = new GameObject("Nombres de países");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(offset, 0f, 0f);
                go.AddComponent<MeshFilter>().sharedMesh = _mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _material;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                _copies.Add(go);
            }

            Rebuild();
            ApplySettings();
            GameSettings.Changed += ApplySettings;
        }

        /// <summary>Los parámetros visuales del shader salen de las constantes de esta clase (un único sitio).</summary>
        private void ApplyMaterialParameters()
        {
            _material.SetFloat("_MaxPixels", MaxPixels);
            _material.SetFloat("_SizeKnee", SizeKneePixels);
            _material.SetFloat("_SizeCompression", SizeCompression);
            _material.SetFloat("_CompactPixels", CompactPixels);
            _material.SetFloat("_CompactAlpha", CompactAlpha);
            _material.SetFloat("_HideZoomStart", LocalFadeStart);
            _material.SetFloat("_HideZoomEnd", LocalFadeEnd);
            _material.SetFloat("_HaloRadius", HaloRadius);
            _material.SetFloat("_DayShadow", DayShadow);
            _material.SetColor("_TextColor", DayTextColor);
            _material.SetColor("_NightTextColor", NightTextColor);
            _material.SetColor("_HaloColor", new Color(HaloColor.r, HaloColor.g, HaloColor.b, HaloDayAlpha));
            _material.SetColor("_NightHaloColor", new Color(HaloColor.r, HaloColor.g, HaloColor.b, HaloNightAlpha));
        }

        private void OnDestroy()
        {
            Font.textureRebuilt -= OnFontTextureRebuilt;
            GameSettings.Changed -= ApplySettings;
            if (_world != null) _world.OwnerChanged -= OnOwnerChanged;
            if (_material != null) Destroy(_material);
            if (_mesh != null) Destroy(_mesh);
            if (_atlas != null) _atlas.Release();
        }

        private void ApplySettings()
        {
            foreach (var go in _copies) go.SetActive(GameSettings.CountryNames);
        }

        private void OnFontTextureRebuilt(Font font)
        {
            if (font == _font) _dirty = true;
        }

        // Fronteras nuevas: se recolocan los nombres (agrupando cambios seguidos, p. ej. una anexión entera).
        private void OnOwnerChanged(Province province)
        {
            if (_rebuildAt < 0f) _rebuildAt = Time.unscaledTime + RebuildDelay;
        }

        private void LateUpdate()
        {
            if (_rebuildAt >= 0f && Time.unscaledTime >= _rebuildAt)
            {
                _rebuildAt = -1f;
                _dirty = true;
            }
            // Si Unity reorganiza el atlas de la fuente cambian las UV de las letras: se regenera todo.
            if (_dirty) Rebuild();
            if (GameSettings.CountryNames) UpdateVisibility(TargetCamera != null ? TargetCamera : Camera.main, Time.unscaledDeltaTime);
        }

        // ================================================================== Nivel de detalle y choques

        /// <summary>Tamaño en pantalla de una etiqueta normal: crece cada vez menos por encima del codo y con tope.</summary>
        public static float NormalDisplayPixels(float px)
        {
            float d = px <= SizeKneePixels ? px : SizeKneePixels * Mathf.Pow(px / SizeKneePixels, SizeCompression);
            return Mathf.Min(d, MaxPixels);
        }

        /// <summary>
        /// Decide cada nombre: primero si es visible a esta escala (aparición con histéresis) y, si lo es, cómo se
        /// representa (normal si así se lee, si no compacta). Después resuelve choques: las normales ocupan su sitio
        /// primero; luego las compactas que ya se veían y por último las nuevas, de mayor a menor país. Los cambios
        /// son graduales (opacidad por vértice) para que no parpadeen.
        /// </summary>
        public void UpdateVisibility(Camera cam, float deltaTime)
        {
            if (cam == null || _order == null || _colors == null) return;
            bool namesShown = Shader.GetGlobalFloat("_GS_Zoom") > LocalFadeEnd;
            System.Array.Copy(_shown, _previous, _shown.Length);
            float w = _map.WorldSize.x;
            float screenH = cam.pixelHeight;
            float p11 = Mathf.Abs(cam.projectionMatrix[1, 1]);
            Vector3 camPos = cam.transform.position;
            var screen = new Rect(0, 0, cam.pixelWidth, cam.pixelHeight);
            ResetGrid(cam.pixelWidth, cam.pixelHeight);

            // Pasada 0: visibilidad y representación de todos + normales. 1: compactas que ya se veían. 2: el resto.
            for (int pass = 0; pass < 3; pass++)
            foreach (int index in _order)
            {
                var label = _labels[index];
                // Copia del mapa (centro, este u oeste) más cercana a la cámara.
                float offset = Mathf.Round((camPos.x - label.Anchor.x) / w) * w;
                var anchor = transform.TransformPoint(new Vector3(label.Anchor.x + offset, label.Height, label.Anchor.y));
                float dist = Mathf.Max(Vector3.Distance(camPos, anchor), 1e-3f);
                float pixelsPerWorld = p11 / dist * screenH * 0.5f;

                if (pass == 0)
                {
                    // 1. Visibilidad: ¿el territorio es ya lo bastante grande en pantalla para su nombre compacto?
                    // Al acercar solo crece, así que una vez visible no se pierde en escala continental ni regional.
                    float px = label.Normal.CapWorld * pixelsPerWorld;
                    var fitSet = label.Compact.GlyphCount > 0 ? label.Compact : label.Normal;
                    float territoryPx = fitSet.CapWorld * pixelsPerWorld;
                    float overflow = territoryPx > 0f ? CompactPixels / territoryPx : float.MaxValue;
                    _visible[index] = overflow <= AppearMaxOverflow * (_visible[index] ? 1f + Hysteresis : 1f);
                    // 2. Representación (solo si es visible): normal si ajustada a su país se lee; si no, compacta.
                    float normalMin = NormalMinPixels * (_lod[index] == Lod.Normal ? 1f - Hysteresis : 1f);
                    _lod[index] = !_visible[index] ? Lod.Hidden
                        : px >= normalMin || label.Compact.GlyphCount == 0 ? Lod.Normal : Lod.Compact;
                }
                var lod = _lod[index];
                if (!namesShown) { _shown[index] = lod; continue; }
                if (lod == Lod.Hidden) { if (pass == 0) _shown[index] = Lod.Hidden; continue; }
                bool wasCompact = _previous[index] == Lod.Compact;
                bool myPass = lod == Lod.Normal ? pass == 0 : (wasCompact ? pass == 1 : pass == 2);
                if (!myPass) continue;

                // Rectángulos de las letras en pantalla, tal como las coloca el shader.
                var set = lod == Lod.Normal ? label.Normal : label.Compact;
                float setPx = set.CapWorld * pixelsPerWorld;
                float scale = lod == Lod.Normal ? NormalDisplayPixels(setPx) / Mathf.Max(setPx, 1e-4f) : CompactPixels / Mathf.Max(setPx, 1e-4f);
                float half = setPx * scale * 0.5f;
                bool show = true, onScreen = false;
                _candidate.Clear();
                for (int g = 0; g < set.GlyphCount && show; g++)
                {
                    var local = label.Anchor + (_glyphCenters[set.FirstGlyph + g] - label.Anchor) * scale;
                    var sp = cam.WorldToScreenPoint(transform.TransformPoint(new Vector3(local.x + offset, label.Height, local.y)));
                    if (sp.z <= 0f) continue;
                    var r = new Rect(sp.x - half, sp.y - half, half * 2f, half * 2f);
                    if (!r.Overlaps(screen)) continue;
                    onScreen = true;
                    // Las normales caben en su país y siempre se ven; las compactas solo si no pisan nada.
                    if (lod == Lod.Compact && GridOverlaps(r)) show = false;
                    else _candidate.Add(r);
                }
                if (show && onScreen)
                    foreach (var r in _candidate) GridAdd(r);
                _shown[index] = show ? lod : Lod.Hidden;
            }

            float step = _snapAlpha ? 1f : deltaTime * FadeSpeed;
            bool changed = false;
            for (int index = 0; index < _labels.Count; index++)
            {
                var label = _labels[index];
                changed |= FadeSet(label.Normal, ref _alphaNormal[index], _shown[index] == Lod.Normal ? 1f : 0f, step);
                changed |= FadeSet(label.Compact, ref _alphaCompact[index], _shown[index] == Lod.Compact ? 1f : 0f, step);
            }
            _snapAlpha = false;
            if (changed) _mesh.colors32 = _colors;
        }

        /// <summary>Acerca la opacidad de un conjunto de letras a su objetivo; true si ha cambiado.</summary>
        private bool FadeSet(GlyphSet set, ref float alpha, float target, float step)
        {
            float a = Mathf.MoveTowards(alpha, target, step);
            if (Mathf.Abs(a - alpha) < 1e-4f) return false;
            alpha = a;
            byte b = (byte)Mathf.RoundToInt(a * 255f);
            for (int v = 0; v < set.VertexCount; v++) _colors[set.FirstVertex + v].a = b;
            return true;
        }

        private void ResetGrid(int width, int height)
        {
            int cols = Mathf.Max(1, Mathf.CeilToInt(width / CellPixels));
            int rows = Mathf.Max(1, Mathf.CeilToInt(height / CellPixels));
            if (_grid == null || cols != _gridCols || rows != _gridRows)
            {
                _gridCols = cols;
                _gridRows = rows;
                _grid = new List<Rect>[cols * rows];
                for (int i = 0; i < _grid.Length; i++) _grid[i] = new List<Rect>();
            }
            else
                foreach (var cell in _grid) cell.Clear();
        }

        private void CellRange(Rect r, out int x0, out int y0, out int x1, out int y1)
        {
            x0 = Mathf.Clamp((int)(r.xMin / CellPixels), 0, _gridCols - 1);
            x1 = Mathf.Clamp((int)(r.xMax / CellPixels), 0, _gridCols - 1);
            y0 = Mathf.Clamp((int)(r.yMin / CellPixels), 0, _gridRows - 1);
            y1 = Mathf.Clamp((int)(r.yMax / CellPixels), 0, _gridRows - 1);
        }

        private bool GridOverlaps(Rect r)
        {
            CellRange(r, out int x0, out int y0, out int x1, out int y1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                foreach (var o in _grid[y * _gridCols + x])
                    if (o.Overlaps(r)) return true;
            return false;
        }

        private void GridAdd(Rect r)
        {
            CellRange(r, out int x0, out int y0, out int x1, out int y1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                _grid[y * _gridCols + x].Add(r);
        }

        // ================================================================== Construcción

        public void Rebuild()
        {
            if (_world == null || _font == null) return;
            var texts = new Dictionary<Country, string>();
            var allChars = new System.Text.StringBuilder();
            foreach (var c in _world.Countries)
            {
                if (!c.IsAlive) continue;
                string t = c.MapName.ToUpperInvariant();
                texts[c] = t;
                allChars.Append(t);
            }
            _font.RequestCharactersInTexture(allChars.ToString(), FontSize, FontStyle.Normal);

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var info = new List<Vector2>();
            var anchors = new List<Vector2>();
            var indices = new List<int>();
            _labels.Clear();
            _glyphCenters.Clear();
            foreach (var pair in texts) AddCountryLabel(pair.Key, pair.Value, vertices, uvs, info, anchors, indices);

            _order = new int[_labels.Count];
            for (int i = 0; i < _order.Length; i++) _order[i] = i;
            System.Array.Sort(_order, (x, y) => _labels[y].Priority.CompareTo(_labels[x].Priority));
            _alphaNormal = new float[_labels.Count];
            _alphaCompact = new float[_labels.Count];
            _visible = new bool[_labels.Count];
            _lod = new Lod[_labels.Count];
            _shown = new Lod[_labels.Count];
            _previous = new Lod[_labels.Count];
            _colors = new Color32[vertices.Count];
            for (int i = 0; i < _colors.Length; i++) _colors[i] = new Color32(255, 255, 255, 0);
            _snapAlpha = true;

            _mesh.Clear();
            _mesh.SetVertices(vertices);
            _mesh.SetUVs(0, uvs);
            _mesh.SetUVs(1, info);
            _mesh.SetUVs(2, anchors);
            _mesh.colors32 = _colors;
            _mesh.SetTriangles(indices, 0);
            _mesh.bounds = new Bounds(new Vector3(_map.WorldSize.x * 0.5f, 0f, _map.WorldSize.y * 0.5f),
                new Vector3(_map.WorldSize.x * 2f, 20f, _map.WorldSize.y * 2f));
            RefreshAtlas();
            _dirty = false;
        }

        /// <summary>Copia el atlas de la fuente a una textura con mipmaps (si no, las letras pequeñas parpadean).</summary>
        private void RefreshAtlas()
        {
            var src = _font.material != null ? _font.material.mainTexture : null;
            if (src == null) return;
            if (_atlas == null || _atlas.width != src.width || _atlas.height != src.height)
            {
                if (_atlas != null) _atlas.Release();
                _atlas = new RenderTexture(src.width, src.height, 0, RenderTextureFormat.ARGB32)
                {
                    name = "AtlasNombresPaises", useMipMap = true, autoGenerateMips = true,
                    filterMode = FilterMode.Trilinear, anisoLevel = 4, wrapMode = TextureWrapMode.Clamp,
                };
                _atlas.Create();
            }
            Graphics.Blit(src, _atlas);
            _material.mainTexture = _atlas;
        }

        private void AddCountryLabel(Country country, string text, List<Vector3> vertices, List<Vector2> uvs, List<Vector2> info, List<Vector2> anchors, List<int> indices)
        {
            foreach (var landmass in LabelledLandmasses(country))
                AddLandmassLabel(country, landmass, text, vertices, uvs, info, anchors, indices);
        }

        private void AddLandmassLabel(Country country, List<int> landmass, string text, List<Vector3> vertices, List<Vector2> uvs, List<Vector2> info, List<Vector2> anchors, List<int> indices)
        {
            var map = _world.Map;
            int width = map.Width;
            var points = SamplePixels(country, landmass);
            if (points.Count == 0)
                foreach (int id in landmass) points.Add(_world.GetProvince(id).Center);
            if (points.Count == 0) return;
            // Países diminutos: no hay forma que seguir, el nombre va recto y centrado.
            bool simple = points.Count < 12;

            // --- Eje principal (PCA)
            Vector2 mean = Vector2.zero;
            foreach (var p in points) mean += p;
            mean /= points.Count;
            float sxx = 0, sxy = 0, syy = 0;
            foreach (var p in points)
            {
                var d = p - mean;
                sxx += d.x * d.x; sxy += d.x * d.y; syy += d.y * d.y;
            }
            float angle = simple ? 0f : 0.5f * Mathf.Atan2(2f * sxy, sxx - syy);
            angle = Mathf.Clamp(angle, -70f * Mathf.Deg2Rad, 70f * Mathf.Deg2Rad);
            var axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            if (axis.x < 0) axis = -axis;                 // siempre de izquierda a derecha
            var normal = new Vector2(-axis.y, axis.x);    // "arriba" del texto

            // --- Extensión a lo largo del eje (percentiles 5–95 para ignorar salientes)
            var ts = new float[points.Count];
            for (int i = 0; i < points.Count; i++) ts[i] = Vector2.Dot(points[i] - mean, axis);
            var sorted = (float[])ts.Clone();
            System.Array.Sort(sorted);
            float t0 = sorted[(int)(sorted.Length * 0.05f)], t1 = sorted[(int)(sorted.Length * 0.95f)];
            float span = t1 - t0;
            if (span < 2f) { simple = true; span = 2f; t0 = -1f; t1 = 1f; }
            float tc = (t0 + t1) * 0.5f, half = span * 0.5f;

            // --- Por tramos: centro y grosor del país perpendicular al eje
            var sMin = new float[Bins];
            var sMax = new float[Bins];
            var count = new int[Bins];
            for (int b = 0; b < Bins; b++) { sMin[b] = float.MaxValue; sMax[b] = float.MinValue; }
            for (int i = 0; i < points.Count; i++)
            {
                float u = (ts[i] - t0) / span;
                if (u < 0f || u >= 1f) continue;
                int b = Mathf.Min(Bins - 1, (int)(u * Bins));
                float s = Vector2.Dot(points[i] - mean, normal);
                sMin[b] = Mathf.Min(sMin[b], s);
                sMax[b] = Mathf.Max(sMax[b], s);
                count[b]++;
            }

            // Ajuste por mínimos cuadrados de una parábola s(u) = c0 + c1 u + c2 u² (u en -1..1) a los centros.
            double m00 = 0, m01 = 0, m02 = 0, m11 = 0, m12 = 0, m22 = 0, r0 = 0, r1 = 0, r2 = 0;
            var thick = new List<float>();
            for (int b = 0; b < Bins; b++)
            {
                if (count[b] < 2) continue;
                double u = ((b + 0.5) / Bins) * 2.0 - 1.0;
                double s = (sMin[b] + sMax[b]) * 0.5;
                double w = count[b];
                m00 += w; m01 += w * u; m02 += w * u * u; m11 += w * u * u; m12 += w * u * u * u; m22 += w * u * u * u * u;
                r0 += w * s; r1 += w * s * u; r2 += w * s * u * u;
                thick.Add(sMax[b] - sMin[b]);
            }
            double c0 = 0, c1 = 0, c2 = 0;
            if (thick.Count < 2) simple = true;
            if (simple)
            {
                thick.Clear();
                thick.Add(Mathf.Max(2f, Mathf.Sqrt(syy / points.Count) * 3f));
            }
            else Solve3(m00, m01, m02, m11, m12, m22, r0, r1, r2, out c0, out c1, out c2);
            // Curvatura moderada: flecha máxima del 12 % de la longitud.
            double maxSag = span * 0.12;
            c2 = System.Math.Max(-maxSag, System.Math.Min(maxSag, c2));
            c1 = System.Math.Max(-span * 0.35, System.Math.Min(span * 0.35, c1));
            thick.Sort();
            float thickness = thick[thick.Count / 2];

            Vector2 Curve(float u) => mean + axis * (tc + u * half) + normal * (float)(c0 + c1 * u + c2 * u * u);

            // --- Tamaño del texto: ocupa ~80 % de la longitud sin pasarse del grosor del país. Los nombres de
            // varias palabras se parten en dos líneas si así caben bastante más grandes ("REINO / UNIDO").
            float widthEm = WidthEm(text, LetterSpacing);
            if (widthEm <= 0f) return;
            var lines = FitLines(text, LetterSpacing, span * 0.8f, thickness * 0.75f, thickness * 0.8f, out float em);
            em = Mathf.Max(em, 1f);
            float textLength = 0f;
            foreach (var line in lines) textLength = Mathf.Max(textLength, WidthEm(line, LetterSpacing) * em);
            // Si el texto es más largo que el país, la curva se endereza para no exagerar la parábola al prolongarla.
            if (textLength > span * 1.1f) { c1 = 0; c2 = 0; }

            // --- Tabla de longitud de arco para repartir las letras a lo largo de la curva.
            const int steps = 96;
            float range = Mathf.Max(1.2f, textLength / span * 1.3f);
            var arcU = new float[steps + 1];
            var arcLen = new float[steps + 1];
            Vector2 prev = Curve(-range);
            for (int i = 0; i <= steps; i++)
            {
                float u = Mathf.Lerp(-range, range, i / (float)steps);
                var p = Curve(u);
                arcU[i] = u;
                arcLen[i] = i == 0 ? 0f : arcLen[i - 1] + Vector2.Distance(prev, p);
                prev = p;
            }
            float centerArc = ArcAt(arcU, arcLen, 0f);

            float worldPerPixel = _map.WorldSize.x / width;
            var center = Curve(0f);
            var anchor3 = ToWorld(center, worldPerPixel);
            var anchor = new Vector2(anchor3.x, anchor3.z); // centro del nombre: el shader lo escala a su alrededor

            // --- Versión normal: letras sobre la curva.
            var normalSet = BeginSet(vertices, em, worldPerPixel);
            float lineStep = em * (CapHeight + 0.45f);
            for (int li = 0; li < lines.Length; li++)
            {
                string line = lines[li];
                // Desplazamiento perpendicular de la línea respecto a la curva (centrado el bloque de texto).
                float lineOffset = (lines.Length - 1) * 0.5f * lineStep - li * lineStep;
                float pen = centerArc - WidthEm(line, LetterSpacing) * em * 0.5f;
                foreach (char ch in line)
                {
                    if (!_font.GetCharacterInfo(ch, out var ci, FontSize, FontStyle.Normal)) continue;
                    float k = em / FontSize;
                    float glyphMid = (ci.minX + ci.maxX) * 0.5f * k;
                    float u = UAt(arcU, arcLen, pen + glyphMid);
                    var tangent = (Curve(u + 0.01f) - Curve(u - 0.01f)).normalized;
                    var up = new Vector2(-tangent.y, tangent.x);
                    AddGlyph(ci, em, glyphMid, Curve(u) + up * lineOffset, tangent, up, normalSet.CapWorld, 0f, anchor, worldPerPixel,
                        vertices, uvs, info, anchors, indices);
                    pen += ci.advance * k + LetterSpacing * em;
                }
            }
            EndSet(ref normalSet, vertices);
            if (normalSet.VertexCount == 0) return;

            // --- Versión compacta: recta (sin curva), letras más juntas, ajustada a la longitud y al grosor del
            // país sobre su mismo eje (así un país largo y estrecho, como Chile, la aprovecha en vertical).
            var compactLines = FitLines(text, CompactLetterSpacing, span * 0.95f, thickness * 0.9f, thickness * 0.9f, out float compactEm);
            compactEm = Mathf.Clamp(compactEm, 0.25f, MaxEmPixels);
            var compactSet = BeginSet(vertices, compactEm, worldPerPixel);
            float compactStep = compactEm * (CapHeight + 0.35f);
            for (int li = 0; li < compactLines.Length; li++)
            {
                string line = compactLines[li];
                float lineOffset = (compactLines.Length - 1) * 0.5f * compactStep - li * compactStep;
                float pen = -WidthEm(line, CompactLetterSpacing) * compactEm * 0.5f;
                foreach (char ch in line)
                {
                    if (!_font.GetCharacterInfo(ch, out var ci, FontSize, FontStyle.Normal)) continue;
                    float k = compactEm / FontSize;
                    float glyphMid = (ci.minX + ci.maxX) * 0.5f * k;
                    AddGlyph(ci, compactEm, glyphMid, center + axis * (pen + glyphMid) + normal * lineOffset, axis, normal,
                        compactSet.CapWorld, 1f, anchor, worldPerPixel, vertices, uvs, info, anchors, indices);
                    pen += ci.advance * k + CompactLetterSpacing * compactEm;
                }
            }
            EndSet(ref compactSet, vertices);

            float u01 = Mathf.Repeat(anchor.x / _map.WorldSize.x, 1f);
            _labels.Add(new Label
            {
                Normal = normalSet,
                Compact = compactSet,
                Anchor = anchor,
                Height = _map.GetTerrainHeight(u01, Mathf.Clamp01(anchor.y / _map.WorldSize.y)),
                Priority = normalSet.CapWorld * textLength * lines.Length * worldPerPixel,
            });
        }

        /// <summary>
        /// Tamaño (em, en píxeles del mapa) y reparto en líneas de un texto para un hueco de longitud y grosor dados.
        /// Los nombres de varias palabras se parten en dos líneas si así caben bastante más grandes.
        /// </summary>
        private string[] FitLines(string text, float spacing, float length, float thickness1, float thickness2, out float em)
        {
            em = Mathf.Min(length / Mathf.Max(WidthEm(text, spacing), 1e-3f), thickness1 / CapHeight, MaxEmPixels);
            int split = BestSplit(text);
            if (split <= 0) return new[] { text };
            string l1 = text.Substring(0, split), l2 = text.Substring(split + 1);
            float w2 = Mathf.Max(WidthEm(l1, spacing), WidthEm(l2, spacing));
            const float lineGap = 0.45f; // separación entre líneas (en em)
            float em2 = Mathf.Min(length / Mathf.Max(w2, 1e-3f), thickness2 / (CapHeight * 2f + lineGap), MaxEmPixels);
            if (em2 <= em * 1.3f) return new[] { text };
            em = em2;
            return new[] { l1, l2 };
        }

        private GlyphSet BeginSet(List<Vector3> vertices, float em, float worldPerPixel) => new GlyphSet
        {
            FirstVertex = vertices.Count,
            FirstGlyph = _glyphCenters.Count,
            CapWorld = em * CapHeight * worldPerPixel,
        };

        private void EndSet(ref GlyphSet set, List<Vector3> vertices)
        {
            set.VertexCount = vertices.Count - set.FirstVertex;
            set.GlyphCount = _glyphCenters.Count - set.FirstGlyph;
        }

        /// <summary>Añade el quad de una letra centrada en c (píxeles del mapa) con su orientación.</summary>
        private void AddGlyph(CharacterInfo ci, float em, float glyphMid, Vector2 c, Vector2 tangent, Vector2 up, float capWorld, float kind,
            Vector2 anchor, float worldPerPixel, List<Vector3> vertices, List<Vector2> uvs, List<Vector2> info, List<Vector2> anchors, List<int> indices)
        {
            if (ci.maxX <= ci.minX || ci.maxY <= ci.minY) return;
            float k = em / FontSize;
            float x0 = ci.minX * k - glyphMid, x1 = ci.maxX * k - glyphMid;
            float y0 = ci.minY * k - em * CapHeight * 0.5f, y1 = ci.maxY * k - em * CapHeight * 0.5f;
            int baseIndex = vertices.Count;
            vertices.Add(ToWorld(c + tangent * x0 + up * y0, worldPerPixel));
            vertices.Add(ToWorld(c + tangent * x1 + up * y0, worldPerPixel));
            vertices.Add(ToWorld(c + tangent * x1 + up * y1, worldPerPixel));
            vertices.Add(ToWorld(c + tangent * x0 + up * y1, worldPerPixel));
            uvs.Add(ci.uvBottomLeft);
            uvs.Add(ci.uvBottomRight);
            uvs.Add(ci.uvTopRight);
            uvs.Add(ci.uvTopLeft);
            // uv1: x = altura de la letra en el mundo, y = 0 normal / 1 compacta.
            for (int i = 0; i < 4; i++) { info.Add(new Vector2(capWorld, kind)); anchors.Add(anchor); }
            var glyphCenter = ToWorld(c, worldPerPixel);
            _glyphCenters.Add(new Vector2(glyphCenter.x, glyphCenter.z));
            indices.Add(baseIndex); indices.Add(baseIndex + 2); indices.Add(baseIndex + 1);
            indices.Add(baseIndex); indices.Add(baseIndex + 3); indices.Add(baseIndex + 2);
        }

        /// <summary>Anchura del texto en "em" (incluido el espaciado entre letras).</summary>
        private float WidthEm(string text, float spacing)
        {
            float advance = 0f;
            int glyphs = 0;
            foreach (char ch in text)
            {
                if (!_font.GetCharacterInfo(ch, out var ci, FontSize, FontStyle.Normal)) continue;
                advance += ci.advance;
                glyphs++;
            }
            return glyphs == 0 ? 0f : advance / FontSize + spacing * (glyphs - 1);
        }

        /// <summary>Índice del espacio que mejor parte el texto en dos líneas parecidas (-1 si no hay).</summary>
        private static int BestSplit(string text)
        {
            int best = -1;
            float bestDiff = float.MaxValue;
            for (int i = 1; i < text.Length - 1; i++)
            {
                if (text[i] != ' ') continue;
                float diff = Mathf.Abs(i - (text.Length - 1 - i));
                if (diff < bestDiff) { bestDiff = diff; best = i; }
            }
            return best;
        }

        private static Vector3 ToWorld(Vector2 pixel, float worldPerPixel) =>
            new Vector3((pixel.x + 0.5f) * worldPerPixel, 0f, (pixel.y + 0.5f) * worldPerPixel);

        /// <summary>
        /// Masas de tierra (componentes conexas de provincias) que llevan nombre: la de la capital siempre y las
        /// demás si son grandes (al menos un 30 % de la mayor y no diminutas). Máximo 4 por país.
        /// </summary>
        private List<List<int>> LabelledLandmasses(Country country)
        {
            var visited = new HashSet<int>();
            var comps = new List<(List<int> ids, long pixels, bool capital)>();
            foreach (int start in country.Provinces)
            {
                if (visited.Contains(start)) continue;
                var comp = new List<int>();
                var queue = new Queue<int>();
                queue.Enqueue(start);
                visited.Add(start);
                long pixels = 0;
                bool hasCapital = false;
                while (queue.Count > 0)
                {
                    int id = queue.Dequeue();
                    var p = _world.GetProvince(id);
                    comp.Add(id);
                    pixels += p.PixelCount;
                    if (id == country.CapitalProvinceId) hasCapital = true;
                    foreach (int nb in p.Neighbors)
                    {
                        var np = _world.GetProvince(nb);
                        if (np == null || np.OwnerId != country.Id || visited.Contains(nb)) continue;
                        visited.Add(nb);
                        queue.Enqueue(nb);
                    }
                }
                comps.Add((comp, pixels, hasCapital));
            }
            comps.Sort((x, y) => y.pixels.CompareTo(x.pixels));
            var result = new List<List<int>>();
            if (comps.Count == 0) return result;
            long largest = comps[0].pixels;
            foreach (var c in comps)
                if (c.capital) { result.Add(c.ids); break; }
            foreach (var c in comps)
            {
                if (result.Count >= 4) break;
                if (c.capital) continue;
                if (c.pixels >= largest * 0.3f && c.pixels >= 1500) result.Add(c.ids);
            }
            if (result.Count == 0) result.Add(comps[0].ids);
            return result;
        }

        /// <summary>
        /// Píxeles (muestreados) de una masa de tierra. Las coordenadas X se "desenrollan" alrededor de su primera
        /// provincia para que un territorio partido por el antimeridiano quede contiguo.
        /// </summary>
        private List<Vector2> SamplePixels(Country country, List<int> chosen)
        {
            var map = _world.Map;
            int width = map.Width;
            var points = new List<Vector2>();
            long total = 0;
            foreach (int id in chosen) total += _world.GetProvince(id).PixelCount;
            int step = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(total / 5000f)));
            var refProvince = _world.GetProvince(country.CapitalProvinceId);
            if (refProvince == null || !chosen.Contains(refProvince.Id)) refProvince = _world.GetProvince(chosen[0]);
            float x0 = refProvince.Center.x;

            foreach (int id in chosen)
            {
                var p = _world.GetProvince(id);
                var b = p.Bounds;
                for (int y = b.yMin; y < b.yMax; y += step)
                for (int x = b.xMin; x < b.xMax; x += step)
                {
                    int px = ((x % width) + width) % width;
                    if (y < 0 || y >= map.Height || map.ProvinceIds[y * width + px] != id) continue;
                    float fx = px;
                    float dx = fx - x0;
                    if (dx > width * 0.5f) fx -= width;
                    else if (dx < -width * 0.5f) fx += width;
                    points.Add(new Vector2(fx, y));
                }
            }
            return points;
        }

        private static float ArcAt(float[] us, float[] arcs, float u)
        {
            for (int i = 1; i < us.Length; i++)
                if (us[i] >= u) return Mathf.Lerp(arcs[i - 1], arcs[i], Mathf.InverseLerp(us[i - 1], us[i], u));
            return arcs[arcs.Length - 1];
        }

        private static float UAt(float[] us, float[] arcs, float arc)
        {
            for (int i = 1; i < arcs.Length; i++)
                if (arcs[i] >= arc) return Mathf.Lerp(us[i - 1], us[i], Mathf.InverseLerp(arcs[i - 1], arcs[i], arc));
            return us[us.Length - 1];
        }

        /// <summary>Resuelve el sistema simétrico 3x3 de las ecuaciones normales (regla de Cramer).</summary>
        private static void Solve3(double a, double b, double c, double d, double e, double f,
            double r0, double r1, double r2, out double x0, out double x1, out double x2)
        {
            // | a b c |       (b = m01, c = m02, d = m11, e = m12, f = m22)
            // | b d e |
            // | c e f |
            double det = a * (d * f - e * e) - b * (b * f - e * c) + c * (b * e - d * c);
            if (System.Math.Abs(det) < 1e-9)
            {
                x0 = a > 0 ? r0 / a : 0; x1 = 0; x2 = 0;
                return;
            }
            x0 = (r0 * (d * f - e * e) - b * (r1 * f - e * r2) + c * (r1 * e - d * r2)) / det;
            x1 = (a * (r1 * f - e * r2) - r0 * (b * f - e * c) + c * (b * r2 - r1 * c)) / det;
            x2 = (a * (d * r2 - r1 * e) - b * (b * r2 - r1 * c) + r0 * (b * e - d * c)) / det;
        }
    }
}
