using System.Collections.Generic;
using GrandStrategy.Core;
using GrandStrategy.Economy;
using GrandStrategy.Map;
using GrandStrategy.Map.Rendering;
using UnityEngine;
using UnityEngine.UI;

namespace GrandStrategy.UI
{
    /// <summary>
    /// Interfaz durante la partida: barra superior (nación del jugador, menú central de 6 botones, tesoro e
    /// ingresos, fecha y tiempo), modos de mapa, panel de información del país pulsado y menú de pausa.
    /// </summary>
    public sealed class GameHUD : MonoBehaviour
    {
        private GameManager _game;
        private RawImage _flag;
        private Text _countryName, _date, _speedPips, _treasury, _income;
        private NationWindow _window;
        private BuildPlacement _placement;
        private readonly List<(Button button, System.Func<bool> active)> _menuButtons = new List<(Button, System.Func<bool>)>();
        private Button _playPause;
        private CountryInfoPanel _info;
        private bool _windowWasOpen, _autoCollapsed;
        private GameObject _pauseMenu;
        private readonly List<(IMapMode mode, Button button)> _mapModeButtons = new List<(IMapMode, Button)>();

        private System.Action _openSettings;

        public static GameHUD Create(Transform parent, GameManager game, System.Action openSettings)
        {
            var root = UIFactory.Rect("GameHUD", parent).Stretch();
            var hud = root.gameObject.AddComponent<GameHUD>();
            hud._game = game;
            hud._openSettings = openSettings;
            hud.Build(root);
            return hud;
        }

        private void Build(RectTransform root)
        {
            // --- Barra superior ---
            var bar = UIFactory.Panel(root, "TopBar", UITheme.PanelBg);
            bar.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 64));

