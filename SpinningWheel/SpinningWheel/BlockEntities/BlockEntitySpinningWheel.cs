using System.Collections.Generic;
using System.Linq;
using SpinningWheel.BLockEntityRenderer;
using SpinningWheel.Blocks;
using SpinningWheel.GUIs;
using SpinningWheel.Inventories;
using SpinningWheel.ModConfig;
using SpinningWheel.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace SpinningWheel.BlockEntities;

#nullable disable

public class BlockEntitySpinningWheel : BlockEntityOpenableContainer, IMountableSeat, IMountable
{
    internal InventorySpinningWheel inventory;
    
    protected ILoadedSound ambientSound;
    
    SpinningWheelContentsRenderer renderer;
    bool shouldRedraw;
    
    BlockFacing facing;
    private float blockRotationDeg = 0f;
    private float y2 = 0.2f;
    public EntityAgent MountedBy;
    bool blockBroken;
    long mountedByEntityId;
    string mountedByPlayerUid;
    EntityControls controls = new EntityControls();
    EntityPos mountPos = new EntityPos();
    public bool DoTeleportOnUnmount { get; set; } = true;
    
    GuiDialogBlockEntitySpinningWheel clientDialog;
    
    // Animation metadata
    private AnimationMetaData idleAnimation = new AnimationMetaData() { Code = "sitidle2", Animation = "SitIdle2" }.Init();
    private AnimationMetaData spinningAnimation = new AnimationMetaData() { Code = "hp-sitspinning", Animation = "HP-SitSpinning" }.Init();

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

