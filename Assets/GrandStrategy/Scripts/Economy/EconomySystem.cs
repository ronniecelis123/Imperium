using System;
using System.Collections.Generic;
using GrandStrategy.Core;
using GrandStrategy.Map;
using UnityEngine;

namespace GrandStrategy.Economy
{
    /// <summary>
    /// Simulación económica de todos los países:
    /// <list type="bullet">
    /// <item>Cada día avanza la construcción (obras nuevas y mejoras, según la capacidad nacional y la prioridad de
    /// cada proyecto) y la producción militar (según las fábricas asignadas, la eficiencia y los depósitos).</item>
    /// <item>Cada <see cref="EconomyRules.PeriodDays"/> días suma al tesoro el balance de ingresos y gastos, hace
    /// crecer el PIB y acumula la inversión privada.</item>
    /// </list>
    /// La UI usa sus métodos (CanBuild, StartProject, StartUpgrade...) para que todas las reglas estén en un único sitio.
    /// </summary>
    public sealed class EconomySystem : MonoBehaviour
    {
        private WorldData _world;
        private GameClock _clock;
        private DateTime _lastDay;

        /// <summary>Se dispara tras cada cobro y al reiniciar la partida.</summary>
        public event Action Updated;
        /// <summary>Se dispara al terminar cada día simulado (construcción y producción avanzadas).</summary>
        public event Action DayProcessed;
        /// <summary>Una obra (edificio nuevo o mejora) se ha terminado: país, proyecto, provincia.</summary>
        public event Action<Country, ConstructionProject, Province> BuildingCompleted;
        /// <summary>Cambios hechos por el jugador (proyectos, prioridades) que la UI debe reflejar.</summary>
        public event Action Changed;

        public void Initialize(WorldData world, GameClock clock)
        {
            _world = world;
            _clock = clock;
            foreach (var c in world.Countries)
            {
                if (c.Economy == null) c.Economy = CountryEconomy.Estimate(c);
                c.Economy.SnapshotInitialBuildings();
                c.Economy.InitialGdp = c.Gdp;
            }
            ResetBuildings();
            _lastDay = _clock.Now.Date;
            _clock.DayPassed += OnDayPassed;
        }

        private void OnDestroy()
        {
            if (_clock != null) _clock.DayPassed -= OnDayPassed;
        }

        /// <summary>Días que faltan para el próximo cobro.</summary>
        public int DaysUntilPayout
        {
            get
            {
                int elapsed = (_clock.Now.Date - _world.StartDate.Date).Days;
                return EconomyRules.PeriodDays - Mod(elapsed, EconomyRules.PeriodDays);
            }
        }

        public DateTime NextPayoutDate => _clock.Now.Date.AddDays(DaysUntilPayout);

        /// <summary>Deja todas las economías como al principio (nueva partida).</summary>
        public void ResetAll()
        {
            ResetBuildings();
            _lastDay = _clock.Now.Date;
            Updated?.Invoke();
        }

        private void ResetBuildings()
        {
            foreach (var p in _world.Provinces) p.Buildings.Clear();
            foreach (var c in _world.Countries)
            {
                if (c.Economy == null) continue;
                c.Economy.ResetToInitial();
                c.Gdp = c.Economy.InitialGdp;
                PlaceInitialBuildings(c);
            }
        }

