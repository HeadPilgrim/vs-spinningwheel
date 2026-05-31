using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using SpinningWheel.ModConfig;

namespace SpinningWheel.Items
{
    public class ItemDropSpindle : Item
    {
        // Animation times in seconds
        private const float SPIN_ANIMATION_TIME = 2.0f;
        private const int   MAX_SPIN_VARIANTS   = 12;

        // Particle tuning — grouped here so BURST_INTERVAL is visible at the top of class
        private const float SPIN_SPEED      = 9.84f;  // 3 rev/sec → tight coils
        private const float MAX_RADIUS      = 0.12f;
        private const float MAX_Y_OFFSET    = -0.55f;
        private const float PARTICLE_LIFE   = 0.08f;
        private const float PARTICLE_GRAV   = 0.02f;
        private const float PARTICLE_MIN_SZ = 0.08f;
        private const float PARTICLE_MAX_SZ = 0.18f;
        private const float BURST_INTERVAL  = 0.03f;  // More bursts = denser helix
        private const int   MIN_PER_BURST   = 1;
        private const int   MAX_PER_BURST   = 2;

        // Sound keyed by player UID — Item is a singleton shared across all players.
        // A single ILoadedSound field would be overwritten/disposed by concurrent users.
        private readonly Dictionary<string, ILoadedSound> spindleSounds = new Dictionary<string, ILoadedSound>();

        // Cache for GetHeldInteractionHelp — built once, never changes at runtime.
        private ItemStack[] _spinnableStacksCache;

        private readonly Random rand = new Random();

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        }

        // ------------------------------------------------------------------
        #region Held Interact

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (!firstEvent) return;

            IPlayer player = (byEntity as EntityPlayer)?.Player;
            if (player == null) return;

            // Check if sneaking and spindle is complete — extract twine
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

            // Check if spindle is complete (ready to extract twine) — give hint
            if (IsSpindleComplete(slot))
            {
                if (api.Side == EnumAppSide.Client)
                {
                    (api as ICoreClientAPI).TriggerIngameError(this, "complete",
                        Lang.Get("spinningwheel:dropspindle-extract-hint"));
                }
                handHandling = EnumHandHandling.NotHandled;
                return;
            }

            // Replaced confusing nullable-boolean idiom with explicit null/empty checks.
            if (offhandSlot == null || offhandSlot.Empty || !CanSpin(offhandSlot.Itemstack))
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

                // Use per-player sound dictionary so concurrent users don't clobber each other.
                string uid = player.PlayerUID;
                if (spindleSounds.TryGetValue(uid, out var existing))
                {
                    existing?.Stop();
                    existing?.Dispose();
                    spindleSounds.Remove(uid);
                }

