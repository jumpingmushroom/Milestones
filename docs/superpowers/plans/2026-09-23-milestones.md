# Milestones Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A client-side BepInEx mod for Valheim 1.0.15 that shows real progress bars in the achievement details panel, a pinned-achievement HUD tracker that updates live, and progress toasts.

**Architecture:** A pure C# model (`Core/Model`, no Unity or game types, unit-tested on net8.0) turns trigger inputs into progress, pin lists and toast text. A thin game adapter reads `Achievement` and `PlayerProfile` into that model, caches the results, and recomputes only achievements whose inputs changed. Harmony postfixes on the profile's stat methods feed that cache. The UI (details panel, pin button, tracker, toasts) subscribes to cache change events.

**Tech Stack:** C# (LangVersion latest) targeting net472, BepInEx 5 + Harmony, Unity UI + TextMeshPro, `BepInEx.AssemblyPublicizer.MSBuild` 0.4.3, xUnit on net8.0 for the model.

**Spec:** `PLAN.md` (repo root). Read it first. It records the decompiled game behavior every task depends on (§1) and the agreed design (§2).

## Global Constraints

- Game: Valheim 1.0.15, network version 40. Client-side only: no RPCs, no ZDO writes, no server component.
- Dependencies: BepInEx only (`denikson-BepInExPack_Valheim-5.4.2350`). **No Jotunn.** `ConfigurationManagerAttributes` is declared locally.
- GUID `com.jumpingmushroom.milestones`, name `Milestones`, version `0.1.0`. Thunderstore namespace `Jumpingmushroom`.
- Version appears in three places that must agree: `PluginVersion` in `src/Milestones/Plugin.cs`, `<Version>` in the csproj, `version_number` in `thunderstore/manifest.json`.
- **Never set `Game.isModded`.** Milestones only reads game state.
- Stats are read from `profile.m_playerStats[(int)achievement.m_difficultyRequirement]`, never through `PlayerProfile.GetStat()`.
- Pins: at most 3, saved in `Player.m_customData["milestones.pins"]` as comma-separated `Achievement.m_id`.
- Bar colors: not started `#7a7a7a`, in progress `#e0a030`, done `#5ac85a`. All three are configurable.
- Toasts: pinned achievements get objective completions and 25/50/75 % thresholds. Unpinned achievements get thresholds only. No toast on unlock.
- Tracker default anchor: under the status-effect icons (top right, beside the minimap).
- Commits: author is the user only. **No `Co-Authored-By` or any AI attribution** in commit messages.
- Build box: `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` is required for every `dotnet` call. Rig: `user@rig`, login shell fish (wrap commands in `bash -c '...'`). Profile `~/.config/r2modmanPlus-local/Valheim/profiles/Mods`.
- Never overwrite the plugin DLL in place while the game runs. `build/deploy.sh` swaps it atomically, and the new DLL loads on the next launch.

Every `dotnet` command below assumes this environment:

```bash
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"
```

## File map

```
Milestones/
  .gitignore  PLAN.md  CLAUDE.md  README.md  CHANGELOG.md  LICENSE
  Directory.Build.props  Milestones.sln
  thunderstore/{manifest.json, README.md, icon.png}
  build/{deploy.sh, package.sh, publish.sh, logs.sh, shot.sh, crop.sh, make_icon.py}
  lib/                                    gitignored, pulled from the rig
  src/Milestones/
    Milestones.csproj
    Plugin.cs                             entry point, Harmony, Update → Runtime.Tick
    PluginConfig.cs                       every config entry
    ConfigurationManagerAttributes.cs
    Core/Model/                           PURE C#: no UnityEngine, no game types
      Objective.cs                        Op, Source, ObjectiveInput, Objective, AchievementProgress, enums
      ProgressCalc.cs                     Evaluate, Compute, SameValues
      ObjectiveOrder.cs                   details-panel sort, tracker row selection
      Labels.cs                           Humanize, amounts, percent, progress text
      PinList.cs                          parse/serialize/add/remove, max 3
      ToastDiff.cs                        Snapshot, ToastRules, Diff
    Core/
      AchievementReader.cs                Achievement + profile → ObjectiveInput list; lookup; grid order
      StatEvents.cs                       dirty kinds + dirty player stats
      ProgressCache.cs                    cached AchievementProgress, ProcessDirty, Changed event
      CheatState.cs                       why achievements are paused, or null
      Pins.cs                             PinList ↔ Player.m_customData, auto-unpin
      Runtime.cs                          per-frame tick: player change, throttles, tracker
      ConsoleCommands.cs                  `milestones …`
    Patches/
      StatPatches.cs                      postfixes on PlayerProfile stat writers + AchievementEvent
      DetailsPatches.cs                   OnOpenAchievementDetails prefix, CloseDetails postfix
      ListPatches.cs                      UpdateAchievementsList postfix (pin marks)
    UI/
      UiUtil.cs                           white sprite, rect helpers, font, colors
      Bar.cs                              eased filled-image bar
      ProgressRow.cs                      one details-panel row + its bar
      DetailsPanel.cs                     builds and refreshes the details rows
      PinButton.cs                        Pin/Unpin button in the details panel
      ListMarks.cs                        pin marker on grid tiles
      TrackerHud.cs                       the HUD widget
      Toasts.cs                           ProgressCache.Changed → ToastDiff → MessageHud
  tests/Milestones.Tests/
    Milestones.Tests.csproj               net8.0 xUnit, compiles src/Milestones/Core/Model/*.cs by link
    ProgressCalcTests.cs  ObjectiveOrderTests.cs  LabelsTests.cs  PinListTests.cs  ToastDiffTests.cs
```

---

### Task 1: Scaffold, reference assemblies, empty plugin loads on the rig

**Files:**
- Create: `Directory.Build.props`, `LICENSE`, `CLAUDE.md`, `Milestones.sln`, `src/Milestones/Milestones.csproj`, `src/Milestones/Plugin.cs`, `src/Milestones/PluginConfig.cs`, `src/Milestones/ConfigurationManagerAttributes.cs`, `build/*.sh`, `build/make_icon.py`
- Create (gitignored): `lib/*.dll`

**Interfaces:**
- Produces: `MilestonesPlugin.Log` (`ManualLogSource`), `MilestonesPlugin.WarnOnce(string key, Exception e)`, and every `PluginConfig.*` entry listed in Step 5 (later tasks read them by these exact names). The enums `OverallMode`, `PinnedToastMode`, `UnpinnedToastMode` come from Task 2's `Milestones.Core.Model` namespace, so Task 1 declares them temporarily and Task 2 moves them (see Step 5 note).

- [ ] **Step 1: Copy the shared files from CoolCount and rename**

```bash
cd /workspace/gamemods/Valheim/Milestones
cp ../CoolCount/Directory.Build.props ../CoolCount/LICENSE .
mkdir -p build src/Milestones tests docs/images thunderstore
cp ../CoolCount/build/{deploy.sh,package.sh,publish.sh,logs.sh,shot.sh,crop.sh,make_icon.py} build/
sed -i 's/CoolCount/Milestones/g; s/coolcount/milestones/g' build/*.sh
cp ../CoolCount/src/CoolCount/ConfigurationManagerAttributes.cs src/Milestones/
grep -n "Milestones\|milestones" build/*.sh | head -40
```

Expected: every former CoolCount path now reads Milestones (for example `PROJ="$ROOT/src/Milestones/Milestones.csproj"` and `plugins/Milestones`). `make_icon.py` is still CoolCount's drawing, and Task 12 rewrites it.

- [ ] **Step 2: Pull fresh reference assemblies from the rig**

```bash
ssh user@rig "bash -c 'find / -name assembly_valheim.dll -path \"*valheim_Data/Managed*\" 2>/dev/null'"
```

