using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace SpinningWheel.BlockEntities
{
    public class BlockEntityFlyShuttleLoom : BlockEntity, IMountableSeat, IMountable
    {
        BlockFacing facing;
        private float y2 = 0.2f;
        public bool On { get; set; }
        public EntityAgent MountedBy;
        bool blockBroken;
        long mountedByEntityId;
        string mountedByPlayerUid;
        EntityControls controls = new EntityControls();
        EntityPos mountPos = new EntityPos();
        public bool DoTeleportOnUnmount { get; set; } = true;
        
        // Animation metadata
        private AnimationMetaData idleAnimation = new AnimationMetaData() { Code = "sitloomidle", Animation = "SitLoomIdle" }.Init();
        private AnimationMetaData startAnimation = new AnimationMetaData() { Code = "sitloomstart", Animation = "SitLoomStart" }.Init();
        private AnimationMetaData weavingAnimation = new AnimationMetaData() { Code = "sitloomfull", Animation = "SitLoomFull" }.Init();
        
        private BlockEntityAnimationUtil animUtil
        {
            get { return GetBehavior<BEBehaviorAnimatable>()?.animUtil; }
        }
        
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
        
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            
            // Get the facing direction from block code
            facing = BlockFacing.FromCode(Block.LastCodePart());
            
            // Initialize controls
            controls.OnAction = onControls;
            
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
                // Force the player to re-evaluate their sitting animation
                MountedBy.AnimManager?.StopAnimation("sitidle2");
                MountedBy.AnimManager?.StopAnimation("sitweaving");
                
                // Start the appropriate animation
                var anim = SuggestedAnimation;
                if (anim != null)
                {
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
            On = tree.GetBool("On");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetLong("mountedByEntityId", mountedByEntityId);
            tree.SetString("mountedByPlayerUid", mountedByPlayerUid);
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
        }
        
        #endregion
    }
}