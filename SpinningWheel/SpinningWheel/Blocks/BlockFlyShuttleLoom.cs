using System;
using System.Collections.Generic;
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
    public class BlockFlyShuttleLoom : Block, IMultiBlockColSelBoxes, IMultiBlockInteract
    {
        private static Dictionary<string, ValuesByMultiblockOffset> valuesByCode = new Dictionary<string, ValuesByMultiblockOffset>();
        
        public ValuesByMultiblockOffset ValuesByMultiblockOffset
        {
            get
            {
                if (valuesByCode.TryGetValue(Code.ToString(), out var values))
                {
                    return values;
                }
                return new ValuesByMultiblockOffset();
            }
        }
        
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        
            string codeKey = Code.ToString();
            if (!valuesByCode.ContainsKey(codeKey))
            {
                valuesByCode[codeKey] = ValuesByMultiblockOffset.FromAttributes(this);
            }
        }
        
        public static IMountableSeat GetMountable(IWorldAccessor world, TreeAttribute tree)
        {
            BlockPos pos = new BlockPos(
                tree.GetInt("posx"),
                tree.GetInt("posy"),
                tree.GetInt("posz")
            );

            BlockEntityFlyShuttleLoom beLoom = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityFlyShuttleLoom;
            return beLoom;
        }

        #region IMultiBlockInteract Implementation

        public bool MBDoPartialSelection(IWorldAccessor world, BlockPos pos, Vec3i offset)
        {
            return true;
        }

        public bool MBOnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
        {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
                return false;

            BlockPos controlBlockPos = blockSel.Position.AddCopy(offset);
            BlockEntityFlyShuttleLoom beLoom = world.BlockAccessor.GetBlockEntity(controlBlockPos) as BlockEntityFlyShuttleLoom;
            
            if (beLoom == null)
            {
                world.Api.Logger.Debug("[FlyShuttleLoom] Block entity is null!");
                return false;
            }

            BlockFacing facing = BlockFacing.FromCode(LastCodePart());
            bool isSeat = IsSeatOffset(offset, facing, blockSel.SelectionBoxIndex);

            // world.Api.Logger.Debug($"[FlyShuttleLoom] Interact - Facing: {facing.Code}, Offset: {offset}, SelBox: {blockSel.SelectionBoxIndex}, IsSeat: {isSeat}");

            if (isSeat)
                return beLoom.OnPlayerInteract(byPlayer);
            else
                return beLoom.OpenGui(byPlayer);
        }

        public bool MBOnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
        {
            return true;
        }

        public void MBOnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
        {
        }

        public bool MBOnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason, Vec3i offset)
        {
            return true;
        }

        public ItemStack MBOnPickBlock(IWorldAccessor world, BlockPos pos, Vec3i offset)
        {
            return new ItemStack(this);
        }

        public WorldInteraction[] MBGetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection blockSel, IPlayer forPlayer, Vec3i offset)
        {
            BlockFacing facing = BlockFacing.FromCode(this.LastCodePart());
            bool isSeat = IsSeatOffset(offset, facing, blockSel.SelectionBoxIndex);

            // world.Api.Logger.Debug($"[FlyShuttleLoom] Help - Facing: {facing.Code}, Offset: {offset}, SelBox: {blockSel.SelectionBoxIndex}, IsSeat: {isSeat}");

            if (isSeat)
            {
                return new WorldInteraction[]
                {
                    new WorldInteraction() { ActionLangCode = "spinningwheel:blockhelp-loom-sit", MouseButton = EnumMouseButton.Right }
                };
            }
            else
            {
                return new WorldInteraction[]
                {
                    new WorldInteraction() { ActionLangCode = "spinningwheel:blockhelp-loom-opencrafting", MouseButton = EnumMouseButton.Right }
                };
            }
        }

        public BlockSounds MBGetSounds(IBlockAccessor blockAccessor, BlockSelection blockSel, ItemStack byItemStack, Vec3i offset)
        {
            return Sounds;
        }

        #endregion

        /// <summary>
        /// Precise seat detection: Only counts as seat if both the offset is a bench piece AND SelectionBoxIndex == 1 (the actual seat hitbox).
        /// </summary>
        private bool IsSeatOffset(Vec3i offset, BlockFacing facing, int selectionBoxIndex)
        {
            if (offset.Y != 0) return false;
            if (selectionBoxIndex != 1) return false; // Critical: Only the seat hitbox allows sitting

            int x = offset.X;
            int z = offset.Z;

            switch (facing.Code)
            {
                case "north":
                    return (x == 0 && z == -1) || (x == -1 && z == -1) || (x == 1 && z == -1);

                case "east":
                    return (x == 1 && z == 0) || (x == 1 && z == -1) || (x == 1 && z == 1) ||
                           (x == 0 && z == -1);

                case "south":
                    return (x == 0 && z == 1) || (x == 1 && z == 1) || (x == -1 && z == 1);

                case "west":
                    return (x == -1 && z == 0) || (x == -1 && z == 1) || (x == -1 && z == -1) ||
                           (x == 0 && z == 1);

                default:
                    return false;
            }
        }

        #region Multi-Block Collision/Selection
        public override bool DoPartialSelection(IWorldAccessor world, BlockPos pos)
        {
            return base.DoPartialSelection(world, pos);
        }

        public Cuboidf[] MBGetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
        {
            if (ValuesByMultiblockOffset.CollisionBoxesByOffset.TryGetValue(offset, out Cuboidf[] collisionBoxes))
                return collisionBoxes;

            Block originalBlock = blockAccessor.GetBlock(pos.AddCopy(offset.X, offset.Y, offset.Z));
            return originalBlock.GetCollisionBoxes(blockAccessor, pos);
        }

        public Cuboidf[] MBGetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
        {
            if (ValuesByMultiblockOffset.SelectionBoxesByOffset.TryGetValue(offset, out Cuboidf[] selectionBoxes))
                return selectionBoxes;

            Block originalBlock = blockAccessor.GetBlock(pos.AddCopy(offset.X, offset.Y, offset.Z));
            return GetSelectionBoxes(blockAccessor, pos);
        }
        
        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            if (ValuesByMultiblockOffset?.SelectionBoxesByOffset == null)
                return base.GetSelectionBoxes(blockAccessor, pos);

            Vec3i controlOffset = new Vec3i(0, 0, 0);
            if (ValuesByMultiblockOffset.SelectionBoxesByOffset.TryGetValue(controlOffset, out Cuboidf[] controlBoxes))
                return controlBoxes;

            return base.GetSelectionBoxes(blockAccessor, pos);
        }
        #endregion
    }
}