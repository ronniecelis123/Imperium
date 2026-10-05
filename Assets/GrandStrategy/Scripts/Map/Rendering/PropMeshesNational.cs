using System.Collections.Generic;
using UnityEngine;

namespace GrandStrategy.Map.Rendering
{
    /// <summary>
    /// Arquitectura nacional de los años 30 para las ciudades de las potencias y de los actores principales
    /// (ver <see cref="ArchStyle"/>). Cada edificio es una silueta reconocible a escala de mapa, no un modelo
    /// detallado: lo que identifica un estilo es la combinación de forma del tejado, materiales y color.
    /// <list type="bullet">
    /// <item>Británico: hileras de casas de ladrillo victorianas con chimeneas y miradores; iglesias de torre almenada.</item>
    /// <item>Francés: casas de piedra caliza con mansarda de zinc (y manzanas haussmannianas); catedral gótica de dos torres.</item>
    /// <item>Germánico: casas de entramado de madera con tejado muy inclinado; iglesia gótica de ladrillo con aguja.</item>
    /// <item>Países Bajos: casas de canal estrechas con frontón escalonado.</item>
    /// <item>Mediterráneo: casas ocres con teja árabe; campanario; iglesia de cúpula renacentista.</item>
    /// <item>Balcánico: iglesia bizantina de cúpula; casas otomanas con piso volado.</item>
    /// <item>Ruso: isba de troncos; iglesia de cúpulas de cebolla; bloque constructivista.</item>
    /// <item>Nórdico: casas de madera rojo falun y ocre; iglesia blanca con aguja negra.</item>
    /// <item>Japonés: machiya de madera oscura y teja gris; torii; pagoda.</item>
    /// <item>Chino: casa de patio (siheyuan) de ladrillo gris; templo rojo de tejado vidriado; pagoda octogonal.</item>
    /// <item>Colonial ibérico: casas encaladas o de colores con teja; iglesia barroca de dos torres.</item>
    /// <item>Colonial holandés del Cabo: casa blanca con techo de paja y frontón curvo central.</item>
    /// <item>Otomano: mezquita de cúpula central con minaretes de lápiz; casa de madera con piso volado.</item>
    /// <item>Himalayo: casa blanca de azotea con franja granate; estupa.</item>
    /// <item>Raj: templo hindú (shikhara) y edificio indosarracénico (se suman a mezquitas y azoteas).</item>
    /// <item>Estadounidense: rascacielos art déco escalonado; casa de madera con porche.</item>
    /// </list>
    /// </summary>
    public static partial class PropMeshes
    {
        // Materiales de época.
        private static readonly Color Timber = new Color(0.27f, 0.18f, 0.12f);
        private static readonly Color RedTile = new Color(0.56f, 0.23f, 0.16f);
        private static readonly Color RomanTile = new Color(0.72f, 0.38f, 0.24f);
        private static readonly Color Zinc = new Color(0.46f, 0.5f, 0.55f);
        private static readonly Color DarkBrick = new Color(0.48f, 0.24f, 0.18f);
        private static readonly Color LondonBrick = new Color(0.72f, 0.6f, 0.42f);
        private static readonly Color Whitewash = new Color(0.94f, 0.92f, 0.87f);
        private static readonly Color Limestone = new Color(0.88f, 0.83f, 0.7f);
        private static readonly Color Log = new Color(0.43f, 0.3f, 0.19f);
        private static readonly Color FaluRed = new Color(0.56f, 0.16f, 0.12f);
        private static readonly Color Ochre = new Color(0.85f, 0.66f, 0.36f);
        private static readonly Color LacquerRed = new Color(0.64f, 0.17f, 0.12f);
        private static readonly Color AsianTile = new Color(0.22f, 0.23f, 0.26f);
        private static readonly Color GreyBrick = new Color(0.56f, 0.56f, 0.55f);
        private static readonly Color Thatch = new Color(0.45f, 0.37f, 0.23f);
        private static readonly Color Lead = new Color(0.5f, 0.53f, 0.56f);
        private static readonly Color Sandstone = new Color(0.78f, 0.52f, 0.38f);
        private static readonly Color Maroon = new Color(0.45f, 0.15f, 0.11f);
        private static readonly Color CopperGreen = new Color(0.33f, 0.56f, 0.48f);

        /// <summary>Colores de pared y tejado de los estilos nacionales (para los arquetipos compartidos).</summary>
        private static bool NationalPalette(ArchStyle style, int variant, out Color wall, out Color roof)
        {
            switch (style)
            {
                case ArchStyle.British:
                    wall = variant % 3 == 2 ? LondonBrick : Brick;
                    roof = Slate;
                    return true;
                case ArchStyle.French:
                    wall = variant % 3 == 2 ? Plaster : Limestone;
                    roof = Zinc;
                    return true;
                case ArchStyle.Germanic:
                    wall = variant % 3 == 0 ? Plaster : variant % 3 == 1 ? new Color(0.9f, 0.84f, 0.66f) : DarkBrick;
                    roof = variant % 2 == 0 ? RedTile : Slate;
                    return true;
                case ArchStyle.LowCountries:
                    wall = variant % 2 == 0 ? DarkBrick : Brick;
                    roof = Slate;
                    return true;
                case ArchStyle.Mediterranean:
                case ArchStyle.Balkan:
                    Color[] med = { Ochre, new Color(0.9f, 0.8f, 0.56f), new Color(0.84f, 0.52f, 0.4f), Whitewash };
                    wall = med[variant % med.Length];
                    roof = RomanTile;
                    return true;
                case ArchStyle.Russian:
                    wall = variant % 2 == 0 ? new Color(0.84f, 0.78f, 0.64f) : new Color(0.72f, 0.66f, 0.58f);
                    roof = new Color(0.34f, 0.42f, 0.38f);
                    return true;
                case ArchStyle.Nordic:
                    Color[] nordic = { FaluRed, Ochre, Whitewash, FaluRed };
                    wall = nordic[variant % nordic.Length];
                    roof = variant % 2 == 0 ? new Color(0.2f, 0.2f, 0.22f) : RedTile;
                    return true;
                case ArchStyle.Japanese:
                    wall = variant % 2 == 0 ? new Color(0.36f, 0.26f, 0.18f) : new Color(0.88f, 0.86f, 0.8f);
                    roof = AsianTile;
                    return true;
                case ArchStyle.Chinese:
                    wall = GreyBrick;
                    roof = new Color(0.3f, 0.31f, 0.33f);
                    return true;
                case ArchStyle.LatinColonial:
                    Color[] latin = { Whitewash, new Color(0.92f, 0.82f, 0.55f), new Color(0.62f, 0.76f, 0.82f), new Color(0.9f, 0.7f, 0.64f), new Color(0.72f, 0.82f, 0.64f) };
                    wall = latin[variant % latin.Length];
                    roof = RomanTile;
                    return true;
                case ArchStyle.CapeDutch:
                    wall = variant % 3 == 2 ? Brick : Whitewash;
                    roof = variant % 3 == 2 ? new Color(0.55f, 0.57f, 0.58f) : Thatch; // ladrillo eduardiano con chapa / paja
                    return true;
                case ArchStyle.Ottoman:
                    Color[] ott = { new Color(0.9f, 0.85f, 0.75f), new Color(0.8f, 0.6f, 0.45f), new Color(0.62f, 0.28f, 0.22f), new Color(0.86f, 0.78f, 0.6f) };
                    wall = ott[variant % ott.Length];
                    roof = RomanTile;
                    return true;
                case ArchStyle.Himalayan:
                    wall = Whitewash;
                    roof = Maroon;
                    return true;
            }
            wall = roof = default;
            return false;
        }

        /// <summary>Tejado de las manzanas con patio según el estilo (zinc en París, teja en Roma y Buenos Aires...).</summary>
        private static Color BlockRoof(ArchStyle style, int variant)
        {
            switch (style)
            {
                case ArchStyle.French: return Zinc;
                case ArchStyle.Mediterranean: case ArchStyle.LatinColonial: case ArchStyle.Balkan: return RomanTile;
                case ArchStyle.Germanic: return variant % 2 == 0 ? RedTile : Slate;
                case ArchStyle.Russian: return new Color(0.34f, 0.42f, 0.38f);
                case ArchStyle.Nordic: return variant % 2 == 0 ? CopperGreen : RedTile;
                default: return variant % 2 == 0 ? Slate : new Color(0.28f, 0.42f, 0.4f);
            }
        }

