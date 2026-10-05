using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GrandStrategy.UI
{
    /// <summary>
    /// Avisos breves (obra terminada, mejora completada...) que aparecen bajo la barra superior, a la derecha, y
    /// desaparecen solos. No reciben clics, así que nunca bloquean el mapa ni hay que cerrarlos.
    /// </summary>
    public sealed class Notifications : MonoBehaviour
    {
        private const float Lifetime = 4.5f;
        private const float FadeTime = 0.6f;
        private const int MaxVisible = 5;

        private static Notifications _instance;

        private sealed class Toast
        {
            public CanvasGroup Group;
            public float Age;
        }

        private readonly List<Toast> _toasts = new List<Toast>();
        private RectTransform _stack;

        public static Notifications Create(Transform parent)
        {
            var root = UIFactory.Rect("Notifications", parent);
            root.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -84), new Vector2(420, 400));
            var v = UIFactory.VerticalLayout(root.gameObject, 6, new RectOffset(0, 0, 0, 0));
            v.childForceExpandHeight = false;
            v.childAlignment = TextAnchor.UpperRight;
            _instance = root.gameObject.AddComponent<Notifications>();
            _instance._stack = root;
            return _instance;
        }

        /// <summary>Muestra un aviso que se cierra solo.</summary>
        public static void Show(string text, string icon = null, bool error = false)
        {
            if (_instance != null) _instance.Add(text, icon, error);
        }

        private void Add(string text, string icon, bool error)
        {
            while (_toasts.Count >= MaxVisible) Remove(_toasts[0]);
            var bg = UIFactory.Panel(_stack, "Toast", new Color(0.06f, 0.065f, 0.06f, 0.92f));
            bg.raycastTarget = false;
            var size = bg.Layout(44);
            size.preferredHeight = -1; // mínimo 44; más si el texto ocupa dos líneas
            size.flexibleHeight = 0;
            var h = UIFactory.HorizontalLayout(bg.gameObject, 10, new RectOffset(10, 12, 6, 6));
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            h.childAlignment = TextAnchor.MiddleLeft;
            if (icon != null)
            {
                var img = UIFactory.Icon(bg.transform, icon, error ? UITheme.Negative : UITheme.Accent);
                img.raycastTarget = false;
                img.Layout(28, 28);
            }
            var label = UIFactory.Label(bg.transform, text, 16, error ? UITheme.Negative : UITheme.Text, TextAnchor.MiddleLeft);
            label.raycastTarget = false;
            label.Layout(-1, 0, 1);
            var group = bg.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            _toasts.Add(new Toast { Group = group });
        }

        private void Update()
        {
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var t = _toasts[i];
                t.Age += Time.unscaledDeltaTime;
                t.Group.alpha = Mathf.Clamp01((Lifetime - t.Age) / FadeTime);
                if (t.Age >= Lifetime) Remove(t);
            }
        }

        private void Remove(Toast t)
        {
            _toasts.Remove(t);
            if (t.Group != null) Destroy(t.Group.gameObject);
        }

        private void OnDisable()
        {
            foreach (var t in _toasts) if (t.Group != null) Destroy(t.Group.gameObject);
            _toasts.Clear();
        }
    }
}
