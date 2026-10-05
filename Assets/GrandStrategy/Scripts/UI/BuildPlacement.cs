using System;
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
    /// Modo construcción: tras pulsar «Construir» en Industria Nacional o Industria militar, el mapa pasa a ser una
    /// herramienta de colocación.
    /// <list type="bullet">
    /// <item>Franja «MODO CONSTRUCCIÓN — EDIFICIO» con coste, tiempo, tesoro, contador de encargados y botón Salir.</item>
    /// <item>Provincias coloreadas con la misma validación que la construcción (EconomySystem.CanBuild): válidas en
    /// verde suave, propias no válidas en rojo apagado, extranjeras en gris. La provincia bajo el cursor se resalta.</item>
    /// <item>Tooltip de provincia con huecos, qué se construirá y su coste, o el motivo si no se puede.</item>
    /// <item>Junto al cursor, el icono del edificio con un punto verde (se puede) o una × roja (no se puede).</item>
    /// <item>Cada clic válido encarga la obra al instante (sin confirmación), muestra «Edificio encargado −$X» sobre
    /// la provincia y aparece su indicador de obra en el mapa. Un clic no válido muestra el motivo en el sitio.</item>
    /// </list>
    /// Se sigue en el modo para encargar más; Esc, clic derecho o «Salir» lo abandonan (sin confirmar nada) y se vuelve
    /// a la pestaña desde la que se entró.
    /// </summary>
    public sealed class BuildPlacement : MonoBehaviour
    {
        private static readonly Color ValidDot = new Color(0.45f, 0.85f, 0.4f);

        private GameManager _game;
        private BuildingDef _building;
        private IMapMode _previousMode;
        private Action _onEnd;
        private Text _title, _info, _help, _counter;
        private Image _icon;
        private int _ordered;
        private string _last;

        // Indicador junto al cursor.
        private RectTransform _cursor, _cursorParent;
        private Image _cursorIcon;
        private Text _cursorBadge;

        public bool Active => _building != null;

        public static BuildPlacement Create(Transform parent, GameManager game)
        {
            var bg = UIFactory.Panel(parent, "BuildPlacement", UITheme.PanelBg);
            var placement = bg.gameObject.AddComponent<BuildPlacement>();
            placement._game = game;
            placement.Build(bg.rectTransform);
            placement.BuildCursor((RectTransform)parent);
            bg.gameObject.SetActive(false);
            return placement;
        }

        private void Build(RectTransform root)
        {
            root.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(900, 78));
            // Franja dorada arriba: herramienta activa (no un diálogo pendiente).
            var accent = UIFactory.Panel(root, "Herramienta", UITheme.Accent, false);
            accent.raycastTarget = false;
            accent.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 3));

            _icon = UIFactory.Icon(root, "hammer", UITheme.Accent);
            _icon.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 1), new Vector2(44, 44));
            // Martillo como insignia del icono del edificio: "herramienta de construcción con este edificio".
            var hammerBack = UIFactory.Panel(root, "Insignia", new Color(0.06f, 0.065f, 0.06f, 0.95f), false);
            hammerBack.raycastTarget = false;
            hammerBack.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(54, -16), new Vector2(20, 20));
            hammerBack.gameObject.AddComponent<Outline>().effectColor = new Color(UITheme.Border.r, UITheme.Border.g, UITheme.Border.b, 0.8f);
            var hammer = UIFactory.Icon(hammerBack.transform, "hammer", UITheme.Accent);
            hammer.rectTransform.Stretch(3, 3, 3, 3);

            // Línea 1: modo y edificio (+ contador a la derecha). Líneas 2 y 3 alineadas con ella.
            _title = FitLabel(root, 18, UITheme.Accent, FontStyle.Bold);
            _title.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(76, -7), new Vector2(-340, 24));
            _counter = FitLabel(root, 16, UITheme.Text, FontStyle.Bold);
            _counter.alignment = TextAnchor.MiddleRight;
            _counter.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-144, -7), new Vector2(150, 24));
            // Línea 2: coste, tiempo, tesoro y última obra.
            _info = FitLabel(root, 15, UITheme.Text, FontStyle.Normal);
            _info.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(76, -32), new Vector2(-226, 21));
            // Línea 3: controles.
            _help = FitLabel(root, 13, UITheme.TextDim, FontStyle.Normal);
            _help.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(76, -54), new Vector2(-226, 18));
            _help.text = "Clic izq.: construir  ·  Puedes colocar varios edificios seguidos  ·  Esc / clic derecho: salir";

            var exit = UIFactory.Button(root, "×  Salir", End, 17);
            ((RectTransform)exit.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, -1), new Vector2(118, 42));
            UITooltip.Attach(exit, "Abandona el modo construcción. Las obras ya encargadas siguen en Proyectos.");
        }

        /// <summary>Icono que acompaña al cursor mientras se coloca un edificio.</summary>
        private void BuildCursor(RectTransform parent)
        {
            _cursorParent = parent;
            _cursor = UIFactory.Rect("CursorConstruccion", parent);
            _cursor.anchorMin = _cursor.anchorMax = Vector2.zero;
            _cursor.pivot = new Vector2(0f, 0f);
            _cursor.sizeDelta = new Vector2(34f, 34f);
            var plate = UIFactory.Panel(_cursor, "Fondo", new Color(0.06f, 0.065f, 0.06f, 0.85f), false);
            plate.raycastTarget = false;
            plate.rectTransform.Stretch();
            plate.gameObject.AddComponent<Outline>().effectColor = new Color(UITheme.Border.r, UITheme.Border.g, UITheme.Border.b, 0.8f);
            _cursorIcon = UIFactory.Icon(_cursor, "hammer", UITheme.Accent);
            _cursorIcon.rectTransform.Stretch(5, 5, 5, 5);
            _cursorBadge = UIFactory.Label(_cursor, "", 16, ValidDot, TextAnchor.MiddleCenter, FontStyle.Bold);
            _cursorBadge.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(0.5f, 0.5f), new Vector2(-2, 3), new Vector2(18, 18));
            _cursorBadge.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            _cursor.gameObject.SetActive(false);
        }

        /// <summary>Texto de una línea que se reduce si no cabe (nunca salta a una segunda línea encima de otra).</summary>
        private static Text FitLabel(Transform parent, int size, Color color, FontStyle style)
        {
            var t = UIFactory.Label(parent, "", size, color, TextAnchor.MiddleLeft, style);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 11;
            t.resizeTextMaxSize = size;
            return t;
        }

        /// <summary>Empieza a colocar un edificio. onEnd se llama al salir del modo.</summary>
        public void Begin(BuildingDef building, Action onEnd)
        {
            if (Active) End();
            _building = building;
            _onEnd = onEnd;
            gameObject.SetActive(true);
            _icon.sprite = UIIcons.Get(building.Icon);
            _cursorIcon.sprite = UIIcons.Get(building.Icon);
            _ordered = 0;
            _last = null;
            _title.text = $"MODO CONSTRUCCIÓN — {building.Name.ToUpperInvariant()}";
            RefreshInfo();

            _previousMode = _game.Map.MapMode;
            _game.Map.SetMapMode(new PlacementMapMode(this));
            _game.ProvinceClickHandler = OnProvinceClicked;
            _game.Interaction.ProvinceRightClicked += OnRightClick;
            _game.Economy.Changed += OnEconomyChanged;
            _game.Economy.Updated += OnEconomyChanged;
            _game.Economy.BuildingCompleted += OnBuildingCompleted;
            HoverTooltip.ExtraInfo = DescribeProvince;
        }

        public void End()
        {
            if (!Active) return;
            _building = null;
            _game.ProvinceClickHandler = null;
            _game.Interaction.ProvinceRightClicked -= OnRightClick;
            _game.Economy.Changed -= OnEconomyChanged;
            _game.Economy.Updated -= OnEconomyChanged;
            _game.Economy.BuildingCompleted -= OnBuildingCompleted;
            HoverTooltip.ExtraInfo = null;
            if (_previousMode != null) _game.Map.SetMapMode(_previousMode);
            _cursor.gameObject.SetActive(false);
            gameObject.SetActive(false);
            var onEnd = _onEnd;
            _onEnd = null;
            onEnd?.Invoke();
        }

        private void OnDisable()
        {
            // Si el HUD se oculta (menú principal...), se sale del modo sin dejar el mapa coloreado.
            if (Active) End();
        }

        private void RefreshInfo()
        {
            var country = _game.PlayerCountry;
            bool afford = country.Economy.Treasury >= _building.Cost;
            _info.text = $"<b>{UIFactory.FormatMoney(_building.Cost)}</b> por edificio   ·   {_building.Days} días   ·   " +
                         $"Tesoro <b><color={(afford ? "#efe7d2" : "#eb6b5c")}>{UIFactory.FormatMoney(country.Economy.Treasury)}</color></b>" +
                         (_building.RequiresCoastal ? "   ·   <color=#e0b85c>solo provincias costeras</color>" : "") +
                         (_last != null ? $"   ·   <color=#a8a08c>Último: {_last}</color>" : "");
            _counter.text = $"Encargados: {_ordered}";
            _counter.color = _ordered > 0 ? UITheme.Positive : UITheme.TextDim;
        }

        private bool OnProvinceClicked(Province province)
        {
            var country = _game.PlayerCountry;
            var def = _building;
            if (_game.Economy.StartProject(country, def, province, out string reason))
            {
                // Sin ventana ni confirmación: la obra queda encargada, se paga y se sigue en el modo construcción.
                _ordered++;
                _last = $"{province.Name} (−{UIFactory.FormatMoney(def.Cost)})";
                MapFloatingText.ShowAt(_game.Map.GetProvinceWorldPosition(province), def.Icon, $"{def.Name} encargado",
                    $"−{UIFactory.FormatMoney(def.Cost)}", UITheme.Accent, UITheme.Negative);
            }
            else
            {
                // El motivo es el mismo que da la validación de la construcción; se muestra donde se ha hecho clic.
                var mouse = Mouse.current;
                Vector2 screen = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
                string icon = _game.Economy.CanAfford(country, def, out _) ? null : "coins";
                if (_game.Interaction.TryGetMapPoint(screen, out var world))
                    MapFloatingText.ShowAt(world, icon, reason, null, UITheme.Negative, UITheme.Negative);
                else
                    MapFloatingText.ShowAtScreen(screen, icon, reason, null, UITheme.Negative, UITheme.Negative);
            }
            RefreshInfo();
            _game.Map.RefreshMapModeColors();
            return true;
        }

        private void OnRightClick(Province _) => End();

        private void OnEconomyChanged()
        {
            if (!Active) return;
            RefreshInfo();
            _game.Map.RefreshMapModeColors();
        }

        private void OnBuildingCompleted(Country country, ConstructionProject project, Province province)
        {
            if (country == _game.PlayerCountry) OnEconomyChanged();
        }

        // --- Indicador junto al cursor: icono del edificio + punto verde (se puede) o × roja (no se puede) ---
        private void LateUpdate()
        {
            var mouse = Mouse.current;
            var province = _game.HoveredProvince;
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool show = Active && mouse != null && !overUI && province != null && _game.State == GameState.Playing;
            if (_cursor.gameObject.activeSelf != show) _cursor.gameObject.SetActive(show);
            if (!show) return;
            bool can = _game.Economy.CanBuild(_game.PlayerCountry, _building, province, out _);
            _cursorBadge.text = can ? "●" : "×";
            _cursorBadge.color = can ? ValidDot : UITheme.Negative;
            _cursorIcon.color = can ? UITheme.Accent : new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.55f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_cursorParent, mouse.position.ReadValue(), null, out var local);
            // Arriba a la derecha del puntero (el tooltip de provincia va debajo).
            _cursor.anchoredPosition = local + _cursorParent.rect.size * _cursorParent.pivot + new Vector2(14f, 12f);
            _cursor.SetAsLastSibling();
        }

        /// <summary>Tooltip de provincia en el modo construcción: huecos, qué se construye y su coste, o por qué no.</summary>
        private string DescribeProvince(Province province)
        {
            if (!Active || province.IsWater) return null;
            var country = _game.PlayerCountry;
            bool can = _game.Economy.CanBuild(country, _building, province, out string reason);
            string action = can
                ? $"<color=#8fd16f><b>Clic para construir {_building.Name} — {UIFactory.FormatMoney(_building.Cost)}</b></color>"
                : $"<color=#eb6b5c><b>{reason}</b></color>";
            if (province.OwnerId != country.Id) return action;

            int free = Mathf.Max(0, _game.Economy.FreeSlots(province));
            string slots = $"Huecos disponibles: <b>{free}/{EconomyRules.ProvinceSlots(province)}</b>";
            if (_building.Type == BuildingType.Hotel)
                slots += $"  ·  Hotel aquí: +{_game.Economy.HotelLocationBonus(province) * 100f:0} % de rendimiento";
            if (_building.RequiresCoastal) slots += province.Coastal ? "  ·  Costera" : "  ·  Sin costa";
            string text = $"{slots}\n{action}";

            // Edificios de la provincia, agrupados por tipo y nivel: "Industria militar ×4, Taller I ×2, Taller II".
            var groups = new System.Collections.Generic.List<(string name, int count)>();
            foreach (var b in province.Buildings)
            {
                string name = b.Def.Upgradable ? $"{b.Def.Name} {EconomyRules.LevelName(b.Level)}" : b.Def.Name;
                int i = groups.FindIndex(g => g.name == name);
                if (i < 0) groups.Add((name, 1));
                else groups[i] = (name, groups[i].count + 1);
            }
            var names = new System.Text.StringBuilder();
            foreach (var (name, count) in groups)
            {
                if (names.Length > 0) names.Append(", ");
                names.Append(name);
                if (count > 1) names.Append(" ×").Append(count);
            }
            int works = 0;
            foreach (var p in country.Economy.Projects) if (p.ProvinceId == province.Id) works++;
            if (names.Length > 0) text += $"\n<color=#b0a890>Edificios: {names}</color>";
            if (works > 0) text += $"\n<color=#ffd04d>Obras en curso aquí: {works}</color>";
            return text;
        }

        /// <summary>
        /// Colores del mapa durante la colocación, calculados con la misma validación que la construcción (incluido el
        /// dinero): verde suave = se puede construir; rojo apagado = provincia propia donde no se puede; gris = ajena.
        /// </summary>
        private sealed class PlacementMapMode : IMapMode
        {
            // Válida: verde suave. No válida: pardo con un toque rojizo (sin teñir el mapa de rojo). Ajena: gris oscuro.
            private static readonly Color32 Valid = new Color32(104, 158, 86, 255);
            private static readonly Color32 Invalid = new Color32(122, 90, 80, 255);
            private static readonly Color32 Foreign = new Color32(78, 77, 74, 255);
            private readonly BuildPlacement _owner;

            public PlacementMapMode(BuildPlacement owner) => _owner = owner;

            public string Name => "Construcción";
            public float OverlayStrength => 1f;

            public Color32 GetProvinceColor(Province province, WorldData world)
            {
                if (province.IsWater) return new Color32(0, 0, 0, 255);
                var country = _owner._game.PlayerCountry;
                if (_owner._building == null || country == null || province.OwnerId != country.Id) return Foreign;
                return _owner._game.Economy.CanBuild(country, _owner._building, province, out _) ? Valid : Invalid;
            }
        }
    }
}