        /// <summary>
        /// Reparte los edificios de 1936 (económicos y militares) entre las provincias del país (más en las de mayor
        /// PIB, por el método de D'Hondt) para poder consultarlos en el mapa y mejorar los económicos. Son de nivel I
        /// y no ocupan huecos de edificios nuevos; los astilleros van a provincias costeras si el país tiene alguna.
        /// Solo se decide su ubicación: el número de edificios es el de los datos históricos.
        /// </summary>
        private void PlaceInitialBuildings(Country c)
        {
            var provinces = new List<Province>();
            foreach (int id in c.Provinces)
            {
                var p = _world.GetProvince(id);
                if (p != null && !p.IsWater) provinces.Add(p);
            }
            if (provinces.Count == 0) return;
            var weight = new float[provinces.Count];
            var assigned = new int[provinces.Count];
            for (int i = 0; i < provinces.Count; i++)
            {
                var p = provinces[i];
                weight[i] = 1f + (p.Gdp > 0 ? p.Gdp : p.Population * (0.6f + p.Development * 0.35f) / 1000f);
            }
            bool anyCoast = provinces.Exists(p => p.Coastal);
            // Primero los edificios más importantes, que acaban en las provincias más ricas.
            for (int t = BuildingCatalog.All.Length - 1; t >= 0; t--)
            {
                var def = BuildingCatalog.All[t];
                bool coastOnly = def.RequiresCoastal && anyCoast;
                for (int n = 0; n < c.Economy.Count(def.Type); n++)
                {
                    int best = -1;
                    for (int i = 0; i < provinces.Count; i++)
                    {
                        if (coastOnly && !provinces[i].Coastal) continue;
                        if (best < 0 || weight[i] / (assigned[i] + 1) > weight[best] / (assigned[best] + 1)) best = i;
                    }
                    assigned[best]++;
                    provinces[best].Buildings.Add(new BuildingInstance { Type = def.Type, Initial = true });
                }
            }
        }

        // ================================================================== Construcción

        /// <summary>Huecos libres de una provincia (descontando edificios nuevos y obras nuevas en curso; las mejoras no ocupan hueco).</summary>
        public int FreeSlots(Province p)
        {
            int used = 0;
            foreach (var b in p.Buildings) if (!b.Initial) used++;
            var owner = _world.GetOwner(p);
            if (owner?.Economy != null)
                foreach (var proj in owner.Economy.Projects)
                    if (proj.ProvinceId == p.Id && !proj.IsUpgrade) used++;
            return EconomyRules.ProvinceSlots(p) - used;
        }

        /// <summary>¿Tiene el país dinero para encargar este edificio?</summary>
        public bool CanAfford(Country country, BuildingDef def, out string reason) => CanPay(country, def.Cost, out reason);

        private static bool CanPay(Country country, float cost, out string reason)
        {
            reason = country.Economy.Treasury < cost
                ? $"Dinero insuficiente: faltan {MoneyFormat.Millions(cost - country.Economy.Treasury)}"
                : null;
            return reason == null;
        }

        /// <summary>¿Se puede construir en esta provincia? Devuelve el motivo si no.</summary>
        public bool CanBuild(Country country, BuildingDef def, Province p, out string reason)
        {
            if (!CanAfford(country, def, out reason)) return false;
            if (p == null || p.IsWater) reason = "Hay que elegir una provincia de tierra";
            else if (p.OwnerId != country.Id) reason = "La provincia no es tuya";
            else if (def.RequiresCoastal && !p.Coastal) reason = "Necesita una provincia costera";
            else if (FreeSlots(p) <= 0) reason = $"Sin espacio (máx. {EconomyRules.ProvinceSlots(p)} edificios nuevos)";
            return reason == null;
        }

        /// <summary>Encarga una obra nueva: el coste se paga entero al momento.</summary>
        public bool StartProject(Country country, BuildingDef def, Province p, out string reason)
        {
            if (!CanBuild(country, def, p, out reason)) return false;
            country.Economy.Treasury -= def.Cost;
            country.Economy.Projects.Add(new ConstructionProject { Building = def, ProvinceId = p.Id, Cost = def.Cost, Days = def.Days });
            Changed?.Invoke();
            return true;
        }

        /// <summary>¿Se puede mejorar este edificio ahora? Devuelve el motivo si no.</summary>
        public bool CanUpgrade(Country country, Province p, BuildingInstance b, out string reason)
        {
            reason = null;
            if (p == null || p.OwnerId != country.Id || !p.Buildings.Contains(b)) reason = "La provincia no es tuya";
            else if (!b.Def.Upgradable) reason = "Este edificio no se puede mejorar";
            else if (b.Level >= EconomyRules.MaxBuildingLevel) reason = "Nivel máximo";
            else if (b.Upgrading) reason = $"Mejorando a nivel {EconomyRules.LevelName(b.Level + 1)}";
            else CanPay(country, EconomyRules.UpgradeCost(b.Def, b.Level + 1), out reason);
            return reason == null;
        }

