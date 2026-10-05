using System.Collections.Generic;
using GrandStrategy.Map;
using UnityEngine;

namespace GrandStrategy.Economy
{
    /// <summary>
    /// Economía e industria de un país: edificios, comercio, capital privado, tesoro, construcciones en curso,
    /// líneas de producción militar y reservas. Los valores iniciales salen de countries.json ("economy"),
    /// basados en la situación real de cada país en 1936. Las cifras de comercio son por periodo de cobro
    /// (<see cref="EconomyRules.PeriodDays"/> días).
    /// </summary>
    public sealed class CountryEconomy
    {
        // --- Edificios (índice = BuildingType) ---
        /// <summary>Número de edificios de cada tipo.</summary>
        public readonly int[] Counts = new int[BuildingCatalog.TypeCount];
        /// <summary>Rendimiento total de cada tipo: suma de (multiplicador de nivel × bonificación de ubicación).</summary>
        public readonly float[] OutputUnits = new float[BuildingCatalog.TypeCount];
        /// <summary>Mantenimiento total de cada tipo en unidades de nivel I (suma de multiplicadores de nivel).</summary>
        public readonly float[] UpkeepUnits = new float[BuildingCatalog.TypeCount];

        public int Workshops { get => Count(BuildingType.Workshop); set => SetCount(BuildingType.Workshop, value); }
        public int CivilianFactories { get => Count(BuildingType.CivilianFactory); set => SetCount(BuildingType.CivilianFactory, value); }
        public int CommercialCenters { get => Count(BuildingType.CommercialCenter); set => SetCount(BuildingType.CommercialCenter, value); }
        public int MilitaryFactories { get => Count(BuildingType.MilitaryFactory); set => SetCount(BuildingType.MilitaryFactory, value); }
        public int AircraftFactories { get => Count(BuildingType.AircraftFactory); set => SetCount(BuildingType.AircraftFactory, value); }
        public int Shipyards { get => Count(BuildingType.Shipyard); set => SetCount(BuildingType.Shipyard, value); }
        /// <summary>Si los datos no lo indican, se deduce de las fábricas militares al iniciar la partida.</summary>
        public int MilitaryDepots
        {
            get => Count(BuildingType.MilitaryDepot);
            set { SetCount(BuildingType.MilitaryDepot, value); _depotsKnown = true; }
        }
        private bool _depotsKnown;

        // --- Capital privado ---
        public int PrivateCompanies;
        /// <summary>Capital privado acumulado (millones de $).</summary>
        public float PrivateCapital;
        /// <summary>Inversión privada nacional acumulada (millones de $). Por ahora solo es un indicador.</summary>
        public double PrivateInvestment;
        /// <summary>Inversión privada generada en el último periodo.</summary>
        public float LastPrivateInvestment;

        // --- PIB (el valor actual vive en Country.Gdp) ---
        /// <summary>PIB al empezar la partida (para reiniciarla y medir el crecimiento acumulado).</summary>
        public float InitialGdp;
        /// <summary>Variación del PIB en el último periodo (millones).</summary>
        public float LastGdpChange;
        /// <summary>Bonificación a la recaudación por el crecimiento acumulado del PIB (fracción, se actualiza cada periodo).</summary>
        public float RevenueBonusShare;

        // --- Comercio internacional (por periodo) ---
        public float Exports;
        public float Imports;

        /// <summary>Dinero del Estado (millones de $). Recibe el balance al final de cada periodo.</summary>
        public double Treasury;
        /// <summary>Gastos fijos del Estado por periodo (se calculan al empezar, ver EconomyRules.FixedStateExpenses).</summary>
        public float FixedExpenses;

        /// <summary>Construcciones en curso, por orden de prioridad (la primera recibe capacidad antes).</summary>
        public readonly List<ConstructionProject> Projects = new List<ConstructionProject>();

        /// <summary>Líneas de producción militar (una por categoría de equipamiento).</summary>
        public readonly ProductionLine[] Lines = new ProductionLine[EquipmentCatalog.All.Length];

        /// <summary>Reservas almacenadas por categoría (índice = EquipmentCategory).</summary>
        public readonly double[] Stockpile = new double[EquipmentCatalog.All.Length];

        /// <summary>Copia de los edificios al empezar (para reiniciar la partida).</summary>
        private int[] _initialBuildings;

        public CountryEconomy()
        {
            for (int i = 0; i < Lines.Length; i++) Lines[i] = new ProductionLine { Equipment = EquipmentCatalog.All[i] };
        }

        public ProductionLine Line(EquipmentCategory c) => Lines[(int)c];

        public int Count(BuildingType type) => Counts[(int)type];
        public float Output(BuildingType type) => OutputUnits[(int)type];

