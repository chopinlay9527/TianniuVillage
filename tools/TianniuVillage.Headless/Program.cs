using System.Diagnostics;
using TianniuVillage.Core;

if (args.Length > 0 && args[0] == "techdiag")
{
    var gt = Game.NewGame(2024);
    foreach (var o in gt.Villagers) o.DecisionCooldown = 0;
    for (int i = 0; i < 4 * Balance.DaysPerSeason * Balance.MinutesPerDay; i++)
    {
        gt.Step();
        if (i % (60 * Balance.MinutesPerDay) == 0)
        {
            var planned = gt.World.Buildings.Where(b => b.State == BuildingState.Planned).ToList();
            var hauls = gt.Jobs.All.Count(j => j.Kind == JobKind.HaulStone);
            var builds = gt.Jobs.All.Count(j => j.Kind == JobKind.Build);
            Console.WriteLine($"day={gt.Day} blds={gt.World.Buildings.Count} planned={planned.Count} hauls={hauls} builds={builds} fail={gt.LastBuildFail} researching={gt.Villagers.Count(v => v.Activity == VillagerActivity.Researching)} prog={gt.World.ResearchProgress:F0}");
            foreach (var b in planned)
                Console.WriteLine($"   planned {b.Key} del=[{string.Join(",", b.Delivered.Select(d => d.Key + ":" + d.Value))}] cost=[{string.Join(",", b.Def.Cost.Select(cc => cc.item + ":" + cc.count))}]");
        }
    }
    Console.WriteLine($"researched=[{string.Join(",", gt.World.Researched)}]");
    return;
}

if (args.Length > 0 && args[0] == "diag")
{
    RunDiag();
    return;
}

if (args.Length > 0 && args[0] == "events")
{
    RunEvents();
    return;
}

if (args.Length > 0 && args[0] == "window")
{
    int fromDay = int.Parse(args[1]);
    int toDay = int.Parse(args[2]);
    var wg = Game.NewGame(20260908);
    int wTotal = toDay * Balance.MinutesPerDay;
    int wSeq = 0;
    for (int i = 0; i < wTotal; i++)
    {
        wg.Step();
        if (wg.Day >= fromDay)
            foreach (var l in wg.Logs.Where(l => l.Seq > wSeq))
            {
                wSeq = Math.Max(wSeq, l.Seq);
                Console.WriteLine($"d{wg.Day} [{l.TimeZh}] {l.Text}");
            }
        if (wSeq == 0) foreach (var l in wg.Logs) wSeq = Math.Max(wSeq, l.Seq);
    }
    return;
}

if (args.Length > 0 && args[0] == "terrain")
{
    int tSeed = args.Length > 1 && int.TryParse(args[1], out var ts) ? ts : 20260908;
    var w = WorldGenerator.Generate(tSeed);
    var counts = new Dictionary<Terrain, int>();
    foreach (var t in w.Map.Tiles) counts[t] = counts.GetValueOrDefault(t) + 1;
    Console.WriteLine($"seed={tSeed} settle=({w.SettleCenter.x},{w.SettleCenter.y})");
    foreach (var (t, n) in counts.OrderByDescending(k => k.Value))
        Console.WriteLine($"  {t,-10} {n,6} ({n * 100.0 / w.Map.Tiles.Length:F1}%)");
    // ASCII 缩略图 (每8x8取中心) 验证山脉/河流形态
    char[] glyphs = { '~', '=', '.', '"', ',', 'T', '^', 'M' }; // 深水浅水沙草林高地山
    for (int my = 4; my < w.Map.H; my += 8)
    {
        var line = new System.Text.StringBuilder();
        for (int mx = 4; mx < w.Map.W; mx += 8)
            line.Append(glyphs[(int)w.Map.Get(mx, my)]);
        Console.WriteLine(line);
    }
    return;
}

if (args.Length > 0 && args[0] == "deaths")
{
    int dSeed = args.Length > 1 && int.TryParse(args[1], out var ds) ? ds : 20260908;
    int dYears = args.Length > 2 && int.TryParse(args[2], out var dy) ? dy : 5;
    var dg = Game.NewGame(dSeed);
    int dTotal = dYears * 4 * Balance.DaysPerSeason * Balance.MinutesPerDay;
    int dSeq = 0;
    for (int i = 0; i < dTotal; i++)
    {
        dg.Step();
        foreach (var l in dg.Logs.Where(l => l.Seq > dSeq))
        {
            dSeq = Math.Max(dSeq, l.Seq);
            if (l.Text.Contains("倒下") || l.Text.Contains("冻死") || l.Text.Contains("殉职") ||
                l.Text.Contains("不治") || l.Text.Contains("狼") || l.Text.Contains("骨折") ||
                l.Text.Contains("划伤") || l.Text.Contains("精神崩溃") || l.Text.Contains("病倒") ||
                l.Text.Contains("传染") || l.Text.Contains("享年"))
                Console.WriteLine($"d{dg.Day} [{l.TimeZh}] {l.Text}");
        }
    }
    Console.WriteLine($"--- pop={dg.Villagers.Count(v => v.Alive)} births={dg.TotalBirths} deaths={dg.TotalDeaths}");
    return;
}

