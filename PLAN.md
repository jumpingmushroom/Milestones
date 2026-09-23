# Milestones — Technical Plan

**Idea:** replace the vanilla achievement details panel's `??? / ???` with real progress: a bar,
`current / total` and a percentage per objective, plus an overall bar per achievement. Let the
player pin up to 3 achievements to a small HUD tracker that updates live as stats change, and
show a short toast when progress lands.

**Target build:** Valheim 1.0.15, network version 40 (`Version.c_networkVersion = 40u`). Findings
below come from `assembly_valheim.dll` (the copy in `../Tally/lib`, dated 2026-09-22) decompiled
with `ilspycmd`. Before building, `lib/` gets a fresh copy pulled from the rig's
`valheim_Data/Managed`, per the sibling repos' rule.

**Scope:** client-side only, no RPCs, no server component. Thunderstore namespace
`Jumpingmushroom`, package `Milestones`, GUID `com.jumpingmushroom.milestones`. BepInEx only, no
Jotunn. The repo layout, build scripts and publicizer setup are the same as CoolCount's.

**Decisions agreed (2026-09-23 design review):**
- Toasts on completed objectives *and* 25/50/75 % thresholds for pinned achievements;
  thresholds only for unpinned ones.
- Unmet objectives show their full name by default. `HideUnmetNames` is an opt-in.
- Large achievements in the tracker show the overall bar plus the 4 unfinished objectives
  closest to done, then "+N more".
- Completed pins auto-unpin about 10 s after the unlock popup.
- The tracker defaults to top-right, under the status-effect icons, which sit beside the
  minimap. Its position is configurable in ConfigurationManager.
- The details panel is populated by our own code (§2.2), not patched row by row after vanilla.

---

## 1. How vanilla achievements work

### 1.1 Data model (`Achievement : ScriptableObject`)

An achievement is unlocked when **all** of its trigger lists are satisfied
(`Achievement.CheckUnlocked`). The trigger lists are:

| List | Type | Reads from `PlayerProfile.m_playerStats[(int)m_difficultyRequirement]` |
|---|---|---|
| `m_statTrigger` | `PlayerStatRequirement {m_stat: PlayerStatType, m_amountAboveEquals}` | `.m_stats` |
| `m_enemyStatsTriggers` | `EnemyStatRequirement {m_stat, m_amount, m_modifier: KillModifiers, m_operator}` | `.m_enemyStats[(int)m_modifier]` |
| `m_itemPickupTriggers` | `DictStatRequirement {m_stat, m_amount, m_operator}` | `.m_itemPickupStats` |
| `m_itemCraftTriggers` | 〃 | `.m_itemCraftStats` |
| `m_foodEatenTriggers` | 〃 | `.m_foodEatenStats` |
| `m_pickableTriggers` | 〃 | `.m_pickableStats` |
| `m_piecePlacedTriggers` | 〃 | `.m_piecesPlacedStats` |
| `m_knownWorldTriggers` / `m_knownWorldKeysTriggers` / `m_knownCommandsTriggers` | 〃 | `.m_knownWorlds` / `.m_knownWorldKeys` / `.m_knownCommands` |
| `m_otherAchievementTriggers` | `List<Achievement>` | the other achievement's `m_unlocked` |

- `RequirementOperator` is `AboveEquals | BelowEquals | Equals | NotEquals`. Only `AboveEquals`
  has a meaningful "progress toward"; the other three are shown as a met / not-met row (bar 0 %
  or 100 %). A missing key counts as **not met** for every operator, as in vanilla (so
  `BelowEquals` on a stat never recorded is *not* met).
- Every lookup uses the achievement's own `m_difficultyRequirement` slot (enum
  `RawStats=0, Any=1, … Hardcore=9`). The mod must read the same slot, not `GetStat()`, which
  switches slots depending on cheat state.
- **Dynamic achievements** (`m_isDynamicallyPopulated`): `AllWeaponCraft`, `AllFoodCooked`,
  `AllItemCraft`, `AllFoodEaten`, `AllBuildPieces`, `FindAllTrophies`, `KillAllCreatures`,
  `KillAllCreaturesHard`. `Achievements.Initialize` fills their trigger lists at startup with one
  `m_amount = 1` entry per item, piece or creature, so they can have **dozens to hundreds of
  objectives**. That drives the HUD and overall-bar design below.
- **Lenient build achievements** (`m_lenientBuildAchievement`) do not use `CheckUnlocked`'s
  "every row met" rule. `Piece.CheckLenientBuildAchUnlocked` unlocks them when
  `Σ min(stat_i, 1.15 × req_i) > Σ req_i`. Overdoing one category can make up for another. Their
  overall bar must use that formula, or it will read "70 %" on an achievement that just unlocked.
  That live check counts pieces standing now (`Piece.m_tagStats`), while the recorded stat is a
  high-water mark that never decreases, so after demolishing pieces the bar can read higher than
  the live unlock check.