        /// <summary>Fija el número de edificios de un tipo, todos de nivel I.</summary>
        private void SetCount(BuildingType type, int value)
        {
            int i = (int)type;
            Counts[i] = value;
            OutputUnits[i] = value;
            UpkeepUnits[i] = value;
        }

        /// <summary>Añade un edificio terminado (rendimiento y mantenimiento en unidades de nivel I).</summary>
        public void AddBuilding(BuildingType type, float output = 1f, float upkeep = 1f)
        {
            int i = (int)type;
            Counts[i]++;
            OutputUnits[i] += output;
            UpkeepUnits[i] += upkeep;
        }

        /// <summary>Aplica una mejora terminada: más rendimiento y mantenimiento sin cambiar el número de edificios.</summary>
        public void ApplyUpgrade(BuildingType type, float extraOutput, float extraUpkeep)
        {
            OutputUnits[(int)type] += extraOutput;
            UpkeepUnits[(int)type] += extraUpkeep;
        }

        /// <summary>Número total de edificios económicos.</summary>
        public int EconomicBuildings
        {
            get
            {
                int n = 0;
                foreach (var d in BuildingCatalog.All) if (d.Category == BuildingCategory.Economic) n += Count(d.Type);
                return n;
            }
        }

        /// <summary>Fábricas disponibles para un tipo de línea.</summary>
        public int PoolSize(ProductionPool pool) =>
            pool == ProductionPool.Land ? MilitaryFactories : pool == ProductionPool.Air ? AircraftFactories : Shipyards;

        public int ActiveProjects(BuildingCategory category)
        {
            int n = 0;
            foreach (var p in Projects) if (p.Building.Category == category) n++;
            return n;
        }

        /// <summary>Guarda los edificios iniciales; se llama una vez al cargar.</summary>
        public void SnapshotInitialBuildings()
        {
            if (!_depotsKnown) MilitaryDepots = Mathf.CeilToInt(MilitaryFactories / 3f);
            _initialBuildings = (int[])Counts.Clone();
        }

        /// <summary>Deja la economía como al empezar la partida (todos los edificios de nivel I).</summary>
        public void ResetToInitial()
        {
            if (_initialBuildings != null)
                for (int i = 0; i < _initialBuildings.Length; i++) SetCount((BuildingType)i, _initialBuildings[i]);
            Projects.Clear();
            PrivateInvestment = 0;
            LastPrivateInvestment = 0f;
            LastGdpChange = 0f;
            RevenueBonusShare = 0f;
            foreach (var line in Lines)
            {
                line.Priority = ProductionPriority.Normal;
                line.Efficiency = EconomyRules.StartEfficiency;
            }
            EconomyRules.AssignFactories(this);
            // Reservas iniciales: lo producido en los últimos meses con la industria de 1936.
            for (int i = 0; i < Lines.Length; i++)
                Stockpile[i] = Lines[i].AssignedFactories * Lines[i].Equipment.OutputPerFactory * Lines[i].Efficiency * EconomyRules.InitialStockpileDays;
            FixedExpenses = 0f;
            FixedExpenses = EconomyRules.FixedStateExpenses(this);
            Treasury = EconomyRules.InitialTreasury(this);
        }

        /// <summary>
        /// Estimación para países sin datos (mundos procedurales o países añadidos a mano): se deduce de la
        /// población y el desarrollo, con los mismos topes moderados que los datos de 1936.
        /// </summary>
        public static CountryEconomy Estimate(Country country)
        {
            float millions = country.Population / 1e6f;
            float dev = Mathf.Clamp(country.AverageDevelopment, 1f, 10f);
            int civ = Mathf.Clamp(Mathf.RoundToInt(millions * dev * 0.04f), 0, 20);
            return new CountryEconomy
            {
                Workshops = Mathf.Clamp(Mathf.RoundToInt(millions * 0.25f), 1, 14),
                CivilianFactories = civ,
                CommercialCenters = Mathf.Clamp(Mathf.RoundToInt(civ * 0.6f) + 1, 1, 12),
                PrivateCompanies = dev >= 5f ? Mathf.Clamp(civ / 3, 0, 6) : 0,
                PrivateCapital = dev >= 5f ? civ * 12f : 0f,
                MilitaryFactories = Mathf.Clamp(civ / 4, 0, 6),
                AircraftFactories = Mathf.Clamp(civ / 8, 0, 3),
                Shipyards = Mathf.Clamp(civ / 8, 0, 3),
                Exports = Mathf.Min(civ * 0.8f, 15f),
                Imports = Mathf.Min(civ * 0.7f, 15f),
            };
        }
    }
}
