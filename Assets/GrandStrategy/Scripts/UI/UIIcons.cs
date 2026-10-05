using System;
using System.Collections.Generic;
using UnityEngine;

namespace GrandStrategy.UI
{
    /// <summary>
    /// Iconos de la interfaz dibujados por código (siluetas blancas que se tiñen con Image.color), para no
    /// depender de imágenes externas. Cada icono es una función de distancia con signo en [-1, 1]²
    /// (negativa dentro de la figura) que se rasteriza con antialiasing. Para añadir uno nuevo basta con
    /// añadir una entrada en <see cref="Shapes"/>.
    /// </summary>
    public static class UIIcons
    {
        private const int Size = 64;
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Cache.TryGetValue(name, out var sprite)) return sprite;
            if (!Shapes.TryGetValue(name, out var sdf)) return null;

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = "Icono_" + name, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[Size * Size];
            float aa = 2.4f / Size; // ancho del suavizado del borde en unidades del icono
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                var p = new Vector2((x + 0.5f) / Size * 2f - 1f, (y + 0.5f) / Size * 2f - 1f);
                float d = sdf(p);
                byte a = (byte)(Mathf.Clamp01(0.5f - d / aa) * 255f);
                pixels[y * Size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            Cache[name] = sprite;
            return sprite;
        }

        // ================================================================== Primitivas (distancias con signo)

        private static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;
        private static float Ring(Vector2 p, Vector2 c, float r, float w) => Mathf.Abs((p - c).magnitude - r) - w;

        private static float Box(Vector2 p, Vector2 c, Vector2 half, float round = 0f)
        {
            var d = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + new Vector2(round, round);
            return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - round;
        }

        /// <summary>Caja girada (ángulo en grados).</summary>
        private static float RBox(Vector2 p, Vector2 c, Vector2 half, float angle, float round = 0f)
        {
            float a = -angle * Mathf.Deg2Rad, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            var q = p - c;
            return Box(new Vector2(q.x * cs - q.y * sn, q.x * sn + q.y * cs), Vector2.zero, half, round);
        }

        /// <summary>Segmento con grosor (cápsula).</summary>
        private static float Seg(Vector2 p, Vector2 a, Vector2 b, float w)
        {
            var pa = p - a;
            var ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - w;
        }

        /// <summary>Triángulo (vértices en cualquier orden).</summary>
        private static float Tri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d = Mathf.Min(Mathf.Min(Seg(p, a, b, 0f), Seg(p, b, c, 0f)), Seg(p, c, a, 0f));
            float s1 = Cross(b - a, p - a), s2 = Cross(c - b, p - b), s3 = Cross(a - c, p - c);
            bool inside = (s1 >= 0 && s2 >= 0 && s3 >= 0) || (s1 <= 0 && s2 <= 0 && s3 <= 0);
            return inside ? -d : d;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        private static float U(params float[] d) { float m = d[0]; for (int i = 1; i < d.Length; i++) m = Mathf.Min(m, d[i]); return m; }
        private static float Sub(float a, float b) => Mathf.Max(a, -b);
        private static Vector2 V(float x, float y) => new Vector2(x, y);

        // ================================================================== Iconos

