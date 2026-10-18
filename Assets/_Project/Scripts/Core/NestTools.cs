using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AntColony
{
    public enum NestTool
    {
        None,
        /// <summary>Drag a box over soil to mark it for digging.</summary>
        Dig,
        /// <summary>
        /// Drag a box to cancel dig orders, remove rooms (part of a built room's cost comes back) and fill empty
        /// tunnels back in (never cutting a room off from the entrance).
        /// </summary>
        Remove,
        Storage,
        Cradle,
        Garden,
    }

    /// <summary>
    /// Designing the nest, in the style of colony-builder games: mark soil to dig, place rooms on dug-out tiles,
    /// and remove what isn't needed. Builders carry out the orders. A preview follows the mouse
    /// (green = can be done, red = blocked or too expensive). The tool stays on until right-click or Esc.
    /// </summary>
    public class NestTools : MonoBehaviour
    {
        /// <summary>Biggest box that can be dragged, in tiles per side.</summary>
        const int MaxAreaSide = 40;

        // Per-tile preview colours: each says what will happen to that tile.
        static readonly Color WillDigColor = new Color(0.95f, 0.8f, 0.3f, 0.5f);
        static readonly Color WillCancelColor = new Color(1f, 0.6f, 0.2f, 0.55f);
        static readonly Color WillRemoveRoomColor = new Color(1f, 0.35f, 0.3f, 0.55f);
        static readonly Color WillFillColor = new Color(0.55f, 0.36f, 0.2f, 0.8f);
        static readonly Color AreaOutlineColor = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color FitsColor = new Color(0.6f, 1f, 0.6f, 0.6f);
        static readonly Color BlockedColor = new Color(1f, 0.4f, 0.4f, 0.6f);

        static readonly Color KeptOpenColor = new Color(0.85f, 0.85f, 0.85f, 0.18f);

        /// <summary>Everything a Remove drag over one box would do. Shared by the preview and the real removal.</summary>
        class RemovalPlan
        {
            public RectInt Area;
            public int Revision;
            public readonly HashSet<Room> Rooms = new HashSet<Room>();
            public readonly List<Vector2Int> DigMarks = new List<Vector2Int>();
            public List<Vector2Int> Fill = new List<Vector2Int>();
            public readonly List<Vector2Int> KeptOpen = new List<Vector2Int>();
            public int CradlesWithBrood;
        }

        readonly List<SpriteRenderer> tileMarkers = new List<SpriteRenderer>();
        RemovalPlan cachedPlan;

        ColonyGame game;
        SpriteRenderer roomPreview;
        bool dragging;
        Vector2Int dragStart;

        public NestTool Tool { get; private set; }
        public bool IsActive => Tool != NestTool.None;
        /// <summary>What the box being dragged will do, e.g. "6 tiles to dig". Empty when not dragging.</summary>
        public string PreviewSummary { get; private set; } = "";

        public void Init(ColonyGame colonyGame)
        {
            game = colonyGame;
        }

        public static RoomType RoomFor(NestTool tool)
        {
            switch (tool)
            {
                case NestTool.Storage: return RoomType.Storage;
                case NestTool.Cradle: return RoomType.Cradle;
                default: return RoomType.Garden;
            }
        }

        public static bool PlacesRoom(NestTool tool) => tool == NestTool.Storage || tool == NestTool.Cradle || tool == NestTool.Garden;

        /// <summary>Picks a tool, or puts it down if it was already in hand. Switches the camera to the nest.</summary>
        public void Toggle(NestTool tool)
        {
            if (Tool == tool)
            {
                Stop();
                return;
            }

            Tool = tool;
            dragging = false;
            if (game.CameraRig.View != CameraView.Nest) game.CameraRig.ToggleView();
        }

        public void Stop()
        {
            Tool = NestTool.None;
            dragging = false;
            HidePreviews();
        }

        /// <summary>Called by <see cref="ColonyControls"/> every frame while the nest is on screen and a tool is in hand.</summary>
        public void HandleMouse(Mouse mouse)
        {
            Vector2 screenPosition = mouse.position.ReadValue();
            bool overUi = game.Hud.IsPointerOverUi(screenPosition);
            Vector2Int tile = game.Nest.WorldToTile(game.CameraRig.ScreenToWorld(screenPosition));

            if (mouse.rightButton.wasPressedThisFrame)
            {
                Stop();
                return;
            }

            if (PlacesRoom(Tool)) HandleRoomTool(mouse, tile, overUi);
            else HandleAreaTool(mouse, tile, overUi);
        }

        // ---------- Dig and remove (drag a box) ----------

        void HandleAreaTool(Mouse mouse, Vector2Int tile, bool overUi)
        {
            if (mouse.leftButton.wasPressedThisFrame && !overUi)
            {
                dragging = true;
                dragStart = tile;
            }

            RectInt area = dragging ? TileArea(dragStart, tile) : new RectInt(tile, Vector2Int.one);
            if (overUi && !dragging) HidePreviews();
            else ShowTileMarkers(area);

            if (!dragging || !mouse.leftButton.wasReleasedThisFrame) return;
            dragging = false;
            PreviewSummary = "";

            if (Tool == NestTool.Dig) MarkForDigging(area);
            else RemoveIn(area);
        }

        void MarkForDigging(RectInt area)
        {
            int marked = 0;
            foreach (Vector2Int tile in area.allPositionsWithin)
            {
                if (game.Nest.SetDigOrder(tile, true)) marked++;
            }

            game.Audio.Play(marked > 0 ? Sound.DigSelect : Sound.UiError);
            if (marked > 0) game.Tutorial.Notify(TutorialSignal.DigMarked);
            if (marked == 0) game.ShowMessage("Nothing to dig there: drag over brown soil (grey rock can't be dug).");
            else if (game.Colony.CountAnts(AntRole.Builder) == 0) game.ShowMessage("Raise a Builder [3] to dig it out!");
        }

        RemovalPlan PlanRemoval(RectInt area)
        {
            Nest nest = game.Nest;
            if (cachedPlan != null && cachedPlan.Area.Equals(area) && cachedPlan.Revision == nest.Revision) return cachedPlan;

            var plan = new RemovalPlan { Area = area, Revision = nest.Revision };
            foreach (Vector2Int tile in area.allPositionsWithin)
            {
                if (!nest.InBounds(tile)) continue;
                if (nest.HasDigOrder(tile)) plan.DigMarks.Add(tile);

                Room room = nest.RoomAt(tile);
                if (room == null || room.Type == RoomType.Queen || plan.Rooms.Contains(room)) continue;
                if (room.Brood != null) plan.CradlesWithBrood++; // never throw away an egg or larva
                else plan.Rooms.Add(room);
            }

            // Rooms go first, so the tunnels leading to them may be filled in the same drag.
            plan.Fill = nest.PlanFill(area, plan.Rooms, plan.KeptOpen);
            cachedPlan = plan;
            return plan;
        }

        void RemoveIn(RectInt area)
        {
            Nest nest = game.Nest;
            RemovalPlan plan = PlanRemoval(area);
            cachedPlan = null;

            foreach (Vector2Int tile in plan.DigMarks) nest.SetDigOrder(tile, false);

            int refund = 0;
            foreach (Room room in plan.Rooms)
            {
                // Blueprints give everything back; built rooms part of it.
                refund += room.IsBuilt ? Mathf.RoundToInt(room.PaidCost * game.Settings.roomRefund) : room.PaidCost;
                nest.RemoveRoom(room);
            }
            if (refund > 0) game.Colony.AddFood(refund, foraged: false);
            nest.Fill(plan.Fill);

            var parts = new List<string>();
            if (plan.Rooms.Count > 0) parts.Add(Loc.F(plan.Rooms.Count == 1 ? "Removed {0} room (+{1} food)" : "Removed {0} rooms (+{1} food)", plan.Rooms.Count, refund));
            if (plan.Fill.Count > 0) parts.Add(Loc.F(plan.Fill.Count == 1 ? "filled {0} tunnel tile" : "filled {0} tunnel tiles", plan.Fill.Count));
            if (plan.DigMarks.Count > 0) parts.Add(Loc.F(plan.DigMarks.Count == 1 ? "cancelled {0} dig mark" : "cancelled {0} dig marks", plan.DigMarks.Count));

            string result = parts.Count > 0 ? string.Join(", ", parts) + "." : "";
            if (plan.KeptOpen.Count > 0)
                result += " " + Loc.T("Grey tunnels stay open: they're the only way to reach a room. Include that room in the box to remove both.");
            if (plan.CradlesWithBrood > 0) result += " " + Loc.T("Cradles with an egg or larva are kept.");
            if (result.Length == 0) result = "Nothing to remove there. Drag over rooms, tunnels or dig marks.";

            string message = result.Trim();
            game.ShowMessage(char.ToUpper(message[0]) + message.Substring(1));
        }

        // ---------- Rooms (click to place) ----------

        void HandleRoomTool(Mouse mouse, Vector2Int tile, bool overUi)
        {
            RoomType type = RoomFor(Tool);
            Vector2Int size = RoomInfo.Of(type).Size;
            var origin = new Vector2Int(tile.x - (size.x - 1) / 2, tile.y); // centred on the mouse, standing on its tile
            int cost = RoomInfo.CostOf(type, game.Settings);
            bool fits = game.Nest.CanPlaceRoom(type, origin);
            bool affordable = game.Colony.Food >= cost;

            ShowRoomPreview(type, origin, fits && affordable, hidden: overUi);
            if (!mouse.leftButton.wasPressedThisFrame || overUi) return;

            if (!fits)
            {
                game.ShowMessage(Loc.F("A {0} needs {1}x{2} dug-out tiles with nothing on them.", Loc.T(RoomInfo.Of(type).Name), size.x, size.y));
                game.Audio.Play(Sound.UiError);
                return;
            }
            if (!game.Colony.TrySpendFood(cost)) return;

            game.Nest.PlaceRoom(type, origin, built: false, paidCost: cost);
            game.Audio.Play(Sound.UiConfirm);
            game.Tutorial.Notify(TutorialSignal.RoomPlaced);
            if (game.Colony.CountAnts(AntRole.Builder) == 0) game.ShowMessage("Raise a Builder [3] to build it!");
        }

        // ---------- Previews ----------

        static RectInt TileArea(Vector2Int a, Vector2Int b)
        {
            var min = Vector2Int.Min(a, b);
            var max = Vector2Int.Max(a, b);
            return new RectInt(min, max - min + Vector2Int.one);
        }

        /// <summary>
        /// Colours every tile in the box by what the tool will do to it, and counts it up for the HUD.
        /// Tiles the tool won't touch stay clear, so it's easy to see whether the drag does anything.
        /// </summary>
        void ShowTileMarkers(RectInt area)
        {
            if (roomPreview != null) roomPreview.enabled = false;
            Nest nest = game.Nest;
            area.width = Mathf.Min(area.width, MaxAreaSide);
            area.height = Mathf.Min(area.height, MaxAreaSide);

            int used = 0;
            int toDig = 0;
            RemovalPlan plan = null;

            // A faint outline of the whole box first.
            Marker(used++, (Vector2)area.position + (Vector2)area.size * 0.5f, area.size, AreaOutlineColor);

            if (Tool == NestTool.Dig)
            {
                foreach (Vector2Int tile in area.allPositionsWithin)
                {
                    if (!nest.InBounds(tile) || !nest.IsDiggable(tile) || nest.HasDigOrder(tile)) continue;
                    Marker(used++, (Vector2)tile + Vector2.one * 0.5f, Vector2.one * 0.9f, WillDigColor);
                    toDig++;
                }
            }
            else
            {
                plan = PlanRemoval(area);
                var fill = new HashSet<Vector2Int>(plan.Fill);
                foreach (Vector2Int tile in area.allPositionsWithin)
                {
                    if (!nest.InBounds(tile)) continue;

                    Color? color = null;
                    if (plan.DigMarks.Contains(tile)) color = WillCancelColor;
                    else if (fill.Contains(tile) && nest.RoomAt(tile) == null) color = WillFillColor;
                    else if (nest.RoomAt(tile) is Room room && plan.Rooms.Contains(room)) color = WillRemoveRoomColor;
                    else if (plan.KeptOpen.Contains(tile)) color = KeptOpenColor;

                    if (color.HasValue) Marker(used++, (Vector2)tile + Vector2.one * 0.5f, Vector2.one * 0.9f, color.Value);
                }
            }

            for (int i = used; i < tileMarkers.Count; i++)
            {
                if (tileMarkers[i] != null) tileMarkers[i].enabled = false;
            }

            if (!dragging)
            {
                PreviewSummary = "";
                return;
            }
            var parts = new List<string>();
            if (Tool == NestTool.Dig) parts.Add(toDig > 0 ? Loc.F("{0} tiles to dig", toDig) : Loc.T("no soil to dig here"));
            else
            {
                if (plan.Rooms.Count > 0) parts.Add(Loc.F(plan.Rooms.Count == 1 ? "{0} room" : "{0} rooms", plan.Rooms.Count));
                if (plan.Fill.Count > 0) parts.Add(Loc.F("{0} tiles to fill", plan.Fill.Count));
                if (plan.DigMarks.Count > 0) parts.Add(Loc.F("{0} dig marks", plan.DigMarks.Count));
                if (plan.KeptOpen.Count > 0) parts.Add(Loc.F("{0} kept open (path to a room)", plan.KeptOpen.Count));
                if (parts.Count == 0) parts.Add(Loc.T("nothing to remove here"));
            }
            PreviewSummary = string.Join(", ", parts);
        }

        /// <summary>Shows pooled marker number <paramref name="index"/> over a rectangle given in tile units.</summary>
        void Marker(int index, Vector2 tileCenter, Vector2 tileSize, Color color)
        {
            tileMarkers.RemoveAll(marker => marker == null); // the world was rebuilt
            while (tileMarkers.Count <= index)
                tileMarkers.Add(game.Art.CreateSprite("Tile Preview", game.NestRoot, game.Art.Pixel, SortingOrder.BuildPreview));

            SpriteRenderer sprite = tileMarkers[index];
            sprite.enabled = true;
            sprite.color = color;
            GameArt.Stretch(sprite, tileSize * Nest.TileSize);
            sprite.transform.position = game.Nest.Origin + tileCenter * Nest.TileSize;
        }

        void ShowRoomPreview(RoomType type, Vector2Int origin, bool fits, bool hidden)
        {
            HideTileMarkers();

            Sprite sprite = type == RoomType.Storage ? game.Art.StoragePile : type == RoomType.Cradle ? game.Art.Cradle : game.Art.FungusGarden;
            if (roomPreview == null) roomPreview = game.Art.CreateSprite("Room Preview", game.NestRoot, sprite, SortingOrder.BuildPreview);

            Vector2Int size = RoomInfo.Of(type).Size;
            roomPreview.enabled = !hidden;
            roomPreview.sprite = sprite;
            roomPreview.color = fits ? FitsColor : BlockedColor;
            GameArt.ScaleToWidth(roomPreview, size.x * Nest.TileSize);
            roomPreview.transform.position = game.Nest.FootprintBottomCenter(origin, size) + new Vector2(0f, GameArt.PivotHeight(roomPreview));
        }

        void HidePreviews()
        {
            HideTileMarkers();
            if (roomPreview != null) roomPreview.enabled = false;
        }

        void HideTileMarkers()
        {
            foreach (SpriteRenderer marker in tileMarkers)
            {
                if (marker != null) marker.enabled = false;
            }
            PreviewSummary = "";
        }
    }
}
