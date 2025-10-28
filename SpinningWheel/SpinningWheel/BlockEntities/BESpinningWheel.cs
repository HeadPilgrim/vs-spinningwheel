using System;
using System.Collections.Generic;
using SpinningWheel.GUIs;
using SpinningWheel.Inventories;
using SpinningWheel.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace SpinningWheel.BlockEntities;

public class BlockEntitySpinningWheel : BlockEntityOpenableContainer, IMountableSeat, IMountable
{
    internal InventorySpinningWheel inventory;
    
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
                return mountPos.Add(1.0, y2 - 0.68, 0.95);  // cposition (0,0,0) + (1,0,1) = center
            if (facing == BlockFacing.EAST)
                return mountPos.Add(0.05, y2 - 0.68, 1.0);  // Rotated 90° clockwise : cposition (1,0,0) + (-1,0,1) = center
            if (facing == BlockFacing.SOUTH)
                return mountPos.Add(0.0, y2 - 0.68, 0.05);  // Rotated 180° : cposition (1,0,1) + (-1,0,-1) = center
            if (facing == BlockFacing.WEST)
                return mountPos.Add(0.95, y2 - 0.68, 0.0);  // Rotated 270° clockwise : cposition (0,0,1) + (1,0,-1) = center
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
    // Make SuggestedAnimation dynamic based on On state:
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
    
    public bool On { get; set; }
    
    public override string InventoryClassName => "spinningwheel";
    
    public override InventoryBase Inventory => inventory;

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

            

                // Get spinTime from the item's spinningProps, default to 10 if not specified

                float spinTime = spinningProps["spinTime"]?.AsFloat(10f) ?? 10f;

                return spinTime;

            }

        }

    

        // Default fallback if no item or no spinningProps

        return 10f;

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



        // Initialize controls
        controls.OnAction = onControls;

        // Register tick listeners
        RegisterGameTickListener(OnSpinTick, 100);
        RegisterGameTickListener(On500msTick, 500);
        
        // Client-side: sync animation with On state
        if (api.Side == EnumAppSide.Client)
        {
            RegisterGameTickListener(OnClientAnimationTick, 100);
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
    
    #endregion
    
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
    
    public void OpenGui(IPlayer player)
    {
        // Only allow the mounted player to open the GUI
        if (MountedBy != player.Entity)
        {
            return;
        }
        if (Api.Side == EnumAppSide.Client)
        {
            if (clientDialog == null || !clientDialog.IsOpened())
            {
                clientDialog = new GuiDialogBlockEntitySpinningWheel(
                    DialogTitle, 
                    Inventory, 
                    Pos, 
                    Api as ICoreClientAPI
                );
                clientDialog.TryOpen();
            }
        }
    }
    
    public bool IsMountedBy(Entity entity) => this.MountedBy == entity;
    public bool IsBeingControlled() => false;
    public bool CanUnmount(EntityAgent entityAgent) => true;
    public bool CanMount(EntityAgent entityAgent) => !AnyMounted();
    public bool AnyMounted() => MountedBy != null;
    
    #endregion
    
    #region Spinning Logic
    private float currentMaxSpinTime = 10f;

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
        
        // Get the current max spin time and store it
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
            int outputQuantity = spinningProps["outputQuantity"].AsInt(1); // Default to 1 if not specified
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
        if (outputSlot.Empty)
        {
            outputSlot.Itemstack = resultStack;
        }
        else
        {
            outputSlot.Itemstack.StackSize += resultStack.StackSize;
        }

        inputSlot.TakeOut(1);
        inputSlot.MarkDirty();
        outputSlot.MarkDirty();
    }
    
    #endregion
    
    #region Slot Management
    
    private void OnSlotModified(int slotid)
    {
        if (Api is ICoreClientAPI)
        {
            clientDialog?.Update(inputSpinTime, currentMaxSpinTime); // Use currentMaxSpinTime
        }
        // Check animation state when slots change (server-side)
        if (Api is ICoreServerAPI)
        {
            bool canSpin = CanSpin();
            bool isPlayerMounted = MountedBy != null;
            bool shouldBeSpinning = canSpin && isPlayerMounted;
            // Update the max spin time when slot changes
            if (shouldBeSpinning)
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
        
        // Client: Update GUI with synced value
        if (Api.Side == EnumAppSide.Client && clientDialog != null && clientDialog.IsOpened())
        {
            clientDialog.Update(inputSpinTime, currentMaxSpinTime); // Use currentMaxSpinTime
        }
    }
    // Client-side: sync animation with server's On state
    private bool clientAnimationRunning = false;
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
            
            // Update player animation to spinning
            RefreshSeatAnimation();
        }

        // Stop animation if Off but running
        else if (!On && clientAnimationRunning)
        {
            animUtil.StopAnimation("wheelspin");
            clientAnimationRunning = false;
            
            // Update player animation back to idle
            RefreshSeatAnimation();
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
        Api.Logger.Notification($"[SpinningWheel] Activate() called");
        On = true;
        MarkDirty(true);
    }
    
    // Stop animation (server sets state, client will sync)
    public void Deactivate()
    {
        Api.Logger.Notification($"[SpinningWheel] Deactivate() called");
        On = false;
        MarkDirty(true);
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
                (Api as ICoreClientAPI).TriggerIngameError(this, "occupied", Lang.Get("spinning-wheel-occupied"));
            }
            return false;
        }

        // Try to mount the player to the spinning wheel
        return byPlayer.Entity.TryMount(this);
    }
    
    public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
    {
        // Mount the player when they right-click
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

        On = tree.GetBool("On");
        currentMaxSpinTime = tree.GetFloat("currentMaxSpinTime", 10f);
        if (Api != null)
        {
            Inventory.AfterBlocksLoaded(Api.World);
        }
        if (Api?.Side == EnumAppSide.Client && clientDialog != null)
        {
            clientDialog.Update(inputSpinTime, currentMaxSpinTime);
        }
    }
    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetLong("mountedByEntityId", mountedByEntityId);
        tree.SetString("mountedByPlayerUid", mountedByPlayerUid);
        tree.SetFloat("inputSpinTime", inputSpinTime);
        tree.SetBool("On", On);
        tree.SetFloat("currentMaxSpinTime", currentMaxSpinTime);
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
    public override void OnBlockRemoved()
    {
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
        
        if (clientDialog != null)
        {
            clientDialog.TryClose();
            clientDialog?.Dispose();
            clientDialog = null;
        }
        
        blockBroken = true;
        MountedBy?.TryUnmount();
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