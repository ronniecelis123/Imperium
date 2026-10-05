using GrandStrategy.Core;
using GrandStrategy.Map;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GrandStrategy.UI
{
    /// <summary>Pequeña etiqueta junto al cursor con la provincia y el país bajo el ratón.</summary>
    public sealed class HoverTooltip : MonoBehaviour
    {
        /// <summary>Línea extra opcional (p. ej. si se puede construir en la provincia). null = nada.</summary>
        public static System.Func<Province, string> ExtraInfo;

        private GameManager _game;
        private RectTransform _rect;
        private RectTransform _canvasRect;
        private Text _text;
        private Image _bg;
        private bool _visible;
        private readonly System.Collections.Generic.Dictionary<int, CityInfo> _cityByProvince =
            new System.Collections.Generic.Dictionary<int, CityInfo>();

        public static HoverTooltip Create(Transform parent, GameManager game)
        {
            var bg = UIFactory.Panel(parent, "HoverTooltip", new Color(0.05f, 0.05f, 0.05f, 0.9f));
            bg.raycastTarget = false;
            var tip = bg.gameObject.AddComponent<HoverTooltip>();
            tip._game = game;
            tip._bg = bg;
            tip._rect = bg.rectTransform;
            tip._canvasRect = (RectTransform)parent;
            tip._rect.anchorMin = tip._rect.anchorMax = Vector2.zero;
            tip._rect.pivot = new Vector2(0, 1);
            tip._text = UIFactory.Label(bg.transform, "", 16, UITheme.Text, TextAnchor.MiddleLeft);
            tip._text.rectTransform.Stretch(10, 10, 4, 4);
            tip._text.horizontalOverflow = HorizontalWrapMode.Overflow;
            // La ciudad más valiosa de cada provincia (para mostrar sus puntos de victoria).
            foreach (var city in game.World.Cities)
                if (city.ProvinceId >= 0 && (!tip._cityByProvince.TryGetValue(city.ProvinceId, out var prev) || city.VictoryPoints > prev.VictoryPoints))
                    tip._cityByProvince[city.ProvinceId] = city;
            game.HoverChanged += tip.OnHoverChanged;
            game.StateChanged += _ => tip.OnHoverChanged(game.HoveredProvince);
            tip.OnHoverChanged(null);
            return tip;
        }

        private void OnHoverChanged(Province province)
        {
            bool visible = province != null &&
                           (_game.State == GameState.CountrySelection || _game.State == GameState.Playing);
            _visible = visible;
            _bg.enabled = _text.enabled = visible && !BuildingMarkers.IsHovering;
            if (!visible) return;

            var owner = _game.World.GetOwner(province);
            string ownerText = owner != null ? $"\n<color=#b0a890>{owner.Name}</color>" : "";
            string cityText = "";
            int lines = owner != null ? 2 : 1;
            if (_cityByProvince.TryGetValue(province.Id, out var city))
            {
                string kind = city.IsCapital ? "<color=#ffd463>Capital</color>" : "<color=#efe9d8>Ciudad importante</color>";
                cityText = $"\n{kind}: {city.Name} · <color=#ffd463>{city.VictoryPoints}</color> puntos de victoria";
                lines++;
            }
            string extra = ExtraInfo?.Invoke(province);
            if (!string.IsNullOrEmpty(extra))
            {
                cityText += "\n" + extra;
                lines += 1 + extra.Split('\n').Length - 1;
            }
            _text.text = $"<b>{province.Name}</b>  <color=#b0a890>({province.Terrain.DisplayName()})</color>{ownerText}{cityText}";
            _rect.sizeDelta = new Vector2(_text.preferredWidth + 24, 8 + lines * 22);
        }

        private void LateUpdate()
        {
            bool show = _visible && !BuildingMarkers.IsHovering;
            if (_bg.enabled != show) _bg.enabled = _text.enabled = show;
            if (!show || Mouse.current == null) return;
            Vector2 screen = Mouse.current.position.ReadValue() + new Vector2(18, -14);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out var local);
            // local es relativo al centro del canvas; las anclas del tooltip están en la esquina inferior izquierda.
            _rect.anchoredPosition = local + _canvasRect.rect.size * 0.5f;
        }
    }
}
