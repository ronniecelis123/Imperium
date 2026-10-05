using UnityEngine;

namespace GrandStrategy.Economy
{
    public enum BalanceState { Negative, Neutral, Positive }

    /// <summary>Desglose de ingresos y gastos de un país en un periodo de cobro.</summary>
    public struct EconomyReport
    {
        // Ingresos
        /// <summary>Ingresos de cada tipo de edificio (con niveles y bonificaciones de ubicación). Índice = BuildingType.</summary>
        public float[] IncomeByType;
        /// <summary>Extra sobre los ingresos de los centros comerciales por los centros de distribución.</summary>
        public float DistributionBonus;
        /// <summary>Bonificación a la recaudación por el crecimiento acumulado del PIB.</summary>
        public float RevenueBonus;
        public float PrivateCompanies, Exports;
        // Gastos
        /// <summary>Mantenimiento de cada tipo de edificio. Índice = BuildingType.</summary>
        public float[] UpkeepByType;
        public float Imports, Administration;
        /// <summary>Gastos fijos del Estado (servicios, funcionarios, deuda...), ver <see cref="EconomyRules.FixedStateExpenses"/>.</summary>
        public float FixedExpenses;

        /// <summary>Ingresos de los edificios económicos (incluida la bonificación de distribución).</summary>
        public float BuildingIncome { get { float s = DistributionBonus; foreach (var d in BuildingCatalog.All) if (d.Category == BuildingCategory.Economic) s += IncomeByType[(int)d.Type]; return s; } }
        public float EconomicUpkeep => SumUpkeep(BuildingCategory.Economic);
        public float MilitaryInstallationsUpkeep => SumUpkeep(BuildingCategory.Military);
        public float Income => BuildingIncome + RevenueBonus + PrivateCompanies + Exports;
        public float Expenses => EconomicUpkeep + MilitaryInstallationsUpkeep + Imports + Administration + FixedExpenses;
        public float Balance => Income - Expenses;
        public float TradeBalance => Exports - Imports;

        private float SumUpkeep(BuildingCategory category)
        {
            float s = 0f;
            foreach (var d in BuildingCatalog.All) if (d.Category == category) s += UpkeepByType[(int)d.Type];
            return s;
        }

        public BalanceState State =>
            Balance > EconomyRules.NeutralThreshold ? BalanceState.Positive
            : Balance < -EconomyRules.NeutralThreshold ? BalanceState.Negative : BalanceState.Neutral;
    }

    /// <summary>
    /// Desglose del crecimiento anual estimado del PIB, en puntos porcentuales al año.
    /// </summary>
    public struct GrowthReport
    {
        /// <summary>Crecimiento mínimo de cualquier economía.</summary>
        public float Base;
        /// <summary>Actividad directa: comercio y servicios (talleres, centros comerciales, bancos, distribución, hoteles).</summary>
        public float Commerce;
        /// <summary>Capacidad productiva: industrias civiles, de bienes de consumo y alimentarias.</summary>
        public float Productive;
        /// <summary>Bonificación por salud financiera del Estado.</summary>
        public float Financial;
        /// <summary>Salud financiera de 0 a 1 (0 = sin superávit ni reservas, 1 = máxima).</summary>
        public float FinancialHealth;
        /// <summary>Actividad económica en relación con el tamaño de la economía (≈1 = lo normal en 1936).</summary>
        public float Intensity;

        public float Activity => Commerce + Productive;
        public float Total => Base + Commerce + Productive + Financial;
    }

    /// <summary>
    /// Reglas de la economía en un solo sitio para poder equilibrarlas fácilmente. Las cantidades de dinero son
    /// millones de $ por periodo de cobro, salvo que se indique otra cosa.
    /// </summary>
    public static class EconomyRules
    {
        /// <summary>Cada cuántos días se cobra el balance.</summary>
        public const int PeriodDays = 15;

        // Los ingresos, el mantenimiento y el peso en el PIB de cada edificio económico están en BuildingCatalog.
        /// <summary>Impuestos que paga cada empresa privada.</summary>
        public const float PrivateCompanyIncome = 2.5f;

        // Mantenimiento de las instalaciones militares por periodo.
        public const float MilitaryFactoryUpkeep = 1f;
        public const float AircraftFactoryUpkeep = 1.2f;
        public const float ShipyardUpkeep = 1.2f;
        public const float DepotUpkeep = 0.4f;

        /// <summary>Gastos de administración: fracción de los ingresos brutos.</summary>
        public const float AdministrationShare = 0.06f;

