namespace SpinningWheel.ModConfig
{
    public class ModConfig
    {
        public static ModConfig Loaded { get; set; } = new ModConfig();

        public bool RequireTailorClass { get; set; } = false;
        public string[] AllowedClasses { get; set; } = new string[] { "tailor" };
    }
}