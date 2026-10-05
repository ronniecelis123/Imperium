using System;
using System.Collections.Generic;
using GrandStrategy.Core;
using GrandStrategy.Economy;
using GrandStrategy.Map;
using UnityEngine;
using UnityEngine.UI;

namespace GrandStrategy.UI
{
    /// <summary>Ventanas que abren los botones de la barra superior (cada una es un pop-up independiente).</summary>
    public enum IndustryTab { Economic, Economy, Military }

    /// <summary>
    /// Ventana central que abren los botones de la barra superior. Cada botón abre su propio pop-up con sus
    /// sub-pestañas:
    /// <list type="bullet">
    /// <item>Industria Nacional: Resumen, Construcción, Mejoras, Proyectos y Empresas.</item>
    /// <item>Economía: Resumen y Comercio internacional.</item>
    /// <item>Industria militar: Resumen, Construcción, Producción y Reservas.</item>
    /// </list>
    /// Investigación, Diplomacia y Ejército aún están por definir y muestran un aviso. El contenido se reconstruye
    /// al abrir, al cambiar de pestaña, cada día simulado y cuando el jugador cambia algo.
    /// <para>
    /// Jerarquía visual común: (1) indicadores principales en una franja destacada arriba, (2) cifras secundarias
    /// en filas y tablas, (3) descripciones y explicaciones en texto discreto o en tooltips. Título, pestañas y
    /// encabezados de tabla quedan fijos; solo se desplaza el contenido de debajo.
    /// </para>
    /// Solo presenta datos: todas las reglas y cálculos están en EconomyRules / EconomySystem.
    /// </summary>
    public sealed class NationWindow : MonoBehaviour
    {
        private const int TextSize = 16;
        private const float Width = 980f;
        private const float MaxHeight = 880f;
        private const float MarginLeft = 28f, MarginRight = 22f, ScrollbarWidth = 6f;

        private struct SubTab
        {
            public string Label, Icon;
            public Action<Country> Build;
        }

        /// <summary>Indicador de la franja superior: título pequeño, cifra destacada y tooltip opcional.</summary>
        private struct Stat
        {
            public string Label, Value, Sub, Tooltip;
            public Color Color;

            public Stat(string label, string value, Color color, string tooltip = null, string sub = null)
            {
                Label = label;
                Value = value;
                Color = color;
                Tooltip = tooltip;
                Sub = sub;
            }
        }

        private enum StripStyle { Normal, Compact, Hero }

        private GameManager _game;
        private BuildPlacement _placement;
        private Text _title, _subtitle;
        private RawImage _flag;
        private RectTransform _subTabBar, _header, _viewport, _content, _scrollbarRect;
        private Transform _into;
        private readonly Dictionary<IndustryTab, SubTab[]> _subTabs = new Dictionary<IndustryTab, SubTab[]>();
        private readonly Dictionary<IndustryTab, int> _subIndex = new Dictionary<IndustryTab, int>();
        /// <summary>Altura mínima de cada ventana: las pestañas de una misma sección no cambian bruscamente de tamaño.</summary>
        private static readonly Dictionary<IndustryTab, float> MinHeight = new Dictionary<IndustryTab, float>
        {
            [IndustryTab.Economic] = 600f, [IndustryTab.Economy] = 560f, [IndustryTab.Military] = 600f,
        };

        private bool _industryMode;
        private IndustryTab _tab;
        private IndustryTab _builtSubTabsFor = (IndustryTab)(-1);
        private string _placeholderTitle;
        private string _lastPage;
        // Obra señalada al abrir Proyectos desde el mapa (se resalta y se lleva a la vista).
        private ConstructionProject _focusProject;
        private RectTransform _focusRow;
        private bool _scrollToFocus;

        // Estado de la pestaña Mejoras (solo organización visual).
        private BuildingType? _upgradeType;
        private int _upgradeProvince = -1;
        private readonly HashSet<int> _expanded = new HashSet<int>();

        /// <summary>Se dispara al abrir, cerrar o cambiar de pestaña (para resaltar los botones de la barra).</summary>
        public event Action Changed;

        public bool IsOpen => gameObject.activeSelf;
        public bool ShowingIndustry => IsOpen && _industryMode;
        public IndustryTab CurrentTab => _tab;
        public string PlaceholderTitle => IsOpen && !_industryMode ? _placeholderTitle : null;

        public static NationWindow Create(Transform parent, GameManager game, BuildPlacement placement)
        {
            var bg = UIFactory.Panel(parent, "NationWindow", UITheme.PanelBg);
            var window = bg.gameObject.AddComponent<NationWindow>();
            window._game = game;
            window._placement = placement;
            window.DefineTabs();
            window.Build(bg.rectTransform);
            bg.gameObject.SetActive(false);
            return window;
        }

        private void DefineTabs()
        {
            _subTabs[IndustryTab.Economic] = new[]
            {
                new SubTab { Label = "Resumen", Icon = "chart", Build = BuildEconomicSummary },
                new SubTab { Label = "Construcción", Icon = "crane", Build = c => BuildConstruction(c, BuildingCategory.Economic) },
                new SubTab { Label = "Mejoras", Icon = "upgrade", Build = BuildUpgrades },
                new SubTab { Label = "Proyectos", Icon = "clock", Build = BuildProjects },
                new SubTab { Label = "Empresas", Icon = "briefcase", Build = BuildCompanies },
            };
            _subTabs[IndustryTab.Economy] = new[]
            {
                new SubTab { Label = "Resumen", Icon = "chart", Build = BuildEconomySummary },
                new SubTab { Label = "Comercio internacional", Icon = "trade", Build = BuildTrade },
            };
            _subTabs[IndustryTab.Military] = new[]
            {
                new SubTab { Label = "Resumen", Icon = "chart", Build = BuildMilitarySummary },
                new SubTab { Label = "Construcción", Icon = "crane", Build = c => BuildConstruction(c, BuildingCategory.Military) },
                new SubTab { Label = "Producción", Icon = "gear", Build = BuildProduction },
                new SubTab { Label = "Reservas", Icon = "crate", Build = BuildReserves },
            };
            foreach (var t in _subTabs.Keys) _subIndex[t] = 0;
        }

        private void Build(RectTransform root)
        {
            root.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(Width, 720));

