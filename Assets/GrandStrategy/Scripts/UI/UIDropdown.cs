using System;
using UnityEngine;
using UnityEngine.UI;

namespace GrandStrategy.UI
{
    /// <summary>
    /// Desplegable sencillo con el estilo del juego: un botón "Título: opción ▼" que abre una lista (con
    /// desplazamiento si es larga) justo debajo. Un clic fuera de la lista la cierra. Solo hay una lista abierta.
    /// </summary>
    public static class UIDropdown
    {
        private const float ItemHeight = 30f;
        private const float MaxListHeight = 380f;

        private static GameObject _open;

        public static Button Create(Transform parent, string caption, string[] options, int selected, Action<int> onSelect, int fontSize = 15)
        {
            selected = Mathf.Clamp(selected, 0, options.Length - 1);
            var button = UIFactory.Button(parent, $"{caption}:  <b>{options[selected]}</b>   ▼", null, fontSize);
            var text = button.GetComponentInChildren<Text>();
            text.alignment = TextAnchor.MiddleLeft;
            text.rectTransform.Stretch(12, 10, 2, 2);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = fontSize;
            button.onClick.AddListener(() => Open(button, options, selected, onSelect));
            return button;
        }

        public static void Close()
        {
            if (_open != null) UnityEngine.Object.Destroy(_open);
            _open = null;
        }

        private static void Open(Button anchor, string[] options, int selected, Action<int> onSelect)
        {
            Close();
            var canvas = anchor.GetComponentInParent<Canvas>().rootCanvas;

            // Capa transparente a pantalla completa: un clic fuera de la lista la cierra.
            var blocker = UIFactory.Panel(canvas.transform, "DropdownBlocker", new Color(0f, 0f, 0f, 0f), false);
            blocker.rectTransform.Stretch();
            var close = blocker.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(Close);
            _open = blocker.gameObject;

            var corners = new Vector3[4];
            ((RectTransform)anchor.transform).GetWorldCorners(corners);
            Vector2 bottomLeft = blocker.rectTransform.InverseTransformPoint(corners[0]);
            Vector2 topRight = blocker.rectTransform.InverseTransformPoint(corners[2]);
            float width = Mathf.Max(topRight.x - bottomLeft.x, 240f);
            float height = Mathf.Min(options.Length * (ItemHeight + 2f) + 8f, MaxListHeight);
            // Si no cabe por debajo, se abre hacia arriba.
            float canvasBottom = -blocker.rectTransform.rect.height * 0.5f;
            bool up = bottomLeft.y - height < canvasBottom + 10f;

            var list = UIFactory.Panel(blocker.transform, "List", UITheme.PanelBgLight);
            var rt = list.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, up ? 0f : 1f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = up ? new Vector2(bottomLeft.x, topRight.y + 2f) : new Vector2(bottomLeft.x, bottomLeft.y - 2f);

            var viewport = UIFactory.Rect("Viewport", list.transform).Stretch(4, 4, 4, 4);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var content = UIFactory.Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            UIFactory.VerticalLayout(content.gameObject, 2, new RectOffset(0, 0, 0, 0));
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                var item = UIFactory.Button(content, options[i], () =>
                {
                    Close();
                    onSelect(index);
                }, 15);
                item.Layout(ItemHeight);
                var t = item.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.MiddleLeft;
                t.rectTransform.Stretch(10, 8, 2, 2);
                item.SetSelected(i == selected);
            }
            // Que la opción elegida quede a la vista en listas largas.
            Canvas.ForceUpdateCanvases();
            float contentHeight = options.Length * (ItemHeight + 2f);
            float viewHeight = height - 8f;
            if (contentHeight > viewHeight)
                content.anchoredPosition = new Vector2(0f, Mathf.Clamp(selected * (ItemHeight + 2f) - viewHeight * 0.4f, 0f, contentHeight - viewHeight));
        }
    }
}
