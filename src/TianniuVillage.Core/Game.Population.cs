namespace TianniuVillage.Core;

public sealed partial class Game
{
    private void AssignHomes()
    {
        var singles = Villagers.Where(v => v.Alive && v.HomeId == 0 && v.Stage != AgeStage.Infant)
            .OrderBy(v => v.Id).ToList();
        foreach (var v in singles)
        {
            var home = World.Buildings.FirstOrDefault(b =>
                b.Key is "hut" or "house" or "manor" && b.State == BuildingState.Complete && b.Occupants < b.Beds);
            if (home == null) break;
            home.Occupants++;
            v.HomeId = home.Id;
            Log($"{v.Name}搬进了新家", LogSeverity.Normal);
        }
    }

    private void DailyTick_Clothes() { }

    private void DistributeWinterClothes()
    {
        if (Season == Season.Winter && !World.WinterClothesAssigned)
        {
            World.WinterClothesAssigned = true;
            int unprepared = 0;
            foreach (var v in Villagers)
            {
                if (!v.Alive) continue;
                if (World.TryTakeItem("clothes", 1) || (HasTech("boneneedle") && Rng.Chance(0.5f)))
                    v.WinterClothes = true;
                else { v.WinterClothes = false; unprepared++; }
            }
            if (unprepared > 0)
                Log($"寒冬来临，{unprepared}位村民没有过冬的衣物", LogSeverity.Important);
            else
                Log("家家户户都备好了过冬的衣物", LogSeverity.Normal);
        }
        else if (Season != Season.Winter && World.WinterClothesAssigned)
        {
            World.WinterClothesAssigned = false;
            foreach (var v in Villagers) v.WinterClothes = false;
        }
    }

    private int _lastHomelessLogDay = -1;

    private void DailyHousing()
    {
        int homeless = 0;
        foreach (var v in Villagers)
        {
            if (!v.Alive || v.Stage == AgeStage.Infant) continue;
            if (v.HomeId == 0)
            {
                homeless++;
                v.Happiness = Math.Max(0f, v.Happiness - 0.5f);
            }
            else
            {
                v.Happiness = Math.Min(100f, v.Happiness + 0.2f);
            }
        }
        if (homeless > 0 && Day != _lastHomelessLogDay)
        {
            _lastHomelessLogDay = Day;
            Log($"{homeless}位村民还没有栖身之所，风餐露宿让他们的处境更糟", LogSeverity.Normal);
        }
    }

    private void AgeAndLifecycle()
    {
        foreach (var v in Villagers.ToArray())
        {
            if (!v.Alive) continue;
            v.Age += 1f / 365f;
            if (v.PostpartumDays > 0) v.PostpartumDays--;

            if (v.PregnantDaysLeft > 0)
            {
                v.PregnantDaysLeft--;
                if (v.PregnantDaysLeft == 0) GiveBirth(v);
            }
            else if (v.Sex == Sex.Female && v.SpouseId != 0 && v.HomeId != 0 &&
                     v.Age >= 16 && v.Age <= 42 && v.Health > 50 && v.Happiness > 55 &&
                     Season is Season.Spring or Season.Summer or Season.Autumn && Villagers.Count(x => x.Alive) < Balance.MaxVillagers)
            {
                if (Rng.Chance(Balance.PregnancyChancePerDay * (HasTech("rites") ? 1.5f : 1f)))
                {
                    v.PregnantDaysLeft = Balance.PregnancyDays;
                    Log($"{v.Name}怀孕了，甜牛村又将迎来新的生命", LogSeverity.Important);
                }
            }

            if (v.Age > Balance.ElderAge)
            {
                float t = v.Age - 58f;
                float hazard = t * t / 130000f;
                if (Rng.Chance(hazard))
                {
                    Kill(v, DeathCause.OldAge, "在睡梦中安详地离开了人世");
                    continue;
                }
            }
        }

        AssignHomes();
    }

