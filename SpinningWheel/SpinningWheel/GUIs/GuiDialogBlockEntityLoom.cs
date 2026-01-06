using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Config;
using SpinningWheel.BlockEntities;
using SpinningWheel.BlockEntityPackets;
using ProtoBuf;
using System.IO;

namespace SpinningWheel.GUIs
{
    public class GuiDialogBlockEntityLoom : GuiDialogBlockEntity
    {
        private float inputWeaveTime;
        private float maxWeaveTime;
        private bool isWeaving;
        private long lastUpdateMs;

        // Tabbed interface support
        private int currentTabIndex = 0; // 0 = Normal, 1 = Pattern
        private bool hasPatternWeavingEnabled = false;

        protected override double FloatyDialogPosition => 0.75;

        public GuiDialogBlockEntityLoom(string DialogTitle, InventoryBase Inventory, BlockPos BlockEntityPosition, ICoreClientAPI capi)
            : base(DialogTitle, Inventory, BlockEntityPosition, capi)
        {
            if (IsDuplicate) return;

            capi.World.Player.InventoryManager.OpenInventory(Inventory);

            // Detect if pattern weaving is enabled
            if (capi.World.BlockAccessor.GetBlockEntity(BlockEntityPosition) is BlockEntityFlyShuttleLoom loomEntity)
            {
                hasPatternWeavingEnabled = loomEntity.HasPatternWeavingEnabled;
                currentTabIndex = (int)loomEntity.CurrentWeavingMode;
            }

            SetupDialog();
        }

        private void OnInventorySlotModified(int slotid)
        {
            // Direct call can cause InvalidOperationException
            capi.Event.EnqueueMainThreadTask(SetupDialog, "setuploomdlg");
        }

