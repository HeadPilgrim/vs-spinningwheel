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
        
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            api.Logger.Debug("InteractStart: firstEvent={0}, blockSel={1}, sneak={2}", firstEvent, blockSel != null, byEntity.Controls.Sneak);
            
            if (blockSel != null || !firstEvent) return;

            IPlayer player = (byEntity as EntityPlayer)?.Player;
            if (player == null) return;
            
            api.Logger.Debug("Player valid. Sneak: {0}, Complete: {1}, Offhand empty: {2}", 
                byEntity.Controls.Sneak, 
                IsSpindleComplete(slot), 
                byEntity.LeftHandItemSlot?.Empty);

            // Check if sneaking and spindle is complete - extract twine
            if (byEntity.Controls.Sneak && IsSpindleComplete(slot))
            {
                if (api.Side == EnumAppSide.Server)
                {
                    ExtractTwine(slot, player);
                }
                handHandling = EnumHandHandling.PreventDefault;
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
                handHandling = EnumHandHandling.PreventDefault;
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
                handHandling = EnumHandHandling.PreventDefault;
                return;
            }

            // Start spinning
            handHandling = EnumHandHandling.PreventDefault;
        }
        
        // Put this at the top of the class (once)
        private readonly Random rand = new Random();

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel != null) return false;
            if (api.Side != EnumAppSide.Client) return secondsUsed < SPIN_ANIMATION_TIME;

            // Spawn burst every 0.1s
            if (secondsUsed % 0.1f > 0.03f) return secondsUsed < SPIN_ANIMATION_TIME;

            Vec3d center = byEntity.Pos.XYZ
                .Add(0, byEntity.LocalEyePos.Y - 1.08, 0)
                .Ahead(0.5f, byEntity.Pos.Pitch, byEntity.Pos.Yaw);

            // SPIRAL MATH
            float angle = secondsUsed * 9.42f;  // ~1.5 rev/sec
            float t = secondsUsed / SPIN_ANIMATION_TIME;  // 0 → 1

            float radius = GameMath.Lerp(0f, 0.22f, t);        // Grow from 0 → 0.22
            float yOff   = GameMath.Lerp(0.08f, -0.08f, t);    // Rise → fall

            int count = 4 + rand.Next(3);  // 4–6 particles per burst

            for (int i = 0; i < count; i++)
            {
                float localAngle = angle + i * (GameMath.PI * 2f / count);
                double dx = radius * Math.Cos(localAngle);
                double dz = radius * Math.Sin(localAngle);

                Vec3d pos = center.AddCopy(dx, yOff, dz);

                float tangSpeed = 0.12f + (float)rand.NextDouble() * 0.08f;
                Vec3f vel = new Vec3f(
                    (float)-Math.Sin(localAngle) * tangSpeed,
                    -0.02f + (float)rand.NextDouble() * 0.02f,
                    (float)Math.Cos(localAngle) * tangSpeed
                );

                var p = new SimpleParticleProperties(
                    minQuantity: 1,
                    maxQuantity: 1,
                    color: ColorUtil.ColorFromRgba(245, 245, 255, 180),
                    minPos: pos.AddCopy(-0.02, -0.02, -0.02),
                    maxPos: pos.AddCopy( 0.02,  0.02,  0.02),
                    minVelocity: vel,
                    maxVelocity: vel.AddCopy(0.03f, 0.03f, 0.03f),
                    lifeLength: 0.3f,
                    gravityEffect: 0.07f,
                    minSize: 0.1f,
                    maxSize: 0.1f
                )
                {
                    ParticleModel = EnumParticleModel.Quad,
                    OpacityEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -90),
                    SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, 0.2f)
                };

                byEntity.World.SpawnParticles(p);
            }

            return secondsUsed < SPIN_ANIMATION_TIME;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel != null || secondsUsed < SPIN_ANIMATION_TIME - 0.1f) return;

            IPlayer player = (byEntity as EntityPlayer)?.Player;
            if (player == null) return;

            ItemSlot offhandSlot = byEntity.LeftHandItemSlot;
            
            // Validate again
            if (IsSpindleComplete(slot) || offhandSlot?.Empty != false || !CanSpin(offhandSlot.Itemstack))
            {
                return;
            }

            // Server-side processing
            if (api.Side == EnumAppSide.Server)
            {
                ProcessSpin(slot, offhandSlot, byEntity);
            }
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

            // Play sound
            api.World.PlaySoundAt(new AssetLocation("game:sounds/player/spinning"), byEntity, null, false, 8);

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