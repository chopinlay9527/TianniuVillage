using System.Text.Json.Serialization;

namespace TianniuVillage.Core;

public static class DiseaseInfo
{
    public static readonly (string zh, float durationMin, float durationMax, float healthDrain, float contagion)[] Data =
    [
        ("", 0, 0, 0, 0),
        ("风寒", 3, 7, 0.15f, 0.05f),
        ("痢疾", 2, 5, 0.20f, 0.08f),
        ("肺炎", 7, 14, 0.35f, 0.10f),
        ("热射病", 1, 3, 0.25f, 0f),
        ("瘟疫", 10, 20, 0.50f, 0.20f)
    ];

    public static string Zh(DiseaseType t) => Data[(int)t].zh;
}

public sealed class Festival
{
    public FestivalType Type;
    public int Tick;
    public int DurationTicks;
    public string Description = "";
}

public sealed class MerchantVisit
{
    public int ArrivalTick;
    public int DepartureTick;
    public bool Active;
    public (string item, int count, string wantItem, int wantCount)[] Offers = [];
}

public sealed partial class Game
{
    public Festival? CurrentFestival;
    public MerchantVisit? Merchant;
    public int LastFestivalDay = -100;
    public int LastMerchantDay = -100;

    private void EnrichmentDailyTick()
    {
        UpdateRoles();
        TryStartFestival();
        TrySpawnMerchant();
        ProcessAccidents();
        ContagionTick();
        InjuryRecovery();
        MentalBreakTick();
        ApplyBuildingEffects();
    }

    private void UpdateRoles()
    {
        var adults = Villagers.Where(v => v.Alive && v.Stage == AgeStage.Adult).ToList();
        if (adults.Count == 0) return;

        var elder = adults.OrderByDescending(v => v.Age).FirstOrDefault();
        if (elder != null && elder.Role != VillageRole.Elder)
        {
            foreach (var v in Villagers.Where(x => x.Alive && x.Role == VillageRole.Elder)) v.Role = VillageRole.None;
            elder.Role = VillageRole.Elder;
            elder.Reputation += 5;
            Log($"{elder.Name}被推举为村中长老", LogSeverity.Normal);
        }

        var hunter = adults.OrderByDescending(v => v.SkillOf("hunting")).FirstOrDefault();
        if (hunter != null && hunter.SkillOf("hunting") > 50 && hunter.Role == VillageRole.None)
        {
            hunter.Role = VillageRole.HuntChief;
            hunter.Reputation += 5;
            Log($"{hunter.Name}成为猎队队长", LogSeverity.Normal);
        }

        var healer = adults.OrderByDescending(v => v.SkillOf("medicine")).FirstOrDefault();
        if (healer != null && healer.SkillOf("medicine") > 50 && healer.Role == VillageRole.None)
        {
            healer.Role = VillageRole.Healer;
            healer.Reputation += 5;
            Log($"{healer.Name}被尊为村中医师", LogSeverity.Normal);
        }
    }

    private void TryStartFestival()
    {
        if (CurrentFestival != null) return;
        if (Day - LastFestivalDay < 15) return;

        var dayOfSeason = Day % Balance.DaysPerSeason;
        var festivalType = FestivalType.None;
        string desc = "";

        if (DayOfYear == 0 && Season == Season.Spring)
        {
            festivalType = FestivalType.NewYear;
            desc = "新春佳节";
        }
        else if (Season == Season.Autumn && dayOfSeason >= 20 && World.CountItem("grain") > Balance.FestivalFoodCost)
        {
            festivalType = FestivalType.Harvest;
            desc = "丰收庆典";
        }
        else if (Season == Season.Winter && dayOfSeason >= 10)
        {
            festivalType = FestivalType.WinterSolstice;
            desc = "冬至篝火晚会";
        }
        else if (Rng.Chance(0.02f) && Villagers.Count(v => v.Alive && v.Happiness > 60) >= 4)
        {
            festivalType = FestivalType.Harvest;
            desc = "村民自发组织的篝火晚会";
        }

        if (festivalType == FestivalType.None) return;

        int cost = festivalType == FestivalType.WinterSolstice ? 0 : Balance.FestivalFoodCost;
        if (cost == 0 || World.TryTakeItem("grain", cost))
        {
            CurrentFestival = new Festival
            {
                Type = festivalType,
                Tick = Tick,
                DurationTicks = 120,
                Description = desc
            };
            LastFestivalDay = Day;
            Log($"🎉 {desc}开始了！全村欢聚一堂", LogSeverity.Important);
            foreach (var v in Villagers.Where(v => v.Alive))
            {
                v.AddMood($"参加{desc}", Balance.FestivalHappinessBoost, 48f);
                v.Happiness = Math.Min(100f, v.Happiness + 5f);
            }
        }
    }

