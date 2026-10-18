using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AntColony
{
    /// <summary>
    /// The Gather tool (6 on the surface): the player picks which food piles to gather, selecting them like soldiers.
    /// - left-click a pile, or drag a box over several (hold Shift to add, Shift-click a marked pile to drop it),
    /// - a plain click or drag replaces the marked piles; clicking empty ground clears them,
    /// - right-click or Esc puts the tool down; the marks stay.
    /// Workers on the Food gatherers / Leaf cutters missions only take from marked piles; with none marked they stay in the nest.
    /// A marked pile carries a gold ring.
    /// </summary>
    public class GatherOrders : MonoBehaviour
    {
        /// <summary>A drag shorter than this (in pixels) counts as a click.</summary>
        const float ClickThreshold = 6f;
        const float PickRadius = 0.9f;
        const string RingName = "Gather Ring";

        static readonly Color MarkColor = AutumnPalette.Gold;

        readonly List<FoodSource> marked = new List<FoodSource>();
        ColonyGame game;
        Vector2 dragStart;

        public bool IsActive { get; private set; }
        public bool IsDragging { get; private set; }
        /// <summary>The drag box in screen pixels (y up), valid while <see cref="IsDragging"/>.</summary>
        public Rect DragRect { get; private set; }

        /// <summary>Piles marked for gathering that still exist.</summary>
        public int MarkedCount
        {
            get
            {
                marked.RemoveAll(food => food == null);
                return marked.Count;
            }
        }

        public void Init(ColonyGame colonyGame)
        {
            game = colonyGame;
        }

        public void Toggle()
        {
            if (IsActive)
            {
                Stop();
                return;
            }

            IsActive = true;
            IsDragging = false;
            game.BuildMode.Stop();
            game.Selection.Clear();
            if (game.CameraRig.View != CameraView.Surface) game.CameraRig.ToggleView();
        }

        public void Stop()
        {
            IsActive = false;
            IsDragging = false;
        }

        /// <summary>Forgets every mark (used when the world is rebuilt).</summary>
        public void Reset()
        {
            marked.Clear();
            Stop();
        }

        /// <summary>Called by <see cref="ColonyControls"/> every frame while the surface is on screen and the tool is in hand.</summary>
        public void HandleMouse(Mouse mouse, bool shiftHeld)
        {
            Vector2 mousePosition = mouse.position.ReadValue();

            if (mouse.rightButton.wasPressedThisFrame)
            {
                Stop();
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame && !game.Hud.IsPointerOverUi(mousePosition))
            {
                IsDragging = true;
                dragStart = mousePosition;
            }

            if (!IsDragging) return;

            DragRect = Rect.MinMaxRect(
                Mathf.Min(dragStart.x, mousePosition.x), Mathf.Min(dragStart.y, mousePosition.y),
                Mathf.Max(dragStart.x, mousePosition.x), Mathf.Max(dragStart.y, mousePosition.y));

            if (!mouse.leftButton.wasReleasedThisFrame) return;

            IsDragging = false;
            if (DragRect.width < ClickThreshold && DragRect.height < ClickThreshold) ClickSelect(mousePosition, shiftHeld);
            else BoxSelect(DragRect, shiftHeld);
            Announce();
        }

        void ClickSelect(Vector2 screenPosition, bool additive)
        {
            Vector2 point = game.CameraRig.ScreenToWorld(screenPosition);
            FoodSource clicked = null;
            float closest = PickRadius;
            foreach (FoodSource food in game.Food.Sources)
            {
                float distance = Vector2.Distance(food.Position, point);
                if (distance < closest)
                {
                    clicked = food;
                    closest = distance;
                }
            }

            if (!additive) ClearMarks();
            if (clicked == null) return;

            if (additive && clicked.IsMarked) SetMarked(clicked, false);
            else SetMarked(clicked, true);
        }

        void BoxSelect(Rect screenRect, bool additive)
        {
            if (!additive) ClearMarks();

            Vector2 cornerA = game.CameraRig.ScreenToWorld(screenRect.min);
            Vector2 cornerB = game.CameraRig.ScreenToWorld(screenRect.max);
            Rect worldRect = Rect.MinMaxRect(
                Mathf.Min(cornerA.x, cornerB.x), Mathf.Min(cornerA.y, cornerB.y),
                Mathf.Max(cornerA.x, cornerB.x), Mathf.Max(cornerA.y, cornerB.y));

            foreach (FoodSource food in game.Food.Sources)
            {
                if (worldRect.Contains(food.Position)) SetMarked(food, true);
            }
        }

        void ClearMarks()
        {
            foreach (FoodSource food in marked)
            {
                if (food == null) continue;
                food.IsMarked = false;
                ShowRing(food, false);
            }
            marked.Clear();
        }

        /// <summary>Marks a pile for gathering (used when loading a saved game).</summary>
        public void Mark(FoodSource food) => SetMarked(food, true);

        void SetMarked(FoodSource food, bool isMarked)
        {
            if (food.IsMarked == isMarked) return;

            food.IsMarked = isMarked;
            ShowRing(food, isMarked);
            if (isMarked)
            {
                marked.Add(food);
                game.Tutorial.Notify(TutorialSignal.FoodMarked);
            }
            else marked.Remove(food);
        }

        /// <summary>A gold ring under a marked pile (created on first use, then just switched on and off).</summary>
        void ShowRing(FoodSource food, bool visible)
        {
            Transform existing = food.transform.Find(RingName);
            if (existing != null)
            {
                existing.gameObject.SetActive(visible);
                return;
            }
            if (!visible) return;

            SpriteRenderer ring = game.Art.CreateSprite(RingName, food.transform, game.Art.SelectionRing, SortingOrder.Food - 1);
            ring.color = MarkColor;
            GameArt.ScaleToWidth(ring, 1.4f);
        }

        void Announce()
        {
            int count = MarkedCount;
            int gatherers = game.Colony.CountOnMission(Mission.Forage, AntRole.Worker) + game.Colony.CountOnMission(Mission.Leaves, AntRole.Worker);
            if (count == 0) game.ShowMessage("No food marked: gatherers stay in the nest.");
            else if (gatherers == 0) game.ShowMessage(Loc.F(count == 1 ? "{0} pile marked. Put workers on Food gatherers or Leaf cutters (M)." : "{0} piles marked. Put workers on Food gatherers or Leaf cutters (M).", count));
            else if (game.Colony.IsStorageFull) game.ShowMessage(Loc.F(count == 1 ? "{0} pile marked, but storage is full: build a Storage room." : "{0} piles marked, but storage is full: build a Storage room.", count));
            else game.ShowMessage(Loc.F(count == 1 ? "{0} pile marked for gathering." : "{0} piles marked for gathering.", count));
        }
    }
}
