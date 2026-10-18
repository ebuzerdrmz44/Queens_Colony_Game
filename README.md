# Queen's Colony

A pixel-art colony defence game made in Unity for the CClub Game Hackathon '26.
Run an ant colony through the fall: gather food, dig the nest, raise ants, and protect the queen through 10 waves of bugs
and a final boss.

## Install

You need:
- **Unity Hub** and the Unity editor **6000.3.25f1** (Unity 6.3). Unity Hub offers to install the exact version when you open the project.
- **Git**, or download the repo as a ZIP from GitHub (green **Code** button → **Download ZIP**).

Steps:
1. Get the project:
   ```bash
   git clone https://github.com/ebuzerdrmz44/cclub-game-hackathon-26.git
   ```
   (The repository is private: ask the owner to add you as a collaborator first.)
2. Open **Unity Hub** → **Add** → **Add project from disk**, and pick the cloned `cclub-game-hackathon-26` folder.
3. Open it. The first time takes a few minutes while Unity imports the assets and builds its `Library` folder.
4. In the **Project** window, open the scene **`Assets/_Project/Scenes/AntColony.unity`** (double-click it).
5. Press **Play** ▶. The game starts on its main menu.

Tip: set the **Game** view to **Full HD (1920x1080)** or **16:9** to see the HUD as intended.

### Making a standalone build (optional)
**File → Build Profiles** → pick your platform (Windows / Linux / macOS) → make sure `Assets/_Project/Scenes/AntColony.unity`
is in the scene list (it's first) → **Build**. Run the built executable to play without Unity.

## How to play

**New game** always starts normal play. Choose **Tutorial** in the main menu to start the guide; **Continue** restores the tutorial only if it was part of your saved game.

The tutorial waits for completed actions: bringing a load home, hatching a worker, finishing a dig,
and finishing construction. When a goal is done, choose **Next** at your own pace. **Read again**
reopens the current explanation; **Skip step** skips an independent task (including waiting for a larva). Gathering and digging are required setup steps, so their individual skip button is disabled. Skipping a view-switch step still opens the view needed for the next task. **Skip tutorial** ends the entire guide. Waves wait during the tutorial,
and completing it gives at least 45 seconds to prepare. Saving also keeps your tutorial step and progress.

### Wave report

Click the wave banner at the top to open a draggable **Wave report**. Left/right arrows browse
only waves that have already started; future waves are hidden. Before the first attack, the panel
shows an empty state. Opening it again selects the latest started wave.

Each wave has an illustrated battle card for enemy kills and remaining enemies (including pending
arrivals), plus portrait cards for soldier, spitter, worker and builder losses. Combat losses follow
the enemy that dealt the fatal blow, even when waves overlap. Non-combat losses and starvation
are summarised separately below the cards. Drag the title to move the window; X or Esc closes it.

The history is saved with the colony. Old saves cannot reconstruct past battles: unknown history
is shown as `--`, and `+` marks partial counts recorded since loading. Subsequent new waves have
complete records. The Music/Pause icons keep their hover appearance and sound, without hover particles.

### Music and pause

The advisor card sits above two smaller, text-free pixel icons in the top-right HUD: a music note and pause bars (a play triangle while paused). The music note opens a centred
player that can be moved by dragging its title bar. It contains the current track, previous/next controls, the full playlist, and the **Wave music**
toggle. On wider screens, existing worker/soldier/builder/spitter artwork with music-only headphones appears beside the playlist, crossfading when the playing or selected track changes. Picking a track keeps the player open. Long lists can be scrolled or navigated with the
up/down buttons. Close it with X or Esc. The player is also available while paused.

### Language

The game is in **English** and **Turkish (Türkçe)**. Switch it in **Settings** (main menu or pause menu). The choice is remembered; the first launch follows the computer's language.

### Saving and settings

- **Settings** is available from the title and pause menus. It contains sound controls and
  **Autosave after waves**, which is ON by default and remembered between sessions.
- Autosave writes after the battlefield is cleared, including all pending enemies and the final boss.
  If you call overlapping waves early, it saves once the combined attack is cleared.
- Autosave and **F5 / Save game** use the same slot. **F9 / Continue** loads that slot.
- Returning to the main menu from pause offers **Save and return**, **Leave without saving**, and **Cancel**.
  If saving fails, the game stays open and shows an error.
- Starting **New game**, **Tutorial**, or **Play again** asks before replacing an existing save.
  Confirming creates a new starting checkpoint. Cancel keeps the previous game.
- Saves are written through a temporary file and replaced only after writing succeeds. The previous
  complete save is kept as `colony-save.json.bak` in the game's persistent data directory.


| Key | Action |
|---|---|
| WASD / arrows, mouse wheel | move / zoom the camera |
| Tab | switch between the meadow (surface) and the nest (underground) |
| 1 / 2 / 3 / Z | raise a worker / soldier / builder / acid spitter |
| M | missions (jobs for workers and builders); drag the title bar to move the panel |
| R | colony report; drag its title bar to move it |
| H | controls |
| N | call the next wave early |
| Esc | put a tool down, close a panel, or pause |
| F5 / F9 | save / load |

**On the surface:** 4 walls, 5 acid towers, 6 gather (pick the food piles workers fetch). Drag a box to select soldiers,
right-click to move or attack, E selects all soldiers, Q halves the group.

**In the nest:** 4 dig, 5 storage, 6 cradle, 7 fungus garden, X remove.

## For the team

- Game code: `Assets/_Project/Scripts`, art in `Assets/_Project/Art`, prefabs in `Assets/_Project/Prefabs`.
- Most tuning (costs, speeds, waves, the boss, audio, UI images, effect prefabs) lives on the **Colony Game** object in
  the scene's Inspector. More details are in `Assets/_Project/README.md`.
- Translations are in `Assets/_Project/Scripts/Core/Localization.Turkish.cs`, keyed by the exact English text. Plain texts
  are translated automatically when drawn; texts with numbers use `Loc.F("Wave {0} is coming!", wave)`. When you add or
  change an English text, add/update its Turkish line too (missing ones just stay in English).
- In the editor, **F8** during Play sets up a test of the final boss wave (and saves it, so F9 replays it).

Music portrait variants live in `Assets/_Project/Art/UI/MusicPortraits`. Each UI Skin portrait has
an optional `musicImage` used only by the player; wave reports use its original `image`.
If the variant is empty, music falls back to the original portrait.

### Opening story

New games can show the queen finding a home, folding her wings, digging a chamber,
and laying her first eggs. Captions follow each story beat; eggs appear on the release
pose. The ending reveals the surface before handing control back to the player.
Esc, Space, Enter or a click skips the story after a short 0.6-second input guard,
so the click that starts a game cannot immediately dismiss it. The simulation and
wave timer stay frozen throughout the story.
