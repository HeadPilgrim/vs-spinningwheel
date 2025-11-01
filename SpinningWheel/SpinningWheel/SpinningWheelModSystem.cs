namespace SpinningWheel.ModSystem {

    using SpinningWheel.BlockEntities;
    using SpinningWheel.Blocks;
    using Vintagestory.API.Client;
    using Vintagestory.API.Server;
    using Vintagestory.API.Config;
    using Vintagestory.API.Common;
    using SpinningWheel.ModConfig;
    using System;

    public class SpinningWheelModSystem : ModSystem
    {
        private readonly string thisModID = "spinningwheel";
        private ICoreClientAPI clientApi;
        private ICoreAPI api;
        private IServerNetworkChannel serverChannel;

        // Called on server and client
        public override void Start(ICoreAPI api)
        {
            this.api = api;
            base.Start(api);
            api.RegisterBlockClass("BlockSpinningWheel", typeof(BlockSpinningWheel));
            api.RegisterBlockEntityClass("BlockEntitySpinningWheel", typeof(BlockEntitySpinningWheel));
            api.World.Logger.Event("started 'SpinningWheel' mod");
            Mod.Logger.Notification("Registered block and block entity for hpspinningwheel");
        }
        
        public override void StartPre(ICoreAPI api)
        {
            // Load/create common config file in ..\VintageStoryData\ModConfig\thisModID
            var cfgFileName = this.thisModID + ".json";
            try
            {
                ModConfig fromDisk;
                if ((fromDisk = api.LoadModConfig<ModConfig>(cfgFileName)) == null)
                { api.StoreModConfig(ModConfig.Loaded, cfgFileName); }
                else
                { ModConfig.Loaded = fromDisk; }
            }
            catch
            {
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
                    this.Mod.Logger.Event($"Received AllowedClasses of {packet.AllowedClasses} from server");
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