        /// <summary>Construye un arquetipo nacional; false si el tipo no es nacional.</summary>
        private static bool BuildNational(MeshKit k, BuildingKind kind, int variant, ArchStyle style, System.Random rng, Color wall, Color roof)
        {
            switch (kind)
            {
                case BuildingKind.HalfTimber: HalfTimberHouse(k, variant, wall, roof); return true;
                case BuildingKind.GothicChurch: GothicBrickChurch(k, variant); return true;
                case BuildingKind.BrickTerrace: BrickTerraceRow(k, variant, rng); return true;
                case BuildingKind.TowerChurch: NormanChurch(k); return true;
                case BuildingKind.MedHouse: MediterraneanHouse(k, variant, wall, rng); return true;
                case BuildingKind.Campanile: CampanileTower(k, variant, wall); return true;
                case BuildingKind.DomedChurch: RenaissanceChurch(k, variant); return true;
                case BuildingKind.Izba: IzbaHouse(k, variant); return true;
                case BuildingKind.OnionChurch: OrthodoxOnionChurch(k, variant); return true;
                case BuildingKind.SovietBlock: ConstructivistBlock(k, variant, wall, rng); return true;
                case BuildingKind.Machiya: MachiyaHouse(k, variant); return true;
                case BuildingKind.Torii: ToriiGate(k); return true;
                case BuildingKind.Siheyuan: SiheyuanCourtyard(k); return true;
                case BuildingKind.ChineseTemple: ChineseTempleHall(k, variant); return true;
                case BuildingKind.BrickPagoda: OctagonalPagoda(k); return true;
                case BuildingKind.ColonialHouse: ColonialHouseLatin(k, variant, wall); return true;
                case BuildingKind.BaroqueChurch: BaroqueTwinTowerChurch(k, variant); return true;
                case BuildingKind.CapeDutch: CapeDutchHouse(k, variant); return true;
                case BuildingKind.OttomanHouse: OttomanTimberHouse(k, wall); return true;
                case BuildingKind.OttomanMosque: OttomanImperialMosque(k, variant); return true;
                case BuildingKind.NordicHouse: NordicWoodHouse(k, variant, wall, roof); return true;
                case BuildingKind.NordicChurch: NordicWhiteChurch(k, style); return true;
                case BuildingKind.DecoTower: ArtDecoTower(k, variant, rng); return true;
                case BuildingKind.Clapboard: ClapboardHouse(k, variant); return true;
                case BuildingKind.FrenchHouse: FrenchMansardHouse(k, variant, wall); return true;
                case BuildingKind.FrenchCathedral: GothicTwinTowerCathedral(k); return true;
                case BuildingKind.DutchGable: CanalHouses(k, variant, rng); return true;
                case BuildingKind.HinduTemple: ShikharaTemple(k); return true;
                case BuildingKind.IndoSaracenic: IndoSaracenicHall(k, variant); return true;
                case BuildingKind.Stupa: Chorten(k); return true;
                case BuildingKind.HimalayanHouse: HimalayanFlatHouse(k, variant, rng); return true;
                case BuildingKind.ByzantineChurch: ByzantineDomeChurch(k, variant); return true;
                // Arquetipos compartidos (casa, adosados, bloque) con los colores y tejados del estilo nacional.
                case BuildingKind.House:
                case BuildingKind.Townhouses:
                case BuildingKind.Apartment:
                    if (!NationalPalette(style, variant, out _, out _)) return false;
                    NationalCommon(k, kind, variant, style, rng, wall, roof);
                    return true;
            }
            return false;
        }

        /// <summary>Casa, adosados y bloque de viviendas con los materiales y el tejado típico de cada estilo.</summary>
        private static void NationalCommon(MeshKit k, BuildingKind kind, int variant, ArchStyle style, System.Random rng, Color wall, Color roof)
        {
            bool lowRoof = style == ArchStyle.Mediterranean || style == ArchStyle.LatinColonial || style == ArchStyle.Balkan || style == ArchStyle.Ottoman;
            bool flat = style == ArchStyle.Himalayan;
            void Roof(Vector3 top, float w, float d, float ridge)
            {
                if (flat) return;
                if (lowRoof) k.HipRoof(top, w, d, ridge * 0.45f, 0.04f, MeshKit.Solid(roof));
                else if (style == ArchStyle.French) k.HipRoof(top, w, d, ridge * 0.8f, 0f, MeshKit.Solid(roof));
                else k.GableRoof(top, w, d, ridge, 0.04f, MeshKit.Solid(roof), MeshKit.Wall(wall));
            }
            switch (kind)
            {
                case BuildingKind.House:
                    k.Box(Vector3.zero, new Vector3(0.9f, 0.58f, 0.7f), MeshKit.Wall(wall), MeshKit.Solid(roof));
                    Roof(Vector3.up * 0.58f, 0.9f, 0.7f, 0.36f);
                    if (!lowRoof && !flat) k.Box(new Vector3(0.25f, 0.62f, 0.12f), new Vector3(0.1f, 0.36f, 0.1f), MeshKit.Solid(Brick), MeshKit.Solid(Slate));
                    break;
                case BuildingKind.Townhouses:
                    for (int i = 0; i < 3; i++)
                    {
                        float h = 0.75f + (float)rng.NextDouble() * 0.35f;
                        var c = new Vector3(-0.55f + i * 0.55f, 0, 0);
                        Color w = i % 2 == 0 ? wall : wall * 0.9f;
                        k.Box(c, new Vector3(0.55f, h, 0.85f), MeshKit.Wall(w), MeshKit.Solid(roof));
                        Roof(c + Vector3.up * h, 0.55f, 0.85f, 0.3f);
                    }
                    break;
                default: // Apartment
                    float ah = 1.3f + (float)rng.NextDouble() * 0.5f;
                    k.Box(Vector3.zero, new Vector3(1f, ah, 0.9f), MeshKit.Wall(wall), MeshKit.Solid(Grey));
                    k.Box(Vector3.up * ah, new Vector3(1.05f, 0.05f, 0.95f), MeshKit.Solid(Stone), MeshKit.Solid(Grey));
                    if (lowRoof || style == ArchStyle.French || style == ArchStyle.Germanic || style == ArchStyle.Nordic)
                        k.HipRoof(Vector3.up * (ah + 0.05f), 1f, 0.9f, style == ArchStyle.French ? 0.28f : 0.18f, 0f, MeshKit.Solid(roof));
                    break;
            }
        }

        // ================================================================== Europa occidental y central

        /// <summary>Casa de entramado (Fachwerk): planta baja revocada, piso volado con vigas oscuras y tejado empinado.</summary>
        private static void HalfTimberHouse(MeshKit k, int variant, Color wall, Color roof)
        {
            Color plaster = wall == DarkBrick ? Plaster : wall;
            var timber = MeshKit.Solid(Timber);
            k.Box(Vector3.zero, new Vector3(0.86f, 0.42f, 0.68f), MeshKit.Wall(plaster), MeshKit.Solid(roof));
            k.Box(Vector3.up * 0.42f, new Vector3(0.94f, 0.4f, 0.76f), MeshKit.Wall(plaster), MeshKit.Solid(roof)); // piso volado
            for (int side = -1; side <= 1; side += 2)
            {
                float z = side * 0.385f;
                for (int i = 0; i < 5; i++)
                    k.Box(new Vector3(-0.42f + i * 0.21f, 0.42f, z), new Vector3(0.035f, 0.4f, 0.012f), timber, timber);
                k.Box(new Vector3(0, 0.42f, z), new Vector3(0.95f, 0.035f, 0.014f), timber, timber);
                k.Box(new Vector3(0, 0.79f, z), new Vector3(0.95f, 0.035f, 0.014f), timber, timber);
                // Riostras en cruz de San Andrés en el centro.
                k.Beam(new Vector3(-0.1f, 0.46f, z), new Vector3(0.1f, 0.78f, z), 0.025f, timber);
                k.Beam(new Vector3(0.1f, 0.46f, z), new Vector3(-0.1f, 0.78f, z), 0.025f, timber);
            }
            k.GableRoof(Vector3.up * 0.82f, 0.94f, 0.76f, 0.62f, 0.05f, MeshKit.Solid(roof), MeshKit.Wall(plaster));
            if (variant % 2 == 1) k.Box(new Vector3(0, 1.0f, -0.3f), new Vector3(0.22f, 0.2f, 0.16f), MeshKit.Wall(plaster), MeshKit.Solid(roof)); // buhardilla
        }

