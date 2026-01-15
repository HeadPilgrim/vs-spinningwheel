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
    public class BlockSpinningWheel : Block, IMultiBlockColSelBoxes, IMultiBlockInteract
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
        
            // Store per block code variant
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
    
            BlockEntitySpinningWheel beSpinningWheel = world.BlockAccessor.GetBlockEntity(pos) as BlockEntitySpinningWheel;
            return beSpinningWheel;
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
            // This is called for the control block itself (offset 0,0,0)
            var interactions = new List<WorldInteraction>
            {
                new WorldInteraction()
                {
                    ActionLangCode = "spinningwheel:blockhelp-spinningwheel-opencrafting",
                    MouseButton = EnumMouseButton.Right
                }
            };
            return interactions.ToArray();
        }

        #region IMultiBlockInteract Implementation

        public bool MBDoParticalSelection(IWorldAccessor world, BlockPos pos, Vec3i offset)
        {
            return true; // Enable partial selection for all parts
        }

        public bool MBOnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
        {
            //world.Api.Logger.Debug($"[SpinningWheel] MBOnBlockInteractStart - offset: {offset.X},{offset.Y},{offset.Z}");
            
            // Check if player has permission
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }

            // Calculate control block position
            BlockPos controlBlockPos = blockSel.Position.AddCopy(offset);
            
            // Get the block entity
            BlockEntitySpinningWheel beSpinningWheel = world.BlockAccessor.GetBlockEntity(controlBlockPos) as BlockEntitySpinningWheel;
            
            if (beSpinningWheel == null)
            {
                world.Api.Logger.Debug("[SpinningWheel] Block entity is null!");
                return false;
            }

            // Get the block's facing direction
            BlockFacing facing = BlockFacing.FromCode(this.LastCodePart());
            
            // Normalize the offset to north-facing coordinates
            Vec3i normalizedOffset = NormalizeOffset(offset, facing);
            string offsetKey = $"{normalizedOffset.X},{normalizedOffset.Y},{normalizedOffset.Z}";
            
            //world.Api.Logger.Debug($"[SpinningWheel] Facing: {facing.Code}, Raw offset: {offset.X},{offset.Y},{offset.Z}, Normalized: {offsetKey}");

            // Route based on which part was clicked (using north-facing coordinates)
            switch (offsetKey)
            {
                case "0,0,-1":
                    return beSpinningWheel.OnPlayerInteract(byPlayer);

                case "-1,0,-1":
                    return beSpinningWheel.OnPlayerInteract(byPlayer);

                default:
                    return beSpinningWheel.OpenGui(byPlayer);
            }
        }

        private Vec3i NormalizeOffset(Vec3i offset, BlockFacing facing)
        {
            // Rotate the offset back to north-facing coordinates
            // We need to reverse the rotation that was applied
    
            int x = offset.X;
            int y = offset.Y;
            int z = offset.Z;
    
            switch (facing.Code)
            {
                case "north":
                    // Already in north orientation
                    return new Vec3i(x, y, z);
            
                case "east":
                    // Block rotated 270° from north (or -90°)
                    // To reverse: rotate 90° counter-clockwise
                    // X_north = Z_east, Z_north = -X_east
                    return new Vec3i(z, y, -x);
            
                case "south":
                    // Block rotated 180° from north
                    // To reverse: rotate 180°
                    // X_north = -X_south, Z_north = -Z_south
                    return new Vec3i(-x, y, -z);
            
                case "west":
                    // Block rotated 90° from north (or -270°)
                    // To reverse: rotate 270° counter-clockwise (or 90° clockwise)
                    // X_north = -Z_west, Z_north = X_west
                    return new Vec3i(-z, y, x);
            
                default:
                    return offset;
            }
        }

        public bool MBOnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
        {
            return true;
        }

        public void MBOnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
        {
            // No special handling needed
        }

        public bool MBOnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason, Vec3i offset)
        {
            return true;
        }

        public ItemStack MBOnPickBlock(IWorldAccessor world, BlockPos pos, Vec3i offset)
        {
            return OnPickBlock(world, pos);
        }

        public WorldInteraction[] MBGetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection blockSel, IPlayer forPlayer, Vec3i offset)
        {
            // Get the block's facing direction
            BlockFacing facing = BlockFacing.FromCode(this.LastCodePart());
    
            // Normalize the offset to north-facing coordinates
            Vec3i normalizedOffset = NormalizeOffset(offset, facing);
            string offsetKey = $"{normalizedOffset.X},{normalizedOffset.Y},{normalizedOffset.Z}";
    
            var interactions = new List<WorldInteraction>();
    
            // Check if looking at a seat
            if (offsetKey == "0,0,-1" || offsetKey == "-1,0,-1")
            {
                // Show "Sit and Spin Fibers!" for seats
                interactions.Add(new WorldInteraction()
                {
                    ActionLangCode = "spinningwheel:blockhelp-spinningwheel-use",
                    MouseButton = EnumMouseButton.Right
                });
            }
            else
            {
                // Show "Open Crafting Menu!" for other parts
                interactions.Add(new WorldInteraction()
                {
                    ActionLangCode = "spinningwheel:blockhelp-spinningwheel-opencrafting",
                    MouseButton = EnumMouseButton.Right
                });
            }
            return interactions.ToArray();
        }

        public BlockSounds MBGetSounds(IBlockAccessor blockAccessor, BlockSelection blockSel, ItemStack stack, Vec3i offset)
        {
            return Sounds;
        }

        #endregion

        #region Multi-Block Collision/Selection

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
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
        
        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            // Only return the control block's own selection boxes (offset 0,0,0)
            // Other offsets are handled by BlockMultiblock pieces calling MBGetSelectionBoxes
    
            if (ValuesByMultiblockOffset?.SelectionBoxesByOffset == null)
            {
                return base.GetSelectionBoxes(blockAccessor, pos);
            }
    
            Vec3i controlOffset = new Vec3i(0, 0, 0);
            if (ValuesByMultiblockOffset.SelectionBoxesByOffset.TryGetValue(controlOffset, out Cuboidf[] controlBoxes))
            {
                return controlBoxes;
            }
            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        #endregion
    }
}