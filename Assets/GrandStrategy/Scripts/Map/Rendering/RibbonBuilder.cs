using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GrandStrategy.Map.Rendering
{
    /// <summary>
    /// Construye mallas de "cintas" sobre el mapa (ríos, caminos, ferrocarriles, calles) a partir de polilíneas
    /// en coordenadas de mundo XZ. El shader las apoya en el relieve y las ensancha al alejar la cámara.
    /// UV0 = posición en el mapa, UV1 = (lado -1..1, distancia a lo largo), UV2.x = semiancho, NORMAL = perpendicular.
    /// </summary>
    public sealed class RibbonBuilder
    {
        private readonly List<Vector3> _verts = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Vector2> _uv0 = new List<Vector2>();
        private readonly List<Vector2> _uv1 = new List<Vector2>();
        private readonly List<Vector2> _uv2 = new List<Vector2>();
        private readonly List<int> _tris = new List<int>();
        private readonly Vector2 _worldSize;
        private readonly List<Vector2> _tmp = new List<Vector2>();

        public int Count { get; private set; }

        public RibbonBuilder(Vector2 worldSize)
        {
            _worldSize = worldSize;
        }

        /// <summary>
        /// Añade una cinta. smooth = curva Catmull-Rom entre los puntos; spacing = separación máxima entre vértices;
        /// taperPoints = vértices sobre los que se afinan los extremos.
        /// </summary>
        public void Add(IList<Vector2> points, float halfWidth, bool smooth, float spacing = 0.03f, int taperPoints = 4)
        {
            if (points.Count < 2) return;
            _tmp.Clear();
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 p1 = points[i], p2 = points[i + 1];
                int sub = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(p1, p2) / spacing), 1, 64);
                for (int s = 0; s < sub; s++)
                {
                    float t = s / (float)sub;
                    if (smooth)
                    {
                        Vector2 p0 = points[Mathf.Max(i - 1, 0)], p3 = points[Mathf.Min(i + 2, points.Count - 1)];
                        _tmp.Add(CatmullRom(p0, p1, p2, p3, t));
                    }
                    else _tmp.Add(Vector2.Lerp(p1, p2, t));
                }
            }
            _tmp.Add(points[points.Count - 1]);

            float along = 0f;
            int start = _verts.Count;
            for (int i = 0; i < _tmp.Count; i++)
            {
                if (i > 0) along += Vector2.Distance(_tmp[i - 1], _tmp[i]);
                var tangent = (_tmp[Mathf.Min(i + 1, _tmp.Count - 1)] - _tmp[Mathf.Max(i - 1, 0)]).normalized;
                var perp = new Vector3(-tangent.y, 0, tangent.x);
                float taper = taperPoints > 0 ? Mathf.Clamp01(Mathf.Min(i, _tmp.Count - 1 - i) / (float)taperPoints) * 0.7f + 0.3f : 1f;
                var mapUV = new Vector2(Mathf.Repeat(_tmp[i].x / _worldSize.x, 1f), _tmp[i].y / _worldSize.y);
                for (int side = -1; side <= 1; side += 2)
                {
                    _verts.Add(new Vector3(_tmp[i].x, 0, _tmp[i].y));
                    _normals.Add(perp);
                    _uv0.Add(mapUV);
                    _uv1.Add(new Vector2(side, along));
                    _uv2.Add(new Vector2(halfWidth * taper, 0));
                }
                if (i > 0)
                {
                    int a = start + (i - 1) * 2;
                    _tris.Add(a); _tris.Add(a + 2); _tris.Add(a + 1);
                    _tris.Add(a + 1); _tris.Add(a + 2); _tris.Add(a + 3);
                }
            }
            Count++;
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = _verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uv0);
            mesh.SetUVs(1, _uv1);
            mesh.SetUVs(2, _uv2);
            mesh.SetTriangles(_tris, 0);
            mesh.bounds = new Bounds(new Vector3(_worldSize.x * 0.5f, 1f, _worldSize.y * 0.5f), new Vector3(_worldSize.x, 4f, _worldSize.y));
            return mesh;
        }

        public static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
