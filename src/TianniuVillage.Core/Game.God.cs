namespace TianniuVillage.Core;

public sealed partial class Game
{
    public void GodSpawnResource()
    {
        string[] options = ["meal", "berries", "log", "stone", "herb"];
        string item = Rng.Pick(options);
        int count = Rng.Next(30, 61);
        World.AddItem(item, count);
        Log($"【天降祥瑞】{ItemDefs.Name(item)}×{count}从天而降，落入村中仓库", LogSeverity.Important);
    }

    public void GodHealAll()
    {
        int healed = 0;
        foreach (var v in Villagers)
        {
            if (!v.Alive) continue;
            if (v.Health < 100f || v.Ill) healed++;
            v.Health = 100f;
            v.Ill = false;
            v.Satiety = Math.Max(v.Satiety, 80f);
            v.Energy = Math.Max(v.Energy, 80f);
            v.AddMood("神明的祝福洒遍全身", 10f, 48f);
        }
        Log($"【神迹】一道暖光笼罩甜牛村，{healed}位村民恢复健康", LogSeverity.Important);
    }

    public void GodBlessHarvest()
    {
        foreach (var b in World.Buildings)
        {
            if (b.Key != "farm" || b.State != BuildingState.Complete) continue;
            for (int i = 0; i < b.CropPhase.Length; i++)
                if (b.CropPhase[i] is 2 or 3) { b.CropPhase[i] = 4; b.CropGrowth[i] = 0; }
        }
        foreach (var node in World.Resources.Values)
            if (node.Kind == ResKind.BerryBush && node.RegenDaysLeft <= 0) node.HasBerries = true;
        World.AddItem("berries", 40);
        Log("【丰收祝福】田间庄稼一夜成熟，浆果挂满枝头", LogSeverity.Important);
        foreach (var v in Villagers.Where(v => v.Alive)) v.AddMood("亲眼见证丰收的神迹", 8f, 72f);
    }

    public void GodPlague()
    {
        var candidates = Villagers.Where(v => v.Alive && !v.Ill).ToList();
        Rng.Shuffle(candidates);
        int count = Math.Min(candidates.Count, Rng.Next(2, 5));
        foreach (var v in candidates.Take(count))
        {
            v.Ill = true;
            v.IllnessDaysLeft = Rng.NextFloat(8, 15);
            v.AddMood("瘟疫缠身", -10f, 120f);
        }
        Log($"【瘟疫】可怕的疫病在村里蔓延，{count}位村民病倒了", LogSeverity.Important);
    }

    public void GodStorm()
    {
        Weather = Weather.Storm;
        WeatherTicksLeft = 360;
        Log("【狂风暴雨】天色骤变，暴雨倾盆而下", LogSeverity.Important);
    }

    public void GodDrought()
    {
        Weather = Weather.Sunny;
        WeatherTicksLeft = 360;
        foreach (var b in World.Buildings)
        {
            if (b.Key != "farm") continue;
            for (int i = 0; i < b.CropGrowth.Length; i++)
                b.CropGrowth[i] = Math.Max(0f, b.CropGrowth[i] - 30f);
        }
        foreach (var node in World.Resources.Values)
            if (node.Kind == ResKind.BerryBush && Rng.Chance(0.4f)) { node.HasBerries = false; ResChanged.Add(node.Id); }
        Log("【干旱】烈日炙烤着大地，庄稼蔫了，浆果也干瘪了", LogSeverity.Important);
    }

    public void GodFire()
    {
        var complete = World.Buildings.Where(b => b.State == BuildingState.Complete && b.Key != "villagecenter").ToList();
        if (complete.Count == 0) return;
        var target = Rng.Pick(complete);
        World.Buildings.Remove(target);
        World.RebuildBlocked();
        Jobs.CancelByBuilding(target.Id);
        Log($"【火灾】{BuildingDefs.All[target.Key].NameZh}突然起火，烧成了灰烬", LogSeverity.Important);
        foreach (var v in Villagers.Where(v => v.Alive))
            if (Dist2(v.Pos, (target.CenterX, target.CenterY)) < 2500)
                v.AddMood("目睹了大火", -8f, 48f);
        var homeOwner = Villagers.FirstOrDefault(v => v.Alive && v.HomeId == target.Id);
        if (homeOwner != null) { homeOwner.HomeId = 0; Log($"{homeOwner.Name}无家可归了", LogSeverity.Important); }
    }

    public void GodSpawnVillager()
    {
        if (Villagers.Count(v => v.Alive) >= Balance.MaxVillagers) return;
        var sex = Rng.Chance(0.5f) ? Sex.Male : Sex.Female;
        var v = new Villager
        {
            Id = World.NextVillagerId++,
            Name = NameGen.Next(Rng, sex, null, UsedNames),
            Sex = sex,
            Age = Rng.NextFloat(18, 30),
            Pos = FindSpawnNear(World.SettleCenter.x, World.SettleCenter.y),
            Diligence = Rng.NextFloat(40, 90),
            Optimism = Rng.NextFloat(40, 90),
            Sociability = Rng.NextFloat(40, 90)
        };
        foreach (var key in v.Skills.Keys.ToList()) v.Skills[key] = Rng.NextFloat(20, 50);
        Villagers.Add(v);
        Log($"【远客来临】一位名叫{v.Name}的年轻人听闻甜牛村的故事，前来定居", LogSeverity.Important);
    }
}
