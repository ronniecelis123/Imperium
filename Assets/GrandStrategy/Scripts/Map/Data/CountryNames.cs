using System.Collections.Generic;

namespace GrandStrategy.Map
{
    /// <summary>
    /// Nombres de los países mostrados al jugador, separados de su identidad interna (Tag, Id), que nunca cambia.
    /// <list type="bullet">
    /// <item>Español (por defecto): el nombre de los datos (<see cref="Country.Name"/>), con algunos nombres
    /// cartográficos más cortos o habituales para el mapa (<see cref="MapOverridesEs"/>).</item>
    /// <item>Inglés: <see cref="Country.NameEn"/>.</item>
    /// </list>
    /// Para añadir un idioma basta con otra tabla por Tag y un caso en <see cref="MapLabel"/>; no hay que tocar IDs.
    /// </summary>
    public static class CountryNames
    {
        /// <summary>Idioma de los nombres mostrados ("es" o "en").</summary>
        public static string Language = "es";

        /// <summary>Nombres para el mapa (español) que difieren del de la ficha: más cortos o más reconocibles.</summary>
        private static readonly Dictionary<string, string> MapOverridesEs = new Dictionary<string, string>
        {
            ["DNZ"] = "Dánzig",              // Ciudad Libre de Dánzig
            ["GXC"] = "Guangxi",             // Camarilla de Guangxi
            ["ROM"] = "Rumanía",
            ["SAU"] = "Arabia Saudita",
        };

        /// <summary>Nombre que se escribe sobre el mapa.</summary>
        public static string MapLabel(Country c)
        {
            if (Language == "en") return string.IsNullOrEmpty(c.NameEn) ? c.Name : c.NameEn;
            return c.Tag != null && MapOverridesEs.TryGetValue(c.Tag, out var name) ? name : c.Name;
        }
    }
}
