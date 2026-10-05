using UnityEngine;
using UnityEngine.UI;

namespace GrandStrategy.UI
{
    public sealed class LoadingScreen : MonoBehaviour
    {
        private Text _status;
        private RectTransform _fill;

        public static LoadingScreen Create(Transform parent)
        {
            var bg = UIFactory.Panel(parent, "LoadingScreen", new Color(0.035f, 0.04f, 0.045f, 1f), false);
            bg.rectTransform.Stretch();
            var screen = bg.gameObject.AddComponent<LoadingScreen>();

            var title = UIFactory.Label(bg.transform, GameInfo.Title, 72, UITheme.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(1200, 100));

            screen._status = UIFactory.Label(bg.transform, "Cargando...", 22, UITheme.TextDim, TextAnchor.MiddleCenter);
            screen._status.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1000, 40));

            var bar = UIFactory.Panel(bg.transform, "ProgressBar", new Color(0.1f, 0.1f, 0.1f, 1f));
            bar.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(640, 14));
            var fill = UIFactory.Panel(bar.transform, "Fill", UITheme.Accent, false);
            screen._fill = fill.rectTransform;
            screen._fill.anchorMin = Vector2.zero;
            screen._fill.anchorMax = new Vector2(0, 1);
            screen._fill.offsetMin = screen._fill.offsetMax = Vector2.zero;
            return screen;
        }

        public void SetProgress(float value, string status)
        {
            _fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1);
            if (!string.IsNullOrEmpty(status)) _status.text = status;
        }

        public void SetError(string message)
        {
            _status.text = "<color=#e06050>Error:</color> " + message;
        }
    }
}
