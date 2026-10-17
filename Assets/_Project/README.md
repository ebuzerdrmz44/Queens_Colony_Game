# Autumn Ant Colony

## Run it
1. In Unity: menu **Ant Colony → Create Game Scene** (only once; it creates `Assets/_Project/Scenes/AntColony.unity`).
2. Press **Play**.

## Prefabs and art
Ants, the queen, enemies, walls, towers and food piles spawn from prefabs in `Assets/_Project/Prefabs`.
- **Ant Colony → Generate Prefabs** saves the generated pixel art as PNGs in `Assets/_Project/Art/Sprites`, builds the
  prefabs and plugs them into the **Colony Game** object (Inspector → Prefabs; enemies under Settings → Enemy Types).
  Save the scene afterwards. "Missing only" keeps existing prefabs; "Replace all" rebuilds them.
- Edit a prefab like any other: swap the sprite on its **Body** (or **Visual**) child, change scale or colour, add components.
- Animation frames are on the unit's script (Worker Ant / Soldier Ant / Builder Ant: Side View and Top View;
  Enemy: Sprites). Walk frames in order, then wind-up and strike frames for attacks. Empty slots use the generated art.
- Clear a prefab slot and that thing is built in code again. Numbers (speed, health, costs) stay in the Settings.
- Overworld scenery (trees, bushes, boulders, stumps, logs, flowers) is in `Prefabs/Scenery`: each has a **Visual**
  child and usually a **Shadow** child. Every slot is a list; one entry is picked at random per spot, so add variants freely.
- Not prefabs yet: rooms, larvae, the nest tiles, acid shots, health bars, grass tufts and pebbles.

## Effects (particles)
The **Colony Game** object has an **Effects** section with a slot per moment: soldier/enemy bites, queen hit, acid hit,
deaths, food picked up / stored, larva fed, egg laid, ant hatched, garden made food, tile dug, room / wall / tower built,
wall / tower destroyed. Drop any prefab in (usually a Particle System with Play On Awake on and Looping off): it's spawned
at that spot and removed when its particles are done. Set its renderer's **Order in Layer** to about 25 so it shows above units.

## How to play
The game opens on a **main menu** (New game, Continue, How to play, Quit). Esc pauses; the pause menu can **save**,
**load** and go back to the menu. **F5** saves and **F9** loads at any time. There is one save slot, kept in
`Application.persistentDataPath/colony-save.json`. It only loads into a world with the same **worldSeed** and nest size,
because rock and scenery are rebuilt from the seed.

Your colony has two halves:
- the **surface**, seen from above like a small strategy game: food piles, enemy waves, the ant hill in the middle;
- the **nest** underground, seen from the side, which you design yourself: dig tunnels, place rooms, raise larvae.

Survive **10 enemy waves**. If enemies reach the ant hill they hurt the queen, and if the queen dies you lose.

### The economy
- **Workers** forage on the surface. **Berries, acorns** and defeated enemies are food straight away.
  **Leaves** are more common, but they only turn into food in a **Fungus Garden** (1 leaf → 2 food every 5 s per garden).
- Food and leaves share **storage**: the queen's chamber holds 60, each Storage room 40 more. What doesn't fit is wasted.
- **Every ant eats** 0.9 food a minute (the queen 1.5). If the food runs out, the colony starves and ants lose health.
- Workers bring their load to the **nearest Storage room**, so storage close to the entrance means shorter trips.

### Raising ants
Press 1 / 2 / 3 to queue a worker / soldier / builder. The queen lays the egg into a free **Cradle** (3 food).
The egg hatches into a larva, and workers carry food to it from storage; it grows only while it's fed.
Worker 8 food in all, builder 10, soldier 15. More cradles = more ants growing at once.

