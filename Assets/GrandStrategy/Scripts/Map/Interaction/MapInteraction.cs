using System;
using GrandStrategy.Map.Rendering;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GrandStrategy.Map.Interaction
{
    /// <summary>
    /// Detecta qué provincia hay bajo el ratón y los clics sobre el mapa.
    /// No decide qué significa un clic: solo emite eventos que interpreta el GameManager.
    /// </summary>
    public sealed class MapInteraction : MonoBehaviour
    {
        private const float ClickDragTolerance = 6f;

        public event Action<Province> ProvinceHovered;
        /// <summary>Clic izquierdo sobre el mapa (provincia null = fuera del mapa).</summary>
        public event Action<Province> ProvinceClicked;
        /// <summary>Clic derecho sin arrastrar (útil en el futuro para órdenes de movimiento).</summary>
        public event Action<Province> ProvinceRightClicked;

        public Province HoveredProvince { get; private set; }
        public bool InteractionEnabled { get; set; } = true;

        private Camera _camera;
        private MapRenderer _map;
        private MapPicker _picker;
        private Vector2 _leftPressPos, _rightPressPos;
        private bool _leftPressed, _rightPressed;

        public void Initialize(Camera cam, MapRenderer map)
        {
            _camera = cam;
            _map = map;
            _picker = new MapPicker(map);
        }

        public bool TryGetMapPoint(Vector2 screenPos, out Vector3 world)
        {
            world = default;
            return _picker != null && _picker.TryPick(_camera.ScreenPointToRay(screenPos), out _, out world);
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || _picker == null || _camera == null) return;

            Vector2 pos = mouse.position.ReadValue();
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool active = InteractionEnabled && !overUI && IsInsideScreen(pos);

            var hovered = active ? _picker.PickProvince(_camera.ScreenPointToRay(pos)) : null;
            if (hovered != HoveredProvince)
            {
                HoveredProvince = hovered;
                ProvinceHovered?.Invoke(hovered);
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                _leftPressed = active;
                _leftPressPos = pos;
            }
            if (mouse.leftButton.wasReleasedThisFrame && _leftPressed)
            {
                _leftPressed = false;
                if (active && (pos - _leftPressPos).sqrMagnitude < ClickDragTolerance * ClickDragTolerance)
                    ProvinceClicked?.Invoke(hovered);
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                _rightPressed = active;
                _rightPressPos = pos;
            }
            if (mouse.rightButton.wasReleasedThisFrame && _rightPressed)
            {
                _rightPressed = false;
                if (active && (pos - _rightPressPos).sqrMagnitude < ClickDragTolerance * ClickDragTolerance)
                    ProvinceRightClicked?.Invoke(hovered);
            }
        }

        private static bool IsInsideScreen(Vector2 p) =>
            p.x >= 0 && p.y >= 0 && p.x <= Screen.width && p.y <= Screen.height;
    }
}