        /// <summary>Iglesia gótica de ladrillo (gótico báltico/alemán): nave alta, contrafuertes y torre con aguja.</summary>
        private static void GothicBrickChurch(MeshKit k, int variant)
        {
            var brick = MeshKit.Wall(DarkBrick);
            var roofCol = MeshKit.Solid(variant % 2 == 0 ? Slate : RedTile);
            k.Box(Vector3.zero, new Vector3(0.62f, 0.85f, 1.55f), brick, roofCol);
            k.GableRoof(Vector3.up * 0.85f, 0.62f, 1.55f, 0.55f, 0.02f, roofCol, brick);
            for (int i = 0; i < 4; i++)
                foreach (float x in new[] { -0.35f, 0.35f })
                    k.Box(new Vector3(x, 0, -0.5f + i * 0.36f), new Vector3(0.08f, 0.62f, 0.1f), MeshKit.Solid(DarkBrick), MeshKit.Solid(DarkBrick));
            k.Box(new Vector3(0, 0, -0.95f), new Vector3(0.42f, 1.7f, 0.42f), brick, MeshKit.Solid(DarkBrick));
            k.Prism(new Vector3(0, 1.7f, -0.95f), 0.24f, 0f, 1.35f, 8, MeshKit.Solid(variant % 2 == 0 ? CopperGreen : Slate), true, false, null, Mathf.PI / 8);
        }

        /// <summary>Hilera de casas victorianas de ladrillo: tejado corrido de pizarra, chimeneas y miradores.</summary>
        private static void BrickTerraceRow(MeshKit k, int variant, System.Random rng)
        {
            float h = 0.72f + (float)rng.NextDouble() * 0.12f;
            Color brickA = variant % 3 == 2 ? LondonBrick : Brick;
            for (int i = 0; i < 4; i++)
            {
                var c = new Vector3(-0.45f + i * 0.3f, 0, 0);
                Color w = i % 2 == 0 ? brickA : brickA * 0.93f;
                k.Box(c, new Vector3(0.3f, h, 0.72f), MeshKit.Wall(w), MeshKit.Solid(Slate));
                k.Box(c + new Vector3(0, 0, -0.38f), new Vector3(0.17f, h * 0.55f, 0.07f), MeshKit.Wall(Stone), MeshKit.Solid(Slate)); // mirador
            }
            k.GableRoof(Vector3.up * h, 1.2f, 0.72f, 0.26f, 0.02f, MeshKit.Solid(Slate), MeshKit.Wall(brickA));
            for (int i = 0; i < 3; i++)
                k.Box(new Vector3(-0.3f + i * 0.3f, h + 0.12f, 0), new Vector3(0.08f, 0.24f, 0.16f), MeshKit.Solid(brickA * 0.85f), MeshKit.Solid(Grey));
        }

        /// <summary>Iglesia de torre cuadrada almenada con pináculos (parroquia inglesa).</summary>
        private static void NormanChurch(MeshKit k)
        {
            var stone = MeshKit.Wall(new Color(0.74f, 0.7f, 0.6f));
            var slate = MeshKit.Solid(Slate);
            k.Box(Vector3.zero, new Vector3(0.6f, 0.62f, 1.35f), stone, slate);
            k.GableRoof(Vector3.up * 0.62f, 0.6f, 1.35f, 0.36f, 0.03f, slate, stone);
            var tower = new Vector3(0, 0, -0.9f);
            k.Box(tower, new Vector3(0.42f, 1.3f, 0.42f), stone, MeshKit.Solid(new Color(0.6f, 0.58f, 0.52f)));
            float[] sx = { -1, 1, 1, -1 }, sz = { -1, -1, 1, 1 };
            for (int i = 0; i < 4; i++)
            {
                var p = tower + new Vector3(sx[i] * 0.18f, 1.3f, sz[i] * 0.18f);
                k.Prism(p, 0.045f, 0f, 0.22f, 4, MeshKit.Solid(new Color(0.7f, 0.66f, 0.56f)));
                k.Box(tower + new Vector3(sx[i] * 0.18f, 1.3f, 0), new Vector3(0.05f, 0.06f, 0.1f), stone, stone); // almenas
            }
        }

        /// <summary>Casa francesa de piedra caliza con mansarda de zinc, buhardillas y chimeneas.</summary>
        private static void FrenchMansardHouse(MeshKit k, int variant, Color wall)
        {
            float h = 0.72f + (variant % 2) * 0.18f;
            k.Box(Vector3.zero, new Vector3(0.92f, h, 0.76f), MeshKit.Wall(wall), MeshKit.Solid(Zinc));
            k.Box(Vector3.up * h, new Vector3(0.96f, 0.04f, 0.8f), MeshKit.Solid(Stone), MeshKit.Solid(Stone)); // cornisa
            k.HipRoof(Vector3.up * (h + 0.04f), 0.9f, 0.74f, 0.26f, 0f, MeshKit.Solid(Zinc));
            for (int i = 0; i < 3; i++)
                k.Box(new Vector3(-0.28f + i * 0.28f, h + 0.06f, -0.33f), new Vector3(0.1f, 0.14f, 0.08f), MeshKit.Wall(Limestone), MeshKit.Solid(Zinc));
            k.Box(new Vector3(0.3f, h + 0.18f, 0.1f), new Vector3(0.07f, 0.2f, 0.14f), MeshKit.Solid(Brick), MeshKit.Solid(Grey));
        }

        /// <summary>Catedral gótica francesa: nave larga, tejado empinado y fachada con dos torres de remate plano.</summary>
        private static void GothicTwinTowerCathedral(MeshKit k)
        {
            var stone = MeshKit.Wall(new Color(0.8f, 0.76f, 0.66f));
            var slate = MeshKit.Solid(Slate);
            k.Box(Vector3.zero, new Vector3(0.66f, 0.85f, 1.75f), stone, slate);
            k.GableRoof(Vector3.up * 0.85f, 0.66f, 1.75f, 0.45f, 0.02f, slate, stone);
            k.Box(new Vector3(0, 0, 0.2f), new Vector3(1.15f, 0.7f, 0.42f), stone, slate); // crucero
            k.GableRoof(new Vector3(0, 0.7f, 0.2f), 1.15f, 0.42f, 0.32f, 0.02f, slate, stone);
            foreach (float x in new[] { -0.22f, 0.22f })
                k.Box(new Vector3(x, 0, -0.95f), new Vector3(0.26f, 1.45f, 0.26f), stone, MeshKit.Solid(new Color(0.66f, 0.62f, 0.54f)));
            k.Box(new Vector3(0, 0, -0.95f), new Vector3(0.2f, 1.05f, 0.24f), stone, slate);
            k.Prism(new Vector3(0, 1.3f, 0.2f), 0.06f, 0f, 0.6f, 6, MeshKit.Solid(Metal)); // aguja del crucero
            // Contrafuertes a los lados de la nave.
            for (int i = 0; i < 4; i++)
                foreach (float x in new[] { -0.39f, 0.39f })
                    k.Box(new Vector3(x, 0, -0.55f + i * 0.4f), new Vector3(0.1f, 0.6f, 0.08f), MeshKit.Solid(new Color(0.72f, 0.68f, 0.6f)), MeshKit.Solid(new Color(0.72f, 0.68f, 0.6f)));
        }

        /// <summary>Tres casas de canal estrechas y altas con frontón escalonado (Ámsterdam, Brujas).</summary>
        private static void CanalHouses(MeshKit k, int variant, System.Random rng)
        {
            for (int i = 0; i < 3; i++)
            {
                float h = 0.95f + (float)rng.NextDouble() * 0.3f;
                var c = new Vector3(-0.34f + i * 0.34f, 0, 0);
                Color w = (i + variant) % 2 == 0 ? DarkBrick : Brick;
                k.Box(c, new Vector3(0.33f, h, 0.72f), MeshKit.Wall(w), MeshKit.Solid(Slate));
                k.GableRoof(c + Vector3.up * h, 0.33f, 0.72f, 0.3f, 0f, MeshKit.Solid(Slate), MeshKit.Wall(w));
                // Frontón escalonado en la fachada (y remate blanco).
                for (int s = 0; s < 3; s++)
                {
                    float sw = 0.33f - s * 0.1f;
                    k.Box(c + new Vector3(0, h + s * 0.1f, -0.36f), new Vector3(sw, 0.1f, 0.05f), MeshKit.Wall(w), MeshKit.Solid(Whitewash));
                }
                k.Box(c + new Vector3(0, h * 0.25f, -0.375f), new Vector3(0.22f, h * 0.7f, 0.012f), MeshKit.Wall(new Color(0.9f, 0.88f, 0.82f)), MeshKit.Solid(Whitewash));
            }
        }

        // ================================================================== Mediterráneo y Balcanes

