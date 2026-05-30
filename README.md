# OC2 Beach Menu Predictor

**OC2 Beach Menu Predictor** is an experimental BepInEx mod for *Overcooked! 2*. It was designed for the Beach Special / Beach 3-4 style level and displays a compact rolling preview of upcoming beach orders.

This repository contains the source code and a PowerShell build script. It is mainly intended for private testing, casual practice, and learning how *Overcooked! 2* BepInEx/Harmony mods work.

## Features

- Shows a compact in-game overlay with upcoming beach-level orders.
- Keeps a rolling preview queue, so newly generated orders replace orders that have already appeared.
- Includes a force/lock queue mode for private testing, where the displayed queue is intended to match the generated order queue.
- Uses a cleaner UI overlay to reduce screen obstruction.
- Designed for BepInEx + Harmony modding environments.

## Supported Level

This project was built around the Beach Special / Beach 3-4 style level.

It is not a universal menu predictor. Other normal levels, DLC levels, custom maps, arcade tools, or heavily modified order-generation systems may require separate adaptation.

## Installation

1. Install BepInEx for *Overcooked! 2*.
2. Build the project with the included `build.ps1` script.
3. Copy the generated DLL into:

```text
Overcooked! 2/BepInEx/plugins/
```

Example build command:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -GameDir "D:\SteamLibrary\steamapps\common\Overcooked! 2" -Install
```

## Controls

Default controls:

```text
2 = Show / hide the overlay
3 = Rebuild the prediction queue
4 = Clear the current queue
```

The exact keys can be changed through the BepInEx configuration file after the first launch.

## Compatibility Notes

This mod may conflict with other mods that modify:

- order generation;
- recipe weights;
- level loading;
- arcade lobby behaviour;
- custom level systems.

If the preview queue does not match the actual orders, test the mod in a clean BepInEx setup first.

## Intended Use

This mod is intended for:

- private lobbies;
- casual practice;
- order-generation testing;
- personal modding experiments.

It is not intended for public matchmaking, competitive scoring, leaderboard submissions, or any situation where other players have not agreed to use gameplay-altering mods.

## Disclaimer

This is an unofficial fan-made mod. It is not affiliated with, endorsed by, sponsored by, or approved by Team17, Ghost Town Games, or any official *Overcooked! 2* developer, publisher, or rights holder.

Use this mod at your own risk. The author is not responsible for crashes, corrupted saves, multiplayer desynchronisation, broken lobbies, lost scores, account issues, or any other damage or inconvenience caused by installing or using this mod.

This mod may alter gameplay behaviour. Do not use it in public lobbies, competitive contexts, leaderboard runs, or with players who have not consented to modded gameplay.

All trademarks, game names, assets, and related intellectual property belong to their respective owners. This project does not include or redistribute official game assets.

## Status

Experimental. Built for private testing and may require per-level adaptation.