        private static readonly Dictionary<string, Func<Vector2, float>> Shapes = new Dictionary<string, Func<Vector2, float>>
        {
            // Flecha hacia arriba con dos niveles (mejora).
            ["upgrade"] = p => U(
                Tri(p, V(-0.7f, 0.05f), V(0.7f, 0.05f), V(0f, 0.85f)),
                Box(p, V(0f, -0.3f), V(0.26f, 0.38f)),
                Box(p, V(0f, -0.85f), V(0.7f, 0.07f))),
            // Espiga de trigo (industria alimentaria).
            ["wheat"] = p => U(
                Seg(p, V(0f, -0.9f), V(0f, 0.75f), 0.06f),
                RBox(p, V(-0.2f, 0.45f), V(0.2f, 0.09f), 40f, 0.08f), RBox(p, V(0.2f, 0.45f), V(0.2f, 0.09f), -40f, 0.08f),
                RBox(p, V(-0.2f, 0.1f), V(0.2f, 0.09f), 40f, 0.08f), RBox(p, V(0.2f, 0.1f), V(0.2f, 0.09f), -40f, 0.08f),
                RBox(p, V(-0.2f, -0.25f), V(0.2f, 0.09f), 40f, 0.08f), RBox(p, V(0.2f, -0.25f), V(0.2f, 0.09f), -40f, 0.08f),
                Circle(p, V(0f, 0.8f), 0.1f)),
            // Cama (hotel).
            ["bed"] = p => U(
                Box(p, V(0f, -0.3f), V(0.85f, 0.14f)),
                Box(p, V(-0.78f, 0f), V(0.08f, 0.55f)),
                Box(p, V(0.78f, -0.55f), V(0.08f, 0.3f)),
                Box(p, V(-0.45f, 0.0f), V(0.22f, 0.14f), 0.08f),
                Box(p, V(0.2f, -0.04f), V(0.48f, 0.12f), 0.06f),
                Box(p, V(-0.78f, -0.6f), V(0.08f, 0.25f))),
            // Almacén con portón (centro de distribución).
            ["warehouse"] = p => Sub(U(
                    Box(p, V(0f, -0.35f), V(0.85f, 0.5f)),
                    Tri(p, V(-0.95f, 0.15f), V(0.95f, 0.15f), V(0f, 0.7f))),
                U(Box(p, V(0f, -0.5f), V(0.42f, 0.35f)), Box(p, V(0f, 0.25f), V(0.25f, 0.08f)))),
            // Camisa (bienes de consumo).
            ["shirt"] = p => Sub(U(
                    Box(p, V(0f, -0.25f), V(0.45f, 0.6f), 0.05f),
                    RBox(p, V(-0.55f, 0.25f), V(0.32f, 0.16f), -35f, 0.05f),
                    RBox(p, V(0.55f, 0.25f), V(0.32f, 0.16f), 35f, 0.05f)),
                Tri(p, V(-0.2f, 0.36f), V(0.2f, 0.36f), V(0f, 0.08f))),
            // Edificio con columnas (banco).
            ["bank"] = p => U(
                Tri(p, V(-0.9f, 0.4f), V(0.9f, 0.4f), V(0f, 0.85f)),
                Box(p, V(0f, 0.33f), V(0.85f, 0.06f)),
                Box(p, V(-0.6f, -0.2f), V(0.1f, 0.45f)), Box(p, V(-0.2f, -0.2f), V(0.1f, 0.45f)),
                Box(p, V(0.2f, -0.2f), V(0.1f, 0.45f)), Box(p, V(0.6f, -0.2f), V(0.1f, 0.45f)),
                Box(p, V(0f, -0.74f), V(0.9f, 0.09f))),
            // Fábrica con tejado en sierra y chimenea.
            ["factory"] = p => Sub(U(
                    Box(p, V(0f, -0.42f), V(0.85f, 0.38f)),
                    Tri(p, V(-0.85f, -0.05f), V(-0.85f, 0.35f), V(-0.3f, -0.05f)),
                    Tri(p, V(-0.3f, -0.05f), V(-0.3f, 0.35f), V(0.25f, -0.05f)),
                    Box(p, V(0.55f, 0.3f), V(0.14f, 0.55f))),
                U(Box(p, V(-0.5f, -0.45f), V(0.12f, 0.12f)), Box(p, V(-0.05f, -0.45f), V(0.12f, 0.12f)), Box(p, V(0.45f, -0.45f), V(0.12f, 0.12f)))),
            // Martillo.
            ["hammer"] = p => U(
                Seg(p, V(-0.62f, -0.72f), V(0.22f, 0.32f), 0.11f),
                RBox(p, V(0.28f, 0.42f), V(0.46f, 0.17f), -40f, 0.04f)),
            // Tienda con toldo.
            ["shop"] = p => U(
                Sub(Box(p, V(0f, -0.42f), V(0.7f, 0.42f)), Box(p, V(0.28f, -0.55f), V(0.16f, 0.3f))),
                Tri(p, V(-0.88f, 0.05f), V(0.88f, 0.05f), V(0f, 0.72f)),
                Box(p, V(-0.32f, -0.3f), V(0.16f, 0.12f), 0.02f)),
            // Monedas apiladas.
            ["coins"] = p => U(
                Box(p, V(-0.25f, -0.7f), V(0.55f, 0.11f), 0.1f),
                Box(p, V(-0.25f, -0.43f), V(0.55f, 0.11f), 0.1f),
                Box(p, V(-0.25f, -0.16f), V(0.55f, 0.11f), 0.1f),
                Sub(Circle(p, V(0.35f, 0.38f), 0.5f), Ring(p, V(0.35f, 0.38f), 0.3f, 0.05f))),
            // Gráfico de barras.
            ["chart"] = p => U(
                Box(p, V(-0.55f, -0.45f), V(0.15f, 0.3f)),
                Box(p, V(-0.05f, -0.25f), V(0.15f, 0.5f)),
                Box(p, V(0.45f, 0f), V(0.15f, 0.75f)),
                Box(p, V(0f, -0.82f), V(0.9f, 0.06f))),
            // Grúa de construcción.
            ["crane"] = p => U(
                Seg(p, V(-0.45f, -0.85f), V(-0.45f, 0.7f), 0.09f),
                Seg(p, V(-0.75f, 0.6f), V(0.85f, 0.6f), 0.08f),
                Seg(p, V(-0.45f, 0.35f), V(-0.15f, 0.6f), 0.05f),
                Seg(p, V(0.55f, 0.6f), V(0.55f, 0.0f), 0.035f),
                Box(p, V(0.55f, -0.15f), V(0.17f, 0.15f)),
                Box(p, V(-0.45f, -0.85f), V(0.35f, 0.07f))),
            // Reloj.
            ["clock"] = p => U(
                Ring(p, Vector2.zero, 0.75f, 0.1f),
                Seg(p, Vector2.zero, V(0f, 0.48f), 0.08f),
                Seg(p, Vector2.zero, V(0.35f, -0.12f), 0.08f)),
            // Maletín.
            ["briefcase"] = p => U(
                Sub(Box(p, V(0f, -0.2f), V(0.85f, 0.52f), 0.1f), Box(p, V(0f, -0.05f), V(0.85f, 0.03f))),
                Sub(Box(p, V(0f, 0.42f), V(0.32f, 0.2f), 0.08f), Box(p, V(0f, 0.38f), V(0.2f, 0.12f)))),
            // Flechas de comercio (exportar / importar).
            ["trade"] = p => U(
                Seg(p, V(-0.75f, 0.35f), V(0.45f, 0.35f), 0.09f),
                Tri(p, V(0.35f, 0.65f), V(0.35f, 0.05f), V(0.85f, 0.35f)),
                Seg(p, V(0.75f, -0.35f), V(-0.45f, -0.35f), 0.09f),
                Tri(p, V(-0.35f, -0.05f), V(-0.35f, -0.65f), V(-0.85f, -0.35f))),
            // Engranaje.
            ["gear"] = p =>
            {
                float ang = Mathf.Atan2(p.y, p.x);
                float teeth = Mathf.Clamp01(Mathf.Cos(ang * 8f) * 3f) * 0.2f;
                return Sub(p.magnitude - (0.6f + teeth), Circle(p, Vector2.zero, 0.25f));
            },
            // Caja de madera (depósito, suministros, reservas).
            ["crate"] = p => U(
                Sub(Box(p, Vector2.zero, V(0.78f, 0.78f), 0.06f), Box(p, Vector2.zero, V(0.6f, 0.6f))),
                Mathf.Max(Seg(p, V(-0.6f, -0.6f), V(0.6f, 0.6f), 0.08f), Box(p, Vector2.zero, V(0.6f, 0.6f))),
                Mathf.Max(Seg(p, V(-0.6f, 0.6f), V(0.6f, -0.6f), 0.08f), Box(p, Vector2.zero, V(0.6f, 0.6f)))),
            // Espadas cruzadas.
            ["swords"] = p => U(
                Seg(p, V(-0.7f, -0.7f), V(0.62f, 0.62f), 0.075f),
                Seg(p, V(0.7f, -0.7f), V(-0.62f, 0.62f), 0.075f),
                Seg(p, V(-0.62f, -0.32f), V(-0.32f, -0.62f), 0.08f),
                Seg(p, V(0.62f, -0.32f), V(0.32f, -0.62f), 0.08f)),
            // Avión visto desde arriba.
            ["plane"] = p => U(
                Seg(p, V(0f, -0.8f), V(0f, 0.8f), 0.13f),
                Tri(p, V(-0.9f, -0.05f), V(0.9f, -0.05f), V(0f, 0.38f)),
                Box(p, V(0f, -0.05f), V(0.9f, 0.08f), 0.04f),
                Box(p, V(0f, -0.72f), V(0.38f, 0.07f), 0.03f)),
            // Ancla.
            ["anchor"] = p => U(
                Ring(p, V(0f, 0.62f), 0.16f, 0.06f),
                Seg(p, V(0f, 0.45f), V(0f, -0.72f), 0.08f),
                Seg(p, V(-0.35f, 0.28f), V(0.35f, 0.28f), 0.07f),
                Mathf.Max(Ring(p, V(0f, -0.15f), 0.62f, 0.07f), p.y + 0.2f),
                Tri(p, V(-0.78f, -0.05f), V(-0.45f, -0.18f), V(-0.68f, -0.4f)),
                Tri(p, V(0.78f, -0.05f), V(0.45f, -0.18f), V(0.68f, -0.4f))),
            // Barco de guerra.
            ["ship"] = p => U(
                Tri(p, V(-0.95f, -0.15f), V(0.95f, -0.15f), V(0.75f, -0.55f)),
                Box(p, V(-0.12f, -0.35f), V(0.83f, 0.2f)),
                Box(p, V(0f, 0.02f), V(0.38f, 0.16f)),
                Box(p, V(-0.05f, 0.3f), V(0.14f, 0.15f)),
                Seg(p, V(0.2f, 0.12f), V(0.65f, 0.25f), 0.04f)),
            // Fusil.
            ["rifle"] = p => U(
                Seg(p, V(-0.35f, -0.25f), V(0.88f, 0.55f), 0.06f),
                RBox(p, V(-0.2f, -0.18f), V(0.32f, 0.11f), 33f, 0.03f),
                Tri(p, V(-0.45f, -0.35f), V(-0.88f, -0.75f), V(-0.62f, -0.12f)),
                Seg(p, V(-0.12f, -0.3f), V(-0.2f, -0.5f), 0.05f)),
            // Cañón con rueda.
            ["cannon"] = p => U(
                Seg(p, V(-0.45f, -0.25f), V(0.82f, 0.45f), 0.14f),
                Sub(Circle(p, V(-0.3f, -0.45f), 0.38f), Circle(p, V(-0.3f, -0.45f), 0.2f)),
                Circle(p, V(-0.3f, -0.45f), 0.08f),
                Seg(p, V(-0.3f, -0.45f), V(-0.88f, -0.82f), 0.07f)),
            // Carro de combate.
            ["tank"] = p => U(
                Box(p, V(0f, -0.48f), V(0.88f, 0.22f), 0.2f),
                Box(p, V(-0.05f, -0.12f), V(0.62f, 0.14f), 0.04f),
                Box(p, V(-0.1f, 0.15f), V(0.32f, 0.16f), 0.08f),
                Seg(p, V(0.15f, 0.18f), V(0.92f, 0.22f), 0.06f)),
            // Matraz (investigación).
            ["flask"] = p => U(
                Box(p, V(0f, 0.55f), V(0.16f, 0.3f)),
                Box(p, V(0f, 0.82f), V(0.28f, 0.06f), 0.03f),
                Tri(p, V(-0.17f, 0.3f), V(0.17f, 0.3f), V(0f, 0.1f)),
                Tri(p, V(-0.17f, 0.3f), V(-0.72f, -0.78f), V(0.72f, -0.78f)),
                Tri(p, V(0.17f, 0.3f), V(0.72f, -0.78f), V(-0.17f, 0.3f))),
            // Documento sellado (diplomacia).
            ["scroll"] = p => U(
                Sub(Box(p, V(-0.12f, 0.05f), V(0.6f, 0.78f), 0.06f),
                    U(Box(p, V(-0.12f, 0.45f), V(0.4f, 0.05f)), Box(p, V(-0.12f, 0.2f), V(0.4f, 0.05f)), Box(p, V(-0.22f, -0.05f), V(0.3f, 0.05f)))),
                Circle(p, V(0.45f, -0.5f), 0.3f)),
            // Casco militar (ejército).
            ["helmet"] = p => U(
                Mathf.Max(Circle(p, V(0f, -0.2f), 0.68f), -(p.y + 0.15f)),
                Box(p, V(0f, -0.22f), V(0.88f, 0.08f), 0.06f),
                Seg(p, V(-0.45f, -0.25f), V(-0.2f, -0.65f), 0.04f),
                Seg(p, V(0.45f, -0.25f), V(0.2f, -0.65f), 0.04f)),
        };
    }
}
