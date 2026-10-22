# What's new

Changes in each version, newest first. Versions before 2.7.0 are only in Chinese; see CHANGELOG.md.

## 2.8.1 (2026-10-22)

- **Three modes for option results**: 「选项后果」 in the story group of the settings page shows them always (default, as before), only on hover (when the mouse is over a dialogue option or dice line, or it is selected with a gamepad; no spoilers unless you look), or never.
- **「总是」 and 「不出」 for random events**: each event in the random event picker can be set to always or never; the rule is remembered and applies even with the picker off.
- **Pick branches decided by a random number**: branch checks that roll a random number (such as being pulled into a run by 小梅 while training) now open the picker too, listing the paths, where they lead and their odds; the other conditions are still checked. Each path can also be set to always or never.
- **Fixed flag notices overlapping or appearing on the right**: several notices at once are now stacked in the middle of the screen.
- **Fixed the note box on the original save screen**: the title was hidden behind the input box, and clicking Save or Cancel could load the save underneath.

## 2.8.0 (2026-10-22)

**New**

- **Legend slot pages**: the legend save screen after an ending and the legend page in the library get page buttons, 5 pages of 12 slots by default (1 page in the base game); saving opens the first page with a free slot. Set the page count under 「传奇栏位页数」 in the save group of the settings page.
- **Search the route map by flag**: the route map search box also matches flag IDs and descriptions, listing the scenes that change or check the flag, each with a link to the story chart.
- **Search in Traditional Chinese, Korean and Japanese**: the search boxes also match the text as shown in the UI language.
- **Movable, resizable enemy forecast in duels**: drag the bottom-right corner to resize; when narrow it switches to one line. Position and size are remembered.

**Interface**

- **Consistent windows and hint boxes**: the random event picker and save note editor use the same paper colors as the Coffee Companion window and follow dark mode; map hints, the branch window, the duel forecast, the fast-forward notice and anytime save loading progress share one dark hint box style.
- **Opacity settings in one place**: the Coffee Companion group of the settings page has three opacity settings, for the main window, small windows and hint boxes, applied immediately. The old branch window and map hint opacity settings are merged into the hint box one.
- **Check records in the backlog no longer show a speaker name**: only the small check line remains, so it isn't mistaken for dialogue.

**Fixes**

- **Read lines turning white again after loading**: after loading an auto save, a recent save or an anytime save, lines you had just read showed as unread; read records are no longer lost.
- **Flag notice duration had no effect**: notices vanished at once whatever the setting, and disappeared on scene changes; they now stay for the set number of seconds across scenes.
- **Traditional Chinese wording**: after character-by-character conversion, terms are now replaced with Traditional Chinese usage, and common wrong characters are fixed. The installer too.
- **Clearer installer message when the download fails**: if the new version can't be downloaded from GitHub, the installer says which version from the zip was installed and where to get the new zip, instead of just "Installed".

## 2.7.3 (2026-10-22)

- **Map event hints fixes**:
  - Side quests whose conditions are met now show as ready to trigger (they always showed as not ready before). When the main quest requires you to go elsewhere, the hint says clicking here won't trigger anything.
  - Only buildings with a main or side quest you can trigger now and haven't played yet stay lit; unseen random events no longer count, and lit buildings no longer flicker.
  - Time limits written in quest conditions are shown as dates, and quests that are past or not yet due go under "not in time".
  - Random events list only the ones that can be drawn, sorted by odds; very long hints shrink to fit. Events you have read any line of are no longer marked as unseen.
  - The hint box has a dark background; its opacity can be set under 「地图提示不透明度」 in the training group of the settings page (default 0.8).
  - Before you've entered any story scene after starting the game, the random events section says it needs one story scene first.
- **No more false conflict warning with the Japanese patch**; when another mod patches the same game code, the warning shows its plugin name instead of an ID.

## 2.7.2 (2026-10-22)

- **Fixed option results under dice lines overlapping or running off screen**: some flag names ended with an invisible line break that made the second half overwrite the first. Long results now stay on one line and shrink if they would run past the right edge of the screen.
- **The installer warns about non-English paths**: when the game is installed in a path with Chinese or other non-English characters, BepInEx fails to load and the mod doesn't work. The installer now shows a red warning under the game folder and asks again before installing; exported diagnostics flag it too.

## 2.7.1 (2026-10-21)

- **Fixed the random event picker not showing up in Tang sect buildings** such as the kitchen and the forge: the map event hints added in 2.7.0 recorded those buildings' random events as empty while on the training map.
- The random events section of the map event hints now lists events properly.

## 2.7.0 (2026-10-21)

**New features**

- **Dark mode**: under 「窗口配色」 (window colors) in the Coffee Companion group of the settings tab, choose light, dark or follow Windows. Dark mode is ink-black with paper-colored text.
- **Event hints on the training map**: hover over a building to see its main and side quests (whether they can trigger now, time window and conditions, met in green and unmet in red) and the odds of each random event. Buildings with events you haven't seen yet stay lit. Can be turned off in the training group of the settings tab.
- **Where flags, books and items are used**: on the stats tab, click a name or its description to list every story that changes it (set, add or subtract, gain, lose) and every place that checks it, with the quest, time, place and how to get there. Each entry can jump to the story graph.
- **Flag categories**: flags on the stats tab are split into sect favor, characters, the Six Paths, story, events and side quests, dialogue options, combat, training and facilities, and other.
- **Export legend**: the game's Status → Legend panel gets an Export button that saves your current save's unfinished legend as a txt file on the desktop. The base game only lets you export at an ending.
- **Ending numbers**: the ending list shows the number from the in-game codex (「结局N」 for endings, 「N.」 for deaths) and can be searched by number. It is sorted by number by default and can be switched to sort by time.
- **Enemy move forecast in duels** (off by default): while you pick a move, the right edge of the screen shows the move the enemy has picked, and marks turns where the enemy may change it in response. Turn it on in the combat group of the settings tab.

**Hotkeys**

- Every hotkey can be unbound. The separate switches for hiding the UI, fast-forward and the stats hotkey were folded into this.
- Hotkey clashes with other mods are resolved automatically: when the Japanese patch, 邪神认知污染ON, 剧情随行 or any mod that sets the same key in its config file is detected, this mod unbinds its own hotkey and shows a notice once.

**Languages**

- The installer follows the Windows display language: Simplified or Traditional Chinese, English, Japanese or Korean.
- Release notes are available in English, Japanese and Korean from this version on; older versions are still shown in Chinese.
- In the Japanese UI, flag names, conditions, option results and the check records in the dialogue log are shown in Japanese.
- The Korean and Japanese UI also use the game's Source Han Serif font and follow the window font setting; the window title is translated too.

**Fixes**

- The Japanese UI didn't appear even with the Japanese patch installed, on PCs that had been running for less than 24.9 days.
- In the action and location menus in Jiangling and elsewhere, right-click didn't open the menu.