        /// <summary>Encarga la mejora de un edificio: se paga al momento, usa capacidad de construcción y no ocupa hueco.</summary>
        public bool StartUpgrade(Country country, Province p, BuildingInstance b, out string reason)
        {
            if (!CanUpgrade(country, p, b, out reason)) return false;
            int target = b.Level + 1;
            float cost = EconomyRules.UpgradeCost(b.Def, target);
            country.Economy.Treasury -= cost;
            b.Upgrading = true;
            country.Economy.Projects.Add(new ConstructionProject
            {
                Building = b.Def, ProvinceId = p.Id, Cost = cost, Days = EconomyRules.UpgradeDays(b.Def, target),
                Upgrade = b, TargetLevel = target,
            });
            Changed?.Invoke();
            return true;
        }

        /// <summary>Cancela una obra o mejora y devuelve parte de lo que falta por construir.</summary>
        public void CancelProject(Country country, ConstructionProject project)
        {
            if (!country.Economy.Projects.Remove(project)) return;
            if (project.IsUpgrade) project.Upgrade.Upgrading = false;
            country.Economy.Treasury += project.RefundIfCancelled;
            Changed?.Invoke();
        }

        /// <summary>Sube (-1) o baja (+1) un proyecto en la cola de prioridad.</summary>
        public void MoveProject(Country country, ConstructionProject project, int direction)
        {
            var list = country.Economy.Projects;
            int i = list.IndexOf(project), j = i + direction;
            if (i < 0 || j < 0 || j >= list.Count) return;
            list[i] = list[j];
            list[j] = project;
            Changed?.Invoke();
        }

        /// <summary>Días estimados para terminar un proyecto con el ritmo actual (-1 si está parado).</summary>
        public static float DaysRemaining(CountryEconomy e, ConstructionProject p)
        {
            float rest = p.Days - p.Progress;
            float rate = ExpectedDailyProgress(e, p);
            return rate > 0f ? rest / rate : -1f;
        }

        /// <summary>
        /// Puntos que recibirá la obra el próximo día con la cola y la capacidad actuales (mismo reparto que
        /// AdvanceConstruction, sin modificar nada). Sirve para estimar plazos también de obras recién encargadas.
        /// </summary>
        public static float ExpectedDailyProgress(CountryEconomy e, ConstructionProject p)
        {
            float capacity = EconomyRules.ConstructionCapacity(e);
            foreach (var q in e.Projects)
            {
                float give = Mathf.Min(capacity, EconomyRules.MaxDailyProgressPerProject);
                if (q == p) return give;
                capacity -= give;
            }
            return 0f;
        }

        /// <summary>Bonificación que tendría un hotel construido en esta provincia (desarrollo y ciudades).</summary>
        public float HotelLocationBonus(Province p)
        {
            float dev = Mathf.Clamp01((p.Development - 3) / 7f) * EconomyRules.HotelDevelopmentBonus;
            bool city = false;
            foreach (var c in _world.Cities)
                if (c.ProvinceId == p.Id && (c.IsCapital || c.Population >= EconomyRules.HotelCityPopulation)) { city = true; break; }
            return Mathf.Min(EconomyRules.HotelMaxLocationBonus, dev + (city ? EconomyRules.HotelCityBonus : 0f));
        }

        // ================================================================== Producción

        public void SetPriority(Country country, EquipmentCategory category, ProductionPriority priority)
        {
            country.Economy.Line(category).Priority = priority;
            EconomyRules.AssignFactories(country.Economy);
            Changed?.Invoke();
        }

        // ================================================================== Simulación diaria

        private void OnDayPassed(DateTime now)
        {
            // Se recorre cada día transcurrido: a velocidad máxima un fotograma lento puede saltarse alguno.
            bool paid = false, processed = false;
            for (var day = _lastDay.AddDays(1); day <= now.Date; day = day.AddDays(1))
            {
                foreach (var c in _world.Countries)
                {
                    if (!c.IsAlive || c.Economy == null) continue;
                    AdvanceConstruction(c);
                    AdvanceProduction(c.Economy);
                }
                processed = true;
                int elapsed = (day - _world.StartDate.Date).Days;
                if (elapsed <= 0 || Mod(elapsed, EconomyRules.PeriodDays) != 0) continue;
                foreach (var c in _world.Countries)
                    if (c.IsAlive && c.Economy != null)
                        ClosePeriod(c);
                paid = true;
            }
            if (now.Date > _lastDay) _lastDay = now.Date;
            if (processed) DayProcessed?.Invoke();
            if (paid) Updated?.Invoke();
        }

