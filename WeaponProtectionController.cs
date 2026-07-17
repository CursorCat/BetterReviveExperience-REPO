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
            public int PreferredSlot;
            public float LastSeenAt;
        }

        private sealed class PendingWeaponReturn
        {
            public HeldWeaponRecord Weapon;
            public float ReadyAt;
            public float GiveUpAt;
        }

        private sealed class PendingForcedDropRecovery
        {
            public HeldWeaponRecord Item;
            public float ReadyAt;
            public float GiveUpAt;
        }

        private sealed class PendingInventorySwap
        {
            public HeldWeaponRecord HeldItem;
            public ItemEquippable OutgoingItem;
            public int TargetSlot;
            public float ReadyAt;
            public float GiveUpAt;
        }

        private const float RecentHoldWindowSeconds = 0.75f;
        private const float DeathCleanupDelaySeconds = 0.6f;
        private const float ReturnTimeoutSeconds = 4f;
        private const float ForcedDropRecoveryDelaySeconds = 0.05f;
        private const float ForcedDropRecoveryTimeoutSeconds = 2f;
        private const float InventorySwapDelaySeconds = 0.25f;
        private const float InventorySwapTimeoutSeconds = 2f;
        private const float ImpactReleaseMinimumDisableSeconds = 0.95f;
        private const float ImpactReleaseMaximumDisableSeconds = 2.05f;

        private static readonly FieldInfo GrabbedPhysObjectField =
            AccessTools.Field(typeof(PhysGrabber), "grabbedPhysGrabObject");

        private static readonly FieldInfo ForceGrabTimerField =
            AccessTools.Field(typeof(ItemEquippable), "forceGrabTimer");

        private static readonly FieldInfo ItemUnequipAutoHoldField =
            AccessTools.Field(typeof(GameplayManager), "itemUnequipAutoHold");

        private static readonly Dictionary<string, HeldWeaponRecord> LastWeaponByPlayer =
            new Dictionary<string, HeldWeaponRecord>();

        private static readonly Dictionary<int, string> LastOwnerByWeapon =
            new Dictionary<int, string>();

        private static readonly Dictionary<string, HeldWeaponRecord> DeathCandidates =
            new Dictionary<string, HeldWeaponRecord>();

        private static readonly Dictionary<string, PendingWeaponReturn> PendingReturns =
            new Dictionary<string, PendingWeaponReturn>();

        private static readonly Dictionary<string, PendingForcedDropRecovery> PendingForcedDropRecoveries =
            new Dictionary<string, PendingForcedDropRecovery>();

        private static readonly Dictionary<string, PendingInventorySwap> PendingInventorySwaps =
            new Dictionary<string, PendingInventorySwap>();

        private static readonly Dictionary<int, float> LastProtectionLogAt =
            new Dictionary<int, float>();

        private static bool AutoHoldWarningLogged;

        public static bool ValidateGameApi(ICollection<string> missing)
        {
            if (GrabbedPhysObjectField == null)
            {
                missing.Add("PhysGrabber.grabbedPhysGrabObject");
            }

            if (ForceGrabTimerField == null)
            {
                missing.Add("ItemEquippable.forceGrabTimer");
            }

            return GrabbedPhysObjectField != null && ForceGrabTimerField != null;
        }

        public static void ObservePlayer(PlayerAvatar player)
        {
            if (!ReviveController.IsHost() || !player || ReviveController.IsDead(player)) return;

            WarnIfNativeAutoHoldDisabled(player);

            PhysGrabObject physical = GetHeldPhysical(player.physGrabber);
            if (!IsStorableItem(physical, out ItemEquippable item)) return;
            if (!IsLatestActiveHolder(player.physGrabber, physical)) return;

            RecordHolder(player, physical, item);
        }

        public static void CaptureBeforeDeath(PlayerAvatar player)
        {
            if (!Plugin.ReturnHeldItemOnDeath.Value || !ReviveController.IsHost() || !player) return;

            string playerId = ReviveController.GetPlayerId(player);
            PhysGrabObject physical = GetHeldPhysical(player.physGrabber);
            HeldWeaponRecord record = null;

            if (IsStorableItem(physical, out ItemEquippable item) &&
                IsLatestActiveHolder(player.physGrabber, physical))
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
            Plugin.Debug($"[BRE] death item captured: player={playerId}, item={ItemName(record.Item)}");
        }

        public static void ConfirmDeath(PlayerAvatar player)
        {
            if (!Plugin.ReturnHeldItemOnDeath.Value || !ReviveController.IsHost() ||
                !player || !ReviveController.IsDead(player))
            {
                return;
            }

            string playerId = ReviveController.GetPlayerId(player);
            PendingForcedDropRecoveries.Remove(playerId);
            PendingInventorySwaps.Remove(playerId);
            if (!DeathCandidates.TryGetValue(playerId, out HeldWeaponRecord weapon)) return;

            DeathCandidates.Remove(playerId);
            PendingReturns[playerId] = new PendingWeaponReturn
            {
                Weapon = weapon,
                ReadyAt = Time.time + DeathCleanupDelaySeconds,
                GiveUpAt = Time.time + ReturnTimeoutSeconds
            };

            Plugin.Log.LogInfo($"[BRE] held item queued for death return: player={playerId}, " +
                               $"item={ItemName(weapon.Item)}");
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
                Plugin.Log.LogWarning($"[BRE] held item return cancelled: player={playerId}, item no longer exists");
                return;
            }

            if (!IsStillLastOwner(playerId, weapon.Physical) || IsHeldByAnotherPlayer(playerId, weapon.Physical))
            {
                PendingReturns.Remove(playerId);
                Plugin.Log.LogInfo($"[BRE] held item return cancelled: player={playerId}, " +
                                   $"item={ItemName(weapon.Item)}, reason=new-holder");
                return;
            }

            if (weapon.Item.IsEquipped())
            {
                PendingReturns.Remove(playerId);
                Plugin.Debug($"[BRE] held item already equipped: player={playerId}, item={ItemName(weapon.Item)}");
                return;
            }

            if (weapon.Physical.playerGrabbing.Count > 0 && Time.time < pending.GiveUpAt)
            {
                return;
            }

            int freeSlot = FindFreeVanillaSlot(player, playerId, weapon.PreferredSlot);
            if (freeSlot >= 0)
            {
                ReviveController.RestoreEquippedState(weapon.Item, freeSlot, weapon.OwnerViewId);
                PendingReturns.Remove(playerId);
                Plugin.Log.LogInfo($"[BRE] held item returned to inventory: player={playerId}, " +
                                   $"item={ItemName(weapon.Item)}, slot={freeSlot}");
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
            Plugin.Log.LogInfo($"[BRE] held item returned nearby: player={playerId}, " +
                               $"item={ItemName(weapon.Item)}, reason=no-free-vanilla-slot");
        }

        public static bool AllowForcedRelease(
            PhysGrabber grabber,
            bool physGrabEnded,
            float disableTimer,
            int releaseObjectViewId,
            PhotonMessageInfo info)
        {
            if (!Plugin.ProtectHeldItems.Value || !ReviveController.IsHost() || !grabber)
            {
                return true;
            }

            if (!IsImpactOrTumbleRelease(disableTimer, releaseObjectViewId))
            {
                return true;
            }

            PlayerAvatar player = grabber.playerAvatar;
            if (!player || ReviveController.IsDead(player)) return true;

            PhysGrabObject physical = GetHeldPhysical(grabber);
            if (!IsStorableItem(physical, out ItemEquippable item)) return true;
            if (!IsLatestActiveHolder(grabber, physical)) return true;

            HeldWeaponRecord record = RecordHolder(player, physical, item);
            string playerId = ReviveController.GetPlayerId(player);

            bool localOwner = IsLocalOwner(player);
            int freeSlot = FindFreeVanillaSlot(player, playerId, record.PreferredSlot);
            if (localOwner && freeSlot < 0)
            {
                int itemKey = GetWeaponKey(physical);
                if (!LastProtectionLogAt.TryGetValue(itemKey, out float lastLog) ||
                    Time.time - lastLog >= 1f)
                {
                    LastProtectionLogAt[itemKey] = Time.time;
                    Plugin.Log.LogInfo($"[BRE] kept forced-drop item in hand: player={playerId}, " +
                                       $"item={ItemName(item)}, reason=no-free-vanilla-slot");
                }

                return false;
            }

            if (localOwner && ForceGrabTimerField != null)
            {
                ForceGrabTimerField.SetValue(item, 0f);
            }

            PendingForcedDropRecoveries[playerId] = new PendingForcedDropRecovery
            {
                Item = record,
                ReadyAt = Time.time + ForcedDropRecoveryDelaySeconds,
                GiveUpAt = Time.time + ForcedDropRecoveryTimeoutSeconds
            };

            Plugin.Log.LogInfo($"[BRE] forced item release queued for inventory: player={playerId}, " +
                               $"item={ItemName(item)}, preferredSlot={record.PreferredSlot}, " +
                               $"disableTimer={disableTimer:0.##}, physGrabEnded={physGrabEnded}, " +
                               $"sender={GetSenderActorNumber(info)}, singleplayer={!SemiFunc.IsMultiplayer()}");
            return true;
        }

        public static void ProcessForcedDropRecovery(PlayerAvatar player)
        {
            if (!ReviveController.IsHost() || !player || ReviveController.IsDead(player)) return;

            string playerId = ReviveController.GetPlayerId(player);
            if (!PendingForcedDropRecoveries.TryGetValue(playerId, out PendingForcedDropRecovery pending) ||
                Time.time < pending.ReadyAt)
            {
                return;
            }

            HeldWeaponRecord heldItem = pending.Item;
            if (heldItem == null || !heldItem.Item || !heldItem.Physical)
            {
                PendingForcedDropRecoveries.Remove(playerId);
                return;
            }

            PhysGrabObject current = GetHeldPhysical(player.physGrabber);
            if (heldItem.Item.IsEquipped())
            {
                PendingForcedDropRecoveries.Remove(playerId);
                return;
            }

            if (current == heldItem.Physical)
            {
                if (Time.time < pending.GiveUpAt) return;

                PendingForcedDropRecoveries.Remove(playerId);
                Plugin.Debug($"[BRE] forced-drop recovery ended with item still held: player={playerId}, " +
                             $"item={ItemName(heldItem.Item)}");
                return;
            }

            if (!IsStillLastOwner(playerId, heldItem.Physical) ||
                IsHeldByAnotherPlayer(playerId, heldItem.Physical))
            {
                PendingForcedDropRecoveries.Remove(playerId);
                return;
            }

            if (heldItem.Physical.playerGrabbing.Count > 0 && Time.time < pending.GiveUpAt)
            {
                return;
            }

            int freeSlot = FindFreeVanillaSlot(player, playerId, heldItem.PreferredSlot);
            if (freeSlot >= 0)
            {
                ReviveController.RestoreEquippedState(heldItem.Item, freeSlot, heldItem.OwnerViewId);
                PendingForcedDropRecoveries.Remove(playerId);
                Plugin.Log.LogInfo($"[BRE] forced-drop item recovered to inventory: player={playerId}, " +
                                   $"item={ItemName(heldItem.Item)}, slot={freeSlot}");
                return;
            }

            if (StatsManager.instance == null && Time.time < pending.GiveUpAt) return;

            Vector3 returnPosition = player.transform.position + player.transform.forward * 0.75f + Vector3.up * 0.5f;
            heldItem.Physical.Teleport(returnPosition, heldItem.Physical.transform.rotation);
            if (heldItem.Physical.rb)
            {
                heldItem.Physical.rb.velocity = Vector3.zero;
                heldItem.Physical.rb.angularVelocity = Vector3.zero;
            }

            PendingForcedDropRecoveries.Remove(playerId);
            Plugin.Log.LogInfo($"[BRE] forced-drop item recovered nearby: player={playerId}, " +
                               $"item={ItemName(heldItem.Item)}, reason=no-free-vanilla-slot");
        }

        public static void CaptureInventorySwap(
            ItemEquippable outgoingItem,
            int physGrabberViewId,
            bool isForceUnequip)
        {
            if (!Plugin.SwapHeldItemOnOccupiedSlot.Value || isForceUnequip ||
                !ReviveController.IsHost() || !outgoingItem || !outgoingItem.IsEquipped())
            {
                return;
            }

            int targetSlot = ReviveController.GetInventorySpotIndex(outgoingItem);
            if (targetSlot < 0 || targetSlot > 2) return;

            PhysGrabber grabber = ResolveGrabber(physGrabberViewId);
            PlayerAvatar player = grabber ? grabber.playerAvatar : null;
            if (!player || ReviveController.IsDead(player)) return;

            PhysGrabObject physical = GetHeldPhysical(grabber);
            if (!IsStorableItem(physical, out ItemEquippable heldItem) ||
                heldItem == outgoingItem ||
                !IsLatestActiveHolder(grabber, physical))
            {
                return;
            }

            HeldWeaponRecord record = RecordHolder(player, physical, heldItem);
            string playerId = ReviveController.GetPlayerId(player);
            PendingForcedDropRecoveries.Remove(playerId);
            PendingInventorySwaps[playerId] = new PendingInventorySwap
            {
                HeldItem = record,
                OutgoingItem = outgoingItem,
                TargetSlot = targetSlot,
                ReadyAt = Time.time + InventorySwapDelaySeconds,
                GiveUpAt = Time.time + InventorySwapTimeoutSeconds
            };

            Plugin.Log.LogInfo($"[BRE] inventory swap queued: player={playerId}, " +
                               $"held={ItemName(heldItem)}, outgoing={ItemName(outgoingItem)}, " +
                               $"slot={targetSlot}");
        }

        public static void ProcessPendingInventorySwap(PlayerAvatar player)
        {
            if (!ReviveController.IsHost() || !player) return;

            string playerId = ReviveController.GetPlayerId(player);
            if (!PendingInventorySwaps.TryGetValue(playerId, out PendingInventorySwap pending) ||
                Time.time < pending.ReadyAt)
            {
                return;
            }

            if (ReviveController.IsDead(player))
            {
                CancelInventorySwap(playerId, pending, "player-dead");
                return;
            }

            HeldWeaponRecord held = pending.HeldItem;
            if (held == null || !held.Item || !held.Physical || !pending.OutgoingItem)
            {
                CancelInventorySwap(playerId, pending, "item-missing");
                return;
            }

            if (held.Item.IsEquipped())
            {
                if (ReviveController.GetInventorySpotIndex(held.Item) == pending.TargetSlot)
                {
                    PendingInventorySwaps.Remove(playerId);
                    Plugin.Log.LogInfo($"[BRE] inventory swap already completed: player={playerId}, " +
                                       $"item={ItemName(held.Item)}, slot={pending.TargetSlot}");
                }
                else
                {
                    CancelInventorySwap(playerId, pending, "held-item-equipped-elsewhere");
                }

                return;
            }

            if (!IsStillLastOwner(playerId, held.Physical) ||
                IsHeldByAnotherPlayer(playerId, held.Physical))
            {
                CancelInventorySwap(playerId, pending, "new-holder");
                return;
            }

            if (pending.OutgoingItem.IsEquipped())
            {
                if (Time.time < pending.GiveUpAt) return;

                CancelInventorySwap(playerId, pending, "outgoing-item-still-equipped");
                return;
            }

            if (held.Physical.playerGrabbing.Count > 0 && Time.time < pending.GiveUpAt)
            {
                return;
            }

            if (!IsVanillaSlotFree(player, playerId, pending.TargetSlot))
            {
                if (Time.time < pending.GiveUpAt) return;

                CancelInventorySwap(playerId, pending, "target-slot-occupied");
                return;
            }

            ReviveController.RestoreEquippedState(
                held.Item,
                pending.TargetSlot,
                held.OwnerViewId
            );
            PendingInventorySwaps.Remove(playerId);
            Plugin.Log.LogInfo($"[BRE] inventory swap completed: player={playerId}, " +
                               $"stored={ItemName(held.Item)}, held={ItemName(pending.OutgoingItem)}, " +
                               $"slot={pending.TargetSlot}");
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
                PreferredSlot = ReviveController.GetInventorySpotIndex(item),
                LastSeenAt = Time.time
            };

            LastOwnerByWeapon[weaponKey] = playerId;
            LastWeaponByPlayer[playerId] = record;

            if (changed)
            {
                Plugin.Debug($"[BRE] storable item holder recorded: player={playerId}, item={ItemName(item)}");
            }

            return record;
        }

        private static PhysGrabObject GetHeldPhysical(PhysGrabber grabber)
        {
            if (!grabber || !grabber.grabbed || GrabbedPhysObjectField == null) return null;

            PhysGrabObject physical = GrabbedPhysObjectField.GetValue(grabber) as PhysGrabObject;
            return physical && physical.playerGrabbing.Contains(grabber)
                ? physical
                : null;
        }

        private static bool IsLatestActiveHolder(PhysGrabber grabber, PhysGrabObject physical)
        {
            if (!grabber || !physical) return false;

            for (int index = physical.playerGrabbing.Count - 1; index >= 0; index--)
            {
                PhysGrabber candidate = physical.playerGrabbing[index];
                if (!candidate || !candidate.grabbed || GrabbedPhysObjectField == null) continue;

                if (GrabbedPhysObjectField.GetValue(candidate) as PhysGrabObject == physical)
                {
                    return candidate == grabber;
                }
            }

            return false;
        }

        private static bool IsImpactOrTumbleRelease(float disableTimer, int releaseObjectViewId)
        {
            return releaseObjectViewId == -1 &&
                   disableTimer >= ImpactReleaseMinimumDisableSeconds &&
                   disableTimer <= ImpactReleaseMaximumDisableSeconds;
        }

        private static int GetSenderActorNumber(PhotonMessageInfo info)
        {
            return info.Sender != null ? info.Sender.ActorNumber : -1;
        }

        private static void WarnIfNativeAutoHoldDisabled(PlayerAvatar player)
        {
            if (AutoHoldWarningLogged || !IsLocalOwner(player) ||
                GameplayManager.instance == null || ItemUnequipAutoHoldField == null)
            {
                return;
            }

            object settingValue = ItemUnequipAutoHoldField.GetValue(GameplayManager.instance);
            if (settingValue is bool enabled && !enabled)
            {
                AutoHoldWarningLogged = true;
                Plugin.Log.LogWarning(
                    "[BRE] The native ItemUnequipAutoHold setting is disabled. Items taken from inventory " +
                    "will be released after the game's temporary hold expires. Enable the game's auto-hold " +
                    "setting; ProtectHeldItems only handles impact and tumble releases."
                );
            }
        }

        private static bool IsStorableItem(PhysGrabObject physical, out ItemEquippable item)
        {
            item = null;
            if (!physical || physical.dead) return false;

            item = physical.GetComponent<ItemEquippable>();
            return item;
        }

        private static PhysGrabber ResolveGrabber(int photonViewId)
        {
            if (!SemiFunc.IsMultiplayer()) return PhysGrabber.instance;

            PhotonView view = PhotonView.Find(photonViewId);
            return view ? view.GetComponent<PhysGrabber>() : null;
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

        private static int FindFreeVanillaSlot(
            PlayerAvatar player,
            string playerId,
            int preferredSlot = -1)
        {
            StatsManager stats = StatsManager.instance;
            bool useLocalInventory = IsLocalOwner(player) && Inventory.instance != null;
            if (!useLocalInventory && (stats == null || string.IsNullOrEmpty(playerId))) return -1;

            if (preferredSlot >= 0 && preferredSlot <= 2 &&
                !IsVanillaSlotTaken(stats, playerId, preferredSlot, useLocalInventory))
            {
                return preferredSlot;
            }

            for (int slot = 0; slot <= 2; slot++)
            {
                if (!IsVanillaSlotTaken(stats, playerId, slot, useLocalInventory)) return slot;
            }

            return -1;
        }

        private static bool IsVanillaSlotFree(PlayerAvatar player, string playerId, int slot)
        {
            if (slot < 0 || slot > 2) return false;

            StatsManager stats = StatsManager.instance;
            bool useLocalInventory = IsLocalOwner(player) && Inventory.instance != null;
            if (!useLocalInventory && (stats == null || string.IsNullOrEmpty(playerId))) return false;

            return !IsVanillaSlotTaken(stats, playerId, slot, useLocalInventory);
        }

        private static bool IsVanillaSlotTaken(
            StatsManager stats,
            string playerId,
            int slot,
            bool useLocalInventory)
        {
            if (useLocalInventory)
            {
                InventorySpot inventorySpot = Inventory.instance.GetSpotByIndex(slot);
                return inventorySpot != null && inventorySpot.IsOccupied();
            }

            if (slot == 0) return stats.playerInventorySpot1.ContainsKey(playerId);
            if (slot == 1) return stats.playerInventorySpot2.ContainsKey(playerId);
            return stats.playerInventorySpot3.ContainsKey(playerId);
        }

        private static bool IsLocalOwner(PlayerAvatar player)
        {
            return player && (!SemiFunc.IsMultiplayer() ||
                              (player.photonView && player.photonView.IsMine));
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

        private static string ItemName(ItemEquippable item)
        {
            if (!item) return "unknown";

            ItemAttributes attributes = item.GetComponent<ItemAttributes>();
            return attributes && attributes.item
                ? attributes.item.itemName
                : item.gameObject.name;
        }

        private static void CancelInventorySwap(
            string playerId,
            PendingInventorySwap pending,
            string reason)
        {
            PendingInventorySwaps.Remove(playerId);
            Plugin.Log.LogInfo($"[BRE] inventory swap cancelled: player={playerId}, " +
                               $"item={ItemName(pending?.HeldItem?.Item)}, reason={reason}");
        }

        public static void Reset()
        {
            LastWeaponByPlayer.Clear();
            LastOwnerByWeapon.Clear();
            DeathCandidates.Clear();
            PendingReturns.Clear();
            PendingForcedDropRecoveries.Clear();
            PendingInventorySwaps.Clear();
            LastProtectionLogAt.Clear();
        }
    }
}
