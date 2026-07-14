# BetterReviveExperience

适用于 R.E.P.O. 的房主端复活与背包保留模组。
Host-side revive and inventory protection mod for R.E.P.O.

## 功能
## Features

- 死亡后保留背包栏物品；任何能放进原版背包格的手持物品都会受到防打落保护，包括 Phase Bridge、Drone 和武器。
- Keep inventory-slot items after death; any held item that fits a vanilla inventory slot receives forced-drop protection, including Phase Bridge, drones, and weapons.

- 玩家死亡时，可收纳的手持物品会进入原版前三格中的空格；满格时传送到死亡玩家的头附近。
- On death, return the storable held item to a free vanilla slot, or place it near the death head when all three slots are occupied.

- 死亡玩家的头进入提取点、卡车区域或推车时自动复活。
- Automatically revive when a death head enters an extraction point, truck area, or cart.

- 商店死亡后自动复活。
- Automatically revive players who die in the shop.

- 房主拿着死亡玩家的头时可按快捷键复活队友。
- The host can revive a teammate with a hotkey while holding their death head.

- 可设置团队货币费用与复活后血量。
- Configure team-currency cost and post-revive health.

- 背包保留、手持物品防打落、死亡物品回收、提取模式、持头快捷键、推车复活、商店复活和调试日志都可分别设置。
- Inventory retention, held-item protection, death item return, extraction mode, held-head hotkey, cart revive, shop revive, and debug logging are configurable separately.

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

- `ProtectHeldItems`：保护所有能放进原版背包的手持物品，默认开启；主动松手不受影响，远程客户端已松手时会自动回收到空格或玩家附近。
- `ProtectHeldItems`: protect all held items that fit vanilla inventory; enabled by default; manual release remains unchanged, with recovery to a free slot or nearby when a remote client already released it.

- `ReturnHeldItemOnDeath`：死亡时将可收纳的手持物品放入原版前三格的空格；满格则传送到死亡玩家的头附近，默认开启。
- `ReturnHeldItemOnDeath`: return a storable held item to a free vanilla slot on death, or place it near the death head when full; enabled by default.

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

- 本模组不会读取或写入扩展背包格。安装任意扩展背包模组时，请预留原版前三格中的至少一格；否则手持物品只能传送到死亡玩家的头附近。
- This mod does not read or write expanded inventory slots. When using any bag-expansion mod, keep at least one of the first three vanilla slots free; otherwise the held item can only be placed near the death head.

- 请不要同时启用其他自动复活模组，以避免重复复活或重复扣费。
- Do not enable other automatic revive mods at the same time, to avoid duplicate revives or charges.
