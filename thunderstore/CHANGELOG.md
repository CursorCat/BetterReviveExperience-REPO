# Changelog

## 0.2.11

- Re-upload the refreshed Thunderstore icon as a new immutable package version.

## 0.2.10

- Replace the Thunderstore icon with the new BetterReviveExperience branding.

## 0.2.9

- Simplify the held-head revive key list to H, R, Y, and F.

## 0.2.8

- Replace free-text held-head hotkey input with a fixed REPOConfig key list.
- Replace the cost range with fixed 1,000-currency increments from 0 to 100,000.
- Enable detailed debug logging by default for this development build.

## 0.2.7

- Add configurable `Revive.HeldHeadReviveKey`; the default remains H.
- Use the game's public run-currency wrappers for revive charges and refunds.
- Validate the currency wrappers during startup API compatibility checks.

## 0.2.6

- Add a REPOConfig `Debug.DebugLogging` toggle for detailed runtime diagnostics.
- Keep routine revive and error messages visible while hiding high-frequency probes by default.
- Document inventory-retention boundaries, tested dependency versions, GameTools interaction, and useful issue-report logs.
- Add independently configurable held-head H-key, cart, and shop revive triggers.
- Enable cart, shop, and host H-key revives by default.
- Apply the configured team cost and revive health to every BRE revive trigger.
- Use the immediate native death-head revive path without safety-spawn relocation or position confirmation.
- Detect and warn about overlapping revive mods that can issue duplicate revive RPCs.
- Require both Photon MasterClient authority and the game's host/single-player authority before every BRE state-changing path.

## 0.2.5

- Require the death head to remain continuously in a valid revive area for one second.
- Stop forcing room-volume recalculation every frame and use the game's cached area state.
- Move revived players to extraction/truck safety spawns through the native `PlayerAvatar.Spawn` RPC.
- Verify the resulting position and retry the safe spawn up to five times.
- Apply the configured revive health exactly in both single-player and multiplayer.
- Add soft GameTools compatibility ordering and a warning for overlapping automatic revive options.

## 0.2.4

- Wait for the death-head trigger setup to finish before issuing a revive.
- Use the game's native `PlayerDeathHead.Revive()` path for extraction-point revives.
- Keep the native truck revive path for truck-area revives.

## 0.2.3

- Keep Harmony patches installed when the loader disposes the plugin component after startup.
- Fix all runtime callbacks being removed immediately after successful patch registration.

## 0.2.2

- Replace automatic patch discovery with explicit Harmony registrations for every game method.
- Add one-time live-game probes for `PlayerAvatar.Update` and `PlayerDeathHead.Update`.
- Distinguish a patch registered at startup from a patch actually invoked during a session.

## 0.2.1

- Refresh the death head's room-volume check immediately before evaluating truck and extraction revive triggers.
- Add explicit diagnostics for death RPCs and death-head creation, so a failed revive can be located at the exact game event.

## 0.2.0

- Rebuilt revive handling around the game's native `PlayerDeathHead.Update` flow.
- Rebuilt inventory retention around the host-targeted `RPC_CompleteUnequip` flow.
- Removed the polling scanner, plugin coroutine, and custom runtime driver.
- Added exact host health synchronization after the vanilla revive RPC completes.
- Preserved the three revive modes, shared cost, timeout refund, and REPOConfig settings.
- Added a startup list of every Harmony target for compatibility diagnostics.

## 0.1.2

- Changed death-head discovery to use the authoritative player list and each player's bound death head.
- Added explicit room-volume checks for extraction and truck detection.
- Added staged diagnostics for host state, death RPCs, death heads, inventory unequip, area detection, and revive RPCs.
- Drive the revive controller from a dedicated persistent Unity runtime object and wait for `GameManager` initialization before scanning.

## 0.1.1

- Reworked inventory retention to run entirely on the host.
- Clients no longer need BetterReviveExperience installed.
- Added disabled, activated machine, and combined extraction/truck revive modes.
- Added shared currency cost and configurable revive health.
- Set the default revive cost to 0 with a configurable 0–100,000 range.
- Added per-player death tracking and host-side item state correction.
- Added revive timeout refunds and scene transition cleanup.
- Added game API compatibility validation logs.
- Added in-game configuration through REPOConfig.
- Fixed Harmony patches not being discovered when the parameterless PatchAll call resolved the wrong assembly.
- Added host-side death-head scanning and direct `deadSet` fallback so revive and inventory protection do not depend on a death event patch firing.
