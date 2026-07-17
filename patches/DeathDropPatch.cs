using HarmonyLib;
using Photon.Pun;

namespace BetterReviveExperience.Patches
{
    // One-time runtime probe. This must appear after a player spawns; without it,
    // a registered Harmony patch has not actually reached the live game loop.
    internal static class PlayerAvatarUpdatePatch
    {
        private static void Postfix(PlayerAvatar __instance)
        {
            WeaponProtectionController.ObservePlayer(__instance);
            WeaponProtectionController.ProcessForcedDropRecovery(__instance);
            ReviveController.OnPlayerAvatarUpdated(__instance);
            WeaponProtectionController.ProcessPendingReturn(__instance);
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), "PlayerDeathRPC")]
    internal static class PlayerDeathPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(PlayerAvatar __instance)
        {
            WeaponProtectionController.CaptureBeforeDeath(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(PlayerAvatar __instance)
        {
            ReviveController.OnPlayerDeath(__instance);
            WeaponProtectionController.ConfirmDeath(__instance);
        }
    }

    [HarmonyPatch(typeof(PlayerDeathHead), "Trigger")]
    internal static class DeathHeadTriggerPatch
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerDeathHead __instance)
        {
            ReviveController.OnDeathHeadTriggered(__instance);
        }
    }

    // PlayerDeathHead.Update is the game's authoritative death-head loop. Its
    // original method updates RoomVolumeCheck before this postfix runs.
    [HarmonyPatch(typeof(PlayerDeathHead), "Update")]
    internal static class DeathHeadUpdatePatch
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerDeathHead __instance)
        {
            WeaponProtectionController.ProcessPendingReturn(__instance.playerAvatar, __instance);
            ReviveController.OnDeathHeadUpdated(__instance);
        }
    }

    internal static class ForcedGrabReleaseReceivePatch
    {
        private static bool Prefix(
            PhysGrabber __instance,
            bool physGrabEnded,
            float _disableTimer,
            int _releaseObjectViewID,
            PhotonMessageInfo _info)
        {
            return WeaponProtectionController.AllowForcedRelease(
                __instance,
                physGrabEnded,
                _disableTimer,
                _releaseObjectViewID,
                _info
            );
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), "ReviveRPC")]
    internal static class PlayerRevivePatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(PlayerAvatar __instance)
        {
            ReviveController.OnPlayerRevived(__instance);
        }
    }

    [HarmonyPatch(typeof(PhysGrabCart), "Update")]
    internal static class CartUpdatePatch
    {
        [HarmonyPostfix]
        private static void Postfix(PhysGrabCart __instance)
        {
            ReviveController.OnCartUpdated(__instance);
        }
    }

    // A dying client's Inventory.ForceUnequip sends this RPC to MasterClient.
    // Blocking it on the host keeps inventory slots without a client-side mod.
    [HarmonyPatch(typeof(ItemEquippable), "RPC_CompleteUnequip")]
    internal static class ForcedUnequipPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(
            ItemEquippable __instance,
            int physGrabberPhotonViewID,
            bool isForceUnequip)
        {
            return ReviveController.AllowForcedUnequip(
                __instance,
                physGrabberPhotonViewID,
                isForceUnequip
            );
        }
    }

    [HarmonyPatch(typeof(RunManager), "ChangeLevel")]
    internal static class LevelChangePatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            ReviveController.Reset(refundPending: true);
            WeaponProtectionController.Reset();
            Plugin.Debug("[BRE] level change: state reset");
        }
    }

    [HarmonyPatch(typeof(RoundDirector), "Start")]
    internal static class RoundStartPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            ReviveController.Reset();
            WeaponProtectionController.Reset();
            Plugin.Debug("[BRE] round start: state reset");
        }
    }

    [HarmonyPatch(typeof(MainMenuOpen), "Start")]
    internal static class MainMenuPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            ReviveController.Reset(refundPending: true);
            WeaponProtectionController.Reset();
            Plugin.Debug("[BRE] main menu: state reset");
        }
    }
}
