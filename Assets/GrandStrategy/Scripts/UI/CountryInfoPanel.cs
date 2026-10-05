using System;
using GrandStrategy.Map;
using UnityEngine;
using UnityEngine.UI;

namespace GrandStrategy.UI
{
    /// <summary>
    /// Panel con la información de un país, a la izquierda de la pantalla como en HOI4: retrato del
    /// dirigente arriba a la izquierda, bandera y nombre a su lado y debajo las estadísticas.
    /// Se reutiliza en la selección de país y durante la partida; el botón de acción cambia según el contexto.
    /// Para añadir datos nuevos (diplomacia, ejército, tecnología...) basta con añadir filas en Build().
    /// Durante la partida se puede plegar (botón ◀): queda una pestaña pequeña "▶ País" para volver a desplegarla.
    /// </summary>
    public sealed class CountryInfoPanel : MonoBehaviour
    {
        private const float HeaderHeight = 214f;

        private RawImage _portrait, _flag;
        private Image _colorStrip;
        private Text _name, _subtitle, _leader;
        private Text _government, _overlord, _capital, _provinces, _population, _gdp, _manpower, _development, _neighbors;
        private GameObject _provinceSection;
        private Text _provinceName, _provinceInfo;
        private Button _actionButton;
        private Button _closeButton, _collapseButton;
        private Action _action;
        private Button _tab;
        private Text _tabLabel;
        private bool _visible, _collapsed, _collapsible;

        public event Action Closed;
        /// <summary>El jugador ha plegado o desplegado la ficha.</summary>
        public event Action<bool> CollapsedChanged;

        /// <summary>Hay un país mostrado (aunque la ficha esté plegada).</summary>
        public bool IsShown => _visible;
        public bool Collapsed => _collapsed;

        /// <summary>Permite plegar la ficha (solo durante la partida; en la selección de país no).</summary>
        public bool Collapsible
        {
            get => _collapsible;
            set
            {
                _collapsible = value;
                _collapseButton.gameObject.SetActive(value);
                if (!value) _collapsed = false;
                ApplyVisibility();
            }
        }

        public static CountryInfoPanel Create(Transform parent)
        {
            var bg = UIFactory.Panel(parent, "CountryInfoPanel", UITheme.PanelBg);
            var panel = bg.gameObject.AddComponent<CountryInfoPanel>();
            panel.Build(bg.rectTransform);
            panel.BuildTab(parent);
            return panel;
        }