if (args.Length > 0 && args[0] == "audit")
{
    RunAudit();
    return;
}

if (args.Length > 0 && args[0] == "marriage")
{
    var gm = Game.NewGame(20260908);
    foreach (var o in gm.Villagers) o.DecisionCooldown = 0;
    int gmTicks = 4 * Balance.DaysPerSeason * Balance.MinutesPerDay;
    int lastSeq = 0;
    for (int i = 0; i < gmTicks; i++)
    {
        gm.Step();
        if (i % 5000 == 0)
        {
            var adults = gm.Villagers.Where(v => v.Alive && v.Stage == AgeStage.Adult).ToList();
            Console.WriteLine($"t={i} adults={adults.Count} married={adults.Count(v => v.SpouseId != 0)} " +
                $"homes={gm.World.Buildings.Count(b => b.Key is "hut" or "house" or "manor" && b.State == BuildingState.Complete)} " +
                $"pairs=[{string.Join(", ", adults.Take(4).Select(a => a.Name[0] + ":" + adults.Where(b => b.Id != a.Id).Max(b => a.Friendships.GetValueOrDefault(b.Id, 0)).ToString("F0")))}]");
        }
        foreach (var log in gm.Logs.Where(l => l.Seq > lastSeq))
        {
            lastSeq = Math.Max(lastSeq, log.Seq);
            if (log.Text.Contains("闲聊") || log.Text.Contains("结为") || log.Text.Contains("怀孕") || log.Text.Contains("住")) 
                Console.WriteLine($"[{log.TimeZh}] {log.Text}");
        }
    }
    Console.WriteLine("=== 最终状态 ===");
    foreach (var b in gm.World.Buildings.Where(b => b.Key is "hut" or "house" or "manor"))
        Console.WriteLine($"房{b.Id} {b.Key} 床位={b.Beds} 占用={b.Occupants} 住户=[{string.Join(",", gm.Villagers.Where(v => v.HomeId == b.Id).Select(v => v.Name))}]");
    foreach (var v in gm.Villagers.Where(v => v.Alive))
        Console.WriteLine($"{v.Name} 性别={(v.Sex == Sex.Male ? "男" : "女")} 偶={v.SpouseId} 家={v.HomeId} 友谊=[{string.Join(",", v.Friendships.Take(4).Select(f => f.Key + ":" + f.Value.ToString("F0")))}]");
    Console.WriteLine($"房子={gm.World.Buildings.Count(b => b.Key is "hut" or "house" or "manor")} 占用={gm.World.Buildings.Where(b => b.Key is "hut" or "house" or "manor").Sum(b => b.Occupants)}");
    return;
}

int seed = args.Length > 0 && int.TryParse(args[0], out var s) ? s : 20260908;
int years = args.Length > 1 && int.TryParse(args[1], out var y) ? y : 3;

var game = Game.NewGame(seed);
int totalTicks = years * 4 * Balance.DaysPerSeason * Balance.MinutesPerDay;

Console.WriteLine($"seed={seed} years={years} settle=({game.World.SettleCenter.x},{game.World.SettleCenter.y})");
Console.WriteLine("day,pop,food,meal,grain,log,plank,stone,herb,happiness,health,births,deaths,buildings,road1,road2,road3");

var sw = Stopwatch.StartNew();
int lastDay = -1;
while (game.Tick < totalTicks)
{
    game.Step();
    if (game.Day != lastDay && game.Day % 30 == 0)
    {
        lastDay = game.Day;
        Report(game);
    }
}
sw.Stop();

var final = game.Villagers.Where(v => v.Alive).ToList();
Console.WriteLine($"--- {years}年模拟完成，用时 {sw.ElapsedMilliseconds}ms ---");
Console.WriteLine($"存活人口: {final.Count}  出生: {game.TotalBirths}  死亡: {game.TotalDeaths}");
Console.WriteLine($"建筑: {string.Join("、", game.World.Buildings.Where(b => b.State == BuildingState.Complete).GroupBy(b => BuildingDefs.All[b.Key].NameZh).Select(g => $"{g.Key}×{g.Count()}"))}");
foreach (var log in game.Logs.Where(l => l.Sev == LogSeverity.Important).TakeLast(12))
    Console.WriteLine($"[{log.TimeZh}] {log.Text}");

