![咖啡伴侣](media/banner.webp)

# 咖啡伴侣

[简体中文](README.md) · [繁體中文](README.zh-Hant.md) · **English** · [日本語](README.ja.md) · [한국어](README.ko.md)

A BepInEx 6 plugin for *Legend of Mortal* (活侠传), formerly known as 「活侠传拓展」. The name 咖啡伴侣 ("coffee companion") comes from 「咖啡」 (coffee), the players' nickname for the heroine 瑞杏 (Ruixing), whose name sounds like the Chinese coffee chain Luckin Coffee (瑞幸, Ruixing). It supports the latest version of the game and adds a story overview, branch picking, a stat editor, a better save/load screen, save-anywhere and more.

The mod's own windows are shown in Simplified Chinese, Traditional Chinese, Korean or Japanese (no English UI). Screenshots below are in Chinese.

[![Download](https://img.shields.io/github/v/release/kevinnie2003/LOMAssistant?label=Download&color=A3302A&logo=github)](https://github.com/kevinnie2003/LOMAssistant/releases/latest/download/LOMAssistant.zip) [![Downloads](https://img.shields.io/github/downloads/kevinnie2003/LOMAssistant/total?label=Downloads&color=555)](https://github.com/kevinnie2003/LOMAssistant/releases)

## Install

The zip already contains BepInEx.

**Installer (recommended)**: unzip and run `咖啡伴侣安装器.exe`, then click 「一键安装 / 更新」 (Install / Update).

- It finds the game folder and downloads the latest version (or installs the bundled one if GitHub is unreachable).
- Old mods known to conflict are moved to `BepInEx\已停用的Mod` (not deleted). Your settings are kept.
- Run it again to update. If Windows SmartScreen warns you, click "More info" → "Run anyway".

**Manual**: in Steam, right-click *Legend of Mortal* → Manage → Browse local files, and extract everything in the zip into the game folder, overwriting files. Remove older mods of the same kind (骰子大师, 多功能 Mod, 赵活系统) from `BepInEx\plugins` first; they patch the same game code.

**Uninstall**: run the installer and click 「卸载本 Mod」 (Uninstall). Saves are not touched.

## Hotkeys

| Key | Action |
|---|---|
| F8 | Settings; on the save screen, edit the note of the selected save |
| F5 / F7 | Save-anywhere: make a new save / load the latest one |
| F6 | The save-anywhere page (all saves; load, note, delete) |
| F2 | Skip read dialogue (like holding Ctrl) until an unread line |
| F4 | Roll back to before the last story check |
| F9 | Stats page |
| Tab | The 咖啡伴侣 window |
| ESC | The game menu gets 「空过」 (skip turn), 「直接获胜」 (win now) and 「导出诊断」 (export diagnostics) |

All keys can be changed on the settings page.

## Features

### Story

- **Story overview** (Tab):
  - **Now**: every option and dice outcome of the current scene with its result, whether the following dialogue has been read, branch conditions, random events at the current location (pick the next one directly), and your current quests with what is still missing.
  - **Route map**: the whole main story laid out on a calendar, side quests below; click a stop to see when, where and under which conditions it triggers. Searchable by quest, place and dialogue.
  - **Story map**: one quest drawn as a flow chart, with the options and conditions on every path and how stats change along the way.
  - **Branch saves**: progress is saved automatically at the start of key scenes so you can replay them with 「从这里重玩」. Stored in the save folder as `LOMStory_*` files and synced by Steam Cloud.
  - **Endings**: endings, deaths and achievements, with how to reach the ones you are missing.
- **Pick dice outcomes**: click a line on the dice wheel to choose that result.
- **Option results**: dialogue options show what they will change, and locked options show their real unlock conditions. 「一键设置」 next to a locked option sets the missing conditions so it unlocks.
- **Branch panel** (off by default, spoilers): shows each branch condition, green when met.
- **Hide UI for CGs**: right-click during the story to hide the dialogue box and the mod's windows.
- **Pick random events** (off by default): choose which random event triggers when working, wandering and so on.
- **Flag change notices** (off by default, spoilers).

### Saves

- **Save anywhere**: F5 saves during dialogue, options, dice rolls or on the training map, without using the game's slots (up to 100, configurable). F7 loads the latest; F6 lists them all. Loading a story save fast-forwards to the saved line.
- **Roll back** (F4): go back to the line before the last story check, adjust stats with F9 and continue; the check is re-evaluated.
- **New save/load screen** (on by default): portrait of your love interest, game date, save time, note, and the first lines you will see after loading. Manual and auto saves on separate pages (LB / RB on a gamepad).
- **100 manual save slots** (20 in the base game), **save notes**, and **5 extra old copies** of each auto save.

### Training and combat

- Settings for: breaking the 100 stat cap, unlimited training, free forging and alchemy, invincibility in duels, no damage in group fights, no ultimate cooldown, game speed 1x/2x/3x/5x, pinball score.
- **Skip turn** and **win now** in the ESC menu.
- **Faster flow**: faster scene changes, loading and story skipping.
- **Action points**: normal / unlimited / N per period. These modes may break story triggers; save often.
- **The fourth senior brother sells everything** (off by default).

### Stats page (F9)

Edit stats, martial arts, sect values, relationships, talents, training points, facilities, books, items, titles and every story flag. Changing stats or flags carelessly can break the story; save first.

### Other

- **Languages**: the mod follows the game language (Simplified Chinese, Traditional Chinese, Korean). With the [Japanese patch](https://github.com/Dlaqe2334/LOM-JPMOD) installed it shows Japanese. You can also choose 「界面语言」 at the top of the settings page. Korean and Japanese currently cover the common UI text; the rest is still Chinese.
- **Export diagnostics**: ESC → settings → 「导出诊断」 packs logs, config and the plugin list onto the desktop (no saves).
- **Conflict detection**: if a conflicting old mod is installed, the settings page opens and names it.

## FAQ

**Found a bug?** Run the installer again to make sure you are on the latest version. If it persists, export diagnostics and open an [issue](https://github.com/kevinnie2003/LOMAssistant/issues) describing how it happened.

**Nothing happens after installing?** Quit Steam completely from the tray and start the game again. If it still does not work, use 「导出诊断」 in the installer.

**Linux / Steam Deck?** Install manually and add `WINEDLLOVERRIDES="winhttp=n,b" %command%` to the Steam launch options.

## Settings

Everything is on the settings page of the window (F8). Changes are saved immediately; hover over a name for details. Settings are also stored in `BepInEx\config\LOMAssistant.cfg`.

## Building

You need the .NET 8 SDK and a game folder with the mod installed (the build references the game's and BepInEx's assemblies; no game files are in this repository).

```bash
GAME="D:/Steam/steamapps/common/LegendOfMortal" DOTNET=/path/to/dotnet ./build.sh
DOTNET=/path/to/dotnet ./Installer/build-installer.sh
```

## Credits

See the [Chinese README](README.md#致谢). This project builds on 活侠传助手 by 爱死, and takes ideas or code from MortalAnytimeSave, 赵活系统 and LOM_SaveSlotExpander. Thanks to [lxsb222-spec](https://github.com/lxsb222-spec) and [blakceyes](https://github.com/blakceyes) for their pull requests.