        /// <summary>Balance (en valor absoluto) por debajo del cual se considera nulo.</summary>
        public const float NeutralThreshold = 0.05f;

        // --- Gastos fijos del Estado ---
        // En 1936 casi todo lo que recauda un Estado ya está comprometido (servicios, funcionarios, deuda). Al
        // empezar la partida, la parte del superávit de cada país que supera el umbral se reduce con rendimientos
        // decrecientes: superávit libre = umbral * (superávit / umbral)^exponente. La diferencia queda como gasto
        // fijo por periodo para toda la partida. Así se conserva el orden entre países, se comprimen las
        // diferencias extremas y los edificios nuevos mantienen todo su rendimiento (el gasto no crece con ellos).
        /// <summary>Superávit por periodo (millones de $) por debajo del cual no hay gastos fijos.</summary>
        public const float FixedExpensesThreshold = 10f;
        /// <summary>Exponente aplicado al superávit por encima del umbral (1 = sin gastos fijos).</summary>
        public const float FixedExpensesExponent = 0.6f;

        // --- Tesoro inicial = base + factor * raíz(ingresos brutos por periodo) ---
        // Depende de la capacidad económica, pero mucho menos que proporcionalmente: con los costes actuales, las
        // grandes potencias pueden pagar unos 3 talleres, las intermedias 1,5-2,5 y los países pequeños 1.
        public const float InitialTreasuryBase = 60f;
        public const float InitialTreasuryPerSqrtIncome = 32f;

        // --- Construcción ---
        /// <summary>Capacidad de construcción base (puntos por día) de cualquier país.</summary>
        public const float BaseConstruction = 1f;
        public const float ConstructionPerCivilianFactory = 0.25f;
        /// <summary>Puntos máximos que recibe un proyecto al día (marca el tiempo mínimo de construcción).</summary>
        public const float MaxDailyProgressPerProject = 1f;
        /// <summary>Fracción del coste no construido que se recupera al cancelar.</summary>
        public const float CancelRefundShare = 0.75f;
        /// <summary>Edificios nuevos por provincia: base + desarrollo / divisor.</summary>
        public const int BaseProvinceSlots = 2;
        public const int DevelopmentPerSlot = 3;

        // --- Niveles de los edificios económicos (índice = nivel - 1) ---
        public const int MaxBuildingLevel = 3;
        /// <summary>Rendimiento de cada nivel (ingresos, peso en el PIB y efectos especiales) respecto al nivel I.</summary>
        public static readonly float[] LevelOutputMultiplier = { 1f, 1.5f, 2f };
        /// <summary>Mantenimiento de cada nivel respecto al nivel I.</summary>
        public static readonly float[] LevelUpkeepMultiplier = { 1f, 1.4f, 1.8f };
        /// <summary>Coste de mejorar a cada nivel, como fracción del coste del edificio.</summary>
        public static readonly float[] UpgradeCostShare = { 0f, 0.6f, 0.8f };
        /// <summary>Tiempo de mejora a cada nivel, como fracción del tiempo de construcción.</summary>
        public static readonly float[] UpgradeDaysShare = { 0f, 0.6f, 0.75f };

        // --- Efectos especiales (con tope) ---
        /// <summary>Extra sobre los ingresos de los centros comerciales por cada centro de distribución (nivel I).</summary>
        public const float DistributionBonusPerUnit = 0.02f;
        public const float DistributionMaxBonus = 0.10f;
        /// <summary>Extra a la inversión privada por cada banco (nivel I).</summary>
        public const float BankInvestmentBonusPerUnit = 0.04f;
        public const float BankMaxInvestmentBonus = 0.20f;
        /// <summary>Bonificación máxima de un hotel por la provincia en la que se construye.</summary>
        public const float HotelMaxLocationBonus = 0.20f;
        /// <summary>Parte por desarrollo: 0 con desarrollo ≤ 3, máxima con desarrollo 10.</summary>
        public const float HotelDevelopmentBonus = 0.12f;
        /// <summary>Parte por tener la capital o una ciudad grande.</summary>
        public const float HotelCityBonus = 0.08f;
        public const int HotelCityPopulation = 500000;

        // --- Recaudación ligada al PIB ---
        // Bonificación = min(máximo, pendiente × crecimiento acumulado del PIB): +10 % PIB → +2 %, +50 % → +10 %.
        // Se aplica una vez sobre los ingresos de los edificios económicos y las empresas (los valores base no cambian).
        public const float RevenueBonusPerGdpGrowth = 0.2f;
        public const float MaxRevenueBonus = 0.15f;

