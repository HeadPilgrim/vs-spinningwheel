using System;
using SpinningWheel.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace SpinningWheel.Blocks;

public class BlockSpinningWheel : Block
{
    public static IMountableSeat GetMountable(IWorldAccessor world, TreeAttribute tree)
    {
        BlockPos pos = new BlockPos(
            tree.GetInt("posx"),
            tree.GetInt("posy"),
            tree.GetInt("posz")
        );
    
        BlockEntitySpinningWheel beSpinningWheel = world.BlockAccessor.GetBlockEntity(pos) as BlockEntitySpinningWheel;
        return beSpinningWheel;
    }
    
    //Possible remove this OnBlockInteract start and move to BESpinningWheel class?
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        // Check if player has permission to use this block
        if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
        {
            return false;
        }

        // Get the spinning wheel block entity and route to it
        BlockEntitySpinningWheel beSpinningWheel = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntitySpinningWheel;
    
        return beSpinningWheel?.OnPlayerInteract(byPlayer) ?? false;
    }
    public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
    {
        // Get the spinning wheel block entity
        BlockEntitySpinningWheel beSpinningWheel = world.BlockAccessor.GetBlockEntity(pos) as BlockEntitySpinningWheel;
    
        // Dismount any player that's currently sitting on it
        beSpinningWheel?.MountedBy?.TryUnmount();
    
        base.OnBlockRemoved(world, pos);
    }
    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
    {
        return new WorldInteraction[]
        {
            new WorldInteraction()
            {
                ActionLangCode = "blockhelp-spinningwheel-use",
                MouseButton = EnumMouseButton.Right
            }
        }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
    }
}