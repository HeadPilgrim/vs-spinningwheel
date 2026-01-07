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
    public class BlockFlyShuttleLoom : BlockGeneric, IMultiBlockColSelBoxes, IMultiBlockInteract
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
        
        // --- IMultiBlockInteract Implementation ---
        #region IMultiBlockInteract Implementation
        public bool MBDoParticalSelection(IWorldAccessor world, BlockPos pos, Vec3i offset)
        {
            return true; // No special particle selection needed
        }

        public BlockSounds MBGetSounds(IBlockAccessor blockAccessor, BlockSelection blockSel, ItemStack byItemStack, Vec3i offset)
        {
            return Sounds; // Use block's default sounds
        }

        public bool MBOnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason, Vec3i offset)
        {
            return false; // No special cancel behavior
        }
        public bool MBOnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
        {
            world.Api.Logger.Debug($"[FlyShuttleLoom] MBOnBlockInteractStart - offset: {offset.X},{offset.Y},{offset.Z}, SelectionBoxIndex: {blockSel.SelectionBoxIndex}");
            
            // Check if player has permission
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }

            // Calculate control block position
            BlockPos controlBlockPos = blockSel.Position.AddCopy(offset);
            
            // Get the block entity
            BlockEntityFlyShuttleLoom beLoom = world.BlockAccessor.GetBlockEntity(controlBlockPos) as BlockEntityFlyShuttleLoom;
            
            if (beLoom == null)
            {
                world.Api.Logger.Debug("[FlyShuttleLoom] Block entity is null!");
                return false;
            }

            // Get the block's facing direction
            BlockFacing facing = BlockFacing.FromCode(LastCodePart());
            
            // Normalize the offset to north-facing coordinates
            Vec3i normalizedOffset = NormalizeOffset(offset, facing);
            string offsetKey = $"{normalizedOffset.X},{normalizedOffset.Y},{normalizedOffset.Z}";
            
            world.Api.Logger.Debug($"[FlyShuttleLoom] Facing: {facing.Code}, Raw offset: {offset.X},{offset.Y},{offset.Z}, Normalized: {offsetKey}");

            // Route based on which part was clicked (using north-facing coordinates)
            switch (offsetKey)
            {
                case "0,0,-1":
                    // Bench center
                    if (blockSel.SelectionBoxIndex == 1)
                    {
                        world.Api.Logger.Debug("[FlyShuttleLoom] Mounting player at bench center");
                        return beLoom.OnPlayerInteract(byPlayer);
                    }
                    break;

                case "-1,0,-1":
                    // Bench left (when facing north)
                    if (blockSel.SelectionBoxIndex == 1)
                    {
                        world.Api.Logger.Debug("[FlyShuttleLoom] Mounting player at bench left");
                        return beLoom.OnPlayerInteract(byPlayer);
                    }
                    break;

                case "1,0,-1":
                    // Bench right (when facing north)
                    if (blockSel.SelectionBoxIndex == 1)
                    {
                        world.Api.Logger.Debug("[FlyShuttleLoom] Mounting player at bench right");
                        return beLoom.OnPlayerInteract(byPlayer);
                    }
                    break;

                default:
                    // All other parts of the loom - open GUI
                    world.Api.Logger.Debug($"[FlyShuttleLoom] Opening GUI for offset: {offsetKey}");
                    return beLoom.OpenGui(byPlayer);
            }

            return false;
        }

        private Vec3i NormalizeOffset(Vec3i offset, BlockFacing facing)
        {
            // Rotate the offset back to north-facing coordinates
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

        public WorldInteraction[] MBGetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection blockSel, IPlayer forPlayer, Vec3i offset)
        {
            // Get the block's facing direction
            BlockFacing facing = BlockFacing.FromCode(this.LastCodePart());
            
            // Normalize the offset to north-facing coordinates
            Vec3i normalizedOffset = NormalizeOffset(offset, facing);
            string offsetKey = $"{normalizedOffset.X},{normalizedOffset.Y},{normalizedOffset.Z}";
            
            // Check if hovering over bench (SelectionBoxIndex 1)
            bool isBenchPosition = 
                (offsetKey == "0,0,-1" || offsetKey == "-1,0,-1" || offsetKey == "1,0,-1") && 
                blockSel.SelectionBoxIndex == 1;
            
            if (isBenchPosition)
            {
                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-loom-sit",
                        MouseButton = EnumMouseButton.Right
                    }
                };
            }
            else
            {
                // Show "Open Crafting Menu" for other parts
                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-loom-opencrafting",
                        MouseButton = EnumMouseButton.Right
                    }
                };
            }
        }

        public bool MBOnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
        {
            return false; // No hold-to-use mechanic
        }

        public void MBOnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
        {
            // No action needed on stop
        }

        public ItemStack MBOnPickBlock(IWorldAccessor world, BlockPos pos, Vec3i offset)
        {
            return new ItemStack(this);
        }
        
        #endregion
        
        // --- Multi-Block Collision/Selection ---
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