            // --- Cabecera ---
            var flagFrame = UIFactory.Panel(root, "FlagFrame", Color.black);
            flagFrame.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -16), new Vector2(66, 44));
            _flag = UIFactory.Rect("Flag", flagFrame.transform).Stretch(2, 2, 2, 2).gameObject.AddComponent<RawImage>();
            _flag.raycastTarget = false;
            _title = UIFactory.Label(root, "", 26, UITheme.Accent, TextAnchor.UpperLeft, FontStyle.Bold);
            _title.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(102, -12), new Vector2(-160, 32));
            _subtitle = UIFactory.Label(root, "", 15, UITheme.TextDim, TextAnchor.UpperLeft);
            _subtitle.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(102, -44), new Vector2(-160, 22));
            Fit(_subtitle, 12);
            var close = UIFactory.Button(root, "X", Close, 18);
            ((RectTransform)close.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -14), new Vector2(32, 32));
            UITooltip.Attach(close, "Cerrar (Esc)");

            // --- Sub-pestañas (se crean según la ventana abierta) ---
            _subTabBar = UIFactory.Rect("SubTabs", root);
            _subTabBar.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -76), new Vector2(-40, 40));
            var hs = UIFactory.HorizontalLayout(_subTabBar.gameObject, 6, new RectOffset(0, 0, 0, 0));
            hs.childForceExpandWidth = true;

            // --- Encabezado fijo de la pestaña (indicadores, filtros, cabeceras de tabla): no se desplaza ---
            _header = UIFactory.Rect("FixedHeader", root);
            var vh = UIFactory.VerticalLayout(_header.gameObject, 6, new RectOffset(0, 0, 0, 0));
            vh.childForceExpandHeight = false;

            // --- Contenido con desplazamiento ---
            _viewport = UIFactory.Rect("Viewport", root);
            _viewport.gameObject.AddComponent<RectMask2D>();
            var vpImage = _viewport.gameObject.AddComponent<Image>();
            vpImage.color = new Color(0, 0, 0, 0); // recibe la rueda del ratón
            _content = UIFactory.Rect("Content", _viewport);
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = new Vector2(1, 1);
            _content.pivot = new Vector2(0.5f, 1);
            _content.offsetMin = _content.offsetMax = Vector2.zero;
            var v = UIFactory.VerticalLayout(_content.gameObject, 6, new RectOffset(0, 0, 0, 4));
            v.childForceExpandHeight = false;
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = _viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = _content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            var bar = UIFactory.VerticalScrollbar(root);
            _scrollbarRect = (RectTransform)bar.transform;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private static string TitleOf(IndustryTab tab) => tab switch
        {
            IndustryTab.Economic => "INDUSTRIA NACIONAL",
            IndustryTab.Economy => "ECONOMÍA",
            _ => "INDUSTRIA MILITAR",
        };

        private void OnEnable()
        {
            if (_game?.Economy == null) return;
            _game.Economy.Updated += Refresh;
            _game.Economy.DayProcessed += Refresh;
            _game.Economy.Changed += Refresh;
        }

        private void OnDisable()
        {
            UIDropdown.Close();
            _focusProject = null;
            if (_game?.Economy == null) return;
            _game.Economy.Updated -= Refresh;
            _game.Economy.DayProcessed -= Refresh;
            _game.Economy.Changed -= Refresh;
        }

        // ================================================================== Abrir / cerrar

        public void ShowIndustry(IndustryTab tab)
        {
            _industryMode = true;
            _tab = tab;
            gameObject.SetActive(true);
            Refresh();
        }

        /// <summary>Abre una sub-pestaña concreta (índice dentro de la pestaña).</summary>
        public void ShowIndustry(IndustryTab tab, int subTab)
        {
            _subIndex[tab] = Mathf.Clamp(subTab, 0, _subTabs[tab].Length - 1);
            ShowIndustry(tab);
        }

        /// <summary>Abre Industria Nacional › Proyectos con una obra resaltada y a la vista.</summary>
        public void ShowProject(ConstructionProject project)
        {
            _focusProject = project;
            _scrollToFocus = true;
            ShowIndustry(IndustryTab.Economic, ProjectsTabIndex);
        }

        private const int ProjectsTabIndex = 3;

        /// <summary>Abre un apartado aún sin diseñar (Investigación, Diplomacia, Ejército).</summary>
        public void ShowPlaceholder(string title)
        {
            _industryMode = false;
            _placeholderTitle = title;
            gameObject.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            gameObject.SetActive(false);
            Changed?.Invoke();
        }

        // ================================================================== Contenido

        private void Refresh()
        {
            if (!IsOpen) return;
            var country = _game.PlayerCountry;
            _flag.texture = country != null ? FlagGenerator.Get(country) : null;
            _subTabBar.gameObject.SetActive(_industryMode);
            if (_industryMode) RebuildSubTabs();
            float top = _industryMode ? 128 : 84;
            // El encabezado fijo necesita su anchura antes de calcular la altura de los textos.
            _header.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2((MarginLeft - MarginRight - ScrollbarWidth - 6f) * 0.5f, -top),
                new Vector2(-(MarginLeft + MarginRight + ScrollbarWidth + 6f), 0));

            Clear(_content);
            Clear(_header);
            _into = _content;
            _focusRow = null;
            if (country == null || country.Economy == null)
            {
                _title.text = "";
                _subtitle.text = "";
            }
            else if (!_industryMode)
            {
                _title.text = _placeholderTitle.ToUpperInvariant();
                _subtitle.text = country.Name;
                Paragraph("Este apartado está por definir. Aquí se mostrará la información de " +
                          _placeholderTitle.ToLowerInvariant() + " cuando se diseñe su sistema.", UITheme.TextDim);
            }
            else
            {
                _title.text = TitleOf(_tab);
                _subtitle.text = $"{country.Name}   ·   Tesoro {UIFactory.FormatMoney(country.Economy.Treasury)}   ·   " +
                                 $"Próximo cobro: {_game.Clock.FormatDay(_game.Economy.NextPayoutDate)} (cada {EconomyRules.PeriodDays} días)";
                _subTabs[_tab][_subIndex[_tab]].Build(country);
            }
            _into = _content;

            // Encabezado fijo (si la pestaña lo usa) y, debajo, el contenido desplazable.
            LayoutRebuilder.ForceRebuildLayoutImmediate(_header);
            float headerHeight = _header.childCount > 0 ? LayoutUtility.GetPreferredHeight(_header) : 0f;
            _header.sizeDelta = new Vector2(_header.sizeDelta.x, headerHeight);
            float viewportTop = top + (headerHeight > 0f ? headerHeight + 8f : 0f);
            _viewport.Stretch(MarginLeft, MarginRight + ScrollbarWidth + 6f, viewportTop, 20);
            _scrollbarRect.anchorMin = new Vector2(1, 0);
            _scrollbarRect.anchorMax = new Vector2(1, 1);
            _scrollbarRect.pivot = new Vector2(1, 0.5f);
            _scrollbarRect.offsetMin = new Vector2(-MarginRight - ScrollbarWidth, 20);
            _scrollbarRect.offsetMax = new Vector2(-MarginRight, -viewportTop);

            // Al cambiar de pestaña se vuelve arriba; al refrescar la misma (cada día) se conserva el desplazamiento.
            string page = _industryMode ? $"{_tab}/{_subIndex[_tab]}" : "P/" + _placeholderTitle;
            if (page != _lastPage)
            {
                _lastPage = page;
                _content.anchoredPosition = Vector2.zero;
                UIDropdown.Close();
                if (page != $"{IndustryTab.Economic}/{ProjectsTabIndex}") _focusProject = null;
            }

            // Altura: se ajusta al contenido, con un mínimo por sección (sin saltos bruscos entre sus pestañas)
            // y un máximo (lo que no cabe se desplaza).
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            var root = (RectTransform)transform;
            float min = _industryMode ? MinHeight[_tab] : 240f;
            float height = Mathf.Clamp(viewportTop + LayoutUtility.GetPreferredHeight(_content) + 26f, min, MaxHeight);
            root.sizeDelta = new Vector2(Width, height);
            if (_scrollToFocus && _focusRow != null)
            {
                // Lleva la obra señalada a la vista (una sola vez; después el jugador desplaza libremente).
                _scrollToFocus = false;
                float depth = -_content.InverseTransformPoint(_focusRow.position).y;
                float view = _viewport.rect.height, total = LayoutUtility.GetPreferredHeight(_content);
                _content.anchoredPosition = new Vector2(0f, Mathf.Clamp(depth - view * 0.4f, 0f, Mathf.Max(0f, total - view)));
            }
            // Sin fundidos al reconstruir: los botones nuevos aparecen ya con su color (si no, parpadean cada día).
            UIFactory.SnapTransitions(_subTabBar);
            UIFactory.SnapTransitions(_header);
            UIFactory.SnapTransitions(_content);
            Changed?.Invoke();
        }

        private void RebuildSubTabs()
        {
            var subs = _subTabs[_tab];
            if (_builtSubTabsFor != _tab)
            {
                _builtSubTabsFor = _tab;
                Clear(_subTabBar);
                for (int i = 0; i < subs.Length; i++)
                {
                    int index = i;
                    UIFactory.IconButton(_subTabBar, subs[i].Label, subs[i].Icon, () =>
                    {
                        _subIndex[_tab] = index;
                        Refresh();
                    }, 16);
                }
            }
            for (int i = 0; i < _subTabBar.childCount; i++)
                _subTabBar.GetChild(i).GetComponent<Button>().SetSelected(i == _subIndex[_tab]);
        }

        // ================================================================== Industria Nacional

        // --- Resumen ---
        private void BuildEconomicSummary(Country c)
        {
            var e = c.Economy;
            var r = EconomyRules.Evaluate(e);
            float capacity = EconomyRules.ConstructionCapacity(e);
            int ecoWorks = e.ActiveProjects(BuildingCategory.Economic), milWorks = e.ActiveProjects(BuildingCategory.Military);
            StatStrip(StripStyle.Normal,
                new Stat("Edificios económicos", e.EconomicBuildings.ToString(), UITheme.Text,
                    "Edificios económicos terminados, incluidos los que ya existían en 1936."),
                new Stat("Capacidad de construcción", UIFactory.FormatCapacity(capacity), UITheme.Accent, CapacityTooltip(capacity)),
                new Stat("Ingresos económicos", UIFactory.FormatMoney(r.BuildingIncome, true), UITheme.Positive,
                    $"Ingresos de los edificios económicos cada {EconomyRules.PeriodDays} días, antes del mantenimiento.\n" +
                    $"Mantenimiento de estos edificios: {UIFactory.FormatPerPeriod(-r.EconomicUpkeep)}.", $"/ {EconomyRules.PeriodDays} días"),
                new Stat("Obras activas", (ecoWorks + milWorks).ToString(), UITheme.Text,
                    $"{ecoWorks} económicas (incluidas mejoras) y {milWorks} militares.\nTodas comparten la capacidad de construcción nacional."));
            Space(4);

            Section("Edificios por tipo", "factory");
            var columns = Columns(2, 28);
            int n = 0;
            foreach (var def in BuildingCatalog.All)
                if (def.Category == BuildingCategory.Economic) BuildingCountRow(columns[n++ % 2], def, e.Count(def.Type));
            Space(6);

            Section("Mantenimiento", "coins");
            UITooltip.Attach(KeyValue(_into, "Mantenimiento de los edificios económicos", UIFactory.FormatPerPeriod(-r.EconomicUpkeep), UITheme.Negative),
                "Lo que cuesta mantener todos los edificios económicos en cada periodo de cobro.\n" +
                "Las mejoras aumentan el mantenimiento (×1.4 en nivel II, ×1.8 en nivel III).");
        }

        private static void BuildingCountRow(Transform parent, BuildingDef def, int count)
        {
            var row = UIFactory.Rect("Row_" + def.Name, parent);
            row.Layout(28).flexibleHeight = 0;
            UIFactory.HorizontalLayout(row.gameObject, 10, new RectOffset(0, 4, 2, 2));
            bool has = count > 0;
            var icon = UIFactory.Icon(row, def.Icon, has ? UITheme.Accent : new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.3f));
            icon.Layout(preferredWidth: 22);
            Fit(UIFactory.Label(row, def.Name, TextSize, has ? UITheme.Text : UITheme.TextNote), 12).Layout(flexibleWidth: 1);
            UIFactory.Label(row, count.ToString(), TextSize + 1, has ? UITheme.Text : UITheme.TextNote, TextAnchor.MiddleRight, FontStyle.Bold)
                .Layout(preferredWidth: 56);
        }

        // --- Construcción (económica o militar): tarjetas de edificios ---
        private void BuildConstruction(Country c, BuildingCategory category)
        {
            var e = c.Economy;
            Fixed(() =>
            {
                ConstructionStrip(e);
                Note("Elige un edificio y después una provincia del mapa. Las obras se gestionan en Industria Nacional › Proyectos.");
            });
            foreach (var def in BuildingCatalog.All)
                if (def.Category == category) BuildingCard(c, def);
        }

        /// <summary>Franja compacta común a Construcción, Mejoras y Proyectos.</summary>
        private void ConstructionStrip(CountryEconomy e)
        {
            float capacity = EconomyRules.ConstructionCapacity(e);
            StatStrip(StripStyle.Compact,
                new Stat("Tesoro", UIFactory.FormatMoney(e.Treasury), e.Treasury < 0 ? UITheme.Negative : UITheme.Text,
                    "Las obras y mejoras se pagan enteras al encargarlas."),
                new Stat("Capacidad de construcción", UIFactory.FormatCapacity(capacity), UITheme.Accent, CapacityTooltip(capacity)),
                new Stat("Obras activas", e.Projects.Count.ToString(), UITheme.Text,
                    $"{e.ActiveProjects(BuildingCategory.Economic)} económicas (incluidas mejoras) y {e.ActiveProjects(BuildingCategory.Military)} militares."));
        }

        private void BuildingCard(Country c, BuildingDef def)
        {
            var e = c.Economy;
            var card = UIFactory.Panel(_into, "Card_" + def.Name, UITheme.Tint, false);
            card.raycastTarget = false;
            var cardLayout = card.Layout(112);
            cardLayout.preferredHeight = -1; // la altura la decide el texto
            cardLayout.flexibleHeight = 0;
            var rt = card.rectTransform;
            var column = UIFactory.VerticalLayout(card.gameObject, 2, new RectOffset(86, 206, 10, 12));
            column.childForceExpandHeight = false;
            BottomLine(rt);

            var icon = UIFactory.Icon(rt, def.Icon, UITheme.Accent);
            icon.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            icon.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -14), new Vector2(54, 54));

            // 1. Nombre  2. Coste, tiempo y mantenimiento  3. Efectos  ·  descripción y requisitos, discretos.
            UIFactory.Label(rt, def.Name, 19, UITheme.Text, TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.Label(rt, def.Description, 14, UITheme.TextNote, TextAnchor.UpperLeft);
            UIFactory.Rect("Space", rt).Layout(3);
            var stats = UIFactory.Label(rt,
                $"Coste <b>{UIFactory.FormatMoney(def.Cost)}</b>   ·   Tiempo <b>{def.Days} días</b>   ·   " +
                $"Mantenimiento <b>{UIFactory.FormatPerPeriod(def.UpkeepPerPeriod)}</b>", TextSize, UITheme.Text, TextAnchor.UpperLeft);
            Tip(stats, "<b>Coste</b>: se paga entero al encargar la obra.\n" +
                       "<b>Tiempo</b>: días a máxima velocidad (1 punto de construcción por día).\n" +
                       $"<b>Mantenimiento</b>: se paga cada {EconomyRules.PeriodDays} días mientras exista el edificio.");
            var effects = UIFactory.Label(rt, "<color=#e0b85c>Efectos</color>   " + string.Join("   ·   ", def.Effects), 15, UITheme.Text, TextAnchor.UpperLeft);
            string bonus = BonusTooltip(def);
            if (bonus != null) Tip(effects, bonus);
            string req = "Requisitos: provincia propia" + (def.RequiresCoastal ? " <color=#e0b85c>costera</color>" : "") + " con hueco libre" +
                         (def.Upgradable ? $"   ·   mejorable hasta nivel {EconomyRules.LevelName(EconomyRules.MaxBuildingLevel)}" : "");
            var reqText = UIFactory.Label(rt, req, 13, UITheme.TextNote, TextAnchor.UpperLeft);
            Tip(reqText, "Los huecos de cada provincia dependen de su desarrollo; los edificios de 1936 no ocupan huecos." +
                         (def.RequiresCoastal ? "\nLos astilleros solo se pueden construir en provincias con costa." : ""));

            // 4. Botón (con el motivo en un tooltip si está deshabilitado) y estado.
            bool can = _game.Economy.CanAfford(c, def, out string reason);
            var button = UIFactory.IconButton(rt, "Construir", "hammer", () => BeginPlacement(def), 17);
            button.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            ((RectTransform)button.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -14), new Vector2(172, 42));
            button.SetInteractable(can);
            if (!can) UITooltip.Attach(button, reason);
            int building = 0;
            foreach (var p in e.Projects) if (p.Building == def && !p.IsUpgrade) building++;
            var status = UIFactory.Label(rt, can ? $"Tienes <b>{e.Count(def.Type)}</b>   ·   En obras <b>{building}</b>" : reason, 13,
                can ? UITheme.TextDim : UITheme.Negative, TextAnchor.UpperCenter);
            Fit(status, 11);
            status.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            status.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -62), new Vector2(188, 40));
        }

        private void BeginPlacement(BuildingDef def)
        {
            var tab = _tab;
            int sub = _subIndex[tab];
            Close();
            // Al terminar de colocar se vuelve a la misma pestaña.
            _placement.Begin(def, () => ShowIndustry(tab, sub));
        }

        // --- Mejoras ---
        private static readonly float[] UpgradeWidths = { 28f, -1f, 46f, 228f, 250f, 108f };
        private static readonly TextAnchor[] UpgradeAligns =
            { TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, TextAnchor.MiddleCenter, TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, TextAnchor.MiddleCenter };
        private const int UpgradeIndent = 22;

        private void BuildUpgrades(Country c)
        {
            var e = c.Economy;
            var provinces = new List<Province>();
            var presentTypes = new List<BuildingDef>();
            foreach (int id in c.Provinces)
            {
                var p = _game.World.GetProvince(id);
                if (p == null || EconomicBuildings(p, null) == 0) continue;
                provinces.Add(p);
                foreach (var b in p.Buildings)
                    if (b.Def.Upgradable && !presentTypes.Contains(b.Def)) presentTypes.Add(b.Def);
            }
            presentTypes.Sort((a, b) => Array.IndexOf(BuildingCatalog.All, a).CompareTo(Array.IndexOf(BuildingCatalog.All, b)));
            if (_upgradeType.HasValue && !presentTypes.Exists(d => d.Type == _upgradeType.Value)) _upgradeType = null;
            if (_upgradeProvince >= 0 && !provinces.Exists(p => p.Id == _upgradeProvince)) _upgradeProvince = -1;

            var byName = new List<Province>(provinces);
            byName.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
            provinces.Sort((a, b) => EconomicBuildings(b, null) != EconomicBuildings(a, null)
                ? EconomicBuildings(b, null).CompareTo(EconomicBuildings(a, null)) : string.CompareOrdinal(a.Name, b.Name));

            // Provincias que pasan los filtros.
            var shown = new List<Province>();
            foreach (var p in provinces)
                if ((_upgradeProvince < 0 || p.Id == _upgradeProvince) && EconomicBuildings(p, _upgradeType) > 0) shown.Add(p);

            Fixed(() =>
            {
                ConstructionStrip(e);
                // Filtros (solo cambian lo que se muestra).
                var filters = UIFactory.Rect("Filtros", _into);
                filters.Layout(36).flexibleHeight = 0;
                var h = UIFactory.HorizontalLayout(filters.gameObject, 8, new RectOffset(0, 0, 0, 0));
                h.childForceExpandHeight = true;
                var typeNames = new string[presentTypes.Count + 1];
                typeNames[0] = "Todos";
                for (int i = 0; i < presentTypes.Count; i++) typeNames[i + 1] = presentTypes[i].Name;
                int typeIndex = _upgradeType.HasValue ? presentTypes.FindIndex(d => d.Type == _upgradeType.Value) + 1 : 0;
                UIDropdown.Create(filters, "Edificio", typeNames, typeIndex, i =>
                {
                    _upgradeType = i == 0 ? (BuildingType?)null : presentTypes[i - 1].Type;
                    Refresh();
                }).Layout(preferredWidth: 300);
                var provNames = new string[byName.Count + 1];
                provNames[0] = "Todas las provincias";
                for (int i = 0; i < byName.Count; i++) provNames[i + 1] = byName[i].Name;
                int provIndex = _upgradeProvince >= 0 ? byName.FindIndex(p => p.Id == _upgradeProvince) + 1 : 0;
                UIDropdown.Create(filters, "Provincia", provNames, provIndex, i =>
                {
                    _upgradeProvince = i == 0 ? -1 : byName[i - 1].Id;
                    Refresh();
                }).Layout(preferredWidth: 300);
                UIFactory.Rect("Hueco", filters).Layout(flexibleWidth: 1);
                var expand = UIFactory.Button(filters, "Desplegar todo", () =>
                {
                    foreach (var p in shown) _expanded.Add(p.Id);
                    Refresh();
                }, 14);
                expand.Layout(preferredWidth: 126);
                var collapse = UIFactory.Button(filters, "Plegar todo", () =>
                {
                    _expanded.Clear();
                    Refresh();
                }, 14);
                collapse.Layout(preferredWidth: 110);

                var note = Note("Mejorar no ocupa hueco, se paga al encargarla y usa la capacidad de construcción (detalles en el tooltip).");
                Tip(note, $"<b>Mejoras</b>\nNivel II: ×{EconomyRules.LevelOutput(2):0.0} ingresos y efectos, ×{EconomyRules.LevelUpkeep(2):0.0} mantenimiento. " +
                          $"Cuesta el {EconomyRules.UpgradeCostShare[1] * 100:0} % del edificio.\n" +
                          $"Nivel III: ×{EconomyRules.LevelOutput(3):0.0} ingresos y efectos, ×{EconomyRules.LevelUpkeep(3):0.0} mantenimiento. " +
                          $"Cuesta el {EconomyRules.UpgradeCostShare[2] * 100:0} % del edificio.\n" +
                          "Se paga al encargarla, usa la capacidad de construcción y aparece en Proyectos.\n" +
                          "Un edificio que ya se está mejorando no admite otra mejora hasta terminar.");
                if (provinces.Count > 0)
                    TableHeader(new[] { "", "Edificio", "Nivel", $"Rendimiento / {EconomyRules.PeriodDays} días", "Siguiente mejora", "" },
                        UpgradeWidths, UpgradeAligns, UpgradeIndent);
            });

            if (provinces.Count == 0)
            {
                Paragraph("Todavía no tienes edificios económicos. Constrúyelos en la pestaña Construcción.", UITheme.Text);
                return;
            }
            if (shown.Count == 0)
            {
                Paragraph("Ningún edificio coincide con los filtros elegidos.", UITheme.TextDim);
                return;
            }
            foreach (var p in shown)
            {
                bool expanded = _expanded.Contains(p.Id) || _upgradeProvince == p.Id;
                ProvinceHeader(p, expanded);
                if (!expanded) continue;
                int index = 0;
                foreach (var b in p.Buildings)
                    if (b.Def.Upgradable && (!_upgradeType.HasValue || b.Type == _upgradeType.Value)) UpgradeRow(c, p, b, index++);
            }
        }

        /// <summary>Edificios económicos de una provincia (de un tipo concreto, o de todos si type es null).</summary>
        private static int EconomicBuildings(Province p, BuildingType? type)
        {
            int n = 0;
            foreach (var b in p.Buildings)
                if (b.Def.Upgradable && (!type.HasValue || b.Type == type.Value)) n++;
            return n;
        }

        /// <summary>Cabecera plegable de una provincia: ► NOMBRE · desarrollo · huecos · edificios.</summary>
        private void ProvinceHeader(Province p, bool expanded)
        {
            var bg = UIFactory.Panel(_into, "Provincia_" + p.Name, Color.white, false);
            bg.Layout(34).flexibleHeight = 0;
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var colors = button.colors;
            colors.normalColor = new Color(1f, 0.95f, 0.8f, expanded ? 0.09f : 0.055f);
            colors.highlightedColor = new Color(1f, 0.92f, 0.7f, 0.14f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.03f);
            colors.selectedColor = colors.normalColor;
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            int id = p.Id;
            button.onClick.AddListener(() =>
            {
                if (!_expanded.Remove(id)) _expanded.Add(id);
                if (_upgradeProvince == id && !_expanded.Contains(id)) _upgradeProvince = -1; // plegar la provincia filtrada quita el filtro
                Refresh();
            });
            var h = UIFactory.HorizontalLayout(bg.gameObject, 8, new RectOffset(10, 12, 0, 0));
            h.childForceExpandHeight = true;
            UIFactory.Label(bg.transform, expanded ? "▼" : "►", 13, UITheme.Accent, TextAnchor.MiddleCenter).Layout(preferredWidth: 16);
            Fit(UIFactory.Label(bg.transform, p.Name.ToUpperInvariant(), TextSize, UITheme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold), 12)
                .Layout(flexibleWidth: 1);

            int total = EconomicBuildings(p, null), match = EconomicBuildings(p, _upgradeType), upgrading = 0;
            foreach (var b in p.Buildings) if (b.Upgrading) upgrading++;
            string count = _upgradeType.HasValue ? $"{match} de {total} edificios" : $"{total} {(total == 1 ? "edificio" : "edificios")}";
            string info = $"Desarrollo {p.Development}   ·   Huecos {Mathf.Max(0, _game.Economy.FreeSlots(p))}/{EconomyRules.ProvinceSlots(p)}   ·   {count}" +
                          (upgrading > 0 ? $"   ·   <color=#e0b85c>{upgrading} mejorando</color>" : "");
            Fit(UIFactory.Label(bg.transform, info, 14, UITheme.TextDim, TextAnchor.MiddleRight), 11).Layout(preferredWidth: 470);
        }

        private void UpgradeRow(Country c, Province p, BuildingInstance b, int index)
        {
            var w = UpgradeWidths;
            var row = (RectTransform)RowBackground(index).transform;
            row.Layout(46).flexibleHeight = 0;
            var h = UIFactory.HorizontalLayout(row.gameObject, 8, new RectOffset(8 + UpgradeIndent, 8, 4, 4));
            h.childForceExpandHeight = true;

            UIFactory.Icon(row, b.Def.Icon, UITheme.Accent).Layout(preferredWidth: w[0]);
            Cell(row, b.Def.Name + (b.Initial ? "\n<size=12><color=#8a8478>existente en 1936</color></size>" : ""), w[1], TextSize, UITheme.Text, UpgradeAligns[1]);
            Cell(row, EconomyRules.LevelName(b.Level), w[2], 17, UITheme.Accent, UpgradeAligns[2], FontStyle.Bold);

            string special = SpecialEffect(b, b.Level);
            var yield = Cell(row, $"<b>{UIFactory.FormatMoney(EconomyRules.InstanceIncome(b), true)}</b>   mant. {UIFactory.FormatMoney(EconomyRules.InstanceUpkeep(b))}" +
                                  (special != null ? $"\n<size=12><color=#a8a08c>{special}</color></size>" : ""), w[3], 14, UITheme.Text, UpgradeAligns[3]);
            string bonus = BonusTooltip(b.Def);
            if (bonus != null) Tip(yield, bonus);

            bool can = _game.Economy.CanUpgrade(c, p, b, out string reason);
            string next;
            Color nextColor = UITheme.TextDim;
            if (b.Level >= EconomyRules.MaxBuildingLevel) next = "Nivel máximo";
            else if (b.Upgrading) { next = $"Mejorando a nivel {EconomyRules.LevelName(b.Level + 1)}…"; nextColor = UITheme.Accent; }
            else
            {
                int target = b.Level + 1;
                float gain = b.Def.Income * (EconomyRules.LevelOutput(target) - EconomyRules.LevelOutput(b.Level)) * (1f + b.LocationBonus);
                float extraUpkeep = b.Def.UpkeepPerPeriod * (EconomyRules.LevelUpkeep(target) - EconomyRules.LevelUpkeep(b.Level));
                next = $"Nivel {EconomyRules.LevelName(target)}:  <b>{UIFactory.FormatMoney(EconomyRules.UpgradeCost(b.Def, target))}</b> · " +
                       $"{EconomyRules.UpgradeDays(b.Def, target)} días\n<size=12><color=#a8a08c>{UIFactory.FormatMoney(gain, true)} ingresos · " +
                       $"{UIFactory.FormatMoney(extraUpkeep, true)} mant.</color></size>";
                nextColor = can ? UITheme.Text : UITheme.Negative;
            }
            Cell(row, next, w[4], 14, nextColor, UpgradeAligns[4]);

            var cell = UIFactory.Rect("Button", row);
            cell.Layout(-1, w[5], 0);
            var button = UIFactory.Button(cell, "Mejorar", () =>
            {
                if (!_game.Economy.StartUpgrade(c, p, b, out string why)) Notifications.Show(why, "upgrade", true);
            }, 15);
            ((RectTransform)button.transform).Stretch(0, 0, 3, 3);
            button.SetInteractable(can);
            if (!can && reason != null) UITooltip.Attach(button, reason);
        }

        /// <summary>Efecto especial de un edificio con un nivel dado (null si no tiene).</summary>
        private static string SpecialEffect(BuildingInstance b, int level)
        {
            float m = EconomyRules.LevelOutput(level);
            switch (b.Type)
            {
                case BuildingType.CivilianFactory: return $"+{EconomyRules.ConstructionPerCivilianFactory * m:0.00} puntos/día de construcción";
                case BuildingType.Bank: return $"+{EconomyRules.BankInvestmentBonusPerUnit * m * 100f:0.#} % inversión (tope {EconomyRules.BankMaxInvestmentBonus * 100f:0} %)";
                case BuildingType.DistributionCenter: return $"+{EconomyRules.DistributionBonusPerUnit * m * 100f:0.#} % a comercios (tope {EconomyRules.DistributionMaxBonus * 100f:0} %)";
                case BuildingType.Hotel: return b.LocationBonus > 0f ? $"ubicación +{b.LocationBonus * 100f:0} %" : "sin bonus de ubicación";
                default: return null;
            }
        }

        /// <summary>Explicación de las bonificaciones especiales de un edificio (para tooltips).</summary>
        private static string BonusTooltip(BuildingDef def)
        {
            switch (def.Type)
            {
                case BuildingType.CivilianFactory:
                    return $"Cada industria civil aporta +{EconomyRules.ConstructionPerCivilianFactory:0.00} puntos/día a la capacidad de construcción nacional " +
                           $"(×{EconomyRules.LevelOutput(2):0.0} en nivel II, ×{EconomyRules.LevelOutput(3):0.0} en nivel III).";
                case BuildingType.Bank:
                    return $"Cada banco suma +{EconomyRules.BankInvestmentBonusPerUnit * 100f:0} % a la inversión privada nacional (más con mejoras), " +
                           $"hasta un máximo de +{EconomyRules.BankMaxInvestmentBonus * 100f:0} % para todo el país.";
                case BuildingType.DistributionCenter:
                    return $"Cada centro suma +{EconomyRules.DistributionBonusPerUnit * 100f:0} % a los ingresos de los centros comerciales del país " +
                           $"(más con mejoras), hasta un máximo de +{EconomyRules.DistributionMaxBonus * 100f:0} %.";
                case BuildingType.Hotel:
                    return $"Rinde hasta +{EconomyRules.HotelMaxLocationBonus * 100f:0} % más según la provincia donde se construya:\n" +
                           $"· hasta +{EconomyRules.HotelDevelopmentBonus * 100f:0} % por desarrollo alto\n" +
                           $"· +{EconomyRules.HotelCityBonus * 100f:0} % en la capital o en provincias con una ciudad de más de " +
                           $"{UIFactory.FormatNumber(EconomyRules.HotelCityPopulation)} habitantes.\n" +
                           "En el modo de construcción, el tooltip de cada provincia indica su bonificación.";
                default:
                    return null;
            }
        }

        // --- Proyectos ---
        private static readonly float[] ProjectWidths = { 28f, -1f, 150f, 150f, 86f, 96f, 118f };
        private static readonly TextAnchor[] ProjectAligns =
        {
            TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, TextAnchor.MiddleCenter,
            TextAnchor.MiddleRight, TextAnchor.MiddleRight, TextAnchor.MiddleCenter,
        };

        private void BuildProjects(Country c)
        {
            var e = c.Economy;
            Fixed(() =>
            {
                ConstructionStrip(e);
                var note = Note($"Las obras avanzan por orden de prioridad. Al cancelar se recupera el {EconomyRules.CancelRefundShare * 100:0} % de lo que falta por construir.");
                Tip(note, $"Cada día, la capacidad de construcción se reparte de arriba abajo (máx. {EconomyRules.MaxDailyProgressPerProject:0} punto/día por obra).\n" +
                          "Las últimas de la lista esperan si no llega para todas.\nUsa ▲ ▼ para cambiar el orden.");
                if (e.Projects.Count > 0)
                    TableHeader(new[] { "", "Edificio", "Provincia", "Progreso", "Tiempo", "Reembolso", "Prioridad" }, ProjectWidths, ProjectAligns);
            });
            if (e.Projects.Count == 0)
            {
                Space(6);
                Paragraph("No hay construcciones en curso. Elige un edificio en la pestaña Construcción.", UITheme.TextDim);
                return;
            }
            for (int i = 0; i < e.Projects.Count; i++) ProjectRow(c, e.Projects[i], i);
        }

        private void ProjectRow(Country c, ConstructionProject p, int index)
        {
            var w = ProjectWidths;
            var rowBg = RowBackground(index);
            var row = (RectTransform)rowBg.transform;
            row.Layout(44).flexibleHeight = 0;
            if (p == _focusProject)
            {
                // Obra pulsada en el mapa: resaltada con el dorado de selección.
                rowBg.color = new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.16f);
                var outline = rowBg.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.7f);
                outline.effectDistance = new Vector2(1f, -1f);
                _focusRow = row;
            }
            var h = UIFactory.HorizontalLayout(row.gameObject, 8, new RectOffset(8, 8, 6, 6));
            h.childForceExpandHeight = true;

            UIFactory.Icon(row, p.Building.Icon, UITheme.Accent).Layout(preferredWidth: w[0]);
            Cell(row, $"<color=#a8a08c>{index + 1}.</color>  {p.DisplayName}", w[1], TextSize, UITheme.Text, ProjectAligns[1]);
            var province = _game.World.GetProvince(p.ProvinceId);
            Cell(row, province != null ? province.Name : "-", w[2], 15, UITheme.TextDim, ProjectAligns[2]);

            float days = EconomySystem.DaysRemaining(c.Economy, p);
            bool waiting = days < 0;
            var progressCell = UIFactory.Rect("Progress", row);
            progressCell.Layout(-1, w[3], 0);
            var bar = UIFactory.ProgressBar(progressCell, p.Progress01, waiting ? UITheme.TextDim : UITheme.Accent);
            bar.Place(new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 20));
            var pct = UIFactory.Label(bar, $"{p.Progress01 * 100:0} %", 13, UITheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            pct.rectTransform.Stretch();
            pct.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);

            Cell(row, waiting ? "En espera" : $"{Mathf.CeilToInt(days)} días", w[4], 15, waiting ? UITheme.Negative : UITheme.Text, ProjectAligns[4]);
            Cell(row, UIFactory.FormatMoney(p.RefundIfCancelled), w[5], 15, UITheme.TextDim, ProjectAligns[5]);

            var buttons = UIFactory.Rect("Buttons", row);
            buttons.Layout(-1, w[6], 0);
            var hb = UIFactory.HorizontalLayout(buttons.gameObject, 4, new RectOffset(0, 0, 0, 0));
            hb.childForceExpandWidth = true;
            var up = UIFactory.Button(buttons, "▲", () => _game.Economy.MoveProject(c, p, -1), 14);
            up.SetInteractable(index > 0);
            UITooltip.Attach(up, index > 0 ? "Subir prioridad" : "Ya es la primera de la lista");
            var down = UIFactory.Button(buttons, "▼", () => _game.Economy.MoveProject(c, p, +1), 14);
            bool last = index >= c.Economy.Projects.Count - 1;
            down.SetInteractable(!last);
            UITooltip.Attach(down, last ? "Ya es la última de la lista" : "Bajar prioridad");
            var cancel = UIFactory.Button(buttons, "X", () => _game.Economy.CancelProject(c, p), 14);
            cancel.GetComponentInChildren<Text>().color = UITheme.Negative;
            UITooltip.Attach(cancel, $"Cancelar la obra (se recuperan {UIFactory.FormatMoney(p.RefundIfCancelled)})");
        }

        // --- Empresas ---
        private void BuildCompanies(Country c)
        {
            var e = c.Economy;
            var r = EconomyRules.Evaluate(e);
            Section("Capital privado", "briefcase");
            KeyValue(_into, "Empresas privadas", e.PrivateCompanies.ToString());
            KeyValue(_into, "Capital privado", UIFactory.FormatMoney(e.PrivateCapital));
            KeyValue(_into, $"Impuestos de empresas ({EconomyRules.PeriodDays} días)", UIFactory.FormatMoney(r.PrivateCompanies, true), UITheme.Positive);
            KeyValue(_into, "Puesto mundial", Rank(c, x => x.PrivateCompanies));
            Space(8);
            BuildPrivateInvestment(c, r);
            Space(8);
            if (e.PrivateCompanies == 0 && c.Government == GovernmentType.Communism)
                Note("Economía planificada: el Estado controla toda la producción y no existe capital privado.");
            else if (e.PrivateCompanies == 0)
                Note("Este país todavía no tiene empresas privadas de importancia.");
            Note("Las empresas son una industria especial ligada al capital privado. Surgirán de forma natural " +
                 "en los países que más desarrollen su economía y aportarán ganancias extra (sistema en desarrollo).");
        }

        private void BuildPrivateInvestment(Country c, EconomyReport r)
        {
            var e = c.Economy;
            Section("Inversión privada nacional", "coins");
            if (!EconomyRules.HasPrivateSector(c))
            {
                Note("En una economía planificada no se acumula inversión privada.");
                return;
            }
            var g = EconomyRules.EvaluateGrowth(c, r);
            float perPeriod = EconomyRules.PrivateInvestmentPerPeriod(c, r, g);
            float threshold = EconomyRules.CompanyInvestmentThreshold;
            float progress = (float)e.PrivateInvestment / threshold;
            KeyValue(_into, "Inversión acumulada",
                $"{UIFactory.FormatMoney(e.PrivateInvestment)} / {UIFactory.FormatMoney(threshold)}  ({Mathf.Min(progress, 1f) * 100f:0} %)",
                progress >= 1f ? UITheme.Positive : UITheme.Text);
            var bar = UIFactory.ProgressBar(_into, progress, UITheme.Accent);
            bar.Layout(14).flexibleHeight = 0;
            string factors =
                "<b>Inversión privada por periodo</b>\n" +
                $"Base: {EconomyRules.PrivateInvestmentShare * 100f:0} % de los ingresos de los edificios económicos " +
                $"({UIFactory.FormatMoney(r.BuildingIncome)})\n" +
                $"Crecimiento del PIB ({Percent(g.Total)}):  +{EconomyRules.PrivateInvestmentMaxGrowthBonus * Mathf.Clamp01(g.Total / EconomyRules.PrivateInvestmentGrowthReference) * 100f:0} %\n" +
                $"Salud financiera ({g.FinancialHealth * 100f:0} %):  +{EconomyRules.PrivateInvestmentMaxFinancialBonus * g.FinancialHealth * 100f:0} %\n" +
                $"Bancos:  +{EconomyRules.BankInvestmentBonus(e) * 100f:0} %  (máx. +{EconomyRules.BankMaxInvestmentBonus * 100f:0} %)\n" +
                $"<b>Multiplicador total: ×{EconomyRules.PrivateInvestmentMultiplier(e, g):0.00}</b>";
            UITooltip.Attach(KeyValue(_into, $"Inversión generada cada {EconomyRules.PeriodDays} días",
                UIFactory.FormatMoney(perPeriod, true), perPeriod > 0f ? UITheme.Positive : UITheme.TextDim), factors);
            KeyValue(_into, "Generada en el último periodo",
                e.LastPrivateInvestment > 0f ? UIFactory.FormatMoney(e.LastPrivateInvestment, true) : "Aún sin datos (primer periodo)",
                UITheme.TextDim);
            KeyValue(_into, "Periodos estimados hasta el umbral",
                progress >= 1f ? "Umbral alcanzado" : perPeriod > 0f
                    ? $"{Mathf.CeilToInt((threshold - (float)e.PrivateInvestment) / perPeriod)} periodos"
                    : "Sin inversión");
            Note("La inversión privada crece con la actividad económica, el crecimiento del PIB, la salud financiera del " +
                 "Estado y los bancos (pasa el ratón por «Inversión generada» para ver el desglose). Su uso para crear o ampliar " +
                 "empresas se añadirá más adelante; por ahora solo acumula progreso.");
        }

        // ================================================================== Economía

        // --- Resumen ---
        private void BuildEconomySummary(Country c)
        {
            var r = EconomyRules.Evaluate(c.Economy);
            var g = EconomyRules.EvaluateGrowth(c, r);
            string tip = GrowthTooltip(g);
            string state = r.State == BalanceState.Positive ? "Superávit" : r.State == BalanceState.Negative ? "Déficit" : "Equilibrio";

            // Lo primero que se ve: si gana o pierde dinero, cuánto, cuánto ingresa y gasta, y cómo crece el PIB.
            StatStrip(StripStyle.Hero,
                new Stat($"Balance cada {EconomyRules.PeriodDays} días", UIFactory.FormatMoney(r.Balance, true), BalanceColor(r.State),
                    $"Ingresos menos gastos de cada periodo. El balance se suma al tesoro cada {EconomyRules.PeriodDays} días.", state),
                new Stat("Ingresos", UIFactory.FormatMoney(r.Income), UITheme.Positive, "Total de ingresos por periodo (desglose debajo)."),
                new Stat("Gastos", UIFactory.FormatMoney(-r.Expenses), UITheme.Negative, "Total de gastos por periodo (desglose debajo)."),
                new Stat("Crecimiento del PIB", Percent(g.Total), g.Total > 0f ? UITheme.Positive : UITheme.Text, tip, "al año"));
            Space(4);

            var shown = new List<BuildingDef>();
            foreach (var def in BuildingCatalog.All)
                if (def.Category == BuildingCategory.Economic && c.Economy.Count(def.Type) > 0) shown.Add(def);
            var columns = Columns(2, 28);
            var income = columns[0];
            Section("Ingresos", "coins", income);
            foreach (var def in shown) KeyValue(income, def.Name, Money(r.IncomeByType[(int)def.Type]), valueWidth: 110);
            if (r.DistributionBonus > 0f)
                UITooltip.Attach(KeyValue(income, "Distribución (extra comercial)", Money(r.DistributionBonus), valueWidth: 110), BonusTooltip(BuildingCatalog.Get(BuildingType.DistributionCenter)));
            float gdpGrowth = c.Economy.InitialGdp > 0f ? (c.Gdp / c.Economy.InitialGdp - 1f) * 100f : 0f;
            UITooltip.Attach(KeyValue(income, $"Recaudación por PIB (+{c.Economy.RevenueBonusShare * 100f:0.0} %)",
                    Money(r.RevenueBonus), valueWidth: 110),
                $"<b>Bonificación a la recaudación</b>\nEl PIB ha crecido {Percent(gdpGrowth)} desde 1936.\n" +
                $"+{EconomyRules.RevenueBonusPerGdpGrowth * 10f:0} % de recaudación por cada +10 % de PIB, hasta +{EconomyRules.MaxRevenueBonus * 100f:0} %.\n" +
                "Se aplica a los ingresos de los edificios económicos y las empresas.\nSe actualiza en cada cobro.");
            KeyValue(income, "Empresas privadas (impuestos)", Money(r.PrivateCompanies), valueWidth: 110);
            KeyValue(income, "Exportaciones", Money(r.Exports), valueWidth: 110);
            TotalRow(income, "Total ingresos", UIFactory.FormatMoney(r.Income, true), UITheme.Positive);

            var expenses = columns[1];
            Section("Gastos", "chart", expenses);
            KeyValue(expenses, "Mantenimiento de edificios económicos", Money(r.EconomicUpkeep), valueWidth: 110);
            KeyValue(expenses, "Mantenimiento de instalaciones militares", Money(r.MilitaryInstallationsUpkeep), valueWidth: 110);
            KeyValue(expenses, "Importaciones", Money(r.Imports), valueWidth: 110);
            KeyValue(expenses, $"Administración ({EconomyRules.AdministrationShare * 100:0} % de ingresos)", Money(r.Administration), valueWidth: 110);
            KeyValue(expenses, "Gastos fijos del Estado", Money(r.FixedExpenses), valueWidth: 110);
            TotalRow(expenses, "Total gastos", UIFactory.FormatMoney(-r.Expenses), UITheme.Negative);
            Space(6);

            Section("Balance del país", "chart");
            KeyValue(_into, $"Balance cada {EconomyRules.PeriodDays} días", $"{UIFactory.FormatMoney(r.Balance, true)}  ({state.ToLowerInvariant()})",
                BalanceColor(r.State));
            KeyValue(_into, "Tesoro", UIFactory.FormatMoney(c.Economy.Treasury));
            KeyValue(_into, "Actividad económica", $"{EconomyRules.ActivityLevel(r.Income)}  ·  puesto {Rank(c, x => EconomyRules.Evaluate(x).Income)}");
            Space(6);

            Section("Producto interior bruto", "chart");
            UITooltip.Attach(KeyValue(_into, "PIB actual", "$" + UIFactory.FormatNumber(c.Gdp * 1e6)), tip);
            UITooltip.Attach(KeyValue(_into, "Crecimiento anual estimado",
                $"{Percent(g.Total)}  (actividad {Percent(g.Activity)} · finanzas {Percent(g.Financial)})",
                g.Total > 0f ? UITheme.Positive : UITheme.Text), tip);
            float last = c.Economy.LastGdpChange;
            KeyValue(_into, $"Variación en el último periodo ({EconomyRules.PeriodDays} días)",
                last == 0f ? "Aún sin datos (primer periodo)"
                : $"{(last > 0f ? "+" : "−")}${UIFactory.FormatNumber(Mathf.Abs(last) * 1e6)}  ({Percent(last / Mathf.Max(c.Gdp - last, 1f) * 100f)})",
                last > 0f ? UITheme.Positive : last < 0f ? UITheme.Negative : UITheme.TextDim);
        }

        // --- Comercio internacional ---
        private void BuildTrade(Country c)
        {
            var r = EconomyRules.Evaluate(c.Economy);
            float trade = r.TradeBalance;
            string kind = trade > EconomyRules.NeutralThreshold ? "Superávit" : trade < -EconomyRules.NeutralThreshold ? "Déficit" : "Equilibrada";
            Color tradeColor = trade > EconomyRules.NeutralThreshold ? UITheme.Positive
                : trade < -EconomyRules.NeutralThreshold ? UITheme.Negative : UITheme.Text;
            StatStrip(StripStyle.Hero,
                new Stat($"Balanza comercial ({EconomyRules.PeriodDays} días)", UIFactory.FormatMoney(trade, true), tradeColor,
                    "Exportaciones menos importaciones en cada periodo de cobro.", kind),
                new Stat("Exportaciones", UIFactory.FormatMoney(r.Exports), UITheme.Positive, "Ventas al extranjero por periodo."),
                new Stat("Importaciones", UIFactory.FormatMoney(-r.Imports), UITheme.Negative, "Compras al extranjero por periodo."),
                new Stat("Volumen comercial", UIFactory.FormatMoney(r.Exports + r.Imports), UITheme.Text, "Exportaciones más importaciones por periodo."));
            Space(6);
            Section("Posición mundial", "chart");
            KeyValue(_into, "Puesto en exportaciones", Rank(c, x => x.Exports));
            KeyValue(_into, "Puesto en importaciones", Rank(c, x => x.Imports));
            Space(6);
            Note("Las cifras parten del comercio exterior real de cada país en 1936. " +
                 "Un déficit comercial no es necesariamente malo si la industria lo compensa.");
        }

        // ================================================================== Industria militar

        // --- Resumen ---
        private void BuildMilitarySummary(Country c)
        {
            var e = c.Economy;
            var r = EconomyRules.Evaluate(e);
            int productive = e.MilitaryFactories + e.AircraftFactories + e.Shipyards;
            StatStrip(StripStyle.Normal,
                new Stat("Capacidad de producción", $"{productive} fábricas", UITheme.Accent,
                    $"Terrestres {e.MilitaryFactories} · aéreas {e.AircraftFactories} · navales {e.Shipyards}.\n" +
                    "Cada tipo de fábrica produce para sus propias líneas (ver Producción)."),
                new Stat("Instalaciones militares", (productive + e.MilitaryDepots).ToString(), UITheme.Text,
                    $"{productive} instalaciones productivas y {e.MilitaryDepots} depósitos militares."),
                new Stat("Mantenimiento militar", UIFactory.FormatMoney(-r.MilitaryInstallationsUpkeep), UITheme.Negative,
                    $"Mantenimiento de fábricas, astilleros y depósitos cada {EconomyRules.PeriodDays} días ({PerDay(r.MilitaryInstallationsUpkeep)}).",
                    $"/ {EconomyRules.PeriodDays} días"),
                new Stat("Obras militares", e.ActiveProjects(BuildingCategory.Military).ToString(), UITheme.Text,
                    "Construcciones militares en curso (comparten la capacidad de construcción nacional)."));
            Space(4);
            Section("Instalaciones militares", "swords");
            KeyValue(_into, "Industrias militares", $"{e.MilitaryFactories}   <color=#a8a08c>({Rank(c, x => x.MilitaryFactories)})</color>");
            KeyValue(_into, "Fábricas aeronáuticas", $"{e.AircraftFactories}   <color=#a8a08c>({Rank(c, x => x.AircraftFactories)})</color>");
            KeyValue(_into, "Astilleros", $"{e.Shipyards}   <color=#a8a08c>({Rank(c, x => x.Shipyards)})</color>");
            KeyValue(_into, "Depósitos militares", e.MilitaryDepots.ToString());
            Space(6);
            Section("Producción diaria de equipamiento", "gear");
            foreach (var line in e.Lines)
                KeyValue(_into, line.Equipment.Name, $"{FormatAmount(EconomyRules.DailyOutput(line))} {line.Equipment.Unit}/día",
                    line.Priority == ProductionPriority.Stopped ? UITheme.TextDim : UITheme.Text, valueWidth: 300);
        }

        // --- Producción ---
        private static readonly float[] ProductionWidths = { 28f, -1f, 92f, 120f, 120f, 72f, 86f, 140f };
        private static readonly TextAnchor[] ProductionAligns =
        {
            TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, TextAnchor.MiddleRight, TextAnchor.MiddleRight,
            TextAnchor.MiddleRight, TextAnchor.MiddleRight, TextAnchor.MiddleRight, TextAnchor.MiddleCenter,
        };

        private void BuildProduction(Country c)
        {
            var e = c.Economy;
            Fixed(() =>
            {
                var note = Note("Cada tipo de instalación reparte sus fábricas entre sus líneas según la prioridad. " +
                                "La eficiencia sube mientras la línea trabaja y baja si se detiene.");
                Tip(note, "Industrias militares → infantería, artillería, blindados y suministros.\n" +
                          "Fábricas aeronáuticas → aviones.\nAstilleros → buques.");
                TableHeader(new[] { "", "Producto", "Capacidad", "Producción/día", "Recursos", "Eficiencia", "Almacenado", "Prioridad" },
                    ProductionWidths, ProductionAligns);
            });
            for (int i = 0; i < e.Lines.Length; i++) ProductionRow(c, e.Lines[i], i);
            Space(6);
            bool full = false;
            foreach (var l in e.Lines) full |= l.StorageFull;
            if (full)
                Paragraph("Los depósitos están llenos: la producción terrestre se ha detenido. Construye depósitos militares.", UITheme.Negative);
            Note("La producción está limitada por la capacidad industrial. Los recursos indicados se consumirán cuando se " +
                 "implemente el sistema de materias primas; los modelos concretos de armas se añadirán más adelante.");
        }

        private void ProductionRow(Country c, ProductionLine l, int index)
        {
            var w = ProductionWidths;
            var row = (RectTransform)RowBackground(index).transform;
            row.Layout(48).flexibleHeight = 0;
            var h = UIFactory.HorizontalLayout(row.gameObject, 8, new RectOffset(8, 8, 6, 6));
            h.childForceExpandHeight = true;
            var eq = l.Equipment;
            bool stopped = l.Priority == ProductionPriority.Stopped;
            UIFactory.Icon(row, eq.Icon, stopped ? new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.4f) : UITheme.Accent)
                .Layout(preferredWidth: w[0]);
            Cell(row, $"<b>{eq.Name}</b>\n<size=12><color=#a8a08c>{eq.Product}</color></size>", w[1], 15, UITheme.Text, ProductionAligns[1]);
            var capacity = Cell(row, $"{l.AssignedFactories:0.0} fáb.", w[2], 15, UITheme.Text, ProductionAligns[2]);
            Tip(capacity, $"<b>Capacidad asignada</b>: {l.AssignedFactories:0.0} {PoolName(eq.Pool)}.\n" +
                          $"Las {PoolName(eq.Pool)} disponibles se reparten entre las líneas compatibles según su prioridad " +
                          "(más fábricas cuanto más alta; ninguna si está detenida).");
            Cell(row, $"{FormatAmount(EconomyRules.DailyOutput(l))} {eq.Unit}", w[3], 15, l.StorageFull ? UITheme.Negative : UITheme.Text, ProductionAligns[3]);
            Cell(row, eq.Resources, w[4], 13, UITheme.TextNote, ProductionAligns[4]);
            Cell(row, $"{l.Efficiency * 100:0} %", w[5], 15, stopped ? UITheme.TextDim : UITheme.Text, ProductionAligns[5]);
            Cell(row, FormatAmount((float)c.Economy.Stockpile[(int)eq.Category]), w[6], 15, UITheme.Text, ProductionAligns[6]);
            PriorityControl(row, w[7], l.Priority,
                () => _game.Economy.SetPriority(c, eq.Category, (ProductionPriority)Mathf.Max(0, (int)l.Priority - 1)),
                () => _game.Economy.SetPriority(c, eq.Category, (ProductionPriority)Mathf.Min(3, (int)l.Priority + 1)));
        }

        private static string PoolName(ProductionPool pool) => pool switch
        {
            ProductionPool.Air => "fábricas aeronáuticas",
            ProductionPool.Naval => "astilleros",
            _ => "industrias militares",
        };

        /// <summary>Control de prioridad:  ◄  NORMAL  ►  (el valor en una casilla oscura, las flechas como botones).</summary>
        private static void PriorityControl(Transform row, float width, ProductionPriority priority, Action lower, Action raise)
        {
            var prio = UIFactory.Rect("Priority", row);
            prio.Layout(-1, width, 0);
            var hp = UIFactory.HorizontalLayout(prio.gameObject, 3, new RectOffset(0, 0, 4, 4));
            hp.childForceExpandWidth = false;
            var less = UIFactory.Button(prio, "◄", lower, 13);
            less.Layout(preferredWidth: 28);
            bool canLower = priority > ProductionPriority.Stopped, canRaise = priority < ProductionPriority.High;
            less.SetInteractable(canLower);
            UITooltip.Attach(less, canLower ? "Bajar prioridad" : "Ya está detenida");
            var box = UIFactory.Panel(prio, "Valor", new Color(0f, 0f, 0f, 0.4f), false);
            box.raycastTarget = false;
            box.Layout(flexibleWidth: 1);
            var label = UIFactory.Label(box.transform, EquipmentCatalog.PriorityName(priority).ToUpperInvariant(), 13,
                priority == ProductionPriority.Stopped ? UITheme.Negative : priority == ProductionPriority.High ? UITheme.Accent : UITheme.Text,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            label.rectTransform.Stretch(2, 2, 0, 0);
            Fit(label, 10);
            var more = UIFactory.Button(prio, "►", raise, 13);
            more.Layout(preferredWidth: 28);
            more.SetInteractable(canRaise);
            UITooltip.Attach(more, canRaise ? "Subir prioridad" : "Ya tiene la prioridad máxima");
        }

        // --- Reservas ---
        private static readonly float[] ReserveWidths = { 28f, -1f, 170f, 170f, 170f };
        private static readonly TextAnchor[] ReserveAligns =
            { TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, TextAnchor.MiddleRight, TextAnchor.MiddleRight, TextAnchor.MiddleRight };

        private void BuildReserves(Country c)
        {
            var e = c.Economy;
            float cap = EconomyRules.StorageCapacity(e), used = EconomyRules.StorageUsed(e);
            bool almostFull = used >= cap * 0.95f;

            // Almacenamiento terrestre: lo que ocupa los depósitos.
            Section("Almacenamiento terrestre", "crate");
            StatStrip(StripStyle.Compact,
                new Stat("Espacio ocupado", $"{used:0} / {cap:0}", almostFull ? UITheme.Negative : UITheme.Text,
                    "Capacidad de los depósitos militares ocupada por el equipamiento terrestre."),
                new Stat("Uso de depósitos", $"{(cap > 0 ? used / cap * 100 : 0):0} %", almostFull ? UITheme.Negative : UITheme.Text,
                    almostFull ? "Los depósitos están casi llenos: si se llenan, la producción terrestre se detiene." : null),
                new Stat("Depósitos militares", e.MilitaryDepots.ToString(), UITheme.Text,
                    $"Capacidad base {EconomyRules.BaseStorage:0} + {EconomyRules.StoragePerDepot:0} por depósito."));
            var barRow = UIFactory.Rect("StorageBar", _into);
            barRow.Layout(18).flexibleHeight = 0;
            var bar = UIFactory.ProgressBar(barRow, cap > 0 ? used / cap : 0, almostFull ? UITheme.Negative : UITheme.Accent);
            bar.Stretch(0, 0, 3, 3);
            TableHeader(new[] { "", "Categoría", "Cantidad", "Producción/día", "Espacio ocupado" }, ReserveWidths, ReserveAligns);
            int row = 0;
            for (int i = 0; i < e.Lines.Length; i++)
                if (e.Lines[i].Equipment.StorageWeight > 0) ReserveRow(e, i, row++, true);
            Space(10);

            // Reservas que no usan los depósitos (aviones y buques).
            Section("Reservas independientes", "plane");
            Note("Los aviones se almacenan independientemente de los depósitos terrestres y los buques no consumen capacidad de estos depósitos.");
            TableHeader(new[] { "", "Categoría", "Cantidad", "Producción/día", "" }, ReserveWidths, ReserveAligns);
            row = 0;
            for (int i = 0; i < e.Lines.Length; i++)
                if (e.Lines[i].Equipment.StorageWeight <= 0) ReserveRow(e, i, row++, false);
            Space(8);
            Note("En el futuro, el reclutamiento, el mantenimiento de los ejércitos y el comercio internacional de armas usarán estas reservas.");
        }

        private void ReserveRow(CountryEconomy e, int line, int index, bool land)
        {
            var w = ReserveWidths;
            var l = e.Lines[line];
            var eq = l.Equipment;
            var row = (RectTransform)RowBackground(index).transform;
            row.Layout(36).flexibleHeight = 0;
            UIFactory.HorizontalLayout(row.gameObject, 8, new RectOffset(8, 8, 4, 4));
            UIFactory.Icon(row, eq.Icon, UITheme.Accent).Layout(preferredWidth: w[0]);
            Cell(row, eq.Name, w[1], TextSize, UITheme.Text, ReserveAligns[1]);
            double amount = e.Stockpile[line];
            Cell(row, $"{FormatAmount((float)amount)} {eq.Unit}", w[2], TextSize, UITheme.Text, ReserveAligns[2], FontStyle.Bold);
            Cell(row, $"+{FormatAmount(l.DailyOutput)}", w[3], 15, l.DailyOutput > 0f ? UITheme.Positive : UITheme.TextDim, ReserveAligns[3]);
            Cell(row, land ? $"{amount * eq.StorageWeight:0}" : "", w[4], 15, UITheme.TextDim, ReserveAligns[4]);
        }

        // ================================================================== Ayudas de maquetación

        private static string Money(float millions) => UIFactory.FormatMoney(millions);

        /// <summary>Cantidad por periodo convertida a cantidad diaria.</summary>
        private static string PerDay(float perPeriod) => UIFactory.FormatMoney(perPeriod / EconomyRules.PeriodDays) + "/día";

        private static string FormatAmount(float v) =>
            v >= 1000f ? v.ToString("#,0") : v >= 10f ? v.ToString("0") : v >= 0.1f ? v.ToString("0.0") : v > 0f ? v.ToString("0.00") : "0";

        private static Color BalanceColor(BalanceState s) =>
            s == BalanceState.Positive ? UITheme.Positive : s == BalanceState.Negative ? UITheme.Negative : UITheme.Text;

        private static string CapacityTooltip(float capacity) =>
            "<b>Capacidad de construcción</b>\n" +
            $"Puntos de construcción por día que se reparten entre las obras por orden de prioridad (máx. {EconomyRules.MaxDailyProgressPerProject:0} punto/día por obra).\n" +
            $"Con la capacidad actual avanzan a máxima velocidad hasta {Mathf.FloorToInt(capacity / EconomyRules.MaxDailyProgressPerProject)} obras a la vez.\n" +
            $"Base {EconomyRules.BaseConstruction:0.00} + {EconomyRules.ConstructionPerCivilianFactory:0.00} por cada industria civil (más con mejoras).";

        /// <summary>Puesto del país entre todos los países vivos según un valor ("3.º de 83"; "-" si es 0).</summary>
        private string Rank(Country country, Func<CountryEconomy, float> value)
        {
            float mine = value(country.Economy);
            if (mine <= 0f) return "-";
            int better = 0, total = 0;
            foreach (var c in _game.World.Countries)
            {
                if (!c.IsAlive || c.Economy == null) continue;
                total++;
                if (value(c.Economy) > mine) better++;
            }
            return $"{better + 1}.º de {total}";
        }

        private static void Clear(RectTransform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--) DestroyNow(parent.GetChild(i).gameObject);
        }

        private static void DestroyNow(GameObject go)
        {
            go.SetActive(false);
            go.transform.SetParent(null, false);
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        /// <summary>Construye en el encabezado fijo (no se desplaza con el contenido).</summary>
        private void Fixed(Action build)
        {
            var previous = _into;
            _into = _header;
            build();
            _into = previous;
        }

        /// <summary>Línea fina dorada en el borde inferior (separa bloques sin marcos gruesos).</summary>
        private static void BottomLine(RectTransform parent)
        {
            var line = UIFactory.Panel(parent, "Linea", UITheme.Line, false);
            line.raycastTarget = false;
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            line.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 1));
        }

        /// <summary>
        /// Franja de indicadores principales: cada celda tiene un título pequeño en mayúsculas y la cifra destacada.
        /// Hero = la primera cifra es la protagonista (balance, balanza comercial).
        /// </summary>
        private void StatStrip(StripStyle style, params Stat[] stats)
        {
            float height = style == StripStyle.Compact ? 54f : style == StripStyle.Hero ? 96f : 74f;
            var strip = UIFactory.Panel(_into, "Indicadores", UITheme.Tint, false);
            strip.raycastTarget = false;
            strip.Layout(height).flexibleHeight = 0;
            var h = UIFactory.HorizontalLayout(strip.gameObject, 0, new RectOffset(0, 0, 0, 0));
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            BottomLine(strip.rectTransform);
            for (int i = 0; i < stats.Length; i++)
            {
                var s = stats[i];
                bool main = style == StripStyle.Hero && i == 0;
                var cell = UIFactory.Rect("Stat_" + s.Label, strip.transform);
                cell.Layout(flexibleWidth: main ? 1.5f : 1f);
                if (i > 0)
                {
                    var sep = UIFactory.Panel(cell, "Separador", UITheme.Line, false);
                    sep.raycastTarget = false;
                    sep.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(1, -18));
                }
                int captionSize = style == StripStyle.Compact ? 11 : 12;
                var caption = UIFactory.Label(cell, s.Label.ToUpperInvariant(), captionSize, UITheme.TextDim, TextAnchor.UpperLeft);
                caption.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(14, -7), new Vector2(-20, 16));
                Fit(caption, 9);
                int valueSize = style == StripStyle.Compact ? 19 : main ? 38 : style == StripStyle.Hero ? 23 : 25;
                string value = s.Value + (s.Sub != null ? $"<size={Mathf.Max(12, valueSize / 2)}><color=#a8a08c>  {s.Sub}</color></size>" : "");
                var label = UIFactory.Label(cell, value, valueSize, s.Color, TextAnchor.LowerLeft, FontStyle.Bold);
                float valueHeight = style == StripStyle.Compact ? 28f : main ? 56f : 38f;
                label.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(14, style == StripStyle.Compact ? 5 : 9),
                    new Vector2(-20, valueHeight));
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                Fit(label, Mathf.Min(14, valueSize));
                if (s.Tooltip != null) UITooltip.Attach(cell, s.Tooltip);
            }
        }

        /// <summary>Columnas de igual anchura (devuelve el contenedor de cada una).</summary>
        private RectTransform[] Columns(int count, float spacing)
        {
            var columns = UIFactory.Rect("Columns", _into);
            var le = columns.gameObject.AddComponent<LayoutElement>();
            le.flexibleHeight = 0;
            var h = UIFactory.HorizontalLayout(columns.gameObject, spacing, new RectOffset(0, 0, 0, 0));
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = false;
            h.childAlignment = TextAnchor.UpperLeft;
            var result = new RectTransform[count];
            for (int i = 0; i < count; i++)
            {
                var col = UIFactory.Rect("Column", columns);
                var v = UIFactory.VerticalLayout(col.gameObject, 4, new RectOffset(0, 0, 0, 0));
                v.childForceExpandHeight = false;
                // Anchura preferida 0 + flexible 1: todas las columnas iguales, sea cual sea la longitud de sus textos.
                var cle = col.gameObject.AddComponent<LayoutElement>();
                cle.minWidth = 0;
                cle.preferredWidth = 0;
                cle.flexibleWidth = 1;
                result[i] = col;
            }
            return result;
        }

        private void Section(string title, string icon, Transform parent = null)
        {
            var row = UIFactory.Rect("Section_" + title, parent ?? _into);
            row.Layout(34).flexibleHeight = 0;
            var h = UIFactory.HorizontalLayout(row.gameObject, 8, new RectOffset(0, 0, 6, 6));
            h.childForceExpandHeight = true;
            UIFactory.Icon(row, icon, UITheme.Accent).Layout(preferredWidth: 22);
            UIFactory.Label(row, title, 19, UITheme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold).Layout(flexibleWidth: 1);
            BottomLine(row);
        }

        private void Paragraph(string text, Color? color = null)
        {
            // Sin altura fija: el Text calcula la suya según el ancho disponible.
            UIFactory.Label(_into, text, TextSize, color ?? UITheme.Text, TextAnchor.UpperLeft);
        }

        /// <summary>Texto informativo: explicaciones y aclaraciones, más pequeño y discreto.</summary>
        private Text Note(string text) => UIFactory.Label(_into, text, 14, UITheme.TextNote, TextAnchor.UpperLeft);

        private void Space(float height) => UIFactory.Rect("Space", _into).Layout(height).flexibleHeight = 0;

        private RectTransform KeyValue(Transform parent, string key, string value, Color? valueColor = null, FontStyle style = FontStyle.Normal,
            float valueWidth = 460f) => KeyValueRow(parent, key, value, valueColor, style, valueWidth);

        private static RectTransform KeyValueRow(Transform parent, string key, string value, Color? valueColor, FontStyle style, float valueWidth)
        {
            var row = UIFactory.Rect("Row_" + key, parent);
            // flexibleHeight = 0: si no, el grupo horizontal (que expande a sus hijos) declara la fila flexible y
            // la lista vertical la estira para llenar la ventana.
            row.Layout(26).flexibleHeight = 0;
            UIFactory.HorizontalLayout(row.gameObject, 8, new RectOffset(0, 0, 0, 0));
            Fit(UIFactory.Label(row, key, TextSize, UITheme.TextDim), 12).Layout(flexibleWidth: 1);
            Fit(UIFactory.Label(row, value, TextSize, valueColor ?? UITheme.Text, TextAnchor.MiddleRight, style == FontStyle.Normal ? FontStyle.Bold : style), 12)
                .Layout(preferredWidth: valueWidth);
            return row;
        }

        /// <summary>Fila de total de una columna: separada por una línea y con la cifra más grande.</summary>
        private static void TotalRow(Transform parent, string key, string value, Color color)
        {
            var row = UIFactory.Rect("Total_" + key, parent);
            row.Layout(32).flexibleHeight = 0;
            UIFactory.HorizontalLayout(row.gameObject, 8, new RectOffset(0, 0, 4, 0));
            var line = UIFactory.Panel(row, "Linea", UITheme.Line, false);
            line.raycastTarget = false;
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            line.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 1));
            UIFactory.Label(row, key, TextSize, UITheme.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Layout(flexibleWidth: 1);
            UIFactory.Label(row, value, 19, color, TextAnchor.MiddleRight, FontStyle.Bold).Layout(preferredWidth: 130);
        }

        /// <summary>Cabecera de tabla: mismas anchuras, márgenes y alineación que sus filas, con una línea debajo.</summary>
        private void TableHeader(string[] cells, float[] widths, TextAnchor[] aligns, int indent = 0)
        {
            var row = UIFactory.Rect("Cabecera", _into);
            row.Layout(26).flexibleHeight = 0;
            UIFactory.HorizontalLayout(row.gameObject, 8, new RectOffset(8 + indent, 8, 0, 2));
            for (int i = 0; i < cells.Length; i++)
                Cell(row, cells[i].ToUpperInvariant(), widths[i], 12, UITheme.TextDim, aligns[i], FontStyle.Bold);
            BottomLine(row);
        }

        /// <summary>Celda de tabla (anchura negativa = columna flexible). El texto se reduce si no cabe.</summary>
        private static Text Cell(Transform row, string text, float width, int size, Color color, TextAnchor align, FontStyle style = FontStyle.Normal)
        {
            var t = UIFactory.Label(row, text, size, color, align, style);
            Fit(t, Mathf.Min(size, 11));
            if (width < 0) t.Layout(-1, 0, 1);
            else t.Layout(preferredWidth: width);
            return t;
        }

        /// <summary>Fondo de fila alterno (muy discreto).</summary>
        private Image RowBackground(int index)
        {
            var bg = UIFactory.Panel(_into, "Fila", index % 2 == 0 ? UITheme.RowAlt : new Color(0, 0, 0, 0), false);
            return bg;
        }

        /// <summary>Tooltip sobre un texto (los textos no reciben el ratón por defecto).</summary>
        private static void Tip(Graphic graphic, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            graphic.raycastTarget = true;
            UITooltip.Attach(graphic, text);
        }

        /// <summary>El texto se reduce (hasta minSize) si no cabe en su rectángulo, en vez de desbordarse.</summary>
        private static Text Fit(Text t, int minSize)
        {
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = minSize;
            t.resizeTextMaxSize = t.fontSize;
            return t;
        }

        private static string Percent(float value, bool sign = true) =>
            (sign && value > 0f ? "+" : value < 0f ? "−" : "") + Mathf.Abs(value).ToString("0.00") + " %";

        /// <summary>Texto del tooltip con el desglose del crecimiento del PIB.</summary>
        private static string GrowthTooltip(GrowthReport g) =>
            "<b>Crecimiento anual estimado del PIB</b>\n" +
            $"Crecimiento base:  {Percent(g.Base)}\n" +
            $"Actividad económica:  {Percent(g.Activity)}\n" +
            $"    · comercio y servicios (talleres, comercio, bancos, distribución, hoteles):  {Percent(g.Commerce)}\n" +
            $"    · industria (civil, bienes de consumo, alimentaria):  {Percent(g.Productive)}\n" +
            $"Salud financiera:  {Percent(g.Financial)}  (máx. {Percent(EconomyRules.FinancialMaxGrowthBonus)})\n" +
            $"<b>Total:  {Percent(g.Total)} al año</b>\n" +
            $"<color=#a8a090>Se aplica proporcionalmente cada {EconomyRules.PeriodDays} días.</color>";
    }
}