- `m_otherAchievementTriggers` (meta achievements) are **not shown at all** by vanilla. We add
  one row per referenced achievement (done / not done).

### 1.2 The details panel (`AchievementsGui`)

- `InventoryGui.UpdateAchievementsList` builds the grid. Each tile's button calls
  `AchievementsGui.OnOpenAchievementDetails(achievement, clickable)`. Secret achievements that
  are still locked are not clickable.
- `OnOpenAchievementDetails` instantiates `m_achievementDetailsElementPrefab`
  (`AchievementDetailUnlockCondition`: two TMP labels, `StatName` and `Progress`) under
  `m_achievementDetailsListRoot`. Stat rows are created directly. Every other list goes through
  coroutines that yield every `m_detailStatsPerFrame` (10) rows.
- `CreateStatRow(statKey, current, total)` is where the `???` comes from: if
  `current < total` it writes `???` and `??? / ???` in grey, otherwise the localized name and the
  numbers in green. It gets no operator and no source list.
- `CloseDetails()` (on close and on `OnEnable`) destroys every row.

### 1.3 Where stats change

Every write goes through seven `PlayerProfile` methods: `IncrementStat`, `IncrementStatEnemy`,
`IncrementStatItemPickup`, `IncrementStatItemCraft`, `IncrementStatPickable`,
`IncrementStatFoodEaten`, `IncrementStatBuildPiecePlaced`, plus `SetStat`. Unlocks go through
`Achievements.AchievementEvent`. Postfixing those gives exact, cheap "something changed" events
with no polling.

### 1.4 The cheat / modded gate (important)

Slot 0 (`RawStats`) always records. **Slots ≥ 1, which achievements read, only record when
`Achievements.CanGetAchievements()` is true.** That is false if the profile used cheats, the
inventory holds a cheated item, the world has cheat modifiers, **or `Game.isModded` is true**
(`Achievements.IsCheatedAtAll`). So:

- Milestones must **never set `Game.isModded`**. It only reads.
- If another mod sets it, or the session is cheated, the bars freeze, because the game has stopped
  counting. The tracker shows one dim line, "Achievements paused: cheats / modded / world
  modifiers", instead of looking broken. The reason is worked out the same way
  `InventoryGui.UpdateAchievementsList` picks its `m_achievementsCheatedText`.

## 2. Design

### 2.1 Core model: `Core/Progress.cs`

The one place that turns an `Achievement` into numbers, mirroring `CheckUnlocked` exactly:

```csharp
record Objective(string Label, float Current, float Target, bool Met, bool HasProgress);
record AchievementProgress(Achievement A, IReadOnlyList<Objective> Objectives,
                           int MetCount, float Overall /*0..1*/, bool Unlocked);
```

- `Current` is clamped for display only. The display reads `current / total`, and a
  `NotEquals`/`Equals`/`BelowEquals` row reads "met" / "not met".
- Labels: `PlayerStatType` → `$stat_<Name>` (as vanilla does), otherwise the stored token
  (`$item_…`, `$enemy_…`, `$piece_…`) through `Localization.instance.Localize`. If a token comes
  back unchanged, fall back to splitting the CamelCase name. Enemy rows with a modifier get a
  suffix ("(unarmed)", "(magic)", …).
- **Overall** (config `OverallMode`, default `Auto`):
  - `Average`: mean of per-objective `min(current/target, 1)`.
  - `Count`: `MetCount / N`, shown as "37 / 212".
  - `Auto`: `Count` when every objective has target 1 (all the dynamic "do each thing once"
    achievements), otherwise `Average`. Lenient build achievements always use the 1.15 formula
    from 1.1.
  - Unlocked → 100 % regardless (stats can be reset by `ResetAchievement` and the like).
- Computing one achievement costs N dictionary lookups. For the biggest (`AllBuildPieces`,
  a few hundred) that is nothing, as long as it runs only when dirty.

### 2.2 Achievements screen: `Patches/DetailsPatches.cs`, `UI/ProgressRow.cs`

- **Prefix `AchievementsGui.OnOpenAchievementDetails`** and take over population when the
  achievement is not a locked secret (locked secrets fall through to vanilla, which still shows
  `???`). Build rows from the `Progress` model using the **same prefab, parent and
  10-per-frame batching** as vanilla, so layout and scrolling are unchanged. Wrapped in
  try/catch; on any exception, log once and let vanilla run.
  - Rejected alternative: postfixing `CreateStatRow` and rewriting the last child. It has no
    operator or source info, can't add meta rows, and depends on coroutine ordering.
