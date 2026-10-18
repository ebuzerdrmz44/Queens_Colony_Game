using System.Collections.Generic;
using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// Route finding on the surface: a grid of half-unit cells over the meadow, each walkable or blocked by solid
    /// scenery (boulders, logs, stumps, tree trunks) or a finished wall or tower. Routes are found with A* (eight
    /// directions, no cutting past blocked corners) and then straightened: a waypoint is kept only where the line to
    /// the next one would hit something. Walls and towers are re-read whenever they change.
    /// </summary>
    public class SurfaceNav
    {
        const float CellSize = 0.5f;
        /// <summary>Room kept around obstacles: about an ant's half-width, so routes don't scrape along them.</summary>
        const float Clearance = 0.35f;
        /// <summary>How far (in cells) to look for a free cell when the start or goal is inside something.</summary>
        const int FreeCellSearch = 6;

        static readonly Vector2Int[] Steps =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1),
        };

        readonly Rect area;
        readonly int width;
        readonly int height;
        readonly bool[,] sceneryBlocked;
        readonly bool[,] blocked;
        /// <summary>The grid the current search uses: with walls and towers (ants) or scenery only (enemies chew walls).</summary>
        bool[,] active;
        int structureSignature = int.MinValue;

        // A* working arrays, reused between searches.
        readonly float[,] cost;
        readonly Vector2Int[,] cameFrom;
        readonly int[,] visitedStamp;
        int stamp;

        public SurfaceNav(Rect playArea, IEnumerable<Obstacle> scenery)
        {
            area = playArea;
            width = Mathf.CeilToInt(area.width / CellSize) + 1;
            height = Mathf.CeilToInt(area.height / CellSize) + 1;
            sceneryBlocked = new bool[width, height];
            blocked = new bool[width, height];
            cost = new float[width, height];
            cameFrom = new Vector2Int[width, height];
            visitedStamp = new int[width, height];

            foreach (Obstacle obstacle in scenery) Mark(sceneryBlocked, obstacle.Center, obstacle.Radius);
        }

        /// <summary>Re-reads walls and towers if any were built or removed since last time.</summary>
        public void Refresh(IReadOnlyList<Structure> structures)
        {
            int signature = 17;
            if (structures != null)
            {
                foreach (Structure structure in structures)
                {
                    if (structure == null || !structure.IsBuilt) continue;
                    signature = signature * 31 + structure.GetInstanceID();
                }
            }
            if (signature == structureSignature) return;
            structureSignature = signature;

            System.Array.Copy(sceneryBlocked, blocked, sceneryBlocked.Length);
            if (structures == null) return;
            foreach (Structure structure in structures)
            {
                if (structure != null && structure.IsBuilt) Mark(blocked, structure.Position, structure.Radius);
            }
        }

        void Mark(bool[,] grid, Vector2 center, float radius)
        {
            float reach = radius + Clearance;
            Vector2Int min = ToCell(center - Vector2.one * reach);
            Vector2Int max = ToCell(center + Vector2.one * reach);
            for (int x = Mathf.Max(0, min.x); x <= Mathf.Min(width - 1, max.x); x++)
            {
                for (int y = Mathf.Max(0, min.y); y <= Mathf.Min(height - 1, max.y); y++)
                {
                    if (Vector2.Distance(ToWorld(new Vector2Int(x, y)), center) < reach) grid[x, y] = true;
                }
            }
        }

        Vector2Int ToCell(Vector2 point) => new Vector2Int(
            Mathf.RoundToInt((point.x - area.xMin) / CellSize),
            Mathf.RoundToInt((point.y - area.yMin) / CellSize));

        Vector2 ToWorld(Vector2Int cell) => new Vector2(area.xMin + cell.x * CellSize, area.yMin + cell.y * CellSize);

        bool InGrid(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;

        bool IsFree(Vector2Int cell) => InGrid(cell) && !active[cell.x, cell.y];

        /// <summary>
        /// Waypoints from <paramref name="from"/> to <paramref name="to"/> (not including the start, ending at
        /// <paramref name="to"/> itself), straightened. False if there is no way through.
        /// </summary>
        /// <param name="includeStructures">False to walk as if walls and towers weren't there (enemies chew through them).</param>
        public bool FindPath(Vector2 from, Vector2 to, List<Vector2> waypoints, bool includeStructures = true)
        {
            active = includeStructures ? blocked : sceneryBlocked;
            waypoints.Clear();
            if (IsLineClear(from, to))
            {
                waypoints.Add(to);
                return true;
            }

            if (!TryNearestFree(ToCell(from), out Vector2Int start) || !TryNearestFree(ToCell(to), out Vector2Int goal)) return false;
            if (!Search(start, goal)) return false;

            // Walk back from the goal to get the cells in order.
            var cells = new List<Vector2Int>();
            for (Vector2Int cell = goal; cell != start; cell = cameFrom[cell.x, cell.y]) cells.Add(cell);
            cells.Reverse();

            // Straighten: from each kept point, skip ahead to the farthest cell still in clear view.
            Vector2 current = from;
            int index = 0;
            while (index < cells.Count)
            {
                int farthest = index;
                for (int i = cells.Count - 1; i > index; i--)
                {
                    if (IsLineClear(current, ToWorld(cells[i])))
                    {
                        farthest = i;
                        break;
                    }
                }
                if (IsLineClear(current, to))
                {
                    break;
                }
                current = ToWorld(cells[farthest]);
                waypoints.Add(current);
                index = farthest + 1;
            }
            waypoints.Add(to); // the goal itself, even if it is a structure's edge (the walker stops at the edge)
            return true;
        }

        bool Search(Vector2Int start, Vector2Int goal)
        {
            stamp++;
            var open = new SortedSet<(float, int, int, int)>(); // (estimate, tie-break, x, y)
            int order = 0;
            cost[start.x, start.y] = 0f;
            visitedStamp[start.x, start.y] = stamp;
            open.Add((Heuristic(start, goal), order++, start.x, start.y));

            while (open.Count > 0)
            {
                var best = open.Min;
                open.Remove(best);
                var cell = new Vector2Int(best.Item3, best.Item4);
                if (cell == goal) return true;

                foreach (Vector2Int step in Steps)
                {
                    Vector2Int next = cell + step;
                    if (!IsFree(next)) continue;
                    // No cutting past a blocked corner.
                    if (step.x != 0 && step.y != 0 && (!IsFree(new Vector2Int(cell.x + step.x, cell.y)) || !IsFree(new Vector2Int(cell.x, cell.y + step.y))))
                        continue;

                    float nextCost = cost[cell.x, cell.y] + (step.x != 0 && step.y != 0 ? 1.4142f : 1f);
                    bool seen = visitedStamp[next.x, next.y] == stamp;
                    if (seen && nextCost >= cost[next.x, next.y]) continue;

                    visitedStamp[next.x, next.y] = stamp;
                    cost[next.x, next.y] = nextCost;
                    cameFrom[next.x, next.y] = cell;
                    open.Add((nextCost + Heuristic(next, goal), order++, next.x, next.y));
                }
            }
            return false;
        }

        static float Heuristic(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dy = Mathf.Abs(a.y - b.y);
            return Mathf.Max(dx, dy) + 0.4142f * Mathf.Min(dx, dy);
        }

        /// <summary>The nearest walkable cell to <paramref name="cell"/> (itself if free), searching outward in rings.</summary>
        bool TryNearestFree(Vector2Int cell, out Vector2Int free)
        {
            cell = new Vector2Int(Mathf.Clamp(cell.x, 0, width - 1), Mathf.Clamp(cell.y, 0, height - 1));
            free = cell;
            if (IsFree(cell)) return true;

            for (int ring = 1; ring <= FreeCellSearch; ring++)
            {
                float best = float.MaxValue;
                for (int x = -ring; x <= ring; x++)
                {
                    for (int y = -ring; y <= ring; y++)
                    {
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != ring) continue;
                        var candidate = new Vector2Int(cell.x + x, cell.y + y);
                        if (!IsFree(candidate)) continue;
                        float distance = x * x + y * y;
                        if (distance < best)
                        {
                            best = distance;
                            free = candidate;
                        }
                    }
                }
                if (best < float.MaxValue) return true;
            }
            return false;
        }

        /// <summary>True if a walker could go straight from <paramref name="a"/> to <paramref name="b"/> without touching a blocked cell.</summary>
        public bool IsLineClear(Vector2 a, Vector2 b)
        {
            float distance = Vector2.Distance(a, b);
            int samples = Mathf.CeilToInt(distance / (CellSize * 0.5f));
            for (int i = 1; i < samples; i++)
            {
                Vector2Int cell = ToCell(Vector2.Lerp(a, b, i / (float)samples));
                if (InGrid(cell) && (active ?? blocked)[cell.x, cell.y]) return false;
            }
            return true;
        }
    }
}
