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

        // Called on server and client
        public override void Start(ICoreAPI api)
        {
            this.api = api;
            base.Start(api);
            
            api.RegisterBlockClass("BlockSpinningWheel", typeof(BlockSpinningWheel));
            api.RegisterBlockEntityClass("BlockEntitySpinningWheel", typeof(BlockEntitySpinningWheel));
            
            api.RegisterItemClass("ItemDropSpindle", typeof(ItemDropSpindle));
            
            api.World.Logger.Event("started 'SpinningWheel' mod");
            Mod.Logger.Notification("Registered block and block entity for hpspinningwheel");
            Mod.Logger.Notification("Registered ItemDropSpindle for portable spinning");
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
        
        public override void StartPre(ICoreAPI api)
        {
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
        
        public override void StartClientSide(ICoreClientAPI capi)
        { 
            capi.Network.RegisterChannel("spinningwheel")
                .RegisterMessageType<SyncClientPacket>()
                .SetMessageHandler<SyncClientPacket>(packet =>
                {
                    ModConfig.Loaded.RequireTailorClass = packet.RequireTailorClass;
                    this.Mod.Logger.Event($"Received RequireTailorClass of {packet.RequireTailorClass} from server");
                    ModConfig.Loaded.AllowedClasses = packet.AllowedClasses;
                    this.Mod.Logger.Event($"Received AllowedClasses from server: {string.Join(", ", packet.AllowedClasses)}");
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
                RequireTailorClass = ModConfig.Loaded.RequireTailorClass,
                AllowedClasses = ModConfig.Loaded.AllowedClasses
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