            // Position player at the center of the 2x2x2 multiblock
            // The center is 1 block (in X and Z) from the control position
            if (facing == BlockFacing.NORTH)
                return mountPos.Add(1.0, y2 - 0.97, 0.95);  // cposition (0,0,0) + (1,0,1) = center
            if (facing == BlockFacing.EAST)
                return mountPos.Add(0.05, y2 - 0.97, 1.0);  // Rotated 90° clockwise : cposition (1,0,0) + (-1,0,1) = center
            if (facing == BlockFacing.SOUTH)
                return mountPos.Add(0.0, y2 - 0.97, 0.05);  // Rotated 180° : cposition (1,0,1) + (-1,0,-1) = center
            if (facing == BlockFacing.WEST)
                return mountPos.Add(0.95, y2 - 0.97, 0.0);  // Rotated 270° clockwise : cposition (0,0,1) + (1,0,-1) = center
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
                return new Vec3f(0, 1.5f, 0.12f);
            if (facing == BlockFacing.EAST)
                return new Vec3f(-0.12f, 1.5f, 0);      // Z becomes X
            if (facing == BlockFacing.SOUTH)
                return new Vec3f(0, 1.5f, -0.12f);     // Z inverts
            if (facing == BlockFacing.WEST)
                return new Vec3f(0.12f, 1.5f, 0);     // X becomes -Z
            return new Vec3f(0, 1.5f, 0);
        }
    }
    
    // SuggestedAnimation dynamic based on On state:
    public AnimationMetaData SuggestedAnimation => On ? spinningAnimation : idleAnimation;
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
    public string SeatId { get => "spinner-0"; set { } }
    public SeatConfig Config { get => null; set { } }
    public long PassengerEntityIdForInit { get => mountedByEntityId; set => mountedByEntityId = value; }
    public Entity Controller => MountedBy;
    public Entity OnEntity => null;
    public EntityControls ControllingControls => null;
    
    #endregion
    
    #region Config
    
    // For how long the item has been spinning
    public float inputSpinTime;
    public float prevInputSpinTime;
    private float currentMaxSpinTime = 4f;
    public bool On { get; set; }
    
    public override string InventoryClassName => "spinningwheel";
    
    public override InventoryBase Inventory
    {
        get { return inventory; }
    }

    public virtual string DialogTitle => Lang.Get("spinningwheel");
    
    // Seconds it requires to complete spinning
    public virtual float maxSpinTime()
    {
        // Check if there's an item in the input slot
        if (InputSlot?.Itemstack?.ItemAttributes != null)
        {
            var itemAttrs = InputSlot.Itemstack.ItemAttributes;
        
            // Check for spinningProps
            if (itemAttrs.KeyExists("spinningProps"))
            {
                var spinningProps = itemAttrs["spinningProps"];
            
                // Get spinTime from the item's spinningProps, default to 4 if not specified
                float spinTime = spinningProps["spinTime"]?.AsFloat(4f) ?? 4f;
                return spinTime;
            }
        }
    
        // Default fallback if no item or no spinningProps
        return 4f;
    }

    #endregion
    #region Helper Getters
    
    public ItemSlot InputSlot => inventory[0];
    
    public ItemSlot OutputSlot => inventory[1];
    
    public ItemStack InputStack
    {
        get => inventory[0]?.Itemstack;
        set 
        { 
            if (inventory[0] != null)
            {
                inventory[0].Itemstack = value;
                inventory[0].MarkDirty();
            }
        }
    }
    
    public ItemStack OutputStack
    {
        get => inventory[1]?.Itemstack;
        set 
        { 
            if (inventory[1] != null)
            {
                inventory[1].Itemstack = value;
                inventory[1].MarkDirty();
            }
        }
    }
    
    private BlockEntityAnimationUtil animUtil
    {
        get { return GetBehavior<BEBehaviorAnimatable>()?.animUtil; }
    }
    
    #endregion
    
    #region Initialization
    
    public BlockEntitySpinningWheel()
    {
        inventory = new InventorySpinningWheel(null, null);
        inventory.SlotModified += OnSlotModified;
    }
    
    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        
        inventory.LateInitialize("spinningwheel-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z, api);
    
        // Initialize controls
        controls.OnAction = onControls;

        // Register tick listeners
        RegisterGameTickListener(OnSpinTick, 100);
        RegisterGameTickListener(On500msTick, 500);
    
        // Client-side: sync animation with On state
        if (api.Side == EnumAppSide.Client)
        {
            RegisterGameTickListener(OnClientAnimationTick, 100);
            RegisterGameTickListener(OnClientGuiUpdateTick, 50);
        
            // Initialize renderer - ADD THIS BLOCK
            renderer = new SpinningWheelContentsRenderer(api as ICoreClientAPI, Pos);
            (api as ICoreClientAPI).Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, "spinningwheel");
            UpdateRenderer();
        }
    
        // Get collision box height
        Cuboidf[] collboxes = Block.GetCollisionBoxes(api.World.BlockAccessor, Pos);
        if (collboxes != null && collboxes.Length > 0)
        {
            y2 = collboxes[0].Y2;
        }
    
        // Get the facing direction from block code
        facing = BlockFacing.FromCode(Block.LastCodePart());
    
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
        if (Api?.Side != EnumAppSide.Client) return;

        if (on)
        {
            if (ambientSound == null || !ambientSound.IsPlaying)
            {
                ambientSound = ((IClientWorldAccessor)Api.World).LoadSound(new SoundParams()
                {
                    Location = new AssetLocation("spinningwheel:sounds/block/wooden-spinning-wheel.ogg"),
                    ShouldLoop = true,
                    Position = Pos.ToVec3f().Add(0.5f, 0.5f, 0.5f),
                    DisposeOnFinish = false,
                    Volume = 0f,
                    Range = 6,
                    SoundType = EnumSoundType.Ambient
                });

                if (ambientSound != null)
                {
                    ambientSound.Start();
                    ambientSound.FadeTo(0.5f, 1f, (s)=> { });
                    ambientSound.PlaybackPosition = ambientSound.SoundLengthSeconds * (float)Api.World.Rand.NextDouble();
                }
            } else
            {
                if (ambientSound.IsPlaying) ambientSound.FadeTo(0.5f, 1f, (s) => { });
            }
        }
        else
        {
            ambientSound?.FadeOut(0.5f, (s) => { s.Dispose(); ambientSound = null; });
        }
    }
    
    #endregion
    
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
    
    private bool CanPlayerUseSpinningWheel(IPlayer player)
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
                Api?.Logger?.Notification($"[SpinningWheel] Class match found: {allowedClass}");
                return true;
            }
        }
    
        Api?.Logger?.Notification($"[SpinningWheel] No matching class found. Allowed classes: {string.Join(", ", ModConfig.ModConfig.Loaded.AllowedClasses)}");
        return false;
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
        
        if (!blockBroken)
        {
            // Try to place the player in a safe position around the spinning wheel
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
    }
    
    public bool IsMountedBy(Entity entity) => this.MountedBy == entity;
    public bool IsBeingControlled() => false;
    public bool CanUnmount(EntityAgent entityAgent) => true;
    public bool CanMount(EntityAgent entityAgent) => !AnyMounted();
    public bool AnyMounted() => MountedBy != null;
    
    #endregion
    
    #region Spinning Logic
    
    private void OnSpinTick(float dt)
    {
        // Client-side: just update visuals
        if (Api is ICoreClientAPI)
        {
            return;
        }

        // Server-side processing
        if (!CanSpin() || MountedBy == null)
        {
            // Stop animation if can't spin
            if (On)
            {
                Deactivate();
            }
    
            // Decay progress slowly
            if (inputSpinTime > 0)
            {
                inputSpinTime -= dt * 0.5f;
                if (inputSpinTime < 0) inputSpinTime = 0;
            }
            return;
        }

        // We can spin and player is mounted - ensure animation is running
        if (!On)
        {
            Activate();
        }

        // Get and store the current max spin time
        currentMaxSpinTime = maxSpinTime();

        // Process spinning
        inputSpinTime += dt;

        if (inputSpinTime >= currentMaxSpinTime)
        {
            SpinInput();
            inputSpinTime = 0;
            MarkDirty(true);
        }
    }
    
    private bool CanSpin()
    {
        ItemSlot inputSlot = InputSlot;
        if (inputSlot?.Itemstack == null) return false;
    
        // Check if this item can be spun
        if (inputSlot.Itemstack.ItemAttributes.KeyExists("spinningProps"))
        {
            var spinningProps = inputSlot.Itemstack.ItemAttributes["spinningProps"];
            int requiredInput = spinningProps["inputQuantity"]?.AsInt(1) ?? 1;
        
            // Check if we have enough input items
            if (inputSlot.Itemstack.StackSize < requiredInput) return false;
        
            ItemSlot outputSlot = OutputSlot;
        
            // Check if output slot has room
            if (outputSlot.Empty) return true;
        
            // Check if we can stack more
            ItemStack resultStack = GetSpinResult(inputSlot.Itemstack);
            if (resultStack != null && outputSlot.Itemstack.Collectible.Equals(outputSlot.Itemstack, resultStack, GlobalConstants.IgnoredStackAttributes))
            {
                return outputSlot.Itemstack.StackSize < outputSlot.Itemstack.Collectible.MaxStackSize;
            }
        }

        return false;
    }

    private ItemStack GetSpinResult(ItemStack input)
    {
        // Check if the input item has spinningProps
        if (input.ItemAttributes.KeyExists("spinningProps"))
        {
            var spinningProps = input.ItemAttributes["spinningProps"];
        
            // Get the outputType and outputQuantity from spinningProps
            string outputType = spinningProps["outputType"].AsString();
            int outputQuantity = spinningProps["outputQuantity"].AsInt(1);
        
            // Create and return the output ItemStack
            ItemStack outputStack = new ItemStack(Api.World.GetItem(new AssetLocation(outputType)), outputQuantity);
            return outputStack;
        }

        // Fallback for items without spinningProps (optional)
        if (input.Collectible.Code.Path.Contains("flaxfibers"))
        {
            return new ItemStack(Api.World.GetItem(new AssetLocation("game:flaxtwine")), 1);
        }

        // No valid recipe found
        return null;
    }

    private void SpinInput()
    {
        ItemSlot inputSlot = InputSlot;
        ItemSlot outputSlot = OutputSlot;
    
        ItemStack resultStack = GetSpinResult(inputSlot.Itemstack);
        if (resultStack == null) return;
    
        // Get how many input items to consume
        int inputQuantity = 1; // Default
        if (inputSlot.Itemstack.ItemAttributes.KeyExists("spinningProps"))
        {
            var spinningProps = inputSlot.Itemstack.ItemAttributes["spinningProps"];
            inputQuantity = spinningProps["inputQuantity"]?.AsInt(1) ?? 1;
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
            clientDialog?.Update(inputSpinTime, currentMaxSpinTime);
        }
        
        UpdateRenderer();
        shouldRedraw = true; 
        
        // Check animation state when slots change (server-side)
        if (Api is ICoreServerAPI)
        {
            bool canSpin = CanSpin();
            bool isPlayerMounted = MountedBy != null;
            bool shouldBeSpinning = canSpin && isPlayerMounted;
    
            // Update max spin time whenever input slot changes
            if (slotid == 0 && !InputSlot.Empty)
            {
                currentMaxSpinTime = maxSpinTime();
            }
    
            if (shouldBeSpinning && !On)
            {
                Activate();
            }
            else if (!shouldBeSpinning && On)
            {
                Deactivate();
            }
        }

        if (slotid == 0)
        {
            if (InputSlot.Empty)
            {
                inputSpinTime = 0.0f;
                currentMaxSpinTime = 4f;
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
        // Server: Mark dirty if spinning
        if (Api is ICoreServerAPI && (MountedBy != null || inputSpinTime > 0))
        {
            MarkDirty();
        }
    }
    
    private void OnClientGuiUpdateTick(float dt)
    {
        if (clientDialog != null && clientDialog.IsOpened())
        {
            clientDialog.Update(inputSpinTime, currentMaxSpinTime);
        }
    }
    
    // Client-side: sync animation with server's On state
    private bool clientAnimationRunning = false;
    private bool clientSoundRunning = false;
    private void OnClientAnimationTick(float dt)
    {
        if (animUtil?.animator == null) return;

        // Start animation if On but not running
        if (On && !clientAnimationRunning)
        {
            animUtil.StartAnimation(new AnimationMetaData() { 
                Animation = "wheelspin", 
                Code = "wheelspin", 
                AnimationSpeed = 1f
            });
            clientAnimationRunning = true;
    
            // Start sound when animation starts
            ToggleAmbientSound(true);
            clientSoundRunning = true;
    
            // Update player animation to spinning
            RefreshSeatAnimation();
        
            // Show the distaff fibers
            UpdateRenderer();
        }
        // Stop animation if Off but running
        else if (!On && clientAnimationRunning)
        {
            animUtil.StopAnimation("wheelspin");
            clientAnimationRunning = false;
    
            // Stop sound when animation stops
            ToggleAmbientSound(false);
            clientSoundRunning = false;
    
            // Update player animation back to idle
            RefreshSeatAnimation();
        
            // Hide the distaff fibers
            UpdateRenderer();
        }
    }
    
    private void RefreshSeatAnimation()
    {
        if (MountedBy != null)
        {
            // Force the player to re-evaluate their sitting animation
            MountedBy.AnimManager?.StopAnimation("sitidle2");
            MountedBy.AnimManager?.StopAnimation("hp-sitspinning");
            
            // Start the appropriate animation
            var anim = SuggestedAnimation;
            if (anim != null)
            {
                MountedBy.AnimManager?.StartAnimation(anim);
            }
        }
    }

    #endregion
    
    #region Animation
    
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
    
    #region Player Interaction
    
    public bool OnPlayerInteract(IPlayer byPlayer)
    {
        // Check if player has the required class (validated on both sides)
        if (!CanPlayerUseSpinningWheel(byPlayer))
        {
            if (Api.Side == EnumAppSide.Client)
            {
                string requiredClasses = string.Join(", ", ModConfig.ModConfig.Loaded.AllowedClasses);
                (Api as ICoreClientAPI).TriggerIngameError(this, "wrongclass", 
                    Lang.Get("spinningwheel:spinning-wheel-requires-class", requiredClasses));
            }
            return false;
        }

        // Check if someone is already using it
        if (MountedBy != null) 
        {
            if (Api.Side == EnumAppSide.Client)
            {
                (Api as ICoreClientAPI).TriggerIngameError(this, "occupied", 
                    Lang.Get("spinningwheel:spinning-wheel-occupied"));
            }
            return false;
        }

        // Try to mount the player to the spinning wheel
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
    
    private string GetOffsetKeyFromSelectionIndex(int globalIndex, out int localBoxIndex)
    {
        int cumulativeIndex = 0;
    
        // Iterate through the ValuesByMultiblockOffset to find which offset contains this box
        foreach (var kvp in ((BlockSpinningWheel)Block).ValuesByMultiblockOffset.SelectionBoxesByOffset)
        {
            Vec3i offset = kvp.Key;
            Cuboidf[] boxes = kvp.Value;
        
            if (boxes == null || boxes.Length == 0) continue;
        
            if (globalIndex >= cumulativeIndex && globalIndex < cumulativeIndex + boxes.Length)
            {
                localBoxIndex = globalIndex - cumulativeIndex;
                return $"{offset.X},{offset.Y},{offset.Z}";
            }
        
            cumulativeIndex += boxes.Length;
        }
    
        localBoxIndex = 0;
        return "0,0,0"; // Default to control block
    }
    
    private Vec3i GetMultiblockOffset(BlockPos clickedPos)
    {
        // Get the master/control block position
        BlockPos masterPos = GetMasterBlockPos();
        
        // Calculate offset from master
        return new Vec3i(
            clickedPos.X - masterPos.X,
            clickedPos.Y - masterPos.Y,
            clickedPos.Z - masterPos.Z
        );
    }
    
    private BlockPos GetMasterBlockPos()
    {
        // The control block is the master - this BlockEntity IS on the control block
        // The control block has offset (0,0,0) in your multiblock structure
        return Pos;
    }
    
    public bool OpenGui(IPlayer player)
    {
        Api.Logger.Debug($"[SpinningWheel] OpenGui called, Side: {Api.Side}");
    
        if (Api.Side == EnumAppSide.Client)
        {
            Api.Logger.Debug("[SpinningWheel] On client side, checking permissions");
        
            // Check if player has the required class first
            if (!CanPlayerUseSpinningWheel(player))
            {
                Api.Logger.Debug("[SpinningWheel] Player doesn't have required class");
                string requiredClasses = string.Join(", ", ModConfig.ModConfig.Loaded.AllowedClasses);
                (Api as ICoreClientAPI).TriggerIngameError(this, "wrongclass", 
                    Lang.Get("spinningwheel:spinning-wheel-requires-class", requiredClasses));
                return false;
            }
        
            Api.Logger.Debug("[SpinningWheel] Opening dialog");
        
            // Open the GUI instead of mounting
            if (clientDialog == null || !clientDialog.IsOpened())
            {
                toggleInventoryDialogClient(player, () =>
                {
                    clientDialog = new GuiDialogBlockEntitySpinningWheel(
                        DialogTitle,
                        Inventory,
                        Pos,
                        Api as ICoreClientAPI
                    );
                    Api.Logger.Debug("[SpinningWheel] Dialog created");
                    return clientDialog;
                });
            }
        }
        else
        {
            Api.Logger.Debug("[SpinningWheel] On server side, returning true");
        }
    
        return true;
    }
    
    private bool MountPlayer(IPlayer byPlayer)
    {
        // Use the existing OnPlayerInteract method which handles mounting
        return OnPlayerInteract(byPlayer);
    }
    
    #endregion
    
    #region Serialization
    
    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        mountedByEntityId = tree.GetLong("mountedByEntityId");
        mountedByPlayerUid = tree.GetString("mountedByPlayerUid");
        inputSpinTime = tree.GetFloat("inputSpinTime");
        currentMaxSpinTime = tree.GetFloat("currentMaxSpinTime", 4f);
        On = tree.GetBool("On");
    
        if (Api != null)
        {
            Inventory.AfterBlocksLoaded(Api.World);
        }
    
        if (Api?.Side == EnumAppSide.Client)
        {
            UpdateRenderer();
            
            if (clientDialog != null)
            {
                clientDialog.Update(inputSpinTime, currentMaxSpinTime);
            }
        }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetLong("mountedByEntityId", mountedByEntityId);
        tree.SetString("mountedByPlayerUid", mountedByPlayerUid);
        tree.SetFloat("inputSpinTime", inputSpinTime);
        tree.SetFloat("currentMaxSpinTime", currentMaxSpinTime);
        tree.SetBool("On", On);
    }
    
    public void MountableToTreeAttributes(TreeAttribute tree)
    {
        tree.SetString("className", "spinningWheel");
        tree.SetInt("posx", Pos.X);
        tree.SetInt("posy", Pos.InternalY);
        tree.SetInt("posz", Pos.Z);
    }
    
    #endregion
    
    #region Block Events
    
    public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
    {
        base.OnReceivedClientPacket(player, packetid, data);
    }

    public override void OnReceivedServerPacket(int packetid, byte[] data)
    {
        base.OnReceivedServerPacket(packetid, data);

        if (packetid == (int)EnumBlockEntityPacketId.Close)
        {
            (Api.World as IClientWorldAccessor).Player.InventoryManager.CloseInventory(Inventory);
            invDialog?.TryClose();
            invDialog?.Dispose();
            invDialog = null;
        }
    }
    public override void OnBlockRemoved()
    {
        ToggleAmbientSound(false);
    
        renderer?.Dispose();
        renderer = null;
    
        if (clientDialog != null)
        {
            clientDialog.TryClose();
            clientDialog?.Dispose();
            clientDialog = null;
        }
        base.OnBlockRemoved();
    }

    public override void OnBlockBroken(IPlayer byPlayer = null)
    {
        base.OnBlockBroken(byPlayer);
    
        ToggleAmbientSound(false);
    
        renderer?.Dispose();
        renderer = null; 
    
        if (clientDialog != null)
        {
            clientDialog.TryClose();
            clientDialog?.Dispose();
            clientDialog = null;
        }
    
        blockBroken = true;
        MountedBy?.TryUnmount();
    }
    
    public override void OnBlockUnloaded()
    {
        base.OnBlockUnloaded();
        renderer?.Dispose();
    }
    
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
            animUtil?.InitializeAnimator("spinningwheel", null, null, GetRotation());
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
    
    void UpdateRenderer()
    {
        if (renderer == null) return;

        // Show hpdistafffibers whenever there's a valid spinnable item in the input slot
        if (InputSlot?.Itemstack != null && InputSlot.Itemstack.ItemAttributes?.KeyExists("spinningProps") == true)
        {
            // Load the hpdistafffibers item
            Item distaffItem = Api.World.GetItem(new AssetLocation("spinningwheel:hpdistafffibers"));
            if (distaffItem != null)
            {
                ItemStack distaffStack = new ItemStack(distaffItem);
                InSpinningWheelProps props = GetRenderProps(distaffStack);
        
                ModelTransform transform = props?.Transform ?? new ModelTransform();
                transform.EnsureDefaultValues();
        
                // Apply rotation based on spinning wheel's facing direction
                if (facing == null)
                {
                    facing = BlockFacing.FromCode(Block?.LastCodePart()) ?? BlockFacing.NORTH;
                }
        
                float yRotation = 0f;
                if (facing == BlockFacing.NORTH)
                    yRotation = 0f;
                else if (facing == BlockFacing.EAST)
                    yRotation = 270f;
                else if (facing == BlockFacing.SOUTH)
                    yRotation = 180f;
                else if (facing == BlockFacing.WEST)
                    yRotation = 90f;
        
                transform.Rotation.Y += yRotation;
        
                // Use the actual fiber color (remove the test red line)
                Vec4f fiberColor = GetFiberColorFromInput(InputSlot.Itemstack);
                renderer.SetContents(distaffStack, transform, fiberColor);
            }
        }
        else
        {
            // Hide the distaff when no valid item in input
            renderer.SetContents(null, null, ColorUtil.WhiteArgbVec);
        }
    }

    private Vec4f GetFiberColorFromInput(ItemStack inputStack)
    {
        if (inputStack?.Item == null) return ColorUtil.WhiteArgbVec;

        string itemCode = inputStack.Item.Code.ToString().ToLower();
        Api.Logger.Debug($"[SpinningWheel] Getting color for: {itemCode}");

        // Check if the item has spinningProps that define output color
        if (inputStack.Item.Attributes?.KeyExists("spinningProps") == true)
        {
            var spinningProps = inputStack.Item.Attributes["spinningProps"];
            
            // Check if there's an output item defined
            if (spinningProps.KeyExists("output"))
            {
                string outputCode = spinningProps["output"].AsString();
                Vec4f colorFromOutput = GetColorFromOutputItem(outputCode);
                if (colorFromOutput != ColorUtil.WhiteArgbVec)
                {
                    Api.Logger.Debug($"[SpinningWheel] Color from output: R={colorFromOutput.R:F2}, G={colorFromOutput.G:F2}, B={colorFromOutput.B:F2}");
                    return colorFromOutput;
                }
            }
        }

        // Extract color name from item code
        string colorName = ExtractColorName(itemCode);
        if (!string.IsNullOrEmpty(colorName))
        {
            Vec4f color = GetClothColor(colorName);
            Api.Logger.Debug($"[SpinningWheel] Color from name '{colorName}': R={color.R:F2}, G={color.G:F2}, B={color.B:F2}");
            return color;
        }

        return ColorUtil.WhiteArgbVec;
    }

    private string ExtractColorName(string itemCode)
    {
        // Handle twine format: "twine-wool-gray" -> "gray"
        if (itemCode.Contains("twine-"))
        {
            string[] parts = itemCode.Split('-');
            if (parts.Length >= 3)
            {
                string colorPart = parts[2]; // twine-wool-[color]
                if (IsColorName(colorPart)) return colorPart;
            }
        }
        
        // Handle fiber formats:
        // "fibers-generic-gray" -> "gray"
        // "fibers-angora-white" -> "white"
        if (itemCode.Contains("fibers-"))
        {
            string[] parts = itemCode.Split('-');
            
            // Check last part first (e.g., "fibers-angora-white")
            if (parts.Length >= 3)
            {
                string lastPart = parts[parts.Length - 1];
                if (IsColorName(lastPart)) return lastPart;
            }
            
            // Check second-to-last part (e.g., "fibers-generic-gray")
            if (parts.Length >= 3)
            {
                string secondLast = parts[parts.Length - 2];
                if (secondLast != "generic" && IsColorName(secondLast)) return secondLast;
            }
            
            // Check second part (e.g., "fibers-gray")
            if (parts.Length >= 2)
            {
                string secondPart = parts[1];
                if (IsColorName(secondPart)) return secondPart;
            }
        }
        
        // Handle thread/yarn formats: "thread-linen-gray" -> "gray"
        if (itemCode.Contains("thread-") || itemCode.Contains("yarn-"))
        {
            string[] parts = itemCode.Split('-');
            if (parts.Length >= 3)
            {
                string colorPart = parts[2];
                if (IsColorName(colorPart)) return colorPart;
            }
        }
        
        // Default colors for specific material types
        if (itemCode.Contains("flax")) return "plain";
        if (itemCode.Contains("cattail")) return "plain";
        if (itemCode.Contains("algae")) return "green";
        if (itemCode.Contains("plain")) return "plain";
        if (itemCode.Contains("mordant")) return "plain"; // Mordant twine is undyed
        
        return "plain"; // Default
    }

    private bool IsColorName(string part)
    {
        string[] knownColors = {
            // Basic colors
            "white", "gray", "grey", "black", "brown", "lightbrown", "redbrown",
            "yellow", "plain", "orange", "red", "green", "blue", "purple", "pink",
            // Dark variants (for Wool & More)
            "darkblue", "darkbrown", "darkgreen", "darkred", "darkgray", "darkgrey",
            // Special
            "mordant"
        };
        return knownColors.Contains(part);
    }

    private Vec4f GetClothColor(string colorName)
    {
        // These colors match the game's cloth/linen colors
        switch (colorName.ToLower())
        {
            case "white":
                return new Vec4f(0.95f, 0.95f, 0.95f, 1.0f);
            case "gray":
            case "grey":
                return new Vec4f(0.6f, 0.6f, 0.6f, 1.0f);
            case "black":
                return new Vec4f(0.15f, 0.15f, 0.15f, 1.0f);
            case "brown":
                return new Vec4f(0.55f, 0.35f, 0.2f, 1.0f);
            case "redbrown":
                return new Vec4f(0.65f, 0.25f, 0.2f, 1.0f);
            case "lightbrown":
                return new Vec4f(0.75f, 0.65f, 0.5f, 1.0f);
            case "yellow":
                return new Vec4f(0.95f, 0.9f, 0.5f, 1.0f);
            case "orange":
                return new Vec4f(0.9f, 0.55f, 0.25f, 1.0f);
            case "red":
                return new Vec4f(0.8f, 0.2f, 0.2f, 1.0f);
            case "green":
                return new Vec4f(0.4f, 0.6f, 0.3f, 1.0f);
            case "blue":
                return new Vec4f(0.3f, 0.4f, 0.7f, 1.0f);
            case "purple":
                return new Vec4f(0.5f, 0.3f, 0.6f, 1.0f);
            case "pink":
                return new Vec4f(0.9f, 0.6f, 0.7f, 1.0f);
            
            // Dark variants
            case "darkblue":
                return new Vec4f(0.15f, 0.2f, 0.4f, 1.0f);
            case "darkbrown":
                return new Vec4f(0.3f, 0.2f, 0.1f, 1.0f);
            case "darkgreen":
                return new Vec4f(0.2f, 0.35f, 0.15f, 1.0f);
            case "darkred":
                return new Vec4f(0.45f, 0.1f, 0.1f, 1.0f);
            case "darkgray":
            case "darkgrey":
                return new Vec4f(0.3f, 0.3f, 0.3f, 1.0f);
            
            case "plain":
            case "mordant":
            default:
                return new Vec4f(0.85f, 0.8f, 0.7f, 1.0f); // Natural fiber color
        }
    }

    private Vec4f GetColorFromOutputItem(string outputCode)
    {
        // Try to load the output item and get its color
        try
        {
            Item outputItem = Api.World.GetItem(new AssetLocation(outputCode));
            if (outputItem != null)
            {
                // Try to extract color from the output item's code
                string outputItemCode = outputItem.Code.ToString().ToLower();
                string colorName = ExtractColorName(outputItemCode);
                if (!string.IsNullOrEmpty(colorName))
                {
                    return GetClothColor(colorName);
                }
            }
        }
        catch
        {
            // Ignore and fall back
        }
        
        return ColorUtil.WhiteArgbVec;
    }

    InSpinningWheelProps GetRenderProps(ItemStack contentStack)
    {
        if (contentStack?.ItemAttributes?.KeyExists("inSpinningWheelProps") == true)
        {
            InSpinningWheelProps props = contentStack.ItemAttributes["inSpinningWheelProps"].AsObject<InSpinningWheelProps>();
            props.Transform.EnsureDefaultValues();
            return props;
        }
        return null;
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