        /// <summary>Casa mediterránea: revoque ocre o encalado, tejado de teja árabe de poca pendiente y balcón.</summary>
        private static void MediterraneanHouse(MeshKit k, int variant, Color wall, System.Random rng)
        {
            float h = 0.55f + (float)rng.NextDouble() * 0.3f;
            k.Box(Vector3.zero, new Vector3(0.9f, h, 0.8f), MeshKit.Wall(wall), MeshKit.Solid(RomanTile));
            k.HipRoof(Vector3.up * h, 0.9f, 0.8f, 0.15f, 0.05f, MeshKit.Solid(RomanTile));
            k.Box(new Vector3(0, h * 0.55f, -0.43f), new Vector3(0.4f, 0.03f, 0.08f), MeshKit.Solid(new Color(0.25f, 0.24f, 0.22f)), MeshKit.Solid(new Color(0.25f, 0.24f, 0.22f)));
            if (variant % 2 == 1)
            {
                k.Box(new Vector3(0.22f, h, 0.15f), new Vector3(0.4f, 0.3f, 0.45f), MeshKit.Wall(wall), MeshKit.Solid(RomanTile));
                k.HipRoof(new Vector3(0.22f, h + 0.3f, 0.15f), 0.4f, 0.45f, 0.1f, 0.03f, MeshKit.Solid(RomanTile));
            }
        }

        /// <summary>Campanario italiano (campanile) junto a una pequeña iglesia.</summary>
        private static void CampanileTower(MeshKit k, int variant, Color wall)
        {
            Color tower = variant % 2 == 0 ? new Color(0.7f, 0.38f, 0.28f) : wall;
            k.Box(new Vector3(0.3f, 0, 0), new Vector3(0.28f, 1.8f, 0.28f), MeshKit.Wall(tower), MeshKit.Solid(RomanTile));
            k.Box(new Vector3(0.3f, 1.45f, 0), new Vector3(0.3f, 0.3f, 0.3f), MeshKit.Solid(new Color(0.9f, 0.86f, 0.76f)), MeshKit.Solid(RomanTile)); // celda de campanas
            k.Pyramid(new Vector3(0.3f, 1.8f, 0), 0.3f, 0.3f, 0.26f, MeshKit.Solid(RomanTile));
            k.Box(new Vector3(-0.2f, 0, 0), new Vector3(0.55f, 0.55f, 1.0f), MeshKit.Wall(wall), MeshKit.Solid(RomanTile));
            k.GableRoof(new Vector3(-0.2f, 0.55f, 0), 0.55f, 1.0f, 0.16f, 0.02f, MeshKit.Solid(RomanTile), MeshKit.Wall(wall));
        }

        /// <summary>Iglesia renacentista de planta de cruz con cúpula sobre tambor y linterna.</summary>
        private static void RenaissanceChurch(MeshKit k, int variant)
        {
            var w = MeshKit.Wall(variant % 2 == 0 ? new Color(0.9f, 0.85f, 0.74f) : new Color(0.86f, 0.74f, 0.56f));
            var tile = MeshKit.Solid(RomanTile);
            k.Box(Vector3.zero, new Vector3(0.5f, 0.6f, 1.4f), w, tile);
            k.GableRoof(Vector3.up * 0.6f, 0.5f, 1.4f, 0.16f, 0.02f, tile, w);
            k.Box(Vector3.zero, new Vector3(1.2f, 0.55f, 0.45f), w, tile);
            k.GableRoof(Vector3.up * 0.55f, 1.2f, 0.45f, 0.15f, 0.02f, tile, w);
            k.Prism(Vector3.up * 0.6f, 0.27f, 0.27f, 0.28f, 12, w, false, true);
            var domeCol = MeshKit.Solid(variant % 2 == 0 ? RomanTile : Lead);
            k.Dome(Vector3.up * 0.88f, 0.28f, 1.25f, 5, 12, domeCol);
            k.Prism(Vector3.up * 1.22f, 0.05f, 0.04f, 0.12f, 6, w);
            k.Prism(Vector3.up * 1.34f, 0.05f, 0f, 0.1f, 6, domeCol);
        }

        /// <summary>Iglesia ortodoxa bizantina (Balcanes, Grecia): cruz griega, tambor y cúpula, muros de franjas.</summary>
        private static void ByzantineDomeChurch(MeshKit k, int variant)
        {
            var stone = MeshKit.Wall(variant % 2 == 0 ? new Color(0.78f, 0.6f, 0.46f) : Whitewash);
            var dome = MeshKit.Solid(variant % 2 == 0 ? RomanTile : new Color(0.35f, 0.5f, 0.68f));
            k.Box(Vector3.zero, new Vector3(0.95f, 0.5f, 0.45f), stone, MeshKit.Solid(RomanTile));
            k.Box(Vector3.zero, new Vector3(0.45f, 0.5f, 0.95f), stone, MeshKit.Solid(RomanTile));
            k.Prism(Vector3.up * 0.5f, 0.2f, 0.2f, 0.25f, 10, stone, false, true);
            k.Dome(Vector3.up * 0.75f, 0.21f, 1f, 4, 10, dome);
            foreach (var p in new[] { new Vector3(0.3f, 0.5f, 0.3f), new Vector3(-0.3f, 0.5f, -0.3f) })
            {
                k.Prism(p, 0.1f, 0.1f, 0.12f, 8, stone, false, true);
                k.Dome(p + Vector3.up * 0.12f, 0.1f, 1f, 3, 8, dome);
            }
        }

        // ================================================================== Rusia

        /// <summary>Isba de troncos con tejado empinado y frontón tallado más claro.</summary>
        private static void IzbaHouse(MeshKit k, int variant)
        {
            var log = MeshKit.Wall(variant % 2 == 0 ? Log : Log * 0.85f);
            var roofCol = MeshKit.Solid(variant % 3 == 0 ? new Color(0.5f, 0.32f, 0.22f) : new Color(0.34f, 0.4f, 0.36f));
            k.Box(Vector3.zero, new Vector3(0.8f, 0.48f, 0.66f), log, roofCol);
            for (int i = 1; i < 4; i++) // juntas de los troncos
                k.Box(new Vector3(0, i * 0.12f, 0), new Vector3(0.82f, 0.012f, 0.68f), MeshKit.Solid(Log * 0.6f), MeshKit.Solid(Log * 0.6f));
            k.GableRoof(Vector3.up * 0.48f, 0.8f, 0.66f, 0.42f, 0.06f, roofCol, MeshKit.Solid(new Color(0.7f, 0.58f, 0.42f)));
            k.Box(new Vector3(-0.25f, 0.5f, 0.12f), new Vector3(0.08f, 0.36f, 0.08f), MeshKit.Solid(Brick), MeshKit.Solid(Slate));
        }

        /// <summary>Iglesia ortodoxa rusa: muros blancos, cúpulas de cebolla y campanario de tejado en tienda.</summary>
        private static void OrthodoxOnionChurch(MeshKit k, int variant)
        {
            var white = MeshKit.Wall(new Color(0.93f, 0.91f, 0.86f));
            Color[] domes = { Gold, new Color(0.22f, 0.36f, 0.66f), new Color(0.2f, 0.45f, 0.32f) };
            var dome = MeshKit.Solid(domes[variant % domes.Length]);
            k.Box(Vector3.zero, new Vector3(0.62f, 0.72f, 0.85f), white, MeshKit.Solid(new Color(0.3f, 0.42f, 0.36f)));
            k.Prism(Vector3.up * 0.72f, 0.16f, 0.16f, 0.36f, 8, white, false, true);
            k.OnionDome(Vector3.up * 1.08f, 0.17f, 0.46f, dome);
            k.Prism(Vector3.up * 1.54f, 0.012f, 0.012f, 0.16f, 4, MeshKit.Solid(Gold));
            if (variant % 2 == 0)
                foreach (var p in new[] { new Vector3(-0.22f, 0.72f, -0.28f), new Vector3(0.22f, 0.72f, -0.28f), new Vector3(-0.22f, 0.72f, 0.28f), new Vector3(0.22f, 0.72f, 0.28f) })
                {
                    k.Prism(p, 0.08f, 0.08f, 0.18f, 8, white, false, true);
                    k.OnionDome(p + Vector3.up * 0.18f, 0.085f, 0.24f, dome);
                }
            // Campanario con tejado en tienda (shatyor).
            var bell = new Vector3(0, 0, -0.62f);
            k.Box(bell, new Vector3(0.24f, 1.05f, 0.24f), white, white);
            k.Prism(bell + Vector3.up * 1.05f, 0.17f, 0f, 0.55f, 8, MeshKit.Solid(new Color(0.24f, 0.42f, 0.32f)), true, false, null, Mathf.PI / 8);
            k.OnionDome(bell + Vector3.up * 1.6f, 0.05f, 0.12f, dome);
        }

