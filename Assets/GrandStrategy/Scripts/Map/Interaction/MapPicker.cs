using GrandStrategy.Map.Rendering;
using UnityEngine;

namespace GrandStrategy.Map.Interaction
{
    /// <summary>
    /// Convierte un rayo de cámara en un punto del mapa intersectando el campo de alturas en CPU
    /// (sin MeshColliders: más rápido y siempre coherente con el relieve del shader).
    /// </summary>
    public sealed class MapPicker
    {
        private readonly MapRenderer _map;

        public MapPicker(MapRenderer map)
        {
            _map = map;
        }

        public bool TryPick(Ray ray, out Vector2 uv, out Vector3 worldPoint)
        {
            uv = default;
            worldPoint = default;
            if (_map == null || _map.World == null || ray.direction.y >= -1e-4f) return false;

            float top = _map.HeightScale + 0.01f;
            // Tramo del rayo entre el plano superior del relieve y el plano del mar.
            float tTop = Mathf.Max(0f, (top - ray.origin.y) / ray.direction.y);
            float tBottom = (0f - ray.origin.y) / ray.direction.y;
            if (tBottom < 0f) return false;

            const int steps = 48;
            float prevT = tTop;
            float hitT = tBottom;
            for (int s = 1; s <= steps; s++)
            {
                float t = Mathf.Lerp(tTop, tBottom, s / (float)steps);
                if (Above(ray, t)) { prevT = t; continue; }
                // Refinar con búsqueda binaria entre prevT (encima) y t (debajo).
                float lo = prevT, hi = t;
                for (int k = 0; k < 10; k++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (Above(ray, mid)) lo = mid; else hi = mid;
                }
                hitT = hi;
                break;
            }

            worldPoint = ray.GetPoint(hitT);
            uv = _map.WorldToUV(worldPoint);
            uv.x -= Mathf.Floor(uv.x); // las copias del mapa a los lados también se pueden pulsar
            return uv.y >= 0f && uv.y <= 1f;
        }

        public Province PickProvince(Ray ray)
        {
            if (!TryPick(ray, out var uv, out _)) return null;
            var map = _map.World.Map;
            int x = Mathf.Clamp((int)(uv.x * map.Width), 0, map.Width - 1);
            int y = Mathf.Clamp((int)(uv.y * map.Height), 0, map.Height - 1);
            return _map.World.GetProvinceAtPixel(x, y);
        }

        private bool Above(Ray ray, float t)
        {
            var p = ray.GetPoint(t);
            var uv = _map.WorldToUV(p);
            return p.y > _map.GetTerrainHeight(uv.x, uv.y);
        }
    }
}
