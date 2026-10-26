> [!CAUTION]
> This is an unofficial, experimental extension. ppy does not allow rulesets that are not game modes or unofficial mirror downloads inside the game. This project falls within those restrictions. See [peppy's statement](https://osu.ppy.sh/community/forums/topics/2208577).
>
> Compatibility targets **osu!lazer 2026.1005.0 stable**. Other versions and platforms have not been fully verified.

# osu!lazer Sayobot Downloader

Download beatmaps from **Sayobot** and import them automatically within osu!lazer. Keep playing osu! / taiko / catch / mania and use the usual download buttons. Failed mirror downloads can fall back to the official source.

Supports downloads without video, system proxies, manual beatmap set IDs or links, and English, Traditional Chinese, Simplified Chinese, and Japanese interfaces. Up to two beatmap sets can download at once, with progress and cancellation in the notification panel.

Downloads and imports run inside the game process using public APIs, without accessing game members through reflection or launching an external downloader. Sayobot requests do not include your osu! account's tokens or cookies.

## Install

1. Extract `osu.Game.Rulesets.Sayobot.dll` from the installation package.
2. Open your osu! data folder from lazer's settings and place the DLL in its `rulesets` subfolder. Create the subfolder if needed.
3. Restart lazer and search for **Sayobot** in settings to configure downloads.
4. Download beatmaps as usual, or enter a beatmap set ID such as `41823` in the manual download field.

Only the ruleset DLL is needed. To update, replace it and restart lazer. To uninstall, delete it and restart lazer.

## Build

Requires **.NET 10 SDK**, **Python 3**, and **unsquashfs** on Linux.

```sh
./scripts/build.sh
```

The script prepares reference assemblies from the targeted stable release, builds the project, runs offline tests, and writes the DLL and installation package to `artifacts/`. It uses `.tools/dotnet/dotnet` when that SDK is available in the workspace.

You can also build against your own extracted lazer assemblies:

```sh
dotnet build osu.Game.Rulesets.Sayobot/osu.Game.Rulesets.Sayobot.csproj -c Release \
  -p:LazerReferenceDirectory=/absolute/path/to/lazer/assemblies
```

See [VALIDATION.md](VALIDATION.md) for test coverage and known limitations.

## AI Usage

**GPT 6.1Sol Xhigh** was used to assist with code, tests, and documentation.
