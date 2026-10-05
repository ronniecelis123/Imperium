using System;
using System.Collections.Generic;
using GrandStrategy.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GrandStrategy.UI
{
    /// <summary>
    /// Ventana de ajustes (desde el menú principal y el de pausa): calidad de cada elemento del mapa
    /// (desactivado / bajo / alto), preajustes y opciones generales. Se guarda en <see cref="GameSettings"/>.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        private static readonly string[] LevelNames = { "Desactivado", "Bajo", "Medio", "Alto" };

        private readonly List<Action> _refreshers = new List<Action>();
        private bool _refreshing;

        public static SettingsPanel Create(Transform parent)
        {
            var overlay = UIFactory.Panel(parent, "Settings", UITheme.Overlay, false);
            overlay.rectTransform.Stretch();
            var panel = overlay.gameObject.AddComponent<SettingsPanel>();
            panel.Build(overlay.transform);
            overlay.gameObject.SetActive(false);
            return panel;
        }

        private void Build(Transform root)
        {
            var box = UIFactory.Panel(root, "Box", UITheme.PanelBg);
            box.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800, 850));
            var list = UIFactory.Rect("List", box.transform).Stretch(34, 34, 24, 28);
            UIFactory.VerticalLayout(list.gameObject, 10, new RectOffset(0, 0, 0, 0));

            UIFactory.Label(list, "AJUSTES", 30, UITheme.Accent, TextAnchor.MiddleCenter, FontStyle.Bold).Layout(48);
            UIFactory.Separator(list);
            UIFactory.Label(list, "Gráficos del mapa", 19, UITheme.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold).Layout(30);
            AddLevelRow(list, "Terreno (detalle del suelo)", () => GameSettings.Terrain, v => GameSettings.Terrain = v, false);
            AddLevelRow(list, "Bosques y vegetación", () => GameSettings.Forests, v => GameSettings.Forests = v);
            AddLevelRow(list, "Ciudades y monumentos", () => GameSettings.Cities, v => GameSettings.Cities = v);
            AddLevelRow(list, "Caminos y ferrocarriles", () => GameSettings.Roads, v => GameSettings.Roads = v);
            AddLevelRow(list, "Ríos", () => GameSettings.Rivers, v => GameSettings.Rivers = v);
            AddLevelRow(list, "Nubes", () => GameSettings.Clouds, v => GameSettings.Clouds = v);

            var presets = UIFactory.Rect("Presets", list);
            presets.Layout(42);
            var h = UIFactory.HorizontalLayout(presets.gameObject, 10, new RectOffset(0, 0, 0, 0));
            h.childForceExpandWidth = true;
            UIFactory.Button(presets, "Gráficos altos", () => Preset(DetailLevel.High), 17);
            UIFactory.Button(presets, "Gráficos medios", () => Preset(DetailLevel.Medium), 17);
            UIFactory.Button(presets, "Gráficos bajos", () => Preset(DetailLevel.Low), 17);
            UIFactory.Button(presets, "Desactivar todo", () => Preset(DetailLevel.Off), 17);

            UIFactory.Separator(list);
            UIFactory.Label(list, "General", 19, UITheme.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold).Layout(30);
            AddToggle(list, "Nombres de países en el mapa", () => GameSettings.CountryNames, v => GameSettings.CountryNames = v);
            AddToggle(list, "Iconos de capitales y ciudades", () => GameSettings.CityMarkers, v => GameSettings.CityMarkers = v);
            AddToggle(list, "Ciclo de día y noche", () => GameSettings.DayNight, v => GameSettings.DayNight = v);
            AddToggle(list, "Mover la cámara con el borde de la pantalla", () => GameSettings.EdgeScrolling, v => GameSettings.EdgeScrolling = v);

            var spacer = UIFactory.Rect("Spacer", list);
            spacer.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            UIFactory.Button(list, "Cerrar", Hide, 22).Layout(50);
        }

        private void Preset(DetailLevel level)
        {
            GameSettings.ApplyPreset(level);
            Refresh();
        }

        /// <summary>Fila con el nombre del elemento y botones Desactivado / Bajo / Medio / Alto.</summary>
        private void AddLevelRow(Transform parent, string label, Func<DetailLevel> read, Action<DetailLevel> write, bool allowOff = true)
        {
            var row = UIFactory.Rect("Row_" + label, parent);
            row.Layout(40);
            var h = UIFactory.HorizontalLayout(row.gameObject, 8, new RectOffset(0, 0, 0, 0));
            h.childForceExpandWidth = false;
            UIFactory.Label(row, label, 18, UITheme.Text).Layout(preferredWidth: 250);
            var buttons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                var level = (DetailLevel)i;
                buttons[i] = UIFactory.Button(row, LevelNames[i], () =>
                {
                    if (_refreshing) return;
                    write(level);
                    Refresh();
                }, 16);
                buttons[i].Layout(preferredWidth: 112);
                if (i == 0 && !allowOff) buttons[i].SetInteractable(false);
            }
            _refreshers.Add(() =>
            {
                var current = read();
                for (int i = 0; i < 4; i++)
                {
                    var colors = buttons[i].colors;
                    colors.normalColor = (int)current == i ? UITheme.ButtonActive : UITheme.Button;
                    colors.selectedColor = colors.normalColor;
                    buttons[i].colors = colors;
                }
            });
        }

        private void AddToggle(Transform parent, string label, Func<bool> read, Action<bool> write)
        {
            var toggle = UIFactory.Toggle(parent, label, read(), v =>
            {
                if (!_refreshing) write(v);
            });
            _refreshers.Add(() => toggle.isOn = read());
        }

        public void Show()
        {
            Refresh();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide() => gameObject.SetActive(false);

        private void Refresh()
        {
            _refreshing = true;
            foreach (var r in _refreshers) r();
            _refreshing = false;
        }
    }
}
