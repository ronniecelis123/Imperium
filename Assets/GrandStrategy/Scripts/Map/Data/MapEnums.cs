namespace GrandStrategy.Map
{
    /// <summary>Tipo de provincia, igual que la columna "type" del definition.csv de HOI4.</summary>
    public enum ProvinceType : byte
    {
        Land = 0,
        Sea = 1,
        Lake = 2,
    }

    /// <summary>Terreno dominante de una provincia (afectará a combate, movimiento y economía).</summary>
    public enum TerrainType : byte
    {
        Ocean = 0,
        Lake,
        Plains,
        Forest,
        Hills,
        Mountains,
        Desert,
        Jungle,
        Marsh,
        Tundra,
        Arctic,
    }

    /// <summary>Forma de gobierno / ideología (mezcla de las ideologías de HOI4 y los gobiernos de AoH2).</summary>
    public enum GovernmentType : byte
    {
        Democracy = 0,
        Monarchy,
        Communism,
        Fascism,
        NonAligned,
    }

    public static class MapEnumExtensions
    {
        public const int TerrainCount = 11;

        public static bool IsWater(this ProvinceType type) => type != ProvinceType.Land;

        public static string DisplayName(this TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Ocean: return "Océano";
                case TerrainType.Lake: return "Lago";
                case TerrainType.Plains: return "Llanura";
                case TerrainType.Forest: return "Bosque";
                case TerrainType.Hills: return "Colinas";
                case TerrainType.Mountains: return "Montañas";
                case TerrainType.Desert: return "Desierto";
                case TerrainType.Jungle: return "Selva";
                case TerrainType.Marsh: return "Pantano";
                case TerrainType.Tundra: return "Tundra";
                case TerrainType.Arctic: return "Ártico";
                default: return terrain.ToString();
            }
        }

        /// <summary>Coste relativo de expansión/movimiento por el terreno (usado por el generador de países).</summary>
        public static float MovementCost(this TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Plains: return 1f;
                case TerrainType.Forest: return 1.4f;
                case TerrainType.Hills: return 1.7f;
                case TerrainType.Mountains: return 3.2f;
                case TerrainType.Desert: return 2.2f;
                case TerrainType.Jungle: return 2.2f;
                case TerrainType.Marsh: return 2f;
                case TerrainType.Tundra: return 2f;
                case TerrainType.Arctic: return 3.5f;
                default: return 1f;
            }
        }

        /// <summary>Habitantes por píxel de mapa (base para la población procedural).</summary>
        public static float PopulationDensity(this TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Plains: return 1f;
                case TerrainType.Forest: return 0.55f;
                case TerrainType.Hills: return 0.6f;
                case TerrainType.Mountains: return 0.25f;
                case TerrainType.Desert: return 0.12f;
                case TerrainType.Jungle: return 0.4f;
                case TerrainType.Marsh: return 0.3f;
                case TerrainType.Tundra: return 0.08f;
                case TerrainType.Arctic: return 0.01f;
                default: return 0f;
            }
        }

        public static string DisplayName(this GovernmentType government)
        {
            switch (government)
            {
                case GovernmentType.Democracy: return "Democracia";
                case GovernmentType.Monarchy: return "Monarquía";
                case GovernmentType.Communism: return "Comunismo";
                case GovernmentType.Fascism: return "Fascismo";
                case GovernmentType.NonAligned: return "No alineado";
                default: return government.ToString();
            }
        }
    }
}
