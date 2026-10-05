using System;
using GrandStrategy.CameraControl;
using GrandStrategy.Economy;
using GrandStrategy.Map;
using GrandStrategy.Map.Interaction;
using GrandStrategy.Map.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandStrategy.Core
{
    public enum GameState
    {
        Loading,
        MainMenu,
        CountrySelection,
        Playing,
        Paused,
    }

    /// <summary>
    /// Máquina de estados del juego. Recibe los eventos del mapa (hover/clic) y decide qué hacer
    /// según el estado actual. La UI escucha sus eventos; nunca toca el mapa directamente.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; } = GameState.Loading;
        public WorldData World { get; private set; }
        public MapRenderer Map { get; private set; }
        public StrategyCamera CameraRig { get; private set; }
        public MapInteraction Interaction { get; private set; }
        public GameClock Clock { get; private set; }
        public EconomySystem Economy { get; private set; }

        public Country PlayerCountry { get; private set; }
        public Country InspectedCountry { get; private set; }
        public Province InspectedProvince { get; private set; }
        public Province HoveredProvince { get; private set; }

        /// <summary>Modos de mapa disponibles (añadir aquí los nuevos).</summary>
        public readonly IMapMode[] MapModes = { new PoliticalMapMode(), new TerrainMapMode(), new PopulationMapMode(), new BuildingsMapMode() };

        public event Action<GameState> StateChanged;
        public event Action<Country, Province> SelectionChanged;
        public event Action<Province> HoverChanged;
        public event Action<Country> PlayerCountryChanged;

        /// <summary>La UI puede registrar aquí el cierre de su ventana abierta: Esc la cierra antes que nada (true = consumido).</summary>
        public Func<bool> EscapeHandler;

        /// <summary>La UI puede interceptar los clics en el mapa (p. ej. al colocar edificios). true = consumido.</summary>
        public Func<Province, bool> ProvinceClickHandler;
        /// <summary>
        /// Se consulta con cada clic sobre el mapa cuando no hay un modo de colocación activo: si devuelve true, el clic
        /// ya se ha usado (p. ej. sobre el indicador de una obra) y no selecciona la provincia.
        /// </summary>
        public Func<Province, bool> MapClickInterceptor;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Initialize(WorldData world, MapRenderer map, StrategyCamera cameraRig, MapInteraction interaction, GameClock clock,
            EconomySystem economy)
        {
            Clock = clock;
            Economy = economy;
            Clock.SetDate(world.StartDate);
            World = world;
            Map = map;
            CameraRig = cameraRig;
            Interaction = interaction;
            Interaction.ProvinceHovered += OnProvinceHovered;
            Interaction.ProvinceClicked += OnProvinceClicked;
        }

        // ------------------------------------------------------------------ Transiciones

        public void ShowMainMenu()
        {
            PlayerCountry = null;
            Map.SetPlayerCountry(-1);
            ClearSelection();
            SetControls(cameraInput: false, mapInput: false);
            CameraRig.AutoDrift = true;
            CameraRig.FocusOn(new Vector3(Map.WorldSize.x * 0.5f, 0, Map.WorldSize.y * 0.5f), 0.72f);
            SetState(GameState.MainMenu);
        }

        public void StartCountrySelection()
        {
            ClearSelection();
            SetControls(cameraInput: true, mapInput: true);
            CameraRig.AutoDrift = false;
            CameraRig.FocusOn(new Vector3(Map.WorldSize.x * 0.5f, 0, Map.WorldSize.y * 0.5f), 0.82f);
            SetState(GameState.CountrySelection);
        }

        public void StartGame(Country country)
        {
            if (country == null) return;
            PlayerCountry = country;
            Map.SetPlayerCountry(country.Id);
            ClearSelection();
            // Como en HOI4: la partida empieza en pausa; Espacio pone el tiempo en marcha.
            Clock.SetDate(World.StartDate);
            Clock.SetPaused(true);
            Economy.ResetAll();
            SetControls(cameraInput: true, mapInput: true);
            FocusCountry(country);
            SetState(GameState.Playing);
            PlayerCountryChanged?.Invoke(country);
        }

        public void SetPaused(bool paused)
        {
            if (paused && State == GameState.Playing)
            {
                SetControls(cameraInput: false, mapInput: false);
                SetState(GameState.Paused);
            }
            else if (!paused && State == GameState.Paused)
            {
                SetControls(cameraInput: true, mapInput: true);
                SetState(GameState.Playing);
            }
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ Selección

        public void InspectCountry(Country country, Province province = null)
        {
            InspectedCountry = country;
            InspectedProvince = province;
            Map.SetSelectedCountry(country != null ? country.Id : -1);
            Map.SetSelectedProvince(province != null ? province.Id : -1);
            SelectionChanged?.Invoke(country, province);
        }

        public void ClearSelection() => InspectCountry(null);

        public void FocusCountry(Country country, float zoom = 0.42f)
        {
            var capital = World.GetProvince(country.CapitalProvinceId);
            if (capital != null) CameraRig.FocusOn(Map.GetProvinceWorldPosition(capital), zoom);
        }

        public void SetMapMode(IMapMode mode) => Map.SetMapMode(mode);

        // ------------------------------------------------------------------ Eventos del mapa

        private void OnProvinceHovered(Province province)
        {
            HoveredProvince = province;
            Map.SetHoveredProvince(province != null && !province.IsWater ? province.Id : -1);
            // En la pantalla de selección se resalta el país entero al pasar el ratón.
            int hoverCountry = State == GameState.CountrySelection && province != null ? province.OwnerId : -1;
            Map.SetHoveredCountry(hoverCountry);
            HoverChanged?.Invoke(province);
        }

        private void OnProvinceClicked(Province province)
        {
            if (State != GameState.CountrySelection && State != GameState.Playing) return;
            if (State == GameState.Playing && ProvinceClickHandler != null && ProvinceClickHandler(province)) return;
            if (State == GameState.Playing && ProvinceClickHandler == null && MapClickInterceptor != null && MapClickInterceptor(province)) return;
            var owner = World.GetOwner(province);
            if (province == null || province.IsWater || owner == null)
            {
                ClearSelection();
                return;
            }
            InspectCountry(owner, province);
        }

        private void LateUpdate()
        {
            if (Map == null || Clock == null) return;
            Map.SetSun(Clock.SubsolarLongitude, Clock.SolarDeclination, GameSettings.DayNight ? 1f : 0f);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (State == GameState.Playing)
            {
                if (kb.spaceKey.wasPressedThisFrame) Clock.TogglePause();
                if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) Clock.SpeedUp();
                if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) Clock.SlowDown();
            }

            if (!kb.escapeKey.wasPressedThisFrame) return;
            if (State == GameState.Playing && EscapeHandler != null && EscapeHandler()) return;

            switch (State)
            {
                case GameState.CountrySelection:
                    if (InspectedCountry != null) ClearSelection();
                    else ShowMainMenu();
                    break;
                case GameState.Playing:
                    if (InspectedCountry != null) ClearSelection();
                    else SetPaused(true);
                    break;
                case GameState.Paused:
                    SetPaused(false);
                    break;
            }
        }

        private void SetControls(bool cameraInput, bool mapInput)
        {
            CameraRig.InputEnabled = cameraInput;
            Interaction.InteractionEnabled = mapInput;
        }

        private void SetState(GameState state)
        {
            State = state;
            if (Clock != null) Clock.TimeAllowed = state == GameState.Playing;
            if (state != GameState.CountrySelection) Map.SetHoveredCountry(-1);
            if (state != GameState.CountrySelection && state != GameState.Playing) Map.SetHoveredProvince(-1);
            StateChanged?.Invoke(state);
        }
    }
}
