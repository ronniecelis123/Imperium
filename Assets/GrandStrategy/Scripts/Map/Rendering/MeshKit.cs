using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GrandStrategy.Map.Rendering
{
    /// <summary>
    /// Pequeño kit para construir mallas a partir de primitivas (cajas, prismas, conos, cúpulas, tornos,
    /// esferas, tejados...). Color por vértice: el alfa = 1 marca fachadas con ventanas (se iluminan de noche).
    /// Las caras planas se orientan solas hacia fuera de la primitiva que las crea.
    /// </summary>
    public sealed class MeshKit
    {
        private readonly List<Vector3> _verts = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Color32> _colors = new List<Color32>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _tris = new List<int>();
        private Vector3 _center;
        /// <summary>UV de la geometría sin textura (los shaders la distinguen por UV negativa).</summary>
        private static readonly Vector2 NoUV = new Vector2(-1f, -1f);

        public static Color32 Wall(Color c) => new Color(c.r, c.g, c.b, 1f);
        public static Color32 Solid(Color c) => new Color(c.r, c.g, c.b, 0f);

        public int VertexCount => _verts.Count;

        // ------------------------------------------------------------------ Básicos

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color32 col)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            n.Normalize();
            if (Vector3.Dot(n, (a + b + c) / 3f - _center) < 0f)
            {
                (b, c) = (c, b);
                n = -n;
            }
            int i = _verts.Count;
            _verts.Add(a); _verts.Add(b); _verts.Add(c);
            _normals.Add(n); _normals.Add(n); _normals.Add(n);
            _colors.Add(col); _colors.Add(col); _colors.Add(col);
            _uvs.Add(NoUV); _uvs.Add(NoUV); _uvs.Add(NoUV);
            _tris.Add(i); _tris.Add(i + 1); _tris.Add(i + 2);
        }

        /// <summary>
        /// Tarjeta (quad) texturizada con normal propia, para hojas/hierba: se dibuja por ambas caras y la
        /// normal "esférica" (desde el centro de la copa) da un sombreado suave de volumen.
        /// uvRect = región del atlas (x, y, ancho, alto). Las esquinas: c - r - u, c - r + u, c + r + u, c + r - u.
        /// </summary>
        public void Card(Vector3 center, Vector3 right, Vector3 up, Rect uvRect, Color32 bottomColor, Color32 topColor, Vector3 normal)
        {
            int i = _verts.Count;
            var n = normal.normalized;
            _verts.Add(center - right - up); _uvs.Add(new Vector2(uvRect.xMin, uvRect.yMin)); _colors.Add(bottomColor);
            _verts.Add(center - right + up); _uvs.Add(new Vector2(uvRect.xMin, uvRect.yMax)); _colors.Add(topColor);
            _verts.Add(center + right + up); _uvs.Add(new Vector2(uvRect.xMax, uvRect.yMax)); _colors.Add(topColor);
            _verts.Add(center + right - up); _uvs.Add(new Vector2(uvRect.xMax, uvRect.yMin)); _colors.Add(bottomColor);
            for (int k = 0; k < 4; k++) _normals.Add(n);
            _tris.Add(i); _tris.Add(i + 1); _tris.Add(i + 2);
            _tris.Add(i); _tris.Add(i + 2); _tris.Add(i + 3);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 col)
        {
            Tri(a, b, c, col);
            Tri(a, c, d, col);
        }

        /// <summary>Caja con base en <paramref name="baseCenter"/>. top = color del techo plano.</summary>
        public void Box(Vector3 baseCenter, Vector3 size, Color32 walls, Color32 top, bool bottom = false)
        {
            _center = baseCenter + Vector3.up * size.y * 0.5f;
            float x0 = baseCenter.x - size.x * 0.5f, x1 = baseCenter.x + size.x * 0.5f;
            float z0 = baseCenter.z - size.z * 0.5f, z1 = baseCenter.z + size.z * 0.5f;
            float y0 = baseCenter.y, y1 = baseCenter.y + size.y;
            Quad(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0), walls);
            Quad(new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), walls);
            Quad(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), walls);
            Quad(new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), walls);
            Quad(new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), top);
            if (bottom) Quad(new Vector3(x0, y0, z0), new Vector3(x0, y0, z1), new Vector3(x1, y0, z1), new Vector3(x1, y0, z0), top);
        }

        /// <summary>Tejado a dos aguas sobre un rectángulo (cumbrera paralela al eje mayor), con alero.</summary>
        public void GableRoof(Vector3 wallTopCenter, float w, float d, float ridge, float overhang, Color32 roof, Color32 gable)
        {
            bool alongX = w >= d;
            float hw = w * 0.5f + overhang, hd = d * 0.5f + overhang;
            var c = wallTopCenter;
            _center = c - Vector3.up * 0.01f;
            if (alongX)
            {
                Vector3 r0 = c + new Vector3(-hw, ridge, 0), r1 = c + new Vector3(hw, ridge, 0);
                Quad(c + new Vector3(-hw, 0, -hd), r0, r1, c + new Vector3(hw, 0, -hd), roof);
                Quad(c + new Vector3(-hw, 0, hd), r0, r1, c + new Vector3(hw, 0, hd), roof);
                Tri(c + new Vector3(-w * 0.5f, 0, -d * 0.5f), c + new Vector3(-w * 0.5f, ridge, 0), c + new Vector3(-w * 0.5f, 0, d * 0.5f), gable);
                Tri(c + new Vector3(w * 0.5f, 0, -d * 0.5f), c + new Vector3(w * 0.5f, ridge, 0), c + new Vector3(w * 0.5f, 0, d * 0.5f), gable);
            }
            else
            {
                Vector3 r0 = c + new Vector3(0, ridge, -hd), r1 = c + new Vector3(0, ridge, hd);
                Quad(c + new Vector3(-hw, 0, -hd), r0, r1, c + new Vector3(-hw, 0, hd), roof);
                Quad(c + new Vector3(hw, 0, -hd), r0, r1, c + new Vector3(hw, 0, hd), roof);
                Tri(c + new Vector3(-w * 0.5f, 0, -d * 0.5f), c + new Vector3(0, ridge, -d * 0.5f), c + new Vector3(w * 0.5f, 0, -d * 0.5f), gable);
                Tri(c + new Vector3(-w * 0.5f, 0, d * 0.5f), c + new Vector3(0, ridge, d * 0.5f), c + new Vector3(w * 0.5f, 0, d * 0.5f), gable);
            }
        }

        /// <summary>Tejado a cuatro aguas (pirámide truncada).</summary>
        public void HipRoof(Vector3 wallTopCenter, float w, float d, float height, float overhang, Color32 roof)
        {
            _center = wallTopCenter - Vector3.up * 0.01f;
            float hw = w * 0.5f + overhang, hd = d * 0.5f + overhang;
            float rw = Mathf.Max(0f, hw - hd);
            var c = wallTopCenter;
            Vector3 a = c + new Vector3(-hw, 0, -hd), b = c + new Vector3(hw, 0, -hd), e = c + new Vector3(hw, 0, hd), f = c + new Vector3(-hw, 0, hd);
            Vector3 r0 = c + new Vector3(-rw, height, 0), r1 = c + new Vector3(rw, height, 0);
            Quad(a, r0, r1, b, roof);
            Quad(f, r0, r1, e, roof);
            Tri(a, r0, f, roof);
            Tri(b, r1, e, roof);
        }

        public void Pyramid(Vector3 baseCenter, float w, float d, float height, Color32 col)
        {
            _center = baseCenter + Vector3.up * height * 0.25f;
            Vector3 apex = baseCenter + Vector3.up * height;
            Vector3 a = baseCenter + new Vector3(-w / 2, 0, -d / 2), b = baseCenter + new Vector3(w / 2, 0, -d / 2);
            Vector3 c = baseCenter + new Vector3(w / 2, 0, d / 2), e = baseCenter + new Vector3(-w / 2, 0, d / 2);
            Tri(a, b, apex, col); Tri(b, c, apex, col); Tri(c, e, apex, col); Tri(e, a, apex, col);
        }

        /// <summary>Prisma/tronco de cono de n lados. rTop = 0 → cono. smooth = normales suaves (cilindros).</summary>
        public void Prism(Vector3 baseCenter, float rBottom, float rTop, float height, int sides, Color32 col, bool capTop = true,
            bool smooth = false, Color32? topColor = null, float angleOffset = 0f)
        {
            _center = baseCenter + Vector3.up * height * 0.5f;
            if (smooth)
            {
                var profile = new[] { new Vector2(rBottom, 0), new Vector2(rTop, height) };
                Lathe(baseCenter, profile, sides, col, col);
            }
            else
            {
                for (int s = 0; s < sides; s++)
                {
                    float a0 = angleOffset + s * Mathf.PI * 2 / sides, a1 = angleOffset + (s + 1) * Mathf.PI * 2 / sides;
                    var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                    var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                    var b0 = baseCenter + d0 * rBottom;
                    var b1 = baseCenter + d1 * rBottom;
                    var t0 = baseCenter + d0 * rTop + Vector3.up * height;
                    var t1 = baseCenter + d1 * rTop + Vector3.up * height;
                    if (rTop <= 1e-5f) Tri(b0, b1, t0, col);
                    else Quad(b0, t0, t1, b1, col);
                }
            }
            if (capTop && rTop > 1e-5f)
            {
                var top = baseCenter + Vector3.up * height;
                _center = top - Vector3.up * height;
                var tc = topColor ?? col;
                for (int s = 0; s < sides; s++)
                {
                    float a0 = angleOffset + s * Mathf.PI * 2 / sides, a1 = angleOffset + (s + 1) * Mathf.PI * 2 / sides;
                    Tri(top, top + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * rTop, top + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * rTop, tc);
                }
            }
        }

        /// <summary>
        /// Superficie de revolución con normales suaves. profile = (radio, altura) de abajo arriba.
        /// colorBottom/colorTop se interpolan (sirve para oscurecer la base de las copas: oclusión).
        /// </summary>
        public void Lathe(Vector3 baseCenter, IList<Vector2> profile, int sides, Color32 colorBottom, Color32 colorTop)
        {
            int start = _verts.Count;
            int rings = profile.Count;
            float minY = profile[0].y, maxY = profile[rings - 1].y;
            for (int r = 0; r < rings; r++)
            {
                // Tangente del perfil → normal en el plano (radio, altura).
                var prev = profile[Mathf.Max(0, r - 1)];
                var next = profile[Mathf.Min(rings - 1, r + 1)];
                var tangent = (next - prev).normalized;
                var n2 = new Vector2(tangent.y, -tangent.x); // apunta hacia fuera si el perfil sube
                float t = maxY > minY ? (profile[r].y - minY) / (maxY - minY) : 0f;
                var col = Color32.Lerp(colorBottom, colorTop, t);
                for (int s = 0; s <= sides; s++)
                {
                    float a = s * Mathf.PI * 2 / sides;
                    var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    _verts.Add(baseCenter + dir * profile[r].x + Vector3.up * profile[r].y);
                    _normals.Add((dir * n2.x + Vector3.up * n2.y).normalized);
                    _colors.Add(col);
                    _uvs.Add(NoUV);
                }
            }
            int stride = sides + 1;
            for (int r = 0; r < rings - 1; r++)
            for (int s = 0; s < sides; s++)
            {
                int i0 = start + r * stride + s, i1 = i0 + 1, i2 = i0 + stride, i3 = i2 + 1;
                // Orden horario visto desde fuera (Unity): i0, i2, i1 / i1, i2, i3
                _tris.Add(i0); _tris.Add(i2); _tris.Add(i1);
                _tris.Add(i1); _tris.Add(i2); _tris.Add(i3);
            }
        }

        /// <summary>Cúpula semiesférica (o achatada con heightScale).</summary>
        public void Dome(Vector3 baseCenter, float radius, float heightScale, int rings, int sides, Color32 col)
        {
            var profile = new List<Vector2>();
            for (int r = 0; r <= rings; r++)
            {
                float a = r / (float)rings * Mathf.PI * 0.5f;
                profile.Add(new Vector2(Mathf.Cos(a) * radius + (r == rings ? 0f : 0f), Mathf.Sin(a) * radius * heightScale));
            }
            profile[rings] = new Vector2(0.0001f, radius * heightScale);
            Lathe(baseCenter, profile, sides, col, col);
        }

        /// <summary>Cúpula bulbosa (cebolla, estilo ruso), con punta.</summary>
        public void OnionDome(Vector3 baseCenter, float radius, float height, Color32 col)
        {
            var p = new List<Vector2>
            {
                new Vector2(radius * 0.75f, 0), new Vector2(radius * 1.05f, height * 0.18f), new Vector2(radius * 1.1f, height * 0.32f),
                new Vector2(radius * 0.9f, height * 0.5f), new Vector2(radius * 0.45f, height * 0.7f), new Vector2(radius * 0.12f, height * 0.88f),
                new Vector2(0.0001f, height),
            };
            Lathe(baseCenter, p, 10, col, col);
        }

        /// <summary>Esfera de baja resolución (icosaedro subdividido) con deformación orgánica opcional.</summary>
        public void Blob(Vector3 center, Vector3 radius, int subdivisions, Color32 colBottom, Color32 colTop, float noise, int seed)
        {
            var (verts, faces) = Icosphere(subdivisions);
            var rng = new System.Random(seed);
            var offsets = new float[verts.Count];
            for (int i = 0; i < offsets.Length; i++) offsets[i] = 1f + ((float)rng.NextDouble() - 0.5f) * noise;
            int start = _verts.Count;
            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i] * offsets[i];
                _verts.Add(center + Vector3.Scale(v, radius));
                _normals.Add(Vector3.Scale(verts[i], new Vector3(1f / radius.x, 1f / radius.y, 1f / radius.z)).normalized);
                float t = verts[i].y * 0.5f + 0.5f;
                _colors.Add(Color32.Lerp(colBottom, colTop, t));
                _uvs.Add(NoUV);
            }
            foreach (var f in faces)
            {
                // Cara hacia fuera: la normal geométrica debe apuntar en la dirección del centroide.
                Vector3 a = verts[f.x], b = verts[f.y], c = verts[f.z];
                bool outward = Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) > 0f;
                _tris.Add(start + f.x);
                _tris.Add(start + (outward ? f.y : f.z));
                _tris.Add(start + (outward ? f.z : f.y));
            }
        }

        private static (List<Vector3>, List<Vector3Int>) Icosphere(int subdivisions)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            var f = new List<Vector3Int>
            {
                new Vector3Int(0, 11, 5), new Vector3Int(0, 5, 1), new Vector3Int(0, 1, 7), new Vector3Int(0, 7, 10), new Vector3Int(0, 10, 11),
                new Vector3Int(1, 5, 9), new Vector3Int(5, 11, 4), new Vector3Int(11, 10, 2), new Vector3Int(10, 7, 6), new Vector3Int(7, 1, 8),
                new Vector3Int(3, 9, 4), new Vector3Int(3, 4, 2), new Vector3Int(3, 2, 6), new Vector3Int(3, 6, 8), new Vector3Int(3, 8, 9),
                new Vector3Int(4, 9, 5), new Vector3Int(2, 4, 11), new Vector3Int(6, 2, 10), new Vector3Int(8, 6, 7), new Vector3Int(9, 8, 1),
            };
            for (int s = 0; s < subdivisions; s++)
            {
                var cache = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (cache.TryGetValue(key, out int idx)) return idx;
                    v.Add(((v[a] + v[b]) * 0.5f).normalized);
                    cache[key] = v.Count - 1;
                    return v.Count - 1;
                }
                var nf = new List<Vector3Int>();
                foreach (var tri in f)
                {
                    int a = Mid(tri.x, tri.y), b = Mid(tri.y, tri.z), c = Mid(tri.z, tri.x);
                    nf.Add(new Vector3Int(tri.x, a, c)); nf.Add(new Vector3Int(tri.y, b, a));
                    nf.Add(new Vector3Int(tri.z, c, b)); nf.Add(new Vector3Int(a, b, c));
                }
                f = nf;
            }
            return (v, f);
        }

        /// <summary>Barra/viga entre dos puntos (sección cuadrada), útil para estructuras metálicas.</summary>
        public void Beam(Vector3 from, Vector3 to, float thickness, Color32 col)
        {
            var axis = to - from;
            var side = Vector3.Cross(axis, Mathf.Abs(axis.normalized.y) > 0.9f ? Vector3.right : Vector3.up).normalized * thickness * 0.5f;
            var up = Vector3.Cross(side, axis).normalized * thickness * 0.5f;
            _center = (from + to) * 0.5f;
            Vector3[] a = { from - side - up, from + side - up, from + side + up, from - side + up };
            Vector3[] b = { to - side - up, to + side - up, to + side + up, to - side + up };
            for (int i = 0; i < 4; i++) Quad(a[i], b[i], b[(i + 1) % 4], a[(i + 1) % 4], col);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = _verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetColors(_colors);
            mesh.SetUVs(0, _uvs);
            mesh.SetTriangles(_tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