### Designing the nest
Underground is a grid of soil, pebbly **stone veins**, solid grey **rock** (can't be dug) and tunnels. Ants climb through any tunnel.
- **Dig**: drag a box over soil or stone veins. Builders dig the tiles that touch a tunnel, closest first.
- **Digging pays**: soil sometimes gives 1 **stone**, stone veins give 3 (but take twice as long). Soil hides
  **seed caches** and **fat grubs** (food), more of them deeper down; you can see one once a tunnel reaches the tile next to it.
- **Rooms** go on dug-out tiles: Storage (2x1), Cradle (1x1), Fungus Garden (2x1). Builders build them.
- **Remove**: drag a box to cancel digging, remove rooms (half the cost back, all of it for unbuilt ones)
  and fill empty tunnels back in. It never fills a tile that would cut a room off from the entrance.
- While you drag, every tile that will change lights up (gold: dig, red: room removed, brown: tunnel filled,
  orange: dig mark cancelled) and the hint above the buttons counts them.

### Knowing what to do
- **Advisor card** (top right): the most important tip right now, e.g. leaves piling up, storage full, hungry larvae.
- **6: Gather** (surface): select food piles like soldiers (click, or drag a box; Shift adds). Only Food gatherers and
  Leaf cutters go outside, and only to marked piles (their own kind first, any marked pile when none of it is left); with nothing marked they wait in the nest. Free workers feed
  larvae and stay home. The game starts with all but one worker on Food gatherers.
- **M: missions**: put workers on one job (larva feeders, food gatherers, leaf cutters) and builders on one area
  (diggers in the nest, wall builders outside) with - and +. Free ants choose their own work.
- **R: colony report**: food gathered / made / eaten per minute as bars, storage, what the workers are doing,
  trip time, brood progress and every tip. **H: help**: how to play and all controls.
- Hover any button for what it does and what it costs. Room labels show each cradle's and garden's state.

### Defending
- Soldiers: left-click / drag a box to select (Shift adds), right-click an enemy to attack it or the ground to move.
  Left alone, they guard their spot. Soldiers attacking one enemy surround it.
- Walls (drag a line, **stone**) block enemies, which have to chew through them, and your own ants walk around them:
  leave a gap so workers can get out.
  Towers (**food + stone**) spit acid at enemies in range; hover one to see its range. Builders build and repair both.
- Boulders, stumps, fallen logs, grove tree trunks and finished walls and towers are solid: ants walk around them, enemies around the scenery
  (bushes and flowers can be walked through).

### Controls
| Input | Surface | Nest |
|---|---|---|
| 1 / 2 / 3 | Queue a worker / soldier / builder | same |
| 4 | Walls | Dig |
| 5 | Towers | Storage room |
| 6 / 7 | | Cradle / Fungus Garden |
| X | | Remove |
| E | Select all soldiers | |
| Q | Halve the selected soldiers | |
| 6 | Gather: select food piles to bring home | |
| M | Missions: give workers / builders one job | same |
| Mouse | Select and command soldiers, place walls / towers | Use the tool in hand |
| Right-click / Esc | Stop placing (Esc then closes an open panel, then pauses) | Put the tool down |
| WASD / arrows, wheel | Pan, zoom | Pan, zoom |
| Tab | Go to the nest | Go to the surface |
| N | Call the next wave early | same |
| R / H | Colony report / help | same |
| F5 / F9 | Save / load | same |

## Scenes
Play builds two more scenes next to `AntColony` (look at the Hierarchy while playing):
- **Surface**: the top-down overworld (food, enemies, ant hill, rally flag, walls and towers).
- **Nest**: the underground grid (tunnels, rooms, brood, queen).

`AntColony` keeps the shared game logic: colony, camera, HUD. Ants move between the two scenes
through the entrance (in through the ant hill, out at the top of the nest). **Tab** fades between them.
Both scenes keep running while you look at the other one.

## Changing things
Select **Colony Game** in the scene:
- **Settings**: every number (costs, speeds, upkeep, larva food, rooms, gardens, waves, world and nest size).
  The balance is explained at the top of `GameSettings.cs`.
- **Art**: drop sprites from the pixel-art pack here. Empty slots use the generated 16x16 autumn pixel art.
  Creature slots take walking frames in order (one sprite also works, it just won't animate),
  and the matching "Attack" slots take a wind-up and a strike frame. Nest art is side view;
  surface art is top-down and should face right (it is rotated toward where the creature walks).
  Tick "faces left" if the side-view ant looks left.

## Code map (`Scripts/`)
- `Core/`: `ColonyGame` (entry point, creates the Surface/Nest scenes and builds everything), `GameSettings`, camera, controls,
  `SoldierSelection` (RTS selection and orders), `BuildMode` (walls and towers), `NestTools` (dig, rooms, remove)
- `World/`: `Surface` (overground), `Nest` (tile grid, digging, paths), `Room`, food, falling leaves
- `Colony/`: `Colony` (food, leaves, upkeep, queen, egg laying, gardens), `Brood` (eggs and larvae), `ColonyStats`, `Advisor`
- `Ants/`: `Ant` base class, `WorkerAnt` (foraging, nursing), `SoldierAnt`, `BuilderAnt` (digging, building, repairs)
- `Enemies/`: `Enemy`, `EnemyStats`, `WaveSpawner`
- `Units/`: health, health bars, sprite body helper, walk + attack animation (`UnitAnimator`), `Crowd` (no overlapping)
- `Defenses/`: `Defenses` (placement, builder jobs), `Structure` (wall / tower), `AcidShot`
- `Art/`: palette and generated pixel sprites
- `UI/`: `GameHud` (pixel HUD: resources, advisor, report, help, buttons), `PixelFont` (5x7 bitmap font)
- `Editor/`: the "Create Game Scene" menu item
