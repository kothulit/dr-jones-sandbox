using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrJones
{
    /// <summary>
    /// Level 1: a braided maze crossed by full-span boulder lanes; each boulder waits in a dead-end niche beside its lane.
    /// Level 2: a maze plus one-way corridors from each fragment to a border wall.
    /// </summary>
    public static class LevelGenerator
    {
        public static string LastError = "";

        static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        sealed class Lane
        {
            public bool Horizontal;
            public int Fixed;
            public int Low;
            public int High;
        }

        public static GridMap Generate(LevelId level, int width, int height, int seed)
        {
            width = Mathf.Max(width, 21);
            height = Mathf.Max(height, 17);
            if (seed == 0)
                seed = Environment.TickCount;

            for (int attempt = 0; attempt < 60; attempt++)
            {
                int trySeed = seed + attempt * 997;
                GridMap map = level == LevelId.BoulderTemple
                    ? TryLevel1(width, height, trySeed)
                    : TryLevel2(width, height, trySeed);
                if (map == null)
                    continue;

                var rng = new System.Random(trySeed ^ 0x51A7);
                if (rng.Next(2) == 1)
                    map = MirrorX(map);
                if (width == height)
                {
                    int turns = rng.Next(4);
                    for (int t = 0; t < turns; t++)
                        map = Rotate90(map);
                }
                else if (rng.Next(2) == 1)
                {
                    map = Rotate90(Rotate90(map));
                }

                if (!Validate(map, out string error))
                {
                    LastError = error;
                    continue;
                }

                map.Seed = trySeed;
                LastError = "";
                return map;
            }

            throw new InvalidOperationException("Не удалось собрать проходимую карту: " + LastError);
        }

        public static int SelfTest(int seeds)
        {
            int ok = 0;
            for (int i = 1; i <= seeds; i++)
            {
                var a = Generate(LevelId.BoulderTemple, 41, 31, i * 13);
                var b = Generate(LevelId.ThorTemple, 41, 31, i * 29);
                if (!Validate(a, out string ea))
                    throw new InvalidOperationException("L1 seed " + i + ": " + ea);
                if (!Validate(b, out string eb))
                    throw new InvalidOperationException("L2 seed " + i + ": " + eb);
                ok += 2;
            }

            return ok;
        }

        static GridMap TryLevel1(int width, int height, int seed)
        {
            var rng = new System.Random(seed);
            var map = NewMap(width, height, LevelId.BoulderTemple);
            if (!CarveFullMaze(map, rng))
                return null;

            var lanes = PickLanes(rng, width, height);
            if (lanes == null)
            {
                LastError = "Не удалось разложить коридоры валунов.";
                return null;
            }

            var laneCells = new HashSet<Vector2Int>();
            for (int i = 0; i < lanes.Count; i++)
            {
                for (int along = lanes[i].Low; along <= lanes[i].High; along++)
                {
                    var cell = OnLane(lanes[i], along);
                    map.Cells[cell.x, cell.y] = CellKind.Floor;
                    laneCells.Add(cell);
                }
            }

            var nooks = new List<Vector2Int>();
            var entries = new List<Vector2Int>();
            for (int i = 0; i < lanes.Count; i++)
            {
                if (!TryCarveNook(map, lanes[i], laneCells, nooks, rng, out var nook, out var entry))
                {
                    LastError = "Нет ниши для валуна " + i + ".";
                    return null;
                }

                nooks.Add(nook);
                entries.Add(entry);
            }

            Braid(map, rng, nooks, 0.12f);

            for (int i = 0; i < 4; i++)
            {
                map.Boulders.Add(new BoulderDef
                {
                    Idle = nooks[i],
                    Entry = entries[i],
                    PatrolA = OnLane(lanes[i], lanes[i].Low),
                    PatrolB = OnLane(lanes[i], lanes[i].High),
                    Speed = 2.4f + (float)rng.NextDouble() * 1.2f,
                    Special = false
                });
            }

            var exitLane = lanes[4];
            bool exitAtLow = rng.Next(2) == 0;
            int border = exitAtLow ? 0 : (exitLane.Horizontal ? width - 1 : height - 1);
            map.ExitCell = exitLane.Horizontal ? new Vector2Int(border, exitLane.Fixed) : new Vector2Int(exitLane.Fixed, border);
            map.Cells[map.ExitCell.x, map.ExitCell.y] = CellKind.Breakable;
            map.Boulders.Add(new BoulderDef
            {
                Idle = nooks[4],
                Entry = entries[4],
                ChargeTo = map.ExitCell,
                DashTo = OnLane(exitLane, exitAtLow ? exitLane.High : exitLane.Low),
                Speed = 3.4f + (float)rng.NextDouble() * 0.8f,
                Special = true
            });

            var avoid = new HashSet<Vector2Int>(laneCells);
            for (int i = 0; i < nooks.Count; i++)
            {
                avoid.Add(nooks[i]);
                for (int d = 0; d < 4; d++)
                    avoid.Add(nooks[i] + Dirs[d]);
            }

            if (!TryPlaceHalls(map, avoid, rng, out var halls))
            {
                LastError = "Не удалось вырезать залы для идолов.";
                return null;
            }

            map.PlayerStart = halls[0];
            for (int i = 1; i < halls.Count; i++)
            {
                map.Collectibles.Add(halls[i]);
                avoid.Add(halls[i]);
            }

            PlaceHeart(map, FloorCells(map, avoid), rng);
            if (!Validate(map, out string error))
            {
                LastError = error;
                return null;
            }

            return map;
        }

        static bool CarveFullMaze(GridMap map, System.Random rng)
        {
            int nx = 0;
            while (1 + nx * 2 <= map.Width - 2)
                nx++;
            int nz = 0;
            while (1 + nz * 2 <= map.Height - 2)
                nz++;
            if (nx < 4 || nz < 4)
            {
                LastError = "Слишком маленькая карта.";
                return false;
            }

            CarveMaze(map, rng, new bool[nx, nz], nx, nz, 0, 0);
            return true;
        }

        static List<Lane> PickLanes(System.Random rng, int width, int height)
        {
            var lanes = new List<Lane>();
            int guard = 0;
            while (lanes.Count < 5 && guard++ < 600)
            {
                int horizontalCount = 0;
                for (int i = 0; i < lanes.Count; i++)
                {
                    if (lanes[i].Horizontal)
                        horizontalCount++;
                }

                bool horizontal = rng.Next(2) == 0;
                if (horizontalCount >= 3)
                    horizontal = false;
                if (lanes.Count - horizontalCount >= 3)
                    horizontal = true;

                int limit = horizontal ? height : width;
                int slots = (limit - 6) / 2;
                if (slots < 2)
                    return null;
                int index = 3 + 2 * rng.Next(slots);
                bool crowded = false;
                for (int i = 0; i < lanes.Count; i++)
                {
                    if (lanes[i].Horizontal == horizontal && Mathf.Abs(lanes[i].Fixed - index) < 6)
                        crowded = true;
                }

                if (crowded)
                    continue;

                lanes.Add(new Lane
                {
                    Horizontal = horizontal,
                    Fixed = index,
                    Low = 1,
                    High = (horizontal ? width : height) - 2
                });
            }

            if (lanes.Count < 5)
                return null;
            Shuffle(lanes, rng);
            return lanes;
        }

        static bool TryCarveNook(GridMap map, Lane lane, HashSet<Vector2Int> laneCells, List<Vector2Int> nooks, System.Random rng, out Vector2Int nook, out Vector2Int entry)
        {
            var side = lane.Horizontal ? Vector2Int.up : Vector2Int.right;
            var options = new List<(Vector2Int nook, Vector2Int entry)>();
            for (int along = lane.Low + 2; along <= lane.High - 2; along++)
            {
                var laneCell = OnLane(lane, along);
                if (CountLanes(laneCells, laneCell, lane) > 0)
                    continue;
                for (int s = -1; s <= 1; s += 2)
                {
                    var candidate = laneCell + side * s;
                    if (!map.InBounds(candidate) || IsPerimeter(map, candidate) || map.Get(candidate) != CellKind.Wall)
                        continue;
                    if (laneCells.Contains(candidate))
                        continue;
                    bool ok = true;
                    for (int d = 0; d < 4 && ok; d++)
                    {
                        var neighbor = candidate + Dirs[d];
                        if (neighbor == laneCell)
                            continue;
                        if (map.Get(neighbor) != CellKind.Wall || laneCells.Contains(neighbor))
                            ok = false;
                        for (int n = 0; n < nooks.Count && ok; n++)
                        {
                            if (Manhattan(nooks[n], candidate) < 3)
                                ok = false;
                        }
                    }

                    if (ok)
                        options.Add((candidate, laneCell));
                }
            }

            if (options.Count == 0)
            {
                nook = entry = default;
                return false;
            }

            var pick = options[rng.Next(options.Count)];
            nook = pick.nook;
            entry = pick.entry;
            map.Cells[nook.x, nook.y] = CellKind.Floor;
            return true;
        }

        static int CountLanes(HashSet<Vector2Int> laneCells, Vector2Int cell, Lane own)
        {
            var across = own.Horizontal ? Vector2Int.up : Vector2Int.right;
            int count = 0;
            if (laneCells.Contains(cell + across))
                count++;
            if (laneCells.Contains(cell - across))
                count++;
            return count;
        }

        static void Braid(GridMap map, System.Random rng, List<Vector2Int> nooks, float chance)
        {
            var nookSet = new HashSet<Vector2Int>(nooks);
            for (int y = 1; y < map.Height - 1; y++)
            {
                for (int x = 1; x < map.Width - 1; x++)
                {
                    if (((x + y) & 1) == 0 || map.Cells[x, y] != CellKind.Wall)
                        continue;
                    var cell = new Vector2Int(x, y);
                    bool nearNook = false;
                    for (int d = 0; d < 4; d++)
                    {
                        if (nookSet.Contains(cell + Dirs[d]))
                            nearNook = true;
                    }

                    if (nearNook)
                        continue;
                    bool horizontalGap = map.Cells[x - 1, y] == CellKind.Floor && map.Cells[x + 1, y] == CellKind.Floor;
                    bool verticalGap = map.Cells[x, y - 1] == CellKind.Floor && map.Cells[x, y + 1] == CellKind.Floor;
                    if ((horizontalGap || verticalGap) && rng.NextDouble() < chance)
                        map.Cells[x, y] = CellKind.Floor;
                }
            }
        }

        static List<Vector2Int> FloorCells(GridMap map, HashSet<Vector2Int> avoid)
        {
            var result = new List<Vector2Int>();
            for (int y = 1; y < map.Height - 1; y++)
            {
                for (int x = 1; x < map.Width - 1; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (map.Get(cell) == CellKind.Floor && (avoid == null || !avoid.Contains(cell)))
                        result.Add(cell);
                }
            }

            return result;
        }

        static bool TryPlaceHalls(GridMap map, HashSet<Vector2Int> blocked, System.Random rng, out List<Vector2Int> halls)
        {
            halls = new List<Vector2Int>();
            var seeds = FloorCells(map, blocked);
            Shuffle(seeds, rng);
            var picks = new List<Vector2Int>();
            for (int i = 0; i < seeds.Count && picks.Count < 6; i++)
            {
                var seed = seeds[i];
                if (!CanExpandHall(map, seed, blocked))
                    continue;
                bool far = true;
                for (int p = 0; p < picks.Count; p++)
                {
                    if (Manhattan(seed, picks[p]) < 7)
                        far = false;
                }

                if (!far)
                    continue;
                picks.Add(seed);
            }

            if (picks.Count < 6)
                return false;

            picks.Sort((a, b) => Manhattan(b, map.ExitCell).CompareTo(Manhattan(a, map.ExitCell)));
            for (int i = 0; i < picks.Count; i++)
            {
                ExpandHall(map, picks[i], blocked);
                halls.Add(picks[i]);
            }

            return true;
        }

        static bool CanExpandHall(GridMap map, Vector2Int center, HashSet<Vector2Int> blocked)
        {
            if (blocked.Contains(center) || EdgeDist(map, center) < 4 || center == map.ExitCell)
                return false;
            int usable = 0;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var cell = new Vector2Int(center.x + dx, center.y + dy);
                    if (!map.InBounds(cell) || IsPerimeter(map, cell) || blocked.Contains(cell) || cell == map.ExitCell)
                        continue;
                    usable++;
                }
            }

            return usable >= 6;
        }

        static void ExpandHall(GridMap map, Vector2Int center, HashSet<Vector2Int> blocked)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var cell = new Vector2Int(center.x + dx, center.y + dy);
                    if (!map.InBounds(cell) || IsPerimeter(map, cell) || blocked.Contains(cell))
                        continue;
                    map.Cells[cell.x, cell.y] = CellKind.Floor;
                }
            }
        }

        static Vector2Int PickStart(GridMap map, List<Vector2Int> pool, System.Random rng)
        {
            int need = (map.Width + map.Height) / 3;
            var far = new List<Vector2Int>();
            for (int i = 0; i < pool.Count; i++)
            {
                if (Manhattan(pool[i], map.ExitCell) >= need)
                    far.Add(pool[i]);
            }

            var source = far.Count > 0 ? far : pool;
            return source[rng.Next(source.Count)];
        }

        static List<Vector2Int> SpreadPick(List<Vector2Int> pool, Vector2Int start, int count, int minFromStart, System.Random rng)
        {
            var chosen = new List<Vector2Int>();
            var candidates = new List<Vector2Int>();
            for (int i = 0; i < pool.Count; i++)
            {
                if (Manhattan(pool[i], start) >= minFromStart)
                    candidates.Add(pool[i]);
            }

            Shuffle(candidates, rng);
            while (chosen.Count < count && candidates.Count > 0)
            {
                int bestIndex = 0;
                int bestScore = -1;
                for (int i = 0; i < candidates.Count; i++)
                {
                    int score = Manhattan(candidates[i], start) / 2;
                    for (int c = 0; c < chosen.Count; c++)
                        score = Mathf.Min(score, Manhattan(candidates[i], chosen[c]));
                    if (chosen.Count == 0)
                        score = Mathf.Min(Manhattan(candidates[i], start), 14) + rng.Next(6);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }

                chosen.Add(candidates[bestIndex]);
                pool.Remove(candidates[bestIndex]);
                candidates.RemoveAt(bestIndex);
            }

            return chosen;
        }

        static void PlaceHeart(GridMap map, List<Vector2Int> pool, System.Random rng)
        {
            var taken = new List<Vector2Int>(map.Collectibles) { map.PlayerStart };
            int bestScore = -1;
            var best = new Vector2Int(-1, -1);
            for (int i = 0; i < pool.Count; i++)
            {
                int score = 99;
                for (int t = 0; t < taken.Count; t++)
                    score = Mathf.Min(score, Manhattan(pool[i], taken[t]));
                score = Mathf.Min(score, 10) * 4 + rng.Next(4);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = pool[i];
                }
            }

            if (best.x >= 0)
            {
                map.Hearts.Add(best);
                pool.Remove(best);
            }
        }

        static GridMap TryLevel2(int width, int height, int seed)
        {
            var rng = new System.Random(seed);
            var map = NewMap(width, height, LevelId.ThorTemple);
            if (!CarveFullMaze(map, rng))
                return null;

            if (!TryPickHammerRun(map, out var from, out var exit))
            {
                LastError = "Нет прямого полёта молота.";
                return null;
            }

            map.HammerFrom = from;
            map.HammerTo = exit;
            map.ExitCell = exit;
            map.Cells[exit.x, exit.y] = CellKind.Breakable;

            var avoid = new HashSet<Vector2Int> { from, exit };
            var walk = from;
            var step = StepToward(from, exit);
            int guard = 0;
            while (walk != exit && guard++ < 128)
            {
                avoid.Add(walk);
                walk += step;
            }

            map.PlayerStart = FarthestFloor(map, exit, avoid);
            if (map.PlayerStart.x < 0)
            {
                LastError = "Нет старта на уровне 2.";
                return null;
            }

            var pool = new List<Vector2Int>();
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (map.Get(cell) != CellKind.Floor || cell == map.PlayerStart)
                        continue;
                    if (EdgeDist(map, cell) < 4)
                        continue;
                    int dx = cell.x - map.ExitCell.x;
                    int dy = cell.y - map.ExitCell.y;
                    if (dx * dx + dy * dy <= 15 * 15)
                        continue;
                    pool.Add(cell);
                }
            }

            Shuffle(pool, rng);
            var chosen = new List<Vector2Int>();
            while (chosen.Count < 8 && pool.Count > 0)
            {
                int bestIndex = -1;
                int bestScore = -1;
                for (int i = 0; i < pool.Count; i++)
                {
                    int spread = 99;
                    for (int c = 0; c < chosen.Count; c++)
                        spread = Mathf.Min(spread, Manhattan(pool[i], chosen[c]));
                    if (chosen.Count == 0)
                        spread = Manhattan(pool[i], map.PlayerStart);
                    int score = Mathf.Min(spread, 11) * 3 + Mathf.Min(EdgeDist(map, pool[i]), 8);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }

                var pick = pool[bestIndex];
                pool.RemoveAt(bestIndex);
                if (!TryAimLightning(map, rng, pick, out var def))
                    continue;
                CarveRay(map, def.Origin, def.Direction);
                chosen.Add(pick);
                map.Lightnings.Add(def);
            }

            if (chosen.Count < 8)
            {
                LastError = "Не хватило коридоров для молний.";
                return null;
            }

            map.Collectibles = chosen;
            var rays = new HashSet<Vector2Int>();
            for (int i = 0; i < map.Lightnings.Count; i++)
            {
                var cursor = map.Lightnings[i].Origin;
                while (map.InBounds(cursor) && !IsPerimeter(map, cursor))
                {
                    rays.Add(cursor);
                    cursor += map.Lightnings[i].Direction;
                }
            }

            rays.Add(map.PlayerStart);
            PlaceHeart(map, FloorCells(map, rays), rng);
            if (!Validate(map, out string error))
            {
                LastError = error;
                return null;
            }

            return map;
        }

        static bool TryAimLightning(GridMap map, System.Random rng, Vector2Int origin, out LightningDef def)
        {
            var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            Shuffle(dirs, rng);
            for (int i = 0; i < 4; i++)
            {
                if (RayLength(map, origin, dirs[i]) >= 2)
                {
                    def = new LightningDef { Origin = origin, Direction = dirs[i] };
                    return true;
                }
            }

            def = default;
            return false;
        }

        static int RayLength(GridMap map, Vector2Int origin, Vector2Int dir)
        {
            int length = 0;
            var cursor = origin + dir;
            while (map.InBounds(cursor) && !IsPerimeter(map, cursor))
            {
                length++;
                cursor += dir;
            }

            return length;
        }

        static void CarveRay(GridMap map, Vector2Int origin, Vector2Int dir)
        {
            int length = RayLength(map, origin, dir);
            for (int i = 1; i <= length; i++)
            {
                var cell = origin + dir * i;
                if (!IsPerimeter(map, cell))
                    map.Cells[cell.x, cell.y] = CellKind.Floor;
            }
        }

        static bool Validate(GridMap map, out string error)
        {
            error = "";
            if (!BorderIsClosed(map, out error))
                return false;
            return map.Level == LevelId.BoulderTemple ? ValidateLevel1(map, out error) : ValidateLevel2(map, out error);
        }

        static bool ValidateLevel1(GridMap map, out string error)
        {
            error = "";
            if (map.Collectibles.Count != 5 || map.Boulders.Count != 5 || !map.Boulders[4].Special)
            {
                error = "Нужно 5 идолов и 5 валунов.";
                return false;
            }

            var nooks = new HashSet<Vector2Int>();
            for (int i = 0; i < map.Boulders.Count; i++)
            {
                var boulder = map.Boulders[i];
                nooks.Add(boulder.Idle);
                int open = 0;
                for (int d = 0; d < 4; d++)
                {
                    var neighbor = boulder.Idle + Dirs[d];
                    if (map.InBounds(neighbor) && map.Get(neighbor) == CellKind.Floor)
                        open++;
                }

                if (open != 1 || Manhattan(boulder.Idle, boulder.Entry) != 1 || map.Get(boulder.Entry) != CellKind.Floor)
                {
                    error = "Ниша валуна " + i + " не тупик.";
                    return false;
                }
            }

            var reach = Flood(map, map.PlayerStart, nooks);
            for (int i = 0; i < map.Collectibles.Count; i++)
            {
                if (!reach.Contains(map.Collectibles[i]) || nooks.Contains(map.Collectibles[i]))
                {
                    error = "Идол " + i + " недоступен.";
                    return false;
                }
            }

            for (int i = 0; i < map.Hearts.Count; i++)
            {
                if (!reach.Contains(map.Hearts[i]))
                {
                    error = "Сердце недоступно.";
                    return false;
                }
            }

            for (int i = 0; i < 4; i++)
            {
                var boulder = map.Boulders[i];
                if (!StraightClear(map, boulder.PatrolA, boulder.PatrolB, false) || !OnSegment(boulder.Entry, boulder.PatrolA, boulder.PatrolB))
                {
                    error = "Коридор валуна " + i + " перекрыт.";
                    return false;
                }
            }

            var special = map.Boulders[4];
            if (!StraightClear(map, special.Entry, special.ChargeTo, true) || !StraightClear(map, special.ChargeTo, special.DashTo, true))
            {
                error = "Трасса финального валуна перекрыта.";
                return false;
            }

            if (!reach.Contains(special.DashTo))
            {
                error = "Нет пути к выходу.";
                return false;
            }

            return true;
        }

        static bool OnSegment(Vector2Int point, Vector2Int a, Vector2Int b)
        {
            if (a.x == b.x)
                return point.x == a.x && point.y >= Mathf.Min(a.y, b.y) && point.y <= Mathf.Max(a.y, b.y);
            return point.y == a.y && point.x >= Mathf.Min(a.x, b.x) && point.x <= Mathf.Max(a.x, b.x);
        }

        static bool ValidateLevel2(GridMap map, out string error)
        {
            error = "";
            if (map.Collectibles.Count != 8 || map.Lightnings.Count != 8)
            {
                error = "Нужно 8 частей молота.";
                return false;
            }

            var reach = Flood(map, map.PlayerStart, null);
            for (int i = 0; i < 8; i++)
            {
                if (!reach.Contains(map.Collectibles[i]))
                {
                    error = "Часть молота " + i + " недостижима.";
                    return false;
                }

                if (RayLength(map, map.Lightnings[i].Origin, map.Lightnings[i].Direction) < 2)
                {
                    error = "Молния " + i + " не доходит до границы.";
                    return false;
                }

                var end = map.Lightnings[i].Origin;
                int guard = 0;
                while (guard++ < 80)
                {
                    var next = end + map.Lightnings[i].Direction;
                    if (!map.InBounds(next) || IsPerimeter(map, next))
                        break;
                    if (map.Get(next) != CellKind.Floor)
                    {
                        error = "Коридор молнии " + i + " не прокопан.";
                        return false;
                    }

                    end = next;
                }
            }

            for (int i = 0; i < map.Hearts.Count; i++)
            {
                if (!reach.Contains(map.Hearts[i]))
                {
                    error = "Сердце недоступно.";
                    return false;
                }
            }

            if (!StraightClear(map, map.HammerFrom, map.HammerTo, true) || !reach.Contains(map.HammerFrom))
            {
                error = "Полёт молота невозможен.";
                return false;
            }

            return true;
        }

        static bool BorderIsClosed(GridMap map, out string error)
        {
            error = "";
            for (int x = 0; x < map.Width; x++)
            {
                if (!BorderCellOk(map, new Vector2Int(x, 0), out error))
                    return false;
                if (!BorderCellOk(map, new Vector2Int(x, map.Height - 1), out error))
                    return false;
            }

            for (int y = 1; y < map.Height - 1; y++)
            {
                if (!BorderCellOk(map, new Vector2Int(0, y), out error))
                    return false;
                if (!BorderCellOk(map, new Vector2Int(map.Width - 1, y), out error))
                    return false;
            }

            return true;
        }

        static bool BorderCellOk(GridMap map, Vector2Int cell, out string error)
        {
            error = "";
            if (cell == map.ExitCell)
            {
                if (map.Get(cell) == CellKind.Breakable)
                    return true;
                error = "Выход не является разрушаемой стеной.";
                return false;
            }

            if (map.Get(cell) == CellKind.Wall)
                return true;
            error = "Дыра в периметре " + cell;
            return false;
        }

        static bool StraightClear(GridMap map, Vector2Int from, Vector2Int to, bool allowBreakableEnds)
        {
            var step = StepToward(from, to);
            if (step == Vector2Int.zero)
                return false;
            var point = from;
            int guard = 0;
            while (guard++ < 128)
            {
                if (!map.InBounds(point))
                    return false;
                var kind = map.Get(point);
                bool terminal = point == from || point == to;
                bool ok = kind == CellKind.Floor || (allowBreakableEnds && terminal && kind == CellKind.Breakable);
                if (!ok)
                    return false;
                if (point == to)
                    return true;
                point += step;
            }

            return false;
        }

        static Vector2Int StepToward(Vector2Int from, Vector2Int to)
        {
            if (from.x != to.x && from.y != to.y)
                return Vector2Int.zero;
            if (to.x > from.x)
                return Vector2Int.right;
            if (to.x < from.x)
                return Vector2Int.left;
            if (to.y > from.y)
                return Vector2Int.up;
            if (to.y < from.y)
                return Vector2Int.down;
            return Vector2Int.zero;
        }

        static HashSet<Vector2Int> Flood(GridMap map, Vector2Int start, HashSet<Vector2Int> blocked)
        {
            var result = new HashSet<Vector2Int>();
            if (!Walkable(map, start, blocked))
                return result;
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            result.Add(start);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    var next = cell + Dirs[i];
                    if (result.Contains(next) || !Walkable(map, next, blocked))
                        continue;
                    result.Add(next);
                    queue.Enqueue(next);
                }
            }

            return result;
        }

        static bool Walkable(GridMap map, Vector2Int cell, HashSet<Vector2Int> blocked)
        {
            if (!map.InBounds(cell) || (blocked != null && blocked.Contains(cell)))
                return false;
            return map.Get(cell) == CellKind.Floor;
        }

        static void CarveMaze(GridMap map, System.Random rng, bool[,] visited, int nx, int nz, int i, int j)
        {
            visited[i, j] = true;
            var cell = new Vector2Int(1 + i * 2, 1 + j * 2);
            map.Cells[cell.x, cell.y] = CellKind.Floor;
            var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            Shuffle(dirs, rng);
            for (int d = 0; d < 4; d++)
            {
                int ni = i + dirs[d].x;
                int nj = j + dirs[d].y;
                if (ni < 0 || nj < 0 || ni >= nx || nj >= nz || visited[ni, nj])
                    continue;
                var next = new Vector2Int(1 + ni * 2, 1 + nj * 2);
                var mid = new Vector2Int((cell.x + next.x) / 2, (cell.y + next.y) / 2);
                map.Cells[mid.x, mid.y] = CellKind.Floor;
                CarveMaze(map, rng, visited, nx, nz, ni, nj);
            }
        }

        static bool TryPickHammerRun(GridMap map, out Vector2Int from, out Vector2Int exit)
        {
            from = new Vector2Int(-1, -1);
            exit = from;
            int best = 1;
            var dirs = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
            for (int y = 1; y < map.Height - 1; y++)
            {
                for (int x = 1; x < map.Width - 1; x++)
                {
                    var start = new Vector2Int(x, y);
                    if (map.Get(start) != CellKind.Floor)
                        continue;
                    for (int d = 0; d < 4; d++)
                    {
                        int length = 0;
                        var point = start;
                        while (true)
                        {
                            var next = point + dirs[d];
                            if (!map.InBounds(next))
                                break;
                            if (map.Get(next) != CellKind.Floor)
                            {
                                if (IsPerimeter(map, next) && length > best)
                                {
                                    best = length;
                                    from = start;
                                    exit = next;
                                }

                                break;
                            }

                            length++;
                            point = next;
                        }
                    }
                }
            }

            return from.x >= 0;
        }

        static Vector2Int FarthestFloor(GridMap map, Vector2Int goal, HashSet<Vector2Int> avoid)
        {
            Vector2Int best = new Vector2Int(-1, -1);
            int score = -1;
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (map.Get(cell) != CellKind.Floor || avoid.Contains(cell))
                        continue;
                    int distance = Manhattan(cell, goal);
                    if (distance > score)
                    {
                        score = distance;
                        best = cell;
                    }
                }
            }

            return best;
        }

        static bool IsPerimeter(GridMap map, Vector2Int cell)
        {
            return cell.x == 0 || cell.y == 0 || cell.x == map.Width - 1 || cell.y == map.Height - 1;
        }

        static Vector2Int OnLane(Lane lane, int along)
        {
            return lane.Horizontal ? new Vector2Int(along, lane.Fixed) : new Vector2Int(lane.Fixed, along);
        }

        static int Manhattan(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        static int EdgeDist(GridMap map, Vector2Int cell)
        {
            return Mathf.Min(Mathf.Min(cell.x, map.Width - 1 - cell.x), Mathf.Min(cell.y, map.Height - 1 - cell.y));
        }

        static GridMap NewMap(int width, int height, LevelId level)
        {
            return new GridMap
            {
                Width = width,
                Height = height,
                Level = level,
                Cells = new CellKind[width, height]
            };
        }

        static void Shuffle<T>(IList<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        static GridMap MirrorX(GridMap src)
        {
            var dst = CloneShell(src, src.Width, src.Height);
            for (int y = 0; y < src.Height; y++)
            {
                for (int x = 0; x < src.Width; x++)
                    dst.Cells[src.Width - 1 - x, y] = src.Cells[x, y];
            }

            CopyPoints(src, dst, p => new Vector2Int(src.Width - 1 - p.x, p.y), d => new Vector2Int(-d.x, d.y));
            return dst;
        }

        static GridMap Rotate90(GridMap src)
        {
            var dst = CloneShell(src, src.Height, src.Width);
            for (int y = 0; y < src.Height; y++)
            {
                for (int x = 0; x < src.Width; x++)
                {
                    var rotated = new Vector2Int(src.Height - 1 - y, x);
                    dst.Cells[rotated.x, rotated.y] = src.Cells[x, y];
                }
            }

            CopyPoints(src, dst, p => new Vector2Int(src.Height - 1 - p.y, p.x), d => new Vector2Int(-d.y, d.x));
            return dst;
        }

        static void CopyPoints(GridMap src, GridMap dst, Func<Vector2Int, Vector2Int> point, Func<Vector2Int, Vector2Int> direction)
        {
            dst.PlayerStart = point(src.PlayerStart);
            dst.ExitCell = point(src.ExitCell);
            dst.HammerFrom = point(src.HammerFrom);
            dst.HammerTo = point(src.HammerTo);
            for (int i = 0; i < src.Collectibles.Count; i++)
                dst.Collectibles.Add(point(src.Collectibles[i]));
            for (int i = 0; i < src.Hearts.Count; i++)
                dst.Hearts.Add(point(src.Hearts[i]));
            for (int i = 0; i < src.Boulders.Count; i++)
            {
                var boulder = src.Boulders[i];
                boulder.Idle = point(boulder.Idle);
                boulder.Entry = point(boulder.Entry);
                boulder.PatrolA = point(boulder.PatrolA);
                boulder.PatrolB = point(boulder.PatrolB);
                boulder.ChargeTo = point(boulder.ChargeTo);
                boulder.DashTo = point(boulder.DashTo);
                dst.Boulders.Add(boulder);
            }

            for (int i = 0; i < src.Lightnings.Count; i++)
            {
                var lightning = src.Lightnings[i];
                lightning.Origin = point(lightning.Origin);
                lightning.Direction = direction(lightning.Direction);
                dst.Lightnings.Add(lightning);
            }
        }

        static GridMap CloneShell(GridMap src, int width, int height)
        {
            return new GridMap
            {
                Width = width,
                Height = height,
                Level = src.Level,
                Seed = src.Seed,
                Cells = new CellKind[width, height]
            };
        }
    }
}
