using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AntColony
{
    /// <summary>
    /// Placing walls and towers on the surface. While a kind is chosen, a see-through preview follows the mouse
    /// (green = fits, red = blocked or too expensive):
    /// - walls: press and drag to lay a straight line of blocks, release to place them,
    /// - towers: click to place one.
    /// Placed blueprints are paid for at once; builders then walk out and build them.
    /// The mode stays on for more placing until right-click or Esc.
    /// </summary>
    public class BuildMode : MonoBehaviour
    {
        /// <summary>Distance between neighbouring wall blocks: exactly touching, so walls have no gaps.</summary>
        const float WallSpacing = Defenses.WallRadius * 2f;
        const float GridSize = 0.5f;
        const int MaxWallBlocksPerLine = 40;

        static readonly Color FitsColor = new Color(0.6f, 1f, 0.6f, 0.55f);
        static readonly Color BlockedColor = new Color(1f, 0.4f, 0.4f, 0.55f);

        readonly List<SpriteRenderer> ghosts = new List<SpriteRenderer>();
        readonly List<Vector2> previewPoints = new List<Vector2>();
        ColonyGame game;
        bool dragging;
        Vector2 dragStart;

        public bool IsActive { get; private set; }
        public StructureKind Kind { get; private set; }

        public void Init(ColonyGame colonyGame)
        {
            game = colonyGame;
        }

        /// <summary>Starts placing this kind, or stops if it was already being placed.</summary>
        public void Toggle(StructureKind kind)
        {
            if (IsActive && Kind == kind)
            {
                Stop();
                return;
            }

            IsActive = true;
            Kind = kind;
            dragging = false;
            game.Gather.Stop();
            game.Selection.Clear();
            if (game.CameraRig.View != CameraView.Surface) game.CameraRig.ToggleView();
        }

        public void Stop()
        {
            IsActive = false;
            dragging = false;
            HideGhosts(0);
            if (game.Defenses != null) game.Defenses.HideRange();
        }

        /// <summary>Called by <see cref="ColonyControls"/> every frame while the surface is on screen and the mode is on.</summary>
        public void HandleMouse(Mouse mouse)
        {
            Vector2 screenPosition = mouse.position.ReadValue();
            bool overUi = game.Hud.IsPointerOverUi(screenPosition);
            Vector2 point = Snap(game.CameraRig.ScreenToWorld(screenPosition));

            if (mouse.rightButton.wasPressedThisFrame)
            {
                Stop();
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame && !overUi)
            {
                dragging = true;
                dragStart = point;
            }

            // Preview: the dragged line for walls, otherwise the single spot under the mouse.
            previewPoints.Clear();
            if (dragging && Kind == StructureKind.Wall) AddWallLine(dragStart, point);
            else previewPoints.Add(point);
            ShowGhosts(overUi && !dragging);
            if (Kind == StructureKind.Tower && !(overUi && !dragging)) game.Defenses.ShowRange(previewPoints[0]);
            else game.Defenses.HideRange();

            if (dragging && mouse.leftButton.wasReleasedThisFrame)
            {
                dragging = false;
                PlacePreview();
            }
        }

        void AddWallLine(Vector2 from, Vector2 to)
        {
            float length = Vector2.Distance(from, to);
            int count = Mathf.Min(MaxWallBlocksPerLine, Mathf.FloorToInt(length / WallSpacing) + 1);
            Vector2 direction = length > 0.0001f ? (to - from) / length : Vector2.zero;
            for (int i = 0; i < count; i++)
                previewPoints.Add(from + direction * (i * WallSpacing));
        }

        void PlacePreview()
        {
            Defenses defenses = game.Defenses;
            int cost = defenses.CostOf(Kind);
            int stoneCost = defenses.StoneCostOf(Kind);
            int placed = 0;

            foreach (Vector2 point in previewPoints)
            {
                if (!defenses.CanPlace(Kind, point)) continue;
                if (!game.Colony.TrySpend(cost, stoneCost)) break;

                defenses.PlaceBlueprint(Kind, point);
                placed++;
            }

            if (placed > 0)
            {
                game.Audio.Play(Sound.UiConfirm);
                game.Tutorial.Notify(Kind == StructureKind.Wall ? TutorialSignal.WallPlaced : TutorialSignal.TowerPlaced);
            }
            if (placed > 0 && game.Colony.CountAnts(AntRole.Builder) == 0)
                game.ShowMessage("Raise a Builder [3] to build it!");
        }

        /// <summary>Draws one see-through structure per preview point, coloured by whether it can be placed.</summary>
        void ShowGhosts(bool hidden)
        {
            if (hidden)
            {
                HideGhosts(0);
                return;
            }

            Sprite sprite = Kind == StructureKind.Wall ? game.Art.Wall : game.Art.Tower;
            float width = Defenses.RadiusOf(Kind) * 2f;
            int affordable = game.Defenses.AffordableCount(Kind);
            int fitting = 0;

            ghosts.RemoveAll(ghost => ghost == null); // the world was rebuilt
            for (int i = 0; i < previewPoints.Count; i++)
            {
                if (i >= ghosts.Count)
                    ghosts.Add(game.Art.CreateSprite("Build Preview", game.SurfaceRoot, sprite, SortingOrder.BuildPreview));

                SpriteRenderer ghost = ghosts[i];
                ghost.enabled = true;
                ghost.sprite = sprite;
                GameArt.ScaleToWidth(ghost, width);
                ghost.transform.position = previewPoints[i];

                bool fits = game.Defenses.CanPlace(Kind, previewPoints[i]) && !OverlapsEarlierPreview(i);
                if (fits) fitting++;
                ghost.color = fits && fitting <= affordable ? FitsColor : BlockedColor;
            }
            HideGhosts(previewPoints.Count);
        }

        /// <summary>Two blocks of the same dragged line never overlap, but a short drag can snap two onto one spot.</summary>
        bool OverlapsEarlierPreview(int index)
        {
            for (int i = 0; i < index; i++)
            {
                if (Vector2.Distance(previewPoints[i], previewPoints[index]) < WallSpacing * 0.9f) return true;
            }
            return false;
        }

        void HideGhosts(int fromIndex)
        {
            for (int i = fromIndex; i < ghosts.Count; i++)
            {
                if (ghosts[i] != null) ghosts[i].enabled = false;
            }
        }

        static Vector2 Snap(Vector2 point)
        {
            return new Vector2(Mathf.Round(point.x / GridSize) * GridSize, Mathf.Round(point.y / GridSize) * GridSize);
        }
    }
}