        /// <summary>Bloque de viviendas constructivista: volumen largo, cubierta plana, bandas de ventanas.</summary>
        private static void ConstructivistBlock(MeshKit k, int variant, Color wall, System.Random rng)
        {
            float h = 1.0f + (float)rng.NextDouble() * 0.4f;
            k.Box(Vector3.zero, new Vector3(1.55f, h, 0.68f), MeshKit.Wall(wall), MeshKit.Solid(Grey));
            for (int i = 1; i < 4; i++)
                k.Box(new Vector3(0, h * i / 4f, 0), new Vector3(1.57f, 0.025f, 0.7f), MeshKit.Solid(wall * 0.82f), MeshKit.Solid(wall * 0.82f));
            if (variant % 2 == 0)
            {
                // Torre de esquina cilíndrica acristalada (como el club Rusakov o el Narkomfin).
                k.Prism(new Vector3(0.7f, 0, -0.2f), 0.24f, 0.24f, h + 0.25f, 12, MeshKit.Wall(new Color(0.55f, 0.6f, 0.62f)), true, true);
            }
            else
                k.Box(new Vector3(-0.5f, h, 0), new Vector3(0.4f, 0.22f, 0.4f), MeshKit.Wall(wall), MeshKit.Solid(Grey));
        }

        // ================================================================== Nórdico

        /// <summary>Casa nórdica de madera (rojo falun, ocre o blanco) con esquinas blancas y tejado empinado.</summary>
        private static void NordicWoodHouse(MeshKit k, int variant, Color wall, Color roof)
        {
            k.Box(Vector3.zero, new Vector3(0.84f, 0.5f, 0.64f), MeshKit.Wall(wall), MeshKit.Solid(roof));
            var trim = MeshKit.Solid(wall == Whitewash ? new Color(0.35f, 0.32f, 0.28f) : Whitewash);
            foreach (float x in new[] { -0.415f, 0.415f })
                foreach (float z in new[] { -0.315f, 0.315f })
                    k.Box(new Vector3(x, 0, z), new Vector3(0.03f, 0.5f, 0.03f), trim, trim);
            k.GableRoof(Vector3.up * 0.5f, 0.84f, 0.64f, 0.38f, 0.05f, MeshKit.Solid(roof), MeshKit.Wall(wall));
            if (variant % 2 == 0) k.Box(new Vector3(0.2f, 0.62f, 0.05f), new Vector3(0.08f, 0.3f, 0.08f), MeshKit.Solid(Whitewash), MeshKit.Solid(Slate));
        }

        /// <summary>Iglesia blanca con torre y aguja oscura (Escandinavia; también la iglesia reformada del Cabo).</summary>
        private static void NordicWhiteChurch(MeshKit k, ArchStyle style)
        {
            var white = MeshKit.Wall(Whitewash);
            var roofCol = MeshKit.Solid(style == ArchStyle.CapeDutch ? Thatch : new Color(0.22f, 0.22f, 0.24f));
            k.Box(Vector3.zero, new Vector3(0.55f, 0.6f, 1.3f), white, roofCol);
            k.GableRoof(Vector3.up * 0.6f, 0.55f, 1.3f, 0.4f, 0.03f, roofCol, white);
            var tower = new Vector3(0, 0, -0.85f);
            k.Box(tower, new Vector3(0.3f, 1.15f, 0.3f), white, white);
            k.Prism(tower + Vector3.up * 1.15f, 0.19f, 0f, 1.0f, 8, MeshKit.Solid(new Color(0.15f, 0.15f, 0.17f)), true, false, null, Mathf.PI / 8);
        }

        // ================================================================== Japón y China

        /// <summary>Machiya: casa urbana de madera oscura con tejado de teja gris y amplio alero.</summary>
        private static void MachiyaHouse(MeshKit k, int variant)
        {
            var wood = MeshKit.Wall(new Color(0.36f, 0.26f, 0.18f));
            var plaster = MeshKit.Wall(new Color(0.9f, 0.88f, 0.82f));
            var tile = MeshKit.Solid(AsianTile);
            k.Box(Vector3.zero, new Vector3(0.95f, 0.36f, 0.72f), wood, tile);
            k.HipRoof(Vector3.up * 0.36f, 0.95f, 0.72f, 0.16f, 0.12f, tile);
            if (variant % 2 == 1)
            {
                k.Box(new Vector3(0, 0.44f, 0.05f), new Vector3(0.7f, 0.26f, 0.5f), plaster, tile);
                k.HipRoof(new Vector3(0, 0.7f, 0.05f), 0.7f, 0.5f, 0.2f, 0.1f, tile);
            }
        }

        /// <summary>Torii: puerta sintoísta roja con travesaño superior negro.</summary>
        private static void ToriiGate(MeshKit k)
        {
            var red = MeshKit.Solid(new Color(0.78f, 0.22f, 0.14f));
            foreach (float x in new[] { -0.34f, 0.34f }) k.Prism(new Vector3(x, 0, 0), 0.045f, 0.04f, 0.78f, 8, red, true, true);
            k.Box(new Vector3(0, 0.6f, 0), new Vector3(0.82f, 0.05f, 0.06f), red, red);
            k.Box(new Vector3(0, 0.76f, 0), new Vector3(1.0f, 0.05f, 0.09f), red, red);
            k.Box(new Vector3(0, 0.81f, 0), new Vector3(1.06f, 0.035f, 0.1f), MeshKit.Solid(new Color(0.1f, 0.1f, 0.1f)), MeshKit.Solid(new Color(0.1f, 0.1f, 0.1f)));
        }

        /// <summary>Siheyuan: casa de patio con cuatro alas de ladrillo gris y tejados de teja curvos.</summary>
        private static void SiheyuanCourtyard(MeshKit k)
        {
            var brick = MeshKit.Wall(GreyBrick);
            var tile = MeshKit.Solid(new Color(0.3f, 0.31f, 0.33f));
            (Vector3 c, float w, float d, float h)[] wings =
            {
                (new Vector3(0, 0, 0.36f), 1f, 0.28f, 0.42f), (new Vector3(0, 0, -0.38f), 1f, 0.24f, 0.34f),
                (new Vector3(-0.38f, 0, 0), 0.24f, 0.48f, 0.34f), (new Vector3(0.38f, 0, 0), 0.24f, 0.48f, 0.34f),
            };
            foreach (var (c, w, d, h) in wings)
            {
                k.Box(c, new Vector3(w, h, d), brick, tile);
                k.GableRoof(c + Vector3.up * h, w, d, 0.14f, 0.06f, tile, brick);
            }
        }

        /// <summary>Templo chino: plataforma de piedra, sala de muros rojos y doble tejado vidriado amarillo o verde.</summary>
        private static void ChineseTempleHall(MeshKit k, int variant)
        {
            var red = MeshKit.Wall(LacquerRed);
            var glaze = MeshKit.Solid(variant % 2 == 0 ? new Color(0.86f, 0.64f, 0.2f) : new Color(0.2f, 0.45f, 0.32f));
            k.Box(Vector3.zero, new Vector3(1.05f, 0.12f, 0.8f), MeshKit.Solid(new Color(0.86f, 0.84f, 0.78f)), MeshKit.Solid(new Color(0.86f, 0.84f, 0.78f)));
            k.Box(Vector3.up * 0.12f, new Vector3(0.82f, 0.38f, 0.56f), red, glaze);
            k.HipRoof(Vector3.up * 0.5f, 0.82f, 0.56f, 0.12f, 0.16f, glaze);
            k.Box(Vector3.up * 0.62f, new Vector3(0.56f, 0.14f, 0.34f), red, glaze);
            k.HipRoof(Vector3.up * 0.76f, 0.56f, 0.34f, 0.16f, 0.12f, glaze);
        }

        /// <summary>Pagoda octogonal de ladrillo de siete pisos.</summary>
        private static void OctagonalPagoda(MeshKit k)
        {
            var brick = MeshKit.Wall(new Color(0.62f, 0.58f, 0.5f));
            var eave = MeshKit.Solid(new Color(0.3f, 0.31f, 0.33f));
            float y = 0;
            for (int i = 0; i < 7; i++)
            {
                float r = 0.3f - i * 0.03f;
                k.Prism(Vector3.up * y, r, r * 0.96f, 0.24f, 8, brick, false, false, null, Mathf.PI / 8);
                k.Prism(Vector3.up * (y + 0.24f), r + 0.07f, r * 0.85f, 0.06f, 8, eave, true, false, null, Mathf.PI / 8);
                y += 0.3f;
            }
            k.Prism(Vector3.up * y, 0.05f, 0f, 0.3f, 8, MeshKit.Solid(Gold));
        }

        // ================================================================== América Latina, el Cabo y EE. UU.

