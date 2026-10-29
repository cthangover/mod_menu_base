# Menu — main menu, game choice, saves and settings

Menu is the first screen you see: the title menu with **Play**, **Load**, **Settings**, **Mods & Collections** and **Exit**. It lets you pick one of the installed games, start it or continue where you stopped, load a save, and change the language, sound and display settings. Other mods can add their own buttons and settings pages to it.

It belongs to the shell that is always loaded, so it is there before any game starts.

![The main menu: Play, Load, Settings, Mods & Collections and Exit on the left, the Cthangover title art on the right.](assets/main-menu.jpg)

## Start a game

**Play** opens **Choose a game**. Every game from your collections is a card with its cover, description, readiness and number of saves.

![Two game cards in Choose a game: Cthangover and My Novel, each "Ready to play", "No saves", with New game and Configure buttons.](assets/game-picker.jpg)

- **New game** starts the game from its first scene. The menu switches to the game's set of mods in the same window — there is no restart.
- **Continue** on a card resumes that game's latest save; **Saves** lists them.
- **Install missing** appears when the game needs mods you do not have; it downloads them.
- **Configure** opens the game's scheme in the mod manager.

While you look at the menu, the games' mods are prepared in the background; the hint at the bottom says when starting will be quick.

**Continue** in the main menu itself appears once you have played: it returns to the last game and save you used.

## Load a save

**Load** shows the saves of one game at a time; pick the game in the selector at the top and click a save to load it.

![The Load window for My Novel with one save, "Day 1, 00:00", and the hint "Click a save to load it".](assets/load.jpg)

If the game's mods changed since the save was made, a **The game has changed** window lists what was added (starts from scratch), removed (its data is kept and returns if turned on again) or updated, and makes a backup of the save before loading. Saves from before games existed appear under **Saves without a game**; you choose the game they belong to and the file moves there.

During a game, saving and loading use the same window through the in-game menu: click a slot to save, a used slot is overwritten after confirmation, **+ New save** adds one.

## Settings

**Settings** has four tabs. Changes are applied and saved at once.

- **General** — interface language.
- **Sound** — on/off and volume for Sounds, Music and Ambience.
- **Display** — window mode (windowed, maximized, borderless or exclusive fullscreen), monitor, window size, V-Sync and frame rate limit, plus **Reset display settings**.
- **Mods** — settings pages that installed mods add, with **Reset to Defaults**.

![The Display tab: window mode, monitor, window size, V-Sync and frame rate limit.](assets/settings-display.jpg)

![The Mods tab: the Menu mod's own page with "Data of disabled mechanics" and a Purge button.](assets/settings-mods.jpg)

Menu's own page on the **Mods** tab is **Data of disabled mechanics → Purge**: it removes from every save the data of mods that are no longer part of the games. Use it only when you are sure: a mechanic turned on again starts from scratch.

## What Menu relies on

Menu draws the screens and decides what each button does. The game list and its readiness come from the collections; starting, switching and saving games are done by the engine's game session and save services; the music of the title screen is the `MainMenu` playlist of Music Pack, which Menu requires. **Mods & Collections** is supplied by Mod Manager.

## For mod authors: add a button or a settings page

The buttons come from a registry, not from a fixed screen, so a mod can add, replace or reorder them. Mod Manager adds its button with a `main_menu` entry in its manifest:

```json
"main_menu": [
  {
    "id": "mods_hub",
    "scene_path": "scenes/mods_hub_screen.tscn",
    "button": { "icon": "mod_manager", "locale": "mod_manager/menu_title", "index": 100 }
  }
]
```

A mod's settings appear on the **Mods** tab when the mod registers settings definitions in the mod settings registry, as Menu does for its Purge action. Text on buttons and settings comes from the mod's own localization.

## If something goes wrong

1. **No games under Play.** No collection of kind *game* is installed or enabled; open **Mods & Collections**.
2. **A game card says Missing mods or Incompatible versions.** Use **Install missing**, or **Configure** to see the problem.
3. **Continue is missing.** It appears after you have played a game at least once.
4. **The window does not keep its size.** Check **Display → Window mode**; window size applies only to windowed mode.
