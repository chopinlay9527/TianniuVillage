namespace TianniuVillage.Core;

public static class NameGen
{
    private static readonly string[] Surnames =
        ["李", "王", "张", "刘", "陈", "杨", "赵", "黄", "周", "吴", "徐", "孙", "胡", "朱", "高", "林", "何", "郭", "马", "罗"];

    private static readonly string[] MaleGiven =
        ["大牛", "铁柱", "石头", "春生", "满仓", "来福", "二狗", "栓子", "大山", "长贵", "顺子", "旺财", "有田", "根生", "水生"];

    private static readonly string[] FemaleGiven =
        ["二妮", "杏花", "翠花", "春梅", "桂花", "秀兰", "小满", "巧云", "腊月", "燕子", "荷香", "彩霞", "云朵", "稻香", "月娥"];

    public static string Next(Rng rng, Sex sex)
    {
        var given = sex == Sex.Male ? MaleGiven : FemaleGiven;
        return rng.Pick(Surnames) + rng.Pick(given);
    }
}