        /// <summary>Casa colonial ibérica: encalada o de color, teja, rejas y, a veces, patio.</summary>
        private static void ColonialHouseLatin(MeshKit k, int variant, Color wall)
        {
            var w = MeshKit.Wall(wall);
            var tile = MeshKit.Solid(RomanTile);
            if (variant % 3 == 2)
            {
                // Casa de patio con tejados de una sola caída hacia el interior.
                foreach (var (c, s) in new[] { (new Vector3(0, 0, -0.36f), new Vector3(1f, 0.45f, 0.28f)), (new Vector3(0, 0, 0.36f), new Vector3(1f, 0.45f, 0.28f)),
                                                (new Vector3(-0.36f, 0, 0), new Vector3(0.28f, 0.45f, 0.44f)), (new Vector3(0.36f, 0, 0), new Vector3(0.28f, 0.45f, 0.44f)) })
                {
                    k.Box(c, s, w, tile);
                    k.HipRoof(c + Vector3.up * s.y, s.x, s.z, 0.1f, 0.03f, tile);
                }
                return;
            }
            float h = 0.5f + (variant % 2) * 0.32f;
            k.Box(Vector3.zero, new Vector3(0.9f, h, 0.8f), w, tile);
            k.HipRoof(Vector3.up * h, 0.9f, 0.8f, 0.14f, 0.05f, tile);
            var iron = MeshKit.Solid(new Color(0.18f, 0.17f, 0.16f));
            for (int i = 0; i < 3; i++) k.Box(new Vector3(-0.28f + i * 0.28f, h * 0.3f, -0.41f), new Vector3(0.12f, h * 0.35f, 0.02f), iron, iron); // rejas
        }

        /// <summary>Iglesia barroca colonial: fachada blanca con dos torres rematadas y cúpula en el crucero.</summary>
        private static void BaroqueTwinTowerChurch(MeshKit k, int variant)
        {
            var white = MeshKit.Wall(variant % 2 == 0 ? Whitewash : new Color(0.92f, 0.84f, 0.62f));
            var tile = MeshKit.Solid(RomanTile);
            var stoneTrim = MeshKit.Solid(new Color(0.55f, 0.5f, 0.42f));
            k.Box(Vector3.zero, new Vector3(0.58f, 0.68f, 1.3f), white, tile);
            k.GableRoof(Vector3.up * 0.68f, 0.58f, 1.3f, 0.18f, 0.02f, tile, white);
            foreach (float x in new[] { -0.24f, 0.24f })
            {
                var t = new Vector3(x, 0, -0.62f);
                k.Box(t, new Vector3(0.22f, 1.2f, 0.22f), white, white);
                k.Box(t + Vector3.up * 1.2f, new Vector3(0.18f, 0.2f, 0.18f), white, white);
                k.Dome(t + Vector3.up * 1.4f, 0.09f, 1.3f, 3, 8, variant % 2 == 0 ? MeshKit.Solid(new Color(0.3f, 0.5f, 0.62f)) : stoneTrim);
            }
            k.Box(new Vector3(0, 0.68f, -0.62f), new Vector3(0.28f, 0.22f, 0.06f), white, white); // frontón entre las torres
            if (variant % 2 == 0)
            {
                k.Prism(new Vector3(0, 0.68f, 0.3f), 0.2f, 0.2f, 0.2f, 10, white, false, true);
                k.Dome(new Vector3(0, 0.88f, 0.3f), 0.2f, 1.2f, 4, 10, MeshKit.Solid(new Color(0.3f, 0.5f, 0.62f)));
            }
        }

        /// <summary>Casa colonial holandesa del Cabo: blanca, techo de paja y frontón curvo central.</summary>
        private static void CapeDutchHouse(MeshKit k, int variant)
        {
            var white = MeshKit.Wall(Whitewash);
            var thatch = MeshKit.Solid(Thatch);
            k.Box(Vector3.zero, new Vector3(1.15f, 0.42f, 0.48f), white, thatch);
            k.GableRoof(Vector3.up * 0.42f, 1.15f, 0.48f, 0.34f, 0.04f, thatch, white);
            // Frontón central ornamental, más alto que el tejado y de perfil curvo (escalonado a esta escala).
            float[] widths = { 0.36f, 0.32f, 0.26f, 0.17f, 0.08f };
            for (int i = 0; i < widths.Length; i++)
                k.Box(new Vector3(0, 0.42f + i * 0.09f, -0.25f), new Vector3(widths[i], 0.09f, 0.05f), white, white);
            if (variant % 2 == 1)
            {
                k.Box(new Vector3(0.48f, 0, 0.42f), new Vector3(0.24f, 0.4f, 0.5f), white, thatch); // ala en L
                k.GableRoof(new Vector3(0.48f, 0.4f, 0.42f), 0.24f, 0.5f, 0.24f, 0.03f, thatch, white);
            }
        }

        /// <summary>Rascacielos art déco con retranqueos escalonados y aguja metálica.</summary>
        private static void ArtDecoTower(MeshKit k, int variant, System.Random rng)
        {
            var stone = MeshKit.Wall(variant % 2 == 0 ? new Color(0.82f, 0.78f, 0.7f) : new Color(0.7f, 0.6f, 0.5f));
            var top = MeshKit.Solid(Grey);
            float y = 0, w = 0.95f;
            float[] heights = { 1.0f + (float)rng.NextDouble() * 0.5f, 1.2f, 0.7f, 0.45f };
            for (int i = 0; i < heights.Length; i++)
            {
                k.Box(Vector3.up * y, new Vector3(w, heights[i], w * 0.85f), stone, top);
                y += heights[i];
                w *= 0.74f;
            }
            k.Prism(Vector3.up * y, 0.07f, 0.01f, 0.7f, 8, MeshKit.Solid(variant % 3 == 0 ? new Color(0.75f, 0.75f, 0.78f) : Metal));
        }

        /// <summary>Casa de madera (tablas horizontales) con porche delantero.</summary>
        private static void ClapboardHouse(MeshKit k, int variant)
        {
            Color[] paint = { new Color(0.93f, 0.92f, 0.88f), new Color(0.9f, 0.85f, 0.66f), new Color(0.7f, 0.76f, 0.78f), new Color(0.78f, 0.84f, 0.74f) };
            var w = MeshKit.Wall(paint[variant % paint.Length]);
            var roofCol = MeshKit.Solid(variant % 2 == 0 ? new Color(0.28f, 0.27f, 0.28f) : new Color(0.45f, 0.28f, 0.22f));
            k.Box(Vector3.zero, new Vector3(0.8f, 0.5f, 0.62f), w, roofCol);
            k.GableRoof(Vector3.up * 0.5f, 0.8f, 0.62f, 0.32f, 0.05f, roofCol, w);
            k.Box(new Vector3(0, 0.3f, -0.42f), new Vector3(0.8f, 0.04f, 0.22f), roofCol, roofCol); // tejadillo del porche
            foreach (float x in new[] { -0.36f, 0f, 0.36f })
                k.Box(new Vector3(x, 0, -0.51f), new Vector3(0.03f, 0.3f, 0.03f), MeshKit.Solid(Whitewash), MeshKit.Solid(Whitewash));
        }

        // ================================================================== Turquía, Himalaya e India

        /// <summary>Casa otomana: planta baja de piedra y piso de madera volado (cumba) con tejado de teja.</summary>
        private static void OttomanTimberHouse(MeshKit k, Color wall)
        {
            k.Box(Vector3.zero, new Vector3(0.78f, 0.32f, 0.66f), MeshKit.Wall(new Color(0.78f, 0.74f, 0.66f)), MeshKit.Solid(RomanTile));
            k.Box(Vector3.up * 0.32f, new Vector3(0.94f, 0.4f, 0.82f), MeshKit.Wall(wall), MeshKit.Solid(RomanTile));
            k.Box(new Vector3(0, 0.36f, -0.45f), new Vector3(0.38f, 0.32f, 0.1f), MeshKit.Wall(wall), MeshKit.Solid(RomanTile)); // mirador volado
            k.HipRoof(Vector3.up * 0.72f, 0.94f, 0.82f, 0.18f, 0.08f, MeshKit.Solid(RomanTile));
        }