        void SetupDialog()
        {
            ItemSlot? hoveredSlot = capi.World.Player.InventoryManager.CurrentHoveredSlot;
            if (hoveredSlot != null && hoveredSlot.Inventory == Inventory)
            {
                capi.Input.TriggerOnMouseLeaveSlot(hoveredSlot);
            }
            else
            {
                hoveredSlot = null;
            }

            // Calculate layout dimensions based on whether tabs are shown
            double titleBarHeight = 31; // Standard VS title bar height
            double tabHeight = 30;
            double tabGap = 5;

            double tabsTop = hasPatternWeavingEnabled ? titleBarHeight : 0;
            double contentTop = hasPatternWeavingEnabled ? (titleBarHeight + tabHeight + tabGap) : 0;

            double contentHeight = currentTabIndex == 0 ? 90 : 120;
            double contentWidth = currentTabIndex == 0 ? 380 : 320;

            // Tab button bounds (if pattern weaving is enabled) - positioned below title bar
            ElementBounds normalTabBounds = hasPatternWeavingEnabled ? ElementBounds.Fixed(0, tabsTop, 100, tabHeight) : null;
            ElementBounds patternTabBounds = hasPatternWeavingEnabled ? ElementBounds.Fixed(105, tabsTop, 100, tabHeight) : null;
            ElementBounds tabContainerBounds = hasPatternWeavingEnabled ? ElementBounds.Fixed(0, 0, 210, titleBarHeight + tabHeight + tabGap) : null;

            // Content area bounds - positioned below tabs (or below title if no tabs)
            ElementBounds loomBounds = ElementBounds.Fixed(0, contentTop, contentWidth, contentHeight);

            // Background bounds with proper padding
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            if (hasPatternWeavingEnabled)
            {
                bgBounds.WithChildren(tabContainerBounds, loomBounds);
            }
            else
            {
                bgBounds.WithChildren(loomBounds);
            }

            // Dialog bounds
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

            ClearComposers();

            var composer = capi.Gui
                .CreateCompo("blockentityloom" + BlockEntityPosition, dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .BeginChildElements(bgBounds);

            // Add tab buttons if pattern weaving is enabled
            if (hasPatternWeavingEnabled)
            {
                string normalText = Lang.Get("spinningwheel:gui-loom-tab-normal");
                string patternText = Lang.Get("spinningwheel:gui-loom-tab-pattern");

                // Use different button styles to indicate active tab
                EnumButtonStyle normalStyle = currentTabIndex == 0 ? EnumButtonStyle.MainMenu : EnumButtonStyle.Normal;
                EnumButtonStyle patternStyle = currentTabIndex == 1 ? EnumButtonStyle.MainMenu : EnumButtonStyle.Normal;

                composer
                    .AddSmallButton(normalText, OnNormalTabClick, normalTabBounds, normalStyle, "normalTab")
                    .AddSmallButton(patternText, OnPatternTabClick, patternTabBounds, patternStyle, "patternTab");
            }

            // Add content based on current tab
            composer.AddDynamicCustomDraw(loomBounds, new DrawDelegateWithBounds(OnBgDraw), "symbolDrawer");

            if (currentTabIndex == 0) // Normal mode
            {
                // 3 Input slots arranged horizontally + output slot
                // Position slots within the content area (after tabs)
                ElementBounds inputSlotsBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, contentTop + 30, 3, 1);
                ElementBounds outputSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 330, contentTop + 30, 1, 1);

                composer
                    .AddItemSlotGrid(Inventory, SendInvPacket, 3, new int[] { 0, 1, 2 }, inputSlotsBounds, "inputSlots")
                    .AddItemSlotGrid(Inventory, SendInvPacket, 1, new int[] { 3 }, outputSlotBounds, "outputslot");
            }
            else // Pattern mode
            {
                // Pattern grid: 2x2 (slots 4-7) + output slot
                // Position slots within the content area (after tabs)
                ElementBounds patternTopRowBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, contentTop + 30, 2, 1);
                ElementBounds patternBottomRowBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, contentTop + 80, 2, 1);
                ElementBounds outputSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 270, contentTop + 55, 1, 1);

                composer
                    .AddItemSlotGrid(Inventory, SendInvPacket, 2, new int[] { 4, 5 }, patternTopRowBounds, "patternTopRow")
                    .AddItemSlotGrid(Inventory, SendInvPacket, 2, new int[] { 6, 7 }, patternBottomRowBounds, "patternBottomRow")
                    .AddItemSlotGrid(Inventory, SendInvPacket, 1, new int[] { 3 }, outputSlotBounds, "outputslot");
            }

            SingleComposer = composer.EndChildElements().Compose();

            lastUpdateMs = capi.ElapsedMilliseconds;

            if (hoveredSlot != null)
            {
                SingleComposer.OnMouseMove(new MouseEvent(capi.Input.MouseX, capi.Input.MouseY));
            }
        }

        public void Update(float inputWeaveTime, float maxWeaveTime)
        {
            bool hasChanged = false;

            // Check if values have actually changed
            if (System.Math.Abs(this.inputWeaveTime - inputWeaveTime) > 0.01f)
            {
                this.inputWeaveTime = inputWeaveTime;
                hasChanged = true;
            }

            if (System.Math.Abs(this.maxWeaveTime - maxWeaveTime) > 0.01f)
            {
                this.maxWeaveTime = maxWeaveTime;
                hasChanged = true;
            }

            // Determine if we're actively weaving
            bool wasWeaving = isWeaving;
            isWeaving = inputWeaveTime > 0 && maxWeaveTime > 0;

            if (!IsOpened()) return;

            // Redraw conditions:
            // 1. Values changed
            // 2. Started or stopped weaving
            // 3. Currently weaving and enough time has passed (smooth updates)
            long now = capi.ElapsedMilliseconds;
            bool shouldRedraw = hasChanged ||
                               (wasWeaving != isWeaving) ||
                               (isWeaving && now - lastUpdateMs > 50); // 20 FPS for smooth animation

            if (shouldRedraw)
            {
                if (SingleComposer != null)
                {
                    SingleComposer.GetCustomDraw("symbolDrawer").Redraw();
                }
                lastUpdateMs = now;
            }
        }

        private void OnBgDraw(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        {
            double slotSize = 50; // Standard slot height
            double barHeight = 20;

            double slotTop, left, barWidth, top;

            if (currentTabIndex == 0) // Normal mode
            {
                // Position the progress bar centered between input and output slots
                // Slots are at Y=30 relative to loomBounds
                slotTop = 30;

                // Center the bar vertically with the slots
                top = slotTop + (slotSize - barHeight) / 2;

                // Position bar between input slots (3 slots = ~150px wide) and output slot (at x=330)
                // Input slots end at approximately x=150, output starts at x=330
                left = 170;  // Space after input slots
                barWidth = 140; // Bar width to span the gap nicely
            }
            else // Pattern mode
            {
                // Position the progress bar to the right of the 2x2 grid
                // Pattern grid starts at Y=30, center is at Y=55 (30 + 50/2)
                slotTop = 55; // Align with center of 2x2 grid

                // Center the bar vertically with the slots
                top = slotTop + (slotSize - barHeight) / 2;

                // Position bar between pattern grid (2x2 = ~100px wide) and output slot (at x=270)
                // Pattern grid ends at approximately x=100, output starts at x=270
                left = 120;  // Space after pattern grid
                barWidth = 130; // Bar width to span the gap nicely
            }

            // Calculate progress (0.0 to 1.0)
            double progress = maxWeaveTime > 0 ? System.Math.Min(inputWeaveTime / maxWeaveTime, 1.0) : 0;

            ctx.Save();

            // Draw background/border
            ctx.SetSourceRGBA(0.2, 0.2, 0.2, 1);
            ctx.Rectangle(
                GuiElement.scaled(left),
                GuiElement.scaled(top),
                GuiElement.scaled(barWidth),
                GuiElement.scaled(barHeight)
            );
            ctx.Fill();

            // Draw progress fill
            if (progress > 0.001)
            {
                LinearGradient gradient = new LinearGradient(
                    GuiElement.scaled(left), 0,
                    GuiElement.scaled(left + barWidth), 0
                );

                // Dynamic color based on progress
                if (progress < 0.3)
                {
                    gradient.AddColorStop(0, new Color(0.8, 0.3, 0.3, 1)); // Reddish - just started
                    gradient.AddColorStop(1, new Color(0.9, 0.4, 0.4, 1));
                }
                else if (progress < 0.7)
                {
                    gradient.AddColorStop(0, new Color(0.9, 0.7, 0.3, 1)); // Orange/yellow - mid progress
                    gradient.AddColorStop(1, new Color(1.0, 0.8, 0.4, 1));
                }
                else
                {
                    gradient.AddColorStop(0, new Color(0.3, 0.8, 0.3, 1)); // Green - almost done
                    gradient.AddColorStop(1, new Color(0.4, 0.9, 0.4, 1));
                }

                ctx.SetSource(gradient);
                ctx.Rectangle(
                    GuiElement.scaled(left + 2),
                    GuiElement.scaled(top + 2),
                    GuiElement.scaled((barWidth - 4) * progress),
                    GuiElement.scaled(barHeight - 4)
                );
                ctx.Fill();
                gradient.Dispose();
            }

            // Draw border/outline
            ctx.SetSourceRGBA(0.6, 0.6, 0.6, 1);
            ctx.LineWidth = GuiElement.scaled(1);
            ctx.Rectangle(
                GuiElement.scaled(left),
                GuiElement.scaled(top),
                GuiElement.scaled(barWidth),
                GuiElement.scaled(barHeight)
            );
            ctx.Stroke();

            // Draw percentage text (centered in the bar)
            if (isWeaving && maxWeaveTime > 0)
            {
                string progressText = $"{(progress * 100):F0}%";

                ctx.SetSourceRGBA(1, 1, 1, 0.9);
                ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Bold);
                ctx.SetFontSize(GuiElement.scaled(11));

                TextExtents extents = ctx.TextExtents(progressText);
                ctx.MoveTo(
                    GuiElement.scaled(left + barWidth/2) - extents.Width / 2,
                    GuiElement.scaled(top + barHeight/2) + extents.Height / 2
                );
                ctx.ShowText(progressText);
            }

            ctx.Restore();
        }

        private void SendInvPacket(object p)
        {
            capi.Network.SendBlockEntityPacket(BlockEntityPosition.X, BlockEntityPosition.Y, BlockEntityPosition.Z, p);
        }

        private void OnTitleBarClose()
        {
            TryClose();
        }

        private bool OnNormalTabClick()
        {
            if (currentTabIndex != 0)
            {
                currentTabIndex = 0;

                // Send packet to server to change mode
                var packet = new SetWeavingModePacket { WeavingMode = (int)WeavingMode.Normal };
                using (MemoryStream ms = new MemoryStream())
                {
                    Serializer.Serialize(ms, packet);
                    capi.Network.SendBlockEntityPacket(BlockEntityPosition.X, BlockEntityPosition.Y, BlockEntityPosition.Z, 1001, ms.ToArray());
                }

                SetupDialog();
            }
            return true;
        }

        private bool OnPatternTabClick()
        {
            if (currentTabIndex != 1)
            {
                currentTabIndex = 1;

                // Send packet to server to change mode
                var packet = new SetWeavingModePacket { WeavingMode = (int)WeavingMode.Pattern };
                using (MemoryStream ms = new MemoryStream())
                {
                    Serializer.Serialize(ms, packet);
                    capi.Network.SendBlockEntityPacket(BlockEntityPosition.X, BlockEntityPosition.Y, BlockEntityPosition.Z, 1001, ms.ToArray());
                }

                SetupDialog();
            }
            return true;
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            Inventory.SlotModified += OnInventorySlotModified;
        }

        public override void OnGuiClosed()
        {
            Inventory.SlotModified -= OnInventorySlotModified;

            if (currentTabIndex == 0) // Normal mode
            {
                SingleComposer.GetSlotGrid("inputSlots")?.OnGuiClosed(capi);
            }
            else // Pattern mode
            {
                SingleComposer.GetSlotGrid("patternTopRow")?.OnGuiClosed(capi);
                SingleComposer.GetSlotGrid("patternBottomRow")?.OnGuiClosed(capi);
            }

            SingleComposer.GetSlotGrid("outputslot")?.OnGuiClosed(capi);

            base.OnGuiClosed();
        }
    }
}
