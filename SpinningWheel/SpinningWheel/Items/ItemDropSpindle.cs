using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace SpinningWheel.Items
{
    public class ItemDropSpindle : Item
    {
        // Animation times in seconds
        private const float SPIN_ANIMATION_TIME = 2.0f;
        private const int MAX_SPIN_VARIANTS = 12;
        private ILoadedSound spindleSound;
        
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            
            if (!firstEvent) return;

            IPlayer player = (byEntity as EntityPlayer)?.Player;
            if (player == null) return;

            // Check if sneaking and spindle is complete - extract twine
            if (byEntity.Controls.Sneak && IsSpindleComplete(slot))
            {
                if (api.Side == EnumAppSide.Server)
                {
                    ExtractTwine(slot, player);
                }
                handHandling = EnumHandHandling.PreventDefaultAction;
                return;
            }

            ItemSlot offhandSlot = byEntity.LeftHandItemSlot;
            
            // Check if spindle is complete (ready to extract twine) - give hint
            if (IsSpindleComplete(slot))
            {
                if (api.Side == EnumAppSide.Client)
                {
                    (api as ICoreClientAPI).TriggerIngameError(this, "complete", 
                        Lang.Get("spinningwheel:dropspindle-extract-hint"));
                }
                handHandling = EnumHandHandling.NotHandled; // Changed from PreventDefaultAction
                return;
            }

            // Check for spinnable item in offhand
            if (offhandSlot?.Empty != false || !CanSpin(offhandSlot.Itemstack))
            {
                if (api.Side == EnumAppSide.Client)
                {
                    (api as ICoreClientAPI).TriggerIngameError(this, "nospinnable", 
                        Lang.Get("spinningwheel:dropspindle-need-fibers"));
                }
                handHandling = EnumHandHandling.PreventDefaultAction;
                return;
            }
            
            // Client-side only: visual and audio feedback
            if (api.Side == EnumAppSide.Client)
            {
                slot.Itemstack.TempAttributes.SetInt("renderVariant", 1);
                
                // Stop any existing sound first
                spindleSound?.Stop();
                spindleSound?.Dispose();
                
                // Load and play the spinning sound
                spindleSound = (api as ICoreClientAPI).World.LoadSound(new SoundParams()
                {
                    Location = new AssetLocation("spinningwheel:sounds/item/dropspindle"),
                    ShouldLoop = false,
                    Position = byEntity.Pos.XYZ.ToVec3f(),
                    DisposeOnFinish = true,
                    Volume = 0.5f,
                    Range = 8
                });
                spindleSound?.Start();
            }

            // Start spinning
            handHandling = EnumHandHandling.PreventDefaultAction;
        }


        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot,
            EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            // Calculate render variant based on animation progress
            int renderVariant = GameMath.Clamp(
                (int)Math.Ceiling(secondsUsed / SPIN_ANIMATION_TIME * MAX_SPIN_VARIANTS), 
                1, 
                MAX_SPIN_VARIANTS
            );

            // Client-side only: Update visual representation
            if (api.Side == EnumAppSide.Client)
            {
                int prevRenderVariant = slot.Itemstack.TempAttributes.GetInt("renderVariant", 0);
                slot.Itemstack.TempAttributes.SetInt("renderVariant", renderVariant);

                if (prevRenderVariant != renderVariant)
                {
                    (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();
                }

                // Particle effects
                if (secondsUsed % BURST_INTERVAL > 0.03f) return secondsUsed < SPIN_ANIMATION_TIME;
                SpawnSpiralBurst(byEntity, secondsUsed);
            }

            return secondsUsed < SPIN_ANIMATION_TIME;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            // Client-side cleanup
            if (api.Side == EnumAppSide.Client)
            {
                // Stop and dispose the sound
                spindleSound?.Stop();
                spindleSound?.Dispose();
                spindleSound = null;
                
                // Reset render variant
                slot.Itemstack?.TempAttributes.RemoveAttribute("renderVariant");
            }

            // Early exit if interaction was too short
            if (secondsUsed < SPIN_ANIMATION_TIME - 0.1f) return;

            IPlayer player = (byEntity as EntityPlayer)?.Player;
            if (player == null) return;

            ItemSlot offhandSlot = byEntity.LeftHandItemSlot;

            // Validate again
            if (IsSpindleComplete(slot) || offhandSlot?.Empty != false || !CanSpin(offhandSlot.Itemstack))
            {
                return;
            }

            // CRITICAL: Only process on server side to prevent double-processing
            if (api.Side == EnumAppSide.Server)
            {
                // CRITICAL: Use absolute time instead of world elapsed time to persist across server restarts
                // Prevents drop spindle breaking if half complete and server restarts or player leaves world. 
                long lastProcessTime = slot.Itemstack.Attributes.GetLong("lastSpinTime", 0);
                long currentTime = DateTime.UtcNow.Ticks / 10000;  // Absolute time in milliseconds
                
                // Prevent processing if less than 100ms since last spin (adjust as needed)
                if (currentTime - lastProcessTime < 100)
                {
                    api.Logger.Debug("Prevented double-spin (too soon): {0}ms since last", currentTime - lastProcessTime);
                    return;
                }
                
                slot.Itemstack.Attributes.SetLong("lastSpinTime", currentTime);
                ProcessSpin(slot, offhandSlot, byEntity);
            }
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            // Client-side cleanup
            if (api.Side == EnumAppSide.Client)
            {
                // Stop and dispose the sound
                spindleSound?.Stop();
                spindleSound?.Dispose();
                spindleSound = null;
                
                // Reset render variant on cancel
                slot.Itemstack?.TempAttributes.RemoveAttribute("renderVariant");
            }

            return true;
        }
        

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            if (IsSpindleComplete(inSlot))
            {
                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "spinningwheel:dropspindle-extract",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sneak"
                    }
                };
            }

            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "spinningwheel:dropspindle-spin",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = GetSpinnableStacks()
                }
            };
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            // Prevent normal attack when holding spindle
            handling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldAttackCancel(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            return false;
        }

        public override bool OnHeldAttackStep(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            return false;
        }

        #region Spinning Logic

        private bool CanSpin(ItemStack itemstack)
        {
            if (itemstack?.ItemAttributes == null) return false;
            return itemstack.ItemAttributes.KeyExists("spinningProps");
        }

        private void ProcessSpin(ItemSlot spindleSlot, ItemSlot fiberSlot, EntityAgent byEntity)
        {
            var spinningProps = fiberSlot.Itemstack.ItemAttributes["spinningProps"];
            int inputQuantity = spinningProps["inputQuantity"]?.AsInt(2) ?? 2;
            
            // Check if enough fibers
            if (fiberSlot.Itemstack.StackSize < inputQuantity)
            {
                return;
            }

            // Get current progress
            int currentSpins = spindleSlot.Itemstack.Attributes.GetInt("spins", 0);
            int spinsNeeded = Attributes["spinsPerCompletion"].AsInt(2);
            
            // Store what we're spinning
            if (currentSpins == 0)
            {
                string outputType = spinningProps["outputType"].AsString();
                spindleSlot.Itemstack.Attributes.SetString("outputType", outputType);
                
                int outputQuantity = spinningProps["outputQuantity"]?.AsInt(1) ?? 1;
                spindleSlot.Itemstack.Attributes.SetInt("outputQuantity", outputQuantity);
            }

            // Increment spin count
            currentSpins++;
            spindleSlot.Itemstack.Attributes.SetInt("spins", currentSpins);

            // Consume fibers
            fiberSlot.TakeOut(inputQuantity);
            fiberSlot.MarkDirty();

            // Damage spindle
            int durabilityCost = Attributes["spinDurabilityCost"]?.AsInt(1) ?? 1;
            DamageItem(api.World, byEntity, spindleSlot, durabilityCost);

            spindleSlot.MarkDirty();

            // Show progress message
            IServerPlayer serverPlayer = (byEntity as EntityPlayer)?.Player as IServerPlayer;
            if (serverPlayer != null)
            {
                if (currentSpins >= spinsNeeded)
                {
                    serverPlayer.SendMessage(GlobalConstants.InfoLogChatGroup, 
                        Lang.Get("spinningwheel:dropspindle-complete"), EnumChatType.Notification);
                }
                else
                {
                    float progress = (float)currentSpins / spinsNeeded * 100f;
                    serverPlayer.SendMessage(GlobalConstants.InfoLogChatGroup, 
                        Lang.Get("spinningwheel:dropspindle-progress", progress.ToString("F0")), 
                        EnumChatType.Notification);
                }
            }
        }

        private bool IsSpindleComplete(ItemSlot slot)
        {
            int currentSpins = slot.Itemstack.Attributes.GetInt("spins", 0);
            int spinsNeeded = Attributes["spinsPerCompletion"].AsInt(2);
            return currentSpins >= spinsNeeded;
        }

        private void ExtractTwine(ItemSlot spindleSlot, IPlayer player)
        {
            string outputType = spindleSlot.Itemstack.Attributes.GetString("outputType");
            int outputQuantity = spindleSlot.Itemstack.Attributes.GetInt("outputQuantity", 1);

            if (string.IsNullOrEmpty(outputType)) return;

            // Create output item
            Item outputItem = api.World.GetItem(new AssetLocation(outputType));
            if (outputItem == null) return;

            ItemStack outputStack = new ItemStack(outputItem, outputQuantity);

            // Try to give to player
            if (!player.InventoryManager.TryGiveItemstack(outputStack))
            {
                // Drop if inventory full
                api.World.SpawnItemEntity(outputStack, player.Entity.Pos.XYZ);
            }

            // Reset spindle
            spindleSlot.Itemstack.Attributes.RemoveAttribute("spins");
            spindleSlot.Itemstack.Attributes.RemoveAttribute("outputType");
            spindleSlot.Itemstack.Attributes.RemoveAttribute("outputQuantity");
            spindleSlot.MarkDirty();

            // Play sound
            api.World.PlaySoundAt(new AssetLocation("game:sounds/player/collect"), player.Entity, null, false, 8);
        }

        private ItemStack[] GetSpinnableStacks()
        {
            // Return example spinnable items for the interaction help
            var items = api.World.Collectibles;
            var spinnableStacks = new System.Collections.Generic.List<ItemStack>();

            foreach (var collectible in items)
            {
                if (collectible.Attributes?.KeyExists("spinningProps") == true)
                {
                    spinnableStacks.Add(new ItemStack(collectible));
                    if (spinnableStacks.Count >= 3) break; // Limit to 3 examples
                }
            }

            return spinnableStacks.ToArray();
        }

        #endregion

        
        #region Particles
        
        // ------------------------------------------------------------
        private readonly Random rand = new Random();

        // ------------------------------------------------------------
        //  STEEP SPIRAL TUNING
        private const float SPIN_SPEED      = 9.84f;  // 3 rev/sec → tight coils
        private const float MAX_RADIUS      = 0.12f;   // Narrow width
        private const float MAX_Y_OFFSET    = -0.55f;   // Deep drop (from +0.2 to -0.6)
        private const float PARTICLE_LIFE   = 0.08f;    // Long enough to trace path
        private const float PARTICLE_GRAV   = 0.02f;   // Very low = follows spiral
        private const float PARTICLE_MIN_SZ = 0.08f;
        private const float PARTICLE_MAX_SZ = 0.18f;
        private const float BURST_INTERVAL  = 0.03f;   // More bursts = denser helix
        private const int   MIN_PER_BURST   = 1;
        private const int   MAX_PER_BURST   = 2;
        // ------------------------------------------------------------
        
        private void SpawnSpiralBurst(EntityAgent byEntity, float secondsUsed)
        {
            Vec3d center = byEntity.Pos.XYZ
                .Add(0, byEntity.LocalEyePos.Y - 1.08, 0)
                .Ahead(0.5f, byEntity.Pos.Pitch, byEntity.Pos.Yaw);

            float t = secondsUsed / SPIN_ANIMATION_TIME;  // [0 → 1]
            float angle = secondsUsed * SPIN_SPEED;

            // STEEP: wide at top → point at bottom, big Y drop
            float radius = GameMath.Lerp(MAX_RADIUS, 0.08f, t);           // 0.12 → 0
            float yOff   = GameMath.Lerp(0, MAX_Y_OFFSET, t);       // +0.2 → -0.6

            int count = MIN_PER_BURST + rand.Next(MAX_PER_BURST - MIN_PER_BURST + 1);

            for (int i = 0; i < count; i++)
            {
                float localAngle = angle + i * (GameMath.PI * 2f / count);
                double dx = radius * Math.Cos(localAngle);
                double dz = radius * Math.Sin(localAngle);
                Vec3d pos = center.AddCopy(dx, yOff, dz);

                // Strong tangential + vertical pull
                float tang = 0.15f + (float)rand.NextDouble() * 0.1f;
                float velY = GameMath.Lerp(0.06f, -0.08f, t);  // rise → strong pull down
                Vec3f vel = new Vec3f(
                    (float)-Math.Sin(localAngle) * tang,
                    velY + (float)rand.NextDouble() * 0.03f,
                    (float)Math.Cos(localAngle) * tang
                );

                // Size tapers with radius
                float sizeFactor = 1f - t;
                float sizeBase = PARTICLE_MIN_SZ + (PARTICLE_MAX_SZ - PARTICLE_MIN_SZ) * sizeFactor;
                float sizeMax  = sizeBase + 0.08f;

                var p = new SimpleParticleProperties(
                    minQuantity: 1, maxQuantity: 1,
                    color: ColorUtil.ColorFromRgba(245, 245, 255, 200),
                    minPos: pos.AddCopy(-0.015, -0.015, -0.015),
                    maxPos: pos.AddCopy( 0.015,  0.015,  0.015),
                    minVelocity: vel,
                    maxVelocity: vel.AddCopy(0.02f, 0.02f, 0.02f),
                    lifeLength: PARTICLE_LIFE,
                    gravityEffect: PARTICLE_GRAV,
                    minSize: sizeBase,
                    maxSize: sizeMax
                )
                {
                    ParticleModel = EnumParticleModel.Quad,
                    OpacityEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -120),
                    SizeEvolve    = new EvolvingNatFloat(EnumTransformFunction.LINEAR, 0.1f)
                };

                byEntity.World.SpawnParticles(p);
            }
        }
        
        #endregion
        
        #region Rendering

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            int currentSpins = inSlot.Itemstack.Attributes.GetInt("spins", 0);
            int spinsNeeded = Attributes["spinsPerCompletion"].AsInt(2);

            if (currentSpins > 0)
            {
                float progress = (float)currentSpins / spinsNeeded * 100f;
                dsc.AppendLine(Lang.Get("spinningwheel:dropspindle-progress-info", progress.ToString("F0")));
                
                if (currentSpins >= spinsNeeded)
                {
                    string outputType = inSlot.Itemstack.Attributes.GetString("outputType");
                    if (!string.IsNullOrEmpty(outputType))
                    {
                        Item outputItem = world.GetItem(new AssetLocation(outputType));
                        if (outputItem != null)
                        {
                            dsc.AppendLine(Lang.Get("spinningwheel:dropspindle-ready", outputItem.GetHeldItemName(new ItemStack(outputItem))));
                        }
                    }
                }
            }
        }

        #endregion
    }
}