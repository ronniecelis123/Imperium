using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GrandStrategy.UI
{
    /// <summary>
    /// Texto de ayuda que aparece junto al cursor al pasar sobre un elemento de la interfaz. Todos los elementos
    /// comparten un único panel, creado la primera vez en el canvas raíz.
    /// </summary>
    public sealed class UITooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static RectTransform _panel;
        private static Text _text;
        private static UITooltip _owner;
        private static object _external;
        /// <summary>Momento en que se oculta el panel si nadie lo reclama antes (evita parpadeos al reconstruir la interfaz).</summary>
        private static float _hideAt = -1f;
        private const float HideDelay = 0.12f;

        private string _content;

        /// <summary>Añade un tooltip a un elemento (le pone un fondo transparente si no recibe el ratón).</summary>
        public static void Attach(Component target, string text)
        {
            if (!target.TryGetComponent<Graphic>(out _))
                target.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            var tip = target.gameObject.GetComponent<UITooltip>() ?? target.gameObject.AddComponent<UITooltip>();
            tip._content = text;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas == null || string.IsNullOrEmpty(_content)) return;
            if (_panel == null) CreatePanel(canvas.transform);
            _owner = this;
            _external = null;
            _hideAt = -1f;
            _text.text = _content;
            _panel.SetAsLastSibling();
            _panel.gameObject.SetActive(true);
            Follow();
        }

        public void OnPointerExit(PointerEventData eventData) => Hide();

        /// <summary>
        /// Muestra el tooltip junto al cursor para algo que no es un elemento de la interfaz (p. ej. un símbolo del
        /// mapa). Se llama cada fotograma mientras dure; text = null mantiene el texto actual.
        /// </summary>
        public static void ShowText(Transform canvas, object owner, string text)
        {
            if (_panel == null) CreatePanel(canvas);
            if (_owner != null) return; // un elemento de la interfaz tiene prioridad
            _hideAt = -1f;
            if (_external != owner || !_panel.gameObject.activeSelf)
            {
                _external = owner;
                _panel.SetAsLastSibling();
                _panel.gameObject.SetActive(true);
            }
            if (text != null && _text.text != text) _text.text = text;
            Follow();
        }

        public static void HideText(object owner)
        {
            if (_external != owner) return;
            _external = null;
            if (_owner == null && _panel != null) _panel.gameObject.SetActive(false);
        }

        private void OnDisable() => Hide();

        private void Update()
        {
            if (_owner == this) Follow();
        }

        private void Hide()
        {
            if (_owner != this) return;
            _owner = null;
            // Se oculta con un pequeño retraso: si el elemento se ha reconstruido, el nuevo lo reclama antes.
            if (_external == null) _hideAt = Time.unscaledTime + HideDelay;
        }

        /// <summary>Oculta el panel cuando vence el retraso (lo llama el propio panel).</summary>
        private sealed class PendingHide : MonoBehaviour
        {
            private void Update()
            {
                if (_hideAt < 0f || Time.unscaledTime < _hideAt) return;
                _hideAt = -1f;
                if (_owner == null && _external == null) gameObject.SetActive(false);
            }
        }

        private static void CreatePanel(Transform canvas)
        {
            var bg = UIFactory.Panel(canvas, "UITooltip", new Color(0.05f, 0.05f, 0.05f, 0.95f));
            bg.raycastTarget = false;
            bg.gameObject.AddComponent<PendingHide>();
            _panel = bg.rectTransform;
            _panel.anchorMin = _panel.anchorMax = Vector2.zero;
            _panel.pivot = new Vector2(0, 1);
            var v = UIFactory.VerticalLayout(bg.gameObject, 0, new RectOffset(12, 12, 8, 8));
            v.childForceExpandHeight = false;
            var fitter = bg.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _text = UIFactory.Label(bg.transform, "", 16, UITheme.Text, TextAnchor.UpperLeft);
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.raycastTarget = false;
        }

        private static void Follow()
        {
            if (_panel == null || Mouse.current == null) return;
            var canvasRect = (RectTransform)_panel.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Mouse.current.position.ReadValue(), null, out var local);
            // Coordenadas desde la esquina inferior izquierda; se mantiene dentro de la pantalla.
            Vector2 pos = local + canvasRect.rect.size * canvasRect.pivot + new Vector2(18, -18);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
            Vector2 size = _panel.rect.size, screen = canvasRect.rect.size;
            if (pos.x + size.x > screen.x) pos.x = screen.x - size.x;
            if (pos.y - size.y < 0) pos.y = size.y;
            _panel.anchoredPosition = pos;
        }
    }
}