            var flagFrame = UIFactory.Panel(bar.transform, "FlagFrame", Color.black);
            flagFrame.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(72, 48));
            var flagButton = flagFrame.gameObject.AddComponent<Button>();
            flagButton.onClick.AddListener(() =>
            {
                if (_game.PlayerCountry == null) return;
                _game.InspectCountry(_game.PlayerCountry, _game.World.GetProvince(_game.PlayerCountry.CapitalProvinceId));
                _game.FocusCountry(_game.PlayerCountry);
            });
            _flag = UIFactory.Rect("Flag", flagFrame.transform).Stretch(2, 2, 2, 2).gameObject.AddComponent<RawImage>();
            _flag.raycastTarget = false;

            _countryName = UIFactory.Label(bar.transform, "", 21, UITheme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            _countryName.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(98, 0), new Vector2(200, 0));
            _countryName.resizeTextForBestFit = true;
            _countryName.resizeTextMinSize = 14;
            _countryName.resizeTextMaxSize = 21;

            // --- Menú central ---
            _placement = BuildPlacement.Create(root, _game);
            _window = NationWindow.Create(root, _game, _placement);
            Notifications.Create(root);
            MapFloatingText.Create(root, _game); // mensajes sobre el mapa (p. ej. «Taller encargado −$50 M»)
            _window.Changed += RefreshMenuButtons;
            _window.Changed += OnWindowChanged;
            _game.EscapeHandler = () =>
            {
                if (_placement.Active)
                {
                    _placement.End();
                    return true;
                }
                if (!_window.IsOpen) return false;
                _window.Close();
                return true;
            };
            var menu = UIFactory.Rect("MainMenu", bar.transform).Stretch(306, 800, 10, 10);
            var menuRow = UIFactory.HorizontalLayout(menu.gameObject, 6, new RectOffset(0, 0, 0, 0));
            menuRow.childForceExpandWidth = true;
            AddMenuButton(menu, "Industria Nacional", "factory", () => ToggleIndustry(IndustryTab.Economic),
                () => _window.ShowingIndustry && _window.CurrentTab == IndustryTab.Economic);
            AddMenuButton(menu, "Economía", "coins", () => ToggleIndustry(IndustryTab.Economy),
                () => _window.ShowingIndustry && _window.CurrentTab == IndustryTab.Economy);
            AddMenuButton(menu, "Industria militar", "swords", () => ToggleIndustry(IndustryTab.Military),
                () => _window.ShowingIndustry && _window.CurrentTab == IndustryTab.Military);
            foreach (var (title, icon) in new[] { ("Investigación", "flask"), ("Diplomacia", "scroll"), ("Ejército", "helmet") })
            {
                var t = title;
                AddMenuButton(menu, t, icon, () => TogglePlaceholder(t), () => _window.PlaceholderTitle == t);
            }

            // --- Tesoro e ingresos (pulsar abre la economía) ---
            var money = UIFactory.Panel(bar.transform, "Money", new Color(0, 0, 0, 0.25f), false);
            money.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-556, 0), new Vector2(232, 50));
            money.gameObject.AddComponent<Button>().onClick.AddListener(() => ToggleIndustry(IndustryTab.Economy));
            _treasury = MoneyField(money.transform, "TESORO", 0f);
            _income = MoneyField(money.transform, $"INGRESOS / {EconomyRules.PeriodDays} DÍAS", 0.5f);

            // --- Fecha y control del tiempo (derecha) ---
            var menuBtn = UIFactory.Button(bar.transform, "≡", () => _game.SetPaused(true), 26);
            ((RectTransform)menuBtn.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(46, 40));
            var plus = UIFactory.Button(bar.transform, "+", () => _game.Clock.SpeedUp(), 24);
            ((RectTransform)plus.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-72, 0), new Vector2(36, 36));
            _speedPips = UIFactory.Label(bar.transform, "", 16, UITheme.Accent, TextAnchor.MiddleCenter);
            _speedPips.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-112, 0), new Vector2(84, 36));
            _playPause = UIFactory.Button(bar.transform, "►", () => _game.Clock.TogglePause(), 20);
            ((RectTransform)_playPause.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-200, 0), new Vector2(44, 40));
            var minus = UIFactory.Button(bar.transform, "−", () => _game.Clock.SlowDown(), 24);
            ((RectTransform)minus.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-250, 0), new Vector2(36, 36));

            _date = UIFactory.Label(bar.transform, "", 17, UITheme.Accent, TextAnchor.MiddleRight, FontStyle.Bold);
            _date.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-296, 0), new Vector2(250, 0));

            // --- Modos de mapa ---
            var modes = UIFactory.Panel(root, "MapModes", UITheme.PanelBg);
            int count = _game.MapModes.Length;
            modes.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 20), new Vector2(140 * count + 24, 70));
            var row = UIFactory.Rect("Row", modes.transform).Stretch(12, 12, 12, 12);
            var h = UIFactory.HorizontalLayout(row.gameObject, 8, new RectOffset(0, 0, 0, 0));
            h.childForceExpandWidth = true;
            foreach (var mode in _game.MapModes)
            {
                var m = mode;
                var b = UIFactory.Button(row, m.Name, () => _game.SetMapMode(m), 18);
                _mapModeButtons.Add((m, b));
            }
            _game.Map.MapModeChanged += _ => RefreshMapModeButtons();
            RefreshMapModeButtons();

            // --- Ayuda de controles ---
            var help = UIFactory.Label(root, "Rueda: zoom  ·  WASD / botón derecho: mover  ·  Clic: info del país  ·  Espacio: pausar tiempo  ·  +/−: velocidad  ·  Esc: menú",
                15, new Color(1, 1, 1, 0.6f), TextAnchor.LowerLeft);
            help.rectTransform.Place(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(20, 20), new Vector2(1100, 30));

            // --- Panel de país ---
            _info = CountryInfoPanel.Create(root);
            _info.Closed += () => _game.ClearSelection();
            _info.Collapsible = true;
            _info.CollapsedChanged += _ => _autoCollapsed = false; // el jugador decide: no se despliega sola
            _info.gameObject.SetActive(false);

            // --- Menú de pausa ---
            var overlay = UIFactory.Panel(root, "PauseMenu", UITheme.Overlay, false);
            overlay.rectTransform.Stretch();
            _pauseMenu = overlay.gameObject;
            var box = UIFactory.Panel(overlay.transform, "Box", UITheme.PanelBg);
            box.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420, 400));
            var list = UIFactory.Rect("List", box.transform).Stretch(30, 30, 24, 30);
            UIFactory.VerticalLayout(list.gameObject, 14, new RectOffset(0, 0, 0, 0));
            UIFactory.Label(list, "PAUSA", 30, UITheme.Accent, TextAnchor.MiddleCenter, FontStyle.Bold).Layout(50);
            UIFactory.Button(list, "Reanudar", () => _game.SetPaused(false), 22).Layout(52);
            UIFactory.Button(list, "Ajustes", () => _openSettings?.Invoke(), 22).Layout(52);
            UIFactory.Button(list, "Menú principal", _game.ShowMainMenu, 22).Layout(52);
            UIFactory.Button(list, "Salir del juego", _game.QuitGame, 22).Layout(52);
            _pauseMenu.SetActive(false);
        }

        private void OnEnable()
        {
            if (_game == null || _game.Clock == null) return;
            _game.SelectionChanged += OnSelectionChanged;
            _game.StateChanged += OnStateChanged;
            _game.Clock.HourPassed += OnHourPassed;
            _game.Clock.StateChanged += RefreshClock;
            _game.Economy.Updated += RefreshTopBar;
            _game.Economy.Changed += RefreshTopBar;      // obras encargadas o canceladas: el tesoro cambia al momento
            _game.Economy.DayProcessed += RefreshTopBar; // edificios terminados cambian el balance
            _game.Economy.BuildingCompleted += OnBuildingCompleted;
            BuildingMarkers.OpenProjectRequested += OnOpenProjectRequested;
            RefreshTopBar();
            RefreshClock();
            OnSelectionChanged(_game.InspectedCountry, _game.InspectedProvince);
            OnStateChanged(_game.State);
        }

        private void OnDisable()
        {
            if (_game == null || _game.Clock == null) return;
            _game.SelectionChanged -= OnSelectionChanged;
            _game.StateChanged -= OnStateChanged;
            _game.Clock.HourPassed -= OnHourPassed;
            _game.Clock.StateChanged -= RefreshClock;
            _game.Economy.Updated -= RefreshTopBar;
            _game.Economy.Changed -= RefreshTopBar;
            _game.Economy.DayProcessed -= RefreshTopBar;
            _game.Economy.BuildingCompleted -= OnBuildingCompleted;
            BuildingMarkers.OpenProjectRequested -= OnOpenProjectRequested;
            _window.Close();
        }

        private void OnHourPassed(System.DateTime _) => _date.text = _game.Clock.FormatDate();

        private void RefreshClock()
        {
            var clock = _game.Clock;
            _date.text = clock.FormatDate();
            _date.color = clock.Paused ? UITheme.TextDim : UITheme.Accent;
            _playPause.SetLabel(clock.Paused ? "►" : "II");
            var pips = new System.Text.StringBuilder();
            for (int i = GameClock.MinSpeed; i <= GameClock.MaxSpeed; i++) pips.Append(i <= clock.Speed ? "■" : "□");
            _speedPips.text = pips.ToString();
        }

        private void OnStateChanged(GameState state)
        {
            _pauseMenu.SetActive(state == GameState.Paused);
            RefreshClock();
        }

        /// <summary>Clic en una obra propia del mapa: Industria Nacional › Proyectos con esa obra señalada.</summary>
        private void OnOpenProjectRequested(Economy.ConstructionProject project)
        {
            if (_placement.Active) return;
            _window.ShowProject(project);
        }

        /// <summary>Aviso breve (sin ventana) cuando termina una obra o mejora del jugador.</summary>
        private void OnBuildingCompleted(Country country, Economy.ConstructionProject project, Province province)
        {
            if (country != _game.PlayerCountry) return;
            Notifications.Show(project.IsUpgrade
                ? $"{project.Building.Name} mejorado a nivel {Economy.EconomyRules.LevelName(project.TargetLevel)} en {province.Name}"
                : $"{project.Building.Name} terminado en {province.Name}", project.Building.Icon);
        }

        private void RefreshTopBar()
        {
            var c = _game.PlayerCountry;
            if (c == null) return;
            _flag.texture = FlagGenerator.Get(c);
            _countryName.text = c.Name;
            if (c.Economy == null) return;
            var report = EconomyRules.Evaluate(c.Economy);
            _treasury.text = UIFactory.FormatMoney(c.Economy.Treasury);
            _treasury.color = c.Economy.Treasury < 0 ? UITheme.Negative : UITheme.Text;
            _income.text = UIFactory.FormatMoney(report.Balance, true);
            _income.color = report.State == BalanceState.Positive ? UITheme.Positive
                : report.State == BalanceState.Negative ? UITheme.Negative : UITheme.Text;
        }

        /// <summary>Etiqueta pequeña con un valor debajo; ocupa media anchura del bloque de dinero.</summary>
        private static Text MoneyField(Transform parent, string caption, float x)
        {
            var area = UIFactory.Rect(caption, parent);
            area.anchorMin = new Vector2(x, 0);
            area.anchorMax = new Vector2(x + 0.5f, 1);
            area.offsetMin = new Vector2(4, 3);
            area.offsetMax = new Vector2(-4, -3);
            var cap = UIFactory.Label(area, caption, 11, UITheme.TextDim, TextAnchor.UpperCenter);
            cap.rectTransform.Stretch();
            cap.horizontalOverflow = HorizontalWrapMode.Overflow;
            var value = UIFactory.Label(area, "", 18, UITheme.Text, TextAnchor.LowerCenter, FontStyle.Bold);
            value.rectTransform.Stretch();
            return value;
        }

        private void AddMenuButton(Transform parent, string label, string icon, System.Action onClick, System.Func<bool> active)
        {
            var b = UIFactory.IconButton(parent, label, icon, onClick, 15);
            // Los nombres largos ("Industria militar", "Investigación") reciben algo más de ancho para caber en una línea.
            b.Layout(flexibleWidth: label.Length > 14 ? 1.75f : label.Length > 10 ? 1.25f : 1f);
            _menuButtons.Add((b, active));
        }

        private void ToggleIndustry(IndustryTab tab)
        {
            if (_placement.Active) _placement.End();
            if (_window.ShowingIndustry && _window.CurrentTab == tab) _window.Close();
            else _window.ShowIndustry(tab);
        }

        private void TogglePlaceholder(string title)
        {
            if (_placement.Active) _placement.End();
            if (_window.PlaceholderTitle == title) _window.Close();
            else _window.ShowPlaceholder(title);
        }

        private void RefreshMenuButtons()
        {
            foreach (var (button, active) in _menuButtons) button.SetSelected(active());
        }

        /// <summary>
        /// Al abrir una ventana principal la ficha del país se pliega sola para dejar sitio; al cerrarla vuelve a
        /// desplegarse (si se plegó sola y el jugador no la ha tocado entretanto).
        /// </summary>
        private void OnWindowChanged()
        {
            bool open = _window.IsOpen;
            if (open == _windowWasOpen) return;
            _windowWasOpen = open;
            if (open && _info.IsShown && !_info.Collapsed)
            {
                _info.SetCollapsed(true);
                _autoCollapsed = true;
            }
            else if (!open && _autoCollapsed)
            {
                _info.SetCollapsed(false);
                _autoCollapsed = false;
            }
        }

        private void RefreshMapModeButtons()
        {
            foreach (var (mode, button) in _mapModeButtons)
                button.SetSelected(mode == _game.Map.MapMode);
        }

        private void OnSelectionChanged(Country country, Province province)
        {
            if (country == null)
            {
                _info.Hide();
                return;
            }
            bool own = country == _game.PlayerCountry;
            // Acción preparada para fases futuras (diplomacia, declarar guerra...).
            _info.Show(country, province, _game.World, own ? "Tu nación" : "Diplomacia (próximamente)", null, false);
            // Con una ventana principal abierta, la ficha nueva aparece plegada para no taparla.
            if (_window.IsOpen && !_info.Collapsed)
            {
                _info.SetCollapsed(true);
                _autoCollapsed = true;
            }
        }
    }
}
