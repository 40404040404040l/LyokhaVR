using System.Collections.Generic;
using UnityEngine;

namespace LyokhaVR
{
    /// <summary>Small static grid built from actual courtyard colliders; walls also block edges.</summary>
    public sealed class CourtyardNavigation : MonoBehaviour
    {
        public Vector2 Minimum = new Vector2(-4, -61);
        public Vector2 Maximum = new Vector2(24, 42);
        private readonly Dictionary<Vector2Int, Vector3> cells = new Dictionary<Vector2Int, Vector3>();
        private static readonly Vector2Int[] Neighbours = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
        public int WalkableCells => cells.Count;

        public void Scan()
        {
            cells.Clear();
            Physics.SyncTransforms();
            for (int x = (int)Minimum.x; x <= Maximum.x; x++)
            for (int z = (int)Minimum.y; z <= Maximum.y; z++)
            {
                if (!Physics.Raycast(new Vector3(x, 2, z), Vector3.down, out var hit, 4, 1, QueryTriggerInteraction.Ignore) || hit.normal.y < .8f || hit.point.y > .55f) continue;
                var foot = hit.point + Vector3.up * .04f;
                if (Physics.CheckCapsule(foot + Vector3.up * .38f, foot + Vector3.up * 1.45f, .34f, 1, QueryTriggerInteraction.Ignore)) continue;
                cells[new Vector2Int(x, z)] = foot;
            }
        }

        public List<Vector3> FindPath(Vector3 from, Vector3 to)
        {
            if (cells.Count == 0) Scan();
            var start = Nearest(from); var goal = Nearest(to);
            var open = new List<Vector2Int> { start };
            var previous = new Dictionary<Vector2Int, Vector2Int>();
            var costs = new Dictionary<Vector2Int, float> { [start] = 0 };
            var closed = new HashSet<Vector2Int>();
            while (open.Count > 0)
            {
                int best = 0;
                for (int i = 1; i < open.Count; i++)
                    if (costs[open[i]] + Distance(open[i], goal) < costs[open[best]] + Distance(open[best], goal)) best = i;
                var current = open[best]; open.RemoveAt(best);
                if (current == goal)
                {
                    var path = new List<Vector3> { cells[current] };
                    while (previous.TryGetValue(current, out var p)) { current = p; path.Add(cells[current]); }
                    path.Reverse(); return path;
                }
                if (!closed.Add(current)) continue;
                foreach (var step in Neighbours)
                {
                    var next = current + step;
                    if (closed.Contains(next) || !cells.ContainsKey(next)) continue;
                    var delta = cells[next] - cells[current];
                    if (Physics.CapsuleCast(cells[current] + Vector3.up * .4f, cells[current] + Vector3.up * 1.4f, .32f, delta.normalized, delta.magnitude, 1, QueryTriggerInteraction.Ignore)) continue;
                    var cost = costs[current] + delta.magnitude;
                    if (costs.TryGetValue(next, out var old) && old <= cost) continue;
                    costs[next] = cost; previous[next] = current;
                    if (!open.Contains(next)) open.Add(next);
                }
            }
            return new List<Vector3>();
        }

        private Vector2Int Nearest(Vector3 target)
        {
            var result = default(Vector2Int); float best = float.MaxValue;
            foreach (var cell in cells)
            {
                var d = (cell.Value - target).sqrMagnitude;
                if (d < best) { best = d; result = cell.Key; }
            }
            return result;
        }
        private static int Distance(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
