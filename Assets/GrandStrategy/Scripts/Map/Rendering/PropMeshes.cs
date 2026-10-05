using System;
using System.Collections.Generic;
using UnityEngine;

namespace GrandStrategy.Map.Rendering
{
    /// <summary>Tipos de árbol.</summary>
    public enum TreeKind { Conifer, Broadleaf, Tropical }

    /// <summary>Arquetipos de edificio. En calidad baja se usa una casa sencilla.</summary>
    public enum BuildingKind
    {
        House, Townhouses, Apartment, Tower, Church, Factory, Civic,
        // Calidad alta
        PerimeterBlock, AsianHouse, AsianTemple, FlatHouse, CourtyardHouse, Mosque, Warehouse, Crane, Pier, Ship,
        // Arquitectura nacional de los años 30 (ver PropMeshesNational.cs)
        HalfTimber, GothicChurch, BrickTerrace, TowerChurch, MedHouse, Campanile, DomedChurch, Izba, OnionChurch, SovietBlock,
        Machiya, Torii, Siheyuan, ChineseTemple, BrickPagoda, ColonialHouse, BaroqueChurch, CapeDutch, OttomanHouse, OttomanMosque,
        NordicHouse, NordicChurch, DecoTower, Clapboard, FrenchHouse, FrenchCathedral, DutchGable, HinduTemple, IndoSaracenic,
        Stupa, HimalayanHouse, ByzantineChurch,
    }

    /// <summary>
    /// Estilo arquitectónico (como los conjuntos de edificios por cultura de Victoria 3). Los cinco primeros son
    /// regionales genéricos; el resto, estilos nacionales de los años 30 para las potencias y actores principales.
    /// </summary>
    public enum ArchStyle
    {
        European, American, EastAsian, MiddleEastern, SouthAsian,
        British, French, Germanic, LowCountries, Mediterranean, Balkan, Russian, Nordic, Japanese, Chinese,
        LatinColonial, CapeDutch, Ottoman, Himalayan,
    }

    /// <summary>
    /// Mallas de árboles, edificios y monumentos en unidades locales (árbol ≈ 1 de alto, edificio ≈ 1 de planta).
    /// Se dibujan con GPU instancing, así que cada malla existe una sola vez en memoria.
    /// </summary>
    public static partial class PropMeshes
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        private static Mesh Cached(string key, Func<Mesh> build)
        {
            if (!Cache.TryGetValue(key, out var mesh) || mesh == null) Cache[key] = mesh = build();
            return mesh;
        }

        public static void ClearCache()
        {
            foreach (var m in Cache.Values)
                if (m != null) UnityEngine.Object.Destroy(m);
            Cache.Clear();
        }

        // Paleta de época.
        private static readonly Color Bark = new Color(0.27f, 0.19f, 0.12f);
        private static readonly Color Stone = new Color(0.8f, 0.75f, 0.64f);
        private static readonly Color Brick = new Color(0.6f, 0.33f, 0.25f);
        private static readonly Color Plaster = new Color(0.86f, 0.8f, 0.68f);
        private static readonly Color Grey = new Color(0.62f, 0.62f, 0.6f);
        private static readonly Color Terracotta = new Color(0.62f, 0.3f, 0.2f);
        private static readonly Color Slate = new Color(0.3f, 0.32f, 0.36f);
        private static readonly Color Marble = new Color(0.92f, 0.9f, 0.85f);
        private static readonly Color Metal = new Color(0.33f, 0.27f, 0.21f);
        private static readonly Color Copper = new Color(0.36f, 0.6f, 0.5f);
        private static readonly Color Gold = new Color(0.85f, 0.66f, 0.25f);

        // ================================================================== Árboles

        /// <summary>Árbol según la calidad: Bajo = figuras simples, Medio = volúmenes suaves, Alto = tarjetas de hojas.</summary>
        public static Mesh Tree(TreeKind kind, GrandStrategy.Core.DetailLevel level) =>
            level >= GrandStrategy.Core.DetailLevel.High ? HighTree(kind) : Tree(kind, level == GrandStrategy.Core.DetailLevel.Medium);

        public static Mesh Tree(TreeKind kind, bool high) => Cached($"tree_{kind}_{high}", () =>
        {
            var k = new MeshKit();
            switch (kind)
            {
                case TreeKind.Conifer:
                    if (!high)
                    {
                        k.Prism(Vector3.up * 0.15f, 0.34f, 0f, 0.85f, 5, MeshKit.Solid(new Color(0.13f, 0.24f, 0.15f)), false);
                        k.Prism(Vector3.zero, 0.06f, 0.05f, 0.15f, 3, MeshKit.Solid(Bark));
                        break;
                    }
                    k.Prism(Vector3.zero, 0.055f, 0.035f, 0.32f, 6, MeshKit.Solid(Bark), false, true);
                    for (int i = 0; i < 3; i++)
                    {
                        float y = 0.16f + i * 0.24f, h = 0.44f - i * 0.05f, r = 0.36f * (1f - i * 0.24f);
                        var profile = new[] { new Vector2(r * 0.55f, 0), new Vector2(r, h * 0.12f), new Vector2(r * 0.6f, h * 0.5f), new Vector2(0.001f, h) };
                        k.Lathe(Vector3.up * y, profile, 9, MeshKit.Solid(new Color(0.08f, 0.16f, 0.1f)), MeshKit.Solid(new Color(0.2f, 0.34f, 0.2f)));
                    }
                    break;

                case TreeKind.Broadleaf:
                case TreeKind.Tropical:
                    bool tropical = kind == TreeKind.Tropical;
                    var dark = tropical ? new Color(0.06f, 0.18f, 0.07f) : new Color(0.12f, 0.2f, 0.08f);
                    var light = tropical ? new Color(0.16f, 0.4f, 0.14f) : new Color(0.32f, 0.46f, 0.18f);
                    if (!high)
                    {
                        k.Prism(Vector3.zero, 0.05f, 0.04f, 0.3f, 3, MeshKit.Solid(Bark));
                        k.Blob(Vector3.up * 0.62f, new Vector3(0.36f, 0.32f, 0.36f), 0, MeshKit.Solid(dark), MeshKit.Solid(light), 0f, 1);
                        break;
                    }
                    float trunkH = tropical ? 0.55f : 0.38f;
                    k.Prism(Vector3.zero, 0.06f, 0.04f, trunkH, 6, MeshKit.Solid(Bark), false, true);
                    k.Beam(new Vector3(0, trunkH * 0.75f, 0), new Vector3(0.18f, trunkH + 0.1f, 0.05f), 0.035f, MeshKit.Solid(Bark));
                    k.Beam(new Vector3(0, trunkH * 0.7f, 0), new Vector3(-0.14f, trunkH + 0.08f, -0.1f), 0.03f, MeshKit.Solid(Bark));
                    float cy = trunkH + (tropical ? 0.12f : 0.22f);
                    var squash = tropical ? 0.55f : 0.85f;
                    k.Blob(new Vector3(0, cy + 0.06f, 0), new Vector3(0.3f, 0.28f * squash, 0.3f), 1, MeshKit.Solid(dark), MeshKit.Solid(light), 0.22f, 3);
                    k.Blob(new Vector3(0.17f, cy - 0.02f, 0.06f), new Vector3(0.22f, 0.2f * squash, 0.22f), 1, MeshKit.Solid(dark), MeshKit.Solid(light), 0.25f, 5);
                    k.Blob(new Vector3(-0.15f, cy - 0.03f, -0.08f), new Vector3(0.21f, 0.19f * squash, 0.21f), 1, MeshKit.Solid(dark), MeshKit.Solid(light), 0.25f, 7);
                    if (tropical) k.Blob(new Vector3(0.02f, cy - 0.02f, 0.18f), new Vector3(0.2f, 0.12f, 0.2f), 1, MeshKit.Solid(dark), MeshKit.Solid(light), 0.25f, 9);
                    break;
            }
            return k.ToMesh($"Tree_{kind}_{(high ? "Alta" : "Baja")}");
        });