        /// <summary>Mezquita otomana: cúpula central de plomo con semicúpulas y minaretes de lápiz.</summary>
        private static void OttomanImperialMosque(MeshKit k, int variant)
        {
            var stone = MeshKit.Wall(new Color(0.87f, 0.84f, 0.76f));
            var lead = MeshKit.Solid(Lead);
            k.Box(Vector3.zero, new Vector3(1.0f, 0.48f, 1.0f), stone, lead);
            k.Prism(Vector3.up * 0.48f, 0.36f, 0.36f, 0.12f, 16, stone, false, true);
            k.Dome(Vector3.up * 0.6f, 0.37f, 0.8f, 5, 16, lead);
            foreach (var d in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                k.Dome(Vector3.up * 0.48f + d * 0.36f, 0.17f, 0.7f, 3, 10, lead); // semicúpulas
            int minarets = variant % 2 == 0 ? 2 : 4;
            Vector3[] corners = { new Vector3(-0.54f, 0, -0.54f), new Vector3(0.54f, 0, -0.54f), new Vector3(0.54f, 0, 0.54f), new Vector3(-0.54f, 0, 0.54f) };
            for (int i = 0; i < minarets; i++)
            {
                var p = corners[i];
                k.Prism(p, 0.05f, 0.042f, 1.6f, 10, stone, false, true);
                k.Prism(p + Vector3.up * 1.1f, 0.07f, 0.07f, 0.03f, 10, stone);  // balcón (şerefe)
                k.Prism(p + Vector3.up * 1.6f, 0.05f, 0f, 0.38f, 10, lead);      // remate cónico de lápiz
            }
        }

        /// <summary>Casa himalaya: muros blancos ligeramente ataludados, franja granate bajo la azotea.</summary>
        private static void HimalayanFlatHouse(MeshKit k, int variant, System.Random rng)
        {
            float h = 0.5f + (float)rng.NextDouble() * 0.35f;
            var white = MeshKit.Wall(Whitewash);
            k.Prism(Vector3.zero, 0.62f, 0.56f, h, 4, white, true, false, MeshKit.Solid(new Color(0.6f, 0.55f, 0.48f)), Mathf.PI / 4);
            k.Prism(Vector3.up * h, 0.57f, 0.57f, 0.08f, 4, MeshKit.Solid(Maroon), true, false, null, Mathf.PI / 4);
            if (variant % 2 == 0) k.Box(new Vector3(0.15f, h + 0.08f, 0.1f), new Vector3(0.3f, 0.2f, 0.3f), white, MeshKit.Solid(Maroon));
        }

        /// <summary>Estupa (chorten) blanca con gradas, cúpula y aguja dorada.</summary>
        private static void Chorten(MeshKit k)
        {
            var white = MeshKit.Wall(Whitewash);
            k.Box(Vector3.zero, new Vector3(0.7f, 0.14f, 0.7f), white, white);
            k.Box(Vector3.up * 0.14f, new Vector3(0.55f, 0.12f, 0.55f), white, white);
            k.Dome(Vector3.up * 0.26f, 0.28f, 0.95f, 4, 12, white);
            k.Box(Vector3.up * 0.52f, new Vector3(0.14f, 0.08f, 0.14f), MeshKit.Solid(Maroon), MeshKit.Solid(Maroon));
            k.Prism(Vector3.up * 0.6f, 0.07f, 0.02f, 0.45f, 8, MeshKit.Solid(Gold));
        }

        /// <summary>Templo hindú del norte: plataforma, mandapa y torre curva (shikhara) con amalaka y remate dorado.</summary>
        private static void ShikharaTemple(MeshKit k)
        {
            var sand = MeshKit.Wall(Sandstone);
            k.Box(Vector3.zero, new Vector3(0.9f, 0.12f, 1.0f), MeshKit.Solid(Sandstone * 0.9f), MeshKit.Solid(Sandstone * 0.9f));
            k.Box(new Vector3(0, 0.12f, -0.3f), new Vector3(0.48f, 0.3f, 0.36f), sand, MeshKit.Solid(Sandstone));
            k.Pyramid(new Vector3(0, 0.42f, -0.3f), 0.48f, 0.36f, 0.18f, MeshKit.Solid(Sandstone));
            var profile = new List<Vector2> { new Vector2(0.3f, 0f), new Vector2(0.3f, 0.3f), new Vector2(0.27f, 0.6f), new Vector2(0.2f, 0.9f), new Vector2(0.1f, 1.1f), new Vector2(0.04f, 1.18f) };
            k.Lathe(new Vector3(0, 0.12f, 0.15f), profile, 8, MeshKit.Solid(Sandstone), MeshKit.Solid(Sandstone * 1.08f));
            k.Prism(new Vector3(0, 1.28f, 0.15f), 0.1f, 0.1f, 0.05f, 12, MeshKit.Solid(Sandstone)); // amalaka
            k.Prism(new Vector3(0, 1.33f, 0.15f), 0.04f, 0f, 0.18f, 8, MeshKit.Solid(Gold));          // kalasha
        }

        /// <summary>Edificio indosarracénico: cuerpo de arenisca, cúpula blanca central y templetes (chhatris) en las esquinas.</summary>
        private static void IndoSaracenicHall(MeshKit k, int variant)
        {
            var body = MeshKit.Wall(variant % 2 == 0 ? Sandstone : new Color(0.92f, 0.86f, 0.74f));
            var white = MeshKit.Solid(Marble);
            k.Box(Vector3.zero, new Vector3(1.2f, 0.6f, 0.8f), body, MeshKit.Solid(Sandstone));
            k.Prism(Vector3.up * 0.6f, 0.24f, 0.24f, 0.18f, 12, body, false, true);
            k.Dome(Vector3.up * 0.78f, 0.25f, 1.25f, 5, 12, white);
            k.Prism(Vector3.up * 1.09f, 0.03f, 0f, 0.15f, 6, MeshKit.Solid(Gold));
            foreach (var p in new[] { new Vector3(-0.5f, 0.6f, -0.3f), new Vector3(0.5f, 0.6f, -0.3f), new Vector3(-0.5f, 0.6f, 0.3f), new Vector3(0.5f, 0.6f, 0.3f) })
            {
                k.Box(p, new Vector3(0.14f, 0.14f, 0.14f), body, body);
                k.Dome(p + Vector3.up * 0.14f, 0.085f, 1.1f, 3, 8, white);
            }
        }

        // ================================================================== Monumentos nuevos (anteriores a 1936)

        /// <summary>Puerta de la India de Bombay (1924): arco de basalto amarillo con cuatro torrecillas.</summary>
        private static void GatewayOfIndia(MeshKit k)
        {
            var s = MeshKit.Wall(new Color(0.84f, 0.72f, 0.5f));
            k.Box(new Vector3(-0.42f, 0, 0), new Vector3(0.36f, 0.85f, 0.5f), s, s);
            k.Box(new Vector3(0.42f, 0, 0), new Vector3(0.36f, 0.85f, 0.5f), s, s);
            k.Box(Vector3.up * 0.85f, new Vector3(1.2f, 0.3f, 0.52f), s, s);
            foreach (float x in new[] { -0.5f, -0.17f, 0.17f, 0.5f })
            {
                k.Prism(new Vector3(x, 1.15f, 0), 0.07f, 0.07f, 0.18f, 8, s, false, true);
                k.Dome(new Vector3(x, 1.33f, 0), 0.075f, 1.2f, 3, 8, s);
            }
        }

        /// <summary>Victoria Memorial de Calcuta (1921): mármol blanco, gran cúpula central y torres de esquina.</summary>
        private static void VictoriaMemorial(MeshKit k)
        {
            var m = MeshKit.Wall(Marble);
            k.Box(Vector3.zero, new Vector3(2.0f, 0.55f, 1.3f), m, MeshKit.Solid(Marble));
            k.Prism(Vector3.up * 0.55f, 0.36f, 0.36f, 0.3f, 16, m, false, true);
            k.Dome(Vector3.up * 0.85f, 0.38f, 1.2f, 6, 16, MeshKit.Solid(Marble));
            k.Prism(Vector3.up * 1.3f, 0.05f, 0.02f, 0.3f, 8, MeshKit.Solid(new Color(0.25f, 0.24f, 0.22f))); // Ángel de la Victoria
            foreach (var p in new[] { new Vector3(-0.85f, 0.55f, -0.5f), new Vector3(0.85f, 0.55f, -0.5f), new Vector3(-0.85f, 0.55f, 0.5f), new Vector3(0.85f, 0.55f, 0.5f) })
            {
                k.Box(p, new Vector3(0.2f, 0.25f, 0.2f), m, m);
                k.Dome(p + Vector3.up * 0.25f, 0.12f, 1.2f, 3, 10, MeshKit.Solid(Marble));
            }
        }

        /// <summary>Union Buildings de Pretoria (1913): dos alas con torres de cúpula y anfiteatro de columnas en medio.</summary>
        private static void UnionBuildings(MeshKit k)
        {
            var s = MeshKit.Wall(new Color(0.82f, 0.72f, 0.56f));
            var roof = MeshKit.Solid(RomanTile);
            foreach (float x in new[] { -1.0f, 1.0f })
            {
                k.Box(new Vector3(x, 0, 0), new Vector3(0.9f, 0.5f, 0.55f), s, roof);
                k.HipRoof(new Vector3(x, 0.5f, 0), 0.9f, 0.55f, 0.16f, 0.03f, roof);
                var t = new Vector3(x * 0.55f, 0, -0.05f);
                k.Box(t, new Vector3(0.24f, 1.2f, 0.24f), s, s);
                k.Dome(t + Vector3.up * 1.2f, 0.14f, 1.3f, 4, 10, MeshKit.Solid(CopperGreen));
            }
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.PI * (0.15f + 0.7f * i / 8f);
                k.Prism(new Vector3(Mathf.Cos(a) * 0.45f, 0, -Mathf.Sin(a) * 0.45f + 0.25f), 0.03f, 0.03f, 0.4f, 6, s, true, true);
            }
        }

