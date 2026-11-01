using System.Linq;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace LastingArmorFixed
{
    public class LastingArmorModFixedSystem : ModSystem
    {
        private Harmony HI => new Harmony(Mod.Info.ModID);
        public LastingArmorConfig ModConfig { get; private set; }

        public override void StartPre(ICoreAPI api)
        {
            if (api.Side.IsServer())
            {
                Mod.Logger.Event("Lasting Armor: Running pre-init phase...");

                ModConfig = LoadConfiguration(api);

                Mod.Logger.Event("Lasting Armor: Applying world config Harmony postfix...");
                HI.Patch(
                    AccessTools.Method(typeof(SurvivalCoreSystem), "applyConfig"),
                    postfix: new HarmonyMethod(typeof(ApplyConfigPostfix), "Postfix")
                );
            }
        }

        public override void Dispose()
        {
            HI.UnpatchAll(HI.Id);
        }

        private LastingArmorConfig LoadConfiguration(ICoreAPI api)
        {
            // TODO Loading the config file should check versions in future
            ModConfig = api.LoadModConfig<LastingArmorConfig>("LastingArmorConfig.json");
            //try
            //{
                if (ModConfig is null)
                {
                    api.Logger.Debug("Lasting Armor: No config found, creating default LastingArmorConfig.json");

                    ModConfig = new LastingArmorConfig();

                    api.StoreModConfig(ModConfig, "LastingArmorConfig.json");
                }
            //}
            //catch (Exception e)
            //{
            //Mod.Logger.Error("Lasting Armor: Error loading config: {0}", e.Message);
            //}
            return ModConfig;
        }
    }

    [HarmonyPatch(typeof(SurvivalCoreSystem), "applyConfig")]
    public class ApplyConfigPostfix
    {
        public static void Postfix(SurvivalCoreSystem __instance)
        {
            // Access the api field via reflection since it's private
            var apiField = AccessTools.Field(typeof(SurvivalCoreSystem), "api");
            var api = apiField?.GetValue(__instance) as ICoreAPI;
            var worldConfig = api?.World?.Config;
            
            api.Logger.Debug("LastingArmor: Postfix initiated.");

            // Get the loaded mod system instance from the API
            var modSystem = api?.ModLoader.GetModSystem<LastingArmorModFixedSystem>();
            int multiplier;
            if (modSystem?.ModConfig?.EnableWorldDurabilityScaling == true)
            {
                multiplier = worldConfig["toolDurability"].GetValue().ToString().ToInt();
                api.Logger.Debug("LastingArmor: {0}",multiplier);
            }
            else
            {
                multiplier = modSystem?.ModConfig?.DurabilityMultiplier ?? 20;
                api.Logger.Debug("LastingArmor: Not using world durability scaling.");
            }

            if (api.Side.IsServer())
            {
                foreach (CollectibleObject collectible in api.World.Collectibles)
                {
                    var isWearable = collectible.CollectibleBehaviors.Any(behavior => behavior is CollectibleBehaviorWearable);
                    if (isWearable && collectible.Durability > 1)
                    {
                        collectible.Durability = (int)((float)collectible.Durability * multiplier);
                        api.Logger.Debug("Lasting Armor: Final durability set to {0} for [{1}]", collectible.Durability, collectible.Code);
                    }
                }
            }
        }
    }

    public class LastingArmorConfig
    {
        public int ConfigVersion { get; set; } = 1; // Version of the config file
        public string CommentScaling { get; set; } = "Setting EnableWorldDurabilityScaling to true makes the mod ignore DurabilityMultiplier."; // Comment for the config file
        public int DurabilityMultiplier { get; set; } = 20; // Default multiplier
        public bool EnableWorldDurabilityScaling { get; set; } = false; // Default setting to enable durability scaling
    }
}