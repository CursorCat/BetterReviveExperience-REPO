# BetterReviveExperience

> **⚠️ 兼容性警告 / Compatibility Warning**
>
> 请勿同时启用其他提供**自动复活、死亡背包保留、手持物品防掉落或自动回收**功能的模组。对于 GameTools 等多功能模组，请关闭其中与 BRE 重叠的选项。
>
> Do not enable other mods that provide **automatic revives, keep-inventory-on-death, held-item drop protection, or automatic item recovery**. For multi-purpose mods such as GameTools, disable the options that overlap with BRE.

[中文](#中文) | [English](#english)

## 中文

BetterReviveExperience 是一款仅需房主安装的 R.E.P.O. 复活与物品保护模组，加入房间的其他玩家无需安装。

### 安装

- 使用 r2modman 或 Thunderstore Mod Manager 安装。
- 依赖 `BepInExPack` 和 `REPOConfig`。
- 在 `Mods → BetterReviveExperience` 中修改设置，修改后建议重启游戏。

### 功能

- 死亡后保留原版背包栏物品。
- 可收纳物品（Phase Bridge、无人机、武器等）因受击或翻滚脱手时自动回到原版背包；死亡时也会优先回收。
- 手持物品时切换到已占用的背包格，会交换物品而不是将手中物品扔到地上。
- 支持提取点、卡车、推车、商店和房主持死亡玩家的头按键复活。
- 可设置复活条件、团队费用、复活血量和快捷键。

### 设置

| 设置 | 默认值 | 作用 |
|---|---:|---|
| `KeepItemsOnDeath` | 开启 | 死亡后保留背包栏物品 |
| `ProtectHeldItems` | 开启 | 受击或翻滚时回收可收纳的手持物品 |
| `SwapHeldItemOnOccupiedSlot` | 开启 | 切换到已占用格时交换物品 |
| `ReturnHeldItemOnDeath` | 开启 | 死亡时回收手持的可收纳物品 |
| `Mode` | `ExtractionOrTruck` | 设置提取点和卡车复活规则 |
| `Cost` | `0` | 每次复活扣除的团队货币，范围 `0–100000`，步进 `1000` |
| `HealthPercent` | `25` | 复活后的血量百分比 |
| `EnableHeldHeadRevive` | 开启 | 房主持死亡玩家的头按键复活 |
| `HeldHeadReviveKey` | `H` | 可选 `H`、`R`、`Y` 或 `F` |
| `EnableCartRevive` | 开启 | 死亡玩家的头进入推车后复活 |
| `EnableShopRevive` | 开启 | 在商店死亡后复活 |
| `DebugLogging` | 开启 | 输出详细诊断日志 |

`Mode` 可选：

- `Disabled`：关闭提取点和卡车自动复活。
- `ExtractionMachineActivated`：死亡玩家的头进入提取点，并在机器激活后复活。
- `ExtractionOrTruck`：死亡玩家的头进入提取点或卡车区域后立即复活。

### 注意

- 请在游戏原生设置中开启 `Item Unequip Auto Hold`，否则从背包取出的物品会在短暂计时后掉落。
- 扩展背包格不受支持；使用扩展背包模组时，请在原版前三格中至少留一个空格。
- 原版前三格全满时，死亡时手持的物品会放到死亡玩家的头附近。
- 团队货币不足时不会复活，也不会扣费。
- 团灭、开新局或重置存档时不保证保留物品。

### 开发者

项目目标框架为 `netstandard2.1`。构建命令：

```bash
dotnet build -c Release
```

游戏更新后，请重新检查 Harmony 目标、游戏程序集和 BepInEx 日志。

## English

BetterReviveExperience is a host-only revive and item-protection mod for R.E.P.O. Other players joining the lobby do not need to install it.

### Installation

- Install with r2modman or Thunderstore Mod Manager.
- Requires `BepInExPack` and `REPOConfig`.
- Change settings under `Mods → BetterReviveExperience`; restarting the game afterward is recommended.

### Features

- Keep vanilla inventory-slot items after death.
- Return storable items such as Phase Bridge, drones, and weapons to vanilla inventory after an impact or tumble, and recover them on death.
- Swap the held item into an occupied inventory slot instead of dropping it.
- Revive through extraction points, the truck, carts, the shop, or the host's held-head hotkey.
- Configure revive conditions, team cost, revive health, and hotkey.

### Settings

| Setting | Default | Purpose |
|---|---:|---|
| `KeepItemsOnDeath` | On | Keep inventory-slot items after death |
| `ProtectHeldItems` | On | Recover storable held items after an impact or tumble |
| `SwapHeldItemOnOccupiedSlot` | On | Swap items when selecting an occupied slot |
| `ReturnHeldItemOnDeath` | On | Recover a storable item held at death |
| `Mode` | `ExtractionOrTruck` | Set extraction-point and truck revive rules |
| `Cost` | `0` | Team currency per revive, `0–100000` in steps of `1000` |
| `HealthPercent` | `25` | Health percentage after revive |
| `EnableHeldHeadRevive` | On | Let the host revive the held death head |
| `HeldHeadReviveKey` | `H` | Choose `H`, `R`, `Y`, or `F` |
| `EnableCartRevive` | On | Revive when a death head enters a cart |
| `EnableShopRevive` | On | Revive after dying in the shop |
| `DebugLogging` | On | Write detailed diagnostic logs |

`Mode` options:

- `Disabled`: disable automatic extraction-point and truck revives.
- `ExtractionMachineActivated`: revive after the death head enters an extraction point and the machine is activated.
- `ExtractionOrTruck`: revive immediately when the death head enters an extraction point or truck area.

### Notes

- Enable the native `Item Unequip Auto Hold` game setting, or inventory items will drop after the temporary grab timer expires.
- Expanded inventory slots are not supported. Keep at least one of the first three vanilla slots free when using a bag-expansion mod.
- If all three vanilla slots are full, the item held at death is placed near the death head.
- A revive is not performed or charged when team currency is insufficient.
- Items are not guaranteed to persist through a team wipe, new run, or save reset.

### For Developers

The project targets `netstandard2.1`. Build with:

```bash
dotnet build -c Release
```

After a game update, recheck the Harmony targets, game assemblies, and BepInEx log.
