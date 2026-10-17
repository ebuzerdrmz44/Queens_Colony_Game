# Autumn Ant Colony

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

The first **New game** runs a short tutorial. The main menu also has a **Tutorial** button to replay it.

| Key | Action |
|---|---|
| WASD / arrows, mouse wheel | move / zoom the camera |
| Tab | switch between the meadow (surface) and the nest (underground) |
| 1 / 2 / 3 / Z | raise a worker / soldier / builder / acid spitter |
| M | missions (jobs for workers and builders) |
| R | colony report |
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
- In the editor, **F8** during Play sets up a test of the final boss wave (and saves it, so F9 replays it).
