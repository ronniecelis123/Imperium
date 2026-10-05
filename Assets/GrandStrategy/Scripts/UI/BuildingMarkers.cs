using System;
using System.Collections.Generic;
using System.Text;
using GrandStrategy.Core;
using GrandStrategy.Economy;
using GrandStrategy.Map;
using GrandStrategy.Map.Rendering;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GrandStrategy.UI
{
    /// <summary>
    /// Símbolos de edificios y obras sobre el mapa. Es una representación más de los datos existentes: lee los
    /// edificios de cada provincia (Province.Buildings) y las obras de cada país (CountryEconomy.Projects, los mismos
    /// objetos ConstructionProject que muestra Industria Nacional › Proyectos), sin listas ni progreso propios.
    /// <list type="bullet">
    /// <item><b>Modo de mapa «Edificios»</b>: todo. De cerca, un icono por tipo con su cantidad (las obras con el
    /// icono atenuado o una grúa/flecha y una barra de progreso); de lejos, un símbolo por provincia con el total.</item>
    /// <item><b>Resto de modos</b> (y el modo construcción): solo las obras del jugador, de forma discreta: icono de lo
    /// que se construye (o grúa ×N si hay varias) y una barra de progreso; de lejos, sin barra.</item>
    /// </list>
    /// Los símbolos que se pisarían (entre sí o con los nombres de las ciudades) se ocultan, dando prioridad a las
    /// provincias del jugador, a las que tienen más edificios u obras y a las cercanas al centro de la pantalla.
    /// Los datos se recalculan solo cuando cambian (obra encargada, terminada, cancelada...) y las barras solo cuando
    /// avanza el día. No reciben clics de la interfaz: el ratón se detecta a mano para no bloquear el zoom ni el mapa;
    /// un clic sobre una obra propia abre Proyectos con esa obra señalada.
    /// </summary>
    [DefaultExecutionOrder(100)] // después de CityMarkers, para conocer el sitio que ocupan sus iconos y nombres
    public sealed class BuildingMarkers : MonoBehaviour
    {
        /// <summary>Zoom (0 cerca, 1 lejos) por debajo del cual se ve un icono por tipo de edificio.</summary>
        private const float DetailZoom = 0.34f;
        /// <summary>Zoom por encima del cual los indicadores de obra se simplifican (sin barra).</summary>
        private const float WorksCompactZoom = 0.62f;
        private const float ChipHeight = 22f, ChipIcon = 16f, BadgeSize = 11f;
        private const float AggregateHeight = 20f, AggregateIcon = 14f;
        private const float WorksIcon = 18f, BarHeight = 4f;
        private const int MaxChips = 6;
        private const float CellSize = 64f;
        private const float TooltipRefresh = 0.25f;

        // Placas ligeras (símbolo cartográfico más que ventana): poco fondo, sin marco salvo un filo verde en lo propio.
        private static readonly Color PlateColor = new Color(0.05f, 0.055f, 0.05f, 0.6f);
        private static readonly Color PlayerPlateColor = new Color(0.06f, 0.12f, 0.05f, 0.7f);
        private static readonly Color EconomicColor = UITheme.Accent;
        private static readonly Color MilitaryColor = new Color(0.92f, 0.52f, 0.44f);
        private static readonly Color WorkColor = new Color(1f, 0.83f, 0.3f);
        private static readonly Color BarBack = new Color(0f, 0f, 0f, 0.6f);

        /// <summary>El ratón está sobre un símbolo (el tooltip de provincia se oculta para no duplicar).</summary>
        public static bool IsHovering { get; private set; }

        /// <summary>El jugador ha pulsado una obra suya en el mapa: hay que abrir Proyectos señalándola.</summary>
        public static event Action<ConstructionProject> OpenProjectRequested;

        private enum Mode { Off, Full, Works }
        private enum Kind { Detail, Aggregate, Works }

        private sealed class Group
        {
            public BuildingType Type;
            public int Count;
            public readonly int[] Levels = new int[EconomyRules.MaxBuildingLevel + 1];
            public readonly List<ConstructionProject> Works = new List<ConstructionProject>();
            public int NewWorks;
        }

        private sealed class Entry
        {
            public Province Province;
            public Vector3 World;
            public readonly List<Group> Groups = new List<Group>();
            /// <summary>Obras del jugador en la provincia (las mismas instancias que en Proyectos).</summary>
            public readonly List<ConstructionProject> PlayerWorks = new List<ConstructionProject>();
            public int Total, Works, Hash, Version;
            public BuildingType Main;
            public bool Used, Own;
            public View View;
        }

        private sealed class Chip
        {
            /// <summary>Rectángulo relativo al centro del símbolo.</summary>
            public Rect Local;
            /// <summary>Tipo de edificio (null = chip "+N" con el resto).</summary>
            public BuildingType? Type;
        }

        /// <summary>
        /// Barra de progreso de un símbolo. Si resume varias obras, muestra la de más prioridad en la cola del país
        /// (la primera en Proyectos), que es la que recibe antes la capacidad de construcción.
        /// </summary>
        private struct Bar
        {
            public RectTransform Fill;
            public List<ConstructionProject> Works;
            public CountryEconomy Economy;
        }

        private sealed class View
        {
            public RectTransform Root, Detail, Aggregate, Works;
            public Vector2 DetailSize, AggregateSize, WorksSize;
            public readonly List<Chip> Chips = new List<Chip>();
            public readonly List<Bar> DetailBars = new List<Bar>(), AggregateBars = new List<Bar>(), WorksBars = new List<Bar>();
            public RectTransform WorksBar;
            public int DetailVersion = -1, AggregateVersion = -1, WorksVersion = -1;
            public int BarStamp = -1;
        }

        private struct Candidate
        {
            public Entry Entry;
            public Vector2 Pos;
            public float Score;
        }

        private struct Placed
        {
            public Entry Entry;
            public Rect Rect;
            public Kind Kind;
        }

        private GameManager _game;
        private RectTransform _rect;
        private Transform _canvas;
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private readonly List<Candidate> _candidates = new List<Candidate>();
        private readonly List<Placed> _placed = new List<Placed>();
        private readonly List<int> _remove = new List<int>();
        private List<Rect>[] _grid = new List<Rect>[0];
        private int _gridCols, _gridRows;
        private Vector2 _gridOrigin;
        private bool _dirty = true;
        private Mode _shownMode = Mode.Off;
        private int _progressStamp;
        private Dictionary<BuildingType, int> _order;

        // Tooltip
        private Entry _hoverEntry;
        private BuildingType? _hoverType;
        private bool _hoverAll;
        private Kind _hoverKind;
        private float _tooltipTimer;

        public static BuildingMarkers Create(Transform parent, GameManager game)
        {
            var rt = UIFactory.Rect("BuildingMarkers", parent).Stretch();
            rt.SetAsFirstSibling(); // por debajo de las ciudades y del resto de la interfaz
            var markers = rt.gameObject.AddComponent<BuildingMarkers>();
            markers._game = game;
            markers._rect = rt;
            markers._canvas = rt.GetComponentInParent<Canvas>().rootCanvas.transform;
            markers._order = new Dictionary<BuildingType, int>();
            for (int i = 0; i < BuildingCatalog.All.Length; i++) markers._order[BuildingCatalog.All[i].Type] = i;
            game.Map.MapModeChanged += markers.OnMapModeChanged;
            game.Economy.Changed += markers.OnEconomyChanged;
            game.Economy.Updated += markers.OnEconomyChanged;
            game.Economy.DayProcessed += markers.OnDayProcessed;
            game.Economy.BuildingCompleted += markers.OnBuildingCompleted;
            game.MapClickInterceptor = markers.OnMapClick;
            return markers;
        }

        private void OnDestroy()
        {
            if (_game == null) return;
            if (_game.Map != null) _game.Map.MapModeChanged -= OnMapModeChanged;
            if (_game.Economy != null)
            {
                _game.Economy.Changed -= OnEconomyChanged;
                _game.Economy.Updated -= OnEconomyChanged;
                _game.Economy.DayProcessed -= OnDayProcessed;
                _game.Economy.BuildingCompleted -= OnBuildingCompleted;
            }
            if (_game.MapClickInterceptor == (Func<Province, bool>)OnMapClick) _game.MapClickInterceptor = null;
        }

        // Obras encargadas, canceladas o reordenadas: cambian los datos (y las prioridades del tooltip).
        private void OnEconomyChanged()
        {
            _dirty = true;
            _progressStamp++;
        }

        // Cada día solo avanzan las barras: no se reconstruye ningún símbolo.
        private void OnDayProcessed() => _progressStamp++;
        private void OnBuildingCompleted(Country c, ConstructionProject p, Province province) => _dirty = true;
        private void OnMapModeChanged(IMapMode mode) => _dirty = true;

        // ================================================================== Datos

        /// <summary>Agrupa los edificios terminados y las obras en curso de cada provincia (solo cuando algo cambia).</summary>
        private void RebuildData()
        {
            _dirty = false;
            var world = _game.World;
            var player = _game.PlayerCountry;
            int playerId = player != null ? player.Id : -1;
            foreach (var e in _entries.Values)
            {
                e.Used = false;
                e.Groups.Clear();
                e.PlayerWorks.Clear();
                e.Total = e.Works = 0;
            }

            foreach (var p in world.Provinces)
            {
                if (p.IsWater || p.Buildings.Count == 0) continue;
                var e = GetEntry(p);
                foreach (var b in p.Buildings)
                {
                    var g = GetGroup(e, b.Type);
                    g.Count++;
                    g.Levels[Mathf.Clamp(b.Level, 1, EconomyRules.MaxBuildingLevel)]++;
                    e.Total++;
                }
            }
            foreach (var c in world.Countries)
            {
                if (c.Economy == null) continue;
                foreach (var proj in c.Economy.Projects)
                {
                    var p = world.GetProvince(proj.ProvinceId);
                    if (p == null || p.IsWater) continue;
                    var e = GetEntry(p);
                    var g = GetGroup(e, proj.Building.Type);
                    g.Works.Add(proj);
                    if (!proj.IsUpgrade) g.NewWorks++;
                    e.Works++;
                    if (c == player) e.PlayerWorks.Add(proj);
                }
            }

            _remove.Clear();
            foreach (var pair in _entries)
            {
                var e = pair.Value;
                if (!e.Used)
                {
                    _remove.Add(pair.Key);
                    continue;
                }
                e.Groups.Sort((a, b) => _order[a.Type].CompareTo(_order[b.Type]));
                e.Own = e.Province.OwnerId == playerId && playerId >= 0;
                int hash = e.Own ? 1 : 0, best = -1;
                foreach (var g in e.Groups)
                {
                    hash = hash * 31 + (int)g.Type;
                    hash = hash * 31 + g.Count;
                    for (int l = 1; l < g.Levels.Length; l++) hash = hash * 31 + g.Levels[l];
                    hash = hash * 31 + g.Works.Count;
                    hash = hash * 31 + g.NewWorks;
                    if (g.Count > best) { best = g.Count; e.Main = g.Type; }
                }
                foreach (var w in e.PlayerWorks) hash = hash * 31 + (int)w.Building.Type * 7 + w.TargetLevel;
                if (hash != e.Hash || e.Version == 0)
                {
                    e.Hash = hash;
                    e.Version++;
                }
            }
            foreach (int id in _remove)
            {
                var e = _entries[id];
                if (e.View != null) Destroy(e.View.Root.gameObject);
                if (_hoverEntry == e) ClearHover();
                _entries.Remove(id);
            }
        }

        private Entry GetEntry(Province p)
        {
            if (!_entries.TryGetValue(p.Id, out var e))
            {
                e = new Entry { Province = p };
                e.World = _game.Map.GetProvinceWorldPosition(p);
                e.World.y += 0.02f;
                _entries[p.Id] = e;
            }
            e.Used = true;
            return e;
        }

        private static Group GetGroup(Entry e, BuildingType type)
        {
            foreach (var g in e.Groups) if (g.Type == type) return g;
            var group = new Group { Type = type };
            e.Groups.Add(group);
            return group;
        }

        private static Group FindGroup(Entry e, BuildingType type)
        {
            foreach (var g in e.Groups) if (g.Type == type) return g;
            return null;
        }

        // ================================================================== Dibujo

        private Mode CurrentMode()
        {
            var state = _game.State;
            if (_game.CameraRig == null || (state != GameState.Playing && state != GameState.Paused)) return Mode.Off;
            return _game.Map.MapMode is BuildingsMapMode ? Mode.Full : Mode.Works;
        }

        private void LateUpdate()
        {
            var mode = CurrentMode();
            if (mode != _shownMode)
            {
                HideAll();
                _shownMode = mode;
            }
            if (mode == Mode.Off) return;
            if (_dirty) RebuildData();

            var rig = _game.CameraRig;
            var cam = rig.Camera;
            float zoom = rig.Zoom01;
            float worldWidth = _game.Map.WorldSize.x;
            float camX = cam.transform.position.x;
            Vector2 canvasSize = _rect.rect.size;
            float scale = canvasSize.y / Mathf.Max(1f, Screen.height);
            bool full = mode == Mode.Full;
            bool detail = full && zoom < DetailZoom;
            bool compactWorks = !full && zoom >= WorksCompactZoom;
            float far = Mathf.InverseLerp(DetailZoom, 1f, zoom);
            // Al alejarse se dejan más separados y se muestran menos (los más importantes).
            float pad = detail ? 3f : Mathf.Lerp(6f, 22f, far);
            int maxVisible = full ? (detail ? 500 : Mathf.RoundToInt(Mathf.Lerp(180f, 30f, far))) : (compactWorks ? 40 : 200);
            float maxDist = canvasSize.magnitude * 0.5f;

            // 1. Provincias en pantalla, ordenadas por importancia (copia del mundo más cercana a la cámara).
            _candidates.Clear();
            foreach (var e in _entries.Values)
            {
                if (!full && e.PlayerWorks.Count == 0)
                {
                    Hide(e);
                    continue;
                }
                var world = e.World;
                world.x += Mathf.Round((camX - world.x) / worldWidth) * worldWidth;
                Vector3 screen = cam.WorldToScreenPoint(world);
                if (screen.z <= 0f || screen.x < -80f || screen.x > Screen.width + 80f || screen.y < -40f || screen.y > Screen.height + 40f)
                {
                    Hide(e);
                    continue;
                }
                Vector2 pos = new Vector2(screen.x, screen.y) * scale - canvasSize * 0.5f;
                float importance = full ? (e.Total + e.Works * 0.75f) * (e.Own ? 3f : 1f) + 1f : e.PlayerWorks.Count + 1f;
                float centre = 1f - 0.6f * Mathf.Clamp01(pos.magnitude / maxDist);
                _candidates.Add(new Candidate { Entry = e, Pos = pos, Score = importance * centre });
            }
            _candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

            // 2. Colocación sin solapes (la ocupación de los nombres de ciudades se reserva primero).
            ResetGrid(canvasSize);
            foreach (var r in CityMarkers.Occupied) Occupy(r);
            _placed.Clear();
            foreach (var cand in _candidates)
            {
                var e = cand.Entry;
                if (_placed.Count >= maxVisible)
                {
                    Hide(e);
                    continue;
                }
                var view = EnsureView(e);
                Kind kind = Kind.Aggregate;
                bool ok = false;
                Rect rect = default;
                if (!full)
                {
                    EnsureWorks(e);
                    // De lejos, sin barra: la placa se encoge y se baja para que el icono quede centrado.
                    view.WorksBar.gameObject.SetActive(!compactWorks);
                    Vector2 size = compactWorks ? new Vector2(view.WorksSize.x, view.WorksSize.y - BarHeight - 2f) : view.WorksSize;
                    view.Works.sizeDelta = size;
                    view.Works.anchoredPosition = new Vector2(0f, compactWorks ? -(BarHeight + 2f) * 0.5f : 0f);
                    float worksScale = compactWorks ? 0.85f : zoom < 0.25f ? 1.1f : 1f;
                    view.Works.localScale = Vector3.one * worksScale;
                    size *= worksScale;
                    ok = TryPlace(cand.Pos, size, pad, out rect);
                    kind = Kind.Works;
                }
                else
                {
                    if (detail)
                    {
                        EnsureDetail(e);
                        ok = TryPlace(cand.Pos, view.DetailSize, pad, out rect);
                        kind = Kind.Detail;
                    }
                    if (!ok)
                    {
                        EnsureAggregate(e);
                        ok = TryPlace(cand.Pos, view.AggregateSize, pad, out rect);
                        kind = Kind.Aggregate;
                    }
                }
                if (!ok)
                {
                    Hide(e);
                    continue;
                }
                if (!view.Root.gameObject.activeSelf) view.Root.gameObject.SetActive(true);
                view.Root.anchoredPosition = rect.center;
                SetActive(view.Detail, kind == Kind.Detail);
                SetActive(view.Aggregate, kind == Kind.Aggregate);
                SetActive(view.Works, kind == Kind.Works);
                if (view.BarStamp != _progressStamp) UpdateBars(view);
                _placed.Add(new Placed { Entry = e, Rect = rect, Kind = kind });
            }

            UpdateHover(canvasSize, scale);
        }

        private static void SetActive(RectTransform rt, bool active)
        {
            if (rt != null && rt.gameObject.activeSelf != active) rt.gameObject.SetActive(active);
        }

        private void HideAll()
        {
            foreach (var e in _entries.Values) Hide(e);
            _placed.Clear();
            ClearHover();
        }

        private static void Hide(Entry e)
        {
            if (e.View != null && e.View.Root.gameObject.activeSelf) e.View.Root.gameObject.SetActive(false);
        }

        /// <summary>Busca un hueco libre junto al centro de la provincia: encima, debajo, a la derecha o a la izquierda.</summary>
        private bool TryPlace(Vector2 pos, Vector2 size, float pad, out Rect rect)
        {
            float dx = size.x * 0.5f + 14f, dy = size.y * 0.5f + 12f;
            for (int i = 0; i < 5; i++)
            {
                Vector2 offset = i switch
                {
                    0 => Vector2.zero,
                    1 => new Vector2(0f, -dy),
                    2 => new Vector2(0f, dy),
                    3 => new Vector2(dx, 0f),
                    _ => new Vector2(-dx, 0f),
                };
                rect = new Rect(pos + offset - size * 0.5f, size);
                var padded = new Rect(rect.x - pad, rect.y - pad, rect.width + pad * 2f, rect.height + pad * 2f);
                if (IsFree(padded))
                {
                    Occupy(padded);
                    return true;
                }
            }
            rect = default;
            return false;
        }

        // --- Rejilla de ocupación de la pantalla (evita comparar cada símbolo con todos los demás) ---

        private void ResetGrid(Vector2 canvasSize)
        {
            int cols = Mathf.CeilToInt(canvasSize.x / CellSize) + 4, rows = Mathf.CeilToInt(canvasSize.y / CellSize) + 4;
            if (cols * rows != _grid.Length)
            {
                _grid = new List<Rect>[cols * rows];
                for (int i = 0; i < _grid.Length; i++) _grid[i] = new List<Rect>();
            }
            _gridCols = cols;
            _gridRows = rows;
            _gridOrigin = -canvasSize * 0.5f - new Vector2(CellSize * 2f, CellSize * 2f);
            foreach (var cell in _grid) cell.Clear();
        }

        private void CellRange(Rect r, out int x0, out int y0, out int x1, out int y1)
        {
            x0 = Mathf.Clamp(Mathf.FloorToInt((r.xMin - _gridOrigin.x) / CellSize), 0, _gridCols - 1);
            x1 = Mathf.Clamp(Mathf.FloorToInt((r.xMax - _gridOrigin.x) / CellSize), 0, _gridCols - 1);
            y0 = Mathf.Clamp(Mathf.FloorToInt((r.yMin - _gridOrigin.y) / CellSize), 0, _gridRows - 1);
            y1 = Mathf.Clamp(Mathf.FloorToInt((r.yMax - _gridOrigin.y) / CellSize), 0, _gridRows - 1);
        }

        private bool IsFree(Rect r)
        {
            CellRange(r, out int x0, out int y0, out int x1, out int y1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                foreach (var other in _grid[y * _gridCols + x])
                    if (other.Overlaps(r)) return false;
            return true;
        }

        private void Occupy(Rect r)
        {
            CellRange(r, out int x0, out int y0, out int x1, out int y1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                _grid[y * _gridCols + x].Add(r);
        }

        // ================================================================== Barras de progreso

        /// <summary>Barra fina: fondo oscuro y relleno dorado. Devuelve el relleno (su anchura = progreso).</summary>
        private static RectTransform CreateBar(Transform parent, Vector2 position, float width)
        {
            var back = UIFactory.Panel(parent, "Progreso", BarBack, false);
            back.raycastTarget = false;
            var rt = back.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, BarHeight);
            rt.anchoredPosition = position;
            var fill = UIFactory.Panel(back.transform, "Relleno", WorkColor, false);
            fill.raycastTarget = false;
            var f = fill.rectTransform;
            f.anchorMin = Vector2.zero;
            f.anchorMax = new Vector2(0f, 1f);
            f.offsetMin = f.offsetMax = Vector2.zero;
            return f;
        }

        /// <summary>Obra de más prioridad (primera en la cola de Proyectos del país) de una lista.</summary>
        private static ConstructionProject PrimaryWork(List<ConstructionProject> works, CountryEconomy economy)
        {
            ConstructionProject best = null;
            int bestIndex = int.MaxValue;
            foreach (var w in works)
            {
                int index = economy != null ? economy.Projects.IndexOf(w) : 0;
                if (index < 0) index = int.MaxValue - 1;
                if (best == null || index < bestIndex)
                {
                    best = w;
                    bestIndex = index;
                }
            }
            return best;
        }

        private void UpdateBars(View view)
        {
            view.BarStamp = _progressStamp;
            foreach (var list in new[] { view.DetailBars, view.AggregateBars, view.WorksBars })
                foreach (var bar in list)
                {
                    if (bar.Fill == null) continue;
                    var primary = PrimaryWork(bar.Works, bar.Economy);
                    // Mismo dato que la barra de Proyectos: ConstructionProject.Progress01.
                    bar.Fill.anchorMax = new Vector2(primary != null ? Mathf.Clamp01(primary.Progress01) : 0f, 1f);
                }
        }

        private CountryEconomy OwnerEconomy(Entry e) => _game.World.GetOwner(e.Province)?.Economy;

        /// <summary>Icono con una sombra fina: se lee sobre cualquier terreno aunque la placa sea ligera.</summary>
        private static Image MarkerIcon(Transform parent, string icon, Color color)
        {
            var img = UIFactory.Icon(parent, icon, color);
            var shadow = img.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = new Vector2(1f, -1f);
            return img;
        }

        // ================================================================== Construcción de los símbolos

        private View EnsureView(Entry e)
        {
            if (e.View != null) return e.View;
            var root = UIFactory.Rect("Edificios_" + e.Province.Name, _rect);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.zero;
            root.gameObject.SetActive(false);
            e.View = new View { Root = root };
            return e.View;
        }

        private static void DestroyContainer(ref RectTransform container)
        {
            if (container == null) return;
            container.gameObject.SetActive(false);
            Destroy(container.gameObject);
            container = null;
        }

        /// <summary>Fila de iconos (uno por tipo de edificio) con su cantidad; los tipos con obras llevan barra.</summary>
        private void EnsureDetail(Entry e)
        {
            var view = e.View;
            if (view.DetailVersion == e.Version && view.Detail != null) return;
            view.DetailVersion = e.Version;
            view.BarStamp = -1;
            DestroyContainer(ref view.Detail);
            view.Chips.Clear();
            view.DetailBars.Clear();

            var plate = CreatePlate(view.Root, "Detalle", e.Own);
            view.Detail = plate.rectTransform;

            // Primero se miden los chips, después se centran.
            int shown = e.Groups.Count > MaxChips ? MaxChips - 1 : e.Groups.Count;
            float x = 3f;
            var items = new List<(RectTransform rt, float width, Group group)>();
            for (int i = 0; i < shown; i++)
            {
                var g = e.Groups[i];
                var def = BuildingCatalog.Get(g.Type);
                var chip = UIFactory.Rect(def.Name, view.Detail);
                chip.anchorMin = chip.anchorMax = new Vector2(0.5f, 0.5f);
                chip.pivot = new Vector2(0f, 0.5f);
                bool built = g.Count > 0;
                var color = def.Category == BuildingCategory.Military ? MilitaryColor : EconomicColor;
                if (!built) color.a = 0.5f; // solo en obras: icono atenuado
                var icon = MarkerIcon(chip, def.Icon, color);
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                icon.rectTransform.pivot = new Vector2(0f, 0.5f);
                icon.rectTransform.sizeDelta = new Vector2(ChipIcon, ChipIcon);
                icon.rectTransform.anchoredPosition = new Vector2(2f, g.Works.Count > 0 ? 1.5f : 0f);
                float w = ChipIcon + 4f;

                int number = built ? g.Count : g.Works.Count;
                if (number > 1)
                {
                    var count = UIFactory.Label(chip, built ? number.ToString() : $"({number})", 13, built ? UITheme.Text : WorkColor,
                        TextAnchor.MiddleLeft, FontStyle.Bold);
                    count.horizontalOverflow = HorizontalWrapMode.Overflow;
                    float tw = Mathf.Ceil(count.preferredWidth);
                    count.rectTransform.anchorMin = count.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                    count.rectTransform.pivot = new Vector2(0f, 0.5f);
                    count.rectTransform.sizeDelta = new Vector2(tw + 2f, ChipHeight);
                    count.rectTransform.anchoredPosition = new Vector2(w, g.Works.Count > 0 ? 1.5f : 0f);
                    w += tw + 3f;
                }
                if (g.Works.Count > 0)
                {
                    // Grúa = obra nueva; flecha = solo mejoras.
                    var badge = MarkerIcon(chip, g.NewWorks > 0 ? "crane" : "upgrade", WorkColor);
                    badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                    badge.rectTransform.sizeDelta = new Vector2(BadgeSize, BadgeSize);
                    badge.rectTransform.anchoredPosition = new Vector2(ChipIcon + 1f, 6f);
                    if (number <= 1) w += 4f;
                }
                chip.sizeDelta = new Vector2(w, ChipHeight);
                if (g.Works.Count > 0)
                    view.DetailBars.Add(new Bar { Fill = CreateBar(chip, new Vector2(0f, -ChipHeight * 0.5f + 3f), w - 4f), Works = g.Works, Economy = OwnerEconomy(e) });
                items.Add((chip, w, g));
                view.Chips.Add(new Chip { Type = g.Type, Local = new Rect(x, 0f, w, ChipHeight) });
                x += w + 2f;
            }
            if (shown < e.Groups.Count)
            {
                var chip = UIFactory.Rect("Mas", view.Detail);
                chip.anchorMin = chip.anchorMax = new Vector2(0.5f, 0.5f);
                chip.pivot = new Vector2(0f, 0.5f);
                var more = UIFactory.Label(chip, $"+{e.Groups.Count - shown}", 13, UITheme.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold);
                more.horizontalOverflow = HorizontalWrapMode.Overflow;
                float w = Mathf.Ceil(more.preferredWidth) + 6f;
                more.rectTransform.Stretch();
                chip.sizeDelta = new Vector2(w, ChipHeight);
                items.Add((chip, w, null));
                view.Chips.Add(new Chip { Type = null, Local = new Rect(x, 0f, w, ChipHeight) });
                x += w + 2f;
            }

            float total = x + 1f;
            view.DetailSize = new Vector2(total, ChipHeight);
            plate.rectTransform.sizeDelta = view.DetailSize;
            float left = -total * 0.5f;
            for (int i = 0; i < items.Count; i++)
            {
                var c = view.Chips[i];
                items[i].rt.anchoredPosition = new Vector2(left + c.Local.x, 0f);
                c.Local = new Rect(left + c.Local.x, -ChipHeight * 0.5f, c.Local.width, ChipHeight);
            }
            // Las barras están ancladas al centro del chip, cuyo pivote es su borde izquierdo: se centran en él.
            foreach (var bar in view.DetailBars)
            {
                var back = (RectTransform)bar.Fill.parent;
                var chipRt = (RectTransform)back.parent;
                back.anchorMin = back.anchorMax = new Vector2(0.5f, 0.5f);
                back.anchoredPosition = new Vector2(0f, -ChipHeight * 0.5f + 3f);
                back.sizeDelta = new Vector2(chipRt.sizeDelta.x - 4f, 3f);
            }
            view.Detail.gameObject.SetActive(false);
        }

        /// <summary>Símbolo agrupado: icono del tipo más numeroso y total de edificios de la provincia (+ barra si hay obras).</summary>
        private void EnsureAggregate(Entry e)
        {
            var view = e.View;
            if (view.AggregateVersion == e.Version && view.Aggregate != null) return;
            view.AggregateVersion = e.Version;
            view.BarStamp = -1;
            DestroyContainer(ref view.Aggregate);
            view.AggregateBars.Clear();

            var plate = CreatePlate(view.Root, "Agrupado", e.Own);
            view.Aggregate = plate.rectTransform;
            bool built = e.Total > 0;
            bool works = e.Works > 0;
            float contentY = works ? 2f : 0f;
            var def = BuildingCatalog.Get(e.Main);
            var color = def.Category == BuildingCategory.Military ? MilitaryColor : EconomicColor;
            if (!built) color.a = 0.5f;
            var icon = MarkerIcon(view.Aggregate, def.Icon, color);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0f, 0.5f);
            icon.rectTransform.sizeDelta = new Vector2(AggregateIcon, AggregateIcon);
            icon.rectTransform.anchoredPosition = new Vector2(3f, contentY);
            float w = AggregateIcon + 5f;

            var count = UIFactory.Label(view.Aggregate, built ? e.Total.ToString() : $"({e.Works})", 12, built ? UITheme.Text : WorkColor,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            count.horizontalOverflow = HorizontalWrapMode.Overflow;
            float tw = Mathf.Ceil(count.preferredWidth);
            count.rectTransform.anchorMin = count.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            count.rectTransform.pivot = new Vector2(0f, 0.5f);
            count.rectTransform.sizeDelta = new Vector2(tw + 2f, AggregateHeight);
            count.rectTransform.anchoredPosition = new Vector2(w, contentY);
            w += tw + 4f;
            if (built && works)
            {
                var badge = MarkerIcon(view.Aggregate, "crane", WorkColor);
                badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                badge.rectTransform.pivot = new Vector2(0f, 0.5f);
                badge.rectTransform.sizeDelta = new Vector2(BadgeSize, BadgeSize);
                badge.rectTransform.anchoredPosition = new Vector2(w - 2f, contentY);
                w += BadgeSize;
            }
            float height = AggregateHeight + (works ? 4f : 0f);
            view.AggregateSize = new Vector2(w + 2f, height);
            plate.rectTransform.sizeDelta = view.AggregateSize;
            if (works)
            {
                var all = new List<ConstructionProject>();
                foreach (var g in e.Groups) all.AddRange(g.Works);
                view.AggregateBars.Add(new Bar { Fill = CreateBar(view.Aggregate, new Vector2(0f, -height * 0.5f + 3f), view.AggregateSize.x - 6f), Works = all, Economy = OwnerEconomy(e) });
            }
            view.Aggregate.gameObject.SetActive(false);
        }

        /// <summary>
        /// Indicador de obras (resto de modos):
        /// <list type="bullet">
        /// <item>Obra nueva: icono del edificio con una grúa pequeña.</item>
        /// <item>Mejora: icono del edificio y "I→II" (sin grúa).</item>
        /// <item>Varias obras: icono del edificio si todas son del mismo tipo (si no, grúa) y "×N".</item>
        /// </list>
        /// Una sola barra: la obra de más prioridad en la cola. El detalle de cada obra va en el tooltip.
        /// </summary>
        private void EnsureWorks(Entry e)
        {
            var view = e.View;
            if (view.WorksVersion == e.Version && view.Works != null) return;
            view.WorksVersion = e.Version;
            view.BarStamp = -1;
            DestroyContainer(ref view.Works);
            view.WorksBars.Clear();

            var plate = CreatePlate(view.Root, "Obras", true);
            view.Works = plate.rectTransform;
            var works = e.PlayerWorks;
            bool single = works.Count == 1;
            bool sameType = true;
            foreach (var x in works) sameType &= x.Building == works[0].Building;
            var def = sameType ? works[0].Building : null;
            bool upgrade = single && works[0].IsUpgrade;
            float rowY = (BarHeight + 2f) * 0.5f;
            float w = 3f;

            var icon = MarkerIcon(view.Works, def != null ? def.Icon : "crane",
                def != null ? (def.Category == BuildingCategory.Military ? MilitaryColor : EconomicColor) : WorkColor);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0f, 0.5f);
            icon.rectTransform.sizeDelta = new Vector2(WorksIcon, WorksIcon);
            icon.rectTransform.anchoredPosition = new Vector2(w, rowY);
            w += WorksIcon;
            if (def != null && !upgrade)
            {
                // Grúa pequeña sobre el icono del edificio = "en obras".
                var badge = MarkerIcon(view.Works, "crane", WorkColor);
                badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                badge.rectTransform.pivot = new Vector2(0f, 0.5f);
                badge.rectTransform.sizeDelta = new Vector2(BadgeSize, BadgeSize);
                badge.rectTransform.anchoredPosition = new Vector2(w - 3f, rowY + 5f);
                w += BadgeSize - 4f;
            }
            string label = upgrade
                ? $"{EconomyRules.LevelName(works[0].TargetLevel - 1)}→{EconomyRules.LevelName(works[0].TargetLevel)}"
                : single ? null : $"×{works.Count}";
            if (label != null)
            {
                var text = UIFactory.Label(view.Works, label, 13, WorkColor, TextAnchor.MiddleLeft, FontStyle.Bold);
                var shadow = text.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
                shadow.effectDistance = new Vector2(1f, -1f);
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                float tw = Mathf.Ceil(text.preferredWidth);
                text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                text.rectTransform.pivot = new Vector2(0f, 0.5f);
                text.rectTransform.sizeDelta = new Vector2(tw + 2f, WorksIcon);
                text.rectTransform.anchoredPosition = new Vector2(w + 1f, rowY);
                w += tw + 3f;
            }
            w += 3f;
            float height = WorksIcon + 3f + BarHeight + 2f;
            view.WorksSize = new Vector2(Mathf.Max(w, 28f), height);
            plate.rectTransform.sizeDelta = view.WorksSize;
            view.WorksBar = CreateBar(view.Works, new Vector2(0f, -height * 0.5f + BarHeight * 0.5f + 2f), view.WorksSize.x - 6f);
            view.WorksBars.Add(new Bar { Fill = view.WorksBar, Works = works, Economy = OwnerEconomy(e) });
            view.WorksBar = (RectTransform)view.WorksBar.parent; // se oculta entera al alejar la cámara
            view.Works.gameObject.SetActive(false);
        }

        private static Image CreatePlate(Transform parent, string name, bool own)
        {
            var plate = UIFactory.Panel(parent, name, own ? PlayerPlateColor : PlateColor, false);
            plate.raycastTarget = false;
            if (own)
            {
                var outline = plate.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.45f, 0.75f, 0.35f, 0.45f);
                outline.effectDistance = new Vector2(1f, -1f);
            }
            var rt = plate.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            return plate;
        }

        // ================================================================== Ratón: tooltip y clic

        private void UpdateHover(Vector2 canvasSize, float scale)
        {
            var mouse = Mouse.current;
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            // En el modo construcción el clic es para construir: los símbolos no muestran tooltip ni se pulsan.
            if (mouse == null || overUI || _game.ProvinceClickHandler != null)
            {
                ClearHover();
                return;
            }
            Vector2 m = mouse.position.ReadValue() * scale - canvasSize * 0.5f;
            Entry hit = null;
            BuildingType? type = null;
            bool all = false;
            Kind kind = Kind.Aggregate;
            for (int i = _placed.Count - 1; i >= 0 && hit == null; i--)
            {
                var p = _placed[i];
                if (!p.Rect.Contains(m)) continue;
                hit = p.Entry;
                kind = p.Kind;
                all = true;
                if (p.Kind == Kind.Detail)
                {
                    Vector2 local = m - p.Rect.center;
                    foreach (var chip in p.Entry.View.Chips)
                        if (chip.Local.Contains(local) && chip.Type.HasValue)
                        {
                            type = chip.Type;
                            all = false;
                            break;
                        }
                }
            }
            if (hit == null)
            {
                ClearHover();
                return;
            }

            bool changed = hit != _hoverEntry || type != _hoverType || all != _hoverAll || kind != _hoverKind;
            _tooltipTimer -= Time.unscaledDeltaTime;
            _hoverEntry = hit;
            _hoverType = type;
            _hoverAll = all;
            _hoverKind = kind;
            IsHovering = true;
            if (changed || _tooltipTimer <= 0f)
            {
                _tooltipTimer = TooltipRefresh; // el progreso de las obras avanza con el tiempo
                string text = kind == Kind.Works ? DescribeWorks(hit) : Describe(hit, all ? (BuildingType?)null : type);
                UITooltip.ShowText(_canvas, this, text);
            }
            else UITooltip.ShowText(_canvas, this, null);
        }

        private void ClearHover()
        {
            if (_hoverEntry == null && !IsHovering) return;
            _hoverEntry = null;
            _hoverType = null;
            IsHovering = false;
            UITooltip.HideText(this);
        }

        /// <summary>Obras del jugador bajo el ratón (las del tipo señalado, o todas las de la provincia).</summary>
        private List<ConstructionProject> HoveredPlayerWorks()
        {
            var result = new List<ConstructionProject>();
            if (_hoverEntry == null) return result;
            foreach (var w in _hoverEntry.PlayerWorks)
                if (_hoverAll || !_hoverType.HasValue || w.Building.Type == _hoverType.Value) result.Add(w);
            return result;
        }

        /// <summary>
        /// Clic sobre el mapa (lo consulta el GameManager antes de interpretarlo): si el ratón está sobre una obra del
        /// jugador, abre Proyectos con esa obra y el clic no selecciona la provincia.
        /// </summary>
        private bool OnMapClick(Province province)
        {
            if (_game.ProvinceClickHandler != null) return false;
            var works = HoveredPlayerWorks();
            if (works.Count == 0) return false;
            // La que antes termine según la cola (la de mayor prioridad).
            var economy = _game.PlayerCountry.Economy;
            works.Sort((a, b) => economy.Projects.IndexOf(a).CompareTo(economy.Projects.IndexOf(b)));
            OpenProjectRequested?.Invoke(works[0]);
            return true;
        }

        private string Describe(Entry e, BuildingType? type)
        {
            var sb = new StringBuilder();
            var p = e.Province;
            sb.Append("<b>").Append(p.Name).Append("</b>");
            var owner = _game.World.GetOwner(p);
            if (owner != null) sb.Append("  <color=#b0a890>(").Append(owner.Name).Append(")</color>");

            if (type.HasValue)
            {
                var g = FindGroup(e, type.Value);
                if (g != null) AppendGroupDetail(sb, g, owner?.Economy);
                if (g != null && g.Works.Count > 0 && owner == _game.PlayerCountry) sb.Append(ProjectsHint);
                return sb.ToString();
            }

            sb.Append("\n<color=#b0a890>").Append(e.Total).Append(e.Total == 1 ? " edificio terminado" : " edificios terminados");
            if (e.Works > 0) sb.Append(" · ").Append(e.Works).Append(e.Works == 1 ? " obra en curso" : " obras en curso");
            sb.Append("</color>");
            foreach (var g in e.Groups) AppendGroupLine(sb, g);
            if (e.PlayerWorks.Count > 0) sb.Append(ProjectsHint);
            return sb.ToString();
        }

        private const string ProjectsHint = "\n<color=#8fd16f>Clic: ver en Industria Nacional › Proyectos</color>";

        /// <summary>Tooltip del indicador de obras: una obra con todos sus datos, o la lista si hay varias.</summary>
        private string DescribeWorks(Entry e)
        {
            var economy = _game.PlayerCountry?.Economy;
            var works = e.PlayerWorks;
            var sb = new StringBuilder();
            if (works.Count == 1)
            {
                var w = works[0];
                sb.Append("<b>").Append(WorkName(w)).Append("</b>");
                sb.Append("\n<color=#b0a890>").Append(e.Province.Name).Append("</color>");
                sb.Append("\nProgreso: <b>").Append(Mathf.FloorToInt(w.Progress01 * 100f)).Append(" %</b>");
                sb.Append("\nTiempo restante: ").Append(TimeLeft(w, economy));
                if (economy != null)
                    sb.Append("\nPrioridad: ").Append(economy.Projects.IndexOf(w) + 1).Append(" de ").Append(economy.Projects.Count);
            }
            else
            {
                sb.Append("<b>").Append(works.Count).Append(" obras activas</b>  <color=#b0a890>").Append(e.Province.Name).Append("</color>");
                foreach (var w in works)
                    sb.Append('\n').Append(WorkName(w)).Append(" — <b>").Append(Mathf.FloorToInt(w.Progress01 * 100f)).Append(" %</b>")
                      .Append(" — <color=#b0a890>").Append(TimeLeft(w, economy)).Append("</color>");
                var primary = PrimaryWork(works, economy);
                if (primary != null)
                    sb.Append("\n<color=#8a8478>La barra muestra la obra con más prioridad: ").Append(WorkName(primary)).Append("</color>");
            }
            sb.Append(ProjectsHint);
            return sb.ToString();
        }

        /// <summary>"Taller" para una obra nueva; "Industria civil I → II" para una mejora.</summary>
        private static string WorkName(ConstructionProject w) =>
            w.IsUpgrade
                ? $"{w.Building.Name} {EconomyRules.LevelName(w.TargetLevel - 1)} → {EconomyRules.LevelName(w.TargetLevel)}"
                : w.Building.Name;

        private static string TimeLeft(ConstructionProject w, CountryEconomy economy)
        {
            float days = economy != null ? EconomySystem.DaysRemaining(economy, w) : -1f;
            return days >= 0f ? $"{Mathf.CeilToInt(days)} días" : "en espera de capacidad";
        }

        /// <summary>Tooltip de un tipo: cantidad, niveles y cada obra con su progreso.</summary>
        private static void AppendGroupDetail(StringBuilder sb, Group g, CountryEconomy economy)
        {
            var def = BuildingCatalog.Get(g.Type);
            sb.Append("\n<color=#e0b85c><b>").Append(def.Name).Append("</b></color>");
            sb.Append(g.Count > 0 ? $"\nConstruidos: {g.Count}" : "\nTodavía no hay ninguno terminado");
            if (def.Upgradable && g.Count > 0) sb.Append("\nNiveles: ").Append(Levels(g));
            foreach (var w in g.Works)
            {
                sb.Append("\n<color=#ffd04d>")
                  .Append(w.IsUpgrade ? $"Mejora {EconomyRules.LevelName(w.TargetLevel - 1)} → {EconomyRules.LevelName(w.TargetLevel)}" : "En construcción")
                  .Append("</color>: ").Append(Mathf.FloorToInt(w.Progress01 * 100f)).Append(" %  ·  ").Append(TimeLeft(w, economy));
                if (economy != null && economy.Projects.Contains(w))
                    sb.Append("  ·  prioridad ").Append(economy.Projects.IndexOf(w) + 1);
            }
        }

        /// <summary>Línea resumida de un tipo (tooltip del símbolo agrupado).</summary>
        private static void AppendGroupLine(StringBuilder sb, Group g)
        {
            var def = BuildingCatalog.Get(g.Type);
            sb.Append('\n').Append(def.Name).Append(": ");
            sb.Append(g.Count > 0 ? g.Count.ToString() : "en obras");
            if (def.Upgradable && g.Count > 0 && g.Levels[1] != g.Count) sb.Append("  <color=#b0a890>(").Append(Levels(g)).Append(")</color>");
            for (int i = 0; i < g.Works.Count && i < 3; i++)
            {
                var w = g.Works[i];
                sb.Append("  <color=#ffd04d>· ")
                  .Append(w.IsUpgrade ? $"mejora {EconomyRules.LevelName(w.TargetLevel - 1)} → {EconomyRules.LevelName(w.TargetLevel)}" : "obra")
                  .Append(' ').Append(Mathf.FloorToInt(w.Progress01 * 100f)).Append(" %</color>");
            }
            if (g.Works.Count > 3) sb.Append("  <color=#ffd04d>· +").Append(g.Works.Count - 3).Append(" obras</color>");
        }

        private static string Levels(Group g)
        {
            var parts = new List<string>();
            for (int l = 1; l < g.Levels.Length; l++)
                if (g.Levels[l] > 0) parts.Add($"{EconomyRules.LevelName(l)} ×{g.Levels[l]}");
            return string.Join(" · ", parts);
        }
    }
}