        // --- Crecimiento del PIB (tasas en % anual; se aplican proporcionalmente cada periodo) ---
        /// <summary>Crecimiento anual mínimo de cualquier economía.</summary>
        public const float GdpBaseGrowth = 0.5f;
        /// <summary>Crecimiento anual máximo que puede aportar la actividad económica (rendimientos decrecientes).</summary>
        public const float GdpMaxActivityGrowth = 3f;
        /// <summary>Intensidad con la que la actividad alcanza el 63 % de su máximo. Mayor = se satura más despacio.</summary>
        public const float GdpActivitySaturation = 1.2f;
        // El peso de cada edificio en la actividad económica está en BuildingCatalog (GdpWeight).
        /// <summary>
        /// Tamaño de referencia de una economía = coeficiente * PIB^exponente. Ajustado con los datos de 1936 (los
        /// edificios crecen aprox. con la raíz del PIB): así un edificio pesa más en un país pequeño, pero sigue
        /// contando en una gran potencia. Reajustado al bajar el peso del taller para que la intensidad mediana de
        /// 1936 sea la misma que antes.
        /// </summary>
        public const float GdpScaleCoefficient = 0.046f;
        public const float GdpScaleExponent = 0.5f;

        // --- Salud financiera ---
        /// <summary>Bonificación máxima al crecimiento anual (puntos porcentuales).</summary>
        public const float FinancialMaxGrowthBonus = 0.05f;
        /// <summary>Balance / ingresos con el que se alcanza el 63 % de la mitad "superávit".</summary>
        public const float FinancialBalanceSaturation = 0.08f;
        /// <summary>Periodos de gastos cubiertos con los que se alcanza el 63 % de la mitad "reservas".</summary>
        public const float FinancialReserveSaturation = 3f;

        // --- Inversión privada nacional ---
        /// <summary>Fracción de los ingresos de los edificios económicos que se convierte en inversión privada cada periodo.</summary>
        public const float PrivateInvestmentShare = 0.03f;
        /// <summary>Extra máximo (fracción) por crecimiento económico; se alcanza con <see cref="PrivateInvestmentGrowthReference"/>.</summary>
        public const float PrivateInvestmentMaxGrowthBonus = 0.5f;
        /// <summary>Crecimiento anual (%) con el que se obtiene el extra máximo por crecimiento.</summary>
        public const float PrivateInvestmentGrowthReference = 3f;
        /// <summary>Extra máximo (fracción) con salud financiera máxima.</summary>
        public const float PrivateInvestmentMaxFinancialBonus = 0.25f;
        /// <summary>Umbral provisional (millones de $) para crear o ampliar una empresa. Todavía no activa nada.</summary>
        public const float CompanyInvestmentThreshold = 80f;

        // --- Producción militar ---
        public const float StartEfficiency = 0.6f;
        public const float EfficiencyGainPerDay = 0.005f;
        public const float EfficiencyLossPerDay = 0.01f;
        public const float MinEfficiency = 0.3f;
        public const float InitialStockpileDays = 60f;
        public const float BaseStorage = 2000f;
        public const float StoragePerDepot = 1500f;

        public static EconomyReport Evaluate(CountryEconomy e)
        {
            var r = new EconomyReport
            {
                IncomeByType = new float[BuildingCatalog.TypeCount],
                UpkeepByType = new float[BuildingCatalog.TypeCount],
                PrivateCompanies = e.PrivateCompanies * PrivateCompanyIncome,
                Exports = e.Exports,
                Imports = e.Imports,
            };
            foreach (var d in BuildingCatalog.All)
            {
                int i = (int)d.Type;
                r.IncomeByType[i] = e.OutputUnits[i] * d.Income;
                r.UpkeepByType[i] = Mathf.Max(0f, e.UpkeepUnits[i]) * d.UpkeepPerPeriod;
            }
            r.DistributionBonus = DistributionBonus(e) * r.IncomeByType[(int)BuildingType.CommercialCenter];
            r.RevenueBonus = e.RevenueBonusShare * (r.BuildingIncome + r.PrivateCompanies);
            r.Administration = r.Income * AdministrationShare;
            r.FixedExpenses = e.FixedExpenses;
            return r;
        }

