using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace BetterReviveExperience
{
    internal static class ReviveController
    {
        private sealed class PendingRevive
        {
            public int Cost;
            public int TargetHealth;
            public float StartedAt;
            public string Trigger;
        }

        private sealed class PendingHealth
        {
            public PlayerAvatar Player;
            public int TargetHealth;
            public float ApplyAt;
        }

        private const float ReviveTimeoutSeconds = 5f;
        private const float HealthSyncDelaySeconds = 0.35f;

        private static readonly FieldInfo SteamIdField = AccessTools.Field(typeof(PlayerAvatar), "steamID");
        private static readonly FieldInfo DeadSetField = AccessTools.Field(typeof(PlayerAvatar), "deadSet");
        private static readonly FieldInfo PlayerDeathHeadField = AccessTools.Field(typeof(PlayerAvatar), "playerDeathHead");
        private static readonly FieldInfo TriggeredField = AccessTools.Field(typeof(PlayerDeathHead), "triggered");
        private static readonly FieldInfo TriggeredTimerField = AccessTools.Field(typeof(PlayerDeathHead), "triggeredTimer");
        private static readonly FieldInfo DeathHeadPhysGrabObjectField = AccessTools.Field(typeof(PlayerDeathHead), "physGrabObject");
        private static readonly FieldInfo InExtractionPointField = AccessTools.Field(typeof(PlayerDeathHead), "inExtractionPoint");
        private static readonly FieldInfo DeathHeadRoomVolumeField = AccessTools.Field(typeof(PlayerDeathHead), "roomVolumeCheck");
        private static readonly FieldInfo RoomInTruckField = AccessTools.Field(typeof(RoomVolumeCheck), "inTruck");
        private static readonly FieldInfo RoomInExtractionPointField = AccessTools.Field(typeof(RoomVolumeCheck), "inExtractionPoint");
        private static readonly FieldInfo CartItemsField = AccessTools.Field(typeof(PhysGrabCart), "itemsInCart");
        private static readonly FieldInfo ExtractionPointActiveField = AccessTools.Field(typeof(RoundDirector), "extractionPointActive");
        private static readonly FieldInfo MaxHealthField = AccessTools.Field(typeof(PlayerHealth), "maxHealth");
        private static readonly FieldInfo InventorySpotIndexField = AccessTools.Field(typeof(ItemEquippable), "inventorySpotIndex");
        private static readonly MethodInfo UpdateItemStateMethod = AccessTools.Method(typeof(ItemEquippable), "RPC_UpdateItemState");
        private static readonly MethodInfo UpdateHealthMethod = AccessTools.Method(typeof(PlayerHealth), "UpdateHealthRPC");
        private static readonly MethodInfo StatGetRunCurrencyMethod = AccessTools.Method(typeof(SemiFunc), "StatGetRunCurrency");
        private static readonly MethodInfo StatSetRunCurrencyMethod = AccessTools.Method(typeof(SemiFunc), "StatSetRunCurrency");

        private static readonly Dictionary<string, PendingRevive> PendingRevives = new Dictionary<string, PendingRevive>();
        private static readonly Dictionary<string, PendingHealth> PendingHealthSyncs = new Dictionary<string, PendingHealth>();
        private static readonly Dictionary<string, string> LastHeadStates = new Dictionary<string, string>();
        private static readonly HashSet<string> InsufficientFundsLogged = new HashSet<string>();
        private static readonly HashSet<string> ObservedPlayerUpdates = new HashSet<string>();
        private static readonly HashSet<string> ObservedDeathHeadUpdates = new HashSet<string>();

        public static bool ValidateGameApi()
        {
            var missing = new List<string>();
            Require(SteamIdField, "PlayerAvatar.steamID", missing);
            Require(DeadSetField, "PlayerAvatar.deadSet", missing);
            Require(PlayerDeathHeadField, "PlayerAvatar.playerDeathHead", missing);
            Require(TriggeredField, "PlayerDeathHead.triggered", missing);
            Require(TriggeredTimerField, "PlayerDeathHead.triggeredTimer", missing);
            Require(DeathHeadPhysGrabObjectField, "PlayerDeathHead.physGrabObject", missing);
            Require(InExtractionPointField, "PlayerDeathHead.inExtractionPoint", missing);
            Require(DeathHeadRoomVolumeField, "PlayerDeathHead.roomVolumeCheck", missing);
            Require(RoomInTruckField, "RoomVolumeCheck.inTruck", missing);
            Require(RoomInExtractionPointField, "RoomVolumeCheck.inExtractionPoint", missing);
            Require(CartItemsField, "PhysGrabCart.itemsInCart", missing);
            Require(ExtractionPointActiveField, "RoundDirector.extractionPointActive", missing);
            Require(MaxHealthField, "PlayerHealth.maxHealth", missing);
            Require(InventorySpotIndexField, "ItemEquippable.inventorySpotIndex", missing);
            Require(UpdateItemStateMethod, "ItemEquippable.RPC_UpdateItemState", missing);
            Require(UpdateHealthMethod, "PlayerHealth.UpdateHealthRPC", missing);
            Require(StatGetRunCurrencyMethod, "SemiFunc.StatGetRunCurrency", missing);
            Require(StatSetRunCurrencyMethod, "SemiFunc.StatSetRunCurrency", missing);
            WeaponProtectionController.ValidateGameApi(missing);

            if (missing.Count == 0)
            {
                Plugin.Debug("[BRE] game API validation passed");
                return true;
            }

            Plugin.Log.LogError("[BRE] game API validation failed: " + string.Join(", ", missing));
            return false;
        }

        private static void Require(MemberInfo member, string name, ICollection<string> missing)
        {
            if (member == null) missing.Add(name);
        }

        public static string GetPlayerId(PlayerAvatar player)
        {
            if (!player) return string.Empty;

            string steamId = SteamIdField?.GetValue(player) as string;
            if (!string.IsNullOrEmpty(steamId)) return steamId;

            return player.photonView
                ? $"view:{player.photonView.ViewID}"
                : $"instance:{player.GetInstanceID()}";
        }

        internal static bool IsDead(PlayerAvatar player)
        {
            return player && DeadSetField != null && (bool)DeadSetField.GetValue(player);
        }

        internal static bool IsHost()
        {
            if (GameManager.instance == null) return false;

            // Match GameTools' authority boundary, but do not use its extra
            // "local player" exception: BRE is intentionally host-only.
            if (!SemiFunc.IsMultiplayer())
            {
                return SemiFunc.IsMasterClientOrSingleplayer();
            }

            return PhotonNetwork.InRoom &&
                   PhotonNetwork.IsMasterClient &&
                   SemiFunc.IsMasterClientOrSingleplayer();
        }

        private static bool IsHeadReady(PlayerDeathHead deathHead)
        {
            if (!deathHead || TriggeredField == null || !(bool)TriggeredField.GetValue(deathHead))
            {
                return false;
            }

            return TriggeredTimerField == null || (float)TriggeredTimerField.GetValue(deathHead) <= 0f;
        }

        private static PlayerDeathHead GetDeathHead(PlayerAvatar player)
        {
            return player && PlayerDeathHeadField != null
                ? PlayerDeathHeadField.GetValue(player) as PlayerDeathHead
                : null;
        }

        public static void OnPlayerDeath(PlayerAvatar player)
        {
            if (!player) return;
            if (!IsHost()) return;

            string playerId = GetPlayerId(player);
            bool dead = IsDead(player);
            Plugin.Debug($"[BRE] PlayerDeathRPC observed: player={playerId}, host=true, deadSet={dead}");
            if (!dead) return;

            PendingHealthSyncs.Remove(playerId);
            InsufficientFundsLogged.Remove(playerId);
            Plugin.Debug($"[BRE] death confirmed: player={playerId}");
        }

        public static void OnPlayerAvatarUpdated(PlayerAvatar player)
        {
            if (!player) return;
            if (!IsHost()) return;

            string playerId = GetPlayerId(player);
            if (ObservedPlayerUpdates.Add(playerId))
            {
                Plugin.Debug($"[BRE] runtime probe: PlayerAvatar.Update reached, player={playerId}, host=true");
            }

            ApplyPendingHealth(playerId);

            // The native revive can clear deadSet before another game component
            // throws. Finalize our cost/health state when that partial success is
            // visible on the following player update.
            if (!IsDead(player) && PendingRevives.TryGetValue(playerId, out PendingRevive pending))
            {
                CompleteRevive(playerId, player, pending);
            }

            PlayerController controller = PlayerController.instance;
            if (controller && controller.playerAvatarScript == player)
            {
                TryHeldHeadRevive(controller);
            }
        }

        private static void TryHeldHeadRevive(PlayerController controller)
        {
            if (!Plugin.EnableHeldHeadRevive.Value || !Input.GetKeyDown(Plugin.HeldHeadReviveKeyCode) ||
                !controller.physGrabActive || !controller.physGrabObject)
            {
                return;
            }

            PlayerDeathHead deathHead = controller.physGrabObject.GetComponent<PlayerDeathHead>();
            if (!deathHead)
            {
                deathHead = controller.physGrabObject.GetComponentInParent<PlayerDeathHead>();
            }

            if (!deathHead || !deathHead.playerAvatar || !IsDead(deathHead.playerAvatar) || !IsHeadReady(deathHead))
            {
                Plugin.Debug("[BRE] held-head revive ignored: grabbed object is not a ready death head");
                return;
            }

            BeginRevive(deathHead, false, $"held-head-{Plugin.HeldHeadReviveKey.Value}");
        }

        public static void OnDeathHeadTriggered(PlayerDeathHead deathHead)
        {
            if (!deathHead || !IsHost() || !deathHead.playerAvatar) return;

            Plugin.Debug($"[BRE] death head triggered: player={GetPlayerId(deathHead.playerAvatar)}, host=true");
        }

        public static void OnDeathHeadUpdated(PlayerDeathHead deathHead)
        {
            if (!deathHead || !IsHost() || !deathHead.playerAvatar) return;

            PlayerAvatar player = deathHead.playerAvatar;
            string playerId = GetPlayerId(player);
            if (ObservedDeathHeadUpdates.Add(playerId))
            {
                Plugin.Debug($"[BRE] runtime probe: PlayerDeathHead.Update reached, player={playerId}, host=true");
            }
            ApplyPendingHealth(playerId);

            if (!IsDead(player)) return;

            if (PendingRevives.TryGetValue(playerId, out PendingRevive pending))
            {
                if (Time.time - pending.StartedAt < ReviveTimeoutSeconds) return;

                PendingRevives.Remove(playerId);
                Refund(pending.Cost);
                Plugin.Log.LogWarning($"[BRE] revive timed out and refunded: player={playerId}, " +
                                      $"trigger={pending.Trigger}, cost={pending.Cost}");
            }

            if (!IsHeadReady(deathHead)) return;

            if (Plugin.EnableShopRevive.Value && SemiFunc.RunIsShop())
            {
                BeginRevive(deathHead, false, "shop");
                return;
            }

            if (Plugin.CurrentReviveMode == ReviveMode.Disabled) return;

            RoomVolumeCheck roomVolume = DeathHeadRoomVolumeField?.GetValue(deathHead) as RoomVolumeCheck;
            bool deathHeadInExtraction = InExtractionPointField != null && (bool)InExtractionPointField.GetValue(deathHead);
            bool roomInExtraction = ReadBool(roomVolume, RoomInExtractionPointField);
            bool inExtraction = deathHeadInExtraction || roomInExtraction;
            bool inTruck = ReadBool(roomVolume, RoomInTruckField);
            bool machineActive = RoundDirector.instance != null &&
                                 ExtractionPointActiveField != null &&
                                 (bool)ExtractionPointActiveField.GetValue(RoundDirector.instance);

            string state = $"roomCheck={(bool)roomVolume}, headExtraction={deathHeadInExtraction}, " +
                           $"roomExtraction={roomInExtraction}, truck={inTruck}, machine={machineActive}";
            if (!LastHeadStates.TryGetValue(playerId, out string previous) || previous != state)
            {
                LastHeadStates[playerId] = state;
                Plugin.Debug($"[BRE] death head: player={playerId}, {state}");
            }

            if (Plugin.CurrentReviveMode == ReviveMode.ExtractionMachineActivated)
            {
                if (inExtraction && machineActive)
                {
                    BeginRevive(deathHead, false, "extraction-machine");
                }
                return;
            }

            if (inTruck)
            {
                BeginRevive(deathHead, true, "truck");
            }
            else if (inExtraction)
            {
                BeginRevive(deathHead, false, "extraction");
            }
        }

        public static void OnCartUpdated(PhysGrabCart cart)
        {
            if (!Plugin.EnableCartRevive.Value || !IsHost() || !cart ||
                GameDirector.instance == null || CartItemsField == null)
            {
                return;
            }

            IEnumerable items = CartItemsField.GetValue(cart) as IEnumerable;
            if (items == null) return;

            foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
            {
                if (!player || !IsDead(player)) continue;

                PlayerDeathHead deathHead = GetDeathHead(player);
                if (!IsHeadReady(deathHead)) continue;

                PhysGrabObject headObject = DeathHeadPhysGrabObjectField?.GetValue(deathHead) as PhysGrabObject;
                if (!headObject || !Contains(items, headObject)) continue;

                BeginRevive(deathHead, false, "cart");
            }
        }

        private static bool Contains(IEnumerable items, PhysGrabObject target)
        {
            foreach (object item in items)
            {
                if (item is PhysGrabObject candidate && candidate == target)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ReadBool(object instance, FieldInfo field)
        {
            return instance != null && field != null && (bool)field.GetValue(instance);
        }

        private static void BeginRevive(PlayerDeathHead deathHead, bool revivedByTruck, string trigger)
        {
            if (!IsHost()) return;

            PlayerAvatar player = deathHead ? deathHead.playerAvatar : null;
            if (!player || !IsDead(player) || PendingRevives.ContainsKey(GetPlayerId(player))) return;

            if (StatsManager.instance == null || PunManager.instance == null)
            {
                Plugin.Log.LogWarning($"[BRE] revive postponed: currency managers are not ready, trigger={trigger}");
                return;
            }

            string playerId = GetPlayerId(player);
            int cost = Plugin.ReviveCostAmount;
            int currency = SemiFunc.StatGetRunCurrency();

            if (currency < cost)
            {
                if (InsufficientFundsLogged.Add(playerId))
                {
                    Plugin.Log.LogInfo($"[BRE] revive denied: player={playerId}, trigger={trigger}, " +
                                       $"cost={cost}, currency={currency}");
                }
                return;
            }

            int maxHealth = GetMaxHealth(player.playerHealth);
            int targetHealth = Mathf.Clamp(
                Mathf.CeilToInt(maxHealth * Plugin.ReviveHealthPercent.Value / 100f),
                1,
                maxHealth
            );

            if (cost > 0)
            {
                SemiFunc.StatSetRunCurrency(currency - cost);
            }

            PendingRevives[playerId] = new PendingRevive
            {
                Cost = cost,
                TargetHealth = targetHealth,
                StartedAt = Time.time,
                Trigger = trigger
            };

            Plugin.Log.LogInfo($"[BRE] revive request: player={playerId}, trigger={trigger}, " +
                               $"cost={cost}, health={targetHealth}/{maxHealth}");

            try
            {
                // This is the same immediate native path used by Hypn's mod:
                // the game revives at the settled death-head position and owns all
                // player/controller/camera synchronization.
                player.Revive(revivedByTruck);
            }
            catch (Exception exception)
            {
                if (!IsDead(player))
                {
                    Plugin.Log.LogWarning($"[BRE] native revive threw after clearing death state; " +
                                          $"continuing health sync: player={playerId}, trigger={trigger}, {exception}");
                    if (PendingRevives.TryGetValue(playerId, out PendingRevive pending))
                    {
                        CompleteRevive(playerId, player, pending);
                    }
                }
                else
                {
                    PendingRevives.Remove(playerId);
                    Refund(cost);
                    Plugin.Log.LogError($"[BRE] revive call failed and refunded: player={playerId}, " +
                                        $"trigger={trigger}, {exception}");
                }
            }
        }

        public static void OnPlayerRevived(PlayerAvatar player)
        {
            if (!IsHost() || !player || IsDead(player)) return;

            string playerId = GetPlayerId(player);
            if (!PendingRevives.TryGetValue(playerId, out PendingRevive pending))
            {
                Plugin.Debug($"[BRE] vanilla/other revive observed: player={playerId}");
                return;
            }

            CompleteRevive(playerId, player, pending);
        }

        private static void CompleteRevive(string playerId, PlayerAvatar player, PendingRevive pending)
        {
            PendingRevives.Remove(playerId);
            InsufficientFundsLogged.Remove(playerId);
            LastHeadStates.Remove(playerId);
            PendingHealthSyncs[playerId] = new PendingHealth
            {
                Player = player,
                TargetHealth = pending.TargetHealth,
                ApplyAt = Time.time + HealthSyncDelaySeconds
            };

            Plugin.Log.LogInfo($"[BRE] revive confirmed: player={playerId}, trigger={pending.Trigger}, cost={pending.Cost}");
        }

        private static void ApplyPendingHealth(string playerId)
        {
            if (!IsHost()) return;

            if (!PendingHealthSyncs.TryGetValue(playerId, out PendingHealth pending) ||
                Time.time < pending.ApplyAt)
            {
                return;
            }

            PendingHealthSyncs.Remove(playerId);
            PlayerAvatar player = pending.Player;
            if (!player || !player.playerHealth) return;

            PlayerHealth health = player.playerHealth;
            int maxHealth = GetMaxHealth(health);
            int targetHealth = Mathf.Clamp(pending.TargetHealth, 1, maxHealth);

            if (SemiFunc.IsMultiplayer())
            {
                PhotonView healthView = health.GetComponent<PhotonView>();
                if (!healthView)
                {
                    Plugin.Log.LogWarning($"[BRE] revive health sync failed: player={playerId}, PhotonView missing");
                    return;
                }

                healthView.RPC("UpdateHealthRPC", RpcTarget.All, targetHealth, maxHealth, true, false);
            }
            else
            {
                health.UpdateHealthRPC(targetHealth, maxHealth, true, false);
            }

            Plugin.Log.LogInfo($"[BRE] revive health synchronized: player={playerId}, health={targetHealth}/{maxHealth}");
        }

        private static int GetMaxHealth(PlayerHealth health)
        {
            return health && MaxHealthField != null ? (int)MaxHealthField.GetValue(health) : 100;
        }

        public static bool AllowForcedUnequip(
            ItemEquippable item,
            int physGrabberPhotonViewId,
            bool isForceUnequip)
        {
            if (!isForceUnequip || !Plugin.KeepItemsOnDeath.Value || !IsHost()) return true;

            PhysGrabber grabber = ResolveGrabber(physGrabberPhotonViewId);
            PlayerAvatar player = grabber ? grabber.playerAvatar : null;
            if (!player || !IsDead(player)) return true;

            int spot = item && InventorySpotIndexField != null
                ? (int)InventorySpotIndexField.GetValue(item)
                : -1;
            if (!item || spot < 0)
            {
                Plugin.Log.LogWarning($"[BRE] could not preserve item: player={GetPlayerId(player)}, slot={spot}");
                return true;
            }

            RestoreEquippedState(item, spot, physGrabberPhotonViewId);
            Plugin.Debug($"[BRE] inventory item preserved: player={GetPlayerId(player)}, slot={spot}");
            return false;
        }

        private static PhysGrabber ResolveGrabber(int photonViewId)
        {
            if (!SemiFunc.IsMultiplayer()) return PhysGrabber.instance;

            PhotonView view = PhotonView.Find(photonViewId);
            return view ? view.GetComponent<PhysGrabber>() : null;
        }

        internal static void RestoreEquippedState(ItemEquippable item, int spot, int ownerId)
        {
            if (SemiFunc.IsMultiplayer())
            {
                item.photonView.RPC("RPC_UpdateItemState", RpcTarget.All, 3, spot, ownerId);
                item.photonView.RPC("RPC_UpdateItemState", RpcTarget.All, 2, spot, ownerId);
                return;
            }

            UpdateItemStateMethod.Invoke(
                item,
                new object[] { 3, spot, ownerId, default(PhotonMessageInfo) }
            );
            UpdateItemStateMethod.Invoke(
                item,
                new object[] { 2, spot, ownerId, default(PhotonMessageInfo) }
            );
        }

        private static void Refund(int cost)
        {
            if (!IsHost() || cost <= 0 || StatsManager.instance == null || PunManager.instance == null) return;

            int currency = SemiFunc.StatGetRunCurrency();
            SemiFunc.StatSetRunCurrency(currency + cost);
        }

        public static void Reset(bool refundPending = false)
        {
            if (refundPending)
            {
                foreach (PendingRevive pending in PendingRevives.Values)
                {
                    Refund(pending.Cost);
                }
            }

            PendingRevives.Clear();
            PendingHealthSyncs.Clear();
            LastHeadStates.Clear();
            InsufficientFundsLogged.Clear();
            ObservedPlayerUpdates.Clear();
            ObservedDeathHeadUpdates.Clear();
        }
    }
}
