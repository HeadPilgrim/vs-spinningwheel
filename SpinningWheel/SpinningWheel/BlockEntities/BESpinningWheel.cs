using System;
using SpinningWheel.GUIs;
using SpinningWheel.Inventories;
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

//Modify to be BlockEntityConatiner instead?
public class BlockEntitySpinningWheel: BlockEntityOpenableContainer, IMountableSeat, IMountable
{
    
    internal InventorySpinningWheel inventory;
    
    static Vec3f eyePos = new Vec3f(0, 0.3f, 0);
    
    BlockFacing facing;
    private float y2 = 0.5f;
    public EntityAgent MountedBy;
    bool blockBroken;
    long mountedByEntityId;
    string mountedByPlayerUid;
    EntityControls controls = new EntityControls();
    EntityPos mountPos = new EntityPos();
    public bool DoTeleportOnUnmount { get; set; } = true;
    
    public EntityPos SeatPosition => Position; // Since we have only one seat, it can be the same as the base position
    public double StepPitch => 0;

    public EntityPos Position
    {
        get
        {
            mountPos.SetPos(Pos);
        
            // Safety check for null facing
            if (facing == null)
            {
                // Try to get facing again
                if (Block != null)
                {
                    facing = BlockFacing.FromCode(Block.LastCodePart());
                }
            
                // If still null, default to NORTH
                if (facing == null)
                {
                    facing = BlockFacing.NORTH;
                }
            }
        
            mountPos.Yaw = this.facing.HorizontalAngleIndex * GameMath.PIHALF + GameMath.PIHALF;

            // Position player in front of the wheel based on which way it's facing
            if (facing == BlockFacing.NORTH) return mountPos.Add(0.5, y2, 0.7);
            if (facing == BlockFacing.EAST) return mountPos.Add(0.3, y2, 0.5);
            if (facing == BlockFacing.SOUTH) return mountPos.Add(0.5, y2, 0.3);
            if (facing == BlockFacing.WEST) return mountPos.Add(0.7, y2, 0.5);

            return mountPos.Add(0.5, y2, 0.5); // Fallback: center
        }
    }
    
    //Starts seraph idle anim? 
    AnimationMetaData meta = new AnimationMetaData() { Code = "hp-sitspinning", Animation = "HP-SitSpinning" }.Init();
    
    public AnimationMetaData SuggestedAnimation => meta;
    public EntityControls Controls => controls;
    public IMountable MountSupplier => this;
    public EnumMountAngleMode AngleMode => EnumMountAngleMode.FixateYaw;
    public Vec3f LocalEyePos => eyePos;
    Entity IMountableSeat.Passenger => MountedBy;
    public bool CanControl => false;
    public Entity Entity => null;
    public Matrixf RenderTransform => null;
    public IMountableSeat[] Seats => new IMountableSeat[] { this };
    
    public bool SkipIdleAnimation => false;
    public float FpHandPitchFollow => 1;
    public string SeatId { get => "bed-0"; set { } }
    public SeatConfig Config { get => null; set { } }
    public long PassengerEntityIdForInit { get => mountedByEntityId; set => mountedByEntityId = value; }
    
    public Entity Controller => MountedBy;

    public Entity OnEntity => null;

    public EntityControls ControllingControls => null;
    
    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
    
        // Initialize controls if you're using them (for GUI interaction)
        controls.OnAction = onControls;
    
        // Get any custom attributes from your block JSON
        // Example: spinSpeed, efficiency, etc.
        if (Block.Attributes != null)
        {
            // spinSpeed = Block.Attributes["spinSpeed"].AsFloat(1.0f);
            // Add any attributes you want to read from block JSON
        }
    
        // Get collision box height (useful for positioning the seated player)
        Cuboidf[] collboxes = Block.GetCollisionBoxes(api.World.BlockAccessor, Pos);
        if (collboxes != null && collboxes.Length > 0)
        {
            y2 = collboxes[0].Y2;
        }
    
