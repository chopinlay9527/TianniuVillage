namespace TianniuVillage.Core;

public static class PathFinder
{
    private const int NodeBudget = 20000;

    private static readonly (int dx, int dy)[] Dirs =
    [
        (1, 0), (-1, 0), (0, 1), (0, -1),
        (1, 1), (1, -1), (-1, 1), (-1, -1)
    ];

    public static Queue<(int x, int y)>? Find(TileMap map, int sx, int sy, int tx, int ty)
    {
        if (!map.InBounds(sx, sy) || !map.InBounds(tx, ty)) return null;
        if (!map.Walkable(tx, ty)) return null;
        if (sx == tx && sy == ty) return new Queue<(int, int)>();

        var open = new PriorityQueue<int, float>();
        var cameFrom = new Dictionary<int, int>();
        var costSoFar = new Dictionary<int, float>();
        var closed = new HashSet<int>();
        int start = sy * map.W + sx, goal = ty * map.W + tx;
        open.Enqueue(start, Heur(map, sx, sy, tx, ty));
        costSoFar[start] = 0;
        int expanded = 0;

        while (open.Count > 0)
        {
            int cur = open.Dequeue();
            if (closed.Contains(cur)) continue;
            closed.Add(cur);

            if (cur == goal) break;
            if (expanded++ > NodeBudget) return Fallback(map, sx, sy, tx, ty);

            int cx = cur % map.W, cy = cur / map.W;
            float curCost = costSoFar[cur];

            foreach (var (dx, dy) in Dirs)
            {
                int nx = cx + dx, ny = cy + dy;
                if (!map.Walkable(nx, ny)) continue;
                int nIdx = ny * map.W + nx;
                if (closed.Contains(nIdx)) continue;
                if (dx != 0 && dy != 0 && (!map.Walkable(cx + dx, cy) || !map.Walkable(cx, cy + dy))) continue;
                float step = map.MoveCost(nx, ny) * (dx != 0 && dy != 0 ? 1.414f : 1f);
                float newCost = curCost + step;
                if (!costSoFar.TryGetValue(nIdx, out var old) || newCost < old)
                {
                    costSoFar[nIdx] = newCost;
                    cameFrom[nIdx] = cur;
                    open.Enqueue(nIdx, newCost + Heur(map, nx, ny, tx, ty));
                }
            }
        }

        if (!cameFrom.ContainsKey(goal) && start != goal) return Fallback(map, sx, sy, tx, ty);
        var path = new Stack<(int, int)>();
        int node = goal;
        while (node != start)
        {
            path.Push((node % map.W, node / map.W));
            if (!cameFrom.TryGetValue(node, out node)) return Fallback(map, sx, sy, tx, ty);
        }
        var result = new Queue<(int, int)>();
        while (path.Count > 0) result.Enqueue(path.Pop());
        return result;
    }

    private static float Heur(TileMap map, int x, int y, int tx, int ty)
    {
        float dx = MathF.Abs(tx - x), dy = MathF.Abs(ty - y);
        float octile = MathF.Max(dx, dy) + 0.414f * MathF.Min(dx, dy);
        return octile * 0.55f;
    }

    private static Queue<(int x, int y)>? Fallback(TileMap map, int sx, int sy, int tx, int ty)
    {
        var q = new Queue<(int, int)>();
        int x = sx, y = sy;
        int attempts = 0;
        while ((x != tx || y != ty) && attempts < 600)
        {
            attempts++;
            int dx = Math.Sign(tx - x), dy = Math.Sign(ty - y);
            int nx = x, ny = y;

            if (dx != 0 && dy != 0)
            {
                bool diagOk = map.Walkable(x + dx, y + dy) &&
                              map.Walkable(x + dx, y) && map.Walkable(x, y + dy) &&
                              map.IsLand(x + dx, y + dy);
                if (diagOk) { nx = x + dx; ny = y + dy; }
                else
                {
                    bool hOk = map.Walkable(x + dx, y) && map.IsLand(x + dx, y);
                    bool vOk = map.Walkable(x, y + dy) && map.IsLand(x, y + dy);
                    if (hOk && (MathF.Abs(tx - x) >= MathF.Abs(ty - y) || !vOk)) nx = x + dx;
                    else if (vOk) ny = y + dy;
                    else if (map.Walkable(x + dx, y)) nx = x + dx;
                    else if (map.Walkable(x, y + dy)) ny = y + dy;
                }
            }
            else if (dx != 0)
            {
                if (map.IsLand(x + dx, y) && map.Walkable(x + dx, y)) nx = x + dx;
                else
                {
                    bool upOk = y > 0 && map.IsLand(x, y - 1) && map.Walkable(x, y - 1);
                    bool dnOk = y < map.H - 1 && map.IsLand(x, y + 1) && map.Walkable(x, y + 1);
                    if (upOk) ny = y - 1;
                    else if (dnOk) ny = y + 1;
                    else if (map.Walkable(x + dx, y)) nx = x + dx;
                }
            }
            else if (dy != 0)
            {
                if (map.IsLand(x, y + dy) && map.Walkable(x, y + dy)) ny = y + dy;
                else
                {
                    bool lfOk = x > 0 && map.IsLand(x - 1, y) && map.Walkable(x - 1, y);
                    bool rtOk = x < map.W - 1 && map.IsLand(x + 1, y) && map.Walkable(x + 1, y);
                    if (lfOk) nx = x - 1;
                    else if (rtOk) nx = x + 1;
                    else if (map.Walkable(x, y + dy)) ny = y + dy;
                }
            }

            if (nx == x && ny == y) break;
            x = nx; y = ny;
            q.Enqueue((x, y));
        }
        return q.Count > 0 && x == tx && y == ty ? q : null;
    }
}