    private void TrySpawnMerchant()
    {
        if (Merchant != null && Merchant.Active) return;
        if (Day - LastMerchantDay < Balance.MerchantVisitInterval) return;

        LastMerchantDay = Day;
        var offers = new List<(string, int, string, int)>();
        var stock = World.Stock;
        if (stock.GetValueOrDefault("log") > 30) offers.Add(("log", 20, "grain", 15));
        if (stock.GetValueOrDefault("plank") > 20) offers.Add(("plank", 10, "herb", 5));
        if (stock.GetValueOrDefault("berries") > 60) offers.Add(("berries", 40, "iron_tool", 1));
        if (offers.Count > 0)
        {
            Merchant = new MerchantVisit
            {
                ArrivalTick = Tick,
                DepartureTick = Tick + Balance.MerchantStayTicks,
                Active = true,
                Offers = offers.ToArray()
            };
            Log("一位商人赶着骡车来到村口，带着货物吆喝着", LogSeverity.Important);
        }
    }

    private void ProcessAccidents()
    {
        foreach (var v in Villagers.Where(v => v.Alive && v.Injury == InjuryType.None))
        {
            if (v.Activity != VillagerActivity.Working) continue;
            var job = v.CurrentJobId != null ? Jobs.Get(v.CurrentJobId.Value) : null;
            if (job == null) continue;

            float chance = job.Kind switch
            {
                JobKind.Fell => Balance.AccidentChanceFell,
                JobKind.Mine or JobKind.MineOre => Balance.AccidentChanceMine,
                JobKind.Hunt => Balance.AccidentChanceHunt,
                _ => 0
            };

            if (chance > 0 && Rng.Chance(chance))
            {
                v.Injury = Rng.Chance(0.3f) ? InjuryType.Fracture : InjuryType.Cut;
                v.InjuryDaysLeft = v.Injury == InjuryType.Fracture ? Rng.NextFloat(10, 20) : Rng.NextFloat(3, 7);
                v.Health -= v.Injury == InjuryType.Fracture ? 25 : 10;
                string injuryZh = v.Injury == InjuryType.Fracture ? "骨折" : "被划伤";
                Log($"{v.Name}在工作时{injuryZh}了！", LogSeverity.Important);
                if (v.Health <= 0) Kill(v, DeathCause.Accident, "因公殉职");
            }
        }
    }

    private void ContagionTick()
    {
        var sick = Villagers.Where(v => v.Alive && v.Ill && v.Disease != DiseaseType.None).ToList();
        foreach (var patient in sick)
        {
            float contagion = DiseaseInfo.Data[(int)patient.Disease].contagion;
            if (contagion <= 0) continue;
            foreach (var nearby in Villagers.Where(o => o.Alive && !o.Ill && o.Disease == DiseaseType.None))
            {
                if (Dist2(patient.Pos, nearby.Pos) > 4) continue;
                if (Rng.Chance(contagion * 0.1f))
                {
                    nearby.Ill = true;
                    nearby.Disease = patient.Disease;
                    nearby.IllnessDaysLeft = DiseaseInfo.Data[(int)patient.Disease].durationMin;
                    Log($"{nearby.Name}被{patient.Name}传染了{DiseaseInfo.Zh(patient.Disease)}", LogSeverity.Normal);
                }
            }
        }
    }

    private void InjuryRecovery()
    {
        foreach (var v in Villagers.Where(v => v.Alive && v.Injury != InjuryType.None))
        {
            v.InjuryDaysLeft -= 1f;
            if (v.InjuryDaysLeft <= 0)
            {
                string zh = v.Injury == InjuryType.Fracture ? "骨折" : "伤口";
                v.Injury = InjuryType.None;
                Log($"{v.Name}的{zh}痊愈了", LogSeverity.Normal);
            }
        }
    }

