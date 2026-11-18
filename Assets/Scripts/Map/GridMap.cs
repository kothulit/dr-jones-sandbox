using System.Collections.Generic;
using UnityEngine;

namespace DrJones
{
    public sealed class GridMap
    {
        public int Width;
        public int Height;
        public int Seed;
        public LevelId Level;
        public CellKind[,] Cells;
        public Vector2Int PlayerStart;
        public Vector2Int ExitCell;
        public List<Vector2Int> Collectibles = new List<Vector2Int>();
        public List<Vector2Int> Hearts = new List<Vector2Int>();
        public List<BoulderDef> Boulders = new List<BoulderDef>();
        public List<LightningDef> Lightnings = new List<LightningDef>();
        public Vector2Int HammerFrom;
        public Vector2Int HammerTo;

        public bool InBounds(Vector2Int c)
        {
            return c.x >= 0 && c.y >= 0 && c.x < Width && c.y < Height;
        }

        public CellKind Get(Vector2Int c)
        {
            return Cells[c.x, c.y];
        }

        public int PathLength(Vector2Int from, Vector2Int to)
        {
            if (from == to)
                return 0;
            var dist = new Dictionary<Vector2Int, int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            dist[from] = 0;
            var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                int steps = dist[cell];
                for (int i = 0; i < dirs.Length; i++)
                {
                    var next = cell + dirs[i];
                    if (dist.ContainsKey(next) || !Passable(next))
                        continue;
                    if (next == to)
                        return steps + 1;
                    dist[next] = steps + 1;
                    queue.Enqueue(next);
                }
            }

            return Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);
        }

        public bool Passable(Vector2Int cell)
        {
            if (!InBounds(cell))
                return false;
            var kind = Get(cell);
            return kind == CellKind.Floor || kind == CellKind.Breakable;
        }

        public Vector3 ExitInward()
        {
            if (ExitCell.x <= 0)
                return Vector3.right;
            if (ExitCell.x >= Width - 1)
                return Vector3.left;
            if (ExitCell.y <= 0)
                return Vector3.forward;
            return Vector3.back;
        }

        public static Vector3 World(Vector2Int cell, float y)
        {
            return new Vector3(cell.x, y, cell.y);
        }
    }
}