    private void GiveBirth(Villager v)
    {
        var sex = Rng.Chance(0.5f) ? Sex.Male : Sex.Female;
        var baby = new Villager
        {
            Id = World.NextVillagerId++,
            Name = NameGen.Next(Rng, sex),
            Sex = sex,
            Age = 0f,
            Pos = v.Pos,
            HomeId = v.HomeId,
            Diligence = (v.Diligence + Rng.NextFloat(0, 100)) / 2,
            Optimism = (v.Optimism + Rng.NextFloat(0, 100)) / 2,
            Sociability = (v.Sociability + Rng.NextFloat(0, 100)) / 2
        };
        foreach (var key in baby.Skills.Keys.ToList()) baby.Skills[key] = 5f;
        Villagers.Add(baby);
        v.ChildrenIds.Add(baby.Id);
        v.PostpartumDays = Balance.PostpartumRestDays;
        TotalBirths++;
        var father = Villagers.FirstOrDefault(o => o.Id == v.SpouseId);
        if (father != null) father.ChildrenIds.Add(baby.Id);
        Log($"{v.Name}顺利诞下{(sex == Sex.Male ? "男" : "女")}婴{baby.Name}，全村都来道贺", LogSeverity.Important);
        v.AddMood("新生命降生", 12f, 72f);
        if (father != null) father.AddMood("当爹了", 12f, 72f);
    }

    public void Kill(Villager v, DeathCause cause, string detail)
    {
        if (!v.Alive) return;
        v.Alive = false;
        TotalDeaths++;
        AbandonJob(v);
        var home = World.Buildings.FirstOrDefault(b => b.Id == v.HomeId);
        if (home != null) home.Occupants = Math.Max(0, home.Occupants - 1);
        if (v.SpouseId != 0)
        {
            var spouse = Villagers.FirstOrDefault(o => o.Id == v.SpouseId);
            if (spouse != null)
            {
                spouse.SpouseId = 0;
                spouse.AddMood("丧偶之痛", -20f, 240f);
                spouse.Remember($"挚爱{v.Name}走了", 10f);
            }
        }
        foreach (var other in Villagers)
        {
            if (!other.Alive || other.Id == v.Id) continue;
            if (other.ChildrenIds.Contains(v.Id) || v.ChildrenIds.Contains(other.Id))
            {
                other.AddMood("痛失亲人", -15f, 168f);
                other.Remember($"亲人{v.Name}离开了", 9f);
            }
            else other.AddMood("村里有人去世了", -5f, 48f);
        }
        Log($"{v.Name}{detail}，享年{(int)v.Age}岁。全村为他送行", LogSeverity.Important);
    }

