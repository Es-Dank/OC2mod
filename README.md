# OC2 Mod Tools

This repository contains experimental BepInEx / Harmony mods for *Overcooked! 2*.

These tools are intended for private testing, custom lobbies, practice, and learning. They are unofficial fan-made mods and are not affiliated with Team17, Ghost Town Games, or the official *Overcooked! 2* developers.

## Projects

### OC2 Beach Menu Predictor

A beach-level menu predictor originally built around the Beach Special / Beach 3-4 style level.

Features include:

- compact in-game overlay;
- rolling upcoming-order preview;
- force/lock queue mode for private testing;
- BepInEx + Harmony based patching.

This mod is experimental and not a universal predictor for every level.

### Test 1 Recipe Counter

Path:

```text
OC2Test1RecipeCounter/
```

A recipe counter designed specifically for the custom DIYLevel test map:

```text
s_test_level / Test 1
```

Main behaviour:

- counts generated orders by menu name;
- supports four-player mode and two-player mode;
- two-player slot 1 = four-player slot 1 + four-player slot 4;
- two-player slot 2 = four-player slot 2 + four-player slot 3;
- if both four-player and two-player display modes are disabled, entering Test 1 does not show the overlay;
- displays only the selected position menu list in-game.

See `OC2Test1RecipeCounter/README_CN.md` for build and usage details.

### Player Slot Mapper

Path:

```text
OC2PlayerSlotMapper/
```

A pre-level player-position mapper. It does not perform real-time in-level swapping.

Main behaviour:

- lets the host assign which colour/player should occupy slots 1–4;
- applies only when entering a level;
- changing settings requires exiting the current level and entering again;
- default mapping:
  - slot 1 = blue;
  - slot 2 = red;
  - slot 3 = green;
  - slot 4 = yellow.

See `OC2PlayerSlotMapper/README_CN.md` for build and usage details.

## General installation

Each project includes its own `build.ps1`.

Example:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Overcooked! 2" -Install
```

The generated DLL is copied to:

```text
Overcooked! 2/BepInEx/plugins/
```

## Compatibility notes

These mods may conflict with other mods that modify:

- order generation;
- recipe weights;
- player/chef assignment;
- level loading;
- arcade lobby behaviour;
- custom level systems.

Test one mod at a time in a clean BepInEx setup if behaviour is inconsistent.

## Disclaimer

These are unofficial fan-made mods. They are not affiliated with, endorsed by, sponsored by, or approved by Team17, Ghost Town Games, or any official *Overcooked! 2* developer, publisher, or rights holder.

Use these mods at your own risk. The author is not responsible for crashes, corrupted saves, multiplayer desynchronisation, broken lobbies, lost scores, account issues, or any other damage or inconvenience caused by installing or using them.

Do not use gameplay-altering mods in public lobbies, competitive contexts, leaderboard runs, or with players who have not consented to modded gameplay.

All trademarks, game names, assets, and related intellectual property belong to their respective owners. This repository does not include or redistribute official game assets.
