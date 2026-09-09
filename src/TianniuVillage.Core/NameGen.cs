namespace TianniuVillage.Core;

public static class NameGen
{
    private static readonly string[] Surnames =
    [
        "李", "王", "张", "刘", "陈", "杨", "赵", "黄", "周", "吴", "徐", "孙", "胡", "朱", "高", "林", "何", "郭", "马", "罗",
        "梁", "宋", "郑", "谢", "韩", "唐", "冯", "于", "董", "萧", "程", "曹", "袁", "邓", "许", "傅", "沈", "曾", "彭", "吕",
        "苏", "卢", "蒋", "蔡", "贾", "丁", "魏", "薛", "叶", "阎", "余", "潘", "杜", "戴", "夏", "钟", "汪", "田", "任", "姜",
        "范", "方", "石", "姚", "谭", "廖", "邹", "熊", "金", "陆", "郝", "孔", "白", "崔", "康", "毛", "邱", "秦", "江", "史",
        "顾", "侯", "邵", "孟", "龙", "万", "段", "雷", "钱", "汤", "尹", "黎", "易", "常", "武", "乔", "贺", "赖", "龚", "文"
    ];

    private static readonly string[] MaleGiven =
    [
        "大牛", "铁柱", "石头", "春生", "满仓", "来福", "二狗", "栓子", "大山", "长贵", "顺子", "旺财", "有田", "根生", "水生",
        "铁蛋", "石锁", "金宝", "银宝", "进宝", "招财", "福生", "禄生", "寿生", "喜子", "柱子", "河生", "海生", "木生", "财旺"
    ];

    private static readonly string[] FemaleGiven =
    [
        "二妮", "杏花", "翠花", "春梅", "桂花", "秀兰", "小满", "巧云", "腊月", "燕子", "荷香", "彩霞", "云朵", "稻香", "月娥",
        "冬梅", "秋菊", "夏荷", "春花", "春桃", "雪梅", "雪莲", "凤英", "桂英", "玉兰", "金兰", "银凤", "喜鹊", "小凤", "大丫"
    ];

    private static readonly string[] MaleChars =
    [
        "铁", "柱", "石", "山", "河", "海", "林", "松", "柏", "根", "福", "禄", "贵", "富", "财", "宝", "金", "旺", "兴", "隆",
        "强", "勇", "刚", "健", "彪", "虎", "龙", "鹏", "飞", "骏", "国", "家", "安", "泰", "平", "顺", "达", "成", "业", "功"
    ];

    private static readonly string[] FemaleChars =
    [
        "芳", "娟", "艳", "娥", "凤", "霞", "云", "月", "雪", "梅", "兰", "菊", "荷", "莲", "桃", "杏", "秀", "惠", "淑", "静",
        "花", "香", "英", "芝", "萍", "蓉", "蕊", "薇", "萱", "蔓", "妹", "妹儿", "妞", "娇", "婉", "娴", "莉", "蕾", "芬", "芸"
    ];

    private static readonly string[] AuspiciousA =
    [
        "春", "夏", "秋", "冬", "福", "禄", "贵", "吉", "祥", "安", "康", "天", "家", "国", "海", "山", "丰", "长", "永", "元"
    ];

    private static readonly string[] AuspiciousB =
    [
        "生", "来", "有", "满", "进", "成", "兴", "旺", "发", "达", "宝", "财", "喜", "乐", "泰", "和", "平", "顺", "盛", "隆"
    ];

    private static readonly string[] FlowerA =
    [
        "春", "冬", "秋", "夏", "雪", "玉", "金", "银", "红", "月", "云", "彩", "小", "大", "凤"
    ];

    private static readonly string[] FlowerB =
    [
        "梅", "兰", "菊", "竹", "荷", "莲", "桃", "杏", "梨", "桂", "花", "香", "英", "霞", "云"
    ];

    private static readonly string[] NumPrefix =
    [
        "大", "二", "三", "四", "五", "小", "老", "阿", "铁", "石"
    ];

    private static readonly string[] NounEnding =
    [
        "牛", "狗", "蛋", "娃", "头", "锁", "墩", "柱", "山", "水", "河", "江", "田", "仓", "囤"
    ];

    public static string Next(Rng rng, Sex sex)
    {
        string given = sex == Sex.Male ? RollMale(rng) : RollFemale(rng);
        return rng.Pick(Surnames) + given;
    }

    public static string Next(Rng rng, Sex sex, string? surname, ICollection<string> used)
    {
        for (int attempt = 0; attempt < 300; attempt++)
        {
            string? given = sex == Sex.Male ? RollMale(rng) : RollFemale(rng);
            string family = surname ?? rng.Pick(Surnames);
            string name = family + given;
            if (!used.Contains(name))
            {
                used.Add(name);
                return name;
            }
        }
        // 理论上到不了这里（组合空间 >6 万）；兜底用 "双/再" 前缀
        for (int attempt = 1; ; attempt++)
        {
            string? given = sex == Sex.Male ? RollMale(rng) : RollFemale(rng);
            string family = surname ?? rng.Pick(Surnames);
            string name = $"{family}{(attempt == 1 ? "双" : attempt == 2 ? "再" : "末")}{given}";
            if (!used.Contains(name)) { used.Add(name); return name; }
        }
    }

    private static string RollMale(Rng rng)
    {
        int m = rng.Next(0, 10);
        return m switch
        {
            <= 3 => rng.Pick(MaleGiven),
            <= 5 => rng.Pick(NumPrefix) + rng.Pick(NounEnding),
            <= 7 => "" + rng.Pick(MaleChars),
            _ => rng.Pick(AuspiciousA) + rng.Pick(AuspiciousB)
        };
    }

    private static string RollFemale(Rng rng)
    {
        int m = rng.Next(0, 10);
        return m switch
        {
            <= 3 => rng.Pick(FemaleGiven),
            <= 5 => "小" + rng.Pick(FemaleChars),
            <= 7 => "" + rng.Pick(FemaleChars),
            _ => rng.Pick(FlowerA) + rng.Pick(FlowerB)
        };
    }
}
