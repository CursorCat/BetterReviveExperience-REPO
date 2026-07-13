using System;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using BetterReviveExperience.Patches;

namespace BetterReviveExperience
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    [BepInDependency("nickklmao.repoconfig", "1.2.6")]
    [BepInDependency("zichen.gametools", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "com.mods.betterreviveexperience";
        public const string PLUGIN_NAME = "BetterReviveExperience";
        public const string PLUGIN_VERSION = "0.2.10";

        private const int ReviveCostStep = 1000;
        private const int ReviveCostMaximum = 100000;
        private static readonly string[] HeldHeadReviveKeyOptions =
        {
            "H", "R", "Y", "F"
        };
        private static readonly string[] ReviveCostOptions = BuildReviveCostOptions();

        public static ManualLogSource Log { get; private set; }

        public static ConfigEntry<bool> KeepItemsOnDeath { get; private set; }
        public static ConfigEntry<string> ReviveTrigger { get; private set; }
        public static ConfigEntry<string> ReviveCost { get; private set; }
        public static ConfigEntry<int> ReviveHealthPercent { get; private set; }
        public static ConfigEntry<bool> EnableHeldHeadRevive { get; private set; }
        public static ConfigEntry<string> HeldHeadReviveKey { get; private set; }
        public static ConfigEntry<bool> EnableCartRevive { get; private set; }
        public static ConfigEntry<bool> EnableShopRevive { get; private set; }
        public static ConfigEntry<bool> DebugLogging { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            KeepItemsOnDeath = Config.Bind(
                "Inventory",
                "KeepItemsOnDeath",
                true,
                "Keep inventory-slot items when a player dies. Held items still drop."
            );

            ReviveTrigger = Config.Bind(
                "Revive",
                "Mode",
                nameof(ReviveMode.ExtractionOrTruck),
                new ConfigDescription(
                    "Disabled, extraction machine activated, or direct extraction/truck revive.",
                    new AcceptableValueList<string>(
                        nameof(ReviveMode.Disabled),
                        nameof(ReviveMode.ExtractionMachineActivated),
                        nameof(ReviveMode.ExtractionOrTruck)
                    )
                )
            );

            ReviveCost = Config.Bind(
                "Revive",
                "Cost",
                "0",
                new ConfigDescription(
                    "Shared team currency consumed per revive. Selectable in 1,000-currency steps.",
                    new AcceptableValueList<string>(ReviveCostOptions)
                )
            );

            ReviveHealthPercent = Config.Bind(
                "Revive",
                "HealthPercent",
                25,
                new ConfigDescription(
                    "Health after revive, from 1 to 100 percent.",
                    new AcceptableValueRange<int>(1, 100)
                )
            );

            EnableHeldHeadRevive = Config.Bind(
                "Revive",
                "EnableHeldHeadRevive",
                true,
                "Allow the host to revive the death head currently held with the physics grabber by pressing H."
            );

            HeldHeadReviveKey = Config.Bind(
                "Revive",
                "HeldHeadReviveKey",
                "H",
                new ConfigDescription(
                    "Key used for held-head revive.",
                    new AcceptableValueList<string>(HeldHeadReviveKeyOptions)
                )
            );

            EnableCartRevive = Config.Bind(
                "Revive",
                "EnableCartRevive",
                true,
                "Immediately revive a player when their death head is placed inside a cart."
            );

            EnableShopRevive = Config.Bind(
                "Revive",
                "EnableShopRevive",
                true,
                "Immediately revive players killed in the shop."
            );

            DebugLogging = Config.Bind(
                "Debug",
                "DebugLogging",
                true,
                "Write detailed death-head, inventory, and Harmony diagnostics to the BepInEx log. " +
                "Enabled by default during development."
            );

            NormalizeConfig();
            ReviveController.ValidateGameApi();

            _harmony = new Harmony(PLUGIN_GUID);
            RegisterPatches();

            int patchCount = 0;
            foreach (MethodBase method in _harmony.GetPatchedMethods())
            {
                patchCount++;
                Debug($"[BRE] patched: {method.DeclaringType?.FullName}.{method.Name}");
            }

            Log.LogInfo($"{PLUGIN_NAME} v{PLUGIN_VERSION} loaded");
            Log.LogInfo($"[BRE] patches={patchCount}, keepItems={KeepItemsOnDeath.Value}, " +
                        $"mode={CurrentReviveMode}, cost={ReviveCostAmount}, " +
                        $"health={ReviveHealthPercent.Value}%, heldHead={EnableHeldHeadRevive.Value}/" +
                        $"{HeldHeadReviveKeyCode}, " +
                        $"cart={EnableCartRevive.Value}, shop={EnableShopRevive.Value}");

            if (Chainloader.PluginInfos.ContainsKey("zichen.gametools"))
            {
                Log.LogWarning("[BRE] GameTools detected. Disable its automatic death-head revive options " +
                               "to keep BetterReviveExperience cost, health, and trigger rules authoritative.");
            }

            WarnOverlappingReviveMod(
                "Hypn.ReviveHeadInTruckOrExtractionPoint",
                "ReviveHeadInTruckOrExtractionPoint"
            );
            WarnOverlappingReviveMod("Kai.Revive_at_Cart", "CartRevive");
            WarnOverlappingReviveMod("endersaltz.LetMeShop", "LetMeShop");
            WarnOverlappingReviveMod("com.yuniverse.reviveplayer", "UltimateReviveNew");
        }

        private void RegisterPatches()
        {
            RegisterPostfix(typeof(PlayerAvatar), "Update", typeof(PlayerAvatarUpdatePatch), "Postfix");
            RegisterPostfix(typeof(PlayerAvatar), "PlayerDeathRPC", typeof(PlayerDeathPatch), "Postfix", Priority.Last);
            RegisterPostfix(typeof(PlayerDeathHead), "Trigger", typeof(DeathHeadTriggerPatch), "Postfix");
            RegisterPostfix(
                typeof(PlayerDeathHead),
                "Update",
                typeof(DeathHeadUpdatePatch),
                "Postfix",
                after: new[] { "zichen.gametools" }
            );
            RegisterPostfix(typeof(PlayerAvatar), "ReviveRPC", typeof(PlayerRevivePatch), "Postfix", Priority.Last);
            RegisterPostfix(typeof(PhysGrabCart), "Update", typeof(CartUpdatePatch), "Postfix");
            RegisterPrefix(typeof(ItemEquippable), "RPC_CompleteUnequip", typeof(ForcedUnequipPatch), "Prefix", Priority.First);
            RegisterPrefix(typeof(RunManager), "ChangeLevel", typeof(LevelChangePatch), "Prefix");
            RegisterPostfix(typeof(RoundDirector), "Start", typeof(RoundStartPatch), "Postfix");
            RegisterPostfix(typeof(MainMenuOpen), "Start", typeof(MainMenuPatch), "Postfix");
        }

        private void RegisterPrefix(Type targetType, string targetName, Type patchType, string patchName, int priority = Priority.Normal)
        {
            RegisterPatch(targetType, targetName, patchType, patchName, isPrefix: true, priority, after: null);
        }

        private void RegisterPostfix(
            Type targetType,
            string targetName,
            Type patchType,
            string patchName,
            int priority = Priority.Normal,
            string[] after = null)
        {
            RegisterPatch(targetType, targetName, patchType, patchName, isPrefix: false, priority, after);
        }

        private void RegisterPatch(
            Type targetType,
            string targetName,
            Type patchType,
            string patchName,
            bool isPrefix,
            int priority,
            string[] after)
        {
            MethodInfo target = AccessTools.Method(targetType, targetName);
            MethodInfo callback = AccessTools.Method(patchType, patchName);
            if (target == null || callback == null)
            {
                throw new MissingMethodException(
                    $"[BRE] Cannot patch {targetType.FullName}.{targetName} with {patchType.FullName}.{patchName}"
                );
            }

            var hook = new HarmonyMethod(callback)
            {
                priority = priority,
                after = after
            };
            _harmony.Patch(target, isPrefix ? hook : null, isPrefix ? null : hook);
        }

        private static void NormalizeConfig()
        {
            ReviveHealthPercent.Value = Mathf.Clamp(ReviveHealthPercent.Value, 1, 100);
        }

        public static int ReviveCostAmount
        {
            get
            {
                return int.TryParse(ReviveCost.Value, out int cost)
                    ? Mathf.Clamp(cost, 0, ReviveCostMaximum)
                    : 0;
            }
        }

        public static ReviveMode CurrentReviveMode
        {
            get
            {
                return Enum.TryParse(ReviveTrigger.Value, true, out ReviveMode mode)
                    ? mode
                    : ReviveMode.Disabled;
            }
        }

        public static KeyCode HeldHeadReviveKeyCode
        {
            get
            {
                string keyName = HeldHeadReviveKey.Value;
                if (keyName?.Length == 1 && keyName[0] >= '0' && keyName[0] <= '9')
                {
                    return KeyCode.Alpha0 + (keyName[0] - '0');
                }

                if (string.Equals(keyName, "Enter", StringComparison.OrdinalIgnoreCase))
                {
                    return KeyCode.Return;
                }

                return Enum.TryParse(keyName, true, out KeyCode key) ? key : KeyCode.H;
            }
        }

        private static string[] BuildReviveCostOptions()
        {
            var options = new string[ReviveCostMaximum / ReviveCostStep + 1];
            for (int cost = 0; cost <= ReviveCostMaximum; cost += ReviveCostStep)
            {
                options[cost / ReviveCostStep] = cost.ToString();
            }

            return options;
        }

        public static void Debug(string message)
        {
            if (DebugLogging?.Value == true)
            {
                Log?.LogInfo(message);
            }
        }

        private static void WarnOverlappingReviveMod(string guid, string name)
        {
            if (Chainloader.PluginInfos.ContainsKey(guid))
            {
                Log.LogWarning($"[BRE] {name} detected. Disable it while BRE manages the same revive trigger " +
                               "to prevent duplicate ReviveRPC calls, health changes, or charges.");
            }
        }

        private void OnDestroy()
        {
            // R.E.P.O.'s loader can dispose the plugin component after Awake while
            // the process continues. Harmony patches are process-wide and must stay
            // installed for the active session; unpatching here removed every live
            // callback immediately after successful registration.
            Log?.LogWarning("[BRE] plugin component destroyed; keeping session patches installed");
        }
    }
}
