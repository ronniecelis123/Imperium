using System.Collections.Generic;

namespace GrandStrategy.Economy
{
    public enum BuildingType
    {
        Workshop,
        CivilianFactory,
        CommercialCenter,
        MilitaryFactory,
        AircraftFactory,
        Shipyard,
        MilitaryDepot,
        Bank,
        ConsumerGoods,
        FoodIndustry,
        DistributionCenter,
        Hotel,
    }

    public enum BuildingCategory { Economic, Military }

    /// <summary>Definición de un edificio construible (coste, tiempo, ingresos, mantenimiento, requisitos y efectos).</summary>
    public sealed class BuildingDef
    {
        public BuildingType Type;
        public BuildingCategory Category;
        public string Name;
        /// <summary>Nombre del icono procedural (UIIcons).</summary>
        public string Icon;
        public string Description;
        /// <summary>Efectos (una línea cada uno).</summary>
        public string[] Effects;
        /// <summary>Coste en millones de $, se paga al ordenar la construcción.</summary>
        public float Cost;
        /// <summary>Días de construcción a máxima velocidad (1 punto de construcción por día).</summary>
        public int Days;
        /// <summary>Ingresos para el Estado por periodo de cobro (nivel I).</summary>
        public float Income;
        /// <summary>Mantenimiento en millones de $ por periodo de cobro (nivel I).</summary>
        public float UpkeepPerPeriod;
        /// <summary>Peso en la actividad económica que hace crecer el PIB (nivel I).</summary>
        public float GdpWeight;
        /// <summary>true = cuenta como capacidad productiva (industria); false = actividad directa (comercio y servicios).</summary>
        public bool GdpProductive;
        public bool RequiresCoastal;

        /// <summary>Los edificios económicos se pueden mejorar hasta el nivel III.</summary>
        public bool Upgradable => Category == BuildingCategory.Economic;
    }

    /// <summary>
    /// Catálogo de edificios. Para añadir uno nuevo basta con añadir su definición aquí (y, si tiene un efecto
    /// especial, aplicarlo en <see cref="EconomyRules"/>). Los efectos de cada nivel se calculan con
    /// <see cref="EconomyRules.LevelOutput"/> y <see cref="EconomyRules.LevelUpkeep"/>.
    /// </summary>
    public static class BuildingCatalog
    {
        private static string PerPeriod(float m) => MoneyFormat.PerPeriod(m, true);

