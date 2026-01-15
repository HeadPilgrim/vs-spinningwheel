using System;
using System.Collections.Generic;
using System.IO;
using SpinningWheel.GUIs;
using SpinningWheel.Inventories;
using SpinningWheel.Recipes;
using SpinningWheel.ModSystem;
using SpinningWheel.ModConfig;
using SpinningWheel.BlockEntityPackets;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using ProtoBuf;

namespace SpinningWheel.BlockEntities
{
    // Weaving mode enum
    public enum WeavingMode
    {
        Normal = 0,
        Pattern = 1
    }

    public class BlockEntityFlyShuttleLoom : BlockEntityOpenableContainer, IMountableSeat, IMountable
    {
        BlockFacing facing;
        private float y2 = 0.2f;
        public bool On { get; set; }

        protected ILoadedSound ambientSound;

        // Inventory and GUI
        internal InventoryFlyshuttleLoom inventory;
        GuiDialogBlockEntityLoom clientDialog;

        // Weaving progress (synced to animation duration)
        public float inputWeaveTime;
        public float prevInputWeaveTime;
        private float currentMaxWeaveTime = 8.53f; // 257 keyframes at 30 fps

        // Helper properties for inventory slots
        // Slots 0-2 are input, slot 3 is output
        public ItemSlot InputSlot1 => inventory?[0];
        public ItemSlot InputSlot2 => inventory?[1];
        public ItemSlot InputSlot3 => inventory?[2];
        public ItemSlot OutputSlot => inventory?[3];

        // Helper to get first non-empty input slot
        private ItemSlot GetFirstInputSlotWithItem()
        {
            if (InputSlot1?.Itemstack != null) return InputSlot1;
            if (InputSlot2?.Itemstack != null) return InputSlot2;
            if (InputSlot3?.Itemstack != null) return InputSlot3;
            return null;
        }

        // Helper to check if any input slot has items
        private bool HasInputItems()
        {
            return InputSlot1?.Itemstack != null ||
                   InputSlot2?.Itemstack != null ||
                   InputSlot3?.Itemstack != null;
        }

        // Pattern slot properties (slots 4-7 for 2x2 grid)
        public ItemSlot PatternSlotTopLeft => inventory?[4];
        public ItemSlot PatternSlotTopRight => inventory?[5];
        public ItemSlot PatternSlotBottomLeft => inventory?[6];
        public ItemSlot PatternSlotBottomRight => inventory?[7];

        // Weaving mode state
        private WeavingMode currentWeavingMode = WeavingMode.Normal;
        private bool hasPatternWeavingEnabled = false;

        public WeavingMode CurrentWeavingMode => currentWeavingMode;
        public bool HasPatternWeavingEnabled => hasPatternWeavingEnabled;

        // Helper to get all input slots as a list
        private ItemSlot[] GetAllInputSlots()
        {
            return new ItemSlot[] { InputSlot1, InputSlot2, InputSlot3 };
        }

