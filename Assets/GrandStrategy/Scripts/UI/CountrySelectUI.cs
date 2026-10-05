using System.Collections.Generic;
using System.Linq;
using GrandStrategy.Core;
using GrandStrategy.Map;
using UnityEngine;
using UnityEngine.UI;

namespace GrandStrategy.UI
{
    /// <summary>
    /// Pantalla de elección de nación: clic en el mapa o en la lista de grandes potencias,
    /// panel con la información del país y botón para empezar a jugar.
    /// </summary>
    public sealed class CountrySelectUI : MonoBehaviour
    {
        private const int GreatPowerCount = 8;

        private GameManager _game;
        private CountryInfoPanel _info;

        public static CountrySelectUI Create(Transform parent, GameManager game)
        {
            var root = UIFactory.Rect("CountrySelect", parent).Stretch();
            var screen = root.gameObject.AddComponent<CountrySelectUI>();
            screen._game = game;
            screen.Build(root);
            return screen;
        }

        private void Build(RectTransform root)
        {
            // Cabecera.
            var top = UIFactory.Panel(root, "TopBanner", UITheme.PanelBg);
            top.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(760, 70));
            var title = UIFactory.Label(top.transform, "ELIGE TU NACIÓN", 30, UITheme.Accent, TextAnchor.UpperCenter, FontStyle.Bold);
            title.rectTransform.Stretch(0, 0, 6, 0);
            var hint = UIFactory.Label(top.transform, "Clic: seleccionar país  ·  Rueda: zoom  ·  WASD / arrastrar con botón derecho: mover",
                15, UITheme.TextDim, TextAnchor.LowerCenter);
            hint.rectTransform.Stretch(0, 0, 0, 8);

            // Grandes potencias.
            var left = UIFactory.Panel(root, "GreatPowers", UITheme.PanelBg);
            // A la derecha: el panel del país ocupa la izquierda.
            left.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -90), new Vector2(340, 80 + GreatPowerCount * 50));
            var list = UIFactory.Rect("List", left.transform).Stretch(14, 14, 14, 14);
            UIFactory.VerticalLayout(list.gameObject, 8, new RectOffset(0, 0, 0, 0));
            UIFactory.Label(list, "GRANDES POTENCIAS", 20, UITheme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold).Layout(30);
            UIFactory.Separator(list);

            IEnumerable<Country> powers = _game.World.Countries.Where(c => c.IsAlive && c.OverlordId < 0)
                .OrderByDescending(c => c.Gdp).Take(GreatPowerCount);
            foreach (var country in powers)
            {
                var c = country;
                var btn = UIFactory.Button(list, "", () =>
                {
                    _game.InspectCountry(c, _game.World.GetProvince(c.CapitalProvinceId));
                    _game.FocusCountry(c, 0.55f);
                }, 18);
                btn.Layout(42);
                var label = btn.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.Stretch(52, 8, 2, 2);
                label.text = c.Name;
                var flag = UIFactory.Rect("Flag", btn.transform).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(36, 24));
                var raw = flag.gameObject.AddComponent<RawImage>();
                raw.texture = FlagGenerator.Get(c);
                raw.raycastTarget = false;
            }

            // Volver.
            var back = UIFactory.Button(root, "◄  Volver al menú", _game.ShowMainMenu, 20);
            ((RectTransform)back.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 20), new Vector2(240, 48));

            _info = CountryInfoPanel.Create(root);
            _info.Closed += () => _game.ClearSelection();
            _info.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (_game != null) _game.SelectionChanged += OnSelectionChanged;
            if (_game != null) OnSelectionChanged(_game.InspectedCountry, _game.InspectedProvince);
        }

        private void OnDisable()
        {
            if (_game != null) _game.SelectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged(Country country, Province province)
        {
            if (country == null)
            {
                _info.Hide();
                return;
            }
            _info.Show(country, province, _game.World, "Jugar con esta nación", () => _game.StartGame(country));
        }
    }
}