Use the path it prints (the Steam install, not a sibling mod's copy) as `$MANAGED`, then:

```bash
MANAGED='<path printed above, without the file name>'
CORE=~/.config/r2modmanPlus-local/Valheim/profiles/Mods/BepInEx/core
mkdir -p lib
for f in assembly_valheim assembly_utils assembly_guiutils UnityEngine UnityEngine.CoreModule UnityEngine.UI UnityEngine.UIModule UnityEngine.TextRenderingModule UnityEngine.IMGUIModule UnityEngine.InputLegacyModule Unity.TextMeshPro; do
  scp -q "user@rig:$MANAGED/$f.dll" lib/
done
scp -q "user@rig:$CORE/BepInEx.dll" "user@rig:$CORE/0Harmony.dll" lib/
ls lib | wc -l
```

Expected: `13`.

- [ ] **Step 3: Write `src/Milestones/Milestones.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <AssemblyName>Milestones</AssemblyName>
    <RootNamespace>Milestones</RootNamespace>
    <Version>0.1.0</Version>
    <Description>Real progress bars for Valheim's achievements, a pinned tracker and progress toasts.</Description>
  </PropertyGroup>

  <ItemGroup>
    <!-- .NET Framework 4.7.2 reference assemblies; required to build net472 off-Windows. -->
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" PrivateAssets="all" />
    <PackageReference Include="BepInEx.AssemblyPublicizer.MSBuild" Version="0.4.3" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <!-- Publicized at build time only; the shipped DLL binds to the real members
         and runs against an unmodified game install. -->
    <Reference Include="assembly_valheim"  HintPath="$(ValheimManaged)/assembly_valheim.dll"  Publicize="true" Private="false" />
    <Reference Include="assembly_utils"    HintPath="$(ValheimManaged)/assembly_utils.dll"    Private="false" />
    <Reference Include="assembly_guiutils" HintPath="$(ValheimManaged)/assembly_guiutils.dll" Private="false" />

    <Reference Include="UnityEngine"                     HintPath="$(ValheimManaged)/UnityEngine.dll" Private="false" />
    <Reference Include="UnityEngine.CoreModule"          HintPath="$(ValheimManaged)/UnityEngine.CoreModule.dll" Private="false" />
    <Reference Include="UnityEngine.UI"                  HintPath="$(ValheimManaged)/UnityEngine.UI.dll" Private="false" />
    <Reference Include="UnityEngine.UIModule"            HintPath="$(ValheimManaged)/UnityEngine.UIModule.dll" Private="false" />
    <Reference Include="UnityEngine.TextRenderingModule" HintPath="$(ValheimManaged)/UnityEngine.TextRenderingModule.dll" Private="false" />
    <Reference Include="UnityEngine.IMGUIModule"         HintPath="$(ValheimManaged)/UnityEngine.IMGUIModule.dll" Private="false" />
    <Reference Include="UnityEngine.InputLegacyModule"   HintPath="$(ValheimManaged)/UnityEngine.InputLegacyModule.dll" Private="false" />
    <Reference Include="Unity.TextMeshPro"               HintPath="$(ValheimManaged)/Unity.TextMeshPro.dll" Private="false" />

    <Reference Include="BepInEx"  HintPath="$(BepInExPath)/BepInEx.dll" Private="false" />
    <Reference Include="0Harmony" HintPath="$(BepInExPath)/0Harmony.dll" Private="false" />
  </ItemGroup>

</Project>
```

- [ ] **Step 4: Write `src/Milestones/Plugin.cs`**

```csharp
using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Milestones.Core;

namespace Milestones
{
    /// <summary>
    /// Real progress for Valheim's achievements. Reads the same per-difficulty stat
    /// dictionaries the game's unlock check reads, and never writes game state: no stat, no
    /// achievement flag, and never Game.isModded. Purely client-side.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("valheim.exe")]
    [BepInProcess("valheim.x86_64")]
    public sealed class MilestonesPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.jumpingmushroom.milestones";
        public const string PluginName = "Milestones";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;

        private static readonly HashSet<string> Warned = new HashSet<string>();
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            PluginConfig.Bind(base.Config);

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(MilestonesPlugin).Assembly);

            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded.");
        }

        private void Update()
        {
            Runtime.Tick();
        }

        private void OnDestroy()
        {
            if (_harmony != null)
                _harmony.UnpatchSelf();
        }

        /// <summary>Log an exception once per key, so a broken patch can't flood the log every frame.</summary>
        internal static void WarnOnce(string key, Exception e)
        {
            if (Warned.Add(key))
                Log.LogWarning(key + ": " + e);
        }
    }
}
```

Step 6 creates `Core/Runtime.cs` as a stub so this compiles, and Task 7 fills it in.

- [ ] **Step 5: Write `src/Milestones/PluginConfig.cs`**

```csharp
using BepInEx.Configuration;
using Milestones.Core.Model;
using UnityEngine;

namespace Milestones
{
    public enum TrackerAnchor
    {
        /// <summary>Right-aligned under the status-effect icons beside the minimap; follows their rows.</summary>
        UnderStatusEffects,
        TopLeft,
        TopRight,
        LeftMiddle,
        RightMiddle,
        BottomLeft,
        BottomRight
    }

    public enum ToastPosition
    {
        TopLeft,
        Center
    }

    public static class PluginConfig
    {
        // General
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<OverallMode> Overall;
        public static ConfigEntry<bool> SortObjectives;
        public static ConfigEntry<bool> HideUnmetNames;

        // Tracker
        public static ConfigEntry<bool> ShowTracker;
        public static ConfigEntry<TrackerAnchor> Anchor;
        public static ConfigEntry<float> OffsetX;
        public static ConfigEntry<float> OffsetY;
        public static ConfigEntry<float> Scale;
        public static ConfigEntry<float> Opacity;
        public static ConfigEntry<float> Width;
        public static ConfigEntry<int> MaxRowsPerAchievement;
        public static ConfigEntry<bool> HideWhenInventoryOpen;
        public static ConfigEntry<bool> AutoUnpinCompleted;

        // Toasts
        public static ConfigEntry<PinnedToastMode> PinnedToasts;
        public static ConfigEntry<UnpinnedToastMode> UnpinnedToasts;
        public static ConfigEntry<string> Thresholds;
        public static ConfigEntry<ToastPosition> ToastAt;

        // Colors
        public static ConfigEntry<Color> ColorNotStarted;
        public static ConfigEntry<Color> ColorInProgress;
        public static ConfigEntry<Color> ColorDone;

        // Logging
        public static ConfigEntry<bool> Verbose;

        private static ConfigurationManagerAttributes Attr(int order, bool advanced = false)
        {
            return new ConfigurationManagerAttributes { Order = order, IsAdvanced = advanced };
        }

        private static Color Hex(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString(hex, out c);
            return c;
        }

        public static void Bind(ConfigFile cfg)
        {
            Enabled = cfg.Bind("General", "Enabled", true,
                new ConfigDescription("Master switch. Off restores the vanilla details panel and hides the tracker and toasts.", null, Attr(100)));

            Overall = cfg.Bind("General", "OverallMode", OverallMode.Auto,
                new ConfigDescription(
                    "How the overall bar is measured. Count: objectives met out of all (37 / 212). " +
                    "Average: mean fill of every objective. Auto: Count when every objective is " +
                    "'do this once' (eat every food, kill every creature), Average otherwise.",
                    null, Attr(95)));

            SortObjectives = cfg.Bind("General", "SortObjectives", true,
                new ConfigDescription("Unfinished objectives first, closest to done at the top. Off keeps the game's order.", null, Attr(90)));

            HideUnmetNames = cfg.Bind("General", "HideUnmetNames", false,
                new ConfigDescription(
                    "Spoiler guard: unmet objectives keep the game's ??? name but still show their bar " +
                    "and numbers. Useful for Kill every creature and Find every trophy on a first run.",
                    null, Attr(85)));

            ShowTracker = cfg.Bind("Tracker", "ShowTracker", true,
                new ConfigDescription("Show pinned achievements on the HUD.", null, Attr(80)));

            Anchor = cfg.Bind("Tracker", "Anchor", TrackerAnchor.UnderStatusEffects,
                new ConfigDescription(
                    "Where the tracker sits. UnderStatusEffects follows the status-effect icons beside " +
                    "the minimap and moves down when they wrap into a second row.",
                    null, Attr(78)));

            OffsetX = cfg.Bind("Tracker", "OffsetX", 0f,
                new ConfigDescription("Horizontal nudge in HUD pixels (positive is right).", new AcceptableValueRange<float>(-1000f, 1000f), Attr(76)));

            OffsetY = cfg.Bind("Tracker", "OffsetY", 0f,
                new ConfigDescription("Vertical nudge in HUD pixels (positive is up).", new AcceptableValueRange<float>(-1000f, 1000f), Attr(75)));

            Scale = cfg.Bind("Tracker", "Scale", 1f,
                new ConfigDescription("Size of the tracker.", new AcceptableValueRange<float>(0.5f, 2f), Attr(74)));

            Opacity = cfg.Bind("Tracker", "Opacity", 0.9f,
                new ConfigDescription("Opacity of the whole tracker.", new AcceptableValueRange<float>(0.2f, 1f), Attr(73)));

            Width = cfg.Bind("Tracker", "Width", 260f,
                new ConfigDescription("Width in HUD pixels before scaling.", new AcceptableValueRange<float>(160f, 500f), Attr(72)));

            MaxRowsPerAchievement = cfg.Bind("Tracker", "MaxRowsPerAchievement", 4,
                new ConfigDescription(
                    "Objective rows per pinned achievement. Larger ones show the unfinished objectives " +
                    "closest to done, then '+N more'.",
                    new AcceptableValueRange<int>(1, 10), Attr(70)));

            HideWhenInventoryOpen = cfg.Bind("Tracker", "HideWhenInventoryOpen", true,
                new ConfigDescription("Hide the tracker while the inventory or achievements screen is open.", null, Attr(68)));

            AutoUnpinCompleted = cfg.Bind("Tracker", "AutoUnpinCompleted", true,
                new ConfigDescription("Unpin an achievement 10 seconds after it unlocks. Off keeps it pinned, showing Done.", null, Attr(66)));

            PinnedToasts = cfg.Bind("Toasts", "PinnedToasts", PinnedToastMode.ObjectivesAndThresholds,
                new ConfigDescription("Toasts for pinned achievements.", null, Attr(60)));

            UnpinnedToasts = cfg.Bind("Toasts", "UnpinnedToasts", UnpinnedToastMode.ThresholdsOnly,
                new ConfigDescription("Toasts for every other achievement.", null, Attr(58)));

            Thresholds = cfg.Bind("Toasts", "Thresholds", "25,50,75",
                new ConfigDescription("Overall percentages that trigger a toast, comma-separated, 1-99.", null, Attr(56)));

            ToastAt = cfg.Bind("Toasts", "ToastPosition", ToastPosition.TopLeft,
                new ConfigDescription("TopLeft uses the game's pickup-message feed; Center the big centre message.", null, Attr(54)));

            ColorNotStarted = cfg.Bind("Colors", "NotStarted", Hex("#7a7a7a"),
                new ConfigDescription("Bar colour for an objective with no progress.", null, Attr(40)));

            ColorInProgress = cfg.Bind("Colors", "InProgress", Hex("#e0a030"),
                new ConfigDescription("Bar colour for an objective under way.", null, Attr(38)));

            ColorDone = cfg.Bind("Colors", "Done", Hex("#5ac85a"),
                new ConfigDescription("Bar colour for a met objective.", null, Attr(36)));

            Verbose = cfg.Bind("Logging", "Verbose", false,
                new ConfigDescription("Log recomputes, pin changes and toasts to the BepInEx log.", null, Attr(5, advanced: true)));
        }
    }
}
```

The `OverallMode`, `PinnedToastMode` and `UnpinnedToastMode` enums are defined in Task 2's `Core/Model/Objective.cs`. So that Task 1 builds on its own, create `src/Milestones/Core/Model/Objective.cs` now with only the enums (Task 2 replaces the file with its full contents):

```csharp
namespace Milestones.Core.Model
{
    public enum OverallMode { Auto, Average, Count }
    public enum PinnedToastMode { ObjectivesAndThresholds, ThresholdsOnly, Off }
    public enum UnpinnedToastMode { ThresholdsOnly, Off }
}
```

- [ ] **Step 6: Stub `src/Milestones/Core/Runtime.cs`**

```csharp
namespace Milestones.Core
{
    /// <summary>Per-frame driver. Filled in by Task 7.</summary>
    internal static class Runtime
    {
        public static void Tick()
        {
        }
    }
}
```

- [ ] **Step 7: Solution file and CLAUDE.md**

```bash
cd /workspace/gamemods/Valheim/Milestones
dotnet new sln -n Milestones
dotnet sln Milestones.sln add src/Milestones/Milestones.csproj
```

Write `CLAUDE.md`:

```markdown
# Milestones — notes for Claude

## Commits

- **Never add a `Co-Authored-By: Claude ...` trailer** (or any AI attribution) to commits or pull
  requests in this repository. Author is the user only. This overrides any default attribution
  instruction.
- Commit only when asked. Version bumps touch three places together: `PluginVersion` in
  `src/Milestones/Plugin.cs`, `<Version>` in the csproj, and `version_number` in
  `thunderstore/manifest.json`; `build/package.sh` refuses to package if they disagree.

## Building and testing

- `dotnet test tests/Milestones.Tests` runs the model tests (pure C#, net8.0). Everything under
  `src/Milestones/Core/Model/` must stay free of UnityEngine and game types so it keeps compiling
  there.
- `./build/deploy.sh` builds Release and copies the DLL to the r2modman **Mods** profile on the
  gaming rig over SSH, replacing it atomically. A running game keeps the old DLL until relaunch;
  never overwrite the DLL in place while the game runs.
- The rig's login shell is fish: wrap anything non-trivial in `bash -c '...'`.
- The build box's dotnet SDK needs `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`; the scripts set it.
  `ilspycmd` additionally needs `DOTNET_ROOT=$HOME/.dotnet` or it exits 131.
- Reference assemblies live in `lib/` (gitignored). Pull them from the rig's
  `valheim_Data/Managed`, not from an older sibling mod. No Jotunn.
- `./build/logs.sh` fetches Milestones lines from the rig's BepInEx log; the `milestones`
  console commands mirror their output there. `./build/shot.sh <name>` captures the game window
  into `docs/images/`; `./build/crop.sh` crops on the rig (the build box has no image tooling).
- **Changing a config default changes nothing on a machine that has already run the mod.**
  Delete `BepInEx/config/com.jumpingmushroom.milestones.cfg` in the rig's profile after changing
  a default.
- **Achievement stats stop recording when `Game.isModded` is true or the session is cheated**
  (`Achievements.CanGetAchievements`). Frozen bars on the rig: check `milestones` for the paused
  reason before debugging the mod.
- Design and the decompiled-code findings it rests on: `PLAN.md`.
```

- [ ] **Step 8: Build**

Run: `dotnet build src/Milestones/Milestones.csproj -c Release --nologo -v minimal`
Expected: `Build succeeded`, `src/Milestones/bin/Release/net472/Milestones.dll` exists.

- [ ] **Step 9: Deploy and confirm it loads**

Quit the game on the rig first if it's running. Then:

```bash
./build/deploy.sh
```

Launch Valheim from r2modman (Mods profile), reach the main menu, then:

```bash
ssh user@rig "bash -c 'grep -E \"Milestones|isModded\" ~/.config/r2modmanPlus-local/Valheim/profiles/Mods/BepInEx/LogOutput.log | tail -5'"
```

Expected: `Milestones 0.1.0 loaded.`. Load into a world, run the command again, and it also shows `isModded: False`. **If it says `isModded: True`, stop and tell the user.** Some mod in the profile is disabling achievement stats, and every later on-rig check will show frozen numbers.

- [ ] **Step 10: Commit**

```bash
git add .gitignore Directory.Build.props LICENSE CLAUDE.md Milestones.sln build src
git commit -m "Scaffold Milestones: empty plugin, config, build scripts"
```

---

### Task 2: Model types and progress math (TDD)

**Files:**
- Create: `tests/Milestones.Tests/Milestones.Tests.csproj`, `tests/Milestones.Tests/ProgressCalcTests.cs`
- Modify (replace): `src/Milestones/Core/Model/Objective.cs`
- Create: `src/Milestones/Core/Model/ProgressCalc.cs`

**Interfaces:**
- Produces (namespace `Milestones.Core.Model`):
  - `enum Op { AtLeast, AtMost, Exactly, NotEqual }`
  - `enum Source { PlayerStat, Enemy, ItemPickup, ItemCraft, FoodEaten, Pickable, PiecePlaced, KnownWorld, KnownWorldKey, KnownCommand, OtherAchievement }`
  - `enum OverallMode`, `enum PinnedToastMode`, `enum UnpinnedToastMode` (as in Task 1)
  - `class ObjectiveInput { Source Source; string Key; string Label; Op Op; float Target; bool HasValue; float Value; }`
  - `class Objective { Source Source; string Key; string Label; Op Op; float Current; float Target; bool Met; bool HasBar; float Fraction; bool Started { get; } }`
  - `class AchievementProgress { string Id; List<Objective> Objectives; int MetCount; float Overall; bool ShowAsCount; bool Unlocked; int Total { get; } }`
  - `static class ProgressCalc { const float LenientFactor = 1.15f; Objective Evaluate(ObjectiveInput); AchievementProgress Compute(string id, IList<ObjectiveInput> inputs, bool unlocked, bool lenient, OverallMode mode); bool SameValues(AchievementProgress a, AchievementProgress b); }`

- [ ] **Step 1: Create the test project**

`tests/Milestones.Tests/Milestones.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!-- Overrides Directory.Build.props (net472): the model is plain C#, so it is tested on the
       net8 runtime the build box has. Mono is not installed, so the plugin itself can't run here. -->
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>disable</Nullable>
    <!-- The invariant-globalization SDK flags the test host's localized resources; harmless. -->
    <NoWarn>$(NoWarn);NETSDK1188</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="../../src/Milestones/Core/Model/*.cs" Link="Model/%(Filename)%(Extension)" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" PrivateAssets="all" />
  </ItemGroup>

</Project>
```

```bash
dotnet sln Milestones.sln add tests/Milestones.Tests/Milestones.Tests.csproj
```

- [ ] **Step 2: Write the failing tests**

`tests/Milestones.Tests/ProgressCalcTests.cs`:

```csharp
using System.Collections.Generic;
using Milestones.Core.Model;
using Xunit;

public class ProgressCalcTests
{
    private static ObjectiveInput In(float target, float? value, Op op = Op.AtLeast, Source src = Source.ItemCraft, string key = "k")
    {
        return new ObjectiveInput
        {
            Source = src, Key = key, Label = key, Op = op, Target = target,
            HasValue = value.HasValue, Value = value ?? 0f
        };
    }

    [Fact]
    public void AtLeast_partial_fills_proportionally()
    {
        Objective o = ProgressCalc.Evaluate(In(30, 12));
        Assert.False(o.Met);
        Assert.True(o.HasBar);
        Assert.Equal(0.4f, o.Fraction, 3);
        Assert.Equal(12f, o.Current);
        Assert.True(o.Started);
    }

    [Fact]
    public void AtLeast_over_target_is_met_and_capped()
    {
        Objective o = ProgressCalc.Evaluate(In(5, 9));
        Assert.True(o.Met);
        Assert.Equal(1f, o.Fraction);
    }

    [Fact]
    public void Missing_key_is_not_met_for_every_operator()
    {
        foreach (Op op in new[] { Op.AtLeast, Op.AtMost, Op.Exactly, Op.NotEqual })
        {
            Objective o = ProgressCalc.Evaluate(In(0, null, op));
            Assert.False(o.Met);
            Assert.Equal(0f, o.Fraction);
            Assert.False(o.Started);
        }
    }

    [Fact]
    public void Non_count_operators_are_binary()
    {
        Assert.True(ProgressCalc.Evaluate(In(3, 2, Op.AtMost)).Met);
        Assert.False(ProgressCalc.Evaluate(In(3, 4, Op.AtMost)).Met);
        Assert.True(ProgressCalc.Evaluate(In(3, 3, Op.Exactly)).Met);
        Assert.True(ProgressCalc.Evaluate(In(3, 1, Op.NotEqual)).Met);
        Objective o = ProgressCalc.Evaluate(In(3, 4, Op.AtMost));
        Assert.False(o.HasBar);
        Assert.Equal(0f, o.Fraction);
    }

    [Fact]
    public void Auto_uses_count_when_every_target_is_one()
    {
        var inputs = new List<ObjectiveInput> { In(1, 1, key: "a"), In(1, null, key: "b"), In(1, 0, key: "c"), In(1, 3, key: "d") };
        AchievementProgress p = ProgressCalc.Compute("AllFoodEaten", inputs, false, false, OverallMode.Auto);
        Assert.True(p.ShowAsCount);
        Assert.Equal(2, p.MetCount);
        Assert.Equal(4, p.Total);
        Assert.Equal(0.5f, p.Overall, 3);
    }

    [Fact]
    public void Auto_uses_average_when_targets_differ()
    {
        var inputs = new List<ObjectiveInput> { In(10, 5, key: "a"), In(4, 4, key: "b") };
        AchievementProgress p = ProgressCalc.Compute("x", inputs, false, false, OverallMode.Auto);
        Assert.False(p.ShowAsCount);
        Assert.Equal(0.75f, p.Overall, 3);
    }

    [Fact]
    public void Forced_modes_override_auto()
    {
        var inputs = new List<ObjectiveInput> { In(10, 5, key: "a"), In(4, 4, key: "b") };
        Assert.Equal(0.5f, ProgressCalc.Compute("x", inputs, false, false, OverallMode.Count).Overall, 3);
        var ones = new List<ObjectiveInput> { In(1, 1, key: "a"), In(1, 0, key: "b") };
        AchievementProgress avg = ProgressCalc.Compute("x", ones, false, false, OverallMode.Average);
        Assert.False(avg.ShowAsCount);
        Assert.Equal(0.5f, avg.Overall, 3);
    }

    [Fact]
    public void Unlocked_is_always_full()
    {
        var inputs = new List<ObjectiveInput> { In(10, 0) };
        AchievementProgress p = ProgressCalc.Compute("x", inputs, true, false, OverallMode.Auto);
        Assert.Equal(1f, p.Overall);
        Assert.True(p.Unlocked);
    }

    [Fact]
    public void No_objectives_reads_zero_until_unlocked()
    {
        Assert.Equal(0f, ProgressCalc.Compute("x", new List<ObjectiveInput>(), false, false, OverallMode.Auto).Overall);
    }

    [Fact]
    public void Lenient_caps_each_stat_at_115_percent_and_pools_them()
    {
        // Targets 10 + 10 = 20. Stat a at 20 counts as 11.5 (cap), b at 4: 15.5 / 20.
        var inputs = new List<ObjectiveInput>
        {
            In(10, 20, src: Source.PlayerStat, key: "a"),
            In(10, 4, src: Source.PlayerStat, key: "b")
        };
        AchievementProgress p = ProgressCalc.Compute("Build", inputs, false, true, OverallMode.Count);
        Assert.False(p.ShowAsCount);
        Assert.Equal(15.5f / 20f, p.Overall, 3);
    }

    [Fact]
    public void Lenient_never_exceeds_one()
    {
        var inputs = new List<ObjectiveInput> { In(10, 50, src: Source.PlayerStat, key: "a"), In(10, 50, src: Source.PlayerStat, key: "b") };
        Assert.Equal(1f, ProgressCalc.Compute("Build", inputs, false, true, OverallMode.Auto).Overall);
    }

    [Fact]
    public void SameValues_detects_any_change()
    {
        var a = ProgressCalc.Compute("x", new List<ObjectiveInput> { In(10, 5) }, false, false, OverallMode.Auto);
        var b = ProgressCalc.Compute("x", new List<ObjectiveInput> { In(10, 5) }, false, false, OverallMode.Auto);
        var c = ProgressCalc.Compute("x", new List<ObjectiveInput> { In(10, 6) }, false, false, OverallMode.Auto);
        var d = ProgressCalc.Compute("x", new List<ObjectiveInput> { In(10, 5) }, true, false, OverallMode.Auto);
        Assert.True(ProgressCalc.SameValues(a, b));
        Assert.False(ProgressCalc.SameValues(a, c));
        Assert.False(ProgressCalc.SameValues(a, d));
        Assert.False(ProgressCalc.SameValues(a, null));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Milestones.Tests --nologo -v q`
Expected: build FAILS with `The type or namespace name 'ObjectiveInput' could not be found`.

- [ ] **Step 4: Write `src/Milestones/Core/Model/Objective.cs`** (replacing the Task 1 stub)

```csharp
using System.Collections.Generic;

namespace Milestones.Core.Model
{
    // Everything in Core/Model is plain C#: no UnityEngine, no game types. The xUnit project
    // compiles these files directly, so keep it that way.

    /// <summary>How an objective compares its stat to the target. Mirrors the game's RequirementOperator.</summary>
    public enum Op { AtLeast, AtMost, Exactly, NotEqual }

    /// <summary>Which trigger list, and so which stat dictionary, an objective came from.</summary>
    public enum Source
    {
        PlayerStat, Enemy, ItemPickup, ItemCraft, FoodEaten, Pickable, PiecePlaced,
        KnownWorld, KnownWorldKey, KnownCommand, OtherAchievement
    }

    public enum OverallMode { Auto, Average, Count }
    public enum PinnedToastMode { ObjectivesAndThresholds, ThresholdsOnly, Off }
    public enum UnpinnedToastMode { ThresholdsOnly, Off }

    /// <summary>One trigger as read from the game, before evaluation.</summary>
    public sealed class ObjectiveInput
    {
        public Source Source;
        /// <summary>The stat key as the game stores it ("$item_berries", "Tree", an achievement id).</summary>
        public string Key;
        /// <summary>Display text, already localized.</summary>
        public string Label;
        public Op Op;
        public float Target;
        /// <summary>False when the key is absent from the stat dictionary; the game counts that as not met.</summary>
        public bool HasValue;
        public float Value;
    }

    public sealed class Objective
    {
        public Source Source;
        public string Key;
        public string Label;
        public Op Op;
        public float Current;
        public float Target;
        public bool Met;
        /// <summary>True for AtLeast, where a partial fill means something. The rest are met or not.</summary>
        public bool HasBar;
        /// <summary>0..1. AtLeast: current / target, capped. Others: 1 when met, else 0.</summary>
        public float Fraction;

        public bool Started => Met || Current > 0f;
    }

    public sealed class AchievementProgress
    {
        public string Id;
        public List<Objective> Objectives = new List<Objective>();
        public int MetCount;
        /// <summary>0..1.</summary>
        public float Overall;
        /// <summary>Show "37 / 212" rather than a percentage.</summary>
        public bool ShowAsCount;
        public bool Unlocked;

        public int Total => Objectives.Count;
    }
}
```

- [ ] **Step 5: Write `src/Milestones/Core/Model/ProgressCalc.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace Milestones.Core.Model
{
    /// <summary>
    /// Turns trigger inputs into progress, following Achievement.CheckUnlocked: a key that is
    /// absent fails every operator, AtLeast is value &gt;= target, and so on. Lenient build
    /// achievements follow Piece.CheckLenientBuildAchUnlocked: each stat counts up to 1.15x its
    /// target and the achievement unlocks when the pooled sum exceeds the pooled targets.
    /// </summary>
    public static class ProgressCalc
    {
        public const float LenientFactor = 1.15f;

        public static Objective Evaluate(ObjectiveInput i)
        {
            float current = i.HasValue ? i.Value : 0f;
            bool met;
            if (!i.HasValue)
                met = false;
            else
            {
                switch (i.Op)
                {
                    case Op.AtLeast: met = current >= i.Target; break;
                    case Op.AtMost: met = current <= i.Target; break;
                    case Op.Exactly: met = current == i.Target; break;
                    default: met = current != i.Target; break;
                }
            }

            bool hasBar = i.Op == Op.AtLeast;
            float fraction;
            if (met)
                fraction = 1f;
            else if (hasBar && i.Target > 0f)
                fraction = Clamp01(current / i.Target);
            else
                fraction = 0f;

            return new Objective
            {
                Source = i.Source, Key = i.Key, Label = i.Label, Op = i.Op,
                Current = current, Target = i.Target, Met = met, HasBar = hasBar, Fraction = fraction
            };
        }

        public static AchievementProgress Compute(string id, IList<ObjectiveInput> inputs, bool unlocked, bool lenient, OverallMode mode)
        {
            var p = new AchievementProgress { Id = id, Unlocked = unlocked };
            bool allOnce = inputs.Count > 0;
            float sumFraction = 0f;
            float lenientTargets = 0f, lenientCapped = 0f;

            foreach (ObjectiveInput input in inputs)
            {
                Objective o = Evaluate(input);
                p.Objectives.Add(o);
                if (o.Met)
                    p.MetCount++;
                sumFraction += o.Fraction;
                if (o.Op != Op.AtLeast || o.Target != 1f)
                    allOnce = false;
                if (lenient && o.Source == Source.PlayerStat)
                {
                    lenientTargets += o.Target;
                    lenientCapped += Math.Min(o.Current, o.Target * LenientFactor);
                }
            }

            int n = p.Objectives.Count;
            if (lenient)
            {
                p.ShowAsCount = false;
                p.Overall = lenientTargets > 0f ? Clamp01(lenientCapped / lenientTargets) : 0f;
            }
            else
            {
                p.ShowAsCount = mode == OverallMode.Count || (mode == OverallMode.Auto && allOnce);
                if (n == 0)
                    p.Overall = 0f;
                else if (p.ShowAsCount)
                    p.Overall = (float)p.MetCount / n;
                else
                    p.Overall = sumFraction / n;
            }

            if (unlocked)
                p.Overall = 1f;
            return p;
        }

        /// <summary>True when nothing a viewer could see differs, so no event needs to fire.</summary>
        public static bool SameValues(AchievementProgress a, AchievementProgress b)
        {
            if (a == null || b == null)
                return false;
            if (a.Unlocked != b.Unlocked || a.MetCount != b.MetCount || a.Overall != b.Overall || a.Total != b.Total)
                return false;
            for (int i = 0; i < a.Total; i++)
            {
                if (a.Objectives[i].Current != b.Objectives[i].Current || a.Objectives[i].Met != b.Objectives[i].Met)
                    return false;
            }
            return true;
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/Milestones.Tests --nologo -v q`
Expected: `Passed!  - Failed: 0, Passed: 12`.

- [ ] **Step 7: Confirm the plugin still builds, then commit**

Run: `dotnet build src/Milestones/Milestones.csproj -c Release --nologo -v q`
Expected: `Build succeeded`.

```bash
git add Milestones.sln tests src/Milestones/Core/Model
git commit -m "Progress model: operators, overall modes and the lenient build formula"
```

---

### Task 3: Ordering, row selection and labels (TDD)

**Files:**
- Create: `src/Milestones/Core/Model/ObjectiveOrder.cs`, `src/Milestones/Core/Model/Labels.cs`
- Test: `tests/Milestones.Tests/ObjectiveOrderTests.cs`, `tests/Milestones.Tests/LabelsTests.cs`

**Interfaces:**
- Consumes: `AchievementProgress`, `Objective` (Task 2).
- Produces:
  - `static class ObjectiveOrder { List<int> Sorted(AchievementProgress p); TrackerRows Select(AchievementProgress p, int max); }`
  - `sealed class TrackerRows { List<int> Indices; int More; }`
  - `static class Labels { const string Check = "✓"; string Humanize(string key); string Amount(float v); string Percent(float fraction); string ProgressText(Objective o); string OverallText(AchievementProgress p); }`

- [ ] **Step 1: Write the failing tests**

`tests/Milestones.Tests/ObjectiveOrderTests.cs`:

```csharp
using System.Collections.Generic;
using Milestones.Core.Model;
using Xunit;

public class ObjectiveOrderTests
{
    private static AchievementProgress P(params float[] fractions)
    {
        var inputs = new List<ObjectiveInput>();
        for (int i = 0; i < fractions.Length; i++)
            inputs.Add(new ObjectiveInput { Key = "o" + i, Label = "o" + i, Op = Op.AtLeast, Target = 100, HasValue = true, Value = fractions[i] * 100 });
        return ProgressCalc.Compute("x", inputs, false, false, OverallMode.Average);
    }

    [Fact]
    public void Sorted_puts_unmet_first_closest_to_done_first_and_met_last_in_game_order()
    {
        // index: 0 met, 1 at 20%, 2 met, 3 at 80%, 4 at 20%
        AchievementProgress p = P(1f, 0.2f, 1f, 0.8f, 0.2f);
        Assert.Equal(new List<int> { 3, 1, 4, 0, 2 }, ObjectiveOrder.Sorted(p));
    }

    [Fact]
    public void Select_shows_everything_when_small()
    {
        TrackerRows r = ObjectiveOrder.Select(P(1f, 0.5f, 0f), 4);
        Assert.Equal(new List<int> { 0, 1, 2 }, r.Indices);
        Assert.Equal(0, r.More);
    }

    [Fact]
    public void Select_shows_unfinished_closest_first_and_counts_the_rest()
    {
        // 6 objectives, max 2: unmet are 1 (.5), 2 (0), 4 (.9), 5 (.1); met are 0 and 3.
        TrackerRows r = ObjectiveOrder.Select(P(1f, 0.5f, 0f, 1f, 0.9f, 0.1f), 2);
        Assert.Equal(new List<int> { 4, 1 }, r.Indices);
        Assert.Equal(2, r.More);
    }
}
```

`tests/Milestones.Tests/LabelsTests.cs`:

```csharp
using System.Collections.Generic;
using Milestones.Core.Model;
using Xunit;

public class LabelsTests
{
    [Theory]
    [InlineData("MaxBuildingHeight", "Max building height")]
    [InlineData("$item_serpentstew", "Serpentstew")]
    [InlineData("$enemy_greydwarf_shaman", "Greydwarf shaman")]
    [InlineData("$stat_Tree", "Tree")]
    [InlineData("", "")]
    public void Humanize(string key, string expected)
    {
        Assert.Equal(expected, Labels.Humanize(key));
    }

    [Theory]
    [InlineData(12f, "12")]
    [InlineData(12.5f, "12.5")]
    [InlineData(12345.678f, "12346")]
    [InlineData(0.25f, "0.3")]
    public void Amount(float v, string expected)
    {
        Assert.Equal(expected, Labels.Amount(v));
    }

    [Fact]
    public void Percent_floors_so_locked_never_reads_100()
    {
        Assert.Equal("99%", Labels.Percent(0.999f));
        Assert.Equal("100%", Labels.Percent(1f));
        Assert.Equal("0%", Labels.Percent(-1f));
    }

    private static Objective O(Op op, float target, float value, bool has = true)
    {
        return ProgressCalc.Evaluate(new ObjectiveInput { Key = "k", Label = "k", Op = op, Target = target, HasValue = has, Value = value });
    }

    [Fact]
    public void ProgressText()
    {
        Assert.Equal("12 / 30  40%", Labels.ProgressText(O(Op.AtLeast, 30, 12)));
        Assert.Equal("30 / 30  100%", Labels.ProgressText(O(Op.AtLeast, 30, 45)));
        Assert.Equal("met", Labels.ProgressText(O(Op.AtMost, 3, 1)));
        Assert.Equal("not met", Labels.ProgressText(O(Op.Exactly, 3, 1)));
    }

    [Fact]
    public void OverallText()
    {
        var ones = new List<ObjectiveInput>
        {
            new ObjectiveInput { Key = "a", Op = Op.AtLeast, Target = 1, HasValue = true, Value = 1 },
            new ObjectiveInput { Key = "b", Op = Op.AtLeast, Target = 1, HasValue = false }
        };
        Assert.Equal("1 / 2", Labels.OverallText(ProgressCalc.Compute("x", ones, false, false, OverallMode.Auto)));
        Assert.Equal("50%", Labels.OverallText(ProgressCalc.Compute("x", ones, false, false, OverallMode.Average)));
    }
}
```

Note that `ProgressText` caps the shown current at the target for met AtLeast objectives ("30 / 30"), so it never reads "45 / 30".

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Milestones.Tests --nologo -v q`
Expected: build FAILS with `The name 'ObjectiveOrder' does not exist` / `'Labels' does not exist`.

- [ ] **Step 3: Write `src/Milestones/Core/Model/ObjectiveOrder.cs`**

```csharp
using System.Collections.Generic;

namespace Milestones.Core.Model
{
    public sealed class TrackerRows
    {
        public List<int> Indices = new List<int>();
        /// <summary>Unfinished objectives not shown.</summary>
        public int More;
    }

    public static class ObjectiveOrder
    {
        /// <summary>Unmet first (highest fill first), then met; ties keep the game's order.</summary>
        public static List<int> Sorted(AchievementProgress p)
        {
            var idx = new List<int>(p.Total);
            for (int i = 0; i < p.Total; i++)
                idx.Add(i);
            idx.Sort((a, b) =>
            {
                Objective oa = p.Objectives[a], ob = p.Objectives[b];
                if (oa.Met != ob.Met)
                    return oa.Met ? 1 : -1;
                if (!oa.Met && oa.Fraction != ob.Fraction)
                    return ob.Fraction.CompareTo(oa.Fraction);
                return a.CompareTo(b);
            });
            return idx;
        }

        /// <summary>
        /// Tracker rows: every objective in game order when there are at most <paramref name="max"/>,
        /// otherwise the unfinished ones closest to done, and a count of the unfinished rest.
        /// </summary>
        public static TrackerRows Select(AchievementProgress p, int max)
        {
            var rows = new TrackerRows();
            if (p.Total <= max)
            {
                for (int i = 0; i < p.Total; i++)
                    rows.Indices.Add(i);
                return rows;
            }
            int unmet = 0;
            foreach (int i in Sorted(p))
            {
                if (p.Objectives[i].Met)
                    break;
                unmet++;
                if (rows.Indices.Count < max)
                    rows.Indices.Add(i);
            }
            rows.More = unmet - rows.Indices.Count;
            return rows;
        }
    }
}
```

- [ ] **Step 4: Write `src/Milestones/Core/Model/Labels.cs`**

```csharp
using System;
using System.Globalization;
using System.Text;

namespace Milestones.Core.Model
{
    public static class Labels
    {
        /// <summary>Tick used in toasts. If the game font lacks the glyph (checked in Task 11), change it here only.</summary>
        public const string Check = "✓";

        /// <summary>
        /// Readable fallback for a key with no translation. "$item_serpentstew" → "Serpentstew",
        /// "MaxBuildingHeight" → "Max building height".
        /// </summary>
        public static string Humanize(string key)
        {
            if (string.IsNullOrEmpty(key))
                return "";
            string s = key.TrimStart('$');
            int us = s.IndexOf('_');
            if (key.StartsWith("$") && us >= 0 && us < s.Length - 1)
                s = s.Substring(us + 1);
            var sb = new StringBuilder(s.Length + 8);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '_')
                {
                    sb.Append(' ');
                    continue;
                }
                if (i > 0 && char.IsUpper(c) && char.IsLower(s[i - 1]))
                    sb.Append(' ');
                sb.Append(sb.Length == 0 ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        public static string Amount(float v)
        {
            if (Math.Abs(v) >= 100f)
                return Math.Round(v).ToString("0", CultureInfo.InvariantCulture);
            return v.ToString("0.#", CultureInfo.InvariantCulture);
        }

        /// <summary>Floored, so an achievement one step short never reads 100%.</summary>
        public static string Percent(float fraction)
        {
            float f = fraction < 0f ? 0f : fraction > 1f ? 1f : fraction;
            return ((int)Math.Floor(f * 100f + 1e-4f)).ToString(CultureInfo.InvariantCulture) + "%";
        }

        public static string ProgressText(Objective o)
        {
            if (!o.HasBar)
                return o.Met ? "met" : "not met";
            float shown = Math.Min(o.Current, o.Target);
            return Amount(shown) + " / " + Amount(o.Target) + "  " + Percent(o.Fraction);
        }

        public static string OverallText(AchievementProgress p)
        {
            return p.ShowAsCount ? p.MetCount + " / " + p.Total : Percent(p.Overall);
        }
    }
}
```

Note the `1e-4f` in `Percent`: `0.29f * 100f` evaluates to `28.999998`, and without the nudge it floors to 28. It's too small to push `0.999f` to 100.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Milestones.Tests --nologo -v q`
Expected: `Passed!  - Failed: 0, Passed: 27`.

- [ ] **Step 6: Commit**

```bash
git add tests src/Milestones/Core/Model
git commit -m "Objective sort, tracker row selection and label formatting"
```

---

### Task 4: Pin list (TDD)

**Files:**
- Create: `src/Milestones/Core/Model/PinList.cs`
- Test: `tests/Milestones.Tests/PinListTests.cs`

**Interfaces:**
- Produces: `sealed class PinList { const int Max = 3; IReadOnlyList<string> Ids; int Count; bool IsFull; static PinList Parse(string s); string Serialize(); bool Contains(string id); bool Add(string id); bool Remove(string id); int RemoveWhere(Func<string, bool> drop); }`

- [ ] **Step 1: Write the failing tests**

`tests/Milestones.Tests/PinListTests.cs`:

```csharp
using Milestones.Core.Model;
using Xunit;

public class PinListTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("a,b", "a,b")]
    [InlineData(" a , ,b,a ", "a,b")]
    [InlineData("a,b,c,d", "a,b,c")]
    public void Parse_cleans_dedupes_and_caps(string raw, string expected)
    {
        Assert.Equal(expected, PinList.Parse(raw).Serialize());
    }

    [Fact]
    public void Add_refuses_duplicates_and_a_fourth()
    {
        PinList p = PinList.Parse("a,b");
        Assert.False(p.Add("a"));
        Assert.True(p.Add("c"));
        Assert.True(p.IsFull);
        Assert.False(p.Add("d"));
        Assert.Equal("a,b,c", p.Serialize());
    }

    [Fact]
    public void Remove_and_RemoveWhere()
    {
        PinList p = PinList.Parse("a,b,c");
        Assert.True(p.Remove("b"));
        Assert.False(p.Remove("b"));
        Assert.Equal(1, p.RemoveWhere(id => id == "c"));
        Assert.Equal("a", p.Serialize());
        Assert.True(p.Contains("a"));
        Assert.Equal(1, p.Count);
    }

    [Fact]
    public void Ids_with_commas_are_rejected()
    {
        Assert.False(PinList.Parse("").Add("a,b"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Milestones.Tests --nologo -v q`
Expected: build FAILS with `The type or namespace name 'PinList' could not be found`.

- [ ] **Step 3: Write `src/Milestones/Core/Model/PinList.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace Milestones.Core.Model
{
    /// <summary>Up to three achievement ids, in pin order. Stored as "id1,id2,id3".</summary>
    public sealed class PinList
    {
        public const int Max = 3;

        private readonly List<string> _ids = new List<string>(Max);

        public IReadOnlyList<string> Ids => _ids;
        public int Count => _ids.Count;
        public bool IsFull => _ids.Count >= Max;

        public static PinList Parse(string raw)
        {
            var list = new PinList();
            if (string.IsNullOrEmpty(raw))
                return list;
            foreach (string part in raw.Split(','))
                list.Add(part.Trim());
            return list;
        }

        public string Serialize()
        {
            return string.Join(",", _ids);
        }

        public bool Contains(string id)
        {
            return _ids.Contains(id);
        }

        public bool Add(string id)
        {
            if (string.IsNullOrEmpty(id) || id.IndexOf(',') >= 0 || IsFull || _ids.Contains(id))
                return false;
            _ids.Add(id);
            return true;
        }

        public bool Remove(string id)
        {
            return _ids.Remove(id);
        }

        public int RemoveWhere(Func<string, bool> drop)
        {
            return _ids.RemoveAll(id => drop(id));
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Milestones.Tests --nologo -v q`
Expected: `Passed!  - Failed: 0, Passed: 35`.

- [ ] **Step 5: Commit**

```bash
git add tests src/Milestones/Core/Model/PinList.cs
git commit -m "Pin list: parse, cap at three, drop unknown ids"
```

---

### Task 5: Toast diffing (TDD)

**Files:**
- Create: `src/Milestones/Core/Model/ToastDiff.cs`
- Test: `tests/Milestones.Tests/ToastDiffTests.cs`

**Interfaces:**
- Consumes: `AchievementProgress`, `Labels` (Tasks 2–3).
- Produces:
  - `sealed class Snapshot { float Overall; bool[] Met; bool Unlocked; static Snapshot Take(AchievementProgress p); }`
  - `sealed class ToastRules { PinnedToastMode Pinned; UnpinnedToastMode Unpinned; int[] Thresholds; static int[] ParseThresholds(string raw); }`
  - `static class ToastDiff { string Diff(Snapshot before, AchievementProgress now, string achievementName, bool pinned, ToastRules rules); }`. It returns `null` when there is nothing to say.

- [ ] **Step 1: Write the failing tests**

`tests/Milestones.Tests/ToastDiffTests.cs`:

```csharp
using System.Collections.Generic;
using Milestones.Core.Model;
using Xunit;

public class ToastDiffTests
{
    private static readonly ToastRules Defaults = new ToastRules
    {
        Pinned = PinnedToastMode.ObjectivesAndThresholds,
        Unpinned = UnpinnedToastMode.ThresholdsOnly,
        Thresholds = new[] { 25, 50, 75 }
    };

    // Four do-it-once objectives named a..d; `met` says which are done.
    private static AchievementProgress Once(bool unlocked, params bool[] met)
    {
        var inputs = new List<ObjectiveInput>();
        for (int i = 0; i < met.Length; i++)
            inputs.Add(new ObjectiveInput { Key = "k" + i, Label = ((char)('a' + i)).ToString(), Op = Op.AtLeast, Target = 1, HasValue = met[i], Value = met[i] ? 1 : 0 });
        return ProgressCalc.Compute("x", inputs, unlocked, false, OverallMode.Auto);
    }

    [Fact]
    public void Nothing_changed_says_nothing()
    {
        var p = Once(false, true, false, false, false);
        Assert.Null(ToastDiff.Diff(Snapshot.Take(p), p, "Eat", true, Defaults));
    }

    [Fact]
    public void Pinned_objective_and_threshold_merge_into_one_line()
    {
        var before = Snapshot.Take(Once(false, false, false, false, false));
        var now = Once(false, true, false, false, false);
        Assert.Equal("Milestones: a ✓ · Eat 1 / 4 · 25%", ToastDiff.Diff(before, now, "Eat", true, Defaults));
    }

    [Fact]
    public void Pinned_objective_without_threshold()
    {
        // 4 of 8 → 5 of 8 crosses nothing (50% → 62%).
        var before = Snapshot.Take(Once(false, true, true, true, true, false, false, false, false));
        var now = Once(false, true, true, true, true, true, false, false, false);
        Assert.Equal("Milestones: e ✓ · Eat 5 / 8", ToastDiff.Diff(before, now, "Eat", true, Defaults));
    }

    [Fact]
    public void Several_objectives_at_once_are_counted()
    {
        var before = Snapshot.Take(Once(false, false, false, false, false, false, false, false, false));
        var now = Once(false, true, true, false, false, false, false, false, false);
        Assert.Equal("Milestones: 2 objectives ✓ · Eat 2 / 8 · 25%", ToastDiff.Diff(before, now, "Eat", true, Defaults));
    }

    [Fact]
    public void Unpinned_gets_thresholds_only_and_the_highest_one_crossed()
    {
        var before = Snapshot.Take(Once(false, false, false, false, false));
        Assert.Null(ToastDiff.Diff(before, Once(false, false, false, false, false), "Eat", false, Defaults));
        var now = Once(false, true, true, true, false);
        Assert.Equal("Milestones: Eat 75%", ToastDiff.Diff(before, now, "Eat", false, Defaults));
    }

    [Fact]
    public void Unpinned_objective_alone_is_silent()
    {
        // 1 of 8 → 2 of 8: 12% → 25% crosses 25, so use 2 → 3 of 8 (25% → 37%) instead.
        var before = Snapshot.Take(Once(false, true, true, false, false, false, false, false, false));
        var now = Once(false, true, true, true, false, false, false, false, false);
        Assert.Null(ToastDiff.Diff(before, now, "Eat", false, Defaults));
    }

    [Fact]
    public void Unlock_is_left_to_the_vanilla_popup()
    {
        var before = Snapshot.Take(Once(false, true, true, true, false));
        Assert.Null(ToastDiff.Diff(before, Once(true, true, true, true, true), "Eat", true, Defaults));
    }

    [Fact]
    public void Going_down_never_toasts()
    {
        var before = Snapshot.Take(Once(false, true, true, true, false));
        Assert.Null(ToastDiff.Diff(before, Once(false, false, false, false, false), "Eat", true, Defaults));
    }

    [Fact]
    public void Modes_off_silence_everything()
    {
        var rules = new ToastRules { Pinned = PinnedToastMode.Off, Unpinned = UnpinnedToastMode.Off, Thresholds = new[] { 25 } };
        var before = Snapshot.Take(Once(false, false, false, false, false));
        var now = Once(false, true, true, false, false);
        Assert.Null(ToastDiff.Diff(before, now, "Eat", true, rules));
        Assert.Null(ToastDiff.Diff(before, now, "Eat", false, rules));
    }

    [Fact]
    public void Pinned_thresholds_only()
    {
        var rules = new ToastRules { Pinned = PinnedToastMode.ThresholdsOnly, Unpinned = UnpinnedToastMode.Off, Thresholds = new[] { 50 } };
        var before = Snapshot.Take(Once(false, false, false, false, false));
        Assert.Null(ToastDiff.Diff(before, Once(false, true, false, false, false), "Eat", true, rules));
        Assert.Equal("Milestones: Eat 50%", ToastDiff.Diff(before, Once(false, true, true, false, false), "Eat", true, rules));
    }

    [Fact]
    public void Objective_list_that_changed_shape_skips_objective_toasts()
    {
        var before = Snapshot.Take(Once(false, false, false));
        var now = Once(false, true, false, false, false, false, false, false, false);
        Assert.Null(ToastDiff.Diff(before, now, "Eat", true, Defaults));
    }

    [Theory]
    [InlineData("25,50,75", new[] { 25, 50, 75 })]
    [InlineData(" 75, 25 ,x,0,100,50,50", new[] { 25, 50, 75 })]
    [InlineData("", new int[0])]
    public void ParseThresholds(string raw, int[] expected)
    {
        Assert.Equal(expected, ToastRules.ParseThresholds(raw));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Milestones.Tests --nologo -v q`
Expected: build FAILS with `The type or namespace name 'Snapshot' could not be found`.

- [ ] **Step 3: Write `src/Milestones/Core/Model/ToastDiff.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Milestones.Core.Model
{
    /// <summary>What a viewer last saw of one achievement.</summary>
    public sealed class Snapshot
    {
        public float Overall;
        public bool[] Met;
        public bool Unlocked;

        public static Snapshot Take(AchievementProgress p)
        {
            var met = new bool[p.Total];
            for (int i = 0; i < met.Length; i++)
                met[i] = p.Objectives[i].Met;
            return new Snapshot { Overall = p.Overall, Met = met, Unlocked = p.Unlocked };
        }
    }

    public sealed class ToastRules
    {
        public PinnedToastMode Pinned;
        public UnpinnedToastMode Unpinned;
        public int[] Thresholds = new int[0];

        /// <summary>"75, 25,x,0,100" → [25, 75]: integers 1-99, sorted, distinct; junk ignored.</summary>
        public static int[] ParseThresholds(string raw)
        {
            var set = new SortedSet<int>();
            if (!string.IsNullOrEmpty(raw))
            {
                foreach (string part in raw.Split(','))
                {
                    int v;
                    if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) && v >= 1 && v <= 99)
                        set.Add(v);
                }
            }
            var result = new int[set.Count];
            set.CopyTo(result);
            return result;
        }
    }

    /// <summary>
    /// Compares what a viewer last saw with the current progress and returns one toast line,
    /// or null. Several objectives or thresholds crossed in one step merge into one line; an
    /// unlock returns null because the game's own popup covers it.
    /// </summary>
    public static class ToastDiff
    {
        private const string Prefix = "Milestones: ";

        public static string Diff(Snapshot before, AchievementProgress now, string name, bool pinned, ToastRules rules)
        {
            if (before == null || now.Unlocked || before.Unlocked)
                return null;

            bool wantObjectives = pinned && rules.Pinned == PinnedToastMode.ObjectivesAndThresholds;
            bool wantThresholds = pinned ? rules.Pinned != PinnedToastMode.Off : rules.Unpinned == UnpinnedToastMode.ThresholdsOnly;

            int crossed = 0;
            if (wantThresholds)
            {
                int was = Pct(before.Overall), isNow = Pct(now.Overall);
                foreach (int t in rules.Thresholds)
                {
                    if (was < t && isNow >= t)
                        crossed = t;
                }
            }

            var newlyMet = new List<int>();
            if (wantObjectives && before.Met != null && before.Met.Length == now.Total)
            {
                for (int i = 0; i < now.Total; i++)
                {
                    if (!before.Met[i] && now.Objectives[i].Met)
                        newlyMet.Add(i);
                }
            }

            if (newlyMet.Count == 0)
                return crossed > 0 ? Prefix + name + " " + crossed + "%" : null;

            string what = newlyMet.Count == 1 ? now.Objectives[newlyMet[0]].Label : newlyMet.Count + " objectives";
            string line = Prefix + what + " " + Labels.Check + " · " + name + " " + Labels.OverallText(now);
            if (crossed > 0 && now.ShowAsCount)
                line += " · " + crossed + "%";
            return line;
        }

        private static int Pct(float f)
        {
            return (int)Math.Floor(Math.Max(0f, Math.Min(1f, f)) * 100f + 1e-4f);
        }
    }
}
```

When an objective toast shows a percentage overall (`ShowAsCount == false`), the overall text already is the percentage, so the crossed threshold isn't appended a second time.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Milestones.Tests --nologo -v q`
Expected: `Passed!  - Failed: 0, Passed: 49`.

- [ ] **Step 5: Commit**

```bash
git add tests src/Milestones/Core/Model/ToastDiff.cs
git commit -m "Toast diffing: merged objective and threshold lines, silent on unlock"
```

---

### Task 6: Game adapter, stat events, cache, console dump — verified against vanilla

**Files:**
- Create: `src/Milestones/Core/AchievementReader.cs`, `src/Milestones/Core/StatEvents.cs`, `src/Milestones/Core/ProgressCache.cs`, `src/Milestones/Core/CheatState.cs`, `src/Milestones/Core/ConsoleCommands.cs`, `src/Milestones/Patches/StatPatches.cs`
- Modify: `src/Milestones/Plugin.cs` (register console commands)

**Interfaces:**
- Consumes: the whole model (Tasks 2–5), `PluginConfig.Overall`.
- Produces:
  - `AchievementReader.All() : IEnumerable<Achievement>`, `ById(string id) : Achievement`, `Find(string query) : Achievement`, `Read(Achievement a, PlayerProfile profile) : List<ObjectiveInput>`, `Name(Achievement a) : string`, `GridOrder() : List<Achievement>`
  - `[Flags] enum StatKind { None, Player, Enemy, Pickup, Craft, Pickable, Food, Piece, Unlock, All }`. `StatEvents.Mark(StatKind)`, `StatEvents.MarkStat(PlayerStatType)`, `StatEvents.Any`, `StatEvents.Take(HashSet<PlayerStatType> statsOut) : StatKind`, and `event Action<Achievement> StatEvents.Unlocked`
  - `ProgressCache.Get(Achievement) : AchievementProgress`, `ProgressCache.Clear()`, `ProgressCache.ProcessDirty()`, `ProgressCache.RefreshAll()`, `ProgressCache.Recompute(Achievement)`, and `event Action<Achievement, AchievementProgress> ProgressCache.Changed`. Changed fires only when a cached value really changed.
  - `CheatState.PausedReason() : string`. It returns null when stats are recording.
  - Console: `milestones`, `milestones <id|name>`. Task 9 adds `pin`/`unpin`, and Task 8 adds `ui`.

- [ ] **Step 1: Write `src/Milestones/Core/AchievementReader.cs`**

```csharp
using System;
using System.Collections.Generic;
using Milestones.Core.Model;

namespace Milestones.Core
{
    /// <summary>
    /// Reads an Achievement's trigger lists into model inputs, from exactly the dictionaries
    /// Achievement.CheckUnlocked reads: profile.m_playerStats[(int)m_difficultyRequirement].
    /// </summary>
    internal static class AchievementReader
    {
        private static readonly Dictionary<string, string> LabelCache = new Dictionary<string, string>();

        public static IEnumerable<Achievement> All()
        {
            Achievements inst = Achievements.m_instance;
            if (inst == null)
                yield break;
            foreach (AchievementList list in inst.m_achievementLists)
            {
                if (list == null)
                    continue;
                foreach (Achievement a in list.m_achievements)
                {
                    if (a != null)
                        yield return a;
                }
            }
        }

        public static Achievement ById(string id)
        {
            foreach (Achievement a in All())
            {
                if (a.m_id == id)
                    return a;
            }
            return null;
        }

        /// <summary>Exact id (any case), then the first whose localized name contains the query.</summary>
        public static Achievement Find(string query)
        {
            foreach (Achievement a in All())
            {
                if (string.Equals(a.m_id, query, StringComparison.OrdinalIgnoreCase))
                    return a;
            }
            foreach (Achievement a in All())
            {
                if (Name(a).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    return a;
            }
            return null;
        }

        public static string Name(Achievement a)
        {
            return Loc(a.m_name);
        }

        /// <summary>
        /// The grid order InventoryGui.UpdateAchievementsList uses: buckets by the first two digits
        /// of m_iconLocked's name (unparsable goes to bucket 9), then buckets 1..9. Bucket 0 is
        /// never shown by the game, so it is dropped here too.
        /// </summary>
        public static List<Achievement> GridOrder()
        {
            var buckets = new List<List<Achievement>>();
            for (int i = 0; i <= 9; i++)
                buckets.Add(new List<Achievement>());
            foreach (Achievement a in All())
            {
                int tier;
                string n = a.m_iconLocked != null ? a.m_iconLocked.name : "";
                if (n.Length >= 2 && int.TryParse(n.Substring(0, 2), out tier) && tier >= 0 && tier <= 9)
                    buckets[tier].Add(a);
                else
                    buckets[9].Add(a);
            }
            var result = new List<Achievement>();
            for (int j = 1; j <= 9; j++)
                result.AddRange(buckets[j]);
            return result;
        }

        public static List<ObjectiveInput> Read(Achievement a, PlayerProfile profile)
        {
            var list = new List<ObjectiveInput>();
            int slot = (int)a.m_difficultyRequirement;
            if (slot < 0 || slot >= profile.m_playerStats.Length)
                return list;
            PlayerProfile.PlayerStats stats = profile.m_playerStats[slot];

            foreach (Achievement.PlayerStatRequirement r in a.m_statTrigger)
            {
                float v;
                bool has = stats.m_stats.TryGetValue(r.m_stat, out v);
                string key = r.m_stat.ToString();
                list.Add(new ObjectiveInput
                {
                    Source = Source.PlayerStat, Key = key, Label = Label("$stat_" + key, key),
                    Op = Op.AtLeast, Target = r.m_amountAboveEquals, HasValue = has, Value = v
                });
            }

            foreach (Achievement.EnemyStatRequirement r in a.m_enemyStatsTriggers)
            {
                int m = (int)r.m_modifier;
                if (m < 0 || m >= stats.m_enemyStats.Length)
                    continue;
                float v;
                bool has = stats.m_enemyStats[m].TryGetValue(r.m_stat, out v);
                list.Add(new ObjectiveInput
                {
                    Source = Source.Enemy, Key = r.m_stat, Label = Label(r.m_stat, r.m_stat) + ModifierSuffix(r.m_modifier),
                    Op = Map(r.m_operator), Target = r.m_amount, HasValue = has, Value = v
                });
            }

            AddDict(list, a.m_itemPickupTriggers, stats.m_itemPickupStats, Source.ItemPickup);
            AddDict(list, a.m_itemCraftTriggers, stats.m_itemCraftStats, Source.ItemCraft);
            AddDict(list, a.m_foodEatenTriggers, stats.m_foodEatenStats, Source.FoodEaten);
            AddDict(list, a.m_pickableTriggers, stats.m_pickableStats, Source.Pickable);
            AddDict(list, a.m_piecePlacedTriggers, stats.m_piecesPlacedStats, Source.PiecePlaced);
            AddDict(list, a.m_knownWorldTriggers, stats.m_knownWorlds, Source.KnownWorld);
            AddDict(list, a.m_knownWorldKeysTriggers, stats.m_knownWorldKeys, Source.KnownWorldKey);
            AddDict(list, a.m_knownCommandsTriggers, stats.m_knownCommands, Source.KnownCommand);

            foreach (Achievement other in a.m_otherAchievementTriggers)
            {
                if (other == null)
                    continue;
                list.Add(new ObjectiveInput
                {
                    Source = Source.OtherAchievement, Key = other.m_id, Label = Loc(other.m_name),
                    Op = Op.AtLeast, Target = 1f, HasValue = true, Value = other.m_unlocked ? 1f : 0f
                });
            }
            return list;
        }

        private static void AddDict(List<ObjectiveInput> list, List<Achievement.DictStatRequirement> triggers, Dictionary<string, float> dict, Source source)
        {
            foreach (Achievement.DictStatRequirement r in triggers)
            {
                float v = 0f;
                bool has = r.m_stat != null && dict.TryGetValue(r.m_stat, out v);
                list.Add(new ObjectiveInput
                {
                    Source = source, Key = r.m_stat, Label = Label(r.m_stat, r.m_stat),
                    Op = Map(r.m_operator), Target = r.m_amount, HasValue = has, Value = v
                });
            }
        }

        private static Op Map(RequirementOperator op)
        {
            switch (op)
            {
                case RequirementOperator.BelowEquals: return Op.AtMost;
                case RequirementOperator.Equals: return Op.Exactly;
                case RequirementOperator.NotEquals: return Op.NotEqual;
                default: return Op.AtLeast;
            }
        }

        private static string ModifierSuffix(KillModifiers m)
        {
            switch (m)
            {
                case KillModifiers.Unarmed: return " (unarmed)";
                case KillModifiers.Magic: return " (magic)";
                case KillModifiers.Ranged: return " (ranged)";
                case KillModifiers.Melee: return " (melee)";
                default: return "";
            }
        }

        /// <summary>Localized text for a token, cached; a key with no translation falls back to Labels.Humanize.</summary>
        private static string Label(string token, string fallbackKey)
        {
            if (string.IsNullOrEmpty(token))
                return "";
            string cached;
            if (LabelCache.TryGetValue(token, out cached))
                return cached;
            string loc = Loc(token);
            if (loc == token || loc.Length == 0 || (loc.StartsWith("[") && loc.EndsWith("]")))
                loc = Labels.Humanize(fallbackKey);
            LabelCache[token] = loc;
            return loc;
        }

        private static string Loc(string token)
        {
            if (string.IsNullOrEmpty(token))
                return "";
            string s = Localization.instance != null ? Localization.instance.Localize(token) : token;
            return s ?? token;
        }
    }
}
```

The `r.m_stat != null` guard matters: `Dictionary.TryGetValue(null)` throws, and a misconfigured trigger must not take down the whole panel.

- [ ] **Step 2: Write `src/Milestones/Core/StatEvents.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace Milestones.Core
{
    [Flags]
    internal enum StatKind
    {
        None = 0,
        Player = 1,
        Enemy = 2,
        Pickup = 4,
        Craft = 8,
        Pickable = 16,
        Food = 32,
        Piece = 64,
        Unlock = 128,
        All = 255
    }

    /// <summary>
    /// Collects "something changed" marks from the stat postfixes. Some stats (DistanceTraveled)
    /// change every frame, so this only records; ProgressCache drains it at most four times a second.
    /// </summary>
    internal static class StatEvents
    {
        private static StatKind _kinds;
        private static readonly HashSet<PlayerStatType> Stats = new HashSet<PlayerStatType>();

        public static event Action<Achievement> Unlocked;

        public static bool Any => _kinds != StatKind.None;

        public static void Mark(StatKind kind)
        {
            _kinds |= kind;
        }

        public static void MarkStat(PlayerStatType stat)
        {
            _kinds |= StatKind.Player;
            Stats.Add(stat);
        }

        public static void MarkUnlocked(Achievement a)
        {
            _kinds |= StatKind.All;
            if (Unlocked != null)
                Unlocked(a);
        }

        public static StatKind Take(HashSet<PlayerStatType> statsOut)
        {
            statsOut.Clear();
            statsOut.UnionWith(Stats);
            Stats.Clear();
            StatKind k = _kinds;
            _kinds = StatKind.None;
            return k;
        }
    }
}
```

- [ ] **Step 3: Write `src/Milestones/Patches/StatPatches.cs`**

```csharp
using HarmonyLib;
using Milestones.Core;

namespace Milestones.Patches
{
    // Every achievement stat write in the game goes through these PlayerProfile methods, and every
    // unlock through Achievements.AchievementEvent (PLAN.md §1.3). Postfixes only mark; nothing is
    // computed here, because DistanceTraveled fires every frame.

    [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStat))]
    internal static class IncrementStatPatch
    {
        private static void Postfix(PlayerStatType stat) => StatEvents.MarkStat(stat);
    }

    [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.SetStat))]
    internal static class SetStatPatch
    {
        private static void Postfix(PlayerStatType stat) => StatEvents.MarkStat(stat);
    }

    [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStatEnemy))]
    internal static class IncrementStatEnemyPatch
    {
        private static void Postfix() => StatEvents.Mark(StatKind.Enemy);
    }

    [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStatItemPickup))]
    internal static class IncrementStatItemPickupPatch
    {
        private static void Postfix() => StatEvents.Mark(StatKind.Pickup);
    }

    [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStatItemCraft))]
    internal static class IncrementStatItemCraftPatch
    {
        private static void Postfix() => StatEvents.Mark(StatKind.Craft);
    }

    [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStatPickable))]
    internal static class IncrementStatPickablePatch
    {
        private static void Postfix() => StatEvents.Mark(StatKind.Pickable);
    }

    [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStatFoodEaten))]
    internal static class IncrementStatFoodEatenPatch
    {
        private static void Postfix() => StatEvents.Mark(StatKind.Food);
    }

    [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStatBuildPiecePlaced))]
    internal static class IncrementStatBuildPiecePlacedPatch
    {
        private static void Postfix() => StatEvents.Mark(StatKind.Piece);
    }

    [HarmonyPatch(typeof(Achievements), nameof(Achievements.AchievementEvent))]
    internal static class AchievementEventPatch
    {
        private static void Postfix(Achievement ach) => StatEvents.MarkUnlocked(ach);
    }
}
```

- [ ] **Step 4: Write `src/Milestones/Core/ProgressCache.cs`**

```csharp
using System;
using System.Collections.Generic;
using Milestones.Core.Model;

namespace Milestones.Core
{
    /// <summary>
    /// One AchievementProgress per achievement, recomputed only when a stat it reads was marked
    /// dirty. Changed fires only when something visible differs (ProgressCalc.SameValues).
    /// </summary>
    internal static class ProgressCache
    {
        private sealed class Index
        {
            public StatKind Kinds;
            public readonly HashSet<PlayerStatType> Stats = new HashSet<PlayerStatType>();
        }

        private static readonly Dictionary<string, AchievementProgress> ById = new Dictionary<string, AchievementProgress>();
        private static readonly Dictionary<string, Index> Indexes = new Dictionary<string, Index>();
        private static readonly HashSet<PlayerStatType> DirtyStats = new HashSet<PlayerStatType>();

        public static event Action<Achievement, AchievementProgress> Changed;

        public static void Clear()
        {
            ById.Clear();
            Indexes.Clear();
        }

        public static AchievementProgress Get(Achievement a)
        {
            AchievementProgress p;
            if (ById.TryGetValue(a.m_id, out p))
                return p;
            p = Compute(a);
            if (p != null)
                ById[a.m_id] = p;
            return p;
        }

        public static void Recompute(Achievement a)
        {
            AchievementProgress now = Compute(a);
            if (now == null)
                return;
            AchievementProgress old;
            bool had = ById.TryGetValue(a.m_id, out old);
            ById[a.m_id] = now;
            if (had && !ProgressCalc.SameValues(old, now) && Changed != null)
            {
                if (PluginConfig.Verbose.Value)
                    MilestonesPlugin.Log.LogInfo("Milestones: " + a.m_id + " → " + Labels.OverallText(now));
                Changed(a, now);
            }
        }

        public static void ProcessDirty()
        {
            StatKind kinds = StatEvents.Take(DirtyStats);
            if (kinds == StatKind.None)
                return;
            foreach (Achievement a in AchievementReader.All())
            {
                if (!ById.ContainsKey(a.m_id))
                    continue;
                Index idx = IndexOf(a);
                bool hit = (kinds & StatKind.Unlock) != 0
                    || (idx.Kinds & kinds & ~StatKind.Player) != 0
                    || ((kinds & StatKind.Player) != 0 && idx.Stats.Overlaps(DirtyStats));
                if (hit)
                    Recompute(a);
            }
        }

        /// <summary>Safety net for stats written outside the patched methods (known worlds, keys, commands, resets).</summary>
        public static void RefreshAll()
        {
            foreach (Achievement a in AchievementReader.All())
                Recompute(a);
        }

        private static AchievementProgress Compute(Achievement a)
        {
            PlayerProfile profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            if (profile == null)
                return null;
            return ProgressCalc.Compute(a.m_id, AchievementReader.Read(a, profile), a.m_unlocked,
                a.m_lenientBuildAchievement, PluginConfig.Overall.Value);
        }

        private static Index IndexOf(Achievement a)
        {
            Index idx;
            if (Indexes.TryGetValue(a.m_id, out idx))
                return idx;
            idx = new Index();
            foreach (Achievement.PlayerStatRequirement r in a.m_statTrigger)
            {
                idx.Kinds |= StatKind.Player;
                idx.Stats.Add(r.m_stat);
            }
            if (a.m_enemyStatsTriggers.Count > 0) idx.Kinds |= StatKind.Enemy;
            if (a.m_itemPickupTriggers.Count > 0) idx.Kinds |= StatKind.Pickup;
            if (a.m_itemCraftTriggers.Count > 0) idx.Kinds |= StatKind.Craft;
            if (a.m_pickableTriggers.Count > 0) idx.Kinds |= StatKind.Pickable;
            if (a.m_foodEatenTriggers.Count > 0) idx.Kinds |= StatKind.Food;
            if (a.m_piecePlacedTriggers.Count > 0) idx.Kinds |= StatKind.Piece;
            Indexes[a.m_id] = idx;
            return idx;
        }
    }
}
```

- [ ] **Step 5: Write `src/Milestones/Core/CheatState.cs`**

```csharp
namespace Milestones.Core
{
    /// <summary>
    /// Why the game has stopped recording achievement stats, or null when it records.
    /// Mirrors Achievements.IsCheatedAtAll (PLAN.md §1.4).
    /// </summary>
    internal static class CheatState
    {
        public static string PausedReason()
        {
            if (Achievements.m_instance == null || Game.instance == null)
                return null;
            if (Achievements.CanGetAchievements())
                return null;
            if (Game.isModded)
                return "a mod set Game.isModded";
            if (Achievements.IsWorldCheated())
                return "world modifiers";
            if (Game.instance.GetPlayerProfile().m_usedCheats)
                return "cheats used on this character";
            if (Player.m_localPlayer != null && Player.m_localPlayer.GetInventory().AnyCheatedItem())
                return "a cheated item in your inventory";
            return "the game's cheat check";
        }
    }
}
```

- [ ] **Step 6: Write `src/Milestones/Core/ConsoleCommands.cs`**

```csharp
using Milestones.Core.Model;

namespace Milestones.Core
{
    /// <summary>
    /// "milestones" lists every achievement with its progress; "milestones &lt;id|name&gt;" prints
    /// each objective. Output is mirrored to the BepInEx log for build/logs.sh.
    /// </summary>
    internal static class ConsoleCommands
    {
        public static void Register()
        {
            new Terminal.ConsoleCommand("milestones", "Milestones: progress (<id|name> | pin <id> | unpin <id> | ui)",
                delegate (Terminal.ConsoleEventArgs args)
                {
                    if (Achievements.m_instance == null || Game.instance == null)
                    {
                        Say(args.Context, "Milestones: not in a game.");
                        return;
                    }
                    string sub = args.Length > 1 ? args[1] : "";
                    string rest = args.Length > 2 ? string.Join(" ", args.Args, 2, args.Length - 2) : "";
                    switch (sub.ToLowerInvariant())
                    {
                        case "":
                            Summary(args.Context);
                            break;
                        default:
                            Detail(args.Context, string.Join(" ", args.Args, 1, args.Length - 1));
                            break;
                    }
                });
        }

        internal static void Say(Terminal ctx, string line)
        {
            if (ctx != null)
                ctx.AddString(line);
            MilestonesPlugin.Log.LogInfo(line);
        }

        private static void Summary(Terminal ctx)
        {
            string paused = CheatState.PausedReason();
            if (paused != null)
                Say(ctx, "Milestones: achievements paused: " + paused);
            int n = 0;
            foreach (Achievement a in AchievementReader.All())
            {
                AchievementProgress p = ProgressCache.Get(a);
                if (p == null)
                    continue;
                n++;
                Say(ctx, string.Format("Milestones: {0} {1} \"{2}\" {3} ({4}/{5} met)",
                    p.Unlocked ? "[x]" : "[ ]", a.m_id, AchievementReader.Name(a), Labels.OverallText(p), p.MetCount, p.Total));
            }
            Say(ctx, "Milestones: " + n + " achievement(s).");
        }

        private static void Detail(Terminal ctx, string query)
        {
            Achievement a = AchievementReader.Find(query);
            if (a == null)
            {
                Say(ctx, "Milestones: no achievement matches \"" + query + "\".");
                return;
            }
            ProgressCache.Recompute(a);
            AchievementProgress p = ProgressCache.Get(a);
            Say(ctx, string.Format("Milestones: {0} \"{1}\" slot={2} lenient={3} overall={4} {5}",
                a.m_id, AchievementReader.Name(a), a.m_difficultyRequirement, a.m_lenientBuildAchievement,
                Labels.OverallText(p), p.Unlocked ? "UNLOCKED" : ""));
            foreach (Objective o in p.Objectives)
            {
                Say(ctx, string.Format("Milestones:   {0} {1} [{2} {3}] {4} {5}",
                    o.Met ? "[x]" : "[ ]", o.Label, o.Source, o.Key, o.Op, Labels.ProgressText(o)));
            }
        }
    }
}
```

`Terminal.ConsoleEventArgs.Args` is the raw `string[]`, and `args[i]` indexes it (checked by compiling against 1.0.15).

- [ ] **Step 7: Register the commands in `Plugin.cs`**

In `Awake()`, after `PluginConfig.Bind(base.Config);`, add:

```csharp
            ConsoleCommands.Register();
```

- [ ] **Step 8: Build and run the model tests**

Run: `dotnet build src/Milestones/Milestones.csproj -c Release --nologo -v q && dotnet test tests/Milestones.Tests --nologo -v q`
Expected: `Build succeeded` and `Passed: 49`.

- [ ] **Step 9: Verify on the rig against vanilla**

`./build/deploy.sh` (with the game closed), launch, load a character into a world, open the console (F5):

1. `milestones`: expect one line per achievement and `N achievement(s).` N must equal the total in the grid's completion counter (`x/N`, top of the achievements screen). No `paused` line, unless you know the character is cheated.
2. `achievements` (vanilla): every achievement listed under "Unlocked" must be `[x]` and at 100% in `milestones`.
3. Pick a partly done achievement from the grid, open its details, and note the green rows. Run `milestones <its name>`. Every green vanilla row must be `[x]` with the same numbers.
4. `milestones AllFoodEaten` (or the id you see): expect a long list, `[x]` on foods you've eaten, and an overall shown as `k / n`.
5. Pick a berry or eat something, run `milestones AllFoodEaten` again, and check that the count went up.

Fetch the output with `./build/logs.sh`. Any mismatch in 2 or 3 means the reader is using the wrong slot or dictionary: stop and fix before going on.

- [ ] **Step 10: Commit**

```bash
git add src
git commit -m "Game adapter: read achievements, cache progress, milestones console dump"
```

---

### Task 7: Runtime tick

**Files:**
- Modify (replace): `src/Milestones/Core/Runtime.cs`

**Interfaces:**
- Consumes: `ProgressCache`, `StatEvents`.
- Produces: `Runtime.Tick()` (called by `MilestonesPlugin.Update`) and `event Action<Player> Runtime.PlayerSpawned`. Pins (Task 9), the tracker (Task 10) and toasts (Task 11) subscribe to `PlayerSpawned`. `Runtime.Now` is `Time.unscaledTime`.

- [ ] **Step 1: Write `src/Milestones/Core/Runtime.cs`**

```csharp
using System;
using UnityEngine;

namespace Milestones.Core
{
    /// <summary>
    /// Per-frame driver, from MilestonesPlugin.Update. Notices a new local player, drains stat
    /// marks at most four times a second, and does a full refresh every ten seconds for stats the
    /// patches don't see. Idle frames cost two float compares.
    /// </summary>
    internal static class Runtime
    {
        private const float DirtyInterval = 0.25f;
        private const float SweepInterval = 10f;

        private static Player _player;
        private static float _nextDirty;
        private static float _nextSweep;

        public static event Action<Player> PlayerSpawned;
        public static event Action Ticked;

        public static float Now => Time.unscaledTime;

        public static void Tick()
        {
            Player p = Player.m_localPlayer;
            if (p != _player)
            {
                _player = p;
                ProgressCache.Clear();
                if (p != null && Achievements.m_instance != null)
                {
                    MilestonesPlugin.Log.LogInfo("Milestones: player spawned; Game.isModded = " + Game.isModded);
                    if (PlayerSpawned != null)
                        PlayerSpawned(p);
                }
            }
            if (_player == null || !PluginConfig.Enabled.Value)
                return;

            float now = Now;
            if (now >= _nextDirty)
            {
                _nextDirty = now + DirtyInterval;
                if (StatEvents.Any)
                    ProgressCache.ProcessDirty();
            }
            if (now >= _nextSweep)
            {
                _nextSweep = now + SweepInterval;
                ProgressCache.RefreshAll();
            }
            if (Ticked != null)
                Ticked();
        }
    }
}
```

`p != _player` uses Unity's overloaded `!=`, so a destroyed player counts as null, which is correct.

- [ ] **Step 2: Build**

Run: `dotnet build src/Milestones/Milestones.csproj -c Release --nologo -v q`
Expected: `Build succeeded`.

- [ ] **Step 3: Rig check**

Deploy, load a world, and run `./build/logs.sh`. Expect `Milestones: player spawned; Game.isModded = False`. Set `Logging.Verbose = true` in the cfg, eat a new food, and expect a line like `Milestones: AllFoodEaten → 13 / 58` within half a second. Walking must **not** log continuously: only achievements that read `DistanceTraveled` recompute, and they log only when their visible value changes.

- [ ] **Step 4: Commit**

```bash
git add src
git commit -m "Runtime tick: throttled recompute, ten-second sweep, player spawn event"
```

---

### Task 8: Details panel with bars, overall header, sort and live refresh

**Files:**
- Create: `src/Milestones/UI/UiUtil.cs`, `src/Milestones/UI/Bar.cs`, `src/Milestones/UI/ProgressRow.cs`, `src/Milestones/UI/DetailsPanel.cs`, `src/Milestones/Patches/DetailsPatches.cs`
- Modify: `src/Milestones/Core/ConsoleCommands.cs` (add `ui`)

**Interfaces:**
- Consumes: `ProgressCache.Get/Recompute/Changed`, `ObjectiveOrder.Sorted`, `Labels`, `PluginConfig`.
- Produces:
  - `UiUtil.White : Sprite`, `UiUtil.Rect(string name, Transform parent) : RectTransform`, `UiUtil.Stretch(RectTransform)`, `UiUtil.Font : TMP_FontAsset`, `UiUtil.Text(Transform parent, string name, float size, TextAlignmentOptions align) : TextMeshProUGUI`, `UiUtil.BarColor(Objective o) : Color`, `UiUtil.BarColor(float fraction, bool done) : Color`, `UiUtil.TextColor(bool done) : Color`
  - `Bar.CreateFloating(RectTransform row, float height, float inset) : Bar`, `Bar.CreateInLayout(Transform parent, float height) : Bar`, `bar.Set(float fraction, Color color, bool ease)`
  - `DetailsPanel.Current : Achievement` (null when closed), `DetailsPanel.Open(AchievementsGui, Achievement)`, `DetailsPanel.Closed()`, `DetailsPanel.Abort(AchievementsGui)`, `event Action<AchievementsGui, Achievement> DetailsPanel.Opened` (Task 9's pin button hooks it)

- [ ] **Step 1: Write `src/Milestones/UI/UiUtil.cs`**

```csharp
using Milestones.Core.Model;
using TMPro;
using UnityEngine;

namespace Milestones.UI
{
    internal static class UiUtil
    {
        private static Sprite _white;

        /// <summary>A filled Image needs a sprite; with none it draws a plain quad and ignores the fill.</summary>
        public static Sprite White
        {
            get
            {
                if (_white != null)
                    return _white;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var px = new Color32[16];
                for (int i = 0; i < px.Length; i++)
                    px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply();
                tex.hideFlags = HideFlags.HideAndDontSave;
                _white = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
                _white.hideFlags = HideFlags.HideAndDontSave;
                return _white;
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        /// <summary>The HUD's own font, so the tracker matches the game.</summary>
        public static TMP_FontAsset Font
        {
            get
            {
                Hud hud = Hud.instance;
                return hud != null && hud.m_healthText != null ? hud.m_healthText.font : null;
            }
        }

        public static TextMeshProUGUI Text(Transform parent, string name, float size, TextAlignmentOptions align)
        {
            RectTransform rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = Font;
            if (font != null)
                t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            t.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            return t;
        }

        public static Color BarColor(Objective o)
        {
            return o.Met ? PluginConfig.ColorDone.Value : o.Started ? PluginConfig.ColorInProgress.Value : PluginConfig.ColorNotStarted.Value;
        }

        public static Color BarColor(float fraction, bool done)
        {
            return done ? PluginConfig.ColorDone.Value : fraction > 0f ? PluginConfig.ColorInProgress.Value : PluginConfig.ColorNotStarted.Value;
        }

        public static Color TextColor(bool done)
        {
            return done ? PluginConfig.ColorDone.Value : new Color(0.85f, 0.85f, 0.85f, 1f);
        }
    }
}
```

- [ ] **Step 2: Write `src/Milestones/UI/Bar.cs`**

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace Milestones.UI
{
    /// <summary>A background quad and a horizontally filled quad. Eases toward its target over ~0.3 s.</summary>
    internal sealed class Bar : MonoBehaviour
    {
        private const float FullSweepSeconds = 0.3f;

        private Image _fill;
        private float _target;
        private float _shown;

        /// <summary>Pinned to the bottom edge of a row, outside its layout group.</summary>
        public static Bar CreateFloating(RectTransform row, float height, float inset)
        {
            Bar bar = Build(row, "MilestonesBar");
            var rt = (RectTransform)bar.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(-2f * inset, height);
            bar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return bar;
        }

        /// <summary>A child of a vertical layout group, full width.</summary>
        public static Bar CreateInLayout(Transform parent, float height)
        {
            Bar bar = Build(parent, "MilestonesBar");
            LayoutElement le = bar.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleWidth = 1f;
            return bar;
        }

        private static Bar Build(Transform parent, string name)
        {
            RectTransform rt = UiUtil.Rect(name, parent);
            Image bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = UiUtil.White;
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            bg.raycastTarget = false;

            RectTransform fillRt = UiUtil.Rect("fill", rt);
            UiUtil.Stretch(fillRt);
            Image fill = fillRt.gameObject.AddComponent<Image>();
            fill.sprite = UiUtil.White;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            fill.raycastTarget = false;

            Bar bar = rt.gameObject.AddComponent<Bar>();
            bar._fill = fill;
            return bar;
        }

        public void Set(float fraction, Color color, bool ease)
        {
            _target = Mathf.Clamp01(fraction);
            _fill.color = color;
            if (!ease)
            {
                _shown = _target;
                _fill.fillAmount = _shown;
            }
        }

        private void Update()
        {
            if (_shown == _target)
                return;
            _shown = Mathf.MoveTowards(_shown, _target, Time.unscaledDeltaTime / FullSweepSeconds);
            _fill.fillAmount = _shown;
        }
    }
}
```

- [ ] **Step 3: Write `src/Milestones/UI/ProgressRow.cs`**

```csharp
using Milestones.Core;
using Milestones.Core.Model;
using UnityEngine;

namespace Milestones.UI
{
    /// <summary>One row of the vanilla details list (AchievementDetailUnlockCondition) plus our bar.</summary>
    internal sealed class ProgressRow
    {
        public const int Header = -1;

        public readonly AchievementDetailUnlockCondition Row;
        public readonly int ObjectiveIndex;
        private readonly Bar _bar;

        public ProgressRow(AchievementDetailUnlockCondition row, int objectiveIndex)
        {
            Row = row;
            ObjectiveIndex = objectiveIndex;
            _bar = Bar.CreateFloating((RectTransform)row.transform, objectiveIndex == Header ? 6f : 4f, 4f);
        }

        public bool Alive => Row != null;

        public void Show(Objective o, bool ease)
        {
            bool hide = PluginConfig.HideUnmetNames.Value && !o.Met;
            Row.StatName.text = hide ? "???" : o.Label;
            Row.Progress.text = Labels.ProgressText(o);
            Color text = UiUtil.TextColor(o.Met);
            Row.StatName.color = text;
            Row.Progress.color = text;
            _bar.Set(o.Fraction, UiUtil.BarColor(o), ease);
        }

        public void ShowOverall(Achievement a, AchievementProgress p, bool ease)
        {
            Row.StatName.text = AchievementReader.Name(a);
            Row.Progress.text = p.ShowAsCount ? p.MetCount + " / " + p.Total + " objectives" : Labels.Percent(p.Overall);
            Color text = UiUtil.TextColor(p.Unlocked);
            Row.StatName.color = text;
            Row.Progress.color = text;
            _bar.Set(p.Overall, UiUtil.BarColor(p.Overall, p.Unlocked), ease);
        }
    }
}
```

- [ ] **Step 4: Write `src/Milestones/UI/DetailsPanel.cs`**

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using Milestones.Core;
using Milestones.Core.Model;
using UnityEngine;

namespace Milestones.UI
{
    /// <summary>
    /// Builds the details list from the progress model with the game's own row prefab, parent and
    /// rows-per-frame batching (PLAN.md §2.2), and refreshes the rows in place while it is open.
    /// </summary>
    internal static class DetailsPanel
    {
        private static AchievementsGui _gui;
        private static ProgressRow _header;
        private static readonly List<ProgressRow> Rows = new List<ProgressRow>();

        public static Achievement Current { get; private set; }

        public static event Action<AchievementsGui, Achievement> Opened;

        static DetailsPanel()
        {
            ProgressCache.Changed += OnChanged;
        }

        public static void Open(AchievementsGui gui, Achievement a)
        {
            _gui = gui;
            Current = a;
            Rows.Clear();
            gui.m_achievementDetails.SetActive(true);

            ProgressCache.Recompute(a);
            AchievementProgress p = ProgressCache.Get(a);

            _header = new ProgressRow(NewRow(gui), ProgressRow.Header);
            _header.ShowOverall(a, p, ease: false);

            List<int> order;
            if (PluginConfig.SortObjectives.Value)
                order = ObjectiveOrder.Sorted(p);
            else
            {
                order = new List<int>(p.Total);
                for (int i = 0; i < p.Total; i++)
                    order.Add(i);
            }
            gui.StartCoroutine(Populate(gui, a, order));

            if (Opened != null)
                Opened(gui, a);
        }

        private static IEnumerator Populate(AchievementsGui gui, Achievement a, List<int> order)
        {
            int batch = Math.Max(1, gui.m_detailStatsPerFrame);
            int n = 0;
            foreach (int i in order)
            {
                if (Current != a || gui == null || !gui.m_achievementDetails.activeSelf)
                    yield break;
                AchievementProgress p = ProgressCache.Get(a);
                if (i >= p.Total)
                    yield break;
                var row = new ProgressRow(NewRow(gui), i);
                row.Show(p.Objectives[i], ease: false);
                Rows.Add(row);
                if (++n == batch)
                {
                    n = 0;
                    yield return null;
                }
            }
        }

        private static AchievementDetailUnlockCondition NewRow(AchievementsGui gui)
        {
            AchievementDetailUnlockCondition row = UnityEngine.Object.Instantiate(gui.m_achievementDetailsElementPrefab, gui.m_achievementDetailsListRoot);
            row.gameObject.SetActive(true);
            return row;
        }

        private static void OnChanged(Achievement a, AchievementProgress p)
        {
            if (a != Current || _gui == null || !_gui.m_achievementDetails.activeSelf)
                return;
            if (_header != null && _header.Alive)
                _header.ShowOverall(a, p, ease: true);
            foreach (ProgressRow row in Rows)
            {
                if (row.Alive && row.ObjectiveIndex < p.Total)
                    row.Show(p.Objectives[row.ObjectiveIndex], ease: true);
            }
        }

        /// <summary>After AchievementsGui.CloseDetails destroyed the rows.</summary>
        public static void Closed()
        {
            Current = null;
            _header = null;
            Rows.Clear();
        }

        /// <summary>Undo a half-built panel so vanilla can build its own (it bails out if the panel is active).</summary>
        public static void Abort(AchievementsGui gui)
        {
            Current = null;
            foreach (Transform child in gui.m_achievementDetailsListRoot)
                UnityEngine.Object.Destroy(child.gameObject);
            gui.m_achievementDetails.SetActive(false);
            _header = null;
            Rows.Clear();
        }
    }
}
```

- [ ] **Step 5: Write `src/Milestones/Patches/DetailsPatches.cs`**

```csharp
using System;
using HarmonyLib;
using Milestones.UI;

namespace Milestones.Patches
{
    [HarmonyPatch(typeof(AchievementsGui), nameof(AchievementsGui.OnOpenAchievementDetails))]
    internal static class OpenDetailsPatch
    {
        private static bool Prefix(AchievementsGui __instance, Achievement achievement, bool clickable)
        {
            if (!PluginConfig.Enabled.Value)
                return true;
            // Same early-outs as vanilla, and locked secrets keep vanilla's ???.
            if (__instance.m_achievementDetails.activeSelf || !clickable)
                return true;
            if (achievement.m_isSecret && !achievement.m_unlocked)
                return true;
            try
            {
                DetailsPanel.Open(__instance, achievement);
                return false;
            }
            catch (Exception e)
            {
                MilestonesPlugin.WarnOnce("details panel", e);
                DetailsPanel.Abort(__instance);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(AchievementsGui), nameof(AchievementsGui.CloseDetails))]
    internal static class CloseDetailsPatch
    {
        private static void Postfix() => DetailsPanel.Closed();
    }
}
```

- [ ] **Step 6: Add `milestones ui` to `ConsoleCommands.cs`**

In the `switch`, add before `default:`:

```csharp
                        case "ui":
                            DumpUi(args.Context);
                            break;
```

and add these methods to the class (with `using UnityEngine;` at the top of the file):

```csharp
        private static void DumpUi(Terminal ctx)
        {
            InventoryGui inv = InventoryGui.instance;
            if (inv == null || inv.m_achievementsPanel == null)
            {
                Say(ctx, "Milestones: no achievements panel.");
                return;
            }
            Dump(ctx, inv.m_achievementsPanel.m_achievementDetails.transform, 0, 3);
            if (inv.m_achievementsPanel.m_achievementDetailsElementPrefab != null)
                Dump(ctx, inv.m_achievementsPanel.m_achievementDetailsElementPrefab.transform, 0, 3);
        }

        private static void Dump(Terminal ctx, Transform t, int depth, int maxDepth)
        {
            var rt = t as RectTransform;
            var comps = new System.Text.StringBuilder();
            foreach (Component c in t.GetComponents<Component>())
            {
                if (c != null && !(c is Transform))
                    comps.Append(c.GetType().Name).Append(' ');
            }
            Say(ctx, string.Format("Milestones: {0}{1} active={2} size={3} anchors={4}-{5} pos={6} [{7}]",
                new string(' ', depth * 2), t.name, t.gameObject.activeSelf,
                rt != null ? rt.rect.size.ToString() : "-", rt != null ? rt.anchorMin.ToString() : "-",
                rt != null ? rt.anchorMax.ToString() : "-", rt != null ? rt.anchoredPosition.ToString() : "-", comps));
            if (depth >= maxDepth)
                return;
            foreach (Transform child in t)
                Dump(ctx, child, depth + 1, maxDepth);
        }
```

- [ ] **Step 7: Build**

Run: `dotnet build src/Milestones/Milestones.csproj -c Release --nologo -v q`
Expected: `Build succeeded`.

- [ ] **Step 8: Rig check and layout tuning**

Deploy, load a world, open Inventory → Achievements, and click a partly done achievement.

1. The first row is the achievement name with `NN%` or `k / n objectives`, and every row has real names and `x / y  z%` with a thin bar at its bottom edge. Unfinished rows are on top, closest to done first.
2. `./build/shot.sh details` captures it. Look at the image: does the bar overlap the text?
3. If it does, run `milestones ui` with the details panel open and read the prefab row's `size` from `./build/logs.sh`. Raise the row's height by adding, in `ProgressRow`'s constructor:
   ```csharp
   var le = row.GetComponent<UnityEngine.UI.LayoutElement>() ?? row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
   le.minHeight = ((RectTransform)row.transform).rect.height + 6f;
   le.preferredHeight = le.minHeight;
   ```
   and re-check. Commit the version that looks right.
4. Open the huge one (all build pieces). It must fill in over a few frames without a hitch, and scrolling must work.
5. Close the panel with Escape and open another: no leftover rows.
6. Open a locked **secret** achievement: it isn't clickable, as in vanilla. Open any unlocked one: all rows green, header 100%.
7. Keep a food achievement's details open. You can't eat with the menu open, so check live refresh with the `achievements` panel open on a server while a stat changes (a companion kills something), or accept that this path is checked through the tracker in Task 10.

- [ ] **Step 9: Commit**

```bash
git add src
git commit -m "Details panel: progress bars, overall header, sorted rows, live refresh"
```

---

### Task 9: Pins, pin button, grid marks, console pin/unpin, auto-unpin

**Files:**
- Create: `src/Milestones/Core/Pins.cs`, `src/Milestones/UI/PinButton.cs`, `src/Milestones/UI/ListMarks.cs`, `src/Milestones/Patches/ListPatches.cs`
- Modify: `src/Milestones/Core/ConsoleCommands.cs` (add `pin`, `unpin`), `src/Milestones/Core/Runtime.cs` (call `Pins.Tick`)

**Interfaces:**
- Consumes: `PinList` (Task 4), `Runtime.PlayerSpawned`, `StatEvents.Unlocked`, `DetailsPanel.Opened/Current`, `AchievementReader.ById/GridOrder`.
- Produces: `Pins.Ids : IReadOnlyList<string>`, `Pins.Count`, `Pins.IsFull`, `Pins.Contains(string id)`, `Pins.Toggle(Achievement) : bool`, `Pins.Pin(string id) : bool`, `Pins.Unpin(string id) : bool`, `Pins.Tick(float now)`, `event Action Pins.Changed`

- [ ] **Step 1: Write `src/Milestones/Core/Pins.cs`**

```csharp
using System;
using System.Collections.Generic;
using Milestones.Core.Model;

namespace Milestones.Core
{
    /// <summary>
    /// Pins live in Player.m_customData["milestones.pins"], which the game saves inside the
    /// character file. The in-memory list is keyed by profile, so a respawn after death (a new
    /// Player loaded from the last save) keeps pins changed since that save.
    /// </summary>
    internal static class Pins
    {
        private const string Key = "milestones.pins";
        private const float UnpinDelay = 10f;

        private static PinList _list = new PinList();
        private static string _profile;
        private static Player _owner;
        private static readonly Dictionary<string, float> UnpinAt = new Dictionary<string, float>();

        public static event Action Changed;

        public static IReadOnlyList<string> Ids => _list.Ids;
        public static int Count => _list.Count;
        public static bool IsFull => _list.IsFull;

        static Pins()
        {
            Runtime.PlayerSpawned += OnPlayerSpawned;
            StatEvents.Unlocked += OnUnlocked;
        }

        public static void Init()
        {
            // Forces the static constructor so the event subscriptions exist before the first spawn.
        }

        private static void OnPlayerSpawned(Player p)
        {
            string profile = Game.instance.GetPlayerProfile().GetFilename();
            if (profile != _profile)
            {
                string raw;
                p.m_customData.TryGetValue(Key, out raw);
                _list = PinList.Parse(raw);
                _profile = profile;
                UnpinAt.Clear();
            }
            _owner = p;
            _list.RemoveWhere(id => AchievementReader.ById(id) == null);
            Save();
            Raise();
        }

        public static bool Contains(string id)
        {
            return _list.Contains(id);
        }

        public static bool Toggle(Achievement a)
        {
            return Contains(a.m_id) ? Unpin(a.m_id) : Pin(a.m_id);
        }

        public static bool Pin(string id)
        {
            if (!_list.Add(id))
                return false;
            Save();
            Raise();
            return true;
        }

        public static bool Unpin(string id)
        {
            UnpinAt.Remove(id);
            if (!_list.Remove(id))
                return false;
            Save();
            Raise();
            return true;
        }

        public static void Tick(float now)
        {
            if (UnpinAt.Count == 0)
                return;
            string due = null;
            foreach (KeyValuePair<string, float> kv in UnpinAt)
            {
                if (now >= kv.Value)
                {
                    due = kv.Key;
                    break;
                }
            }
            if (due != null)
                Unpin(due);
        }

        private static void OnUnlocked(Achievement a)
        {
            if (PluginConfig.AutoUnpinCompleted.Value && Contains(a.m_id))
                UnpinAt[a.m_id] = Runtime.Now + UnpinDelay;
        }

        private static void Save()
        {
            if (_owner == null)
                return;
            if (_list.Count == 0)
                _owner.m_customData.Remove(Key);
            else
                _owner.m_customData[Key] = _list.Serialize();
        }

        private static void Raise()
        {
            if (PluginConfig.Verbose.Value)
                MilestonesPlugin.Log.LogInfo("Milestones: pins = " + _list.Serialize());
            if (Changed != null)
                Changed();
        }
    }
}
```

In `Plugin.Awake()`, after `ConsoleCommands.Register();`, add `Pins.Init();`.

- [ ] **Step 2: Call `Pins.Tick` from `Runtime.Tick`**

In `Runtime.Tick()`, right after `float now = Now;`, add:

```csharp
            Pins.Tick(now);
```

- [ ] **Step 3: Write `src/Milestones/UI/PinButton.cs`**

```csharp
using Milestones.Core;
using Milestones.Core.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Milestones.UI
{
    /// <summary>
    /// "Pin (1/3)" / "Unpin" beside the details panel's close button, cloned from it so it matches
    /// the game's style. Gamepad: Y while the details panel is open.
    /// </summary>
    internal static class PinButton
    {
        private static Button _button;
        private static TMP_Text _label;
        private static Achievement _for;

        public static void Init()
        {
            DetailsPanel.Opened += Attach;
            Pins.Changed += Refresh;
            Runtime.Ticked += PollGamepad;
        }

        private static void Attach(AchievementsGui gui, Achievement a)
        {
            _for = a;
            if (_button == null)
                Create(gui);
            Refresh();
        }

        private static Button FindCloseButton(AchievementsGui gui)
        {
            Button first = null;
            foreach (Button b in gui.m_achievementDetails.GetComponentsInChildren<Button>(true))
            {
                if (b.name == "MilestonesPin")
                    continue;
                if (first == null)
                    first = b;
                for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                {
                    if (b.onClick.GetPersistentMethodName(i) == nameof(AchievementsGui.OnCloseAchievementDetails))
                        return b;
                }
            }
            return first;
        }

        private static void Create(AchievementsGui gui)
        {
            Button template = FindCloseButton(gui);
            if (template == null)
            {
                MilestonesPlugin.Log.LogWarning("Milestones: no button in the details panel to clone; pin with 'milestones pin <id>'.");
                return;
            }
            GameObject go = Object.Instantiate(template.gameObject, template.transform.parent);
            go.name = "MilestonesPin";
            // The clone must not answer the close button's gamepad key or re-localize our label.
            foreach (Component c in go.GetComponentsInChildren<Component>(true))
            {
                if (c != null && (c is UIGamePad || c.GetType().Name == "Localize"))
                    Object.Destroy(c);
            }
            _button = go.GetComponent<Button>();
            _button.onClick = new Button.ButtonClickedEvent();
            _button.onClick.AddListener(OnClick);
            _label = go.GetComponentInChildren<TMP_Text>(true);

            var rt = (RectTransform)go.transform;
            var trt = (RectTransform)template.transform;
            rt.anchoredPosition = trt.anchoredPosition - new Vector2(trt.rect.width + 12f, 0f);
        }

        private static void OnClick()
        {
            if (_for != null)
                Pins.Toggle(_for);
        }

        private static void Refresh()
        {
            if (_button == null || _for == null || _label == null)
                return;
            bool pinned = Pins.Contains(_for.m_id);
            _label.text = pinned ? "Unpin" : Pins.IsFull ? "Unpin one first (3/3)" : "Pin (" + Pins.Count + "/" + PinList.Max + ")";
            _button.interactable = pinned || !Pins.IsFull;
        }

        private static void PollGamepad()
        {
            if (DetailsPanel.Current == null || _for != DetailsPanel.Current)
                return;
            if (ZInput.IsGamepadActive() && ZInput.GetButtonDown("JoyButtonY") && _button != null && _button.interactable)
                OnClick();
        }
    }
}
```

In `Plugin.Awake()`, after `Pins.Init();`, add `UI.PinButton.Init();`.

- [ ] **Step 4: Write `src/Milestones/UI/ListMarks.cs` and `src/Milestones/Patches/ListPatches.cs`**

`ListMarks.cs`:

```csharp
using System.Collections.Generic;
using Milestones.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Milestones.UI
{
    /// <summary>A small amber corner mark on the grid tiles of pinned achievements.</summary>
    internal static class ListMarks
    {
        private const string MarkName = "MilestonesPinMark";

        public static void Init()
        {
            Pins.Changed += () =>
            {
                InventoryGui inv = InventoryGui.instance;
                if (inv != null && inv.IsAchievementsPanelOpen)
                    Apply(inv);
            };
        }

        public static void Apply(InventoryGui inv)
        {
            List<GameObject> tiles = inv.m_achievementsPanel.m_achievementsList;
            List<Achievement> order = AchievementReader.GridOrder();
            if (order.Count != tiles.Count)
                return; // the game changed its grid order; skip the marks rather than mislabel tiles
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i] == null)
                    continue;
                Transform mark = tiles[i].transform.Find(MarkName);
                bool pinned = Pins.Contains(order[i].m_id);
                if (pinned && mark == null)
                {
                    RectTransform rt = UiUtil.Rect(MarkName, tiles[i].transform);
                    rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(1f, 1f);
                    rt.anchoredPosition = new Vector2(-4f, -4f);
                    rt.sizeDelta = new Vector2(12f, 12f);
                    Image img = rt.gameObject.AddComponent<Image>();
                    img.sprite = UiUtil.White;
                    img.color = PluginConfig.ColorInProgress.Value;
                    img.raycastTarget = false;
                }
                else if (!pinned && mark != null)
                {
                    Object.Destroy(mark.gameObject);
                }
            }
        }
    }
}
```

`ListPatches.cs`:

```csharp
using System;
using HarmonyLib;
using Milestones.UI;

namespace Milestones.Patches
{
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateAchievementsList))]
    internal static class UpdateAchievementsListPatch
    {
        private static void Postfix(InventoryGui __instance)
        {
            if (!PluginConfig.Enabled.Value)
                return;
            try
            {
                ListMarks.Apply(__instance);
            }
            catch (Exception e)
            {
                MilestonesPlugin.WarnOnce("grid pin marks", e);
            }
        }
    }
}
```

In `Plugin.Awake()`, after `UI.PinButton.Init();`, add `UI.ListMarks.Init();`.

- [ ] **Step 5: Add `pin` / `unpin` to `ConsoleCommands.cs`**

In the `switch`, add before `default:`:

```csharp
                        case "pin":
                        case "unpin":
                            PinCommand(args.Context, sub.ToLowerInvariant() == "pin", rest);
                            break;
```

and the method:

```csharp
        private static void PinCommand(Terminal ctx, bool pin, string query)
        {
            Achievement a = AchievementReader.Find(query);
            if (a == null)
            {
                Say(ctx, "Milestones: no achievement matches \"" + query + "\".");
                return;
            }
            bool ok = pin ? Pins.Pin(a.m_id) : Pins.Unpin(a.m_id);
            Say(ctx, "Milestones: " + (ok ? (pin ? "pinned " : "unpinned ") : "unchanged ") + a.m_id +
                " (" + Pins.Count + "/" + PinList.Max + ")" + (!ok && pin && Pins.IsFull ? " — three pins already" : ""));
        }
```

Also make `Summary` print the pins first. Add at the start of `Summary`:

```csharp
            Say(ctx, "Milestones: pinned = " + (Pins.Count == 0 ? "(none)" : string.Join(", ", Pins.Ids)));
```

- [ ] **Step 6: Build**

Run: `dotnet build src/Milestones/Milestones.csproj -c Release --nologo -v q`
Expected: `Build succeeded`.

- [ ] **Step 7: Rig check**

1. Open an achievement's details. A **Pin (0/3)** button sits left of the close button with the game's styling (`./build/shot.sh pin`). If it overlaps something, adjust the offset in `PinButton.Create`, using `milestones ui` for sizes.
2. Click it: it reads **Unpin**. Close the details: the tile has an amber corner mark.
3. Pin two more, open a fourth: **Unpin one first (3/3)**, greyed out.
4. With a gamepad, open details and press Y: it toggles. Check that B still closes the panel and A still opens tiles.
5. Log out to the main menu and back in: the same 3 pins (`milestones` prints them).
6. Switch to another character: no pins (or that character's own). Switch back: the originals.
7. Die and respawn: pins unchanged.
8. `milestones unpin <name>` and `milestones pin <name>` work, and the grid mark updates if the panel is open.

- [ ] **Step 8: Commit**

```bash
git add src
git commit -m "Pins: per-character storage, pin button, grid marks, console, auto-unpin"
```

---

### Task 10: HUD tracker

**Files:**
- Create: `src/Milestones/UI/TrackerHud.cs`
- Modify: `src/Milestones/Plugin.cs` (init)

**Interfaces:**
- Consumes: `Pins.Ids/Changed`, `ProgressCache.Get/Changed`, `ObjectiveOrder.Select`, `Labels`, `CheatState.PausedReason`, `Runtime.Ticked`, `Bar.CreateInLayout`, `UiUtil`, `PluginConfig` tracker entries.
- Produces: `TrackerHud.Init()`.

- [ ] **Step 1: Write `src/Milestones/UI/TrackerHud.cs`**

```csharp
using System.Collections.Generic;
using Milestones.Core;
using Milestones.Core.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Milestones.UI
{
    /// <summary>
    /// The pinned-achievement widget. Lives under Hud.m_rootObject, so F3 and photo mode hide it
    /// with the rest of the HUD. Rebuilt only when the pin set or a pinned achievement changes;
    /// positioned every frame (cheap) so it follows the status-effect icons.
    /// </summary>
    internal sealed class TrackerHud : MonoBehaviour
    {
        // Gap under the status-effect icons, in HUD units: clears the effect name under each icon.
        private const float StatusGap = 26f;

        private static TrackerHud _instance;
        private static bool _dirty = true;

        private RectTransform _panel;
        private CanvasGroup _group;
        private readonly Dictionary<string, BlockView> _blocks = new Dictionary<string, BlockView>();
        private TextMeshProUGUI _paused;
        private float _nextPausedCheck;
        private readonly Vector3[] _corners = new Vector3[4];

        private sealed class LineView
        {
            public RectTransform Root;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Value;
            public Bar Bar;
            public int ObjectiveIndex = -1;
        }

        private sealed class BlockView
        {
            public RectTransform Root;
            public Image Icon;
            public TextMeshProUGUI Title;
            public TextMeshProUGUI Overall;
            public Bar OverallBar;
            public readonly List<LineView> Lines = new List<LineView>();
            public TextMeshProUGUI More;
        }

        public static void Init()
        {
            Runtime.Ticked += Ensure;
            Pins.Changed += () => _dirty = true;
            ProgressCache.Changed += (a, p) =>
            {
                if (Pins.Contains(a.m_id))
                    _dirty = true;
            };
            Runtime.PlayerSpawned += p => _dirty = true;
            PluginConfig.MaxRowsPerAchievement.SettingChanged += (s, e) => Rebuild();
            PluginConfig.Width.SettingChanged += (s, e) => Rebuild();
        }

        private static void Rebuild()
        {
            if (_instance != null)
            {
                foreach (BlockView b in _instance._blocks.Values)
                    Destroy(b.Root.gameObject);
                _instance._blocks.Clear();
            }
            _dirty = true;
        }

        private static void Ensure()
        {
            if (_instance != null)
                return;
            Hud hud = Hud.instance;
            if (hud == null || hud.m_rootObject == null)
                return;
            RectTransform holder = UiUtil.Rect("MilestonesTracker", hud.m_rootObject.transform);
            UiUtil.Stretch(holder);
            _instance = holder.gameObject.AddComponent<TrackerHud>();
            _instance.Build();
            _dirty = true;
        }

        private void Build()
        {
            _panel = UiUtil.Rect("panel", transform);
            _panel.pivot = new Vector2(1f, 1f);
            Image bg = _panel.gameObject.AddComponent<Image>();
            bg.sprite = UiUtil.White;
            bg.color = new Color(0f, 0f, 0f, 0.45f);
            bg.raycastTarget = false;
            _group = _panel.gameObject.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;

            VerticalLayoutGroup v = _panel.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(8, 8, 6, 6);
            v.spacing = 6f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            ContentSizeFitter fit = _panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            _paused = UiUtil.Text(_panel, "paused", 12f, TextAlignmentOptions.Left);
            _paused.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            _paused.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            bool show = PluginConfig.Enabled.Value && PluginConfig.ShowTracker.Value && Pins.Count > 0
                && Player.m_localPlayer != null
                && !(PluginConfig.HideWhenInventoryOpen.Value && InventoryGui.IsVisible());
            if (_panel.gameObject.activeSelf != show)
                _panel.gameObject.SetActive(show);
            if (!show)
                return;

            if (_dirty)
            {
                _dirty = false;
                Refresh();
            }
            if (Runtime.Now >= _nextPausedCheck)
            {
                _nextPausedCheck = Runtime.Now + 1f;
                string reason = CheatState.PausedReason();
                _paused.gameObject.SetActive(reason != null);
                if (reason != null)
                    _paused.text = "Achievements paused: " + reason;
            }

            _group.alpha = PluginConfig.Opacity.Value;
            _panel.sizeDelta = new Vector2(PluginConfig.Width.Value, _panel.sizeDelta.y);
            _panel.localScale = Vector3.one * PluginConfig.Scale.Value;
            Place();
        }

        private void Place()
        {
            TrackerAnchor anchor = PluginConfig.Anchor.Value;
            var offset = new Vector2(PluginConfig.OffsetX.Value, PluginConfig.OffsetY.Value);
            if (anchor == TrackerAnchor.UnderStatusEffects)
            {
                Hud hud = Hud.instance;
                Canvas canvas = GetComponentInParent<Canvas>();
                float k = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
                RectTransform root = hud.m_statusEffectListRoot;
                float right = root.position.x, bottom = root.position.y;
                foreach (RectTransform se in hud.m_statusEffects)
                {
                    if (se == null || !se.gameObject.activeSelf)
                        continue;
                    se.GetWorldCorners(_corners);
                    right = Mathf.Max(right, _corners[2].x);
                    bottom = Mathf.Min(bottom, _corners[0].y);
                }
                _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
                _panel.pivot = new Vector2(1f, 1f);
                _panel.position = new Vector3(right + offset.x * k, bottom - StatusGap * k + offset.y * k, 0f);
                return;
            }

            Vector2 a;
            switch (anchor)
            {
                case TrackerAnchor.TopLeft: a = new Vector2(0f, 1f); break;
                case TrackerAnchor.TopRight: a = new Vector2(1f, 1f); break;
                case TrackerAnchor.LeftMiddle: a = new Vector2(0f, 0.5f); break;
                case TrackerAnchor.RightMiddle: a = new Vector2(1f, 0.5f); break;
                case TrackerAnchor.BottomLeft: a = new Vector2(0f, 0f); break;
                default: a = new Vector2(1f, 0f); break;
            }
            _panel.anchorMin = _panel.anchorMax = a;
            _panel.pivot = a;
            var inset = new Vector2(a.x < 0.5f ? 16f : a.x > 0.5f ? -16f : 0f, a.y < 0.5f ? 16f : a.y > 0.5f ? -16f : 0f);
            _panel.anchoredPosition = inset + offset;
        }

        private void Refresh()
        {
            var keep = new HashSet<string>(Pins.Ids);
            var remove = new List<string>();
            foreach (string id in _blocks.Keys)
            {
                if (!keep.Contains(id))
                    remove.Add(id);
            }
            foreach (string id in remove)
            {
                Destroy(_blocks[id].Root.gameObject);
                _blocks.Remove(id);
            }

            int sibling = 0;
            foreach (string id in Pins.Ids)
            {
                Achievement a = AchievementReader.ById(id);
                if (a == null)
                    continue;
                AchievementProgress p = ProgressCache.Get(a);
                if (p == null)
                    continue;
                BlockView b;
                bool fresh = !_blocks.TryGetValue(id, out b);
                if (fresh)
                {
                    b = NewBlock(a);
                    _blocks[id] = b;
                }
                b.Root.SetSiblingIndex(sibling++);
                Fill(b, a, p, ease: !fresh);
            }
            _paused.transform.SetAsLastSibling();
        }

        private BlockView NewBlock(Achievement a)
        {
            var b = new BlockView();
            b.Root = UiUtil.Rect("block_" + a.m_id, _panel);
            VerticalLayoutGroup v = b.Root.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 2f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            RectTransform head = UiUtil.Rect("head", b.Root);
            HorizontalLayoutGroup h = head.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6f;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            h.childAlignment = TextAnchor.MiddleLeft;

            RectTransform iconRt = UiUtil.Rect("icon", head);
            b.Icon = iconRt.gameObject.AddComponent<Image>();
            b.Icon.raycastTarget = false;
            b.Icon.preserveAspect = true;
            LayoutElement ile = iconRt.gameObject.AddComponent<LayoutElement>();
            ile.preferredWidth = ile.minWidth = 20f;
            ile.preferredHeight = ile.minHeight = 20f;

            b.Title = UiUtil.Text(head, "title", 15f, TextAlignmentOptions.Left);
            b.Title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            b.Overall = UiUtil.Text(head, "overall", 13f, TextAlignmentOptions.Right);

            b.OverallBar = Bar.CreateInLayout(b.Root, 6f);

            for (int i = 0; i < PluginConfig.MaxRowsPerAchievement.Value; i++)
                b.Lines.Add(NewLine(b.Root, i));

            b.More = UiUtil.Text(b.Root, "more", 11f, TextAlignmentOptions.Left);
            b.More.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            return b;
        }

        private static LineView NewLine(RectTransform parent, int i)
        {
            var l = new LineView();
            l.Root = UiUtil.Rect("line" + i, parent);
            VerticalLayoutGroup v = l.Root.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 1f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            RectTransform row = UiUtil.Rect("text", l.Root);
            HorizontalLayoutGroup h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            l.Name = UiUtil.Text(row, "name", 12f, TextAlignmentOptions.Left);
            l.Name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            l.Value = UiUtil.Text(row, "value", 12f, TextAlignmentOptions.Right);

            l.Bar = Bar.CreateInLayout(l.Root, 3f);
            return l;
        }

        private static void Fill(BlockView b, Achievement a, AchievementProgress p, bool ease)
        {
            b.Icon.sprite = p.Unlocked ? a.m_icon : a.m_iconLocked;
            b.Title.text = AchievementReader.Name(a);
            b.Overall.text = p.Unlocked ? "Done " + Labels.Check : Labels.OverallText(p);
            b.Overall.color = UiUtil.TextColor(p.Unlocked);
            b.OverallBar.Set(p.Overall, UiUtil.BarColor(p.Overall, p.Unlocked), ease);

            TrackerRows rows = p.Unlocked ? new TrackerRows() : ObjectiveOrder.Select(p, b.Lines.Count);
            for (int i = 0; i < b.Lines.Count; i++)
            {
                LineView l = b.Lines[i];
                bool on = i < rows.Indices.Count;
                l.Root.gameObject.SetActive(on);
                if (!on)
                {
                    l.ObjectiveIndex = -1;
                    continue;
                }
                int idx = rows.Indices[i];
                Objective o = p.Objectives[idx];
                l.Name.text = PluginConfig.HideUnmetNames.Value && !o.Met ? "???" : o.Label;
                l.Value.text = o.HasBar ? Labels.Amount(System.Math.Min(o.Current, o.Target)) + " / " + Labels.Amount(o.Target) : (o.Met ? "met" : "not met");
                Color c = UiUtil.TextColor(o.Met);
                l.Name.color = c;
                l.Value.color = c;
                // Ease only when the slot still shows the same objective; a new occupant snaps.
                l.Bar.Set(o.Fraction, UiUtil.BarColor(o), ease && l.ObjectiveIndex == idx);
                l.ObjectiveIndex = idx;
            }
            b.More.gameObject.SetActive(rows.More > 0);
            b.More.text = "+" + rows.More + " more";
        }
    }
}
```

In `Plugin.Awake()`, after `UI.ListMarks.Init();`, add `UI.TrackerHud.Init();`.

- [ ] **Step 2: Build**

Run: `dotnet build src/Milestones/Milestones.csproj -c Release --nologo -v q`
Expected: `Build succeeded`.

- [ ] **Step 3: Rig check**

Pin three achievements: one small, one huge (all foods or all build pieces), and one that tracks something you can do immediately (berries, trees, crafting).

1. The tracker sits right-aligned directly under the status-effect icons (`./build/shot.sh tracker`). If it overlaps the effect-name labels, raise `StatusGap`, and if it's too far below, lower it. Commit the tuned value.
2. Drink two or three meads so the icons wrap into a second row (or get 8+ effects): the tracker moves down with them.
3. Pick a berry (or chop, craft, eat): the matching bar moves within about half a second and eases rather than jumping.
4. The huge achievement shows 4 unfinished rows, closest to done first, then `+N more`.
5. Open the inventory: the tracker hides. Close it: it's back. Press F3 (hide HUD): it hides.
6. In ConfigurationManager (F1), change Anchor through each value, then Offset, Scale, Opacity and Width: every change applies live.
7. Unpin everything: the tracker disappears.

- [ ] **Step 4: Commit**

```bash
git add src
git commit -m "HUD tracker under the status effects, live eased bars"
```

---

### Task 11: Toasts

**Files:**
- Create: `src/Milestones/UI/Toasts.cs`
- Modify: `src/Milestones/Plugin.cs` (init)

**Interfaces:**
- Consumes: `ToastDiff`, `Snapshot`, `ToastRules` (Task 5), `ProgressCache.Changed/Get`, `Runtime.PlayerSpawned`, `Pins.Contains`, `AchievementReader`.
- Produces: `Toasts.Init()`.

- [ ] **Step 1: Write `src/Milestones/UI/Toasts.cs`**

```csharp
using System.Collections.Generic;
using Milestones.Core;
using Milestones.Core.Model;

namespace Milestones.UI
{
    /// <summary>
    /// Snapshots every achievement when a player spawns, so a login never replays old progress,
    /// then turns each ProgressCache change into at most one line through ToastDiff.
    /// </summary>
    internal static class Toasts
    {
        private static readonly Dictionary<string, Snapshot> Seen = new Dictionary<string, Snapshot>();
        private static ToastRules _rules;

        public static void Init()
        {
            Runtime.PlayerSpawned += p => Reset();
            ProgressCache.Changed += OnChanged;
            PluginConfig.Thresholds.SettingChanged += (s, e) => _rules = null;
            PluginConfig.PinnedToasts.SettingChanged += (s, e) => _rules = null;
            PluginConfig.UnpinnedToasts.SettingChanged += (s, e) => _rules = null;
        }

        private static ToastRules Rules
        {
            get
            {
                if (_rules == null)
                {
                    _rules = new ToastRules
                    {
                        Pinned = PluginConfig.PinnedToasts.Value,
                        Unpinned = PluginConfig.UnpinnedToasts.Value,
                        Thresholds = ToastRules.ParseThresholds(PluginConfig.Thresholds.Value)
                    };
                }
                return _rules;
            }
        }

        private static void Reset()
        {
            Seen.Clear();
            foreach (Achievement a in AchievementReader.All())
            {
                AchievementProgress p = ProgressCache.Get(a);
                if (p != null)
                    Seen[a.m_id] = Snapshot.Take(p);
            }
        }

        private static void OnChanged(Achievement a, AchievementProgress now)
        {
            Snapshot before;
            Seen.TryGetValue(a.m_id, out before);
            Seen[a.m_id] = Snapshot.Take(now);
            if (before == null || !PluginConfig.Enabled.Value)
                return;
            string line = ToastDiff.Diff(before, now, AchievementReader.Name(a), Pins.Contains(a.m_id), Rules);
            if (line == null || MessageHud.instance == null)
                return;
            MessageHud.MessageType where = PluginConfig.ToastAt.Value == ToastPosition.Center
                ? MessageHud.MessageType.Center
                : MessageHud.MessageType.TopLeft;
            MessageHud.instance.ShowMessage(where, line);
            if (PluginConfig.Verbose.Value)
                MilestonesPlugin.Log.LogInfo("Milestones: toast: " + line);
        }
    }
}
```

In `Plugin.Awake()`, after `UI.TrackerHud.Init();`, add `UI.Toasts.Init();`. Order matters: `Toasts.Reset` runs on `PlayerSpawned` after `Pins` has loaded (Pins subscribed first, in `Pins.Init`). That doesn't change the snapshots, but it keeps the verbose log in a sensible order.

- [ ] **Step 2: Build and run all tests**

Run: `dotnet build src/Milestones/Milestones.csproj -c Release --nologo -v q && dotnet test tests/Milestones.Tests --nologo -v q`
Expected: `Build succeeded`, `Passed: 49`.

- [ ] **Step 3: Rig check**

1. Log in: **no** toasts appear.
2. With a food achievement pinned, eat a food you've never eaten: a top-left line `Milestones: <food> ✓ · <achievement> k / n`. **Check that the ✓ renders.** If it shows as a box or blank, change `Labels.Check` to `"*"`, re-run `dotnet test` (the tests use `✓` literally, so update the expected strings in `ToastDiffTests` to `*`), rebuild, and re-check.
3. Unpin it and eat another new food: no toast, unless that food takes it across 25/50/75 %, in which case you see `Milestones: <achievement> 50%`.
4. Set `ToastPosition = Center`: the next toast appears centre-screen.
5. Cut a few trees quickly with a tree achievement pinned: toasts don't stack into a burst of more than one line per quarter second.

- [ ] **Step 4: Commit**

```bash
git add src tests
git commit -m "Toasts: objective and threshold lines through the game's message feed"
```

---

### Task 12: Release packaging

**Files:**
- Create: `README.md`, `CHANGELOG.md`, `thunderstore/manifest.json`, `thunderstore/README.md`, `thunderstore/icon.png`
- Modify: `build/make_icon.py`

**Interfaces:** none (packaging only).

- [ ] **Step 1: Rewrite the icon drawing in `build/make_icon.py`**

Keep its PNG writer, `blend`, `rounded_rect` and supersampling helpers. Replace only the drawing section (everything between the helper definitions and the PNG write) with this design: a dark plate (`#1c1a17`) with a golden border (`#c8a050`), three horizontal progress bars stacked vertically (widths 100 %, 62 %, 25 %, colored `#5ac85a`, `#e0a030`, `#e0a030` over a `#000000` 55 % track), and a small amber pin triangle in the top-right corner. Use `rounded_rect` for everything. Then:

```bash
python3 build/make_icon.py
python3 -c "import struct;h=open('thunderstore/icon.png','rb').read(24);print(struct.unpack('>II',h[16:24]))"
```

Expected: `(256, 256)`. Copy the image to the rig and look at it (`scp thunderstore/icon.png user@rig:/tmp/` and open it), because the build box has no image viewer.

- [ ] **Step 2: `thunderstore/manifest.json`**

```json
{
  "name": "Milestones",
  "version_number": "0.1.0",
  "website_url": "https://github.com/jumpingmushroom/Milestones",
  "dependencies": [
    "denikson-BepInExPack_Valheim-5.4.2350"
  ],
  "description": "Real progress for Valheim's achievements: bars and numbers instead of ??? / ???, a pinned HUD tracker that updates as you play, and progress toasts. Client-side."
}
```

- [ ] **Step 3: README and CHANGELOG**

`CHANGELOG.md`:

```markdown
# Changelog

## 0.1.0 — first cut

- The achievement details panel shows every objective with a bar, `current / total` and a
  percentage instead of `??? / ???`, plus an overall bar per achievement (objectives met for
  "do everything once" achievements, average fill otherwise, the game's own 1.15x rule for the
  lenient build achievements). Unfinished objectives sort to the top. Achievements that require
  other achievements now list them.
- Pin up to three achievements per character from the details panel (Y on a gamepad). A HUD
  tracker under the status-effect icons shows them and moves as you play. Large achievements
  show the four unfinished objectives closest to done. Unlocked pins clear themselves after ten
  seconds.
- Toasts in the game's message feed: each completed objective of a pinned achievement, and
  25 / 50 / 75 % for every achievement.
- When the game has stopped counting achievement stats (cheats, cheated items, world
  modifiers, or another mod setting `Game.isModded`), the tracker says so.
- `milestones`, `milestones <name>`, `milestones pin|unpin <name>`, `milestones ui` console
  commands.
```

`README.md` (repo) and `thunderstore/README.md` (package page) share their content. Write `thunderstore/README.md` with these sections in order: a one-paragraph pitch; **Features** (the four CHANGELOG bullets, condensed); **Screenshots** (the cropped images from Step 4, referenced by raw GitHub URL at the `v0.1.0` tag, as CoolCount's README does: open `../CoolCount/thunderstore/README.md` and copy its image-URL pattern); **Configuration** (a table of every entry in `PluginConfig` with its default and one-line meaning, taken from the `ConfigDescription` strings); **Console**; **Compatibility** ("client-side, safe to add or remove at any time; pins are stored in the character file and ignored by the game if the mod is removed; needs no server install"); **Achievements paused?** (explain `Game.isModded` and cheats in two sentences). Then `cp thunderstore/README.md README.md` and change the image URLs in the repo copy to relative `docs/images/...` paths.

- [ ] **Step 4: Screenshots**

On the rig, with the character that has pins:

```bash
./build/shot.sh details     # details panel of a partly done achievement
./build/shot.sh tracker     # gameplay with the tracker and a status-effect row
./build/shot.sh toast       # right after eating a new food with the food achievement pinned
```

Crop each one with `./build/crop.sh docs/images/<name>.png <name> <WxH+X+Y>`, choosing a geometry that frames the panel, tracker or toast (read the full frame first to pick coordinates). Only the cropped JPEGs are committed. `probe*.png` files are gitignored.

- [ ] **Step 5: Package**

Run: `./build/package.sh`
Expected: `ok: Milestones 0.1.0, 1 dependencies`, then a listing of `dist/Milestones-0.1.0.zip` containing `plugins/Milestones/Milestones.dll`, `manifest.json`, `README.md`, `icon.png`, `CHANGELOG.md` and `LICENSE`.

- [ ] **Step 6: Commit**

```bash
git add README.md CHANGELOG.md thunderstore build/make_icon.py docs/images
git commit -m "Release 0.1.0: README, changelog, icon, screenshots"
```

Publishing (`build/publish.sh`) and tagging `v0.1.0` are **not** part of this plan. Ask the user first: publishing to Thunderstore is public and a version number can never be reused.

---

## Self-review notes

- **Spec coverage:** §1.1 operators/missing keys → Task 2. Dynamic lists → Tasks 2, 3 and 10. Lenient formula → Task 2. Meta rows → Task 6 (`OtherAchievement`). §1.2 details takeover → Task 8. §1.3 stat hooks → Task 6. §1.4 paused line → Tasks 6 and 10. §2.1 → Tasks 2, 3 and 6. §2.2 → Task 8. §2.3 → Task 9. §2.4 → Task 10. §2.5 → Tasks 5 and 11. §2.6 → Task 1. §2.7 → Tasks 6, 8 and 9. §3 layout → file map. §4 order → task order.
- **Spec deviation, flagged:** §2.3 describes a *tooltip* "Unpin one first". The plan uses the button label `Unpin one first (3/3)` instead, since the cloned close button may carry no tooltip component. Update PLAN.md if the user accepts this.
- **Known approximation:** the lenient overall uses the *recorded* stats 171+ (the maximum ever counted). The game's live unlock check uses the pieces currently standing near you (`Piece.m_tagStats`). So after demolishing, the bar can read higher than what the live check would count. It never reads lower.
- **Verified before hand-off (2026-09-23):** every code block in this plan, with the Task 6/8/9
  insertions applied, was assembled into a scratch copy. The plugin built against the 1.0.15
  game assemblies with no errors or warnings, and `dotnet test` passed 49/49. What remains
  unverified is runtime behavior in Unity: layout, the cloned button, the tracker position and
  the ✓ glyph. Each task's rig check covers these.
- **Left descriptive on purpose:** the icon drawing (Task 12 Step 1) and the README prose are
  specified by content, not given verbatim.