                var sound = (api as ICoreClientAPI).World.LoadSound(new SoundParams()
                {
                    Location      = new AssetLocation("spinningwheel:sounds/item/dropspindle"),
                    ShouldLoop    = false,
                    Position      = byEntity.Pos.XYZ.ToVec3f(),
                    DisposeOnFinish = true,
                    Volume        = 0.5f,
                    Range         = 8
                });
                sound?.Start();
                if (sound != null) spindleSounds[uid] = sound;
            }

            handHandling = EnumHandHandling.PreventDefaultAction;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot,
            EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            int renderVariant = GameMath.Clamp(
                (int)Math.Ceiling(secondsUsed / SPIN_ANIMATION_TIME * MAX_SPIN_VARIANTS),
                1,
                MAX_SPIN_VARIANTS
            );
            
            int prevRenderVariant = slot.Itemstack.TempAttributes.GetInt("renderVariant", 0);
            slot.Itemstack.TempAttributes.SetInt("renderVariant", renderVariant);

            if (prevRenderVariant != renderVariant)
            {
                (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();
            }
            
            if (api.Side == EnumAppSide.Server)
            {
                if (secondsUsed % BURST_INTERVAL > 0.03f) return secondsUsed < SPIN_ANIMATION_TIME;
                SpawnSpiralBurst(byEntity, secondsUsed);
            }

            return secondsUsed < SPIN_ANIMATION_TIME;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            // Sound is client-only; variant cleanup must run on both sides so other clients
            // don't get stuck on the last animation frame after the interaction ends.
            if (api.Side == EnumAppSide.Client)
            {
                StopSound((byEntity as EntityPlayer)?.Player?.PlayerUID);
            }
            slot.Itemstack?.TempAttributes.RemoveAttribute("renderVariant");

            if (secondsUsed < SPIN_ANIMATION_TIME - 0.1f) return;

            IPlayer player = (byEntity as EntityPlayer)?.Player;
            if (player == null) return;

            ItemSlot offhandSlot = byEntity.LeftHandItemSlot;

            // Replaced confusing nullable-boolean idiom with explicit null/empty checks.
            if (IsSpindleComplete(slot) || offhandSlot == null || offhandSlot.Empty || !CanSpin(offhandSlot.Itemstack))
            {
                return;
            }

            if (api.Side == EnumAppSide.Server)
            {
                // Guard against double-processing within the same tick / rapid re-trigger.
                // Uses wall-clock milliseconds — simple and sufficient for a per-interaction guard.
                long lastProcessTime = slot.Itemstack.Attributes.GetLong("lastSpinTime", 0);
                long currentTime     = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                if (currentTime - lastProcessTime < 500)
                {
                    api.Logger.Debug("[DropSpindle] Prevented double-spin ({0}ms since last) for player {1}",
                        currentTime - lastProcessTime, player.PlayerName);
                    return;
                }

                // Pass currentTime into ProcessSpin so lastSpinTime is written in the same
                // contiguous attribute pass as spins/outputType/outputQuantity, directly before
                // MarkDirty(). This keeps all writes to the spindle slot in one batch, reducing
                // the window in which a delta-sync optimiser (e.g. Synergy) could capture a
                // partial attribute state.
                ProcessSpin(slot, offhandSlot, byEntity, currentTime);
            }
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            if (api.Side == EnumAppSide.Client)
            {
                StopSound((byEntity as EntityPlayer)?.Player?.PlayerUID);
            }
            slot.Itemstack?.TempAttributes.RemoveAttribute("renderVariant");

            return true;
        }

        #endregion
        
        #region Interaction Help / Attack Overrides

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            if (IsSpindleComplete(inSlot))
            {
                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "spinningwheel:dropspindle-extract",
                        MouseButton    = EnumMouseButton.Right,
                        HotKeyCode     = "sneak"
                    }
                };
            }

            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "spinningwheel:dropspindle-spin",
                    MouseButton    = EnumMouseButton.Right,
                    Itemstacks     = GetSpinnableStacks()
                }
            };
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
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

        #endregion
        
        #region Spinning Logic

        private bool CanSpin(ItemStack itemstack)
        {
            if (itemstack?.ItemAttributes == null) return false;
            return itemstack.ItemAttributes.KeyExists("spinningProps");
        }

        private void ProcessSpin(ItemSlot spindleSlot, ItemSlot fiberSlot, EntityAgent byEntity, long currentTime)
        {
            var spinningProps  = fiberSlot.Itemstack.ItemAttributes["spinningProps"];
            int inputQuantity  = spinningProps["inputQuantity"]?.AsInt(2) ?? 2;

            if (fiberSlot.Itemstack.StackSize < inputQuantity) return;

            int currentSpins = spindleSlot.Itemstack.Attributes.GetInt("spins", 0);
            int spinsNeeded  = Attributes["spinsPerCompletion"].AsInt(2);

            string newOutputType = spinningProps["outputType"].AsString();

            if (currentSpins == 0)
            {
                // First spin — lock in the output type and quantity.
                spindleSlot.Itemstack.Attributes.SetString("outputType", newOutputType);

                int outputQuantity = spinningProps["outputQuantity"]?.AsInt(1) ?? 1;
                spindleSlot.Itemstack.Attributes.SetInt("outputQuantity", outputQuantity);
            }
            else
            {
                // Validate that subsequent spins use the same fiber type.
                // Switching fibers mid-spin would silently produce the wrong output.
                string lockedOutputType = spindleSlot.Itemstack.Attributes.GetString("outputType");
                if (lockedOutputType != newOutputType)
                {
                    IServerPlayer serverPlayer = (byEntity as EntityPlayer)?.Player as IServerPlayer;
                    Item lockedItem = api.World.GetItem(new AssetLocation(lockedOutputType));
                    string lockedItemName = lockedItem != null
                        ? lockedItem.GetHeldItemName(new ItemStack(lockedItem))
                        : lockedOutputType;

                    serverPlayer?.SendMessage(GlobalConstants.InfoLogChatGroup,
                        Lang.Get("spinningwheel:dropspindle-wrong-fiber", lockedItemName), EnumChatType.Notification);
                    return;
                }
            }

            currentSpins++;

            // SYNERGY: All spindleSlot attribute writes are intentionally batched here in one
            // contiguous block, with MarkDirty() called once at the very end. This ensures any
            // delta-sync optimiser (e.g. Synergy's Attribute Sync Delta Updates patch) always
            // captures a fully consistent snapshot rather than a partial write state.
            spindleSlot.Itemstack.Attributes.SetInt("spins", currentSpins);
            spindleSlot.Itemstack.Attributes.SetLong("lastSpinTime", currentTime);

            fiberSlot.TakeOut(inputQuantity);
            fiberSlot.MarkDirty();

            int durabilityCost = Attributes["spinDurabilityCost"]?.AsInt(1) ?? 1;
            DamageItem(api.World, byEntity, spindleSlot, durabilityCost);

            spindleSlot.MarkDirty();

            // Progress messages
            if (ModConfig.ModConfig.Loaded.ShowDropSpindleProgressMessages)
            {
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
        }

        private bool IsSpindleComplete(ItemSlot slot)
        {
            int currentSpins = slot.Itemstack.Attributes.GetInt("spins", 0);
            int spinsNeeded  = Attributes["spinsPerCompletion"].AsInt(2);
            return currentSpins >= spinsNeeded;
        }

        private void ExtractTwine(ItemSlot spindleSlot, IPlayer player)
        {
            string outputType    = spindleSlot.Itemstack.Attributes.GetString("outputType");
            int    outputQuantity = spindleSlot.Itemstack.Attributes.GetInt("outputQuantity", 1);

            if (string.IsNullOrEmpty(outputType)) return;

            Item outputItem = api.World.GetItem(new AssetLocation(outputType));
            if (outputItem == null) return;

            ItemStack outputStack = new ItemStack(outputItem, outputQuantity);

            if (!player.InventoryManager.TryGiveItemstack(outputStack))
            {
                api.World.SpawnItemEntity(outputStack, player.Entity.Pos.XYZ);
            }

            spindleSlot.Itemstack.Attributes.RemoveAttribute("spins");
            spindleSlot.Itemstack.Attributes.RemoveAttribute("outputType");
            spindleSlot.Itemstack.Attributes.RemoveAttribute("outputQuantity");
            spindleSlot.MarkDirty();

            api.World.PlaySoundAt(new AssetLocation("game:sounds/player/collect"), player.Entity, null, false, 8);
        }

        // Cached — no need to walk all world collectibles on every tooltip render.
        private ItemStack[] GetSpinnableStacks()
        {
            if (_spinnableStacksCache != null) return _spinnableStacksCache;

            var items          = api.World.Collectibles;
            var spinnableStacks = new List<ItemStack>();

            foreach (var collectible in items)
            {
                if (collectible.Attributes?.KeyExists("spinningProps") == true)
                {
                    spinnableStacks.Add(new ItemStack(collectible));
                    if (spinnableStacks.Count >= 3) break;
                }
            }

            _spinnableStacksCache = spinnableStacks.ToArray();
            return _spinnableStacksCache;
        }

        #endregion
        
        #region Particles

        private void SpawnSpiralBurst(EntityAgent byEntity, float secondsUsed)
        {
            Vec3d center = byEntity.Pos.XYZ
                .Add(0, byEntity.LocalEyePos.Y - 1.08, 0)
                .Ahead(0.5f, byEntity.Pos.Pitch, byEntity.Pos.Yaw);

            float t      = secondsUsed / SPIN_ANIMATION_TIME;
            float angle  = secondsUsed * SPIN_SPEED;
            float radius = GameMath.Lerp(MAX_RADIUS, 0.08f, t);
            float yOff   = GameMath.Lerp(0, MAX_Y_OFFSET, t);

            int count = MIN_PER_BURST + rand.Next(MAX_PER_BURST - MIN_PER_BURST + 1);

            for (int i = 0; i < count; i++)
            {
                float  localAngle = angle + i * (GameMath.PI * 2f / count);
                double dx  = radius * Math.Cos(localAngle);
                double dz  = radius * Math.Sin(localAngle);
                Vec3d  pos = center.AddCopy(dx, yOff, dz);

                float tang = 0.15f + (float)rand.NextDouble() * 0.1f;
                float velY = GameMath.Lerp(0.06f, -0.08f, t);
                Vec3f vel  = new Vec3f(
                    (float)-Math.Sin(localAngle) * tang,
                    velY + (float)rand.NextDouble() * 0.03f,
                    (float) Math.Cos(localAngle) * tang
                );

                float sizeFactor = 1f - t;
                float sizeBase   = PARTICLE_MIN_SZ + (PARTICLE_MAX_SZ - PARTICLE_MIN_SZ) * sizeFactor;
                float sizeMax    = sizeBase + 0.08f;

                var p = new SimpleParticleProperties(
                    minQuantity: 1, maxQuantity: 1,
                    color:        ColorUtil.ColorFromRgba(245, 245, 255, 200),
                    minPos:       pos.AddCopy(-0.015, -0.015, -0.015),
                    maxPos:       pos.AddCopy( 0.015,  0.015,  0.015),
                    minVelocity:  vel,
                    maxVelocity:  vel.AddCopy(0.02f, 0.02f, 0.02f),
                    lifeLength:   PARTICLE_LIFE,
                    gravityEffect: PARTICLE_GRAV,
                    minSize:      sizeBase,
                    maxSize:      sizeMax
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
            int spinsNeeded  = Attributes["spinsPerCompletion"].AsInt(2);

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
                            dsc.AppendLine(Lang.Get("spinningwheel:dropspindle-ready",
                                outputItem.GetHeldItemName(new ItemStack(outputItem))));
                        }
                    }
                }
            }
        }

        #endregion
        
        #region Helpers

        /// <summary>
        /// Stops and disposes the sound for the given player UID, then removes it from the dictionary.
        /// Safe to call with a null uid.
        /// </summary>
        private void StopSound(string uid)
        {
            if (uid == null) return;
            if (spindleSounds.TryGetValue(uid, out var sound))
            {
                sound?.Stop();
                sound?.Dispose();
                spindleSounds.Remove(uid);
            }
        }

        #endregion
    }
}
