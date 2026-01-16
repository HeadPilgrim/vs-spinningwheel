namespace SpinningWheel.ModConfig
{
    public class ModConfig
    {
        public static ModConfig Loaded { get; set; } = new ModConfig();

        // Class/Trait restriction - when enabled, player must have an allowed class OR trait
        public bool RequireClassOrTrait { get; set; } = false;
        public string[] AllowedClasses { get; set; } = new string[] { };
        public string[] AllowedTraits { get; set; } = new string[] { "clothier" };
        
        // Recipe toggle
        public bool DisableTwineGridRecipes { get; set; } = true;

        // Drop spindle chat messages
        public bool ShowDropSpindleProgressMessages { get; set; } = false;
        
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

        // ===========================================
        // Loom Weaving Settings
        // ===========================================

        // Vanilla flax twine weaving settings (flax twine -> linen)
        public int FlaxTwineWeaveInputQuantity { get; set; } = 9;
        public int FlaxTwineWeaveOutputQuantity { get; set; } = 3;

        // Wool twine weaving settings (Wool & More mod -> wool cloth)
        public int WoolTwineWeaveInputQuantity { get; set; } = 9;
        public int WoolTwineWeaveOutputQuantity { get; set; } = 3;

        // Tailor's Delight thread weaving settings (thread -> game cloth)
        public int TailorsDelightThreadWeaveInputQuantity { get; set; } = 9;
        public int TailorsDelightThreadWeaveOutputQuantity { get; set; } = 3;
    }
}