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

        public bool MBDoPartialSelection(IWorldAccessor world, BlockPos pos, Vec3i offset)
        {
            return true; // Enable partial selection for all parts
        }

        public bool MBOnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
        {
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

            // The offset passed in from BlockMultiblock is already in world-space (OffsetInv),
            // so we compare directly using the facing to disambiguate seat vs non-seat pieces.
            BlockFacing facing = BlockFacing.FromCode(this.LastCodePart());
            bool isSeat = IsSeatOffset(offset, facing);
            //world.Api.Logger.Debug($"[SpinningWheel] Interact - Facing: {facing.Code}, Offset: {offset.X},{offset.Y},{offset.Z}, IsSeat: {isSeat}");
            if (isSeat)
                return beSpinningWheel.OnPlayerInteract(byPlayer);
            else
                return beSpinningWheel.OpenGui(byPlayer);
        }

        /// <summary>
        /// Returns true if the given world-space offset (from clicked piece back to control block)
        /// corresponds to one of the two seat pieces for the given facing.
        /// Offsets derived from the collisionSelectionBoxesByType JSON seat entries.
        /// </summary>
        private bool IsSeatOffset(Vec3i offset, BlockFacing facing)
        {
            if (offset.Y != 0) return false;

            int x = offset.X;
            int z = offset.Z;

            switch (facing.Code)
            {
                case "north":
                    return (x ==  0 && z == -1) || (x == -1 && z == -1);
                case "east":
                    return (x ==  1 && z ==  0) || (x ==  1 && z == -1);
                case "south":
                    return (x ==  0 && z ==  1) || (x ==  1 && z ==  1);
                case "west":
                    return (x == -1 && z ==  0) || (x == -1 && z ==  1);
                default:
                    return false;
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
            BlockFacing facing = BlockFacing.FromCode(this.LastCodePart());
            bool isSeat = IsSeatOffset(offset, facing);
            //world.Api.Logger.Debug($"[SpinningWheel] InteractionHelp - Facing: {facing.Code}, Offset: {offset.X},{offset.Y},{offset.Z}, IsSeat: {isSeat}");
            var interactions = new List<WorldInteraction>();

            if (IsSeatOffset(offset, facing))
            {
                interactions.Add(new WorldInteraction()
                {
                    ActionLangCode = "spinningwheel:blockhelp-spinningwheel-use",
                    MouseButton = EnumMouseButton.Right
                });
            }
            else
            {
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

        // --- Multi-Block Collision/Selection ---
        #region Multi-Block Collision/Selection

        public override bool DoPartialSelection(IWorldAccessor world, BlockPos pos)
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