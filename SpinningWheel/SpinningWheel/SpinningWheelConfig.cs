namespace SpinningWheel
{
    public class SpinningWheelConfig
    {
        public bool RequireTailorClass { get; set; } = false;
        public string[] AllowedClasses { get; set; } = new string[] { "tailor" };
    }
}