using System;
using SpinningWheel.BlockEntities;
using SpinningWheel.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace SpinningWheel.Blocks
{
    public class BlockSpinningWheel : Block, IMultiBlockColSelBoxes, IMultiBlockCollisions
    {
        public ValuesByMultiblockOffset ValuesByMultiblockOffset { get; set; } = new();

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            ValuesByMultiblockOffset = ValuesByMultiblockOffset.FromAttributes(this);
        }

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
                    ActionLangCode = "spinningwheel:blockhelp-spinningwheel-use",
                    MouseButton = EnumMouseButton.Right
                }
            }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

        #region Multi-Block Collision/Selection

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return false;
        }

        public Cuboidf[] MBGetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
        {
            if (ValuesByMultiblockOffset.CollisionBoxesByOffset.TryGetValue(offset, out Cuboidf[] collisionBoxes))
            {
                return collisionBoxes;
            }
            Block originalBlock = blockAccessor.GetBlock(pos.AddCopy(offset.X, offset.Y, offset.Z));
            return originalBlock.GetCollisionBoxes(blockAccessor, pos);
        }

        public Cuboidf[] MBGetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
        {
            if (ValuesByMultiblockOffset.SelectionBoxesByOffset.TryGetValue(offset, out Cuboidf[] selectionBoxes))
            {
                return selectionBoxes;
            }
            Block originalBlock = blockAccessor.GetBlock(pos.AddCopy(offset.X, offset.Y, offset.Z));
            return GetSelectionBoxes(blockAccessor, pos);
        }

        public bool MBCanAcceptFallOnto(IWorldAccessor world, BlockPos pos, Block fallingBlock, TreeAttribute blockEntityAttributes, Vec3i offsetInv)
        {
            return base.CanAcceptFallOnto(world, pos, fallingBlock, blockEntityAttributes);
        }

        public bool MBOnFallOnto(IWorldAccessor world, BlockPos pos, Block block, TreeAttribute blockEntityAttributes, Vec3i offsetInv)
        {
            return base.OnFallOnto(world, pos, block, blockEntityAttributes);
        }

        public void MBOnEntityInside(IWorldAccessor world, Entity entity, BlockPos pos, Vec3i offsetInv)
        {
            base.OnEntityInside(world, entity, pos);
        }

        public void MBOnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact, Vec3i offsetInv)
        {
            OnEntityCollide(world, entity, pos.AddCopy(offsetInv), facing, collideSpeed, isImpact);
        }

        #endregion
    }
}