        /// <summary>Palacio de Bellas Artes (México, 1934): mármol blanco con cúpulas anaranjadas y doradas.</summary>
        private static void BellasArtes(MeshKit k)
        {
            var m = MeshKit.Wall(new Color(0.94f, 0.92f, 0.86f));
            var orange = MeshKit.Solid(new Color(0.9f, 0.55f, 0.18f));
            k.Box(Vector3.zero, new Vector3(1.5f, 0.6f, 1.1f), m, MeshKit.Solid(Marble));
            k.Prism(Vector3.up * 0.6f, 0.34f, 0.34f, 0.2f, 16, m, false, true);
            k.Dome(Vector3.up * 0.8f, 0.36f, 1.25f, 6, 16, orange);
            k.Prism(Vector3.up * 1.25f, 0.08f, 0.06f, 0.14f, 8, m);
            k.Dome(Vector3.up * 1.39f, 0.07f, 1.4f, 3, 8, MeshKit.Solid(Gold));
            foreach (float x in new[] { -0.55f, 0.55f })
                k.Dome(new Vector3(x, 0.6f, -0.35f), 0.14f, 1.2f, 3, 10, orange);
        }

        /// <summary>Congreso de la Nación (Buenos Aires, 1906): fachada neoclásica y gran cúpula verde.</summary>
        private static void CongresoArgentina(MeshKit k)
        {
            var s = MeshKit.Wall(new Color(0.8f, 0.76f, 0.68f));
            k.Box(Vector3.zero, new Vector3(1.8f, 0.6f, 1.0f), s, MeshKit.Solid(Grey));
            for (int i = 0; i < 6; i++) k.Prism(new Vector3(-0.3f + i * 0.12f, 0, -0.56f), 0.035f, 0.035f, 0.5f, 6, s, true, true);
            k.Box(new Vector3(0, 0.5f, -0.56f), new Vector3(0.75f, 0.12f, 0.12f), s, s);
            k.Prism(Vector3.up * 0.6f, 0.3f, 0.3f, 0.35f, 16, s, false, true);
            k.Dome(Vector3.up * 0.95f, 0.31f, 1.5f, 6, 16, MeshKit.Solid(CopperGreen));
            k.Prism(Vector3.up * 1.42f, 0.06f, 0.04f, 0.14f, 8, s);
            k.Dome(Vector3.up * 1.56f, 0.05f, 1.4f, 3, 8, MeshKit.Solid(CopperGreen));
        }

        /// <summary>Aduana del Bund (Shanghái, 1927): bloque neoclásico con torre del reloj.</summary>
        private static void CustomsHouse(MeshKit k)
        {
            var s = MeshKit.Wall(new Color(0.78f, 0.74f, 0.66f));
            k.Box(Vector3.zero, new Vector3(1.1f, 1.0f, 0.8f), s, MeshKit.Solid(Grey));
            k.Box(Vector3.up * 1.0f, new Vector3(0.42f, 0.9f, 0.42f), s, MeshKit.Solid(Grey));
            k.Box(Vector3.up * 1.6f, new Vector3(0.45f, 0.18f, 0.45f), MeshKit.Solid(new Color(0.92f, 0.9f, 0.84f)), s); // esferas del reloj
            k.Pyramid(Vector3.up * 1.9f, 0.4f, 0.4f, 0.35f, MeshKit.Solid(Grey));
            k.Prism(Vector3.up * 2.25f, 0.02f, 0f, 0.2f, 4, MeshKit.Solid(Metal));
            // Bloques vecinos del Bund (Banco de Hong Kong y Shanghái, con cúpula).
            k.Box(new Vector3(1.0f, 0, 0), new Vector3(0.8f, 0.8f, 0.8f), s, MeshKit.Solid(Grey));
            k.Dome(new Vector3(1.0f, 0.8f, 0), 0.24f, 1.1f, 4, 12, MeshKit.Solid(Lead));
            k.Box(new Vector3(-0.95f, 0, 0), new Vector3(0.7f, 1.2f, 0.75f), MeshKit.Wall(new Color(0.7f, 0.68f, 0.62f)), MeshKit.Solid(Grey));
            k.Pyramid(new Vector3(-0.95f, 1.2f, 0), 0.5f, 0.5f, 0.4f, MeshKit.Solid(CopperGreen)); // Sassoon House
        }

        /// <summary>Mausoleo de Sun Yat-sen (Nankín, 1929): muros blancos y tejados de teja azul sobre una escalinata.</summary>
        private static void SunYatSenMausoleum(MeshKit k)
        {
            var white = MeshKit.Wall(new Color(0.93f, 0.92f, 0.88f));
            var blue = MeshKit.Solid(new Color(0.22f, 0.36f, 0.62f));
            for (int i = 0; i < 4; i++) k.Box(new Vector3(0, i * 0.06f, -0.9f + i * 0.35f), new Vector3(0.9f - i * 0.1f, 0.06f, 0.35f), MeshKit.Solid(Marble), MeshKit.Solid(Marble));
            k.Box(new Vector3(0, 0.24f, 0.55f), new Vector3(0.9f, 0.42f, 0.6f), white, blue);
            k.HipRoof(new Vector3(0, 0.66f, 0.55f), 0.9f, 0.6f, 0.22f, 0.12f, blue);
            k.Box(new Vector3(0, 0, -1.25f), new Vector3(0.6f, 0.3f, 0.2f), white, blue); // pórtico de entrada
            k.HipRoof(new Vector3(0, 0.3f, -1.25f), 0.6f, 0.2f, 0.1f, 0.08f, blue);
        }

        /// <summary>Ayuntamiento de Estocolmo (1923): ladrillo rojo, torre alta con las tres coronas doradas.</summary>
        private static void StockholmCityHall(MeshKit k)
        {
            var brick = MeshKit.Wall(new Color(0.6f, 0.28f, 0.2f));
            k.Box(Vector3.zero, new Vector3(1.6f, 0.6f, 1.0f), brick, MeshKit.Solid(CopperGreen));
            k.HipRoof(Vector3.up * 0.6f, 1.6f, 1.0f, 0.12f, 0f, MeshKit.Solid(CopperGreen));
            var tower = new Vector3(0.7f, 0, -0.35f);
            k.Box(tower, new Vector3(0.36f, 2.0f, 0.36f), brick, brick);
            k.Box(tower + Vector3.up * 2.0f, new Vector3(0.28f, 0.3f, 0.28f), MeshKit.Solid(CopperGreen), MeshKit.Solid(CopperGreen));
            k.Prism(tower + Vector3.up * 2.3f, 0.04f, 0.01f, 0.25f, 6, MeshKit.Solid(Gold));
            k.Box(tower + Vector3.up * 2.55f, new Vector3(0.16f, 0.06f, 0.06f), MeshKit.Solid(Gold), MeshKit.Solid(Gold)); // tres coronas
        }

        /// <summary>Torre de Belém (Lisboa): baluarte de piedra con torre de almenas y garitas.</summary>
        private static void BelemTower(MeshKit k)
        {
            var s = MeshKit.Wall(new Color(0.9f, 0.86f, 0.74f));
            k.Box(Vector3.zero, new Vector3(0.9f, 0.35f, 0.7f), s, s);
            k.Box(new Vector3(0, 0, 0.12f), new Vector3(0.44f, 1.25f, 0.44f), s, s);
            foreach (var p in new[] { new Vector3(-0.4f, 0.35f, -0.3f), new Vector3(0.4f, 0.35f, -0.3f), new Vector3(-0.2f, 1.25f, -0.08f), new Vector3(0.2f, 1.25f, -0.08f) })
            {
                k.Prism(p, 0.05f, 0.05f, 0.1f, 8, s, false, true);
                k.Dome(p + Vector3.up * 0.1f, 0.05f, 1.4f, 3, 8, s);
            }
        }
    }
}