        // Costes: los edificios económicos tardan entre 7 y 10 años en amortizarse con su beneficio neto (ingresos
        // menos mantenimiento y administración); el taller es la entrada barata y los de efectos especiales
        // (banco, distribución, industria civil) amortizan algo peor a cambio de su bonificación.
        public static readonly BuildingDef[] All =
        {
            // --- Económicos ---
            new BuildingDef
            {
                Type = BuildingType.Workshop, Category = BuildingCategory.Economic, Name = "Taller", Icon = "hammer",
                Description = "Producción artesanal y pequeña manufactura. La inversión más barata y rápida.",
                Effects = new[] { PerPeriod(0.5f), "actividad económica leve" },
                Cost = 50f, Days = 60, Income = 0.5f, UpkeepPerPeriod = 0.22f, GdpWeight = 0.4f,
            },
            new BuildingDef
            {
                Type = BuildingType.FoodIndustry, Category = BuildingCategory.Economic, Name = "Industria alimentaria", Icon = "wheat",
                Description = "Procesado y conservación de alimentos: harinas, conservas, lácteos. Ingresos estables.",
                Effects = new[] { PerPeriod(1.1f), "crecimiento del PIB moderado" },
                Cost = 150f, Days = 90, Income = 1.1f, UpkeepPerPeriod = 0.3f, GdpWeight = 1f, GdpProductive = true,
            },
            new BuildingDef
            {
                Type = BuildingType.Hotel, Category = BuildingCategory.Economic, Name = "Hotel", Icon = "bed",
                Description = "Hoteles y actividad ligada a los viajes. Rinde más en provincias desarrolladas o con grandes ciudades.",
                Effects = new[] { PerPeriod(0.9f), $"hasta +{EconomyRules.HotelMaxLocationBonus * 100:0} % según la provincia" },
                Cost = 120f, Days = 75, Income = 0.9f, UpkeepPerPeriod = 0.25f, GdpWeight = 0.5f,
            },
            new BuildingDef
            {
                Type = BuildingType.DistributionCenter, Category = BuildingCategory.Economic, Name = "Centro de distribución", Icon = "warehouse",
                Description = "Almacenes comerciales y centros de transporte de mercancías.",
                Effects = new[]
                {
                    PerPeriod(0.9f),
                    $"+{EconomyRules.DistributionBonusPerUnit * 100:0} % ingresos de centros comerciales (máx. +{EconomyRules.DistributionMaxBonus * 100:0} %)",
                },
                Cost = 160f, Days = 90, Income = 0.9f, UpkeepPerPeriod = 0.25f, GdpWeight = 0.6f,
            },
            new BuildingDef
            {
                Type = BuildingType.CommercialCenter, Category = BuildingCategory.Economic, Name = "Centro comercial", Icon = "shop",
                Description = "Comercio interior y servicios. Buenos ingresos y actividad comercial.",
                Effects = new[] { PerPeriod(2f), "actividad comercial alta" },
                Cost = 260f, Days = 90, Income = 2f, UpkeepPerPeriod = 0.45f, GdpWeight = 1.2f,
            },
            new BuildingDef
            {
                Type = BuildingType.ConsumerGoods, Category = BuildingCategory.Economic, Name = "Fábrica de bienes de consumo", Icon = "shirt",
                Description = "Ropa, muebles, radios y electrodomésticos sencillos. Rentable, pero cara de mantener.",
                Effects = new[] { PerPeriod(2.2f), "crecimiento del PIB alto" },
                Cost = 260f, Days = 120, Income = 2.2f, UpkeepPerPeriod = 0.65f, GdpWeight = 1.5f, GdpProductive = true,
            },
            new BuildingDef
            {
                Type = BuildingType.Bank, Category = BuildingCategory.Economic, Name = "Banco", Icon = "bank",
                Description = "Institución financiera: crédito y ahorro para la actividad privada.",
                Effects = new[]
                {
                    PerPeriod(1.6f),
                    $"+{EconomyRules.BankInvestmentBonusPerUnit * 100:0} % inversión privada (máx. +{EconomyRules.BankMaxInvestmentBonus * 100:0} %)",
                },
                Cost = 280f, Days = 120, Income = 1.6f, UpkeepPerPeriod = 0.4f, GdpWeight = 0.6f,
            },
            new BuildingDef
            {
                Type = BuildingType.CivilianFactory, Category = BuildingCategory.Economic, Name = "Industria civil", Icon = "factory",
                Description = "Maquinaria e industria pesada civil. Es el motor de la economía y de la construcción.",
                Effects = new[] { $"+{EconomyRules.ConstructionPerCivilianFactory:0.00} puntos/día de capacidad de construcción", PerPeriod(3f) },
                Cost = 400f, Days = 150, Income = 3f, UpkeepPerPeriod = 0.6f, GdpWeight = 0.8f, GdpProductive = true,
            },

            // --- Militares (sin cambios de coste) ---
            new BuildingDef
            {
                Type = BuildingType.MilitaryFactory, Category = BuildingCategory.Military, Name = "Industria militar", Icon = "swords",
                Description = "Produce equipamiento terrestre: armas de infantería, artillería, blindados y suministros.",
                Effects = new[] { "+1 fábrica para las líneas de producción terrestres" },
                Cost = 250f, Days = 120, UpkeepPerPeriod = EconomyRules.MilitaryFactoryUpkeep,
            },
            new BuildingDef
            {
                Type = BuildingType.AircraftFactory, Category = BuildingCategory.Military, Name = "Fábrica aeronáutica", Icon = "plane",
                Description = "Produce aviones y componentes aeronáuticos.",
                Effects = new[] { "+1 fábrica para la línea de producción de aviones" },
                Cost = 300f, Days = 150, UpkeepPerPeriod = EconomyRules.AircraftFactoryUpkeep,
            },
            new BuildingDef
            {
                Type = BuildingType.Shipyard, Category = BuildingCategory.Military, Name = "Astillero", Icon = "anchor",
                Description = "Permite construir y reparar buques.",
                Effects = new[] { "+1 astillero para la línea de producción de buques" },
                Cost = 330f, Days = 180, UpkeepPerPeriod = EconomyRules.ShipyardUpkeep, RequiresCoastal = true,
            },
            new BuildingDef
            {
                Type = BuildingType.MilitaryDepot, Category = BuildingCategory.Military, Name = "Depósito militar", Icon = "crate",
                Description = "Almacena equipamiento y suministros para el ejército.",
                Effects = new[] { $"+{EconomyRules.StoragePerDepot:0} de capacidad de almacenamiento" },
                Cost = 80f, Days = 60, UpkeepPerPeriod = EconomyRules.DepotUpkeep,
            },
        };