        // Get the facing direction from block code (e.g., "spinningwheel-north")
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
    private void onControls(EnumEntityAction action, bool on, ref EnumHandling handled)
    {
        if (action == EnumEntityAction.Sneak && on)
        {
            MountedBy?.TryUnmount();
            controls.StopAllMovement();
            handled = EnumHandling.PassThrough;
        }
    }
    
    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);

        mountedByEntityId = tree.GetLong("mountedByEntityId");
        mountedByPlayerUid = tree.GetString("mountedByPlayerUid");
    }
    
    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        tree.SetLong("mountedByEntityId", mountedByEntityId);
        tree.SetString("mountedByPlayerUid", mountedByPlayerUid);
    }
    public void MountableToTreeAttributes(TreeAttribute tree)
    {
        tree.SetString("className", "spinningWheel");
        tree.SetInt("posx", Pos.X);
        tree.SetInt("posy", Pos.InternalY);
        tree.SetInt("posz", Pos.Z);
    }
    
    public void DidUnmount(EntityAgent entityAgent)
    {
        MountedBy = null;

        if (!blockBroken)
        {
            // Try to place the player in a safe position around the spinning wheel
            foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
            {
                Vec3d placePos = Pos.ToVec3d().AddCopy(facing).Add(0.5, 0.001, 0.5);

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

        if (entityAgent.Api?.Side == EnumAppSide.Server)
        {
            Console.WriteLine("Spinning Wheel Mounted");
            /*if (restingListener == 0)
            {
                var oldapi = this.Api;
                this.Api = entityAgent.Api;   // in case this.Api is currently null if this is called by LoadEntity method for entityAgent; a null Api here would cause RegisterGameTickListener to throw an exception
                restingListener = RegisterGameTickListener(RestPlayer, 200);
                this.Api = oldapi;
            }
            hoursTotal = entityAgent.Api.World.Calendar.TotalHours;*/
        }

        /*if (MountedBy != null)
        {
            entityAgent.Api.Event.EnqueueMainThreadTask(() => // Might not be initialized yet if this is loaded from spawnchunks
            {
                if (MountedBy != null)
                {
                    EntityBehaviorTiredness ebt = MountedBy.GetBehavior("tiredness") as EntityBehaviorTiredness;
                    if (ebt != null) ebt.IsSleeping = true;
                }
            }, "issleeping");
        }*/


        MarkDirty(false);
    }
    public bool IsMountedBy(Entity entity) => this.MountedBy == entity;
    public bool IsBeingControlled() => false;
    public bool CanUnmount(EntityAgent entityAgent) => true;
    public bool CanMount(EntityAgent entityAgent) => !AnyMounted();

    public bool AnyMounted() => MountedBy != null;
    public bool On { get; set; }
    
    #region Config
    
    // For how long the ore has been cooking
    public float inputStackCookingTime;
    // How much of the current fuel is consumed
    public float fuelBurnTime;
    // How much fuel is available
    public float maxFuelBurnTime;
    
    public float cachedFuel;
    
    
    public override string InventoryClassName
    {
        get { return "spinningwheel"; }
    }
    public override InventoryBase Inventory
    {
        get { return inventory; }
    }
    
    #endregion
  
    
    public BlockEntitySpinningWheel()
    {
        inventory = new InventorySpinningWheel(null, null);
        inventory.SlotModified += OnSlotModified;
    }
    
    private void OnSlotModified(int slotid)
    {
        Block = Api.World.BlockAccessor.GetBlock(Pos);

        //This is probably useless??? TEST
        MarkDirty(Api.Side == EnumAppSide.Server); // Save useless triple-remesh by only letting the server decide when to redraw

        if (Api is ICoreClientAPI && clientDialog != null)
        {
            //SetDialogValues(clientDialog.Attributes);
        }

        Api.World.BlockAccessor.GetChunkAtBlockPos(Pos)?.MarkModified();
    }
    
    // Sync to client every 500ms
    private void On500msTick(float dt)
    {
        if (Api is ICoreServerAPI && (IsRidden))
        {
            MarkDirty();
        }
    }
    public virtual string DialogTitle
    {
        get { return Lang.Get("spinningwheel"); }
    }
    
    GuiDialogBlockEntitySpinningWheel clientDialog;

    private bool IsRidden;
    
    private BlockEntityAnimationUtil animUtil
    {
        get { return GetBehavior<BEBehaviorAnimatable>().animUtil; }
    }
    
    
    
    
    //Starts animation
    public void Activate()
    {
        On = true;
        animUtil.StartAnimation(new AnimationMetaData() { Animation = "wheelspin", Code = "wheelspin", AnimationSpeed = 1f});
        MarkDirty(true);
    }
    
    //Stops animation
    public void Deactivate()
    {
        animUtil.StopAnimation("wheelspin");
        On = false;
        MarkDirty(true);
    }

    
    //Currently OnInteract probably will be replaced with OnPlayerRightClick() or  some other interaction once mounted
    public bool OnInteract(BlockSelection blockSel, IPlayer byPlayer)
    {
        var slot = byPlayer.InventoryManager.ActiveHotbarSlot;
        Console.WriteLine("Spinning wheel interacted by player: " + byPlayer.PlayerName);
        Console.WriteLine("Hotbar slot empty: " + slot.Empty);
        if (slot.Empty)
        {
            if (On)
            {
                Deactivate();
                Console.WriteLine("Deactivating spinning wheel.");
            }
            else
            {
                Console.WriteLine("Activating spinning wheel.");
                Activate();
            }
            return true;
        }
        return true;
    }
    
    /* Commeneted out until I figure out GUI solutions
     public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
    {
        if (Api.Side == EnumAppSide.Client)
        {
            toggleInventoryDialogClient(byPlayer, () => {
                SyncedTreeAttribute dtree = new SyncedTreeAttribute();
                SetDialogValues(dtree);
                clientDialog = new GuiDialogBlockEntitySpinningWheel(DialogTitle, Inventory, Pos, dtree, Api as ICoreClientAPI);
                return clientDialog;
            });
        }

        return true;
    }*/
    public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
    {
        throw new NotImplementedException();
    }

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
            animUtil?.InitializeAnimator("spinningwheel");
        }

        return base.OnTesselation(mesher, tessThreadTesselator);
    }
}