        /// <summary>Pestaña que sustituye a la ficha cuando está plegada.</summary>
        private void BuildTab(Transform parent)
        {
            _tab = UIFactory.Button(parent, "", () => SetCollapsed(false, true), 17);
            var rt = (RectTransform)_tab.transform;
            rt.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -78), new Vector2(230, 40));
            _tabLabel = _tab.GetComponentInChildren<Text>();
            _tabLabel.alignment = TextAnchor.MiddleLeft;
            _tabLabel.rectTransform.Stretch(12, 10, 2, 2);
            _tabLabel.resizeTextForBestFit = true;
            _tabLabel.resizeTextMinSize = 12;
            _tabLabel.resizeTextMaxSize = 17;
            UITooltip.Attach(_tab, "Mostrar la ficha del país");
            _tab.gameObject.SetActive(false);
        }

        /// <summary>Pliega o despliega la ficha. byPlayer = lo ha pedido el jugador (se avisa con CollapsedChanged).</summary>
        public void SetCollapsed(bool collapsed, bool byPlayer = false)
        {
            if (!_collapsible) collapsed = false;
            if (_collapsed == collapsed) return;
            _collapsed = collapsed;
            ApplyVisibility();
            if (byPlayer) CollapsedChanged?.Invoke(collapsed);
        }

        private void ApplyVisibility()
        {
            gameObject.SetActive(_visible && !_collapsed);
            if (_tab != null)
            {
                _tab.gameObject.SetActive(_visible && _collapsed);
                _tab.transform.SetSiblingIndex(transform.GetSiblingIndex());
            }
        }

        private void Build(RectTransform root)
        {
            // Lado izquierdo, debajo de la barra superior.
            root.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -78), new Vector2(450, 760));

            _colorStrip = UIFactory.Panel(root, "ColorStrip", Color.white, false);
            _colorStrip.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 6));

            // --- Cabecera: retrato (arriba a la izquierda), bandera y nombre ---
            var portraitFrame = UIFactory.Panel(root, "PortraitFrame", new Color(0.05f, 0.04f, 0.03f, 1f));
            portraitFrame.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -18), new Vector2(141, 186));
            _portrait = UIFactory.Rect("Portrait", portraitFrame.transform).Stretch(3, 3, 3, 3).gameObject.AddComponent<RawImage>();
            _portrait.raycastTarget = false;

            var flagFrame = UIFactory.Panel(root, "FlagFrame", Color.black);
            flagFrame.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(170, -20), new Vector2(96, 64));
            _flag = UIFactory.Rect("Flag", flagFrame.transform).Stretch(2, 2, 2, 2).gameObject.AddComponent<RawImage>();
            _flag.raycastTarget = false;

            _name = UIFactory.Label(root, "", 23, UITheme.Text, TextAnchor.UpperLeft, FontStyle.Bold);
            _name.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(170, -92), new Vector2(-186, 60));
            _subtitle = UIFactory.Label(root, "", 15, UITheme.TextDim, TextAnchor.UpperLeft);
            _subtitle.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(170, -152), new Vector2(-186, 22));
            _leader = UIFactory.Label(root, "", 16, UITheme.Accent, TextAnchor.UpperLeft, FontStyle.Italic);
            _leader.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(170, -176), new Vector2(-186, 40));

            // --- Estadísticas ---
            var content = UIFactory.Rect("Content", root).Stretch(18, 18, HeaderHeight, 18);
            UIFactory.VerticalLayout(content.gameObject, 5, new RectOffset(0, 0, 0, 0));
            UIFactory.Separator(content);
            _government = Row(content, "Gobierno");
            _overlord = Row(content, "Soberano");
            _capital = Row(content, "Capital");
            _provinces = Row(content, "Provincias");
            _population = Row(content, "Población");
            _gdp = Row(content, "PIB (dólares de 1990)");
            _manpower = Row(content, "Mano de obra");
            _development = Row(content, "Desarrollo medio");
            _neighbors = Row(content, "Países vecinos");

            // Sección de la provincia pulsada.
            var prov = UIFactory.Rect("ProvinceSection", content);
            _provinceSection = prov.gameObject;
            UIFactory.VerticalLayout(_provinceSection, 4, new RectOffset(0, 0, 8, 0));
            UIFactory.Separator(prov);
            _provinceName = UIFactory.Label(prov, "", 19, UITheme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            _provinceName.Layout(28);
            _provinceInfo = UIFactory.Label(prov, "", 16, UITheme.TextDim, TextAnchor.UpperLeft);
            _provinceInfo.Layout(66);

            var spacer = UIFactory.Rect("Spacer", content);
            spacer.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;

            _actionButton = UIFactory.Button(content, "Acción", () => _action?.Invoke(), 22);
            _actionButton.Layout(52);

            _closeButton = UIFactory.Button(root, "X", Hide, 18);
            ((RectTransform)_closeButton.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-8, -14), new Vector2(30, 30));
            UITooltip.Attach(_closeButton, "Cerrar la ficha");
            _collapseButton = UIFactory.Button(root, "◄", () => SetCollapsed(true, true), 15);
            ((RectTransform)_collapseButton.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-44, -14), new Vector2(30, 30));
            UITooltip.Attach(_collapseButton, "Plegar la ficha (se puede volver a abrir desde la pestaña de la izquierda)");
            _collapseButton.gameObject.SetActive(false);
        }

        private static Text Row(Transform parent, string label)
        {
            var row = UIFactory.Rect("Row_" + label, parent);
            row.Layout(26);
            UIFactory.HorizontalLayout(row.gameObject, 8, new RectOffset(0, 0, 0, 0));
            var l = UIFactory.Label(row, label, 17, UITheme.TextDim);
            l.Layout(flexibleWidth: 1);
            var v = UIFactory.Label(row, "-", 17, UITheme.Text, TextAnchor.MiddleRight, FontStyle.Bold);
            v.Layout(preferredWidth: 210);
            return v;
        }

        /// <summary>Muestra el panel. actionLabel null = sin botón de acción.</summary>
        public void Show(Country country, Province province, WorldData world, string actionLabel, Action action,
            bool actionEnabled = true, bool closable = true)
        {
            if (country == null)
            {
                Hide();
                return;
            }
            _visible = true;
            ApplyVisibility();
            _tabLabel.text = $"►  {country.Name}";

            _portrait.texture = PortraitLoader.Get(country);
            _flag.texture = FlagGenerator.Get(country);
            _colorStrip.color = country.Color;
            _name.text = country.Name;
            _subtitle.text = $"[{country.Tag}]  ·  {country.Government.DisplayName()}";
            _leader.text = string.IsNullOrEmpty(country.Leader) ? "" : country.Leader;

            var capital = world.GetProvince(country.CapitalProvinceId);
            var overlord = world.GetCountry(country.OverlordId);
            _government.text = country.Government.DisplayName();
            _overlord.transform.parent.gameObject.SetActive(overlord != null);
            _overlord.text = overlord != null ? overlord.Name : "-";
            // La ciudad capital (no la provincia que la contiene: Washington D.C. está en la provincia de Maryland).
            CityInfo capitalCity = null;
            foreach (var city in world.Cities)
                if (city.IsCapital && city.OwnerId == country.Id) { capitalCity = city; break; }
            _capital.text = capitalCity != null ? capitalCity.Name : capital != null ? capital.Name : "-";
            _provinces.text = country.Provinces.Count.ToString();
            _population.text = UIFactory.FormatNumber(country.Population);
            _gdp.text = "$" + UIFactory.FormatNumber(country.Gdp * 1e6);
            _manpower.text = UIFactory.FormatNumber(country.Manpower);
            _development.text = country.AverageDevelopment.ToString("0.0") + " / 10";
            _neighbors.text = country.Neighbors.Count.ToString();

            bool showProvince = province != null && !province.IsWater;
            _provinceSection.SetActive(showProvince);
            if (showProvince)
            {
                _provinceName.text = province.Name + (province.Id == country.CapitalProvinceId ? "  (capital)" : "");
                _provinceInfo.text =
                    $"Terreno: {province.Terrain.DisplayName()}{(province.Coastal ? "  ·  Costera" : "")}\n" +
                    $"Población: {UIFactory.FormatNumber(province.Population)}\n" +
                    $"Desarrollo: {province.Development} / 10";
            }

            _action = action;
            _actionButton.gameObject.SetActive(actionLabel != null);
            if (actionLabel != null)
            {
                _actionButton.SetLabel(actionLabel);
                _actionButton.SetInteractable(actionEnabled && action != null);
            }
            _closeButton.gameObject.SetActive(closable);
        }

        public void Hide()
        {
            if (!_visible) return;
            _visible = false;
            ApplyVisibility();
            Closed?.Invoke();
        }
    }
}
