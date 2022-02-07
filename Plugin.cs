namespace GameTweaks
{
    using BepInEx;
    using BepInEx.Configuration;
    using BepInEx.Logging;
    using HarmonyLib;
    using Realmforge.Server;
    using Realmforge.Server.Scripting;
    using Realmforge.Server.Scripting.Action;
    using Realmforge.Shared;

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static ConfigEntry<int> HumanDigSpeedMultiplier;
        public static ConfigEntry<int> AIDigSpeedMultiplier;
        public static ConfigEntry<float> HumanDamageMultiplier;
        public static ConfigEntry<float> AIDamageMultiplier;
        public static ConfigEntry<bool> AIAttackAllowed;
        public static ConfigEntry<bool> AIAttackPunished;
        public static ConfigEntry<int> StartingFocus;
        public static ConfigEntry<int> FocusPerUpgrade;
        public static ConfigEntry<int> StartingWorkers;
        public static ConfigEntry<int> WorkersPerUpgrade;
        public static ConfigEntry<int> SquadMonsterPerFocus;
        public static ConfigEntry<int> FreeZombies;
        public static ConfigEntry<int> FreeSkeletons; 
        public static ConfigEntry<int> FreeTurnedHeroes;
        public static ManualLogSource logger;

        private void Awake()
        {
            logger = Logger;
            // Plugin startup logic
            Harmony.CreateAndPatchAll(typeof(Plugin), PluginInfo.PLUGIN_GUID);
            
            HumanDigSpeedMultiplier = this.Config.Bind("Damage Modifiers", "Human Dig Damage Multiplier", 1, "Multiplier for Human Controlled workers dig speed.");
            AIDigSpeedMultiplier = this.Config.Bind("Damage Modifiers", "AI Dig Damage Multiplier", 1, "Multiplier for AI Controlled entities dig speed.");
            HumanDamageMultiplier = this.Config.Bind("Damage Modifiers", "Human Combat Damage Multiplier", 1.0f, "Multiplier for Human Controlled entities Damage values.");
            AIDamageMultiplier = this.Config.Bind("Damage Modifiers", "AI Combat Damage Multiplier", 1.0f, "Multiplier for AI Controlled entities Damage values.");
            
            
            StartingFocus = this.Config.Bind("Population Modifiers", "Starting Population Points", 5, "Starting Population Points.");
            FocusPerUpgrade = this.Config.Bind("Population Modifiers", "Population Points Per Upgrade", 1, "How many Population Points given for each research upgrade.");
            StartingWorkers = this.Config.Bind("Population Modifiers", "Starting Workers", 5, "How many Little Snots you start with.");
            WorkersPerUpgrade = this.Config.Bind("Population Modifiers", "Max Workers Per Upgrade", 1, "How many Little Snots slots given for each research upgrade.");
            SquadMonsterPerFocus = this.Config.Bind("Population Modifiers","Squad Monsters Per Population Point", 1, "How Many Population points used per zombies/skeletons after all Free slots used.");
            FreeZombies = this.Config.Bind("Population Modifiers", "Free Zombies", 1, "Number of Free Zombies before costing Population Points");
            FreeSkeletons = this.Config.Bind("Population Modifiers", "Free Skeletons", 1, "Number of Free Skeletons before costing Population Points");
            FreeTurnedHeroes = this.Config.Bind("Population Modifiers", "Free Turned Heroes", 3, "Number of Free Converted Heroes before costing Population Points");

            AIAttackAllowed = this.Config.Bind("AI Modifiers", "AI can Attack", true, "Setting false makes the AI never able to attack Human Controlled things.");
            AIAttackPunished = this.Config.Bind("AI Modifiers", "AI Attack Thoughts Punished", false, "Setting true makes the AI die instantly if they THINK to attack Human Controlled things when not allowed.");

            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }

        [HarmonyPatch(typeof(TileManager), nameof(TileManager.DamageTile))]
        private static void Prefix(Entity source, ref float damage)
        {
            if (source.Owner.IsHuman())
                damage *= HumanDigSpeedMultiplier.Value;
            else
                damage *= AIDigSpeedMultiplier.Value;
        }

        [HarmonyPatch(typeof(Combat), nameof(Combat.CanAttack), new System.Type[] { typeof(Entity), typeof(Entity), typeof(bool) })]
        private static bool Prefix(Combat __instance, Entity self, Entity defender, ref bool __result)
        {
            if(AIAttackAllowed.Value || !defender.Owner.IsHuman() || self.Owner.IsHuman())
                return true;

            if (AIAttackPunished.Value)
                self.Combat.Kill(defender, defender.Owner, true);

            __result = false;
            return false;
        }

        [HarmonyPatch(typeof(Combat), nameof(Combat.ReceiveDamage), new System.Type[] { typeof(Combat.DamageData) })]
        private static void Prefix(Combat __instance, ref Combat.DamageData data)
        {
            if(data.DamageOwner.IsHuman())
                data.Damage.Amount *= HumanDamageMultiplier.Value;
            else
                data.Damage.Amount *= AIDamageMultiplier.Value;
        }

        [HarmonyPatch(typeof(ImpersonateManager), nameof(ImpersonateManager._InitGenerated))]
        private static void Postfix(ImpersonateManager __instance)
        {
            __instance.synchData.focusPointsMax = StartingFocus.Value;
            __instance.synchData.freeSkeleton = FreeSkeletons.Value;
            __instance.synchData.freeZombies = FreeZombies.Value;
            __instance.synchData.freeTurnedHeroes = FreeTurnedHeroes.Value;
            __instance.synchData.squadMonsterPerFocusPoint = SquadMonsterPerFocus.Value;
        }

        [HarmonyPatch(typeof(ImpersonateManager), nameof(ImpersonateManager.FocusPointsMax), MethodType.Setter)]
        private static bool Prefix(ImpersonateManager __instance, ref int value)
        {
            if(FocusPerUpgrade.Value == 1 && StartingFocus.Value == 5)
                return true;

            if(value == __instance.FocusPointsMax)
                return false;

            __instance.synchData.focusPointsMax = StartingFocus.Value + ((value - 5) * FocusPerUpgrade.Value);
            __instance.SetSynchDirty(ImpersonateManager.Synch.ID.FocusPointsMax);
            return false;
        }

        [HarmonyPatch(typeof(ThroneRoomSpawner), nameof(ImpersonateManager._InitGenerated))]
        private static void Postfix(ThroneRoomSpawner __instance)
        {
            __instance.synchData.maxWorker = StartingFocus.Value;
        }

        [HarmonyPatch(typeof(ThroneRoomSpawner), nameof(ThroneRoomSpawner.MaxWorker), MethodType.Setter)]
        private static bool Prefix(ThroneRoomSpawner __instance, ref int value)
        {
            if (WorkersPerUpgrade.Value == 1 && StartingWorkers.Value == 5)
                return true;

            if (value == __instance.MaxWorker)
                return false;

            __instance.synchData.maxWorker = StartingWorkers.Value + ((value - 5) * WorkersPerUpgrade.Value);
            __instance.SetSynchDirty(ThroneRoomSpawner.Synch.ID.MaxWorker);
            return false;
        }
    }
}
