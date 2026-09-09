namespace TianniuVillage.Core;

public sealed record TechDef(string Id, string NameZh, string EffectZh, int Cost, int Tier, string? Prereq);

public static class TechDefs
{
    public static readonly TechDef[] All =
    [
        new("stoneaxe", "磨制石斧", "伐木效率提升25%", 800, 1, null),
        new("grassshoes", "编织草鞋", "村民移动速度提升15%", 800, 1, null),
        new("hotpot", "火塘煨汤", "熟食更加果腹", 800, 1, null),
        new("knotrecord", "结绳记事", "全体劳作效率提升5%", 900, 1, null),
        new("herbalism", "药理常识", "病人康复更快", 1600, 2, "knotrecord"),
        new("croprotation", "轮作休耕", "农田产量提升30%", 1600, 2, "stoneaxe"),
        new("boneneedle", "骨针缝衣", "冬衣制作省料一半", 1600, 2, "grassshoes"),
        new("smelting", "熔炼术", "可以在熔炉中冶炼金属", 2400, 2, "knotrecord"),
        new("crafting", "手工技艺", "可以建造手工坊制作器物", 2400, 2, "hotpot"),
        new("rites", "乡村礼制", "生育提升，吸引更多移民", 3000, 3, "herbalism"),
        new("stargazing", "望气观星", "风暴的破坏减半", 3000, 3, "croprotation"),
        new("prospecting", "探矿术", "可以发现并开采铁矿", 3000, 3, "smelting"),
        new("domestication", "驯化术", "可以建造畜栏与牧场", 3000, 3, "crafting")
    ];
}