        /// <summary>Gastos fijos del Estado según el superávit inicial (se calculan una vez al empezar la partida).</summary>
        public static float FixedStateExpenses(CountryEconomy e)
        {
            var r = Evaluate(e);
            float surplus = r.Balance + r.FixedExpenses; // superávit sin gastos fijos
            if (surplus <= FixedExpensesThreshold) return 0f;
            float free = FixedExpensesThreshold * Mathf.Pow(surplus / FixedExpensesThreshold, FixedExpensesExponent);
            return surplus - free;
        }

        /// <summary>Tesoro con el que empieza un país.</summary>
        public static float InitialTreasury(CountryEconomy e) =>
            InitialTreasuryBase + InitialTreasuryPerSqrtIncome * Mathf.Sqrt(Mathf.Max(0f, Evaluate(e).Income));

        /// <summary>
        /// Salud financiera (0..1), sin depender de cantidades absolutas: mitad por el balance recurrente en
        /// relación con los ingresos y mitad por los periodos de gastos que cubre el tesoro. El coste de construir
        /// nunca entra en el balance (no es un gasto recurrente) y el dinero ya pagado por obras en curso no cuenta
        /// como reserva. Nunca es negativa: no hay penalización.
        /// </summary>
        public static float FinancialHealth(CountryEconomy e, EconomyReport r)
        {
            float balanceRatio = r.Income > 0f ? r.Balance / r.Income : 0f;
            float fromBalance = 1f - Mathf.Exp(-Mathf.Max(0f, balanceRatio) / FinancialBalanceSaturation);
            float periodsCovered = (float)e.Treasury / Mathf.Max(r.Expenses, 0.01f);
            float fromReserves = 1f - Mathf.Exp(-Mathf.Max(0f, periodsCovered) / FinancialReserveSaturation);
            return 0.5f * fromBalance + 0.5f * fromReserves;
        }

        /// <summary>Crecimiento anual estimado del PIB con la situación actual.</summary>
        public static GrowthReport EvaluateGrowth(Map.Country c) => EvaluateGrowth(c, Evaluate(c.Economy));

        public static GrowthReport EvaluateGrowth(Map.Country c, EconomyReport r)
        {
            var e = c.Economy;
            float commerce = 0f, productive = 0f;
            foreach (var d in BuildingCatalog.All)
            {
                if (d.Category != BuildingCategory.Economic) continue;
                float weight = e.Output(d.Type) * d.GdpWeight;
                if (d.GdpProductive) productive += weight;
                else commerce += weight;
            }
            float points = commerce + productive;
            float scale = GdpScaleCoefficient * Mathf.Pow(Mathf.Max(c.Gdp, 1f), GdpScaleExponent);
            float intensity = points / Mathf.Max(scale, 0.01f);
            float activity = GdpMaxActivityGrowth * (1f - Mathf.Exp(-intensity / GdpActivitySaturation));
            float health = FinancialHealth(e, r);
            return new GrowthReport
            {
                Base = GdpBaseGrowth,
                Commerce = points > 0f ? activity * commerce / points : 0f,
                Productive = points > 0f ? activity * productive / points : 0f,
                Financial = FinancialMaxGrowthBonus * health,
                FinancialHealth = health,
                Intensity = intensity,
            };
        }

        /// <summary>Fracción del año que representa un periodo de cobro.</summary>
        public const float PeriodYearFraction = PeriodDays / 365f;

        /// <summary>¿Tiene este país sector privado? (en las economías planificadas no hay inversión privada).</summary>
        public static bool HasPrivateSector(Map.Country c) => c.Government != Map.GovernmentType.Communism;

        /// <summary>
        /// Inversión privada nacional generada en un periodo (millones de $): una parte de la actividad de los
        /// edificios económicos, aumentada por el crecimiento y la salud financiera (con topes). La inversión
        /// acumulada no realimenta el PIB.
        /// </summary>
        public static float PrivateInvestmentPerPeriod(Map.Country c, EconomyReport r, GrowthReport g)
        {
            if (!HasPrivateSector(c)) return 0f;
            return r.BuildingIncome * PrivateInvestmentShare * PrivateInvestmentMultiplier(c.Economy, g);
        }

        /// <summary>Multiplicador de la inversión privada (1 = economía estancada sin reservas ni bancos).</summary>
        public static float PrivateInvestmentMultiplier(CountryEconomy e, GrowthReport g) =>
            1f + PrivateInvestmentMaxGrowthBonus * Mathf.Clamp01(g.Total / PrivateInvestmentGrowthReference)
               + PrivateInvestmentMaxFinancialBonus * g.FinancialHealth
               + BankInvestmentBonus(e);

