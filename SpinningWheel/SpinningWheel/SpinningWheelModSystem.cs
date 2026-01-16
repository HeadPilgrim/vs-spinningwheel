namespace SpinningWheel.ModSystem {

    using SpinningWheel.BlockEntities;
    using SpinningWheel.Blocks;
    using SpinningWheel.Items;
    using SpinningWheel.Configuration;
    using Vintagestory.API.Client;
    using Vintagestory.API.Server;
    using Vintagestory.API.Common;
    using SpinningWheel.ModConfig;
    using SpinningWheel.Recipes;
    using System;

    public class SpinningWheelModSystem : ModSystem
    {
        private readonly string thisModID = "spinningwheel";
        private ICoreClientAPI clientApi;
        private ICoreAPI api;
        private IServerNetworkChannel serverChannel;
        private SpinningWheelConfigPatcher configPatcher;

        // Pattern weaving support (requires tailorsdelight and wool mods)
        private bool hasTailorsDelightAndWool = false;
        private LoomPatternRecipeLoader patternRecipeLoader;

        public bool HasPatternWeavingEnabled => hasTailorsDelightAndWool;
        public LoomPatternRecipeLoader PatternRecipeLoader => patternRecipeLoader;

        // Called very early, before assets are loaded
        public override void StartPre(ICoreAPI api)
        {
            // Register classes FIRST, before anything else
            api.RegisterBlockClass("BlockSpinningWheel", typeof(BlockSpinningWheel));
            api.RegisterBlockEntityClass("BlockEntitySpinningWheel", typeof(BlockEntitySpinningWheel));
            api.RegisterMountable("spinningWheel", BlockSpinningWheel.GetMountable);

            api.RegisterBlockClass("BlockFlyShuttleLoom", typeof(BlockFlyShuttleLoom));
            api.RegisterBlockEntityClass("BlockEntityFlyShuttleLoom", typeof(BlockEntityFlyShuttleLoom));
            api.RegisterMountable("flyShuttleLoom", BlockFlyShuttleLoom.GetMountable);

            api.RegisterItemClass("ItemDropSpindle", typeof(ItemDropSpindle));

            api.Logger.Notification("[Immersive Fibercraft] Registered block, block entity, and mountable classes");
            api.Logger.Notification("[Immersive Fibercraft] Registered ItemDropSpindle for portable spinning");
            
            // Load/create common config file in ..\VintageStoryData\ModConfig\thisModID
            var cfgFileName = this.thisModID + ".json";
            try
            {
                ModConfig fromDisk;
                if ((fromDisk = api.LoadModConfig<ModConfig>(cfgFileName)) == null)
                { 
                    api.StoreModConfig(ModConfig.Loaded, cfgFileName);
                    api.Logger.Notification("[Immersive Fibercraft] Created new config file with default values spinningwheel.json");
                }
                else
                { 
                    ModConfig.Loaded = fromDisk;
                    api.Logger.Notification("[Immersive Fibercraft] Loaded config from disk");
                }
            }
            catch (Exception ex)
            {
                api.Logger.Error("[Immersive Fibercraft] Error loading config, creating new one: " + ex.Message);
                api.StoreModConfig(ModConfig.Loaded, cfgFileName);
            }
            
            base.StartPre(api);
        }

        // Called on server and client after StartPre
        public override void Start(ICoreAPI api)
        {
            this.api = api;
            base.Start(api);
            
            api.World.Logger.Event("started 'Immersive Fibercraft' mod");
        }
        
        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);

            // Detect required mods for pattern weaving using ModLoader API
            bool hasTailorsDelight = api.ModLoader.IsModEnabled("tailorsdelight");
            bool hasWool = api.ModLoader.IsModEnabled("wool");
            hasTailorsDelightAndWool = hasTailorsDelight && hasWool;

            api.Logger.Notification($"[Immersive Fibercraft] Mod detection - tailorsdelight: {hasTailorsDelight}, wool: {hasWool}");

            if (hasTailorsDelightAndWool)
            {
                api.Logger.Notification("[Immersive Fibercraft] Detected tailorsdelight and wool mods - enabling pattern weaving");

                // Load pattern recipes
                patternRecipeLoader = new LoomPatternRecipeLoader(api);
                patternRecipeLoader.LoadPatternRecipes();
            }
            else
            {
                api.Logger.Notification("[Immersive Fibercraft] Pattern weaving disabled (requires both tailorsdelight and wool mods)");
            }

            // Note: Config-based patches (recipe disabling, spinning/weaving properties) are applied
            // in AssetsFinalize when GridRecipes and Collectibles are fully loaded
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            base.AssetsFinalize(api);
    
            // Patch all spinnable items to allow offhand placement
            PatchSpinnableItems(api);
            
            // Apply config-based patches using the dedicated patcher class
            configPatcher = new SpinningWheelConfigPatcher(api, ModConfig.Loaded);
            configPatcher.ApplyAllPatches();
        }

        private void PatchSpinnableItems(ICoreAPI api)
        {
            int patchedCount = 0;
    
            foreach (var collectible in api.World.Collectibles)
            {
                // Skip null collectibles
                if (collectible?.Attributes == null) continue;
        
                // Check if item has spinningProps
                if (collectible.Attributes.KeyExists("spinningProps"))
                {
                    EnumItemStorageFlags currentFlags = collectible.StorageFlags;
            
                    // Check if offhand flag is missing
                    if (!currentFlags.HasFlag(EnumItemStorageFlags.Offhand))
                    {
                        // Add offhand flag while preserving all other flags
                        collectible.StorageFlags = currentFlags | EnumItemStorageFlags.Offhand;
                
                        api.Logger.Notification(
                            $"[Immersive Fibercraft] Patched {collectible.Code} storage flags: {currentFlags} -> {collectible.StorageFlags}"
                        );
                
                        patchedCount++;
                    }
                }
            }
    
            if (patchedCount > 0)
            {
                api.Logger.Notification($"[Immersive Fibercraft] Successfully patched {patchedCount} spinnable items for offhand compatibility");
            }
        }
        
        public override void StartClientSide(ICoreClientAPI capi)
        { 
            capi.Network.RegisterChannel("spinningwheel")
                .RegisterMessageType<SyncClientPacket>()
                .SetMessageHandler<SyncClientPacket>(packet =>
                {
                    // Class/Trait restrictions
                    ModConfig.Loaded.RequireClassOrTrait = packet.RequireClassOrTrait;
                    this.Mod.Logger.Event($"Received RequireClassOrTrait of {packet.RequireClassOrTrait} from server");
                    ModConfig.Loaded.AllowedClasses = packet.AllowedClasses;
                    this.Mod.Logger.Event($"Received AllowedClasses from server: {string.Join(", ", packet.AllowedClasses ?? new string[0])}");
                    ModConfig.Loaded.AllowedTraits = packet.AllowedTraits;
                    this.Mod.Logger.Event($"Received AllowedTraits from server: {string.Join(", ", packet.AllowedTraits ?? new string[0])}");
                    
                    // Recipe control
                    ModConfig.Loaded.DisableTwineGridRecipes = packet.DisableTwineGridRecipes;
                    this.Mod.Logger.Event($"Received DisableTwineGridRecipes of {packet.DisableTwineGridRecipes} from server");

                    // Drop spindle chat messages
                    ModConfig.Loaded.ShowDropSpindleProgressMessages = packet.ShowDropSpindleProgressMessages;
                    this.Mod.Logger.Event($"Received ShowDropSpindleProgressMessages of {packet.ShowDropSpindleProgressMessages} from server");

                    // Vanilla flax settings
                    ModConfig.Loaded.FlaxSpinTime = packet.FlaxSpinTime;
                    ModConfig.Loaded.FlaxInputQuantity = packet.FlaxInputQuantity;
                    ModConfig.Loaded.FlaxOutputQuantity = packet.FlaxOutputQuantity;
                    this.Mod.Logger.Event($"Received Flax settings from server: SpinTime={packet.FlaxSpinTime}, Input={packet.FlaxInputQuantity}, Output={packet.FlaxOutputQuantity}");
                    
                    // Cotton settings
                    ModConfig.Loaded.CottonSpinTime = packet.CottonSpinTime;
                    ModConfig.Loaded.CottonInputQuantity = packet.CottonInputQuantity;
                    ModConfig.Loaded.CottonOutputQuantity = packet.CottonOutputQuantity;
                    this.Mod.Logger.Event($"Received Cotton settings from server: SpinTime={packet.CottonSpinTime}, Input={packet.CottonInputQuantity}, Output={packet.CottonOutputQuantity}");
                    
                    // Wool fiber settings
                    ModConfig.Loaded.WoolFiberSpinTime = packet.WoolFiberSpinTime;
                    ModConfig.Loaded.WoolFiberInputQuantity = packet.WoolFiberInputQuantity;
                    ModConfig.Loaded.WoolFiberOutputQuantity = packet.WoolFiberOutputQuantity;
                    this.Mod.Logger.Event($"Received Wool Fiber settings from server: SpinTime={packet.WoolFiberSpinTime}, Input={packet.WoolFiberInputQuantity}, Output={packet.WoolFiberOutputQuantity}");
                    
                    // Wool twine settings
                    ModConfig.Loaded.WoolTwineSpinTime = packet.WoolTwineSpinTime;
                    ModConfig.Loaded.WoolTwineInputQuantity = packet.WoolTwineInputQuantity;
                    ModConfig.Loaded.WoolTwineOutputQuantity = packet.WoolTwineOutputQuantity;
                    this.Mod.Logger.Event($"Received Wool Twine settings from server: SpinTime={packet.WoolTwineSpinTime}, Input={packet.WoolTwineInputQuantity}, Output={packet.WoolTwineOutputQuantity}");
                    
                    // Papyrus settings
                    ModConfig.Loaded.PapyrusSpinTime = packet.PapyrusSpinTime;
                    ModConfig.Loaded.PapyrusInputQuantity = packet.PapyrusInputQuantity;
                    ModConfig.Loaded.PapyrusOutputQuantity = packet.PapyrusOutputQuantity;
                    this.Mod.Logger.Event($"Received Papyrus settings from server: SpinTime={packet.PapyrusSpinTime}, Input={packet.PapyrusInputQuantity}, Output={packet.PapyrusOutputQuantity}");
                    
                    // Algae settings
                    ModConfig.Loaded.AlgaeSpinTime = packet.AlgaeSpinTime;
                    ModConfig.Loaded.AlgaeInputQuantity = packet.AlgaeInputQuantity;
                    ModConfig.Loaded.AlgaeOutputQuantity = packet.AlgaeOutputQuantity;
                    this.Mod.Logger.Event($"Received Algae settings from server: SpinTime={packet.AlgaeSpinTime}, Input={packet.AlgaeInputQuantity}, Output={packet.AlgaeOutputQuantity}");

                    // Flax twine weaving settings
                    ModConfig.Loaded.FlaxTwineWeaveInputQuantity = packet.FlaxTwineWeaveInputQuantity;
                    ModConfig.Loaded.FlaxTwineWeaveOutputQuantity = packet.FlaxTwineWeaveOutputQuantity;
                    this.Mod.Logger.Event($"Received Flax Twine Weave settings from server: Input={packet.FlaxTwineWeaveInputQuantity}, Output={packet.FlaxTwineWeaveOutputQuantity}");

                    // Wool twine weaving settings
                    ModConfig.Loaded.WoolTwineWeaveInputQuantity = packet.WoolTwineWeaveInputQuantity;
                    ModConfig.Loaded.WoolTwineWeaveOutputQuantity = packet.WoolTwineWeaveOutputQuantity;
                    this.Mod.Logger.Event($"Received Wool Twine Weave settings from server: Input={packet.WoolTwineWeaveInputQuantity}, Output={packet.WoolTwineWeaveOutputQuantity}");

                    // Tailor's Delight thread weaving settings
                    ModConfig.Loaded.TailorsDelightThreadWeaveInputQuantity = packet.TailorsDelightThreadWeaveInputQuantity;
                    ModConfig.Loaded.TailorsDelightThreadWeaveOutputQuantity = packet.TailorsDelightThreadWeaveOutputQuantity;
                    this.Mod.Logger.Event($"Received Tailor's Delight Thread Weave settings from server: Input={packet.TailorsDelightThreadWeaveInputQuantity}, Output={packet.TailorsDelightThreadWeaveOutputQuantity}");
                });
            
            clientApi = capi;
        }
        
        public override void StartServerSide(ICoreServerAPI sapi)
        {
            // send connecting players the config settings
            sapi.Event.PlayerJoin += this.OnPlayerJoin; // add method so we can remove it in dispose to prevent memory leaks
            // register network channel to send data to clients
            this.serverChannel = sapi.Network.RegisterChannel("spinningwheel")
                .RegisterMessageType<SyncClientPacket>()
                .SetMessageHandler<SyncClientPacket>((player, packet) => { /* do nothing.*/ });
        }

        public void OnPlayerJoin(IServerPlayer player)
        {
            this.serverChannel.SendPacket(new SyncClientPacket
            {
                // Class/Trait restrictions
                RequireClassOrTrait = ModConfig.Loaded.RequireClassOrTrait,
                AllowedClasses = ModConfig.Loaded.AllowedClasses,
                AllowedTraits = ModConfig.Loaded.AllowedTraits,
        
                // Recipe control
                DisableTwineGridRecipes = ModConfig.Loaded.DisableTwineGridRecipes,

                // Drop spindle chat messages
                ShowDropSpindleProgressMessages = ModConfig.Loaded.ShowDropSpindleProgressMessages,

                // Vanilla flax settings
                FlaxSpinTime = ModConfig.Loaded.FlaxSpinTime,
                FlaxInputQuantity = ModConfig.Loaded.FlaxInputQuantity,
                FlaxOutputQuantity = ModConfig.Loaded.FlaxOutputQuantity,
        
                // Cotton settings
                CottonSpinTime = ModConfig.Loaded.CottonSpinTime,
                CottonInputQuantity = ModConfig.Loaded.CottonInputQuantity,
                CottonOutputQuantity = ModConfig.Loaded.CottonOutputQuantity,
        
                // Wool fiber settings
                WoolFiberSpinTime = ModConfig.Loaded.WoolFiberSpinTime,
                WoolFiberInputQuantity = ModConfig.Loaded.WoolFiberInputQuantity,
                WoolFiberOutputQuantity = ModConfig.Loaded.WoolFiberOutputQuantity,
        
                // Wool twine settings
                WoolTwineSpinTime = ModConfig.Loaded.WoolTwineSpinTime,
                WoolTwineInputQuantity = ModConfig.Loaded.WoolTwineInputQuantity,
                WoolTwineOutputQuantity = ModConfig.Loaded.WoolTwineOutputQuantity,
        
                // Papyrus settings
                PapyrusSpinTime = ModConfig.Loaded.PapyrusSpinTime,
                PapyrusInputQuantity = ModConfig.Loaded.PapyrusInputQuantity,
                PapyrusOutputQuantity = ModConfig.Loaded.PapyrusOutputQuantity,
        
                // Algae settings
                AlgaeSpinTime = ModConfig.Loaded.AlgaeSpinTime,
                AlgaeInputQuantity = ModConfig.Loaded.AlgaeInputQuantity,
                AlgaeOutputQuantity = ModConfig.Loaded.AlgaeOutputQuantity,

                // Flax twine weaving settings
                FlaxTwineWeaveInputQuantity = ModConfig.Loaded.FlaxTwineWeaveInputQuantity,
                FlaxTwineWeaveOutputQuantity = ModConfig.Loaded.FlaxTwineWeaveOutputQuantity,

                // Wool twine weaving settings
                WoolTwineWeaveInputQuantity = ModConfig.Loaded.WoolTwineWeaveInputQuantity,
                WoolTwineWeaveOutputQuantity = ModConfig.Loaded.WoolTwineWeaveOutputQuantity,

                // Tailor's Delight thread weaving settings
                TailorsDelightThreadWeaveInputQuantity = ModConfig.Loaded.TailorsDelightThreadWeaveInputQuantity,
                TailorsDelightThreadWeaveOutputQuantity = ModConfig.Loaded.TailorsDelightThreadWeaveOutputQuantity
            }, player);
        }
        
        public override void Dispose()
        {
            // remove our player join listener so we dont create memory leaks
            if (this.api is ICoreServerAPI sapi)
            {
                sapi.Event.PlayerJoin -= this.OnPlayerJoin;
            }
        }
    }
}