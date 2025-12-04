namespace SpinningWheel.ModSystem {

    using SpinningWheel.BlockEntities;
    using SpinningWheel.Blocks;
    using SpinningWheel.Items;
    using SpinningWheel.Configuration;
    using Vintagestory.API.Client;
    using Vintagestory.API.Server;
    using Vintagestory.API.Common;
    using SpinningWheel.ModConfig;
    using System;

    public class SpinningWheelModSystem : ModSystem
    {
        private readonly string thisModID = "spinningwheel";
        private ICoreClientAPI clientApi;
        private ICoreAPI api;
        private IServerNetworkChannel serverChannel;
        private SpinningWheelConfigPatcher configPatcher;

        // Called very early, before assets are loaded
        public override void StartPre(ICoreAPI api)
        {
            // Register classes FIRST, before anything else
            api.RegisterBlockClass("BlockSpinningWheel", typeof(BlockSpinningWheel));
            api.RegisterBlockEntityClass("BlockEntitySpinningWheel", typeof(BlockEntitySpinningWheel));
            
            api.RegisterBlockClass("BlockFlyshuttleLoom", typeof(BlockFlyshuttleLoom));
            
            api.RegisterItemClass("ItemDropSpindle", typeof(ItemDropSpindle));
            
            api.Logger.Notification("[SpinningWheel] Registered block and block entity classes");
            api.Logger.Notification("[SpinningWheel] Registered ItemDropSpindle for portable spinning");
            
            // Load/create common config file in ..\VintageStoryData\ModConfig\thisModID
            var cfgFileName = this.thisModID + ".json";
            try
            {
                ModConfig fromDisk;
                if ((fromDisk = api.LoadModConfig<ModConfig>(cfgFileName)) == null)
                { 
                    api.StoreModConfig(ModConfig.Loaded, cfgFileName);
                    api.Logger.Notification("[SpinningWheel] Created new config file with default values");
                }
                else
                { 
                    ModConfig.Loaded = fromDisk;
                    api.Logger.Notification("[SpinningWheel] Loaded config from disk");
                }
            }
            catch (Exception ex)
            {
                api.Logger.Error("[SpinningWheel] Error loading config, creating new one: " + ex.Message);
                api.StoreModConfig(ModConfig.Loaded, cfgFileName);
            }
            
            base.StartPre(api);
        }

        // Called on server and client after StartPre
        public override void Start(ICoreAPI api)
        {
            this.api = api;
            base.Start(api);
            
            api.World.Logger.Event("started 'SpinningWheel' mod");
        }
        
        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);
            
            // Apply config-based patches using the dedicated patcher class
            // Do this early, right after assets are loaded
            configPatcher = new SpinningWheelConfigPatcher(api, ModConfig.Loaded);
            configPatcher.ApplyAllPatches();
            
            api.Logger.Notification("[SpinningWheel] Config patches applied in AssetsLoaded");
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
                            $"[SpinningWheel] Patched {collectible.Code} storage flags: {currentFlags} -> {collectible.StorageFlags}"
                        );
                
                        patchedCount++;
                    }
                }
            }
    
            if (patchedCount > 0)
            {
                api.Logger.Notification($"[SpinningWheel] Successfully patched {patchedCount} spinnable items for offhand compatibility");
            }
        }
        
        public override void StartClientSide(ICoreClientAPI capi)
        { 
            capi.Network.RegisterChannel("spinningwheel")
                .RegisterMessageType<SyncClientPacket>()
                .SetMessageHandler<SyncClientPacket>(packet =>
                {
                    // Class restrictions
                    ModConfig.Loaded.RequireTailorClass = packet.RequireTailorClass;
                    this.Mod.Logger.Event($"Received RequireTailorClass of {packet.RequireTailorClass} from server");
                    ModConfig.Loaded.AllowedClasses = packet.AllowedClasses;
                    this.Mod.Logger.Event($"Received AllowedClasses from server: {string.Join(", ", packet.AllowedClasses)}");
                    
                    // Recipe control
                    ModConfig.Loaded.DisableTwineGridRecipes = packet.DisableTwineGridRecipes;
                    this.Mod.Logger.Event($"Received DisableTwineGridRecipes of {packet.DisableTwineGridRecipes} from server");
                    
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
                });
            
            clientApi = capi;
            // Client creates default config if server hasn't sent one yet
            capi.Input.RegisterHotKey("openspinningwheel", "Open Spinning Wheel GUI", 
                GlKeys.F, HotkeyType.GUIOrOtherControls);
            
            capi.Input.SetHotKeyHandler("openspinningwheel", OnOpenSpinningWheelHotkey);
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
        
        private bool OnOpenSpinningWheelHotkey(KeyCombination keyCombination)
        {
            EntityPlayer player = clientApi.World.Player.Entity;
            
            if (player?.MountedOn?.MountSupplier is BlockEntitySpinningWheel spinningWheel)
            {
                spinningWheel.OpenGui(clientApi.World.Player);
                return true;
            }
            
            return false;
        }

        public void OnPlayerJoin(IServerPlayer player)
        {
            this.serverChannel.SendPacket(new SyncClientPacket
            {
                // Class restrictions
                RequireTailorClass = ModConfig.Loaded.RequireTailorClass,
                AllowedClasses = ModConfig.Loaded.AllowedClasses,
        
                // Recipe control
                DisableTwineGridRecipes = ModConfig.Loaded.DisableTwineGridRecipes,
        
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
                AlgaeOutputQuantity = ModConfig.Loaded.AlgaeOutputQuantity
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