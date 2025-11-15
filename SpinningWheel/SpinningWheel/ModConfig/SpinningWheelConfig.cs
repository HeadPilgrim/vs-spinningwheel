namespace SpinningWheel.ModConfig
{
    public class ModConfig
    {
        public static ModConfig Loaded { get; set; } = new ModConfig();

        // Tailor class restriction
        public bool RequireTailorClass { get; set; } = false;
        public string[] AllowedClasses { get; set; } = new string[] { "tailor" };
        
        // Recipe toggle
        public bool DisableTwineGridRecipes { get; set; } = true;
        
        // Vanilla flax settings
        public float FlaxSpinTime { get; set; } = 4.0f;
        public int FlaxInputQuantity { get; set; } = 2;
        public int FlaxOutputQuantity { get; set; } = 1;
        
        // Cotton settings (Floral Zones mod)
        public float CottonSpinTime { get; set; } = 4.0f;
        public int CottonInputQuantity { get; set; } = 2;
        public int CottonOutputQuantity { get; set; } = 1;
        
        // Wool fiber settings
        public float WoolFiberSpinTime { get; set; } = 4.0f;
        public int WoolFiberInputQuantity { get; set; } = 2;
        public int WoolFiberOutputQuantity { get; set; } = 1;
        
        // Wool twine settings (Tailor's Delight mod)
        public float WoolTwineSpinTime { get; set; } = 4.0f;
        public int WoolTwineInputQuantity { get; set; } = 2;
        public int WoolTwineOutputQuantity { get; set; } = 2;
        
        // Papyrus settings (Long-term food mod)
        public float PapyrusSpinTime { get; set; } = 4.0f;
        public int PapyrusInputQuantity { get; set; } = 2;
        public int PapyrusOutputQuantity { get; set; } = 1;
        
        // Algae settings (Long-term food mod)
        public float AlgaeSpinTime { get; set; } = 6.5f;
        public int AlgaeInputQuantity { get; set; } = 1;
        public int AlgaeOutputQuantity { get; set; } = 1;
    }
}