- **Row sort:** incomplete first, closest to done at the top, completed rows last (config
  `SortObjectives`, default on; off keeps vanilla order).
- **`UI/ProgressRow`**: a component added to each row instance. Children: a background `Image` and
  a fill `Image` (`Type.Filled`, horizontal) built on a 4×4 white runtime sprite (as in
  CoolCount), placed under the two labels. `Progress` text becomes `12 / 30  40%`.
  Colors: grey `#7a7a7a` not started (current = 0), amber `#e0a030` in progress, green
  `#5ac85a` done. All three can be set in config. `raycastTarget = false` throughout.
- **Overall header:** one extra row at the top: the achievement name, the overall bar and
  "73 %" or "37 / 212 objectives".
- **Spoilers:** config `HideUnmetNames` (default **off**). When on, unmet objectives keep
  vanilla's `???` name but still get a bar and numbers. Useful for KillAllCreatures /
  FindAllTrophies on a first playthrough.
- **Live while open:** if the panel is open and a stat event fires, refresh the text and fills of
  existing rows in place. There is no rebuild, so the scroll position holds.

### 2.3 Pin toggle and storage: `UI/PinButton.cs`, `Core/Pins.cs`

- One button created lazily inside `m_achievementDetails`, cloned from an existing button in that
  panel (the close button) so it inherits the font and sprite. The label reads **Pin (1/3)** or
  **Unpin**. At 3 pins it is disabled, with the tooltip "Unpin one first". Gamepad: bound to
  `JoyButtonX`; `JoyButtonY` is already "close inventory" in `InventoryGui.Update` while the
  achievements panel is open, so it would fight vanilla.
- A small pin icon on the grid tiles of pinned achievements (a postfix on
  `InventoryGui.UpdateAchievementsList`), so they are easy to find again.
- **Storage, per character:** `Player.m_customData["milestones.pins"] = "id1,id2,id3"`
  (`Achievement.m_id`). It is saved inside the character file by vanilla (`Player.Save`),
  unknown keys are ignored on load, and it survives uninstall harmlessly. It is per character,
  not per world, which matches achievements being profile-wide. IDs that no longer resolve are
  dropped on load. Config `AutoUnpinCompleted` (default **on**) unpins an achievement 10 s after
  its vanilla unlock popup. With it off, the achievement stays pinned showing "Done ✓" until you
  unpin it.

### 2.4 HUD tracker: `UI/TrackerHud.cs`

- Built under `Hud.instance.m_rootObject` so it hides with the HUD (F3 / photo mode). The TMP
  font and material are copied from an existing HUD label, as CoolCount does from `m_amount`.
  It has a dark translucent panel, and for each pinned achievement: its icon (`m_iconLocked`), its
  name, the overall bar, then one compact bar per objective.
- **Large achievements:** show at most `MaxRowsPerAchievement` (default 4) *incomplete*
  objectives, closest to done first, then a "+208 more" line. The overall bar still covers
  everything. Small achievements (≤ 4 objectives) show all rows including completed ones.
- Position: config `Anchor` (`UnderStatusEffects` default, `TopLeft`, `TopRight`,
  `LeftMiddle`, `RightMiddle`, `BottomLeft`, `BottomRight`), `Offset` (x, y), `Scale` and
  `Opacity`, all editable live in ConfigurationManager.
  The status-effect icons are **not** top-left. `Hud.UpdateStatusEffects` places each one at
  `x = -4 - col × m_statusEffectSpacing (55)`, `y = -row × 55`, with `m_effectsPerRow = 7`, so
  they run leftward from `m_statusEffectListRoot`, beside the minimap at the top right. The
  tracker parents its right edge to that root's world position and sits
  `rows × 55 + 8` px below it, so a second row of meads pushes it down instead of overlapping
  it. Hidden while the inventory or achievements panel is open (config), and when
  nothing is pinned.
- **Updates:** the §1.3 postfixes set a dirty flag. The tracker recomputes pinned achievements
  at most every 0.25 s while dirty and never otherwise. No per-frame work beyond checking one bool.
  Bars ease to their new width over ~0.3 s so a berry pick visibly nudges the bar.

### 2.5 Toasts: `UI/Toasts.cs`

