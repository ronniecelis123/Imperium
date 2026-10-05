using GrandStrategy.Core;
using UnityEngine;

namespace GrandStrategy.UI
{
    /// <summary>Crea el canvas y muestra la pantalla que corresponde a cada estado del juego.</summary>
    public sealed class UIManager : MonoBehaviour
    {
        public LoadingScreen Loading { get; private set; }

        private Canvas _canvas;
        private GameManager _game;
        private MainMenuUI _mainMenu;
        private CountrySelectUI _countrySelect;
        private GameHUD _hud;
        private SettingsPanel _settings;

        /// <summary>Se llama al arrancar (antes de tener mundo) para poder mostrar la pantalla de carga.</summary>
        public void CreateLoadingScreen()
        {
            _canvas = UIFactory.CreateCanvas("UI", 10);
            _canvas.transform.SetParent(transform, false);
            Loading = LoadingScreen.Create(_canvas.transform);
        }

        /// <summary>Se llama cuando el mundo ya está generado.</summary>
        public void Initialize(GameManager game)
        {
            _game = game;
            CityMarkers.Create(_canvas.transform, game);
            BuildingMarkers.Create(_canvas.transform, game); // por debajo de las ciudades: no tapa sus nombres
            _mainMenu = MainMenuUI.Create(_canvas.transform, game, ShowSettings);
            _countrySelect = CountrySelectUI.Create(_canvas.transform, game);
            _hud = GameHUD.Create(_canvas.transform, game, ShowSettings);
            HoverTooltip.Create(_canvas.transform, game);
            _settings = SettingsPanel.Create(_canvas.transform);

            _mainMenu.gameObject.SetActive(false);
            _countrySelect.gameObject.SetActive(false);
            _hud.gameObject.SetActive(false);

            Loading.transform.SetAsLastSibling();
            game.StateChanged += OnStateChanged;
        }

        public void HideLoadingScreen() => Loading.gameObject.SetActive(false);

        public void ShowSettings() => _settings.Show();

        private void OnStateChanged(GameState state)
        {
            _mainMenu.gameObject.SetActive(state == GameState.MainMenu);
            _countrySelect.gameObject.SetActive(state == GameState.CountrySelection);
            _hud.gameObject.SetActive(state == GameState.Playing || state == GameState.Paused);
            if (state == GameState.Playing) _settings.Hide();
        }
    }
}
