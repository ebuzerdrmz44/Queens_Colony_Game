using System.Collections.Generic;
using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// Every wall block and tower on the surface: where they may be placed, what they cost,
    /// which one a builder should work on next and which one is in an enemy's way.
    /// </summary>
    public class Defenses : MonoBehaviour
    {
        public const float WallRadius = 0.5f;
        public const float TowerRadius = 0.8f;
        /// <summary>Keep the ant hill itself free so ants can always get in and out.</summary>
        const float EntranceClearance = 2f;
        /// <summary>Neighbouring wall blocks may touch (or overlap a hair) so walls have no gaps.</summary>
        const float OverlapTolerance = 0.05f;

        static readonly Color RangeColor = new Color(0.55f, 0.85f, 0.25f, 0.8f);

        readonly List<Structure> structures = new List<Structure>();
        ColonyGame game;
        SpriteRenderer rangeView;

        public IReadOnlyList<Structure> Structures => structures;

        public void Init(ColonyGame colonyGame)
        {
            game = colonyGame;
            rangeView = game.Art.CreateSprite("Tower Range", transform, game.Art.RangeCircle, SortingOrder.OrderMarkers);
            rangeView.color = RangeColor;
            rangeView.enabled = false;
        }

        /// <summary>Shows the range of the tower under the mouse (a placement preview uses <see cref="ShowRange"/>).</summary>
        void LateUpdate()
        {
            Structure hovered = null;
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && game.CameraRig.View == CameraView.Surface && !game.BuildMode.IsActive)
            {
                Vector2 point = game.CameraRig.ScreenToWorld(mouse.position.ReadValue());
                foreach (Structure structure in structures)
                {
                    if (structure.Kind != StructureKind.Tower) continue;
                    if (Vector2.Distance(point, structure.Position) <= structure.Radius) hovered = structure;
                }
            }

            if (hovered != null) ShowRange(hovered.Position);
            else if (!game.BuildMode.IsActive || game.BuildMode.Kind != StructureKind.Tower) HideRange();
        }

        /// <summary>Draws a tower's range circle around <paramref name="center"/>.</summary>
        public void ShowRange(Vector2 center)
        {
            rangeView.enabled = true;
            GameArt.ScaleToWidth(rangeView, game.Settings.towerRange * 2f);
            rangeView.transform.position = center;
        }

        public void HideRange()
        {
            rangeView.enabled = false;
        }

        public static float RadiusOf(StructureKind kind) => kind == StructureKind.Wall ? WallRadius : TowerRadius;

        /// <summary>Food price: walls are stone only, towers cost food and stone.</summary>
        public int CostOf(StructureKind kind) => kind == StructureKind.Wall ? 0 : game.Settings.towerCost;

        public int StoneCostOf(StructureKind kind) => kind == StructureKind.Wall ? game.Settings.wallCost : game.Settings.towerStoneCost;

        /// <summary>How many of this kind the colony can pay for right now.</summary>
        public int AffordableCount(StructureKind kind)
        {
            int food = CostOf(kind);
            int stone = StoneCostOf(kind);
            int byFood = food > 0 ? game.Colony.Food / food : int.MaxValue;
            int byStone = stone > 0 ? game.Colony.Stone / stone : int.MaxValue;
            return Mathf.Min(byFood, byStone);
        }

        /// <summary>True if a structure of this kind fits at the position (inside the field, off the ant hill, not on another structure).</summary>
        public bool CanPlace(StructureKind kind, Vector2 position)
        {
            float radius = RadiusOf(kind);
            Rect bounds = game.Surface.Bounds;
            bool inside = position.x - radius >= bounds.xMin && position.x + radius <= bounds.xMax
                       && position.y - radius >= bounds.yMin && position.y + radius <= bounds.yMax;
            if (!inside) return false;

            if (Vector2.Distance(position, game.Surface.EntrancePoint) < EntranceClearance + radius) return false;
            if (game.Surface.IsBlocked(position, radius)) return false;

            foreach (Structure structure in structures)
            {
                if (Vector2.Distance(position, structure.Position) < radius + structure.Radius - OverlapTolerance) return false;
            }
            return true;
        }

        /// <summary>Places a blueprint for builders to finish. The caller has already paid for it.</summary>
        public Structure PlaceBlueprint(StructureKind kind, Vector2 position)
        {
            Structure structure;
            Structure prefab = game.Prefabs.StructureFor(kind);
            if (prefab != null)
            {
                structure = Instantiate(prefab, transform, false);
                structure.name = kind.ToString();
            }
            else
            {
                var structureObject = new GameObject(kind.ToString());
                structureObject.transform.SetParent(transform, false);
                structure = structureObject.AddComponent<Structure>();
            }
            structure.Init(game, kind, position, RadiusOf(kind));
            structures.Add(structure);
            return structure;
        }

        public void Remove(Structure structure)
        {
            structures.Remove(structure);
        }

        // ---------- Saving ----------

        public void Write(SaveData data)
        {
            foreach (Structure structure in structures)
            {
                data.structures.Add(new StructureSave
                {
                    kind = (int)structure.Kind,
                    position = structure.Position,
                    built = structure.IsBuilt,
                    buildProgress = structure.BuildProgress,
                    health = structure.IsBuilt ? structure.Health.Current : 0f,
                });
            }
        }

        public void Read(SaveData data)
        {
            foreach (StructureSave saved in data.structures)
            {
                Structure structure = PlaceBlueprint((StructureKind)saved.kind, saved.position);
                structure.Restore(saved.built, saved.buildProgress, saved.health);
            }
        }

        /// <summary>The closest structure that is unfinished or damaged, or null. Unfinished ones come first.</summary>
        public Structure FindJob(Vector2 from)
        {
            Structure best = null;
            float bestScore = float.MaxValue;

            foreach (Structure structure in structures)
            {
                if (!structure.NeedsWork) continue;

                float score = Vector2.Distance(from, structure.Position);
                if (structure.IsBuilt) score += 1000f; // repairs wait until all blueprints are done
                if (score < bestScore)
                {
                    best = structure;
                    bestScore = score;
                }
            }
            return best;
        }

        /// <summary>
        /// A finished structure that a walker of <paramref name="radius"/> at <paramref name="position"/>, heading
        /// along <paramref name="heading"/>, is pressed up against (within <paramref name="gap"/> of its edge). Or null.
        /// </summary>
        public Structure FindBlocking(Vector2 position, float radius, Vector2 heading, float gap)
        {
            foreach (Structure structure in structures)
            {
                if (!structure.IsBuilt) continue;

                Vector2 toStructure = structure.Position - position;
                float edgeDistance = toStructure.magnitude - radius - structure.Radius;
                if (edgeDistance > gap) continue;
                if (Vector2.Dot(toStructure.normalized, heading.normalized) > 0.2f) return structure;
            }
            return null;
        }
    }
}