static void Report(Game game)
{
    var alive = game.Villagers.Where(v => v.Alive).ToList();
    int r1 = 0, r2 = 0, r3 = 0;
    foreach (var r in game.World.Map.Roads)
    {
        if (r == 1) r1++;
        else if (r == 2) r2++;
        else if (r == 3) r3++;
    }
    Console.WriteLine(
        $"{game.Day},{alive.Count},{game.CountFood()},{game.World.CountItem("meal")},{game.World.CountItem("grain")}," +
        $"{game.World.CountItem("log")},{game.World.CountItem("plank")},{game.World.CountItem("stone")},{game.World.CountItem("herb")}," +
        $"{(alive.Count > 0 ? alive.Average(v => v.Happiness) : 0):F1},{(alive.Count > 0 ? alive.Average(v => v.Health) : 0):F1}," +
        $"{game.TotalBirths},{game.TotalDeaths},{game.World.Buildings.Count(b => b.State == BuildingState.Complete)},{r1},{r2},{r3}");
}

static void RunAudit()
{
    var game = Game.NewGame(20260908);
    int total = 2 * 4 * Balance.DaysPerSeason * Balance.MinutesPerDay;
    int lastSampleHour = -1;
    int nightSamples = 0, nightAwake = 0, workSamples = 0, workWorking = 0;
    var activityOfDay = new Dictionary<int, int>();
    var lastSeq = 0;
    var logTags = new Dictionary<string, int>();

    while (game.Tick < total)
    {
        game.Step();

        int hour = game.MinuteOfDay / 60;
        if (hour != lastSampleHour)
        {
            lastSampleHour = hour;
            var awake = game.Villagers.Where(v => v.Alive && v.Stage != AgeStage.Infant).ToList();
            bool night = game.MinuteOfDay >= Balance.SleepStartMinute || game.MinuteOfDay < Balance.WakeMinute;
            if (night)
            {
                nightSamples += awake.Count;
                nightAwake += awake.Count(v => v.Activity != VillagerActivity.Sleeping);
            }
            else
            {
                workSamples += awake.Count;
                workWorking += awake.Count(v => v.Activity == VillagerActivity.Working);
            }
            foreach (var v in awake)
            {
                int k = (int)v.Activity;
                activityOfDay[k] = activityOfDay.GetValueOrDefault(k) + 1;
            }
        }

        foreach (var log in game.Logs.Where(l => l.Seq > lastSeq))
        {
            lastSeq = Math.Max(lastSeq, log.Seq);
            string? tag = log.Text switch
            {
                var t when t.Contains("倒下") || t.Contains("冻死") || t.Contains("不治") || t.Contains("离开了") || t.Contains("葬") => "死亡",
                var t when t.Contains("结为夫妇") => "结婚",
                var t when t.Contains("怀孕") => "怀孕",
                var t when t.Contains("诞下") => "出生",
                var t when t.Contains("病倒") => "生病",
                var t when t.Contains("落成") => "建筑竣工",
                var t when t.Contains("踩踏") && t.Contains("土路") => "土路成型",
                var t when t.Contains("铺成") => "道路立项",
                var t when t.Contains("竣工") => "道路竣工",
                var t when t.Contains("损坏") || t.Contains("踩烂") || t.Contains("碎裂") => "道路损坏",
                var t when t.Contains("旅人") || t.Contains("定居") => "外来者",
                var t when t.Contains("掀翻") => "风暴毁物",
                var t when t.Contains("领悟") => "科技突破",
                _ => null
            };
            if (tag != null) logTags[tag] = logTags.GetValueOrDefault(tag) + 1;
        }
    }

    Console.WriteLine("=== 2 年运行体检 ===");
    Console.WriteLine($"最终人口: {game.Villagers.Count(v => v.Alive)} (出生 {game.TotalBirths} / 死亡 {game.TotalDeaths})");
    Console.WriteLine($"食物={game.CountFood()} 石料={game.World.CountItem("stone")} 木板={game.World.CountItem("plank")}");
    int r1 = 0, r2 = 0, r3 = 0;
    foreach (var r in game.World.Map.Roads) { if (r == 1) r1++; else if (r == 2) r2++; else if (r == 3) r3++; }
    Console.WriteLine($"道路: 土路{r1} 碎石{r2} 石板{r3}  交通条目={game.World.Traffic.Count}");

    // 道路连续性：孤立格（8邻域无路）与单格缺口（两侧是路本格无路）
    int totalRoad = r1 + r2 + r3, isolated = 0, nearMiss = 0;
    var map = game.World.Map;
    for (int y = 1; y < map.H - 1; y++)
        for (int x = 1; x < map.W - 1; x++)
        {
            bool roadHere = map.Roads[map.Index(x, y)] > 0;
            int roadNbrs = 0;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (map.Roads[map.Index(x + dx, y + dy)] > 0) roadNbrs++;
                }
            if (roadHere && roadNbrs == 0) isolated++;
            if (!roadHere && roadNbrs >= 2
                && ((map.Roads[map.Index(x - 1, y)] > 0 && map.Roads[map.Index(x + 1, y)] > 0)
                 || (map.Roads[map.Index(x, y - 1)] > 0 && map.Roads[map.Index(x, y + 1)] > 0)))
            {
                nearMiss++;
                string cause = game.World.BuildingAt(x, y) != null ? "建筑"
                    : !map.IsLand(x, y) ? "水" : !map.Walkable(x, y) ? "不可走"
                    : $"低流量{game.World.Traffic.GetValueOrDefault(map.Index(x, y))}";
                Console.WriteLine($"   缺口({x},{y}) {cause} 地形={map.Get(x, y)}");
            }
        }
    Console.WriteLine($"道路连续性: 总格数={totalRoad} 孤立格={isolated} 单格缺口={nearMiss}");
    Console.WriteLine($"科技: 已悟{game.World.Researched.Count} 当前={game.World.CurrentTech ?? "—"} 进度={game.World.ResearchProgress:F0}");
    Console.WriteLine();
    Console.WriteLine("--- 日志事件统计 ---");
    foreach (var (tag, n) in logTags.OrderByDescending(k => k.Value))
        Console.WriteLine($"{tag}: {n}");
    Console.WriteLine();
    Console.WriteLine("--- 作息健康度 ---");
    Console.WriteLine($"夜间采样 {nightSamples} 人次，未眠 {nightAwake} ({(float)nightAwake / Math.Max(1, nightSamples) * 100:F1}%)");
    Console.WriteLine($"日间采样 {workSamples} 人次，工作中 {workWorking} ({(float)workWorking / Math.Max(1, workSamples) * 100:F1}%)");
    string[] actZh = ["发呆", "散步", "赶路", "干活", "吃饭", "睡觉", "休息", "聊天", "上学", "玩耍", "养病", "切磋", "喝水", "钻研"];
    Console.WriteLine("--- 全时段活动分布 ---");
    foreach (var (k, n) in activityOfDay.OrderByDescending(k => k.Value))
        Console.WriteLine($"{actZh[k]}: {n}");
}

