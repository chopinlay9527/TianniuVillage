namespace TianniuVillage.Core;

public sealed class SocialMessage
{
    public int Seq;
    public int Tick;
    public int Kind;
    public string Text = "";
    public int[] Actors = [];
}

public sealed partial class Game
{
    public List<SocialMessage> SocialFeed = new();
    private int _socialSeq;
    public int LastSocialSeq => _socialSeq;
    public void SetSocialSeq(int seq) => _socialSeq = seq;

    public void SocialMsg(int kind, string text, params Villager[] actors)
    {
        SocialFeed.Add(new SocialMessage
        {
            Seq = ++_socialSeq,
            Tick = Tick,
            Kind = kind,
            Text = text,
            Actors = actors.Select(a => a.Id).ToArray()
        });
        if (SocialFeed.Count > 300) SocialFeed.RemoveRange(0, 50);
    }

    private void MaybeQuarrel(Villager a, Villager b)
    {
        if (Rng.Chance(0.04f))
        {
            float oldA = a.Friendships.GetValueOrDefault(b.Id, 50);
            if (oldA < 0) return;
            BumpFriendship(a, b.Id, -8f);
            BumpFriendship(b, a.Id, -8f);
            a.AddMood("跟人吵了一架", -6f, 24f);
            b.AddMood("跟人吵了一架", -6f, 24f);
            a.Remember($"和{b.Name}吵了一架", 6f);
            SocialMsg(3, $"{a.Name}和{b.Name}吵了一架，两人气鼓鼓地分开了", a, b);
        }
        else if (a.Friendships.GetValueOrDefault(b.Id, 50) < -15 && Rng.Chance(0.3f))
        {
            BumpFriendship(a, b.Id, 10f);
            BumpFriendship(b, a.Id, 10f);
            a.AddMood("和好如初", 4f, 24f);
            b.AddMood("和好如初", 4f, 24f);
            SocialMsg(3, $"{a.Name}和{b.Name}重归于好，又说说笑笑走在一起", a, b);
        }
    }

    private void MaybeGossip(Villager speaker, Villager listener)
    {
        if (speaker.Memories.Count == 0) return;
        if (!Rng.Chance(0.15f)) return;
        var mem = speaker.Memories[Rng.Next(speaker.Memories.Count)];
        if (mem.Text.Length == 0) return;

        bool negative = mem.Text.Contains("吵") || mem.Text.Contains("病") || mem.Text.Contains("死") || mem.Text.Contains("饿");
        string mood = speaker.Optimism > 60 ? "眉飞色舞" : speaker.Optimism < 40 ? "神神秘秘" : "小声";
        string text = negative
            ? $"{speaker.Name}{mood}地跟{listener.Name}咬耳朵：「{mem.Text}，你可别往外说」"
            : $"{speaker.Name}跟{listener.Name}聊起：「{mem.Text}」";
        SocialMsg(2, text, speaker, listener);

        var subject = Villagers.FirstOrDefault(v => v.Alive && mem.Text.Contains(v.Name));
        if (subject != null && subject.Id != speaker.Id && subject.Id != listener.Id)
        {
            float shift = negative ? -2f : 2f;
            BumpFriendship(listener, subject.Id, shift);
        }
    }

    private void DailyRumor()
    {
        if (!Rng.Chance(0.4f)) return;
        int alive = Villagers.Count(v => v.Alive);
        string rumor = Season switch
        {
            Season.Autumn => Rng.Pick(new[] { "听说明年开春雨水多，得趁早修屋顶", "山那边的村子今年也丰收了，都在传", "老猎人说今年山里的鹿格外肥" }),
            Season.Winter => Rng.Pick(new[] { "听说今年冬天格外长，得多备柴火", "隔壁村有人冻着了，大家要多注意", "老人说冬雪厚来年虫害少" }),
            Season.Spring => Rng.Pick(new[] { "听说河边发现了新的鱼群", "东边林子的蘑菇今年长得早", "游方郎中路过，说今年是个好年景" }),
            _ => Rng.Pick(new[] { "孩子们说在小溪里看到大鱼了", "望山坡的老树又粗了一圈", "夜里听到山里有奇怪的叫声" })
        };
        SocialMsg(1, rumor);
        if (alive > 0)
        {
            var lucky = Villagers[Rng.Next(alive)];
            if (lucky.Alive) lucky.AddMood("听了些有趣的传闻", 1f, 12f);
        }
    }
}
