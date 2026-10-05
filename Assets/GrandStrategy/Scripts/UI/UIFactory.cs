using System;
using UnityEngine;
using UnityEngine.UI;

namespace GrandStrategy.UI
{
    /// <summary>Paleta de la interfaz: paneles oscuros con bordes dorados, estilo HOI4.</summary>
    public static class UITheme
    {
        public static readonly Color PanelBg = new Color(0.07f, 0.075f, 0.08f, 0.94f);
        public static readonly Color PanelBgLight = new Color(0.12f, 0.125f, 0.12f, 0.96f);
        public static readonly Color Border = new Color(0.62f, 0.52f, 0.32f, 1f);
        public static readonly Color Text = new Color(0.93f, 0.89f, 0.78f, 1f);
        public static readonly Color TextDim = new Color(0.68f, 0.65f, 0.58f, 1f);
        public static readonly Color Accent = new Color(0.88f, 0.72f, 0.36f, 1f);
        public static readonly Color Button = new Color(0.19f, 0.21f, 0.18f, 1f);
        public static readonly Color ButtonHover = new Color(0.33f, 0.32f, 0.23f, 1f);
        public static readonly Color ButtonPressed = new Color(0.1f, 0.1f, 0.09f, 1f);
        public static readonly Color ButtonDisabled = new Color(0.13f, 0.13f, 0.125f, 0.75f);
        public static readonly Color ButtonActive = new Color(0.42f, 0.36f, 0.2f, 1f);
        public static readonly Color ButtonActiveHover = new Color(0.5f, 0.43f, 0.24f, 1f);
        /// <summary>Texto de un botón deshabilitado: apagado pero legible.</summary>
        public static readonly Color TextDisabled = new Color(0.6f, 0.58f, 0.53f, 0.8f);
        /// <summary>Texto informativo (descripciones, requisitos, notas): más discreto que TextDim.</summary>
        public static readonly Color TextNote = new Color(0.6f, 0.58f, 0.52f, 1f);
        /// <summary>Línea fina de separación (dorado tenue).</summary>
        public static readonly Color Line = new Color(0.62f, 0.52f, 0.32f, 0.28f);
        /// <summary>Fondo discreto de bloques destacados (indicadores, tarjetas).</summary>
        public static readonly Color Tint = new Color(1f, 0.95f, 0.8f, 0.035f);
        public static readonly Color Overlay = new Color(0f, 0f, 0f, 0.55f);
        public static readonly Color Positive = new Color(0.55f, 0.82f, 0.45f, 1f);
        public static readonly Color Negative = new Color(0.92f, 0.42f, 0.36f, 1f);
        public static readonly Color RowAlt = new Color(1f, 1f, 1f, 0.04f);
    }

    /// <summary>Creación de UI (uGUI) desde código, para no depender de prefabs mientras el proyecto crece.</summary>
    public static class UIFactory
    {
        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Coloca un RectTransform con anclas, pivote, posición y tamaño.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPosition;
            return rt;
        }

        /// <summary>Estira el RectTransform para llenar al padre con márgenes.</summary>
        public static RectTransform Stretch(this RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color color, bool border = true)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (border)
            {
                var outline = rt.gameObject.AddComponent<Outline>();
                outline.effectColor = UITheme.Border;
                outline.effectDistance = new Vector2(2, -2);
            }
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect("Text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            return t;
        }

        public static Button Button(Transform parent, string label, Action onClick, int fontSize = 22)
        {
            var img = Panel(parent, "Button_" + label, Color.white);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = UITheme.Button;
            colors.highlightedColor = UITheme.ButtonHover;
            colors.selectedColor = UITheme.Button;
            colors.pressedColor = UITheme.ButtonPressed;
            colors.disabledColor = UITheme.ButtonDisabled;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            var text = Label(img.transform, label, fontSize, UITheme.Text, TextAnchor.MiddleCenter);
            text.rectTransform.Stretch(8, 8, 2, 2);
            return button;
        }

