using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace SpinningWheel.Utilities
{
    internal interface IMultiBlockFallOnto
    {
        bool MBOnFallOnto(IWorldAccessor world, BlockPos pos, Block block, TreeAttribute blockEntityAttributes, Vec3i offsetInv);

        bool MBCanAcceptFallOnto(IWorldAccessor world, BlockPos pos, Block fallingBlock, TreeAttribute blockEntityAttributes, Vec3i offsetInv);
    }
}