        /// <summary>Fin de periodo: crecimiento del PIB, inversión privada y cobro del balance (por este orden,
        /// todo calculado con la situación del periodo que termina). Después se actualiza la bonificación de
        /// recaudación por el crecimiento acumulado del PIB, que se aplicará en el siguiente periodo.</summary>
        private static void ClosePeriod(Country c)
        {
            var e = c.Economy;
            var report = EconomyRules.Evaluate(e);
            var growth = EconomyRules.EvaluateGrowth(c, report);
            float before = c.Gdp;
            c.Gdp *= 1f + growth.Total / 100f * EconomyRules.PeriodYearFraction;
            e.LastGdpChange = c.Gdp - before;
            e.LastPrivateInvestment = EconomyRules.PrivateInvestmentPerPeriod(c, report, growth);
            e.PrivateInvestment += e.LastPrivateInvestment;
            e.Treasury += report.Balance;
            e.RevenueBonusShare = e.InitialGdp > 0f ? EconomyRules.RevenueBonusShare(c.Gdp / e.InitialGdp - 1f) : 0f;
        }

        private void AdvanceConstruction(Country country)
        {
            var e = country.Economy;
            if (e.Projects.Count == 0) return;
            float capacity = EconomyRules.ConstructionCapacity(e);
            for (int i = 0; i < e.Projects.Count; i++)
            {
                var p = e.Projects[i];
                float give = Mathf.Min(capacity, EconomyRules.MaxDailyProgressPerProject);
                capacity -= give;
                p.Progress += give;
                p.LastDailyProgress = give;
                if (p.Progress < p.Days) continue;

                // Terminado (no se vuelve a cobrar: el coste se pagó al encargarlo).
                e.Projects.RemoveAt(i--);
                var province = _world.GetProvince(p.ProvinceId);
                if (province == null || province.OwnerId != country.Id) continue; // la provincia cambió de dueño
                if (p.IsUpgrade)
                {
                    var b = p.Upgrade;
                    b.Upgrading = false;
                    if (!province.Buildings.Contains(b)) continue;
                    float bonus = 1f + b.LocationBonus;
                    e.ApplyUpgrade(b.Type,
                        (EconomyRules.LevelOutput(p.TargetLevel) - EconomyRules.LevelOutput(b.Level)) * bonus,
                        EconomyRules.LevelUpkeep(p.TargetLevel) - EconomyRules.LevelUpkeep(b.Level));
                    b.Level = p.TargetLevel;
                }
                else
                {
                    var b = new BuildingInstance { Type = p.Building.Type };
                    if (b.Type == BuildingType.Hotel) b.LocationBonus = HotelLocationBonus(province);
                    e.AddBuilding(b.Type, 1f + b.LocationBonus);
                    province.Buildings.Add(b);
                    if (p.Building.Category == BuildingCategory.Military) EconomyRules.AssignFactories(e);
                }
                BuildingCompleted?.Invoke(country, p, province);
            }
        }

        private static void AdvanceProduction(CountryEconomy e)
        {
            EconomyRules.AssignFactories(e);
            float free = EconomyRules.StorageCapacity(e) - EconomyRules.StorageUsed(e);
            foreach (var l in e.Lines)
            {
                bool working = l.AssignedFactories > 0f;
                l.Efficiency = working
                    ? Mathf.Min(1f, l.Efficiency + EconomyRules.EfficiencyGainPerDay)
                    : Mathf.Max(EconomyRules.MinEfficiency, l.Efficiency - EconomyRules.EfficiencyLossPerDay);
                float output = EconomyRules.DailyOutput(l);
                float weight = l.Equipment.StorageWeight;
                l.StorageFull = false;
                if (weight > 0f && output * weight > free)
                {
                    output = Mathf.Max(0f, free / weight);
                    l.StorageFull = true;
                }
                free -= output * weight;
                l.DailyOutput = output;
                e.Stockpile[(int)l.Equipment.Category] += output;
            }
        }

        private static int Mod(int a, int m) => ((a % m) + m) % m;
    }
}
