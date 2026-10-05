namespace GrandStrategy.Economy
{
    public enum EquipmentCategory { Infantry, Artillery, Armor, Aircraft, Ships, Supplies }

    /// <summary>Tipo de instalación que alimenta una línea de producción.</summary>
    public enum ProductionPool { Land, Air, Naval }

    public enum ProductionPriority { Stopped, Low, Normal, High }

    /// <summary>
    /// Categoría de equipamiento. De momento hay un producto genérico por categoría; la estructura permite
    /// añadir más adelante modelos concretos (fusiles, cazas...) con sus propias cifras.
    /// </summary>
    public sealed class EquipmentDef
    {
        public EquipmentCategory Category;
        public string Name;
        /// <summary>Producto que se fabrica en la línea.</summary>
        public string Product;
        public string Unit;
        public string Icon;
        public ProductionPool Pool;
        /// <summary>Unidades por fábrica y día con eficiencia del 100 %.</summary>
        public float OutputPerFactory;
        /// <summary>Espacio que ocupa cada unidad en los depósitos (0 = no se guarda en depósitos).</summary>
        public float StorageWeight;
        /// <summary>Materias primas necesarias (se consumirán cuando exista el sistema de recursos).</summary>
        public string Resources;
    }

    /// <summary>Línea de producción de un país.</summary>
    public sealed class ProductionLine
    {
        public EquipmentDef Equipment;
        public ProductionPriority Priority = ProductionPriority.Normal;
        /// <summary>Eficiencia productiva 0..1: crece mientras la línea trabaja y cae si se detiene.</summary>
        public float Efficiency = EconomyRules.StartEfficiency;
        /// <summary>Fábricas asignadas (lo calcula EconomySystem cada día).</summary>
        public float AssignedFactories;
        /// <summary>Unidades producidas el último día.</summary>
        public float DailyOutput;
        /// <summary>true si el último día no se produjo por falta de espacio en los depósitos.</summary>
        public bool StorageFull;
    }

    public static class EquipmentCatalog
    {
        public static readonly EquipmentDef[] All =
        {
            new EquipmentDef { Category = EquipmentCategory.Infantry, Name = "Equipamiento de infantería", Product = "Armas y equipo de infantería",
                Unit = "lotes", Icon = "rifle", Pool = ProductionPool.Land, OutputPerFactory = 4f, StorageWeight = 1f, Resources = "Acero" },
            new EquipmentDef { Category = EquipmentCategory.Artillery, Name = "Artillería", Product = "Piezas de artillería",
                Unit = "piezas", Icon = "cannon", Pool = ProductionPool.Land, OutputPerFactory = 0.8f, StorageWeight = 2f, Resources = "Acero, tungsteno" },
            new EquipmentDef { Category = EquipmentCategory.Armor, Name = "Vehículos blindados", Product = "Carros de combate y blindados",
                Unit = "vehículos", Icon = "tank", Pool = ProductionPool.Land, OutputPerFactory = 0.15f, StorageWeight = 5f, Resources = "Acero, cromo, petróleo" },
            new EquipmentDef { Category = EquipmentCategory.Aircraft, Name = "Aviones", Product = "Aviones de combate",
                Unit = "aviones", Icon = "plane", Pool = ProductionPool.Air, OutputPerFactory = 0.25f, StorageWeight = 0f, Resources = "Aluminio, caucho, petróleo" },
            new EquipmentDef { Category = EquipmentCategory.Ships, Name = "Buques", Product = "Buques de guerra",
                Unit = "buques", Icon = "ship", Pool = ProductionPool.Naval, OutputPerFactory = 0.01f, StorageWeight = 0f, Resources = "Acero, cromo" },
            new EquipmentDef { Category = EquipmentCategory.Supplies, Name = "Suministros militares", Product = "Munición, víveres y repuestos",
                Unit = "t", Icon = "crate", Pool = ProductionPool.Land, OutputPerFactory = 20f, StorageWeight = 0.05f, Resources = "Acero, caucho" },
        };

        public static string PriorityName(ProductionPriority p) =>
            p == ProductionPriority.Stopped ? "Detenida" : p == ProductionPriority.Low ? "Baja" : p == ProductionPriority.Normal ? "Normal" : "Alta";

        public static float PriorityWeight(ProductionPriority p) =>
            p == ProductionPriority.Stopped ? 0f : p == ProductionPriority.Low ? 1f : p == ProductionPriority.Normal ? 2f : 4f;
    }
}
