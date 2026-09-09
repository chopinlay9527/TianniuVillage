namespace TianniuVillage.Core;

public sealed partial class Game
{
    private int _socialScanTick;

    private static readonly string[] ChatTopics =
    [
        "今年的天气真不错",
        "东边的浆果丛又结果了",
        "昨晚的月亮真圆啊",
        "听说山那边有更大的村庄",
        "家里的屋顶该修修了",
        "孩子们长得真快",
        "田里的庄稼喝饱了水",
        "河水涨了不少",
        "新酿的浆果酱味道如何",
        "山里的鹿群又出现了"
    ];

    private void UpdateSocial()
    {
        if (Tick - _socialScanTick < 20) return;
        _socialScanTick = Tick;

        var active = Villagers.Where(v => v.Alive && v.Stage != AgeStage.Infant &&
            v.Activity is VillagerActivity.Wandering or VillagerActivity.Idle or VillagerActivity.Playing).ToList();
        if (active.Count < 2) return;

        for (int i = 0; i < active.Count; i++)
        {
            var a = active[i];
            if (a.SocialCooldown > 0) { a.SocialCooldown--; continue; }
            for (int j = i + 1; j < active.Count; j++)
            {
                var b = active[j];
                if (b.SocialCooldown > 0) continue;
                if (Dist2(a.Pos, b.Pos) > 2) continue;
                HaveConversation(a, b);
                break;
            }
        }
    }

    private void HaveConversation(Villager a, Villager b)
    {
        a.SocialCooldown = 45;
        b.SocialCooldown = 45;

        float affinity = (a.Sociability + b.Sociability) / 200f;
        bool eveningGather = MinuteOfDay >= Balance.WorkEndMinute && MinuteOfDay < Balance.SleepStartMinute;
        float delta = 2.5f + affinity * 3f;
        if (eveningGather) delta *= 2f;
        BumpFriendship(a, b.Id, delta);
        BumpFriendship(b, a.Id, delta);

        MaybeQuarrel(a, b);
        MaybeGossip(a, b);

        string topic = Rng.Pick(ChatTopics);
        a.Speech = topic;
        a.SpeechTicksLeft = 6;
        b.Speech = Rng.Chance(0.5f) ? "可不是嘛" : "哈哈，说得好";
        b.SpeechTicksLeft = 5;

        a.AddMood("与乡亲闲聊", 2f, 8f);
        b.AddMood("与乡亲闲聊", 2f, 8f);

        SocialMsg(0, $"{a.Name}和{b.Name}在路边闲聊：「{topic}」", a, b);

        float oldF = a.Friendships.GetValueOrDefault(b.Id, 0);
        if (oldF > 80 && Rng.Chance(0.1f))
            SocialMsg(4, $"{a.Name}和{b.Name}越走越近，形影不离", a, b);
        else if (oldF is > -20 and < 0 && Rng.Chance(0.1f))
            SocialMsg(4, $"{a.Name}和{b.Name}似乎疏远了", a, b);

        TryProposeMarriage(a, b);
    }

    private void BumpFriendship(Villager v, int otherId, float delta)
    {
        v.Friendships[otherId] = Math.Min(100f, v.Friendships.GetValueOrDefault(otherId) + delta);
    }

    private void TryProposeMarriage(Villager a, Villager b)
    {
        if (a.Stage != AgeStage.Adult || b.Stage != AgeStage.Adult) return;
        if (a.SpouseId != 0 || b.SpouseId != 0) return;
        if (a.Sex == b.Sex) return;
        // 声望高的人更受青睐：择偶好感门槛随双方声望降低（最多 -12）
        float required = 55f - Math.Min(12f, (a.Reputation + b.Reputation) * 0.2f);
        if (a.Friendships.GetValueOrDefault(b.Id) < required) return;

        var sharedHome = World.Buildings.FirstOrDefault(x => x.Id == a.HomeId && x.Id == b.HomeId && x.State == BuildingState.Complete)
            ?? World.Buildings.FirstOrDefault(x => x.Id == a.HomeId && x.State == BuildingState.Complete && x.Occupants < x.Beds)
            ?? World.Buildings.FirstOrDefault(x => x.Id == b.HomeId && x.State == BuildingState.Complete && x.Occupants < x.Beds);
        if (sharedHome == null) return;

        a.Reputation += 2;
        b.Reputation += 2;

        a.SpouseId = b.Id;
        b.SpouseId = a.Id;
        sharedHome.Occupants++;
        if (a.HomeId != sharedHome.Id && a.HomeId != 0)
            ReleaseHome(a);
        if (b.HomeId != sharedHome.Id && b.HomeId != 0)
            ReleaseHome(b);
        a.HomeId = sharedHome.Id;
        b.HomeId = sharedHome.Id;

        Log($"{a.Name}与{b.Name}情投意合，结为夫妇。全村都来喝喜酒", LogSeverity.Important);
        a.AddMood("新婚之喜", 15f, 96f);
        b.AddMood("新婚之喜", 15f, 96f);
        a.Remember($"与{b.Name}成婚，是这辈子最幸福的日子", 10f);
        b.Remember($"与{a.Name}成婚，是这辈子最幸福的日子", 10f);
        foreach (var other in Villagers)
            if (other.Alive && other.Id != a.Id && other.Id != b.Id)
                other.AddMood("村里办了喜事", 5f, 48f);
    }

    private void ReleaseHome(Villager v)
    {
        var old = World.Buildings.FirstOrDefault(x => x.Id == v.HomeId);
        if (old != null) old.Occupants = Math.Max(0, old.Occupants - 1);
    }

    private void ReflectDuringSleep(Villager v)
    {
        if (v.Memories.Count == 0 || MinuteOfDay != 180) return;
        var mem = Rng.Pick(v.Memories);
        if (mem.Importance > 5f)
            v.AddMood($"想起了往事：{mem.Text}", -2f, 12f);
        else
            v.AddMood($"回忆起{mem.Text}", 1f, 8f);
    }
}
