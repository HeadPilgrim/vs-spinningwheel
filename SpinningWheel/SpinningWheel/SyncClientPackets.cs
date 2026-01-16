using ProtoBuf;

namespace SpinningWheel.ModSystem
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class SyncClientPacket
    {
        // Class/Trait restrictions
        public bool RequireClassOrTrait;
        public string[] AllowedClasses;
        public string[] AllowedTraits;
        
        // Recipe control
        public bool DisableTwineGridRecipes;

        // Drop spindle chat messages
        public bool ShowDropSpindleProgressMessages;
        
        // ===========================================
        // Spinning Settings
        // ===========================================
        
        // Vanilla flax settings
        public float FlaxSpinTime;
        public int FlaxInputQuantity;
        public int FlaxOutputQuantity;
        
        // Cotton settings (Floral Zones Caribbean mod)
        public float CottonSpinTime;
        public int CottonInputQuantity;
        public int CottonOutputQuantity;
        
        // Wool fiber settings (Wool & More mod)
        public float WoolFiberSpinTime;
        public int WoolFiberInputQuantity;
        public int WoolFiberOutputQuantity;
        
        // Wool twine settings (Tailor's Delight mod)
        public float WoolTwineSpinTime;
        public int WoolTwineInputQuantity;
        public int WoolTwineOutputQuantity;
        
        // Papyrus settings (Long-term Food mod)
        public float PapyrusSpinTime;
        public int PapyrusInputQuantity;
        public int PapyrusOutputQuantity;
        
        // Algae settings (Long-term Food mod)
        public float AlgaeSpinTime;
        public int AlgaeInputQuantity;
        public int AlgaeOutputQuantity;

        // ===========================================
        // Loom Weaving Settings
        // ===========================================

        // Vanilla flax twine weaving settings (flax twine -> linen)
        public int FlaxTwineWeaveInputQuantity;
        public int FlaxTwineWeaveOutputQuantity;

        // Wool twine weaving settings (Wool & More mod -> wool cloth)
        public int WoolTwineWeaveInputQuantity;
        public int WoolTwineWeaveOutputQuantity;

        // Tailor's Delight thread weaving settings (thread -> game cloth)
        public int TailorsDelightThreadWeaveInputQuantity;
        public int TailorsDelightThreadWeaveOutputQuantity;
    }
}