- **Pinned** achievements toast on every completed objective ("Milestones: Serpent stew ✓ ·
  Eat everything 37 / 58") and when overall progress crosses a threshold (25 / 50 / 75 %).
- **Unpinned** achievements toast only on thresholds. This is checked for every achievement on
  each stat event, which is cheap at event rate because only achievements whose inputs changed
  are recomputed.
- No toast on the unlock itself: vanilla's `AchievementUnlockPopup` already covers that.
- Previous values are snapshotted in memory at login, so loading a character never replays old
  toasts. If one stat event crosses several thresholds or completes several objectives, they
  are merged into a single toast.
- Rendering: `MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, text)`, the
  game's own pickup-message feed at the top left, away from the tracker. It is vanilla-styled
  and queues itself. Config `ToastPosition` can switch to `Center`.

### 2.6 Config (BepInEx, `com.jumpingmushroom.milestones.cfg`)

General: `Enabled`, `OverallMode`, `SortObjectives`, `HideUnmetNames`.
Tracker: `ShowTracker`, `Anchor`, `Offset`, `Scale`, `Opacity`, `MaxRowsPerAchievement`,
`HideWhenInventoryOpen`, `AutoUnpinCompleted`.
Toasts: `PinnedToasts` (`ObjectivesAndThresholds` default / `ThresholdsOnly` / `Off`),
`UnpinnedToasts` (`ThresholdsOnly` default / `Off`), `Thresholds` (default `25,50,75`),
`ToastPosition` (`TopLeft` default / `Center`).
Colors: `ColorNotStarted`, `ColorInProgress`, `ColorDone`.
`ConfigurationManagerAttributes` is declared locally, as in CoolCount.

### 2.7 Console

`milestones` lists pins plus a one-line progress summary for every achievement. `milestones
<id|name>` gives a per-objective breakdown. `milestones pin|unpin <id>`. `milestones ui` dumps
the details-panel hierarchy, for tuning the layout. All output is mirrored to the BepInEx log, so
`build/logs.sh` can read it without a screenshot.

## 3. Project layout

```
Milestones/
  PLAN.md  README.md  CHANGELOG.md  CLAUDE.md  LICENSE  Directory.Build.props  Milestones.sln
  thunderstore.toml  thunderstore/{manifest.json, README.md, icon.png}
  build/{deploy.sh, package.sh, publish.sh, logs.sh, shot.sh, crop.sh, make_icon.py}
  lib/            (gitignored; pulled from the rig)
  src/Milestones/
    Plugin.cs  PluginConfig.cs  ConfigurationManagerAttributes.cs  Milestones.csproj
    Core/Model/{Objective.cs, ProgressCalc.cs, PinList.cs, ToastDiff.cs, Labels.cs}
                  (pure C#: no UnityEngine, no game types; unit-tested)
    Core/{AchievementReader.cs, ProgressCache.cs, Pins.cs, StatEvents.cs, CheatState.cs,
          ConsoleCommands.cs}
    Patches/{DetailsPatches.cs, StatPatches.cs, ListPatches.cs}
  tests/Milestones.Tests/   (net8.0 xUnit; compiles Core/Model/*.cs by link)
    UI/{ProgressRow.cs, PinButton.cs, TrackerHud.cs, Toasts.cs, UiUtil.cs}
```

## 4. Milestones for the mod itself

1. Scaffold, fresh `lib/`, `Progress` model, and the `milestones` console dump. Check numbers
   against the vanilla `achievements` command on the rig.
2. Details panel: bars, overall header, sort, live refresh.
3. Pins, pin button, and the `m_customData` round-trip (log out and in, switch character).
4. HUD tracker with live updates (pick a berry, watch it move).
5. Toasts, config polish, README with screenshots, package 0.1.0.

**Testing:** the pure model in `Core/Model/` (progress math, operators, lenient formula, pin
list, toast diffing, label fallback) has no Unity or game dependency. It is unit-tested by a
net8.0 xUnit project that compiles those files by link, and runs on the build box with
`dotnet test`. Mono isn't installed there, so the net472 plugin itself can't be run in tests.
Everything that touches Unity or the game is checked on the rig: the numbers against vanilla's
`achievements` console command, the UI with `build/shot.sh` captures, and live behavior through
the console dumps in the BepInEx log (`build/logs.sh`).

## 5. Open questions / to verify on the rig

- Whether toasts get lost in the top-left pickup feed during heavy looting (§2.5). If so,
  `ToastPosition = Center` becomes the default.
- Whether anything in the Mods profile sets `Game.isModded`. The BepInEx log prints
  `isModded: …` at `Game` start (Game.cs:274). If it is true, achievement stats are not
  recording at all, and no mod can show progress that the game isn't counting.
- The detail row prefab's height and layout group: whether a bar fits under the labels or the
  row needs a `LayoutElement.preferredHeight` bump. `milestones ui` will tell.
- Which gamepad button to use for Pin in the details panel without clashing with vanilla (B closes).
- How the vanilla `$stat_*` tokens cover all ~205 `PlayerStatType` values. Missing ones fall
  back to CamelCase splitting.
- Compatibility with mods that restyle the inventory or achievements panel. Everything is
  looked up through `AchievementsGui` fields, not hierarchy paths, which should keep it robust.
