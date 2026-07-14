using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace BetterReviveExperience
{
    internal static class WeaponProtectionController
    {
        private sealed class HeldWeaponRecord
        {
            public PlayerAvatar Player;
            public PhysGrabObject Physical;
            public ItemEquippable Item;
            public int OwnerViewId;
            public float LastSeenAt;
        }

        private sealed class PendingWeaponReturn
        {
            public HeldWeaponRecord Weapon;
            public float ReadyAt;
            public float GiveUpAt;
        }

        private const float RecentHoldWindowSeconds = 0.75f;
        private const float DeathCleanupDelaySeconds = 0.6f;
        private const float ReturnTimeoutSeconds = 4f;

        private static readonly FieldInfo GrabbedPhysObjectField =
            AccessTools.Field(typeof(PhysGrabber), "grabbedPhysGrabObject");

        private static readonly Dictionary<string, HeldWeaponRecord> LastWeaponByPlayer =
            new Dictionary<string, HeldWeaponRecord>();

        private static readonly Dictionary<int, string> LastOwnerByWeapon =
            new Dictionary<int, string>();

        private static readonly Dictionary<string, HeldWeaponRecord> DeathCandidates =
            new Dictionary<string, HeldWeaponRecord>();

        private static readonly Dictionary<string, PendingWeaponReturn> PendingReturns =
            new Dictionary<string, PendingWeaponReturn>();

        private static readonly Dictionary<int, float> LastProtectionLogAt =
            new Dictionary<int, float>();

        public static bool ValidateGameApi(ICollection<string> missing)
        {
            if (GrabbedPhysObjectField == null)
            {
                missing.Add("PhysGrabber.grabbedPhysGrabObject");
                return false;
            }

            return true;
        }

        public static void ObservePlayer(PlayerAvatar player)
        {
            if (!ReviveController.IsHost() || !player || ReviveController.IsDead(player)) return;

            PhysGrabObject physical = GetHeldPhysical(player.physGrabber);
            if (!IsProtectedWeapon(physical, out ItemEquippable item)) return;

            RecordHolder(player, physical, item);
        }

        public static void CaptureBeforeDeath(PlayerAvatar player)
        {
            if (!Plugin.ReturnHeldWeaponOnDeath.Value || !ReviveController.IsHost() || !player) return;

            string playerId = ReviveController.GetPlayerId(player);
            PhysGrabObject physical = GetHeldPhysical(player.physGrabber);
            HeldWeaponRecord record = null;

            if (IsProtectedWeapon(physical, out ItemEquippable item))
            {
                record = RecordHolder(player, physical, item);
            }
            else if (LastWeaponByPlayer.TryGetValue(playerId, out HeldWeaponRecord recent) &&
                     Time.time - recent.LastSeenAt <= RecentHoldWindowSeconds &&
                     IsStillLastOwner(playerId, recent.Physical))
            {
                record = recent;
            }

            if (record == null) return;

            DeathCandidates[playerId] = record;
            Plugin.Debug($"[BRE] death weapon captured: player={playerId}, weapon={WeaponName(record.Item)}");
        }

        public static void ConfirmDeath(PlayerAvatar player)
        {
            if (!Plugin.ReturnHeldWeaponOnDeath.Value || !ReviveController.IsHost() ||
                !player || !ReviveController.IsDead(player))
            {
                return;
            }

            string playerId = ReviveController.GetPlayerId(player);
            if (!DeathCandidates.TryGetValue(playerId, out HeldWeaponRecord weapon)) return;

            DeathCandidates.Remove(playerId);
            PendingReturns[playerId] = new PendingWeaponReturn
            {
                Weapon = weapon,
                ReadyAt = Time.time + DeathCleanupDelaySeconds,
                GiveUpAt = Time.time + ReturnTimeoutSeconds
            };

            Plugin.Log.LogInfo($"[BRE] held weapon queued for death return: player={playerId}, " +
                               $"weapon={WeaponName(weapon.Item)}");
        }

        public static void ProcessPendingReturn(PlayerAvatar player, PlayerDeathHead deathHead = null)
        {
            if (!ReviveController.IsHost() || !player) return;

            string playerId = ReviveController.GetPlayerId(player);
            if (!PendingReturns.TryGetValue(playerId, out PendingWeaponReturn pending) ||
                Time.time < pending.ReadyAt)
            {
                return;
            }

            HeldWeaponRecord weapon = pending.Weapon;
            if (weapon == null || !weapon.Item || !weapon.Physical)
            {
                PendingReturns.Remove(playerId);
                Plugin.Log.LogWarning($"[BRE] held weapon return cancelled: player={playerId}, weapon no longer exists");
                return;
            }

            if (!IsStillLastOwner(playerId, weapon.Physical) || IsHeldByAnotherPlayer(playerId, weapon.Physical))
            {
                PendingReturns.Remove(playerId);
                Plugin.Log.LogInfo($"[BRE] held weapon return cancelled: player={playerId}, " +
                                   $"weapon={WeaponName(weapon.Item)}, reason=new-holder");
                return;
            }

            if (weapon.Item.IsEquipped())
            {
                PendingReturns.Remove(playerId);
                Plugin.Debug($"[BRE] held weapon already equipped: player={playerId}, weapon={WeaponName(weapon.Item)}");
                return;
            }

            if (weapon.Physical.playerGrabbing.Count > 0 && Time.time < pending.GiveUpAt)
            {
                return;
            }

            int freeSlot = FindFreeVanillaSlot(playerId);
            if (freeSlot >= 0)
            {
                ReviveController.RestoreEquippedState(weapon.Item, freeSlot, weapon.OwnerViewId);
                PendingReturns.Remove(playerId);
                Plugin.Log.LogInfo($"[BRE] held weapon returned to inventory: player={playerId}, " +
                                   $"weapon={WeaponName(weapon.Item)}, slot={freeSlot}");
                return;
            }

            if (StatsManager.instance == null && Time.time < pending.GiveUpAt)
            {
                return;
            }

            Vector3 returnPosition = GetFallbackPosition(player, deathHead);
            weapon.Physical.Teleport(returnPosition, weapon.Physical.transform.rotation);
            if (weapon.Physical.rb)
            {
                weapon.Physical.rb.velocity = Vector3.zero;
                weapon.Physical.rb.angularVelocity = Vector3.zero;
            }

            PendingReturns.Remove(playerId);
            Plugin.Log.LogInfo($"[BRE] held weapon returned nearby: player={playerId}, " +
                               $"weapon={WeaponName(weapon.Item)}, reason=no-free-vanilla-slot");
        }

        public static bool AllowOutgoingRpc(
            PhotonView view,
            string methodName,
            RpcTarget target,
            object[] parameters)
        {
            if (!Plugin.ProtectHeldWeapons.Value || !ReviveController.IsHost() ||
                !view || methodName != "ReleaseObjectRPC" || target != RpcTarget.All ||
                parameters == null || parameters.Length < 3)
            {
                return true;
            }

            PhysGrabber grabber = view.GetComponent<PhysGrabber>();
            PlayerAvatar player = grabber ? grabber.playerAvatar : null;
            if (!player || ReviveController.IsDead(player)) return true;

            PhysGrabObject physical = GetHeldPhysical(grabber);
            if (!IsProtectedWeapon(physical, out ItemEquippable item)) return true;

            RecordHolder(player, physical, item);
            int weaponKey = GetWeaponKey(physical);
            if (!LastProtectionLogAt.TryGetValue(weaponKey, out float lastLog) || Time.time - lastLog >= 1f)
            {
                LastProtectionLogAt[weaponKey] = Time.time;
                Plugin.Log.LogInfo($"[BRE] prevented forced weapon drop: player={ReviveController.GetPlayerId(player)}, " +
                                   $"weapon={WeaponName(item)}");
            }

            return false;
        }

        private static HeldWeaponRecord RecordHolder(
            PlayerAvatar player,
            PhysGrabObject physical,
            ItemEquippable item)
        {
            string playerId = ReviveController.GetPlayerId(player);
            int weaponKey = GetWeaponKey(physical);

            if (LastOwnerByWeapon.TryGetValue(weaponKey, out string previousOwner) &&
                previousOwner != playerId &&
                LastWeaponByPlayer.TryGetValue(previousOwner, out HeldWeaponRecord previousRecord) &&
                previousRecord.Physical == physical)
            {
                LastWeaponByPlayer.Remove(previousOwner);
            }

            bool changed = !LastWeaponByPlayer.TryGetValue(playerId, out HeldWeaponRecord record) ||
                           record.Physical != physical;

            record = new HeldWeaponRecord
            {
                Player = player,
                Physical = physical,
                Item = item,
                OwnerViewId = SemiFunc.IsMultiplayer() && player.physGrabber && player.physGrabber.photonView
                    ? player.physGrabber.photonView.ViewID
                    : -1,
                LastSeenAt = Time.time
            };

            LastOwnerByWeapon[weaponKey] = playerId;
            LastWeaponByPlayer[playerId] = record;

            if (changed)
            {
                Plugin.Debug($"[BRE] weapon holder recorded: player={playerId}, weapon={WeaponName(item)}");
            }

            return record;
        }

        private static PhysGrabObject GetHeldPhysical(PhysGrabber grabber)
        {
            return grabber && GrabbedPhysObjectField != null
                ? GrabbedPhysObjectField.GetValue(grabber) as PhysGrabObject
                : null;
        }

        private static bool IsProtectedWeapon(PhysGrabObject physical, out ItemEquippable item)
        {
            item = null;
            if (!physical || physical.dead) return false;

            item = physical.GetComponent<ItemEquippable>();
            ItemAttributes attributes = physical.GetComponent<ItemAttributes>();
            if (!item || !attributes || !attributes.item) return false;

            SemiFunc.itemType type = attributes.item.itemType;
            return type == SemiFunc.itemType.gun ||
                   type == SemiFunc.itemType.melee ||
                   type == SemiFunc.itemType.launcher;
        }

        private static bool IsStillLastOwner(string playerId, PhysGrabObject physical)
        {
            return physical &&
                   LastOwnerByWeapon.TryGetValue(GetWeaponKey(physical), out string owner) &&
                   owner == playerId;
        }

        private static bool IsHeldByAnotherPlayer(string playerId, PhysGrabObject physical)
        {
            foreach (PhysGrabber grabber in physical.playerGrabbing)
            {
                if (grabber && grabber.playerAvatar &&
                    ReviveController.GetPlayerId(grabber.playerAvatar) != playerId)
                {
                    return true;
                }
            }

            return false;
        }

        private static int FindFreeVanillaSlot(string playerId)
        {
            StatsManager stats = StatsManager.instance;
            if (stats == null || string.IsNullOrEmpty(playerId)) return -1;

            if (!stats.playerInventorySpot1.ContainsKey(playerId)) return 0;
            if (!stats.playerInventorySpot2.ContainsKey(playerId)) return 1;
            if (!stats.playerInventorySpot3.ContainsKey(playerId)) return 2;
            return -1;
        }

        private static Vector3 GetFallbackPosition(PlayerAvatar player, PlayerDeathHead deathHead)
        {
            PhysGrabObject headPhysical = deathHead ? deathHead.GetComponent<PhysGrabObject>() : null;
            if (headPhysical)
            {
                return headPhysical.centerPoint + Vector3.up * 0.5f;
            }

            if (deathHead)
            {
                return deathHead.transform.position + Vector3.up * 0.5f;
            }

            return player.transform.position + Vector3.up * 0.75f;
        }

        private static int GetWeaponKey(PhysGrabObject physical)
        {
            PhotonView view = physical.GetComponent<PhotonView>();
            return view && view.ViewID != 0
                ? view.ViewID
                : physical.GetInstanceID();
        }

        private static string WeaponName(ItemEquippable item)
        {
            if (!item) return "unknown";

            ItemAttributes attributes = item.GetComponent<ItemAttributes>();
            return attributes && attributes.item
                ? attributes.item.itemName
                : item.gameObject.name;
        }

        public static void Reset()
        {
            LastWeaponByPlayer.Clear();
            LastOwnerByWeapon.Clear();
            DeathCandidates.Clear();
            PendingReturns.Clear();
            LastProtectionLogAt.Clear();
        }
    }
}
