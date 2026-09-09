using System.Text.Json.Serialization;

namespace TianniuVillage.Core;

public sealed class Villager
{
    public int Id;
    public string Name = "";
    public Sex Sex;
    public float Age;
    public (int x, int y) Pos;

    public float Satiety = 90f;
    public float Thirst = 80f;
    public float Energy = 90f;
    public float Stamina = 100f;
    public float Health = 100f;
    public float Happiness = 70f;
    public float MentalAvgToday = 70f;

    public float Diligence;
    public float Optimism;
    public float Sociability;

    public Dictionary<string, float> Skills = new()
    {
        ["farming"] = 20f, ["building"] = 20f, ["gathering"] = 20f,
        ["cooking"] = 20f, ["medicine"] = 20f, ["hunting"] = 20f,
        ["learning"] = 15f
    };

    public int HomeId;
    public int SpouseId;
    public List<int> ChildrenIds = [];
    public int PregnantDaysLeft;
    public int PostpartumDays;
    public bool Ill;
    public float IllnessDaysLeft;
    public bool WinterClothes;
    public bool SleepingIndoor;
    public bool Alive = true;

    public List<MoodMod> MoodMods = [];
    public List<MemoryEntry> Memories = [];
    public Dictionary<int, float> Friendships = new();
    public string Speech = "";
    public int SpeechTicksLeft;

    public int? CurrentJobId;
    public string? SelfTask;
    public string? SelfTaskItem;
    public Dictionary<string, int> CarryLoad = new();
    public float MoveProgress;
    public int Facing;
    public VillagerActivity Activity = VillagerActivity.Idle;
    [JsonIgnore] public Queue<(int x, int y)>? Path;
    public int TicksInActivity;
    public int WorkAccumulated;
    public int DecisionCooldown;
    public int SocialCooldown;
    public float MentalTodaySum;
    public float MentalTodaySamples;

    [JsonIgnore] public AgeStage Stage => Age switch
    {
        < 7 => AgeStage.Infant,
        < Balance.AdultAge => AgeStage.Child,
        < Balance.ElderAge => AgeStage.Adult,
        _ => AgeStage.Elder
    };

    [JsonIgnore] public bool IsWorkingAge => Stage is AgeStage.Adult or AgeStage.Elder;

    [JsonIgnore] public float Mood
    {
        get
        {
            float sum = 0;
            foreach (var m in MoodMods) sum += m.Amount;
            return Math.Clamp(Balance.MoodBase + Optimism / 12f + sum, 0f, 100f);
        }
    }

    [JsonIgnore] public float SpeedFactor => (Health < 30f ? 0.6f : 1f) * (Energy < 15f ? 0.7f : 1f);

    public void AddMood(string zh, float amount, float hours)
    {
        if (amount == 0) return;
        var existing = MoodMods.Find(m => m.Zh == zh);
        if (existing != null) { existing.Amount += amount; existing.TicksLeft = (int)(hours * 60); return; }
        MoodMods.Add(new MoodMod { Zh = zh, Amount = amount, TicksLeft = (int)(hours * 60) });
        if (MoodMods.Count > 24) MoodMods.Sort((a, b) => a.TicksLeft.CompareTo(b.TicksLeft));
        if (MoodMods.Count > 24) MoodMods.RemoveAt(0);
    }

    public void Remember(string text, float importance)
    {
        Memories.Add(new MemoryEntry { Text = text, Importance = importance, Day = -1 });
        if (Memories.Count > Balance.MemoryCapacity)
            Memories.RemoveAt(0);
    }

    public float SkillOf(string jobSkill) => Skills.TryGetValue(jobSkill, out var v) ? v : 20f;

    public void LearnSkill(string jobSkill, float amount)
    {
        if (jobSkill.Length == 0) return;
        float cur = SkillOf(jobSkill);
        float scaled = amount * Math.Max(0.15f, 1f - cur / 120f);
        Skills[jobSkill] = Math.Min(100f, cur + scaled);
    }
}

public sealed class MoodMod
{
    public string Zh = "";
    public float Amount;
    public int TicksLeft;
}

public sealed class MemoryEntry
{
    public string Text = "";
    public float Importance;
    public int Day;
}
