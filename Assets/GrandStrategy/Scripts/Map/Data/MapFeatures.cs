using System.Collections.Generic;
using UnityEngine;

namespace GrandStrategy.Map
{
    /// <summary>Ciudad importante (lleva edificios en el mapa). Posición en coordenadas UV del mapa.</summary>
    public sealed class CityInfo
    {
        public string Name;
        public Vector2 UV;
        public int Population;
        public bool IsCapital;
        public int OwnerId = -1;
        /// <summary>Provincia en la que está la ciudad (-1 si no se pudo determinar).</summary>
        public int ProvinceId = -1;
        /// <summary>
        /// Puntos de victoria (como en HOI4): cuánto vale la ciudad para la puntuación de guerra y la capitulación.
        /// Se calcula al cargar con <see cref="ComputeVictoryPoints"/>.
        /// </summary>
        public int VictoryPoints;

        /// <summary>Valor según población; la capital suma un extra importante.</summary>
        public static int ComputeVictoryPoints(int population, bool isCapital)
        {
            int points = population >= 5000000 ? 30
                : population >= 2000000 ? 20
                : population >= 1000000 ? 15
                : population >= 500000 ? 10
                : 5;
            return isCapital ? points + 15 : points;
        }
    }

    /// <summary>Tramo de río como polilínea en coordenadas UV. Width: 1 = gran río, 0.5 = afluente.</summary>
    public sealed class RiverPath
    {
        public string Name;
        public float Width = 1f;
        public readonly List<Vector2> Points = new List<Vector2>();
    }
}
