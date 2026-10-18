using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AntColony
{
    /// <summary>
    /// Strategy-game style control of soldiers on the surface:
    /// - left-click a soldier to select it, or drag a box around several (hold Shift to add to the selection),
    /// - right-click an enemy to send the selected soldiers after it, or the ground to move them there,
    /// - with nothing selected, right-click moves the rally flag where new soldiers gather,
    /// - E selects every soldier, Q halves the selection (to split a group).
    /// </summary>
    public class SoldierSelection : MonoBehaviour
    {
        /// <summary>A drag shorter than this (in pixels) counts as a click.</summary>
        const float ClickThreshold = 6f;
        const float PickRadius = 0.8f;
        const float FormationSpacing = 0.9f;

        static readonly Color MoveMarkerColor = new Color(0.55f, 0.95f, 0.45f);
        static readonly Color AttackMarkerColor = new Color(1f, 0.35f, 0.3f);

        readonly List<SoldierAnt> selected = new List<SoldierAnt>();
        ColonyGame game;
        Vector2 dragStart;

        /// <summary>Two clicks on soldiers this close in time (seconds) are a double-click.</summary>
        const float DoubleClickTime = 0.35f;
        SoldierAnt lastClicked;
        float lastClickTime = -10f;

        public IReadOnlyList<SoldierAnt> Selected => selected;
        /// <summary>Move or attack orders given so far, for the goals.</summary>
        public int OrdersGiven { get; private set; }
        public bool IsDragging { get; private set; }
        /// <summary>The drag box in screen pixels (y up), valid while <see cref="IsDragging"/>.</summary>
        public Rect DragRect { get; private set; }

        public void Init(ColonyGame colonyGame)
        {
            game = colonyGame;
        }

        public void Clear()
        {
            foreach (SoldierAnt soldier in selected)
            {
                if (soldier != null) soldier.SetSelected(false);
            }
            selected.Clear();
            IsDragging = false;
        }

        public void SelectAll()
        {
            Clear();
            foreach (SoldierAnt soldier in game.Colony.Soldiers) Add(soldier);
            game.ShowMessage(selected.Count > 0 ? Loc.F("{0} soldiers selected.", selected.Count) : "You have no soldiers yet.");
        }

        /// <summary>
        /// Keeps half of the selected soldiers (rounded up) and lets the rest go, so a group can be split in two.
        /// The ones kept are those on one side of the group, so each half stays together.
        /// </summary>
        public void Halve()
        {
            selected.RemoveAll(soldier => soldier == null);
            if (selected.Count <= 1)
            {
                game.ShowMessage("Only one soldier selected.");
                return;
            }

            // Split along the group's longer side: sort by x if it is wider than tall, else by y.
            Vector2 min = selected[0].Position;
            Vector2 max = min;
            foreach (SoldierAnt soldier in selected)
            {
                min = Vector2.Min(min, soldier.Position);
                max = Vector2.Max(max, soldier.Position);
            }
            bool wide = max.x - min.x >= max.y - min.y;
            selected.Sort((a, b) => wide ? a.Position.x.CompareTo(b.Position.x) : a.Position.y.CompareTo(b.Position.y));

            int keep = (selected.Count + 1) / 2;
            for (int i = selected.Count - 1; i >= keep; i--) Remove(selected[i]);
            game.ShowMessage(Loc.F(keep == 1 ? "{0} soldier selected." : "{0} soldiers selected.", keep));
        }

        /// <summary>Called by <see cref="ColonyControls"/> every frame while the surface is on screen.</summary>
        public void HandleMouse(Mouse mouse, bool shiftHeld)
        {
            selected.RemoveAll(soldier => soldier == null); // dead soldiers
            Vector2 mousePosition = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame && !game.Hud.IsPointerOverUi(mousePosition))
            {
                IsDragging = true;
                dragStart = mousePosition;
            }

            if (IsDragging)
            {
                DragRect = Rect.MinMaxRect(
                    Mathf.Min(dragStart.x, mousePosition.x), Mathf.Min(dragStart.y, mousePosition.y),
                    Mathf.Max(dragStart.x, mousePosition.x), Mathf.Max(dragStart.y, mousePosition.y));

                if (mouse.leftButton.wasReleasedThisFrame)
                {
                    IsDragging = false;
                    if (DragRect.width < ClickThreshold && DragRect.height < ClickThreshold) ClickSelect(mousePosition, shiftHeld);
                    else BoxSelect(DragRect, shiftHeld);
                }
            }

            if (mouse.rightButton.wasPressedThisFrame && !game.Hud.IsPointerOverUi(mousePosition))
                GiveOrder(game.CameraRig.ScreenToWorld(mousePosition));
        }

        /// <summary>Stops a drag that is in progress, e.g. when the view switches to the nest.</summary>
        public void CancelDrag()
        {
            IsDragging = false;
        }

        void ClickSelect(Vector2 screenPosition, bool additive)
        {
            Vector2 point = game.CameraRig.ScreenToWorld(screenPosition);
            SoldierAnt clicked = null;
            float closest = PickRadius;

            foreach (SoldierAnt soldier in game.Colony.Soldiers)
            {
                if (!soldier.IsOnSurface) continue;
                float distance = Vector2.Distance(soldier.Position, point);
                if (distance < closest)
                {
                    clicked = soldier;
                    closest = distance;
                }
            }

            // Double-click: every ant of the same kind (soldiers or spitters) on the surface.
            bool doubleClick = clicked != null && lastClicked != null && clicked.Role == lastClicked.Role
                               && Time.unscaledTime - lastClickTime < DoubleClickTime;
            lastClicked = clicked;
            lastClickTime = Time.unscaledTime;
            if (doubleClick)
            {
                SelectAllOfType(clicked.Role, additive);
                lastClicked = null; // a third click starts over
                return;
            }

            if (!additive) Clear();
            if (clicked == null) return;

            if (additive && clicked.IsSelected) Remove(clicked);
            else Add(clicked);
        }

        /// <summary>Selects every soldier of one kind on the surface (keeping the current ones with Shift).</summary>
        public void SelectAllOfType(AntRole role, bool additive = false)
        {
            if (!additive) Clear();
            foreach (SoldierAnt soldier in game.Colony.Soldiers)
            {
                if (soldier.Role == role && soldier.IsOnSurface) Add(soldier);
            }
            int count = 0;
            foreach (SoldierAnt soldier in selected) if (soldier.Role == role) count++;
            game.ShowMessage(role == AntRole.Spitter
                ? Loc.F(count == 1 ? "All {0} spitter selected." : "All {0} spitters selected.", count)
                : Loc.F(count == 1 ? "All {0} soldier selected." : "All {0} soldiers selected.", count));
        }

        void BoxSelect(Rect screenRect, bool additive)
        {
            if (!additive) Clear();

            Vector2 cornerA = game.CameraRig.ScreenToWorld(screenRect.min);
            Vector2 cornerB = game.CameraRig.ScreenToWorld(screenRect.max);
            Rect worldRect = Rect.MinMaxRect(
                Mathf.Min(cornerA.x, cornerB.x), Mathf.Min(cornerA.y, cornerB.y),
                Mathf.Max(cornerA.x, cornerB.x), Mathf.Max(cornerA.y, cornerB.y));

            foreach (SoldierAnt soldier in game.Colony.Soldiers)
            {
                if (soldier.IsOnSurface && worldRect.Contains(soldier.Position)) Add(soldier);
            }
        }

        void GiveOrder(Vector2 point)
        {
            if (selected.Count == 0)
            {
                game.Colony.SetRallyPoint(point);
                game.ShowMessage("New soldiers will gather at the flag.");
                return;
            }

            OrdersGiven++;
            Enemy enemy = game.Waves.FindEnemyAt(point, PickRadius);
            if (enemy != null)
            {
                foreach (SoldierAnt soldier in selected) soldier.OrderAttack(enemy);
                OrderMarker.Show(game, enemy.Position, AttackMarkerColor);
                return;
            }

            // Spread the group out in a square formation around the clicked point.
            int columns = Mathf.CeilToInt(Mathf.Sqrt(selected.Count));
            for (int i = 0; i < selected.Count; i++)
            {
                int row = i / columns;
                int column = i % columns;
                var offset = new Vector2(column - (columns - 1) * 0.5f, row - (columns - 1) * 0.5f) * FormationSpacing;
                selected[i].OrderMove(point + offset);
            }
            OrderMarker.Show(game, point, MoveMarkerColor);
        }

        void Add(SoldierAnt soldier)
        {
            if (soldier.IsSelected) return;
            soldier.SetSelected(true);
            selected.Add(soldier);
        }

        void Remove(SoldierAnt soldier)
        {
            soldier.SetSelected(false);
            selected.Remove(soldier);
        }
    }
}
