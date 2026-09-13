using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

// 官方 LPC 图集 (.tsx) → terrain_profile.json 导入工具
//
// 用法:
//   dotnet run --project tools/TianniuVillage.TileSetImport -- import <官方目录> <输出json>
//   dotnet run --project tools/TianniuVillage.TileSetImport -- summary <profile.json>
//
// 说明:
// - 权威语义: Tiled wangid 的 8 个数 = top,topright,right,bottomright,bottom,bottomleft,left,topleft;
//   角位为奇数下标 (1,3,5,7) = TR,BR,BL,TL; 取值是 1 基颜色 ID, 0 表示"无颜色"。
// - 四季: spring/summer/winter 的 wangtile 完全一致; autumn 为旧版, 单独存为覆盖表。

if (args.Length < 2)
{
    Console.Error.WriteLine("用法: import <官方目录> <输出json> | summary <profile.json>");
    return 1;
}

if (args[0] == "import" && args.Length >= 3) return Import(args[1], args[2], args.Length > 3 ? args[3] : null);
if (args[0] == "summary" && args.Length >= 2) return Summary(args[1]);
Console.Error.WriteLine("用法: import <官方目录> <输出json> [cs_out] | summary <profile.json>");
return 1;

static int Import(string officialDir, string outPath, string? csOut)
{
    var seasons = new (string Season, string File)[]
    {
        ("spring", "lpc-tileset-terrain-spring.tsx"),
        ("summer", "lpc-tileset-terrain-summer.tsx"),
        ("autumn", "lpc-tileset-terrain-autumn-old.tsx"),
        ("winter", "lpc-tileset-terrain-winter.tsx"),
    };

    XDocument? canonical = null;
    string canonicalSeason = "";
    var seasonalTables = new JsonObject();
    var seasonalCoverage = new JsonObject();

    foreach (var (season, file) in seasons)
    {
        var path = Path.Combine(officialDir, file);
        if (!File.Exists(path)) { Console.Error.WriteLine($"缺少文件: {path}"); return 1; }
        var doc = XDocument.Load(path);
        var ws = FindTerrainWangSet(doc)
                 ?? throw new InvalidOperationException($"{file} 中找不到 Terrain wangset");
        var (table, coverage) = ExtractCornerTable(ws);
        seasonalTables[season] = table;
        seasonalCoverage[season] = coverage;
        if (canonical is null) { canonical = doc; canonicalSeason = season; }
    }

    // 共享表: spring 为基准; 记录各季是否与基准一致
    var canonicalWs = FindTerrainWangSet(canonical!)!;
    var (baseTable, baseCoverage) = ExtractCornerTable(canonicalWs);
    var parity = new JsonObject();
    foreach (var season in seasons.Select(s => s.Season))
    {
        var t = seasonalTables[season]!.AsObject();
        parity[season] = t.Count == baseTable.Count &&
                         t.All(kv => baseTable.TryGetPropertyValue(kv.Key, out var v) &&
                                     v!.ToJsonString() == kv.Value!.ToJsonString());
    }

    // 颜色定义 (1 基 id)
    var colors = new JsonArray();
    int colorId = 0;
    foreach (var wc in canonicalWs.Elements("wangcolor"))
    {
        colorId++;
        colors.Add(new JsonObject
        {
            ["id"] = colorId,
            ["name"] = (string?)wc.Attribute("name") ?? $"color{colorId}",
            ["class"] = (string?)wc.Attribute("class") ?? "",
            ["sampleTile"] = (int?)wc.Attribute("tile") ?? -1,
            ["rgb"] = (string?)wc.Attribute("color") ?? "",
            ["probability"] = (double?)wc.Attribute("probability") ?? 1.0,
        });
    }

    // 动画帧 (水/冰)：tsx 中 <tile> 是 <tileset> 的直接子元素
    var animations = new JsonObject();
    foreach (var tile in canonical!.Root!.Elements("tile"))
    {
        var anim = tile.Element("animation");
        if (anim is null) continue;
        var frames = new JsonArray();
        foreach (var f in anim.Elements("frame"))
            frames.Add((int?)f.Attribute("tileid") ?? -1);
        animations[(string?)tile.Attribute("id") ?? "?"] = frames;
    }

    // 带碰撞/物体的 tile (崖壁等按构件使用)
    var objectTiles = new JsonArray();
    foreach (var tile in canonical.Root!.Elements("tile"))
    {
        var og = tile.Element("objectgroup");
        if (og is null) continue;
        objectTiles.Add(new JsonObject
        {
            ["tileId"] = (int?)tile.Attribute("id") ?? -1,
            ["objects"] = new JsonArray(og.Elements("object")
                .Select(o => (JsonNode)new JsonObject
                {
                    ["name"] = (string?)o.Attribute("name") ?? "",
                    ["x"] = (double?)o.Attribute("x") ?? 0,
                    ["y"] = (double?)o.Attribute("y") ?? 0,
                    ["width"] = (double?)o.Attribute("width") ?? 0,
                    ["height"] = (double?)o.Attribute("height") ?? 0,
                }).ToArray()),
        });
    }

    var image = canonical.Root!.Element("image");
    var profile = new JsonObject
    {
        ["$comment"] = "由 tools/TianniuVillage.TileSetImport 从官方 .tsx 生成，勿手改。角位键 = 1基颜色ID 的 TR,BR,BL,TL；0=无颜色。",
        ["source"] = new JsonObject
        {
            ["tileset"] = (string?)canonical.Root!.Attribute("name") ?? "",
            ["tiledVersion"] = (string?)canonical.Root!.Attribute("tiledversion") ?? "",
            ["credits"] = "assets/lpc/official/CREDITS.txt (OGA-BY 3.0 / CC-BY 3.0)",
        },
        ["sheet"] = new JsonObject
        {
            ["tileWidth"] = (int?)canonical.Root!.Attribute("tilewidth") ?? 32,
            ["tileHeight"] = (int?)canonical.Root!.Attribute("tileheight") ?? 32,
            ["columns"] = (int?)canonical.Root!.Attribute("columns") ?? 64,
            ["tileCount"] = (int?)canonical.Root!.Attribute("tilecount") ?? 4096,
            ["imageTemplate"] = (string?)image?.Attribute("source") ?? "lpc-terrain-{season}.png",
        },
        ["seasons"] = new JsonArray(seasons.Select(s => (JsonNode)s.Season).ToArray()),
        ["canonicalSeason"] = canonicalSeason,
        ["seasonParityWithCanonical"] = parity,
        ["colors"] = colors,
        ["cornerTable"] = baseTable,
        ["cornerTableBySeason"] = seasonalTables,
        ["coverage"] = baseCoverage,
        ["animations"] = animations,
        ["objectTiles"] = objectTiles,
    };

    var json = profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(outPath, json);
    // 同步产出 script 包装（WebView2 虚拟主机下 fetch 可能静默失败，沿用已验证的同步加载方案）
    var jsPath = Path.ChangeExtension(outPath, ".js");
    File.WriteAllText(jsPath, "window.TERRAIN_PROFILE = " + profile.ToJsonString() + ";\n");
    Console.WriteLine($"已写出 {outPath}");
    Console.WriteLine($"已写出 {jsPath}");
    Console.WriteLine($"  colors: {colors.Count}, cornerKeys: {baseTable.Count}, animations: {animations.Count}, objectTiles: {objectTiles.Count}");
    foreach (var season in seasons.Select(s => s.Season))
        Console.WriteLine($"  season {season}: 与基准一致 = {parity[season]}");

    // 生成 C# 覆盖表：让 Core 生成器的邻接约束与权威元数据同源
    if (csOut is not null)
    {
        var nameById = colors.ToDictionary(c => (int)c!["id"]!, c => (string)c!["name"]!);
        var pairs = baseCoverage.AsObject().Select(kv => kv.Key)
            .Select(k => k.Split('-').Select(int.Parse).ToArray())
            .Where(p => nameById.ContainsKey(p[0]) && nameById.ContainsKey(p[1]))
            .Select(p => (A: nameById[p[0]], B: nameById[p[1]]))
            .OrderBy(p => p.A, StringComparer.Ordinal).ThenBy(p => p.B, StringComparer.Ordinal)
            .ToArray();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// 由 tools/TianniuVillage.TileSetImport 从官方 LPC .tsx 生成（见 assets/lpc/official/CREDITS.txt）。勿手改。");
        sb.AppendLine("// 重新生成: dotnet run --project tools/TianniuVillage.TileSetImport -- import <官方目录> <profile.json> <TerrainCoverage.g.cs>");
        sb.AppendLine("namespace TianniuVillage.Core;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>官方 Wang Set 实际提供的相邻过渡（按官方色名）。</summary>");
        sb.AppendLine("public static class TerrainCoverage");
        sb.AppendLine("{");
        sb.AppendLine("    public static readonly string[] Colors =");
        sb.AppendLine("    {");
        foreach (var c in colors) sb.AppendLine($"        \"{c!["name"]}\",");
        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>无序色名对（\"A|B\"，字典序）—— 作者定义了过渡画的两色组合。</summary>");
        sb.AppendLine("    public static readonly string[] Pairs =");
        sb.AppendLine("    {");
        foreach (var p in pairs)
        {
            var (x, y) = string.CompareOrdinal(p.A, p.B) <= 0 ? (p.A, p.B) : (p.B, p.A);
            sb.AppendLine($"        \"{x}|{y}\",");
        }
        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine("    private static readonly System.Collections.Generic.HashSet<string> PairSet = new(Pairs);");
        sb.AppendLine();
        sb.AppendLine("    public static bool Supports(string a, string b) =>");
        sb.AppendLine("        a == b || PairSet.Contains(string.CompareOrdinal(a, b) <= 0 ? a + \"|\" + b : b + \"|\" + a);");
        sb.AppendLine("}");
        File.WriteAllText(csOut, sb.ToString());
        Console.WriteLine($"已写出 {csOut}（{pairs.Length} 对相邻过渡）");
    }
    return 0;
}