        /// <summary>Extra a la inversión privada por los bancos (con tope).</summary>
        public static float BankInvestmentBonus(CountryEconomy e) =>
            Mathf.Min(BankMaxInvestmentBonus, BankInvestmentBonusPerUnit * e.Output(BuildingType.Bank));

        /// <summary>Extra sobre los ingresos de los centros comerciales por los centros de distribución (con tope).</summary>
        public static float DistributionBonus(CountryEconomy e) =>
            Mathf.Min(DistributionMaxBonus, DistributionBonusPerUnit * e.Output(BuildingType.DistributionCenter));

        /// <summary>Bonificación a la recaudación según el crecimiento acumulado del PIB (fracción, nunca negativa).</summary>
        public static float RevenueBonusShare(float cumulativeGdpGrowth) =>
            Mathf.Clamp(cumulativeGdpGrowth * RevenueBonusPerGdpGrowth, 0f, MaxRevenueBonus);

        // ================================================================== Niveles

        public static string LevelName(int level) => level <= 1 ? "I" : level == 2 ? "II" : "III";
        public static float LevelOutput(int level) => LevelOutputMultiplier[Mathf.Clamp(level, 1, MaxBuildingLevel) - 1];
        public static float LevelUpkeep(int level) => LevelUpkeepMultiplier[Mathf.Clamp(level, 1, MaxBuildingLevel) - 1];
        /// <summary>Coste de mejorar un edificio hasta targetLevel (2 o 3).</summary>
        public static float UpgradeCost(BuildingDef d, int targetLevel) => d.Cost * UpgradeCostShare[Mathf.Clamp(targetLevel, 1, MaxBuildingLevel) - 1];
        public static int UpgradeDays(BuildingDef d, int targetLevel) =>
            Mathf.Max(1, Mathf.RoundToInt(d.Days * UpgradeDaysShare[Mathf.Clamp(targetLevel, 1, MaxBuildingLevel) - 1]));

        /// <summary>Ingresos por periodo de un edificio concreto con su nivel y ubicación.</summary>
        public static float InstanceIncome(BuildingInstance b) => b.Def.Income * LevelOutput(b.Level) * (1f + b.LocationBonus);
        public static float InstanceUpkeep(BuildingInstance b) => b.Def.UpkeepPerPeriod * LevelUpkeep(b.Level);

        /// <summary>Puntos de construcción por día.</summary>
        public static float ConstructionCapacity(CountryEconomy e) => BaseConstruction + e.Output(BuildingType.CivilianFactory) * ConstructionPerCivilianFactory;

        public static float StorageCapacity(CountryEconomy e) => BaseStorage + Mathf.Max(0, e.MilitaryDepots) * StoragePerDepot;

        public static float StorageUsed(CountryEconomy e)
        {
            float used = 0f;
            for (int i = 0; i < e.Stockpile.Length; i++) used += (float)e.Stockpile[i] * EquipmentCatalog.All[i].StorageWeight;
            return used;
        }

        /// <summary>Huecos para edificios nuevos en una provincia.</summary>
        public static int ProvinceSlots(Map.Province p) => BaseProvinceSlots + p.Development / DevelopmentPerSlot;

        /// <summary>Reparte las fábricas de cada tipo entre sus líneas según la prioridad.</summary>
        public static void AssignFactories(CountryEconomy e)
        {
            foreach (ProductionPool pool in System.Enum.GetValues(typeof(ProductionPool)))
            {
                float total = 0f;
                foreach (var l in e.Lines)
                    if (l.Equipment.Pool == pool) total += EquipmentCatalog.PriorityWeight(l.Priority);
                int factories = e.PoolSize(pool);
                foreach (var l in e.Lines)
                    if (l.Equipment.Pool == pool)
                        l.AssignedFactories = total > 0f ? factories * EquipmentCatalog.PriorityWeight(l.Priority) / total : 0f;
            }
        }

        /// <summary>Producción diaria esperada de una línea (sin contar el límite de almacenamiento).</summary>
        public static float DailyOutput(ProductionLine l) => l.AssignedFactories * l.Equipment.OutputPerFactory * l.Efficiency;

        /// <summary>Nivel de actividad económica según los ingresos brutos del periodo.</summary>
        public static string ActivityLevel(float income) =>
            income >= 150f ? "Muy alta" : income >= 60f ? "Alta" : income >= 20f ? "Media" : income >= 6f ? "Baja" : "Muy baja";
    }
}
