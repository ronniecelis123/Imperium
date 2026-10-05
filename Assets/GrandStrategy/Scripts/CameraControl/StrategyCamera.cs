using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GrandStrategy.CameraControl
{
    /// <summary>
    /// Cámara de juego de estrategia:
    /// <list type="bullet">
    /// <item>Rueda del ratón: zoom suave hacia el cursor (y alejar).</item>
    /// <item>WASD / flechas: desplazamiento (Shift = rápido). Botón central o derecho: arrastrar el mapa.</item>
    /// <item>Inclinación automática: casi cenital al alejar, más inclinada al acercar (como HOI4).</item>
    /// </list>
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class StrategyCamera : MonoBehaviour
    {
        [Header("Zoom")]
        public float MinHeight = 1.2f;
        public float MaxHeight = 150f;
        [Tooltip("Incremento de zoom normalizado por cada paso de la rueda.")]
        public float ZoomStep = 0.065f;
        public float ZoomSmoothing = 12f;

        [Header("Inclinación (grados)")]
        public float NearPitch = 48f;
        public float FarPitch = 84f;

        [Header("Desplazamiento")]
        public float KeyboardPanSpeed = 1.1f;
        public float FocusSmoothing = 6f;
        [Tooltip("Desplazar el mapa al llevar el ratón al borde de la pantalla (como en HOI4).")]
        public bool EdgeScrolling = true;
        public float EdgeSize = 14f;

        /// <summary>El mundo es continuo de este a oeste (como HOI4).</summary>
        public bool WrapX = true;

        public bool InputEnabled = true;
        /// <summary>Desplazamiento lento automático (fondo del menú principal).</summary>
        public bool AutoDrift;

        /// <summary>Devuelve la altura del terreno en (x, z) del mundo. La cámara se mantiene por encima.</summary>
        public Func<float, float, float> TerrainHeightProvider;

        public float Zoom01 => _zoom;
        /// <summary>Altura de la cámara sobre el suelo.</summary>
        public float Height => Mathf.Lerp(MinHeight, MaxHeight, _zoom * _zoom);
        public Camera Camera { get; private set; }
        public event Action<float> ZoomChanged;

        private Vector2 _focus;
        private float _zoom = 0.6f, _targetZoom = 0.6f;
        private Rect _bounds = new Rect(0, 0, 200, 100);
        private bool _hasFocusTarget;
        private Vector2 _focusTarget;
        private bool _dragging;
        private Vector3 _dragAnchor;
        private float _driftDir = 1f;
        private float _groundHeight;

        private void Awake()
        {
            Camera = GetComponent<Camera>();
        }

        public void Setup(Rect worldBounds, float initialZoom)
        {
            _bounds = worldBounds;
            _focus = worldBounds.center;
            _zoom = _targetZoom = Mathf.Clamp01(initialZoom);
            ApplyTransform();
            ZoomChanged?.Invoke(_zoom);
        }

        /// <summary>Centra la cámara suavemente en un punto del mundo (y opcionalmente cambia el zoom).</summary>
        public void FocusOn(Vector3 worldPoint, float? zoom01 = null)
        {
            float dx = worldPoint.x - _focus.x;
            if (WrapX) dx -= Mathf.Round(dx / _bounds.width) * _bounds.width; // camino más corto
            _focusTarget = new Vector2(_focus.x + dx, worldPoint.z);
            _hasFocusTarget = true;
            if (zoom01.HasValue) _targetZoom = Mathf.Clamp01(zoom01.Value);
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            var mouse = Mouse.current;
            Vector2 mousePos = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool mouseInScreen = mouse != null && mousePos.x >= 0 && mousePos.y >= 0 &&
                                 mousePos.x <= Screen.width && mousePos.y <= Screen.height;

            if (InputEnabled)
                HandleInput(dt, mouse, mousePos, overUI, mouseInScreen);
            else if (AutoDrift)
                Drift(dt);

            // Zoom suave anclado al cursor: el punto bajo el ratón permanece bajo el ratón.
            if (Mathf.Abs(_targetZoom - _zoom) > 1e-4f)
            {
                bool anchor = InputEnabled && mouseInScreen && !_hasFocusTarget && !_dragging;
                bool hasBefore = GroundPoint(mousePos, out var before) && anchor;
                _zoom = Mathf.Lerp(_zoom, _targetZoom, 1f - Mathf.Exp(-ZoomSmoothing * dt));
                if (Mathf.Abs(_targetZoom - _zoom) <= 1e-4f) _zoom = _targetZoom;
                ApplyTransform();
                if (hasBefore && GroundPoint(mousePos, out var after))
                    _focus += new Vector2(before.x - after.x, before.z - after.z);
                ZoomChanged?.Invoke(_zoom);
            }

            if (_hasFocusTarget)
            {
                _focus = Vector2.Lerp(_focus, _focusTarget, 1f - Mathf.Exp(-FocusSmoothing * dt));
                if ((_focus - _focusTarget).sqrMagnitude < 0.0004f) _hasFocusTarget = false;
            }

            ClampFocus();
            WrapFocus();
            if (TerrainHeightProvider != null)
            {
                // Seguir el relieve suavemente para no atravesar montañas al acercarse.
                float ground = Mathf.Max(0f, TerrainHeightProvider(_focus.x, _focus.y));
                _groundHeight = Mathf.Lerp(_groundHeight, ground, 1f - Mathf.Exp(-5f * dt));
            }
            ApplyTransform();
        }

        private void HandleInput(float dt, Mouse mouse, Vector2 mousePos, bool overUI, bool mouseInScreen)
        {
            // --- Rueda ---
            if (mouse != null && !overUI && mouseInScreen)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    _targetZoom = Mathf.Clamp01(_targetZoom - Mathf.Sign(scroll) * ZoomStep);
                    _hasFocusTarget = false;
                }
            }

            // --- Arrastre con botón central o derecho ---
            if (mouse != null)
            {
                bool dragHeld = mouse.middleButton.isPressed || mouse.rightButton.isPressed;
                bool dragStart = mouse.middleButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame;
                if (dragStart && !overUI && GroundPoint(mousePos, out _dragAnchor))
                {
                    _dragging = true;
                    _hasFocusTarget = false;
                }
                if (_dragging)
                {
                    if (!dragHeld) _dragging = false;
                    else if (GroundPoint(mousePos, out var current))
                    {
                        _focus += new Vector2(_dragAnchor.x - current.x, _dragAnchor.z - current.z);
                        ApplyTransform();
                    }
                }
            }

            // --- Teclado ---
            Vector2 dir = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir.x -= 1;
                if (kb.pageUpKey.wasPressedThisFrame) _targetZoom = Mathf.Clamp01(_targetZoom - ZoomStep);
                if (kb.pageDownKey.wasPressedThisFrame) _targetZoom = Mathf.Clamp01(_targetZoom + ZoomStep);
            }

            // --- Bordes de pantalla (opcional) ---
            // Funciona también sobre la interfaz (la barra superior ocupa el borde de arriba).
            Vector2 edge = Vector2.zero;
            if (EdgeScrolling && mouseInScreen && !_dragging && Application.isFocused)
            {
                // Velocidad progresiva: más rápido cuanto más pegado al borde.
                if (mousePos.x < EdgeSize) edge.x -= 1f - mousePos.x / EdgeSize * 0.6f;
                if (mousePos.x > Screen.width - EdgeSize) edge.x += 1f - (Screen.width - mousePos.x) / EdgeSize * 0.6f;
                if (mousePos.y < EdgeSize) edge.y -= 1f - mousePos.y / EdgeSize * 0.6f;
                if (mousePos.y > Screen.height - EdgeSize) edge.y += 1f - (Screen.height - mousePos.y) / EdgeSize * 0.6f;
            }
            if (edge != Vector2.zero)
            {
                _focus += Vector2.ClampMagnitude(edge, 1f) * Height * KeyboardPanSpeed * dt;
                _hasFocusTarget = false;
            }

            if (dir != Vector2.zero)
            {
                float speed = Height * KeyboardPanSpeed * (kb != null && kb.leftShiftKey.isPressed ? 2.5f : 1f);
                _focus += dir.normalized * speed * dt;
                _hasFocusTarget = false;
            }
        }

        private void Drift(float dt)
        {
            if (WrapX)
            {
                _focus.x += Height * 0.02f * dt; // el mundo gira sin fin
                return;
            }
            _focus.x += _driftDir * Height * 0.025f * dt;
            if (_focus.x > _bounds.xMax - _bounds.width * 0.2f) _driftDir = -1f;
            if (_focus.x < _bounds.xMin + _bounds.width * 0.2f) _driftDir = 1f;
        }

        /// <summary>Mantiene el foco dentro del mapa central desplazando todo lo relacionado una vuelta entera.</summary>
        private void WrapFocus()
        {
            if (!WrapX) return;
            float w = _bounds.width;
            float shift = _focus.x >= _bounds.xMax ? -w : _focus.x < _bounds.xMin ? w : 0f;
            if (shift == 0f) return;
            _focus.x += shift;
            _dragAnchor.x += shift;
            if (_hasFocusTarget) _focusTarget.x += shift;
        }

        private void ClampFocus()
        {
            if (!WrapX)
            {
                float marginX = _bounds.width * 0.02f;
                _focus.x = Mathf.Clamp(_focus.x, _bounds.xMin - marginX, _bounds.xMax + marginX);
            }
            _focus.y = Mathf.Clamp(_focus.y, _bounds.yMin, _bounds.yMax);
        }

        private void ApplyTransform()
        {
            float pitch = Mathf.Lerp(NearPitch, FarPitch, Mathf.SmoothStep(0f, 1f, _zoom));
            float h = Height;
            float back = h / Mathf.Tan(pitch * Mathf.Deg2Rad);
            transform.SetPositionAndRotation(new Vector3(_focus.x, _groundHeight + h, _focus.y - back), Quaternion.Euler(pitch, 0f, 0f));
        }

        /// <summary>Intersección del rayo del cursor con el plano del mar (suficiente para zoom/arrastre).</summary>
        private bool GroundPoint(Vector2 screenPos, out Vector3 point)
        {
            point = default;
            if (Camera == null) return false;
            var ray = Camera.ScreenPointToRay(screenPos);
            if (ray.direction.y >= -1e-4f) return false;
            float t = (_groundHeight - ray.origin.y) / ray.direction.y;
            point = ray.GetPoint(t);
            return true;
        }
    }
}