static void RunDiag()
{
    var game = Game.NewGame(99);
    for (int i = 0; i < 10 * Balance.MinutesPerDay; i++)
    {
        game.Step();
        if (i == 1440 || i == 9000)
        {
            var alive = game.Villagers.Where(v => v.Alive).ToList();
            var v0 = alive[0];
            Console.WriteLine($"t={i} food={game.CountFood()} v0: act={v0.Activity} pos=({v0.Pos.x},{v0.Pos.y})");
        }
    }
}

static void RunEvents()
{
    var game = Game.NewGame(20260908);
    int total = 3 * 4 * Balance.DaysPerSeason * Balance.MinutesPerDay;
    int lastSeq = 0;
    int lastDay = -1;
    while (game.Tick < total)
    {
        game.Step();
        if (game.Day % 30 == 0 && game.Day != lastDay && game.MinuteOfDay == 0)
        {
            lastDay = game.Day;
            var alive = game.Villagers.Where(v => v.Alive).ToList();
            Console.WriteLine($"### day={game.Day} pop={alive.Count} food={game.CountFood()} deaths={game.TotalDeaths} births={game.TotalBirths}");
        }
        foreach (var log in game.Logs.Where(l => l.Seq > lastSeq))
        {
            lastSeq = Math.Max(lastSeq, log.Seq);
            if (log.Text.Contains("死") || log.Text.Contains("夫妇") || log.Text.Contains("怀孕") ||
                log.Text.Contains("诞下") || log.Text.Contains("领悟") || log.Text.Contains("落成"))
                Console.WriteLine($"[{log.TimeZh}] {log.Text}");
        }
    }
}

