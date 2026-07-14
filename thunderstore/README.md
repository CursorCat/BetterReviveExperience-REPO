# BetterReviveExperience

适用于 R.E.P.O. 的房主端复活与背包保留模组。
Host-side revive and inventory protection mod for R.E.P.O.

## 功能
## Features

- 死亡后保留背包栏物品；枪械、近战和发射器不会因受击或击倒而被强制打落。
- Keep inventory-slot items after death; guns, melee weapons, and launchers resist forced drops from hits and knockdowns.

- 玩家死亡时，手持武器会进入原版前三格中的空格；满格时传送到死亡玩家的头附近。
- On death, return the held weapon to a free vanilla slot, or place it near the death head when all three slots are occupied.

- 死亡玩家的头进入提取点、卡车区域或推车时自动复活。
- Automatically revive when a death head enters an extraction point, truck area, or cart.

- 商店死亡后自动复活。
- Automatically revive players who die in the shop.

- 房主拿着死亡玩家的头时可按快捷键复活队友。
- The host can revive a teammate with a hotkey while holding their death head.

- 可设置团队货币费用与复活后血量。
- Configure team-currency cost and post-revive health.

- 背包保留、武器防打落、死亡武器回收、提取模式、持头快捷键、推车复活、商店复活和调试日志都可分别设置。
- Inventory retention, forced-drop protection, death weapon return, extraction mode, held-head hotkey, cart revive, shop revive, and debug logging are configurable separately.

## 安装
## Installation

只需要房主安装；客户端不需要安装。
Only the host needs to install the mod; clients do not need it.

请同时安装 REPOConfig，以便在游戏内调整全部设置。
Install REPOConfig as well to adjust settings in-game.

## 设置
## Settings

- `KeepItemsOnDeath`：是否保留背包栏物品，默认开启。
- `KeepItemsOnDeath`: keep inventory-slot items; enabled by default.

- `ProtectHeldWeapons`：防止受击、击倒等强制松手事件打落枪械、近战和发射器，默认开启；主动松手不受影响。
- `ProtectHeldWeapons`: prevent forced drops of guns, melee weapons, and launchers; enabled by default; manual release remains unchanged.

- `ReturnHeldWeaponOnDeath`：死亡时将手持武器放入原版前三格的空格；满格则传送到死亡玩家的头附近，默认开启。
- `ReturnHeldWeaponOnDeath`: return the held weapon to a free vanilla slot on death, or place it near the death head when full; enabled by default.

- `Mode`：提取点/卡车规则，可选 `Disabled`、`ExtractionMachineActivated`、`ExtractionOrTruck`，默认 `ExtractionOrTruck`。
- `Mode`: extraction/truck rule; choose `Disabled`, `ExtractionMachineActivated`, or `ExtractionOrTruck`; default `ExtractionOrTruck`.

- `Cost`：每次复活消耗的团队货币，默认 `0`；可选 `0` 至 `100000`，每次增加 `1000`。
- `Cost`: team currency cost per revive; default `0`; choose `0` to `100000` in `1000` increments.

- `HealthPercent`：复活后的血量百分比，默认 `25`。
- `HealthPercent`: health percentage after revive; default `25`.

- `EnableHeldHeadRevive`：是否允许持死亡玩家的头快捷键复活，默认开启。
- `EnableHeldHeadRevive`: enable held-head hotkey revive; enabled by default.

- `HeldHeadReviveKey`：持死亡玩家的头复活按键，默认 `H`；仅可选 `H`、`R`、`Y`、`F`，不可自由输入文字。
- `HeldHeadReviveKey`: held-head revive key; default `H`; choose only `H`, `R`, `Y`, or `F` instead of entering free text.

- `EnableCartRevive`：死亡玩家的头放入推车后自动复活，默认开启。
- `EnableCartRevive`: auto-revive when a death head is placed in a cart; enabled by default.

- `EnableShopRevive`：商店死亡后自动复活，默认开启。
- `EnableShopRevive`: auto-revive players killed in the shop; enabled by default.

- `DebugLogging`：是否输出详细日志；当前开发版本默认开启，正式发布时会默认关闭。
- `DebugLogging`: write detailed logs; enabled by default in this development build and disabled by default for releases.

## 注意事项
## Notes

- 费用不足时不会复活，也不会扣钱。
- If there is not enough currency, the player is not revived and no money is charged.

- 团灭、开新局或存档重置时，不保证保留背包物品。
- Inventory is not guaranteed to persist through full-run failure, a new run, or a save reset.

- 本模组不会读取或写入扩展背包格。安装任意扩展背包模组时，请预留原版前三格中的至少一格；否则手持武器只能传送到死亡玩家的头附近。
- This mod does not read or write expanded inventory slots. When using any bag-expansion mod, keep at least one of the first three vanilla slots free; otherwise the held weapon can only be placed near the death head.

- 请不要同时启用其他自动复活模组，以避免重复复活或重复扣费。
- Do not enable other automatic revive mods at the same time, to avoid duplicate revives or charges.