        /// <summary>Número de tipos de edificio (tamaño de los arrays indexados por BuildingType).</summary>
        public static readonly int TypeCount = System.Enum.GetValues(typeof(BuildingType)).Length;

        private static Dictionary<BuildingType, BuildingDef> _byType;

        public static BuildingDef Get(BuildingType type)
        {
            if (_byType == null)
            {
                _byType = new Dictionary<BuildingType, BuildingDef>();
                foreach (var d in All) _byType[d.Type] = d;
            }
            return _byType[type];
        }
    }

    /// <summary>Un edificio concreto situado en una provincia.</summary>
    public sealed class BuildingInstance
    {
        public BuildingType Type;
        /// <summary>Nivel actual (1 a <see cref="EconomyRules.MaxBuildingLevel"/>).</summary>
        public int Level = 1;
        /// <summary>Edificio que ya existía en 1936: no ocupa hueco de edificios nuevos.</summary>
        public bool Initial;
        /// <summary>Bonificación de rendimiento por ubicación (hotel), fija desde que se construye.</summary>
        public float LocationBonus;
        /// <summary>Hay una mejora en curso (no se puede encargar otra).</summary>
        public bool Upgrading;

        public BuildingDef Def => BuildingCatalog.Get(Type);
        public bool CanBeUpgraded => Def.Upgradable && Level < EconomyRules.MaxBuildingLevel && !Upgrading;
    }

    /// <summary>Construcción en curso: un edificio nuevo o la mejora de uno existente.</summary>
    public sealed class ConstructionProject
    {
        public BuildingDef Building;
        public int ProvinceId;
        /// <summary>Importe pagado al encargarla (millones de $).</summary>
        public float Cost;
        /// <summary>Puntos de construcción necesarios.</summary>
        public int Days;
        /// <summary>Edificio que se mejora (null = obra nueva).</summary>
        public BuildingInstance Upgrade;
        /// <summary>Nivel que tendrá el edificio al terminar.</summary>
        public int TargetLevel = 1;
        /// <summary>Puntos de construcción acumulados (termina al llegar a <see cref="Days"/>).</summary>
        public float Progress;
        /// <summary>Puntos recibidos el último día (para estimar el tiempo restante).</summary>
        public float LastDailyProgress;

        public bool IsUpgrade => Upgrade != null;
        public string DisplayName => IsUpgrade ? $"{Building.Name} → nivel {EconomyRules.LevelName(TargetLevel)}" : Building.Name;
        public float Progress01 => Days > 0 ? Progress / Days : 1f;
        /// <summary>Dinero que se recupera si se cancela (la parte aún no construida).</summary>
        public float RefundIfCancelled => Cost * (1f - Progress01) * EconomyRules.CancelRefundShare;
    }
}
