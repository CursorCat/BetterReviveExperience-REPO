# BetterReviveExperience

适用于 R.E.P.O. 的房主端复活与背包保留模组。
Host-side revive and inventory protection mod for R.E.P.O.

## 功能
## Features

- 死亡后保留背包栏物品；Phase Bridge、Drone、武器等可收纳物品在受击或翻滚时会自动回到背包。
- Keep inventory-slot items after death; storable items such as Phase Bridge, drones, and weapons automatically return to inventory after a hit or tumble.

- 记录可收纳物品最后的实际持有者；玩家死亡时，手持物品会进入原版前三格中的空格，满格时传送到死亡玩家的头附近。
- Track the last actual holder of each storable item; on death, return the held item to a free vanilla slot, or place it near the death head when all three slots are occupied.

- 死亡玩家的头进入提取点、卡车区域或推车时可自动复活。
- Automatically revive when a death head enters an extraction point, truck area, or cart.

- 在商店死亡后自动复活。
- Automatically revive players who die in the shop.

- 房主拿着死亡玩家的头时可按快捷键复活队友。
- The host can revive a teammate with a hotkey while holding their death head.

- 可设置复活费用与复活后血量。
- Configure revive cost and post-revive health.

- 背包保留、手持物品防打落、死亡物品回收、提取模式、持头快捷键、推车复活、商店复活和调试日志都可分别设置。
- Inventory retention, held-item protection, death item return, extraction mode, held-head hotkey, cart revive, shop revive, and debug logging are configurable separately.

## 安装
## Installation

只需要房主安装；朋友加入房间不需要安装本模组。
Only the host needs to install the mod; friends joining the lobby do not need it.

请通过 r2modman 或 Thunderstore Mod Manager 安装，并同时安装 REPOConfig。
Install through r2modman or Thunderstore Mod Manager, together with REPOConfig.

## 配置
## Configuration

在游戏菜单打开 `Mods → BetterReviveExperience`。
Open `Mods → BetterReviveExperience` in the game menu.

| 设置 | 默认值 | 简要说明 |
|---|---:|---|
| `KeepItemsOnDeath` | 开启 / On | 保留背包栏物品。<br>Keep inventory-slot items. |
| `ProtectHeldItems` | 开启 / On | 可收纳的手持物品因受击或翻滚被强制松开时，优先回到原格，再使用其他原版空格；主动松手不受影响。前三格全满时，单人/房主继续持有，远程玩家的物品回收到附近。<br>After an impact or tumble release, return a storable held item to its original slot, then another vanilla slot; manual release remains unchanged. When full, the local player keeps holding it and a remote item is recovered nearby. |
| `ReturnHeldItemOnDeath` | 开启 / On | 死亡时将可收纳的手持物品放入原版前三格的空格；满格则传送到死亡玩家的头附近。<br>Return a storable held item to a free vanilla slot on death, or place it near the death head when full. |
| `Mode` | `ExtractionOrTruck` | 选择 `Disabled`、`ExtractionMachineActivated` 或 `ExtractionOrTruck`。<br>Choose `Disabled`, `ExtractionMachineActivated`, or `ExtractionOrTruck`. |
| `Cost` | `0` | 每次复活消耗的团队货币；可选 `0` 至 `100000`，每次增加 `1000`。<br>Team currency cost per revive; choose `0` to `100000` in `1000` increments. |
| `HealthPercent` | `25` | 复活后的血量百分比。<br>Health percentage after revive. |
| `EnableHeldHeadRevive` | 开启 / On | 是否允许持死亡玩家的头快捷键复活。<br>Enable held-head hotkey revive. |
| `HeldHeadReviveKey` | `H` | 持死亡玩家的头复活按键；仅可选 `H`、`R`、`Y`、`F`，不可自由输入文字。<br>Held-head revive key; choose only `H`, `R`, `Y`, or `F` instead of entering free text. |
| `EnableCartRevive` | 开启 / On | 死亡玩家的头放进推车后自动复活。<br>Auto-revive when a death head is placed in a cart. |
| `EnableShopRevive` | 开启 / On | 商店死亡后自动复活。<br>Auto-revive players killed in the shop. |
| `DebugLogging` | 开启 / On | 当前开发版本默认输出详细日志；正式发布时会默认关闭。<br>This development build writes detailed logs by default; release builds will default to off. |

修改设置后，请重新启动游戏再测试。
Restart the game after changing settings before testing.

## 注意事项
## Notes

- 费用不足时不会复活，也不会扣钱。
- If there is not enough currency, the player is not revived and no money is charged.

- 团灭、开新局或存档重置时，不保证保留背包物品。
- Inventory is not guaranteed to persist through full-run failure, a new run, or a save reset.

- 本模组不会读取或写入扩展背包格。安装任意扩展背包模组时，请预留原版前三格中的至少一格；否则死亡时的手持物品只能传送到死亡玩家的头附近。
- This mod does not read or write expanded inventory slots. When using any bag-expansion mod, keep at least one of the first three vanilla slots free; otherwise the held item can only be placed near the death head.

- 如果 H 与其他模组冲突，请把 `HeldHeadReviveKey` 改成其他按键。
- If H conflicts with another mod, change `HeldHeadReviveKey` to another key.

- 请在游戏原生设置中开启“从背包取出后自动持有物品”。若该设置关闭，物品会在游戏的临时抓取计时结束后自动掉落；`ProtectHeldItems` 只处理受击和翻滚。
- Enable the game's native auto-hold-after-unequip setting. When it is disabled, the game releases an item after its temporary hold expires; `ProtectHeldItems` only handles impacts and tumbles.

- 请不要同时启用其他自动复活模组，以避免重复复活或重复扣费。
- Do not enable other automatic revive mods at the same time, to avoid duplicate revives or charges.

## 开发者说明
## For Developers

本节仅面向维护或修改模组的开发者；普通玩家不需要阅读或执行以下内容。
This section is only for developers maintaining or changing the mod; regular players do not need to read or run the items below.

- 项目使用 `netstandard2.1`，并直接引用本机 R.E.P.O. 与 BepInEx 的程序集。
- The project targets `netstandard2.1` and directly references the local R.E.P.O. and BepInEx assemblies.

- 使用以下命令构建：
- Build with:

```bash
dotnet build -c Release
```

- 构建成功后，DLL 会自动复制到当前 r2modman 配置的 BetterReviveExperience 插件目录。
- After a successful build, the DLL is automatically copied to the BetterReviveExperience plugin folder in the active r2modman profile.

- 游戏更新后，请先检查编译结果和游戏日志，再发布新版本。
- After a game update, check the build result and game log before publishing a new version.
