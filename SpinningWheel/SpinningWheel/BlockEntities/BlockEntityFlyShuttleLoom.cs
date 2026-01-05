using System;
using System.Collections.Generic;
using SpinningWheel.GUIs;
using SpinningWheel.Inventories;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace SpinningWheel.BlockEntities
{
    public class BlockEntityFlyShuttleLoom : BlockEntityOpenableContainer, IMountableSeat, IMountable
    {
        BlockFacing facing;
        private float y2 = 0.2f;
        public bool On { get; set; }

        // Inventory and GUI
        internal InventoryFlyshuttleLoom inventory;
        GuiDialogBlockEntityLoom clientDialog;

        // Weaving progress (synced to animation duration)
        public float inputWeaveTime;
        public float prevInputWeaveTime;
        private float currentMaxWeaveTime = 8.53f; // 257 keyframes at 30 fps

        // Helper properties for inventory slots
        public ItemSlot InputSlot => inventory?[0];
        public ItemSlot OutputSlot => inventory?[1];
        public ItemStack InputStack
        {
            get => inventory?[0]?.Itemstack;
            set
            {
                if (inventory?[0] != null)
                {
                    inventory[0].Itemstack = value;
                    inventory[0].MarkDirty();
                }
            }
        }
        public ItemStack OutputStack
        {
            get => inventory?[1]?.Itemstack;
            set
            {
                if (inventory?[1] != null)
                {
                    inventory[1].Itemstack = value;
                    inventory[1].MarkDirty();
                }
            }
        }

        public EntityAgent MountedBy;
        bool blockBroken;
        long mountedByEntityId;
        string mountedByPlayerUid;
        EntityControls controls = new EntityControls();
        EntityPos mountPos = new EntityPos();
        public bool DoTeleportOnUnmount { get; set; } = true;
        
        // Animation metadata with blending for smooth transitions
        private AnimationMetaData idleAnimation = new AnimationMetaData()
        {
            Code = "sitloomidle",
            Animation = "SitLoomIdle",
            EaseInSpeed = 1000f,
            EaseOutSpeed = 1000f
        }.Init();
        private AnimationMetaData startAnimation = new AnimationMetaData()
        {
            Code = "sitloomstart",
            Animation = "SitLoomStart",
            EaseInSpeed = 1000f,
            EaseOutSpeed = 1000f
        }.Init();
        private AnimationMetaData weavingAnimation = new AnimationMetaData()
        {
            Code = "sitloomfull",
            Animation = "SitLoomFull",
            EaseInSpeed = 1000f,
            EaseOutSpeed = 1000f
        }.Init();
        
        private BlockEntityAnimationUtil animUtil
        {
            get { return GetBehavior<BEBehaviorAnimatable>()?.animUtil; }
        }

        // BlockEntityOpenableContainer properties
        public override string InventoryClassName => "flyshuttleloom";
        public override InventoryBase Inventory => inventory;
        public virtual string DialogTitle => Lang.Get("Fly Shuttle Loom");

        #region IMountable/IMountableSeat Implementation
        
        public EntityPos SeatPosition => Position;
        public double StepPitch => 0;
        
        public EntityPos Position
        {
            get
            {
                mountPos.SetPos(Pos);
                // Safety check for null facing
                if (facing == null)
                {
                    facing = BlockFacing.FromCode(Block?.LastCodePart()) ?? BlockFacing.NORTH;
                }
                mountPos.Yaw = facing.HorizontalAngleIndex * GameMath.PIHALF + GameMath.PIHALF;

                // Position player at the center front of the loom
                // Adjust based on loom dimensions: 3 wide, 2 deep
                if (facing == BlockFacing.NORTH)
                    return mountPos.Add(0.5, 0.6, 1.4);  // Center of 3-wide loom
                if (facing == BlockFacing.EAST)
                    return mountPos.Add(0.5, y2 - 0.97, 1.5);  // Rotated 90° clockwise
                if (facing == BlockFacing.SOUTH)
                    return mountPos.Add(0.5, y2 - 0.97, 0.5);  // Rotated 180°
                if (facing == BlockFacing.WEST)
                    return mountPos.Add(1.5, y2 - 0.97, 0.5);  // Rotated 270° clockwise
                return mountPos.Add(0.5, y2, 0.5); // Fallback
            }
        }
        
        public Vec3f LocalEyePos
        {
            get
            {
                if (facing == null)
                {
                    facing = BlockFacing.FromCode(Block?.LastCodePart()) ?? BlockFacing.NORTH;
                }
                // Rotate the eye position offset based on facing
                if (facing == BlockFacing.NORTH)
                    return new Vec3f(0, 1f, 0.12f);
                if (facing == BlockFacing.EAST)
                    return new Vec3f(-0.12f, 1f, 0);
                if (facing == BlockFacing.SOUTH)
                    return new Vec3f(0, 1f, -0.12f);
                if (facing == BlockFacing.WEST)
                    return new Vec3f(0.12f, 1f, 0);
                return new Vec3f(0, 1.5f, 0);
            }
        }
        
        // SuggestedAnimation dynamic based on On state
        public AnimationMetaData SuggestedAnimation => On ? weavingAnimation : idleAnimation;
        public EntityControls Controls => controls;
        public IMountable MountSupplier => this;
        public EnumMountAngleMode AngleMode => EnumMountAngleMode.FixateYaw;
        Entity IMountableSeat.Passenger => MountedBy;
        public bool CanControl => false;
        public Entity Entity => null;
        public Matrixf RenderTransform => null;
        public IMountableSeat[] Seats => new IMountableSeat[] { this };
        public bool SkipIdleAnimation => false;
        public float FpHandPitchFollow => 1;
        public string SeatId { get => "weaver-0"; set { } }
        public SeatConfig Config { get => null; set { } }
        public long PassengerEntityIdForInit { get => mountedByEntityId; set => mountedByEntityId = value; }
        public Entity Controller => MountedBy;
        public Entity OnEntity => null;
        public EntityControls ControllingControls => null;
        
        #endregion

        public BlockEntityFlyShuttleLoom()
        {
            inventory = new InventoryFlyshuttleLoom(null, null);
            inventory.SlotModified += OnSlotModified;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            
            // Get the facing direction from block code
            facing = BlockFacing.FromCode(Block.LastCodePart());

            // Initialize inventory
            inventory.LateInitialize("flyshuttleloom-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z, api);

            // Initialize controls
            controls.OnAction = onControls;

            // Register tick listeners for weaving
            RegisterGameTickListener(OnWeaveTick, 100); // Process weaving every 100ms
            RegisterGameTickListener(On500msTick, 500); // Sync to clients

            // Client-side: sync animation with On state and GUI updates
            if (api.Side == EnumAppSide.Client)
            {
                RegisterGameTickListener(OnClientAnimationTick, 100);
                RegisterGameTickListener(OnClientGuiUpdateTick, 50);
            }

            // Get collision box height
            Cuboidf[] collboxes = Block.GetCollisionBoxes(api.World.BlockAccessor, Pos);
            if (collboxes != null && collboxes.Length > 0)
            {
                y2 = collboxes[0].Y2;
            }
            
            // Remount player if they were sitting when the world was saved/reloaded
            if (MountedBy == null && (mountedByEntityId != 0 || mountedByPlayerUid != null))
            {
                var entity = mountedByPlayerUid != null 
                    ? api.World.PlayerByUid(mountedByPlayerUid)?.Entity 
                    : api.World.GetEntityById(mountedByEntityId) as EntityAgent;
                if (entity?.SidedProperties != null)
                {
                    entity.TryMount(this);
                }
            }
        }
        
        #region Controls & Mounting
        
        private void onControls(EnumEntityAction action, bool on, ref EnumHandling handled)
        {
            if (action == EnumEntityAction.Sneak && on)
            {
                MountedBy?.TryUnmount();
                controls.StopAllMovement();
                handled = EnumHandling.PassThrough;
            }
        }
        
        public void DidMount(EntityAgent entityAgent)
        {
            if (MountedBy != null && MountedBy != entityAgent)
            {
                entityAgent.TryUnmount();
                return;
            }

            if (MountedBy == entityAgent)
            {
                // Already mounted
                return;
            }

            MountedBy = entityAgent;
            mountedByPlayerUid = (entityAgent as EntityPlayer)?.PlayerUID;
            mountedByEntityId = MountedBy.EntityId;

            MarkDirty(false);
        }
        
        public void DidUnmount(EntityAgent entityAgent)
        {
            MountedBy = null;

            // Close the GUI when player dismounts
            if (Api.Side == EnumAppSide.Client && clientDialog != null)
            {
                clientDialog.TryClose();
            }

            // Instantly reset weaving progress when player dismounts
            inputWeaveTime = 0;
            if (On)
            {
                Deactivate();
            }

            if (!blockBroken)
            {
                // Try to place the player in a safe position around the loom
                foreach (BlockFacing checkFacing in BlockFacing.HORIZONTALS)
                {
                    Vec3d placePos = Pos.ToVec3d().AddCopy(checkFacing).Add(0.5, 0.001, 0.5);
                    // Check if this position is safe (not colliding with blocks)
                    if (!Api.World.CollisionTester.IsColliding(Api.World.BlockAccessor, entityAgent.SelectionBox, placePos, false))
                    {
                        entityAgent.TeleportTo(placePos);
                        break;
                    }
                }
            }

            mountedByEntityId = 0;
            mountedByPlayerUid = null;
            MarkDirty(false);
        }
        
        public bool IsMountedBy(Entity entity) => this.MountedBy == entity;
        public bool IsBeingControlled() => false;
        public bool CanUnmount(EntityAgent entityAgent) => true;
        public bool CanMount(EntityAgent entityAgent) => !AnyMounted();
        public bool AnyMounted() => MountedBy != null;
        
        #endregion
        
        #region Animation
        
        // Client-side: sync animation with server's On state
        private bool clientAnimationRunning = false;
        private void OnClientAnimationTick(float dt)
        {
            if (animUtil?.animator == null) return;

            // Start animation if On but not running
            if (On && !clientAnimationRunning)
            {
                animUtil.StartAnimation(new AnimationMetaData() { 
                    Animation = "loom_full_cycle", 
                    Code = "loom_full_cycle", 
                    AnimationSpeed = 1f
                });
                clientAnimationRunning = true;
                
                // Update player animation to weaving
                RefreshSeatAnimation();
            }
            // Stop animation if Off but running
            else if (!On && clientAnimationRunning)
            {
                animUtil.StopAnimation("loom_full_cycle");
                clientAnimationRunning = false;
                
                // Update player animation back to idle
                RefreshSeatAnimation();
            }
        }
        
        private void RefreshSeatAnimation()
        {
            if (MountedBy != null)
            {
                // Get the target animation
                var anim = SuggestedAnimation;
                if (anim != null)
                {
                    // Only stop animations that are NOT the one we're about to start
                    // This prevents the player model from snapping to T-pose during transitions
                    if (anim.Code != "sitloomidle")
                        MountedBy.AnimManager?.StopAnimation("sitloomidle");
                    if (anim.Code != "sitloomstart")
                        MountedBy.AnimManager?.StopAnimation("sitloomstart");
                    if (anim.Code != "sitloomfull")
                        MountedBy.AnimManager?.StopAnimation("sitloomfull");

                    // Start the new animation (will override if already playing)
                    MountedBy.AnimManager?.StartAnimation(anim);
                }
            }
        }
        
        // Start animation (server sets state, client will sync)
        public void Activate()
        {
            On = true;
            MarkDirty(true);
        }
        
        // Stop animation (server sets state, client will sync)
        public void Deactivate()
        {
            On = false;
            MarkDirty(true);
        }
        
        #endregion

        #region Weaving Logic

        // Animation duration constants (257 keyframes at 30 fps = 8.53 seconds)
        private const float ANIMATION_DURATION = 8.53f;

        private void OnWeaveTick(float dt)
        {
            // Client-side: just update visuals
            if (Api is ICoreClientAPI)
            {
                return;
            }

            // Server-side processing
            if (!CanWeave() || MountedBy == null)
            {
                // Stop animation if can't weave
                if (On)
                {
                    Deactivate();
                }

                // Instantly reset progress (no decay - weaving stops immediately)
                inputWeaveTime = 0;
                return;
            }

            // We can weave and player is mounted - ensure animation is running
            if (!On)
            {
                Activate();
            }

            // Set max weave time to animation duration
            currentMaxWeaveTime = ANIMATION_DURATION;

            // Process weaving - sync with animation
            inputWeaveTime += dt;

            // Check if animation cycle completed
            if (inputWeaveTime >= ANIMATION_DURATION)
            {
                WeaveInput();
                inputWeaveTime = 0; // Reset for next cycle

                // Animation will continue if there's more to weave (CanWeave() will be true)
                // Otherwise it will stop in the next tick
                MarkDirty(true);
            }
        }

        private bool CanWeave()
        {
            ItemSlot inputSlot = InputSlot;
            if (inputSlot?.Itemstack == null) return false;

            // Check if this item can be woven
            if (inputSlot.Itemstack.ItemAttributes.KeyExists("weavingProps"))
            {
                var weavingProps = inputSlot.Itemstack.ItemAttributes["weavingProps"];
                int requiredInput = weavingProps["inputQuantity"]?.AsInt(1) ?? 1;

                // Check if we have enough input items
                if (inputSlot.Itemstack.StackSize < requiredInput) return false;

                ItemSlot outputSlot = OutputSlot;

                // Check if output slot has room
                if (outputSlot.Empty) return true;

                // Check if we can stack more
                ItemStack resultStack = GetWeaveResult(inputSlot.Itemstack);
                if (resultStack != null && outputSlot.Itemstack.Collectible.Equals(outputSlot.Itemstack, resultStack, GlobalConstants.IgnoredStackAttributes))
                {
                    return outputSlot.Itemstack.StackSize < outputSlot.Itemstack.Collectible.MaxStackSize;
                }
            }

            return false;
        }

        private ItemStack GetWeaveResult(ItemStack input)
        {
            // Check if the input item has weavingProps
            if (input.ItemAttributes.KeyExists("weavingProps"))
            {
                var weavingProps = input.ItemAttributes["weavingProps"];

                // Get the outputType and outputQuantity from weavingProps
                string outputType = weavingProps["outputType"].AsString();
                int outputQuantity = weavingProps["outputQuantity"].AsInt(1);

                AssetLocation outputLocation = new AssetLocation(outputType);

                // Try to get as item first
                Item outputItem = Api.World.GetItem(outputLocation);
                if (outputItem != null)
                {
                    return new ItemStack(outputItem, outputQuantity);
                }

                // Try to get as block
                Block outputBlock = Api.World.GetBlock(outputLocation);
                if (outputBlock != null)
                {
                    return new ItemStack(outputBlock, outputQuantity);
                }

                // Output type not found
                Api.Logger.Error($"[FlyShuttleLoom] Could not find item or block with code: {outputType}");
                return null;
            }

            // No valid recipe found
            return null;
        }

        private void WeaveInput()
        {
            ItemSlot inputSlot = InputSlot;
            ItemSlot outputSlot = OutputSlot;

            ItemStack resultStack = GetWeaveResult(inputSlot.Itemstack);
            if (resultStack == null) return;

            // Get how many input items to consume
            int inputQuantity = 1; // Default
            if (inputSlot.Itemstack.ItemAttributes.KeyExists("weavingProps"))
            {
                var weavingProps = inputSlot.Itemstack.ItemAttributes["weavingProps"];
                inputQuantity = weavingProps["inputQuantity"]?.AsInt(1) ?? 1;
            }

            // Add output
            if (outputSlot.Empty)
            {
                outputSlot.Itemstack = resultStack;
            }
            else
            {
                outputSlot.Itemstack.StackSize += resultStack.StackSize;
            }

            // Consume the correct amount of input
            inputSlot.TakeOut(inputQuantity);
            inputSlot.MarkDirty();
            outputSlot.MarkDirty();
        }

        #endregion

        #region Slot Management

        private void OnSlotModified(int slotid)
        {
            if (Api is ICoreClientAPI)
            {
                clientDialog?.Update(inputWeaveTime, currentMaxWeaveTime);
            }

            // Check animation state when slots change (server-side)
            if (Api is ICoreServerAPI)
            {
                bool canWeave = CanWeave();
                bool isPlayerMounted = MountedBy != null;
                bool shouldBeWeaving = canWeave && isPlayerMounted;

                if (shouldBeWeaving && !On)
                {
                    Activate();
                }
                else if (!shouldBeWeaving && On)
                {
                    Deactivate();
                }
            }

            if (slotid == 0)
            {
                if (InputSlot.Empty)
                {
                    inputWeaveTime = 0.0f;
                    currentMaxWeaveTime = ANIMATION_DURATION;
                }
                MarkDirty();

                if (clientDialog != null && clientDialog.IsOpened())
                {
                    clientDialog.SingleComposer.ReCompose();
                }
            }
        }

        #endregion

        #region Sync & Updates

        // Sync to client every 500ms
        private void On500msTick(float dt)
        {
            // Server: Mark dirty if weaving
            if (Api is ICoreServerAPI && (MountedBy != null || inputWeaveTime > 0))
            {
                MarkDirty();
            }
        }

        private void OnClientGuiUpdateTick(float dt)
        {
            if (clientDialog != null && clientDialog.IsOpened())
            {
                clientDialog.Update(inputWeaveTime, currentMaxWeaveTime);
            }
        }

        #endregion

        #region Player Interaction

        public bool OnPlayerInteract(IPlayer byPlayer)
        {
            // Check if someone is already using it
            if (MountedBy != null) 
            {
                if (Api.Side == EnumAppSide.Client)
                {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "occupied", 
                        "Someone is already using this loom");
                }
                return false;
            }

            // Mount the player
            return byPlayer.Entity.TryMount(this);
        }
        
        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            // This method is required by the abstract base class but won't be called directly
            // because the Block class implements IMultiBlockInteract which takes precedence.
            // However, we still need it for fallback cases or if called programmatically.
    
            // Default to opening GUI
            return OpenGui(byPlayer);
        }

        // Add new method for GUI opening
        public bool OpenGui(IPlayer player)
        {
            Api.Logger.Debug($"[FlyShuttleLoom] OpenGui called, Side: {Api.Side}");

            if (Api.Side == EnumAppSide.Client)
            {
                Api.Logger.Debug("[FlyShuttleLoom] On client side, opening dialog");

                // Open the GUI
                if (clientDialog == null || !clientDialog.IsOpened())
                {
                    toggleInventoryDialogClient(player, () =>
                    {
                        clientDialog = new GuiDialogBlockEntityLoom(
                            DialogTitle,
                            Inventory,
                            Pos,
                            Api as ICoreClientAPI
                        );
                        Api.Logger.Debug("[FlyShuttleLoom] Dialog created");
                        return clientDialog;
                    });
                }
            }
            else
            {
                Api.Logger.Debug("[FlyShuttleLoom] On server side, returning true");
            }

            return true;
        }

        #endregion
        
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (animUtil?.animator == null)
            {
                // Get the facing before initializing
                if (facing == null)
                {
                    facing = BlockFacing.FromCode(Block?.LastCodePart()) ?? BlockFacing.NORTH;
                }
                // Initialize with the block's rotation
                animUtil?.InitializeAnimator("flyshuttleloom", null, null, GetRotation());
            }

            return base.OnTesselation(mesher, tessThreadTesselator);
        }
        
        private Vec3f GetRotation()
        {
            if (facing == null)
            {
                facing = BlockFacing.FromCode(Block?.LastCodePart()) ?? BlockFacing.NORTH;
            }

            // Match the rotateY values from JSON shapeByType
            float yRotation = 0f;
            if (facing == BlockFacing.NORTH)
                yRotation = 0f;
            else if (facing == BlockFacing.EAST)
                yRotation = 270f;
            else if (facing == BlockFacing.SOUTH)
                yRotation = 180f;
            else if (facing == BlockFacing.WEST)
                yRotation = 90f;
            
            return new Vec3f(0, yRotation, 0);
        }
        
        #region Serialization
        
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            mountedByEntityId = tree.GetLong("mountedByEntityId");
            mountedByPlayerUid = tree.GetString("mountedByPlayerUid");
            inputWeaveTime = tree.GetFloat("inputWeaveTime");
            currentMaxWeaveTime = tree.GetFloat("currentMaxWeaveTime", ANIMATION_DURATION);
            On = tree.GetBool("On");

            if (Api != null)
            {
                Inventory.AfterBlocksLoaded(Api.World);
            }

            if (Api?.Side == EnumAppSide.Client)
            {
                if (clientDialog != null)
                {
                    clientDialog.Update(inputWeaveTime, currentMaxWeaveTime);
                }

                // Refresh animations when state is loaded (fixes desync on world load)
                // Use a delayed task to ensure the player is fully mounted
                if (MountedBy != null)
                {
                    (Api as ICoreClientAPI)?.Event.EnqueueMainThreadTask(() =>
                    {
                        RefreshSeatAnimation();
                        // Also ensure block animation state matches
                        if (On && !clientAnimationRunning && animUtil?.animator != null)
                        {
                            animUtil.StartAnimation(new AnimationMetaData()
                            {
                                Animation = "loom_full_cycle",
                                Code = "loom_full_cycle",
                                AnimationSpeed = 1f
                            });
                            clientAnimationRunning = true;
                        }
                    }, "refreshloomanimations");
                }
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetLong("mountedByEntityId", mountedByEntityId);
            tree.SetString("mountedByPlayerUid", mountedByPlayerUid);
            tree.SetFloat("inputWeaveTime", inputWeaveTime);
            tree.SetFloat("currentMaxWeaveTime", currentMaxWeaveTime);
            tree.SetBool("On", On);
        }
        
        public void MountableToTreeAttributes(TreeAttribute tree)
        {
            tree.SetString("className", "flyShuttleLoom");
            tree.SetInt("posx", Pos.X);
            tree.SetInt("posy", Pos.InternalY);
            tree.SetInt("posz", Pos.Z);
        }
        
        #endregion
        
        #region Block Events
        
        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            blockBroken = true;
            MountedBy?.TryUnmount();

            if (clientDialog != null)
            {
                clientDialog.TryClose();
                clientDialog?.Dispose();
                clientDialog = null;
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);
            blockBroken = true;
            MountedBy?.TryUnmount();

            if (clientDialog != null)
            {
                clientDialog.TryClose();
                clientDialog?.Dispose();
                clientDialog = null;
            }
        }
        
        #endregion

        #region Collection Mappings

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            foreach (var slot in Inventory)
            {
                if (slot.Itemstack == null) continue;
                if (slot.Itemstack.Class == EnumItemClass.Item)
                {
                    itemIdMapping[slot.Itemstack.Item.Id] = slot.Itemstack.Item.Code;
                }
                else
                {
                    blockIdMapping[slot.Itemstack.Block.BlockId] = slot.Itemstack.Block.Code;
                }

                slot.Itemstack.Collectible.OnStoreCollectibleMappings(Api.World, slot, blockIdMapping, itemIdMapping);
            }
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            base.OnLoadCollectibleMappings(worldForResolve, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);

            foreach (var slot in Inventory)
            {
                if (slot.Itemstack == null) continue;
                if (!slot.Itemstack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve))
                {
                    slot.Itemstack = null;
                }
                else
                {
                    slot.Itemstack.Collectible.OnLoadCollectibleMappings(worldForResolve, slot, oldBlockIdMapping, oldItemIdMapping, resolveImports);
                }
            }
        }

        #endregion
    }
}