    private void DailyEvents()
    {
        if (Villagers.Count(v => v.Alive) >= 5)
        {
            float winterMult = Season == Season.Winter ? 2f : 1f;
            if (Rng.Chance(0.008f * winterMult))
            {
                int count = Rng.Next(1, 3);
                var candidates = Villagers.Where(v => v.Alive && !v.Ill && v.Age >= 7).ToList();
                var weighted = new List<Villager>();
                foreach (var v in candidates)
                {
                    weighted.Add(v);
                    if ((!v.WinterClothes && Season == Season.Winter) || v.HomeId == 0) weighted.Add(v);
                }
                Rng.Shuffle(weighted);
                foreach (var v in weighted.Take(count))
                {
                    v.Ill = true;
                    v.IllnessDaysLeft = Rng.NextFloat(3, 8);
                    v.AddMood("生病了，浑身乏力", -8f, 96f);
                    Log($"{v.Name}病倒了，需要草药照料", LogSeverity.Important);
                }
            }
        }

        if (Rng.Chance(0.02f))
        {
            var travelerItems = new[] { ("berries", 30), ("grain", 20), ("herb", 8) };
            var (item, count) = Rng.Pick(travelerItems);
            World.AddItem(item, count);
            var lucky = Villagers.FirstOrDefault(v => v.Alive);
            Log($"一位神秘旅人路过甜牛村，留下了{ItemDefs.Name(item)}×{count}", LogSeverity.Important);
            if (lucky != null) lucky.AddMood("听了旅人讲的远方故事", 6f, 36f);
        }

        if (Villagers.Count(v => v.Alive) <= 3 && Rng.Chance(0.03f * (HasTech("rites") ? 1.5f : 1f)))
        {
            var sex = Rng.Chance(0.5f) ? Sex.Male : Sex.Female;
            var v = new Villager
            {
                Id = World.NextVillagerId++,
                Name = NameGen.Next(Rng, sex),
                Sex = sex,
                Age = Rng.NextFloat(18, 30),
                Pos = FindSpawnNear(World.SettleCenter.x, World.SettleCenter.y),
                Diligence = Rng.NextFloat(40, 90),
                Optimism = Rng.NextFloat(40, 90),
                Sociability = Rng.NextFloat(40, 90)
            };
            foreach (var key in v.Skills.Keys.ToList()) v.Skills[key] = Rng.NextFloat(20, 50);
            Villagers.Add(v);
            Log($"一位名叫{v.Name}的年轻人听闻甜牛村的故事，远道而来定居", LogSeverity.Important);
        }

        if (Rng.Chance(0.02f * (HasTech("stargazing") ? 0.5f : 1f)) && World.Buildings.Any(b => b.State == BuildingState.Complete))
        {
                var fragile = World.Buildings
                    .Where(b => b.State == BuildingState.Complete && b.Key is "farm" or "wharf")
                    .ToList();
            if (fragile.Count > 0)
            {
                var target = Rng.Pick(fragile);
                World.Buildings.Remove(target);
                World.RebuildBlocked();
                Jobs.CancelByBuilding(target.Id);
                Log($"一场暴风雨掀翻了{BuildingDefs.All[target.Key].NameZh}，大家决定重建", LogSeverity.Important);
                foreach (var v in Villagers.Where(v => v.Alive)) v.AddMood("风暴毁了庄稼", -6f, 48f);
            }
        }
    }

    private void RegenResources()
    {
        foreach (var node in World.Resources.Values)
        {
            if (node.RegenDaysLeft > 0)
            {
                node.RegenDaysLeft--;
                if (node.RegenDaysLeft == 0)
                {
                    switch (node.Kind)
                    {
                        case ResKind.Tree:
                            node.Amount = Rng.Next(80, 160);
                            ResChanged.Add(node.Id);
                            break;
                        case ResKind.BerryBush:
                            node.HasBerries = true;
                            ResChanged.Add(node.Id);
                            break;
                        case ResKind.MushroomPatch:
                            node.Amount = Balance.MushroomYield;
                            ResChanged.Add(node.Id);
                            break;
                        case ResKind.HerbPatch:
                            node.Amount = Balance.HerbYield;
                            ResChanged.Add(node.Id);
                            break;
                    }
                }
            }
        }
    }

    private void ReplenishAnimals()
    {
        int deer = World.Animals.Count(a => a.Kind == "deer");
        int rabbits = World.Animals.Count(a => a.Kind == "rabbit");
        if (deer < 14 && Rng.Chance(0.3f))
        {
            var p = WorldGenerator.RandomLandTile(World.Map, Rng, t => t is Terrain.Forest or Terrain.Grass);
            if (p.HasValue) World.Animals.Add(new Animal { Id = World.NextAnimalId++, Kind = "deer", X = p.Value.x, Y = p.Value.y });
        }
        if (rabbits < 20 && Rng.Chance(0.3f))
        {
            var p = WorldGenerator.RandomLandTile(World.Map, Rng, t => t is Terrain.Grass);
            if (p.HasValue) World.Animals.Add(new Animal { Id = World.NextAnimalId++, Kind = "rabbit", X = p.Value.x, Y = p.Value.y });
        }
    }
}
