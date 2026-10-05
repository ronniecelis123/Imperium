using GrandStrategy.Core;
using UnityEngine;

namespace GrandStrategy.UI
{
    /// <summary>Menú principal sobre el mapa en movimiento.</summary>
    public sealed class MainMenuUI : MonoBehaviour
    {
        public static MainMenuUI Create(Transform parent, GameManager game, System.Action openSettings)
        {
            var root = UIFactory.Rect("MainMenu", parent).Stretch();
            var menu = root.gameObject.AddComponent<MainMenuUI>();

            var side = UIFactory.Panel(root, "SidePanel", new Color(0.04f, 0.045f, 0.05f, 0.9f));
            side.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(90, 0), new Vector2(480, 0));

            var title = UIFactory.Label(side.transform, GameInfo.Title, 76, UITheme.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(0, 100));
            var subtitle = UIFactory.Label(side.transform, GameInfo.Subtitle.ToUpperInvariant(), 24, UITheme.TextDim, TextAnchor.MiddleCenter);
            subtitle.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -245), new Vector2(0, 36));

            var buttons = UIFactory.Rect("Buttons", side.transform);
            buttons.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(340, 300));
            UIFactory.VerticalLayout(buttons.gameObject, 14, new RectOffset(0, 0, 0, 0));

            UIFactory.Button(buttons, "Nueva partida", game.StartCountrySelection, 26).Layout(58);
            var load = UIFactory.Button(buttons, "Cargar partida", null, 26);
            load.Layout(58);
            load.SetInteractable(false);
            UIFactory.Button(buttons, "Ajustes", openSettings, 26).Layout(58);
            UIFactory.Button(buttons, "Salir", game.QuitGame, 26).Layout(58);

            var version = UIFactory.Label(side.transform, GameInfo.Version, 16, UITheme.TextDim, TextAnchor.MiddleCenter);
            version.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(0, 30));
            return menu;
        }
    }
}
