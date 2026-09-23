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