    private void MentalBreakTick()
    {
        foreach (var v in Villagers.Where(v => v.Alive))
        {
            if (v.MentalBreaking)
            {
                v.MentalBreakDaysLeft--;
                if (v.MentalBreakDaysLeft <= 0 || v.Mood > 30f)
                {
                    v.MentalBreaking = false;
                    v.AddMood("终于想通了", 5f, 24f);
                    Log($"{v.Name}走出了低迷", LogSeverity.Normal);
                }
            }
            else if (v.Mood < Balance.MentalBreakThreshold && !v.Ill)
            {
                v.MentalBreakDaysLeft--;
                if (v.MentalBreakDaysLeft <= 0)
                {
                    v.MentalBreaking = true;
                    v.MentalBreakDaysLeft = Rng.Next(2, 5);
                    string[] breaks = ["独自发呆", "蒙头大睡", "在村口徘徊", "拒绝进食"];
                    string brk = Rng.Pick(breaks);
                    Log($"⚠ {v.Name}精神状态恶化，开始{brk}", LogSeverity.Important);
                }
            }
            else
            {
                v.MentalBreakDaysLeft = Balance.MentalBreakTrigger;
            }
        }
    }

    public void ApplyBuildingEffects()
    {
        foreach (var b in World.Buildings.Where(b => b.State == BuildingState.Complete))
        {
            if (b.Key == "quarry" && Tick % 1440 == 0 && World.CountItem("stone") < 60)
            {
                World.AddItem("stone", Balance.QuarryDailyStone);
                if (Rng.Chance(0.1f)) Log("采石场开采出了一批石料", LogSeverity.Normal);
            }
            if (b.Key == "herbgarden" && Tick % 1440 == 0 && World.CountItem("herb") < 20)
            {
                World.AddItem("herb", 2);
                if (Rng.Chance(0.1f)) Log("药圃培育出了一批草药", LogSeverity.Normal);
            }
        }
    }

    private void WearTool(Villager v)
    {
        if (Rng.Chance(Balance.ToolWearChance))
        {
            if (World.CountItem("iron_tool") > 0 && World.TryTakeItem("iron_tool", 1))
                Log($"{v.Name}的铁制工具磨损报废了", LogSeverity.Normal);
            else if (World.CountItem("copper_tool") > 0 && World.TryTakeItem("copper_tool", 1))
                Log($"{v.Name}的铜制工具磨损报废了", LogSeverity.Normal);
        }
    }

    public void TickMerchant()
    {
        if (Merchant == null || !Merchant.Active) return;
        if (Tick >= Merchant.DepartureTick)
        {
            Merchant.Active = false;
            Log("商人收拾货物，赶着骡车离开了", LogSeverity.Normal);
            return;
        }

        if (Merchant.Offers.Length > 0 && Rng.Chance(0.01f))
        {
            var offer = Merchant.Offers[Rng.Next(Merchant.Offers.Length)];
            if (World.CountItem(offer.item) >= offer.count)
            {
                World.TryTakeItem(offer.item, offer.count);
                World.AddItem(offer.wantItem, offer.wantCount);
                Log($"商人用{ItemDefs.Name(offer.wantItem)}×{offer.wantCount}换走了{ItemDefs.Name(offer.item)}×{offer.count}", LogSeverity.Normal);
            }
        }
    }

    public void TickFestival()
    {
        if (CurrentFestival == null) return;
        if (Tick - CurrentFestival.Tick >= CurrentFestival.DurationTicks)
        {
            Log($"{CurrentFestival.Description}圆满结束", LogSeverity.Normal);
            CurrentFestival = null;
            return;
        }

        if (Tick % 20 == 0 && Villagers.Count(v => v.Alive) > 0)
        {
            var v = Villagers[Rng.Next(Villagers.Count)];
            if (v.Alive && Rng.Chance(0.5f))
            {
                string[] joys = ["举杯畅饮", "载歌载舞", "谈笑风生", "品尝美食"];
                v.Speech = Rng.Pick(joys);
                v.SpeechTicksLeft = 8;
            }
        }
    }
}
