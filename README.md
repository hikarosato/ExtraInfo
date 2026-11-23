# ExtraInfo

A **Vintage Story** client/server mod that adds the information the game does not show you: how long a process will still take, how far along it is, what an animal eats, where a translocator leads, and much more. All of it appears directly in the block/item tooltips and in the handbook.

This version is a rework focused on **accurate numbers** and a **clean, compact tooltip**.

---

## Highlights

- **Accurate timers.** Several calculations were rewritten to mirror the game's own logic (firepit, coke oven, coal piles, beehive kiln, cementation furnace, farmland, pumpkin vines, animal pregnancy, and more). See [What was fixed](#what-was-fixed).
- **Compact, readable tooltips.** Each timer is one line, e.g. `Will finish in: 42% 3 hours, 12 minutes`, instead of a multi-line block with an ASCII bar and a separate real-time line.
- **Whole-growth projections.** Farmland, saplings and seeds now show the time to the *next* stage **and** the time until fully grown.
- **New: firepit heat forecast.** Shows the item's temperature against the temperature it needs, warns when the fuel is too weak or when neither the fuel nor the leftover heat is enough to cook everything, and the cooking times now include the time needed to heat the item up and the cooking done on leftover heat after the fuel runs out.
- **New: fruit tree cuttings.** Shows the time until the cutting roots (or fails).
- **Hardened against crashes.** Null checks for modded entities, disabled drops and missing dialogs.
- **Config GUI** through [ConfigLib](https://mods.vintagestory.at/configlib), if installed.

---

## Features

| Area | What you see |
|---|---|
| **Processing blocks** | Progress and time left for firepit (cooking and fuel), bloomery, boiler, coke oven, cementation furnace, charcoal pit, pit kiln, beehive kiln, quern, skep |
| **Fuel** | Burn time for burning coal piles and burnable piles |
| **Farming and plants** | Farmland, berry bush cuttings, fruit tree cuttings, pumpkin vines, saplings and seeds (next stage and total growth) |
| **Animals** | Pregnancy, mating cooldown, lactation / refill timers |
| **Block info** | Block breaking time, block transitions (spoilage etc.), anvil workable temperature, mechanical block info, pile totals, translocator destination, bomb stats |
| **Metal** | Metal units per nugget, ore and stack |
| **Clay forming** | Placement preview |
| **Handbook** | Creature diet, items eatable by creatures, entity drops, panning drops, pit kiln info, trader goods, trough feed options, workable temperature; hotkey to open the handbook page of the creature you look at |

---

## What was fixed

Every item below is a correctness or stability fix compared to the previous version.

**Wrong or misleading timers**

- **Beehive kiln** used the kiln's own wear counter (it resets only after 168 h) instead of the real progress. It now reads each item's own `hoursHeatReceived`, accounts for the heat-up time, and reports the slowest item in the batch. A "paused" header is shown when the kiln is not heating.
- **Coal pile / coke oven / burnable pile** were calculated from visual *layers*, so the time was off whenever the stack size was odd, and the pile time could jump backwards each time an item was consumed. They now use the real stack size and the game's burn start time.
- **Firepit** timers were in "counter units", not seconds, and assumed the item was already hot. Times are now calculated by simulating the firepit the way the game does (furnace warm-up, item heating and cooling, cooking speed `clamp(temp / meltingPoint, 1, 30)`, queued fuel, `SmeltedRatio`), so they include the heat-up time and the cooking that continues on leftover heat after the fuel runs out. The reduced fuel consumption of an idle, fully heated firepit is also taken into account, and total fuel includes the queued fuel's own burn duration, which may differ from the burning item.
- **Cementation furnace** used 159.2 h in one place and 160 in another; both now use the game's 160 h, so the bar and the remaining hours agree.
- **Pumpkin vine**: the first stage 3 correctly shows 18 h instead of 12 h; withered vines no longer show a pointless "next stage" timer; values are clamped.
- **Animal pregnancy**: pregnancy and cooldown now respect values overridden by other mods or by the entity JSON, with a fallback to vanilla values per species.
- **Block transitions**: the client copy of a block entity does not tick, so the remaining time is now corrected by the time elapsed since the last accurate value, instead of calling `CheckTransition` on the client.
- **Tree stats**: the seed stage reads `growthDays` (what the game actually uses) instead of `sproutDays`.

**Crashes and logic bugs**

- Handbook **panning drops**: no more index-out-of-range when all drops of a group are disabled by another mod.
- Handbook **entity drops**: null-safe `Drops`.
- **Open handbook page for entity**: the dialog is now opened if it was closed, and the vanilla hotkey no longer immediately closes it again. Creatures without a page fall back to vanilla behavior.
- **Clayforming preview**: all cached state is reset, so the preview is rebuilt correctly after you switch to another clay form.
- **Mod lifecycle**: the Harmony instance is created once and unpatched only if it was actually created (previously `new Harmony(...)` was created on every access).

---

## Old version vs. this version

| | Previous version | This version |
|---|---|---|
| Tooltip layout | Header line, ASCII bar line, in-game time line, real-time line, blank lines | One line per timer, no stray blank lines |
| Progress display | Configurable ASCII bar (`[####____] 42%`) | Coloured percentage (`42%`) |
| Time format | Seconds-level, long (`3 hours, 12 minutes, 5 seconds`) | Compact: days only from 2 days; `1 day, 3 hours`; `3 hours, 12 minutes`; `N minutes` |
| Real-time (IRL) display | Yes | Removed |
| Config options | 11 settings for time and bar | 3 settings (`ShowInGameTimeInfo`, `ShowProgressPercent`, ...) |
| Config GUI | Included an animated bar preview and symbol pickers | Slimmer GUI, ~170 fewer lines |
| Firepit / coke oven / coal pile / beehive kiln | Approximate or incorrect in some states | Mirrors the game's logic |
| Total growth time (farmland, trees) | Next stage only | Next stage **and** whole growth |
| Fruit tree cuttings | Not supported | Supported |
| Robustness | Possible NRE / index errors | Null guards and clamped values |

## Configuration

Config file: `ModConfig/ExtraInfo.json`. If **ConfigLib** is installed, the same options are available in its in-game GUI.

| Option | Default | Description |
|---|---|---|
| `ShowInGameTimeInfo` | `true` | Show the remaining in-game time |
| `ShowProgressPercent` | `false` | Show the progress percentage |
| `ShowHeatForecast` | `true` | Show the item's temperature and fuel warnings in the firepit tooltip (needs `ShowFirepitProgress`) |
| `ShowBlockBreakingTime` | `true` | Show block breaking progress |
| `Show...Progress` | `true` | Per-system toggles (fuel, tree, quern, beehive kiln, ...) |

Old config files remain valid; removed options are simply ignored.

---

## Localization

This version introduces the keys `extrainfo:Minutes`, `extrainfo:Color.Cyan`, `extrainfo:TotalGrowthIn`, `extrainfo:TimeUntilRooting`, `extrainfo:beehivekiln-paused-left`, `extrainfo:firepit-forecast-temp`, `extrainfo:firepit-forecast-unreachable`, `extrainfo:firepit-forecast-fuel-short`, `extrainfo:firepit-forecast-fuel-short`, `extrainfo:firepit-forecast-residual` and `extrainfo:Config.Setting.ShowHeatForecast`.
