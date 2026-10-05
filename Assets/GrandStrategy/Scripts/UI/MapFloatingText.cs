using System.Collections.Generic;
using GrandStrategy.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GrandStrategy.UI
{
    /// <summary>
    /// Mensajes breves que aparecen sobre un punto del mapa, suben un poco y se desvanecen (≈1.4 s): p. ej.
    /// «Taller encargado · −$50 M» sobre la provincia donde se ha hecho clic, o el motivo por el que no se puede
    /// construir. Siguen al mapa al mover la cámara (y respetan la repetición horizontal del mundo). No reciben clics.
    /// </summary>
    public sealed class MapFloatingText : MonoBehaviour
    {
        private const float Lifetime = 1.4f;
        private const float FadeTime = 0.45f;
        private const float Rise = 26f;
        private const int MaxItems = 6;

        private static MapFloatingText _instance;

        private sealed class Item
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Vector3 World;
            public bool HasWorld;
            public Vector2 Screen;
            public float Age;
        }

        private GameManager _game;
        private RectTransform _rect;
        private readonly List<Item> _items = new List<Item>();

        public static MapFloatingText Create(Transform parent, GameManager game)
        {
            var rt = UIFactory.Rect("MapFloatingText", parent).Stretch();
            rt.SetAsFirstSibling(); // sobre el mapa, por debajo de barras y ventanas
            _instance = rt.gameObject.AddComponent<MapFloatingText>();
            _instance._game = game;
            _instance._rect = rt;
            return _instance;
        }

        /// <summary>Mensaje anclado a un punto del mapa (en coordenadas de mundo).</summary>
        public static void ShowAt(Vector3 world, string icon, string title, string detail, Color titleColor, Color detailColor)
        {
            if (_instance != null) _instance.Add(true, world, Vector2.zero, icon, title, detail, titleColor, detailColor);
        }

        /// <summary>Mensaje anclado a un punto de la pantalla (cuando no hay punto del mapa, p. ej. fuera del mundo).</summary>
        public static void ShowAtScreen(Vector2 screen, string icon, string title, string detail, Color titleColor, Color detailColor)
        {
            if (_instance != null) _instance.Add(false, Vector3.zero, screen, icon, title, detail, titleColor, detailColor);
        }

        private void Add(bool hasWorld, Vector3 world, Vector2 screen, string icon, string title, string detail, Color titleColor, Color detailColor)
        {
            while (_items.Count >= MaxItems) Remove(_items[0]);
            var root = UIFactory.Rect("Mensaje", _rect);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(320f, 70f);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            float y = 14f;
            if (icon != null)
            {
                var img = UIFactory.Icon(root, icon, titleColor);
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                img.rectTransform.sizeDelta = new Vector2(22f, 22f);
                img.rectTransform.anchoredPosition = new Vector2(0f, 26f);
                img.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            }
            var t = UIFactory.Label(root, title, 16, titleColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            t.rectTransform.sizeDelta = new Vector2(320f, 20f);
            t.rectTransform.anchoredPosition = new Vector2(0f, y - 6f);
            Outline(t);
            if (!string.IsNullOrEmpty(detail))
            {
                var d = UIFactory.Label(root, detail, 15, detailColor, TextAnchor.MiddleCenter, FontStyle.Bold);
                d.horizontalOverflow = HorizontalWrapMode.Overflow;
                d.rectTransform.anchorMin = d.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                d.rectTransform.sizeDelta = new Vector2(320f, 20f);
                d.rectTransform.anchoredPosition = new Vector2(0f, y - 25f);
                Outline(d);
            }
            var item = new Item { Root = root, Group = group, World = world, HasWorld = hasWorld, Screen = screen };
            _items.Add(item);
            Place(item);
        }

        private static void Outline(Text t)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(1f, -1f);
        }

        private void LateUpdate()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var item = _items[i];
                item.Age += Time.unscaledDeltaTime;
                if (item.Age >= Lifetime)
                {
                    Remove(item);
                    continue;
                }
                Place(item);
            }
        }

        /// <summary>Posición en pantalla (copia del mundo más cercana a la cámara), subida, escala y transparencia.</summary>
        private void Place(Item item)
        {
            var rig = _game.CameraRig;
            Vector2 canvasSize = _rect.rect.size;
            float scale = canvasSize.y / Mathf.Max(1f, UnityEngine.Screen.height);
            Vector2 screen = item.Screen;
            bool visible = true;
            if (item.HasWorld && rig != null)
            {
                var world = item.World;
                float width = _game.Map.WorldSize.x;
                world.x += Mathf.Round((rig.Camera.transform.position.x - world.x) / width) * width;
                Vector3 s = rig.Camera.WorldToScreenPoint(world);
                visible = s.z > 0f;
                screen = new Vector2(s.x, s.y);
            }
            float t = item.Age / Lifetime;
            item.Root.anchoredPosition = screen * scale - canvasSize * 0.5f + new Vector2(0f, 18f + Rise * Mathf.Sqrt(t));
            float pop = Mathf.Clamp01(item.Age / 0.12f);
            item.Root.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, pop);
            item.Group.alpha = visible ? Mathf.Clamp01((Lifetime - item.Age) / FadeTime) : 0f;
        }

        private void Remove(Item item)
        {
            _items.Remove(item);
            if (item.Root != null) Destroy(item.Root.gameObject);
        }

        private void OnDisable()
        {
            foreach (var item in _items) if (item.Root != null) Destroy(item.Root.gameObject);
            _items.Clear();
        }
    }
}