        /// <summary>Botón con un icono (UIIcons) a la izquierda del texto; el conjunto queda centrado.</summary>
        public static Button IconButton(Transform parent, string label, string icon, Action onClick, int fontSize = 18)
        {
            var button = Button(parent, label, onClick, fontSize);
            var sprite = UIIcons.Get(icon);
            if (sprite == null) return button;
            var text = button.GetComponentInChildren<Text>();
            float iconSize = Mathf.Round(fontSize * 1.3f), gap = 7f;
            // El texto se desplaza a la derecha lo que ocupa el icono, y el icono se pega a su izquierda.
            text.rectTransform.Stretch(8 + iconSize + gap, 8, 2, 2);
            var img = Rect("Icon", button.transform).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = UITheme.Accent;
            img.raycastTarget = false;
            img.preserveAspect = true;
            float shift = (iconSize + gap) * 0.5f;
            img.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(shift - text.preferredWidth * 0.5f - gap - iconSize * 0.5f, 0f), new Vector2(iconSize, iconSize));
            return button;
        }

        /// <summary>Icono suelto (UIIcons) teñido de un color.</summary>
        public static Image Icon(Transform parent, string icon, Color color)
        {
            var img = Rect("Icon_" + icon, parent).gameObject.AddComponent<Image>();
            img.sprite = UIIcons.Get(icon);
            img.color = color;
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        /// <summary>Barra de progreso simple (0..1).</summary>
        public static RectTransform ProgressBar(Transform parent, float value, Color fill)
        {
            var bg = Panel(parent, "ProgressBar", new Color(0f, 0f, 0f, 0.45f), false);
            bg.raycastTarget = false;
            var bar = Panel(bg.transform, "Fill", fill, false);
            bar.raycastTarget = false;
            var rt = bar.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return bg.rectTransform;
        }

        /// <summary>Interruptor (casilla + texto) con el estilo del juego.</summary>
        public static Toggle Toggle(Transform parent, string label, bool value, Action<bool> onChanged, int fontSize = 18)
        {
            var row = Rect("Toggle_" + label, parent);
            row.Layout(36);
            var toggle = row.gameObject.AddComponent<Toggle>();

            var box = Panel(row, "Box", new Color(0.12f, 0.12f, 0.11f, 1f));
            box.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(2, 0), new Vector2(28, 28));
            var check = Panel(box.transform, "Check", UITheme.Accent, false);
            check.rectTransform.Stretch(5, 5, 5, 5);
            check.raycastTarget = false;

            var text = Label(row, label, fontSize, UITheme.Text);
            text.rectTransform.Stretch(44, 0, 0, 0);

            toggle.targetGraphic = box;
            toggle.graphic = check;
            var colors = toggle.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.2f, 1.1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = Color.white;
            toggle.colors = colors;
            toggle.isOn = value;
            if (onChanged != null) toggle.onValueChanged.AddListener(v => onChanged(v));
            return toggle;
        }

        public static void SetLabel(this Button button, string label)
        {
            var t = button.GetComponentInChildren<Text>();
            if (t != null) t.text = label;
        }

        public static void SetInteractable(this Button button, bool interactable)
        {
            button.interactable = interactable;
            var t = button.GetComponentInChildren<Text>();
            if (t != null) t.color = interactable ? UITheme.Text : UITheme.TextDisabled;
            var icon = button.transform.Find("Icon");
            if (icon != null && icon.TryGetComponent<Image>(out var img))
                img.color = interactable ? UITheme.Accent : new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.35f);
        }

        public static LayoutElement Layout(this Component c, float preferredHeight = -1, float preferredWidth = -1,
            float flexibleWidth = -1)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            le.minHeight = preferredHeight;
            le.preferredWidth = preferredWidth;
            le.flexibleWidth = flexibleWidth;
            return le;
        }

        public static VerticalLayoutGroup VerticalLayout(GameObject go, float spacing, RectOffset padding)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HorizontalLayout(GameObject go, float spacing, RectOffset padding)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = padding;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            return h;
        }

        public static Image Separator(Transform parent)
        {
            var img = Panel(parent, "Separator", new Color(UITheme.Border.r, UITheme.Border.g, UITheme.Border.b, 0.5f), false);
            img.Layout(2);
            img.raycastTarget = false;
            return img;
        }

        /// <summary>12 345 678 → "12,3 M".</summary>
        public static string FormatNumber(double value)
        {
            double abs = Math.Abs(value);
            if (abs >= 1e12) return (value / 1e12).ToString("0.0") + " bill.";
            if (abs >= 1e9) return (value / 1e9).ToString("0.0") + " mil M";
            if (abs >= 1e6) return (value / 1e6).ToString("0.0") + " M";
            if (abs >= 1e3) return (value / 1e3).ToString("0.0") + " K";
            return value.ToString("0");
        }

        /// <summary>Cantidad en millones de $ → "$61.3 M" / "$0.22 M" / "$1.2 mil M" (con signo si se pide).</summary>
        public static string FormatMoney(double millions, bool sign = false) => Economy.MoneyFormat.Millions(millions, sign);

        /// <summary>"$0.22 M / 15 días".</summary>
        public static string FormatPerPeriod(double millions, bool sign = false) => Economy.MoneyFormat.PerPeriod(millions, sign);

        /// <summary>"7.25 puntos/día".</summary>
        public static string FormatCapacity(float points) => points.ToString("0.00") + " puntos/día";

        /// <summary>Barra de desplazamiento vertical fina (dorada) para un ScrollRect.</summary>
        public static Scrollbar VerticalScrollbar(Transform parent)
        {
            var bg = Panel(parent, "Scrollbar", new Color(0f, 0f, 0f, 0.3f), false);
            var bar = bg.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            var area = Rect("Area", bg.transform).Stretch(1, 1, 1, 1);
            var handle = Panel(area, "Handle", new Color(UITheme.Border.r, UITheme.Border.g, UITheme.Border.b, 0.65f), false);
            handle.rectTransform.Stretch();
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;
            var colors = bar.colors;
            colors.highlightedColor = new Color(1.25f, 1.2f, 1.1f, 1f);
            colors.pressedColor = new Color(1.4f, 1.3f, 1.1f, 1f);
            bar.colors = colors;
            return bar;
        }

        /// <summary>
        /// Pone al instante todos los botones (y demás Selectables) de un panel en su color definitivo.
        /// Un botón recién creado arranca en blanco y, al asignarle colores o el estado deshabilitado, hace un
        /// fundido hasta su color: si el panel se reconstruye (p. ej. cada día) eso se ve como un parpadeo.
        /// </summary>
        public static void SnapTransitions(Transform root)
        {
            foreach (var s in root.GetComponentsInChildren<Selectable>(false))
            {
                if (s.transition != Selectable.Transition.ColorTint || s.targetGraphic == null) continue;
                var c = s.colors;
                var target = (s.IsInteractable() ? c.normalColor : c.disabledColor) * c.colorMultiplier;
                s.targetGraphic.CrossFadeColor(target, 0f, true, true);
            }
        }

        /// <summary>Marca un botón como seleccionado (pestaña o modo activo): fondo dorado oscuro y texto dorado.</summary>
        public static void SetSelected(this Button button, bool selected)
        {
            var colors = button.colors;
            colors.normalColor = selected ? UITheme.ButtonActive : UITheme.Button;
            colors.highlightedColor = selected ? UITheme.ButtonActiveHover : UITheme.ButtonHover;
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
            var t = button.GetComponentInChildren<Text>();
            if (t != null && button.interactable)
            {
                t.color = selected ? UITheme.Accent : UITheme.Text;
                t.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
            }
        }
    }
}