        // ================================================================== Árboles de calidad alta (tarjetas)

        /// <summary>
        /// Árbol con "tarjetas" de hojas (leaf cards): tronco y ramas reales + muchos quads con la textura del atlas.
        /// Las normales de las tarjetas salen del centro de la copa (sombreado esférico suave).
        /// Alfa del color = peso del viento.
        /// </summary>
        private static Mesh HighTree(TreeKind kind) => Cached($"tree_{kind}_ultra", () =>
        {
            var k = new MeshKit();
            var rng = new System.Random(17 + (int)kind);
            switch (kind)
            {
                case TreeKind.Conifer:
                {
                    k.Prism(Vector3.zero, 0.045f, 0.012f, 0.98f, 7, MeshKit.Solid(Bark), false, true);
                    // Núcleo oscuro: evita ver a través de la copa entre ramas.
                    k.Lathe(Vector3.up * 0.16f, new[] { new Vector2(0.17f, 0), new Vector2(0.12f, 0.35f), new Vector2(0.001f, 0.8f) }, 8,
                        MeshKit.Solid(new Color(0.07f, 0.14f, 0.08f)), MeshKit.Solid(new Color(0.11f, 0.2f, 0.11f)));
                    const int levels = 8;
                    for (int i = 0; i < levels; i++)
                    {
                        float y = 0.17f + i * 0.1f;
                        float len = 0.44f * (1f - i / (levels + 0.6f));
                        int count = i < 6 ? 9 : 6;
                        float ao = Mathf.Lerp(0.75f, 1.15f, i / (float)(levels - 1));
                        for (int b = 0; b < count; b++)
                        {
                            float a = b * Mathf.PI * 2f / count + i * 2.4f + (float)rng.NextDouble() * 0.4f;
                            var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                            var droop = Vector3.down * len * 0.22f;
                            var trunkPt = Vector3.up * y;
                            var center = trunkPt + d * len * 0.5f + droop * 0.5f;
                            var right = d * len * 0.5f + droop * 0.5f;              // a lo largo de la rama
                            var up = Vector3.Cross(Vector3.up, d) * len * 0.5f + Vector3.up * len * 0.08f; // ancho
                            var col = new Color(ao, ao, ao, 0.85f);
                            k.Card(center, right, up, VegetationTextures.NeedlesRect, col, col, (d * 0.7f + Vector3.up * 0.7f));
                        }
                    }
                    // Punta
                    var tipCol = new Color(1.05f, 1.05f, 1.05f, 1f);
                    k.Card(Vector3.up * 1.0f, Vector3.up * 0.12f, Vector3.right * 0.06f, VegetationTextures.NeedlesRect, tipCol, tipCol, Vector3.up);
                    k.Card(Vector3.up * 1.0f, Vector3.up * 0.12f, Vector3.forward * 0.06f, VegetationTextures.NeedlesRect, tipCol, tipCol, Vector3.up);
                    break;
                }

                case TreeKind.Broadleaf:
                {
                    k.Prism(Vector3.zero, 0.06f, 0.035f, 0.5f, 7, MeshKit.Solid(Bark), false, true);
                    var crown = Vector3.up * 0.7f;
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI * 0.5f + 0.4f;
                        k.Beam(Vector3.up * (0.32f + i * 0.04f), crown + new Vector3(Mathf.Cos(a) * 0.22f, 0.02f, Mathf.Sin(a) * 0.22f), 0.03f, MeshKit.Solid(Bark));
                    }
                    k.Blob(crown, new Vector3(0.27f, 0.22f, 0.27f), 1, MeshKit.Solid(new Color(0.08f, 0.14f, 0.05f)), MeshKit.Solid(new Color(0.14f, 0.24f, 0.08f)), 0.15f, 11);
                    AddLeafCards(k, rng, crown, new Vector3(0.37f, 0.3f, 0.37f), 34, VegetationTextures.LeavesRect, 0.2f);
                    break;
                }

                case TreeKind.Tropical:
                {
                    // Palmera: tronco curvo por segmentos + hojas en abanico que caen.
                    var p = Vector3.zero;
                    var top = Vector3.zero;
                    for (int i = 0; i < 6; i++)
                    {
                        var next = p + new Vector3(0.03f + i * 0.008f, 0.16f, 0.01f);
                        k.Beam(p, next, 0.05f - i * 0.004f, MeshKit.Solid(new Color(0.42f, 0.34f, 0.24f)));
                        p = next;
                    }
                    top = p;
                    for (int f = 0; f < 10; f++)
                    {
                        float a = f * Mathf.PI * 2f / 10f + (float)rng.NextDouble() * 0.3f;
                        var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        var center = top + d * 0.26f + Vector3.down * 0.07f;
                        var right = d * 0.3f + Vector3.down * 0.12f;
                        var up = Vector3.Cross(Vector3.up, d) * 0.13f;
                        var col = new Color(0.95f, 0.95f, 0.95f, 1f);
                        k.Card(center, right, up, VegetationTextures.FrondRect, col, col, d * 0.4f + Vector3.up);
                    }
                    break;
                }
            }
            return k.ToMesh($"Tree_{kind}_Alta");
        });

        /// <summary>Tarjetas de hojas repartidas por la superficie de un elipsoide (copa).</summary>
        private static void AddLeafCards(MeshKit k, System.Random rng, Vector3 center, Vector3 radius, int count, Rect rect, float cardSize)
        {
            for (int i = 0; i < count; i++)
            {
                // Dirección aleatoria con algo más de densidad arriba.
                float z = Mathf.Lerp(-0.55f, 1f, Mathf.Sqrt((float)rng.NextDouble()));
                float a = (float)(rng.NextDouble() * Mathf.PI * 2);
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                var dir = new Vector3(Mathf.Cos(a) * r, z, Mathf.Sin(a) * r);
                var pos = center + Vector3.Scale(dir, radius) * (0.75f + (float)rng.NextDouble() * 0.3f);
                var tangent = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                float spin = (float)(rng.NextDouble() * Mathf.PI * 2);
                var right = (tangent * Mathf.Cos(spin) + Vector3.Cross(dir, tangent) * Mathf.Sin(spin)) * cardSize;
                var up = Vector3.Cross(dir, right).normalized * cardSize;
                float ao = Mathf.Lerp(0.55f, 1.08f, dir.y * 0.5f + 0.5f);
                var col = new Color(ao, ao, ao, 1f);
                k.Card(pos, right, up, rect, col * 0.92f, col, dir);
            }
        }

        // ================================================================== Vegetación de suelo

        /// <summary>Mata de hierba: tres tarjetas cruzadas.</summary>
        public static Mesh Grass() => Cached("grass", () =>
        {
            var k = new MeshKit();
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI / 3f;
                var right = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.5f;
                k.Card(Vector3.up * 0.42f, right, Vector3.up * 0.42f, VegetationTextures.GrassRect,
                    new Color(0.55f, 0.55f, 0.55f, 0f), new Color(1.05f, 1.05f, 1.05f, 1f), Vector3.up + right * 0.3f);
            }
            return k.ToMesh("Hierba");
        });

        /// <summary>Arbusto: núcleo oscuro con tarjetas de hojas.</summary>
        public static Mesh Bush() => Cached("bush", () =>
        {
            var k = new MeshKit();
            var rng = new System.Random(3);
            var c = Vector3.up * 0.32f;
            k.Blob(c, new Vector3(0.3f, 0.22f, 0.3f), 1, MeshKit.Solid(new Color(0.07f, 0.12f, 0.05f)), MeshKit.Solid(new Color(0.12f, 0.2f, 0.07f)), 0.2f, 4);
            AddLeafCards(k, rng, c, new Vector3(0.4f, 0.3f, 0.4f), 14, VegetationTextures.LeavesRect, 0.22f);
            return k.ToMesh("Arbusto");
        });

        // ================================================================== Edificios

        /// <summary>Casa sencilla (calidad baja).</summary>
        public static Mesh SimpleBuilding() => Cached("building_low", () =>
        {
            var k = new MeshKit();
            k.Box(Vector3.zero, new Vector3(1f, 0.9f, 0.8f), MeshKit.Wall(Plaster), MeshKit.Solid(Terracotta));
            k.GableRoof(Vector3.up * 0.9f, 1f, 0.8f, 0.35f, 0f, MeshKit.Solid(Terracotta), MeshKit.Wall(Plaster));
            return k.ToMesh("Edificio_Bajo");
        });

        public static Mesh Building(BuildingKind kind, int variant) => Cached($"building_{kind}_{variant}", () =>
        {
            var k = new MeshKit();
            var rng = new System.Random(variant * 31 + (int)kind);
            Color wall = variant % 3 == 0 ? Plaster : variant % 3 == 1 ? Stone : Brick;
            Color roof = variant % 2 == 0 ? Terracotta : Slate;
            switch (kind)
            {
                case BuildingKind.House:
                    k.Box(Vector3.zero, new Vector3(0.9f, 0.6f, 0.7f), MeshKit.Wall(wall), MeshKit.Solid(roof));
                    k.GableRoof(Vector3.up * 0.6f, 0.9f, 0.7f, 0.36f, 0.07f, MeshKit.Solid(roof), MeshKit.Wall(wall));
                    k.Box(new Vector3(0.25f, 0.62f, 0.12f), new Vector3(0.1f, 0.38f, 0.1f), MeshKit.Solid(Brick), MeshKit.Solid(Slate));
                    break;

                case BuildingKind.Townhouses:
                    for (int i = 0; i < 3; i++)
                    {
                        float h = 0.75f + (float)rng.NextDouble() * 0.4f;
                        var c = new Vector3(-0.55f + i * 0.55f, 0, 0);
                        Color w = i % 2 == 0 ? wall : Plaster;
                        k.Box(c, new Vector3(0.55f, h, 0.85f), MeshKit.Wall(w), MeshKit.Solid(roof));
                        k.GableRoof(c + Vector3.up * h, 0.55f, 0.85f, 0.3f, 0.02f, MeshKit.Solid(roof), MeshKit.Wall(w));
                    }
                    break;

                case BuildingKind.Apartment:
                    float ah = 1.3f + (float)rng.NextDouble() * 0.5f;
                    k.Box(Vector3.zero, new Vector3(1f, ah, 0.9f), MeshKit.Wall(wall), MeshKit.Solid(Grey));
                    k.Box(Vector3.up * ah, new Vector3(1.06f, 0.06f, 0.96f), MeshKit.Solid(Stone), MeshKit.Solid(Grey));          // cornisa
                    k.Box(Vector3.up * 0.0f, new Vector3(1.04f, 0.16f, 0.94f), MeshKit.Solid(Stone), MeshKit.Solid(Stone));          // zócalo
                    k.Box(new Vector3(0.2f, ah + 0.06f, -0.1f), new Vector3(0.28f, 0.16f, 0.3f), MeshKit.Solid(Grey), MeshKit.Solid(Slate));
                    break;

                case BuildingKind.Tower:
                    k.Box(Vector3.zero, new Vector3(1f, 0.5f, 1f), MeshKit.Wall(Stone), MeshKit.Solid(Grey));
                    k.Box(Vector3.up * 0.5f, new Vector3(0.85f, 2.4f, 0.85f), MeshKit.Wall(wall == Brick ? Stone : wall), MeshKit.Solid(Grey));
                    k.Box(Vector3.up * 2.9f, new Vector3(0.62f, 0.9f, 0.62f), MeshKit.Wall(Stone), MeshKit.Solid(Grey));
                    k.Box(Vector3.up * 3.8f, new Vector3(0.4f, 0.5f, 0.4f), MeshKit.Wall(Stone), MeshKit.Solid(Grey));
                    k.Prism(Vector3.up * 4.3f, 0.06f, 0f, 0.6f, 4, MeshKit.Solid(Metal));
                    break;

                case BuildingKind.Church:
                    k.Box(Vector3.zero, new Vector3(0.7f, 0.75f, 1.5f), MeshKit.Wall(Stone), MeshKit.Solid(Slate));
                    k.GableRoof(Vector3.up * 0.75f, 0.7f, 1.5f, 0.42f, 0.04f, MeshKit.Solid(Slate), MeshKit.Wall(Stone));
                    k.Box(new Vector3(0, 0, -0.9f), new Vector3(0.38f, 1.45f, 0.38f), MeshKit.Wall(Stone), MeshKit.Solid(Slate));
                    k.Pyramid(new Vector3(0, 1.45f, -0.9f), 0.38f, 0.38f, 0.95f, MeshKit.Solid(Slate));
                    break;

                case BuildingKind.Factory:
                    k.Box(Vector3.zero, new Vector3(1.5f, 0.55f, 1f), MeshKit.Wall(Brick), MeshKit.Solid(Grey));
                    for (int i = 0; i < 3; i++)
                    {
                        // Tejado en diente de sierra (lucernarios).
                        float x0 = -0.75f + i * 0.5f, x1 = x0 + 0.5f;
                        var lowA = new Vector3(x0, 0.55f, -0.5f); var lowB = new Vector3(x0, 0.55f, 0.5f);
                        var highA = new Vector3(x1, 0.85f, -0.5f); var highB = new Vector3(x1, 0.85f, 0.5f);
                        k.Quad(lowA, lowB, highB, highA, MeshKit.Solid(Slate));
                        k.Quad(new Vector3(x1, 0.55f, -0.5f), highA, highB, new Vector3(x1, 0.55f, 0.5f), MeshKit.Wall(new Color(0.45f, 0.5f, 0.52f)));
                        k.Tri(lowA, highA, new Vector3(x1, 0.55f, -0.5f), MeshKit.Solid(Brick));
                        k.Tri(lowB, highB, new Vector3(x1, 0.55f, 0.5f), MeshKit.Solid(Brick));
                    }
                    k.Prism(new Vector3(0.55f, 0, 0.62f), 0.1f, 0.07f, 2.1f, 8, MeshKit.Solid(Brick), true, true);
                    k.Prism(new Vector3(0.2f, 0, 0.62f), 0.08f, 0.06f, 1.6f, 8, MeshKit.Solid(Brick), true, true);
                    break;

                case BuildingKind.Civic:
                    k.Box(Vector3.zero, new Vector3(1.1f, 0.7f, 1f), MeshKit.Wall(Stone), MeshKit.Solid(Grey));
                    k.Prism(Vector3.up * 0.7f, 0.3f, 0.3f, 0.18f, 12, MeshKit.Wall(Stone), false, true);
                    k.Dome(Vector3.up * 0.88f, 0.3f, 1f, 5, 12, MeshKit.Solid(Copper));
                    break;
            }
            return k.ToMesh($"Edificio_{kind}_{variant}");
        });

        // ================================================================== Edificios de calidad alta por estilo

        private static void StylePalette(ArchStyle style, int variant, out Color wall, out Color roof)
        {
            if (NationalPalette(style, variant, out wall, out roof)) return;
            switch (style)
            {
                case ArchStyle.EastAsian:
                    wall = variant % 2 == 0 ? new Color(0.88f, 0.85f, 0.78f) : new Color(0.48f, 0.34f, 0.24f);
                    roof = variant % 3 == 0 ? new Color(0.22f, 0.36f, 0.32f) : new Color(0.2f, 0.21f, 0.24f);
                    break;
                case ArchStyle.MiddleEastern:
                    wall = variant % 3 == 0 ? new Color(0.93f, 0.9f, 0.84f) : variant % 3 == 1 ? new Color(0.85f, 0.74f, 0.55f) : new Color(0.8f, 0.68f, 0.5f);
                    roof = new Color(0.78f, 0.7f, 0.56f);
                    break;
                case ArchStyle.SouthAsian:
                    Color[] pastel = { new Color(0.92f, 0.82f, 0.62f), new Color(0.9f, 0.7f, 0.62f), new Color(0.94f, 0.92f, 0.86f), new Color(0.78f, 0.8f, 0.66f) };
                    wall = pastel[variant % pastel.Length];
                    roof = new Color(0.62f, 0.4f, 0.3f);
                    break;
                default:
                    wall = variant % 3 == 0 ? Plaster : variant % 3 == 1 ? Stone : Brick;
                    roof = variant % 2 == 0 ? Terracotta : Slate;
                    break;
            }
        }

        /// <summary>Edificios de la calidad alta (nuevos tipos y variantes regionales).</summary>
        public static Mesh StyledBuilding(BuildingKind kind, int variant, ArchStyle style) => Cached($"sb_{kind}_{variant}_{style}", () =>
        {
            var k = new MeshKit();
            var rng = new System.Random(variant * 97 + (int)kind * 13 + (int)style);
            StylePalette(style, variant, out var wall, out var roof);
            switch (kind)
            {
                case BuildingKind.PerimeterBlock:
                {
                    // Manzana con patio interior (ensanche europeo / haussmanniano), tejados abuhardillados.
                    float t = 0.3f;
                    var mansard = BlockRoof(style, variant); // zinc en París, teja en Roma o Buenos Aires...
                    (Vector3 c, Vector3 s)[] wings =
                    {
                        (new Vector3(0, 0, -0.5f + t / 2), new Vector3(1f, 0, t)), (new Vector3(0, 0, 0.5f - t / 2), new Vector3(1f, 0, t)),
                        (new Vector3(-0.5f + t / 2, 0, 0), new Vector3(t, 0, 1f - 2 * t)), (new Vector3(0.5f - t / 2, 0, 0), new Vector3(t, 0, 1f - 2 * t)),
                    };
                    foreach (var (c, s) in wings)
                    {
                        float h = 0.95f + (float)rng.NextDouble() * 0.35f;
                        Color w = rng.NextDouble() < 0.7 ? wall : Plaster;
                        k.Box(c, new Vector3(s.x, h, s.z), MeshKit.Wall(w), MeshKit.Solid(mansard));
                        k.Box(c + Vector3.up * h, new Vector3(s.x + 0.03f, 0.04f, s.z + 0.03f), MeshKit.Solid(Stone), MeshKit.Solid(Stone));
                        k.HipRoof(c + Vector3.up * (h + 0.04f), s.x, s.z, 0.22f, 0f, MeshKit.Solid(mansard));
                    }
                    if (variant % 3 == 0) // rotonda en esquina
                    {
                        k.Prism(new Vector3(0.42f, 0, 0.42f), 0.12f, 0.12f, 1.3f, 10, MeshKit.Wall(Stone), false, true);
                        k.Dome(new Vector3(0.42f, 1.3f, 0.42f), 0.12f, 1.3f, 4, 10, MeshKit.Solid(mansard));
                    }
                    break;
                }

                case BuildingKind.AsianHouse:
                {
                    float h = 0.42f + (float)rng.NextDouble() * 0.15f;
                    k.Box(Vector3.zero, new Vector3(0.95f, h, 0.7f), MeshKit.Wall(wall), MeshKit.Solid(roof));
                    k.HipRoof(Vector3.up * h, 0.95f, 0.7f, 0.3f, 0.16f, MeshKit.Solid(roof));
                    if (variant % 2 == 1)
                    {
                        k.Box(new Vector3(0, h + 0.22f, 0), new Vector3(0.55f, 0.3f, 0.45f), MeshKit.Wall(wall), MeshKit.Solid(roof));
                        k.HipRoof(new Vector3(0, h + 0.52f, 0), 0.55f, 0.45f, 0.22f, 0.12f, MeshKit.Solid(roof));
                    }
                    break;
                }

                case BuildingKind.AsianTemple:
                {
                    var red = new Color(0.62f, 0.2f, 0.15f);
                    float y = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        float w = 0.7f - i * 0.15f;
                        k.Box(Vector3.up * y, new Vector3(w, 0.3f, w), MeshKit.Wall(red), MeshKit.Solid(roof));
                        k.HipRoof(Vector3.up * (y + 0.3f), w, w, 0.14f, 0.2f, MeshKit.Solid(roof));
                        y += 0.42f;
                    }
                    k.Prism(Vector3.up * y, 0.03f, 0.005f, 0.4f, 6, MeshKit.Solid(Gold));
                    break;
                }

                case BuildingKind.FlatHouse:
                {
                    float h = 0.5f + (float)rng.NextDouble() * 0.5f;
                    k.Box(Vector3.zero, new Vector3(0.85f, h, 0.85f), MeshKit.Wall(wall), MeshKit.Solid(roof));
                    // Pretil (parapeto) alrededor de la azotea.
                    var parapet = MeshKit.Solid(wall * 0.95f);
                    k.Box(new Vector3(0, h, -0.4f), new Vector3(0.85f, 0.08f, 0.05f), parapet, parapet);
                    k.Box(new Vector3(0, h, 0.4f), new Vector3(0.85f, 0.08f, 0.05f), parapet, parapet);
                    k.Box(new Vector3(-0.4f, h, 0), new Vector3(0.05f, 0.08f, 0.85f), parapet, parapet);
                    k.Box(new Vector3(0.4f, h, 0), new Vector3(0.05f, 0.08f, 0.85f), parapet, parapet);
                    if (variant % 2 == 0) k.Box(new Vector3(0.2f, h, 0.15f), new Vector3(0.28f, 0.2f, 0.28f), MeshKit.Wall(wall), MeshKit.Solid(roof));
                    break;
                }

                case BuildingKind.CourtyardHouse:
                {
                    float h = 0.45f;
                    float t = 0.28f;
                    k.Box(new Vector3(0, 0, -0.36f), new Vector3(1f, h, t), MeshKit.Wall(wall), MeshKit.Solid(roof));
                    k.Box(new Vector3(0, 0, 0.36f), new Vector3(1f, h, t), MeshKit.Wall(wall), MeshKit.Solid(roof));
                    k.Box(new Vector3(-0.36f, 0, 0), new Vector3(t, h, 0.44f), MeshKit.Wall(wall), MeshKit.Solid(roof));
                    k.Box(new Vector3(0.36f, 0, 0), new Vector3(t, h + 0.25f, 0.44f), MeshKit.Wall(wall), MeshKit.Solid(roof));
                    break;
                }

                case BuildingKind.Mosque:
                {
                    var domeCol = variant % 2 == 0 ? new Color(0.3f, 0.55f, 0.6f) : new Color(0.85f, 0.82f, 0.75f);
                    k.Box(Vector3.zero, new Vector3(0.95f, 0.5f, 0.95f), MeshKit.Wall(wall), MeshKit.Solid(roof));
                    k.Prism(Vector3.up * 0.5f, 0.32f, 0.32f, 0.1f, 14, MeshKit.Wall(wall), false, true);
                    k.Dome(Vector3.up * 0.6f, 0.32f, 1.05f, 5, 14, MeshKit.Solid(domeCol));
                    k.Prism(new Vector3(0.55f, 0, 0.55f), 0.06f, 0.05f, 1.35f, 8, MeshKit.Solid(wall), false, true);
                    k.Prism(new Vector3(0.55f, 1.35f, 0.55f), 0.06f, 0f, 0.25f, 8, MeshKit.Solid(domeCol));
                    break;
                }

                case BuildingKind.Warehouse:
                    k.Box(Vector3.zero, new Vector3(1.6f, 0.5f, 0.8f), MeshKit.Wall(Brick), MeshKit.Solid(Slate));
                    k.GableRoof(Vector3.up * 0.5f, 1.6f, 0.8f, 0.18f, 0.03f, MeshKit.Solid(Slate), MeshKit.Wall(Brick));
                    break;

                case BuildingKind.Crane:
                {
                    var steel = MeshKit.Solid(new Color(0.55f, 0.35f, 0.2f));
                    float[] sx = { -0.12f, 0.12f, 0.12f, -0.12f }, sz = { -0.12f, -0.12f, 0.12f, 0.12f };
                    for (int i = 0; i < 4; i++) k.Beam(new Vector3(sx[i], 0, sz[i]), new Vector3(sx[i] * 0.5f, 1.4f, sz[i] * 0.5f), 0.04f, steel);
                    k.Box(Vector3.up * 1.4f, new Vector3(0.22f, 0.15f, 0.22f), steel, steel);
                    k.Beam(new Vector3(-0.3f, 1.55f, 0), new Vector3(1.3f, 1.62f, 0), 0.06f, steel);
                    k.Beam(new Vector3(0, 1.9f, 0), new Vector3(1.2f, 1.62f, 0), 0.02f, steel);
                    k.Beam(new Vector3(0, 1.55f, 0), new Vector3(0, 1.9f, 0), 0.04f, steel);
                    break;
                }

                case BuildingKind.Pier:
                    k.Box(new Vector3(0, -0.05f, 0), new Vector3(0.28f, 0.1f, 2.2f), MeshKit.Solid(new Color(0.36f, 0.3f, 0.24f)), MeshKit.Solid(new Color(0.45f, 0.38f, 0.3f)));
                    break;

                case BuildingKind.Ship:
                {
                    // Vapor mercante: casco con proa, superestructura y chimenea.
                    var hull = MeshKit.Solid(variant % 2 == 0 ? new Color(0.15f, 0.15f, 0.17f) : new Color(0.3f, 0.12f, 0.1f));
                    k.Box(new Vector3(0, -0.06f, -0.1f), new Vector3(0.32f, 0.2f, 1.2f), hull, MeshKit.Solid(new Color(0.5f, 0.42f, 0.32f)));
                    k.Tri(new Vector3(-0.16f, -0.06f, 0.5f), new Vector3(0.16f, -0.06f, 0.5f), new Vector3(0, -0.06f, 0.85f), hull);
                    k.Tri(new Vector3(-0.16f, 0.14f, 0.5f), new Vector3(0.16f, 0.14f, 0.5f), new Vector3(0, 0.14f, 0.85f), MeshKit.Solid(new Color(0.5f, 0.42f, 0.32f)));
                    k.Quad(new Vector3(-0.16f, -0.06f, 0.5f), new Vector3(-0.16f, 0.14f, 0.5f), new Vector3(0, 0.14f, 0.85f), new Vector3(0, -0.06f, 0.85f), hull);
                    k.Quad(new Vector3(0.16f, -0.06f, 0.5f), new Vector3(0.16f, 0.14f, 0.5f), new Vector3(0, 0.14f, 0.85f), new Vector3(0, -0.06f, 0.85f), hull);
                    k.Box(new Vector3(0, 0.14f, -0.25f), new Vector3(0.24f, 0.16f, 0.35f), MeshKit.Wall(new Color(0.9f, 0.88f, 0.82f)), MeshKit.Solid(Grey));
                    k.Prism(new Vector3(0, 0.3f, -0.22f), 0.05f, 0.05f, 0.22f, 8, MeshKit.Solid(variant % 3 == 0 ? new Color(0.7f, 0.15f, 0.12f) : new Color(0.12f, 0.12f, 0.12f)), true, true);
                    k.Beam(new Vector3(0, 0.14f, 0.3f), new Vector3(0, 0.55f, 0.3f), 0.015f, MeshKit.Solid(Metal));
                    break;
                }

                default:
                    if (BuildNational(k, kind, variant, style, rng, wall, roof)) break;
                    return Building(kind, variant);
            }
            return k.ToMesh($"Edificio_{kind}_{style}_{variant}");
        });

        // ================================================================== Monumentos

        /// <summary>Monumento característico de una ciudad (o null). scale = tamaño en el mundo por unidad local.</summary>
        public static bool TryGetLandmark(string city, out Mesh mesh, out float scale, out Vector2 offset)
        {
            mesh = null;
            scale = 0.026f;
            offset = Vector2.zero;
            if (string.IsNullOrEmpty(city)) return false;
            // Se aceptan el nombre español (el de los datos para las capitales) y el inglés/local.
            string key = city.Trim();
            Action<MeshKit> build;
            switch (key)
            {
                case "París": case "Paris": key = "Paris"; build = Eiffel; break;
                case "Londres": case "London": key = "London"; build = BigBen; break;
                case "Moscú": case "Moscow": key = "Moscow"; build = Kremlin; scale = 0.022f; break;
                case "Berlín": case "Berlin": key = "Berlin"; build = BrandenburgGate; break;
                case "Roma": case "Rome": key = "Rome"; build = Colosseum; break;
                case "Nueva York": case "New York": key = "NewYork"; build = EmpireState; break;
                case "Washington D.C.": case "Washington,  D.C.": case "Washington, D.C.": key = "Washington"; build = Capitol; break;
                case "Tokio": case "Tokyo": key = "Tokyo"; build = Pagoda; break;
                case "Madrid": build = RoyalPalace; scale = 0.022f; break;
                case "El Cairo": case "Cairo": key = "Cairo"; build = Pyramids; scale = 0.022f; offset = new Vector2(-0.045f, -0.022f); break;
                case "Pekín": case "Beijing": key = "Beijing"; build = ForbiddenCity; scale = 0.022f; break;
                case "Estambul": case "Istanbul": key = "Istanbul"; build = HagiaSophia; break;
                case "Atenas": case "Athens": key = "Athens"; build = Parthenon; break;
                case "Río de Janeiro": case "Rio de Janeiro": key = "Rio"; build = ChristRedeemer; offset = new Vector2(-0.022f, 0.009f); break;
                case "Sídney": case "Sydney": key = "Sydney"; build = HarbourBridge; break;
                case "Nueva Delhi": case "Delhi": key = "Delhi"; build = IndiaGate; break;
                case "Budapest": build = Parliament; break;
                case "Viena": case "Vienna": key = "Vienna"; build = Cathedral; break;
                // Monumentos de las potencias regionales (todos anteriores a 1936).
                case "Bombay": case "Mumbai": key = "Bombay"; build = GatewayOfIndia; break;
                case "Calcuta": case "Kolkata": key = "Calcutta"; build = VictoriaMemorial; scale = 0.022f; break;
                case "Pretoria": build = UnionBuildings; scale = 0.022f; break;
                case "Ciudad de México": case "Mexico City": key = "Mexico"; build = BellasArtes; break;
                case "Buenos Aires": build = CongresoArgentina; break;
                case "Shanghái": case "Shanghai": key = "Shanghai"; build = CustomsHouse; scale = 0.022f; break;
                case "Nankín": case "Nanjing": key = "Nanjing"; build = SunYatSenMausoleum; scale = 0.022f; break;
                case "Estocolmo": case "Stockholm": key = "Stockholm"; build = StockholmCityHall; break;
                case "Lisboa": case "Lisbon": key = "Lisbon"; build = BelemTower; break;
                default: return false;
            }
            float s = scale;
            mesh = Cached("landmark_" + key, () =>
            {
                var k = new MeshKit();
                build(k);
                return k.ToMesh("Monumento_" + key);
            });
            scale = s;
            return true;
        }

        private static void Eiffel(MeshKit k)
        {
            var c = MeshKit.Solid(Metal);
            float[] sx = { -1, 1, 1, -1 }, sz = { -1, -1, 1, 1 };
            for (int i = 0; i < 4; i++)
            {
                k.Beam(new Vector3(sx[i] * 0.62f, 0, sz[i] * 0.62f), new Vector3(sx[i] * 0.2f, 1.1f, sz[i] * 0.2f), 0.09f, c);
                k.Beam(new Vector3(sx[i] * 0.2f, 1.15f, sz[i] * 0.2f), new Vector3(sx[i] * 0.09f, 2.05f, sz[i] * 0.09f), 0.06f, c);
                int j = (i + 1) % 4;
                k.Beam(new Vector3(sx[i] * 0.5f, 0.38f, sz[i] * 0.5f), new Vector3(sx[j] * 0.5f, 0.38f, sz[j] * 0.5f), 0.05f, c); // arcos
            }
            k.Box(Vector3.up * 1.08f, new Vector3(0.55f, 0.08f, 0.55f), c, c);
            k.Box(Vector3.up * 2.03f, new Vector3(0.26f, 0.06f, 0.26f), c, c);
            k.Prism(Vector3.up * 2.08f, 0.1f, 0.025f, 1.15f, 4, c, true, false, null, Mathf.PI / 4);
            k.Beam(Vector3.up * 3.2f, Vector3.up * 3.55f, 0.025f, c);
        }

        private static void BigBen(MeshKit k)
        {
            var sand = MeshKit.Wall(new Color(0.82f, 0.72f, 0.5f));
            k.Box(Vector3.zero, new Vector3(0.36f, 2.0f, 0.36f), sand, sand);
            k.Box(Vector3.up * 2.0f, new Vector3(0.44f, 0.42f, 0.44f), MeshKit.Solid(new Color(0.9f, 0.86f, 0.7f)), sand);
            k.Box(Vector3.up * 2.42f, new Vector3(0.36f, 0.22f, 0.36f), sand, sand);
            k.Pyramid(Vector3.up * 2.64f, 0.36f, 0.36f, 0.75f, MeshKit.Solid(Slate));
            // Palacio de Westminster
            k.Box(new Vector3(1.3f, 0, 0), new Vector3(2.2f, 0.55f, 0.55f), sand, MeshKit.Solid(Slate));
            k.GableRoof(new Vector3(1.3f, 0.55f, 0), 2.2f, 0.55f, 0.18f, 0f, MeshKit.Solid(Slate), sand);
            for (int i = 0; i < 4; i++) k.Prism(new Vector3(0.45f + i * 0.6f, 0.55f, 0.24f), 0.04f, 0f, 0.35f, 4, MeshKit.Solid(Slate));
            k.Box(new Vector3(2.45f, 0, 0), new Vector3(0.45f, 1.5f, 0.45f), sand, MeshKit.Solid(Slate));
        }

        private static void Kremlin(MeshKit k)
        {
            var red = MeshKit.Solid(new Color(0.62f, 0.2f, 0.16f));
            var green = MeshKit.Solid(new Color(0.18f, 0.42f, 0.3f));
            // Muralla y torres
            k.Box(new Vector3(0, 0, -1.4f), new Vector3(2.9f, 0.3f, 0.12f), red, red);
            k.Box(new Vector3(0, 0, 1.4f), new Vector3(2.9f, 0.3f, 0.12f), red, red);
            k.Box(new Vector3(-1.4f, 0, 0), new Vector3(0.12f, 0.3f, 2.9f), red, red);
            k.Box(new Vector3(1.4f, 0, 0), new Vector3(0.12f, 0.3f, 2.9f), red, red);
            foreach (var p in new[] { new Vector3(-1.4f, 0, -1.4f), new Vector3(1.4f, 0, -1.4f), new Vector3(1.4f, 0, 1.4f), new Vector3(-1.4f, 0, 1.4f), new Vector3(0, 0, -1.4f) })
            {
                k.Box(p, new Vector3(0.24f, 0.6f, 0.24f), red, red);
                k.Pyramid(p + Vector3.up * 0.6f, 0.24f, 0.24f, 0.4f, green);
            }
            // Catedral de San Basilio (fuera de la muralla)
            var basil = new Vector3(0, 0, -2.1f);
            k.Prism(basil, 0.2f, 0.2f, 0.9f, 8, MeshKit.Wall(new Color(0.75f, 0.35f, 0.25f)));
            k.Prism(basil + Vector3.up * 0.9f, 0.2f, 0.02f, 0.6f, 8, green);
            Color[] domes = { new Color(0.85f, 0.2f, 0.2f), new Color(0.2f, 0.55f, 0.3f), new Color(0.25f, 0.45f, 0.8f), Gold };
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2 + Mathf.PI / 4;
                var p = basil + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.38f;
                k.Prism(p, 0.12f, 0.12f, 0.55f, 8, MeshKit.Wall(new Color(0.8f, 0.7f, 0.55f)));
                k.OnionDome(p + Vector3.up * 0.55f, 0.13f, 0.38f, MeshKit.Solid(domes[i]));
            }
            k.Box(new Vector3(0.5f, 0, 0.2f), new Vector3(1.2f, 0.5f, 0.7f), MeshKit.Wall(new Color(0.9f, 0.82f, 0.62f)), green);
        }

        private static void BrandenburgGate(MeshKit k)
        {
            var s = MeshKit.Solid(Stone);
            k.Box(Vector3.zero, new Vector3(1.5f, 0.08f, 0.4f), s, s);
            for (int i = 0; i < 6; i++)
            {
                k.Prism(new Vector3(-0.65f + i * 0.26f, 0.08f, 0.12f), 0.05f, 0.045f, 0.55f, 8, s, false, true);
                k.Prism(new Vector3(-0.65f + i * 0.26f, 0.08f, -0.12f), 0.05f, 0.045f, 0.55f, 8, s, false, true);
            }
            k.Box(Vector3.up * 0.63f, new Vector3(1.5f, 0.24f, 0.42f), s, s);
            k.Box(Vector3.up * 0.87f, new Vector3(0.45f, 0.12f, 0.2f), MeshKit.Solid(Copper), MeshKit.Solid(Copper)); // cuadriga
            // Reichstag
            k.Box(new Vector3(0.2f, 0, -1.4f), new Vector3(1.7f, 0.6f, 1f), MeshKit.Wall(Stone), MeshKit.Solid(Grey));
            foreach (var x in new[] { -0.6f, 1f }) k.Box(new Vector3(x, 0, -1.4f), new Vector3(0.3f, 0.8f, 1.02f), MeshKit.Wall(Stone), MeshKit.Solid(Grey));
        }

        private static void Colosseum(MeshKit k)
        {
            var stone = MeshKit.Wall(new Color(0.78f, 0.66f, 0.5f));
            const int seg = 28;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                // Un lado más bajo (en ruinas).
                float h0 = Mathf.Lerp(0.55f, 0.32f, Mathf.Clamp01(Mathf.Sin(a0) * 1.5f));
                float h1 = Mathf.Lerp(0.55f, 0.32f, Mathf.Clamp01(Mathf.Sin(a1) * 1.5f));
                Vector3 o0 = new Vector3(Mathf.Cos(a0) * 1f, 0, Mathf.Sin(a0) * 0.8f), o1 = new Vector3(Mathf.Cos(a1) * 1f, 0, Mathf.Sin(a1) * 0.8f);
                Vector3 i0 = new Vector3(Mathf.Cos(a0) * 0.62f, 0, Mathf.Sin(a0) * 0.45f), i1 = new Vector3(Mathf.Cos(a1) * 0.62f, 0, Mathf.Sin(a1) * 0.45f);
                k.Quad(o0, o0 + Vector3.up * h0, o1 + Vector3.up * h1, o1, stone);   // muro exterior (con "ventanas" = arcos)
                k.Quad(i0, i0 + Vector3.up * h0 * 0.8f, i1 + Vector3.up * h1 * 0.8f, i1, MeshKit.Solid(new Color(0.6f, 0.5f, 0.38f)));
                k.Quad(o0 + Vector3.up * h0, i0 + Vector3.up * h0 * 0.8f, i1 + Vector3.up * h1 * 0.8f, o1 + Vector3.up * h1, MeshKit.Solid(new Color(0.7f, 0.6f, 0.45f)));
            }
        }

        private static void EmpireState(MeshKit k)
        {
            var s = MeshKit.Wall(new Color(0.78f, 0.76f, 0.7f));
            k.Box(Vector3.zero, new Vector3(1.3f, 0.6f, 0.95f), s, MeshKit.Solid(Grey));
            k.Box(Vector3.up * 0.6f, new Vector3(0.85f, 3.4f, 0.62f), s, MeshKit.Solid(Grey));
            k.Box(Vector3.up * 4.0f, new Vector3(0.58f, 0.5f, 0.44f), s, MeshKit.Solid(Grey));
            k.Box(Vector3.up * 4.5f, new Vector3(0.36f, 0.4f, 0.3f), s, MeshKit.Solid(Grey));
            k.Prism(Vector3.up * 4.9f, 0.1f, 0.02f, 1.0f, 8, MeshKit.Solid(Metal));
            // Rascacielos vecinos
            k.Box(new Vector3(1.3f, 0, 0.6f), new Vector3(0.6f, 2.4f, 0.6f), s, MeshKit.Solid(Grey));
            k.Box(new Vector3(-1.2f, 0, -0.5f), new Vector3(0.55f, 2.9f, 0.55f), MeshKit.Wall(Stone), MeshKit.Solid(Grey));
            k.Pyramid(new Vector3(-1.2f, 2.9f, -0.5f), 0.55f, 0.55f, 0.5f, MeshKit.Solid(Copper));
        }

        private static void Capitol(MeshKit k)
        {
            var w = MeshKit.Wall(Marble);
            k.Box(Vector3.zero, new Vector3(2.4f, 0.45f, 0.6f), w, MeshKit.Solid(Grey));
            k.Box(Vector3.zero, new Vector3(0.75f, 0.55f, 0.85f), w, MeshKit.Solid(Grey));
            k.Prism(Vector3.up * 0.55f, 0.3f, 0.28f, 0.32f, 16, w, false, true);
            k.Dome(Vector3.up * 0.87f, 0.28f, 1.25f, 6, 16, MeshKit.Solid(Marble));
            k.Prism(Vector3.up * 1.2f, 0.05f, 0.04f, 0.2f, 8, MeshKit.Solid(Marble), true, true);
        }

        private static void Pagoda(MeshKit k)
        {
            var wall = MeshKit.Wall(new Color(0.65f, 0.2f, 0.15f));
            var roof = MeshKit.Solid(new Color(0.2f, 0.22f, 0.24f));
            float y = 0;
            for (int i = 0; i < 5; i++)
            {
                float w = 0.62f - i * 0.08f;
                k.Box(Vector3.up * y, new Vector3(w, 0.3f, w), wall, wall);
                k.HipRoof(Vector3.up * (y + 0.3f), w, w, 0.14f, 0.2f, roof);
                y += 0.42f;
            }
            k.Prism(Vector3.up * y, 0.03f, 0.01f, 0.6f, 6, MeshKit.Solid(Gold));
        }

        private static void RoyalPalace(MeshKit k)
        {
            var w = MeshKit.Wall(new Color(0.9f, 0.86f, 0.76f));
            var r = MeshKit.Solid(Slate);
            k.Box(new Vector3(0, 0, -0.8f), new Vector3(1.9f, 0.55f, 0.35f), w, r);
            k.Box(new Vector3(0, 0, 0.8f), new Vector3(1.9f, 0.55f, 0.35f), w, r);
            k.Box(new Vector3(-0.8f, 0, 0), new Vector3(0.35f, 0.55f, 1.25f), w, r);
            k.Box(new Vector3(0.8f, 0, 0), new Vector3(0.35f, 0.55f, 1.25f), w, r);
            k.Box(new Vector3(0, 0.55f, -0.8f), new Vector3(0.5f, 0.2f, 0.37f), w, r);
        }

        private static void Pyramids(MeshKit k)
        {
            var sand = MeshKit.Solid(new Color(0.85f, 0.74f, 0.52f));
            k.Pyramid(Vector3.zero, 1.6f, 1.6f, 1.05f, sand);
            k.Pyramid(new Vector3(-1.6f, 0, -1.3f), 1.4f, 1.4f, 0.95f, sand);
            k.Pyramid(new Vector3(-2.7f, 0, -2.4f), 0.75f, 0.75f, 0.5f, sand);
        }

        private static void ForbiddenCity(MeshKit k)
        {
            var red = MeshKit.Wall(new Color(0.62f, 0.16f, 0.12f));
            var gold = MeshKit.Solid(new Color(0.86f, 0.65f, 0.2f));
            k.Box(new Vector3(0, 0, -1.2f), new Vector3(2.1f, 0.22f, 0.1f), red, red);
            k.Box(new Vector3(0, 0, 1.2f), new Vector3(2.1f, 0.22f, 0.1f), red, red);
            k.Box(new Vector3(-1.05f, 0, 0), new Vector3(0.1f, 0.22f, 2.5f), red, red);
            k.Box(new Vector3(1.05f, 0, 0), new Vector3(0.1f, 0.22f, 2.5f), red, red);
            k.Box(new Vector3(0, 0, 0.1f), new Vector3(0.95f, 0.32f, 0.5f), red, red);
            k.HipRoof(new Vector3(0, 0.32f, 0.1f), 0.95f, 0.5f, 0.22f, 0.12f, gold);
            k.Box(new Vector3(0, 0, -0.75f), new Vector3(0.6f, 0.26f, 0.3f), red, red);
            k.HipRoof(new Vector3(0, 0.26f, -0.75f), 0.6f, 0.3f, 0.16f, 0.1f, gold);
        }

        private static void HagiaSophia(MeshKit k)
        {
            var w = MeshKit.Wall(new Color(0.85f, 0.7f, 0.55f));
            k.Box(Vector3.zero, new Vector3(1.2f, 0.5f, 1.2f), w, MeshKit.Solid(Grey));
            k.Prism(Vector3.up * 0.5f, 0.5f, 0.5f, 0.12f, 16, w, false, true);
            k.Dome(Vector3.up * 0.62f, 0.5f, 0.7f, 6, 16, MeshKit.Solid(new Color(0.5f, 0.52f, 0.55f)));
            foreach (var p in new[] { new Vector3(-0.75f, 0, -0.75f), new Vector3(0.75f, 0, -0.75f), new Vector3(0.75f, 0, 0.75f), new Vector3(-0.75f, 0, 0.75f) })
            {
                k.Prism(p, 0.055f, 0.045f, 1.35f, 8, MeshKit.Solid(new Color(0.88f, 0.82f, 0.7f)), false, true);
                k.Prism(p + Vector3.up * 1.35f, 0.05f, 0f, 0.3f, 8, MeshKit.Solid(Slate));
            }
        }

        private static void Parthenon(MeshKit k)
        {
            var m = MeshKit.Solid(Marble);
            k.Box(Vector3.zero, new Vector3(1.5f, 0.1f, 0.75f), m, m);
            for (int i = 0; i < 8; i++)
            {
                float x = -0.66f + i * 0.19f;
                k.Prism(new Vector3(x, 0.1f, 0.3f), 0.045f, 0.04f, 0.45f, 8, m, false, true);
                k.Prism(new Vector3(x, 0.1f, -0.3f), 0.045f, 0.04f, 0.45f, 8, m, false, true);
            }
            k.Box(new Vector3(0, 0.1f, 0), new Vector3(1.1f, 0.4f, 0.4f), MeshKit.Solid(new Color(0.85f, 0.82f, 0.75f)), m);
            k.Box(Vector3.up * 0.55f, new Vector3(1.5f, 0.12f, 0.75f), m, m);
            k.GableRoof(Vector3.up * 0.67f, 1.5f, 0.75f, 0.15f, 0.02f, m, m);
        }

        private static void ChristRedeemer(MeshKit k)
        {
            var s = MeshKit.Solid(new Color(0.88f, 0.86f, 0.8f));
            k.Box(Vector3.zero, new Vector3(0.3f, 0.3f, 0.3f), s, s);
            k.Prism(Vector3.up * 0.3f, 0.1f, 0.07f, 0.9f, 8, s, true, true);
            k.Box(Vector3.up * 1.0f, new Vector3(0.95f, 0.09f, 0.09f), s, s);
            k.Blob(Vector3.up * 1.27f, new Vector3(0.07f, 0.08f, 0.07f), 1, s, s, 0f, 1);
        }

        private static void HarbourBridge(MeshKit k)
        {
            var steel = MeshKit.Solid(new Color(0.42f, 0.44f, 0.46f));
            const int segs = 14;
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < segs; i++)
                {
                    float x0 = -1.6f + i * 3.2f / segs, x1 = x0 + 3.2f / segs;
                    float y0 = 0.2f + Mathf.Sin((i / (float)segs) * Mathf.PI) * 0.75f, y1 = 0.2f + Mathf.Sin(((i + 1) / (float)segs) * Mathf.PI) * 0.75f;
                    k.Beam(new Vector3(x0, y0, side * 0.12f), new Vector3(x1, y1, side * 0.12f), 0.06f, steel);
                    if (i % 2 == 0) k.Beam(new Vector3(x0, y0, side * 0.12f), new Vector3(x0, 0.42f, side * 0.12f), 0.025f, steel);
                }
            k.Box(Vector3.up * 0.4f, new Vector3(3.6f, 0.05f, 0.3f), steel, steel);
            foreach (var x in new[] { -1.75f, 1.75f }) k.Box(new Vector3(x, 0, 0), new Vector3(0.28f, 0.75f, 0.4f), MeshKit.Solid(Stone), MeshKit.Solid(Stone));
        }

        private static void IndiaGate(MeshKit k)
        {
            var s = MeshKit.Solid(new Color(0.8f, 0.6f, 0.45f));
            k.Box(new Vector3(-0.35f, 0, 0), new Vector3(0.26f, 0.9f, 0.32f), s, s);
            k.Box(new Vector3(0.35f, 0, 0), new Vector3(0.26f, 0.9f, 0.32f), s, s);
            k.Box(Vector3.up * 0.9f, new Vector3(0.98f, 0.3f, 0.34f), s, s);
            k.Box(Vector3.up * 1.2f, new Vector3(0.5f, 0.12f, 0.24f), s, s);
            k.Dome(Vector3.up * 1.32f, 0.1f, 0.8f, 4, 10, s);
        }

        private static void Parliament(MeshKit k)
        {
            var w = MeshKit.Wall(new Color(0.88f, 0.84f, 0.75f));
            var roof = MeshKit.Solid(new Color(0.65f, 0.3f, 0.22f));
            k.Box(Vector3.zero, new Vector3(2.5f, 0.45f, 0.6f), w, roof);
            k.GableRoof(Vector3.up * 0.45f, 2.5f, 0.6f, 0.2f, 0f, roof, w);
            k.Prism(Vector3.up * 0.45f, 0.3f, 0.3f, 0.3f, 16, w, false, true);
            k.Dome(Vector3.up * 0.75f, 0.3f, 1.4f, 6, 16, roof);
            k.Prism(Vector3.up * 1.17f, 0.04f, 0f, 0.3f, 8, roof);
            for (int i = 0; i < 6; i++) k.Prism(new Vector3(-1.15f + i * 0.46f, 0.45f, 0.28f), 0.035f, 0f, 0.35f, 6, roof);
        }

        private static void Cathedral(MeshKit k)
        {
            var s = MeshKit.Wall(new Color(0.75f, 0.7f, 0.6f));
            var roof = MeshKit.Solid(new Color(0.25f, 0.38f, 0.3f));
            k.Box(Vector3.zero, new Vector3(0.65f, 0.65f, 1.6f), s, roof);
            k.GableRoof(Vector3.up * 0.65f, 0.65f, 1.6f, 0.6f, 0.03f, roof, s);
            k.Box(new Vector3(0, 0, -0.95f), new Vector3(0.32f, 1.3f, 0.32f), s, s);
            k.Pyramid(new Vector3(0, 1.3f, -0.95f), 0.32f, 0.32f, 1.2f, MeshKit.Solid(new Color(0.6f, 0.56f, 0.48f)));
        }
    }
}