static XElement? FindTerrainWangSet(XDocument doc) =>
    doc.Root?.Element("wangsets")?.Elements("wangset")
        .FirstOrDefault(w => ((string?)w.Attribute("name") ?? "").Contains("Terrain"));

// 角位表: "TR,BR,BL,TL" (1基颜色ID, 0=无) -> 候选 tileId 列表
static (JsonObject Table, JsonObject Coverage) ExtractCornerTable(XElement wangSet)
{
    var table = new JsonObject();
    var pairKeys = new JsonObject();   // 非零角位颜色对 -> 键数
    foreach (var wt in wangSet.Elements("wangtile"))
    {
        var tileId = (int?)wt.Attribute("tileid") ?? -1;
        var ids = ((string?)wt.Attribute("wangid") ?? "").Split(',');
        if (ids.Length != 8) continue;
        int tr = int.Parse(ids[1]), br = int.Parse(ids[3]), bl = int.Parse(ids[5]), tl = int.Parse(ids[7]);
        var key = $"{tr},{br},{bl},{tl}";
        if (table[key] is not JsonArray arr) { arr = new JsonArray(); table[key] = arr; }
        arr.Add(tileId);

        var nz = new[] { tr, br, bl, tl }.Where(c => c > 0).Distinct().OrderBy(c => c).ToArray();
        for (int i = 0; i < nz.Length; i++)
            for (int j = i + 1; j < nz.Length; j++)
            {
                var pk = $"{nz[i]}-{nz[j]}";
                pairKeys[pk] = (pairKeys[pk]?.GetValue<int>() ?? 0) + 1;
            }
    }
    return (table, pairKeys);
}