        // Helper to calculate total quantity of weavable items across all input slots
        // Returns 0 if slots contain different items or items without weavingProps
        private int GetTotalWeavableQuantity()
        {
            ItemStack referenceStack = null;
            int totalQuantity = 0;

            foreach (var slot in GetAllInputSlots())
            {
                if (slot?.Itemstack == null) continue;

                // Check if this item has weavingProps
                if (!slot.Itemstack.ItemAttributes.KeyExists("weavingProps"))
                {
                    return 0; // Mixed items - invalid
                }

                // First item sets the reference
                if (referenceStack == null)
                {
                    referenceStack = slot.Itemstack;
                    totalQuantity = slot.Itemstack.StackSize;
                }
                else
                {
                    // Check if this is the same item type as the reference
                    if (!slot.Itemstack.Collectible.Equals(slot.Itemstack, referenceStack, GlobalConstants.IgnoredStackAttributes))
                    {
                        return 0; // Different items - invalid
                    }
                    totalQuantity += slot.Itemstack.StackSize;
                }
            }

            return totalQuantity;
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
                // Player sits at the working side of the loom, facing into it
                if (facing == BlockFacing.NORTH)
                    return mountPos.Add(0.5, 0.6, 1.4);   // Player south of loom, facing north
                if (facing == BlockFacing.EAST)
                    return mountPos.Add(-0.4, 0.6, 0.5);  // Player west of loom, facing east
                if (facing == BlockFacing.SOUTH)
                    return mountPos.Add(0.5, 0.6, -0.4);  // Player north of loom, facing south
                if (facing == BlockFacing.WEST)
                    return mountPos.Add(1.4, 0.6, 0.5);   // Player east of loom, facing west
                return mountPos.Add(0.5, 0.6, 0.5); // Fallback
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

            // Check if pattern weaving is enabled
            var modSystem = api.ModLoader.GetModSystem<SpinningWheelModSystem>();
            hasPatternWeavingEnabled = modSystem?.HasPatternWeavingEnabled ?? false;

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

        public void ToggleAmbientSound(bool on)
        {
            Api?.Logger.Debug($"[Loom Sound] ToggleAmbientSound called with on={on}, Api.Side={Api?.Side}");

            if (Api?.Side != EnumAppSide.Client)
            {
                Api?.Logger.Debug("[Loom Sound] Not client side, returning");
                return;
            }

            if (on)
            {
                Api.Logger.Debug($"[Loom Sound] Turning ON - ambientSound null? {ambientSound == null}, IsPlaying? {ambientSound?.IsPlaying}");

                // If we already have a playing sound, nothing to do
                if (ambientSound != null && ambientSound.IsPlaying)
                {
                    Api.Logger.Debug("[Loom Sound] Sound already playing");
                    return;
                }

                // Dispose any existing sound (might be stopped/fading)
                if (ambientSound != null)
                {
                    Api.Logger.Debug("[Loom Sound] Disposing old sound");
                    ambientSound.Stop();
                    ambientSound.Dispose();
                    ambientSound = null;
                }

                // Create new sound
                Api.Logger.Debug("[Loom Sound] Loading new sound...");
                ambientSound = ((IClientWorldAccessor)Api.World).LoadSound(new SoundParams()
                {
                    Location = new AssetLocation("spinningwheel:sounds/block/flyshuttle-loom-cycle.ogg"),
                    ShouldLoop = true,
                    Position = Pos.ToVec3f().Add(0.5f, 0.5f, 0.5f),
                    DisposeOnFinish = false,
                    Volume = 0.5f,
                    Range = 6,
                    SoundType = EnumSoundType.Ambient
                });

                Api.Logger.Debug($"[Loom Sound] LoadSound returned: {(ambientSound == null ? "NULL" : "valid sound")}");

                if (ambientSound != null)
                {
                    ambientSound.Start();
                    Api.Logger.Debug($"[Loom Sound] Sound started, IsPlaying={ambientSound.IsPlaying}");
                }
                else
                {
                    Api.Logger.Error("[Loom Sound] Failed to load sound!");
                }
            }
            else
            {
                Api.Logger.Debug($"[Loom Sound] Turning OFF - ambientSound null? {ambientSound == null}");
                if (ambientSound != null)
                {
                    ambientSound.Stop();
                    ambientSound.Dispose();
                    ambientSound = null;
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
            }

            // Always pass through to allow normal input handling
            handled = EnumHandling.PassThrough;
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
                // Calculate dismount position at the front of the loom (where the player was sitting)
                // The loom is 2 blocks deep, and the player sits at the front
                Vec3d frontPos = GetFrontDismountPosition();

                // Try to place the player at the front first, then check sides
                BlockFacing[] dismountOrder = GetDismountFacingOrder();

                foreach (BlockFacing checkFacing in dismountOrder)
                {
                    Vec3d placePos = frontPos.AddCopy(checkFacing.Normalf.X, 0, checkFacing.Normalf.Z).Add(0, 0.001, 0);
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

        /// <summary>
        /// Gets the position at the front of the loom where the player should dismount
        /// </summary>
        private Vec3d GetFrontDismountPosition()
        {
            Vec3d basePos = Pos.ToVec3d();

            // Return position at the front of the loom based on facing
            // Player sits at the front, so dismount should be there
            if (facing == BlockFacing.NORTH)
                return basePos.Add(0.5, 0, 1.5);   // Front is south side
            if (facing == BlockFacing.EAST)
                return basePos.Add(-0.5, 0, 0.5);  // Front is west side
            if (facing == BlockFacing.SOUTH)
                return basePos.Add(0.5, 0, -0.5);  // Front is north side
            if (facing == BlockFacing.WEST)
                return basePos.Add(1.5, 0, 0.5);   // Front is east side

            return basePos.Add(0.5, 0, 0.5); // Fallback
        }

        /// <summary>
        /// Gets the order of facings to try for dismount, prioritizing forward from the loom
        /// </summary>
        private BlockFacing[] GetDismountFacingOrder()
        {
            // First try directly in front (away from loom), then sides, then back
            if (facing == BlockFacing.NORTH)
                return new[] { BlockFacing.SOUTH, BlockFacing.EAST, BlockFacing.WEST, BlockFacing.NORTH };
            if (facing == BlockFacing.EAST)
                return new[] { BlockFacing.WEST, BlockFacing.NORTH, BlockFacing.SOUTH, BlockFacing.EAST };
            if (facing == BlockFacing.SOUTH)
                return new[] { BlockFacing.NORTH, BlockFacing.EAST, BlockFacing.WEST, BlockFacing.SOUTH };
            if (facing == BlockFacing.WEST)
                return new[] { BlockFacing.EAST, BlockFacing.NORTH, BlockFacing.SOUTH, BlockFacing.WEST };

            return BlockFacing.HORIZONTALS;
        }
        
        public bool IsMountedBy(Entity entity) => this.MountedBy == entity;
        public bool IsBeingControlled() => false;
        public bool CanUnmount(EntityAgent entityAgent) => true;
        public bool CanMount(EntityAgent entityAgent) => !AnyMounted();
        public bool AnyMounted() => MountedBy != null;

        private bool CanPlayerUseLoom(IPlayer player)
        {
            // Use server config (or default if not loaded yet)
            if (ModConfig.ModConfig.Loaded == null || !ModConfig.ModConfig.Loaded.RequireTailorClass)
            {
                return true;
            }

            // Get player's class
            string playerClass = player.Entity.WatchedAttributes.GetString("characterClass", "").ToLower();

            // Check if player's class is in the allowed list
            foreach (string allowedClass in ModConfig.ModConfig.Loaded.AllowedClasses)
            {
                if (playerClass == allowedClass.ToLower())
                {
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Animation
        
        // Client-side: sync animation with server's On state
        private bool clientAnimationRunning = false;
        private void OnClientAnimationTick(float dt)
        {
            if (animUtil?.animator == null)
            {
                Api?.Logger.VerboseDebug("[Loom Sound] OnClientAnimationTick: animUtil or animator is null");
                return;
            }

            // Start animation if On but not running
            if (On && !clientAnimationRunning)
            {
                Api?.Logger.Debug($"[Loom Sound] Starting animation - On={On}, clientAnimationRunning={clientAnimationRunning}");
                animUtil.StartAnimation(new AnimationMetaData() {
                    Animation = "loom_full_cycle",
                    Code = "loom_full_cycle",
                    AnimationSpeed = 1f
                });
                clientAnimationRunning = true;

                // Start sound when animation starts
                Api?.Logger.Debug("[Loom Sound] Calling ToggleAmbientSound(true)");
                ToggleAmbientSound(true);

                // Update player animation to weaving
                RefreshSeatAnimation();
            }
            // Stop animation if Off but running
            else if (!On && clientAnimationRunning)
            {
                Api?.Logger.Debug($"[Loom Sound] Stopping animation - On={On}, clientAnimationRunning={clientAnimationRunning}");
                animUtil.StopAnimation("loom_full_cycle");
                clientAnimationRunning = false;

                // Stop sound when animation stops
                Api?.Logger.Debug("[Loom Sound] Calling ToggleAmbientSound(false)");
                ToggleAmbientSound(false);

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
            // Log current mode
            //Api?.Logger.Notification($"[Loom] CanWeave called - Current mode: {currentWeavingMode}");

            // Route to appropriate weaving logic based on mode
            if (currentWeavingMode == WeavingMode.Normal)
            {
                //Api?.Logger.Notification("[Loom] Routing to CanWeaveNormal");
                return CanWeaveNormal();
            }
            else if (currentWeavingMode == WeavingMode.Pattern)
            {
                //Api?.Logger.Notification("[Loom] Routing to CanWeavePattern");
                return CanWeavePattern();
            }

            //Api?.Logger.Warning($"[Loom] Unknown weaving mode: {currentWeavingMode}");
            return false;
        }

        private bool CanWeaveNormal()
        {
            // Check if we have any input items
            if (!HasInputItems()) return false;

            // Get the first slot with items to check weavingProps
            ItemSlot inputSlot = GetFirstInputSlotWithItem();
            if (inputSlot?.Itemstack == null) return false;

            // Check if this item can be woven
            if (!inputSlot.Itemstack.ItemAttributes.KeyExists("weavingProps")) return false;

            var weavingProps = inputSlot.Itemstack.ItemAttributes["weavingProps"];
            int requiredInput = weavingProps["inputQuantity"]?.AsInt(1) ?? 1;

            // Calculate total quantity across all input slots
            int totalQuantity = GetTotalWeavableQuantity();

            // Check if we have enough total input items across all slots
            if (totalQuantity < requiredInput) return false;

            ItemSlot outputSlot = OutputSlot;

            // Check if output slot has room
            if (outputSlot.Empty) return true;

            // Check if we can stack more
            ItemStack resultStack = GetWeaveResult(inputSlot.Itemstack);
            if (resultStack != null && outputSlot.Itemstack.Collectible.Equals(outputSlot.Itemstack, resultStack, GlobalConstants.IgnoredStackAttributes))
            {
                return outputSlot.Itemstack.StackSize < outputSlot.Itemstack.Collectible.MaxStackSize;
            }

            return false;
        }

        private bool CanWeavePattern()
        {
            // Log current pattern slots (even if empty/incomplete)
            /*
            Api?.Logger.Notification($"[Loom] Pattern check - Current slots:");
            Api?.Logger.Notification($"  Top-Left: {(PatternSlotTopLeft?.Itemstack != null ? $"{PatternSlotTopLeft.Itemstack.Collectible.Code} (qty: {PatternSlotTopLeft.Itemstack.StackSize})" : "EMPTY")}");
            Api?.Logger.Notification($"  Top-Right: {(PatternSlotTopRight?.Itemstack != null ? $"{PatternSlotTopRight.Itemstack.Collectible.Code} (qty: {PatternSlotTopRight.Itemstack.StackSize})" : "EMPTY")}");
            Api?.Logger.Notification($"  Bottom-Left: {(PatternSlotBottomLeft?.Itemstack != null ? $"{PatternSlotBottomLeft.Itemstack.Collectible.Code} (qty: {PatternSlotBottomLeft.Itemstack.StackSize})" : "EMPTY")}");
            Api?.Logger.Notification($"  Bottom-Right: {(PatternSlotBottomRight?.Itemstack != null ? $"{PatternSlotBottomRight.Itemstack.Collectible.Code} (qty: {PatternSlotBottomRight.Itemstack.StackSize})" : "EMPTY")}");
            */

            if (!hasPatternWeavingEnabled)
            {
                return false;
            }

            // Check if all 4 pattern slots have items
            if (PatternSlotTopLeft?.Itemstack == null || PatternSlotTopRight?.Itemstack == null ||
                PatternSlotBottomLeft?.Itemstack == null || PatternSlotBottomRight?.Itemstack == null)
            {
                //Api?.Logger.Notification("[Loom] Not all pattern slots filled - cannot weave");
                return false;
            }

            // Get matching recipe
            var recipe = GetMatchingPatternRecipe();
            if (recipe == null)
            {
                return false;
            }

            // Check if each slot has enough quantity
            if (!recipe.HasSufficientInput(
                PatternSlotTopLeft.Itemstack,
                PatternSlotTopRight.Itemstack,
                PatternSlotBottomLeft.Itemstack,
                PatternSlotBottomRight.Itemstack))
                return false;

            ItemSlot outputSlot = OutputSlot;

            // Check if output slot has room
            if (outputSlot.Empty) return true;

            // Check if we can stack more
            ItemStack resultStack = recipe.GetOutput(Api);
            if (resultStack != null && outputSlot.Itemstack.Collectible.Equals(outputSlot.Itemstack, resultStack, GlobalConstants.IgnoredStackAttributes))
            {
                return outputSlot.Itemstack.StackSize < outputSlot.Itemstack.Collectible.MaxStackSize;
            }

            return false;
        }

        private LoomPatternRecipe GetMatchingPatternRecipe()
        {
            var modSystem = Api.ModLoader.GetModSystem<SpinningWheelModSystem>();
            var patternLoader = modSystem?.PatternRecipeLoader;

            if (patternLoader == null)
            {
                Api?.Logger.Warning("[Loom] Pattern loader is null");
                return null;
            }

            Api?.Logger.Notification($"[Loom] Checking against {patternLoader.PatternRecipes.Count} pattern recipes");

            return patternLoader.FindMatchingRecipe(
                PatternSlotTopLeft?.Itemstack,
                PatternSlotTopRight?.Itemstack,
                PatternSlotBottomLeft?.Itemstack,
                PatternSlotBottomRight?.Itemstack);
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
            if (currentWeavingMode == WeavingMode.Normal)
            {
                WeaveInputNormal();
            }
            else if (currentWeavingMode == WeavingMode.Pattern)
            {
                WeaveInputPattern();
            }
        }

        private void WeaveInputNormal()
        {
            // Get the first slot with items to check recipe
            ItemSlot firstInputSlot = GetFirstInputSlotWithItem();
            if (firstInputSlot == null) return;

            ItemSlot outputSlot = OutputSlot;

            ItemStack resultStack = GetWeaveResult(firstInputSlot.Itemstack);
            if (resultStack == null) return;

            // Get how many input items to consume total
            int inputQuantity = 1; // Default
            if (firstInputSlot.Itemstack.ItemAttributes.KeyExists("weavingProps"))
            {
                var weavingProps = firstInputSlot.Itemstack.ItemAttributes["weavingProps"];
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

            // Consume items from slots in order until we've consumed enough
            int remainingToConsume = inputQuantity;
            foreach (var slot in GetAllInputSlots())
            {
                if (slot?.Itemstack == null || remainingToConsume <= 0) continue;

                int toTakeFromThisSlot = Math.Min(remainingToConsume, slot.Itemstack.StackSize);
                slot.TakeOut(toTakeFromThisSlot);
                slot.MarkDirty();
                remainingToConsume -= toTakeFromThisSlot;
            }

            outputSlot.MarkDirty();
        }

        private void WeaveInputPattern()
        {
            var recipe = GetMatchingPatternRecipe();
            if (recipe == null) return;

            ItemSlot outputSlot = OutputSlot;
            ItemStack resultStack = recipe.GetOutput(Api);
            if (resultStack == null) return;

            // Add output
            if (outputSlot.Empty)
            {
                outputSlot.Itemstack = resultStack;
            }
            else
            {
                outputSlot.Itemstack.StackSize += resultStack.StackSize;
            }

            // Consume from all 4 pattern slots
            int quantityPerSlot = recipe.QuantityPerSlot;
            PatternSlotTopLeft.TakeOut(quantityPerSlot);
            PatternSlotTopRight.TakeOut(quantityPerSlot);
            PatternSlotBottomLeft.TakeOut(quantityPerSlot);
            PatternSlotBottomRight.TakeOut(quantityPerSlot);

            PatternSlotTopLeft.MarkDirty();
            PatternSlotTopRight.MarkDirty();
            PatternSlotBottomLeft.MarkDirty();
            PatternSlotBottomRight.MarkDirty();
            outputSlot.MarkDirty();
        }

        /// <summary>
        /// Sets the weaving mode and resets progress when switching
        /// </summary>
        public void SetWeavingMode(WeavingMode mode)
        {
            if (currentWeavingMode != mode)
            {
                currentWeavingMode = mode;

                // Reset progress when switching modes
                inputWeaveTime = 0;

                // Update max weave time - always use standard animation duration
                currentMaxWeaveTime = ANIMATION_DURATION;

                // Stop animation if running
                if (On)
                {
                    Deactivate();
                }

                MarkDirty(true);
            }
        }

        // Packet handler for receiving weaving mode changes from client
        private const int PACKET_ID_SET_WEAVING_MODE = 1001;

        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(player, packetid, data);

            if (packetid == PACKET_ID_SET_WEAVING_MODE && data != null)
            {
                using (MemoryStream ms = new MemoryStream(data))
                {
                    var packet = Serializer.Deserialize<SetWeavingModePacket>(ms);
                    WeavingMode newMode = (WeavingMode)packet.WeavingMode;
                    
                    SetWeavingMode(newMode);
                }
            }
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

            // Handle changes to any input slot (0, 1, or 2) for normal mode
            if (slotid >= 0 && slotid <= 2)
            {
                // If all input slots are empty, reset progress
                if (!HasInputItems())
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

            // Handle changes to any pattern slot (4, 5, 6, or 7) for pattern mode
            if (slotid >= 4 && slotid <= 7)
            {
                // Reset progress when pattern changes
                inputWeaveTime = 0;
                currentMaxWeaveTime = ANIMATION_DURATION;

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
            // Check if player has the required class (validated on both sides)
            if (!CanPlayerUseLoom(byPlayer))
            {
                if (Api.Side == EnumAppSide.Client)
                {
                    string requiredClasses = string.Join(", ", ModConfig.ModConfig.Loaded.AllowedClasses);
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "wrongclass",
                        Lang.Get("spinningwheel:loom-requires-class", requiredClasses));
                }
                return false;
            }

            // Check if someone is already using it
            if (MountedBy != null)
            {
                if (Api.Side == EnumAppSide.Client)
                {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "occupied",
                        Lang.Get("spinningwheel:loom-occupied"));
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

            if (Api.Side == EnumAppSide.Client)
            {

                // Check if player has the required class first
                if (!CanPlayerUseLoom(player))
                {
                    string requiredClasses = string.Join(", ", ModConfig.ModConfig.Loaded.AllowedClasses);
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "wrongclass",
                        Lang.Get("spinningwheel:loom-requires-class", requiredClasses));
                    return false;
                }

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
                        return clientDialog;
                    });
                }
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

            // Restore weaving mode
            currentWeavingMode = (WeavingMode)tree.GetInt("weavingMode", 0);

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
                            Api?.Logger.Debug("[Loom Sound] FromTreeAttributes starting animation and sound");
                            animUtil.StartAnimation(new AnimationMetaData()
                            {
                                Animation = "loom_full_cycle",
                                Code = "loom_full_cycle",
                                AnimationSpeed = 1f
                            });
                            clientAnimationRunning = true;
                            ToggleAmbientSound(true);
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

            // Save weaving mode
            tree.SetInt("weavingMode", (int)currentWeavingMode);
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
            ToggleAmbientSound(false);

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
            ToggleAmbientSound(false);
            base.OnBlockUnloaded();
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            ToggleAmbientSound(false);

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