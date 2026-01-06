using ProtoBuf;

namespace SpinningWheel.BlockEntityPackets
{
    [ProtoContract]
    public class SetWeavingModePacket
    {
        [ProtoMember(1)]
        public int WeavingMode { get; set; }
    }
}