static int Summary(string profilePath)
{
    var node = JsonNode.Parse(File.ReadAllText(profilePath))!.AsObject();
    var colors = node["colors"]!.AsArray();
    Console.WriteLine($"图集: {node["source"]!["tileset"]}  {node["sheet"]!["columns"]}列×{node["sheet"]!["tileWidth"]}px");
    Console.WriteLine("颜色 (1基 id / 名称 / 样例tile):");
    foreach (var c in colors)
        Console.WriteLine($"  {c!["id"],2}  {c["name"],-16} tile={c["sampleTile"]}");
    Console.WriteLine($"角位键: {node["cornerTable"]!.AsObject().Count}   动画: {node["animations"]!.AsObject().Count}   构件tile: {node["objectTiles"]!.AsArray().Count}");
    Console.WriteLine("过渡覆盖 (角位共现):");
    var names = colors.ToDictionary(c => (int)c!["id"]!, c => (string)c!["name"]!);
    foreach (var kv in node["coverage"]!.AsObject().OrderBy(k => k.Key))
    {
        var parts = kv.Key.Split('-').Select(x => int.Parse(x)).ToArray();
        Console.WriteLine($"  {names.GetValueOrDefault(parts[0], "?")} <-> {names.GetValueOrDefault(parts[1], "?")}: {kv.Value}");
    }
    foreach (var kv in node["seasonParityWithCanonical"]!.AsObject())
        Console.WriteLine($"  季 {kv.Key}: 与基准一致 = {kv.Value}");
    return 0;
}
