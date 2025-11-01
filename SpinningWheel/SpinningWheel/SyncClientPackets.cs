using ProtoBuf;

namespace SpinningWheel.ModSystem
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class SyncClientPacket
    {
        public bool RequireTailorClass;
        public string[] AllowedClasses;
    }
}