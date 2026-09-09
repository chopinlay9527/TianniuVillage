namespace TianniuVillage.Core;

public sealed partial class Game
{
    private static readonly string[] FoodPreference = ["meal", "berries", "fish", "meat", "mushroom", "grain"];

    private void UpdateNeeds(Villager v)
    {
        switch (v.Activity)
        {
            case VillagerActivity.Sleeping:
                v.Energy = Math.Min(100f, v.Energy + (v.HomeId != 0 ? Balance.EnergyRecoverPerHourBed : Balance.EnergyRecoverPerHourGround) / 60f);
                v.Stamina = Math.Min(100f, v.Stamina + Balance.StaminaRegenPerHourSleep / 60f);
                v.Satiety = Math.Max(0f, v.Satiety - Balance.SatietyDecayPerHour / 60f);
                v.Thirst = Math.Max(0f, v.Thirst - Balance.ThirstDecayPerHour / 60f);
                break;
            case VillagerActivity.Resting:
                v.Stamina = Math.Min(100f, v.Stamina + Balance.StaminaRegenPerHourIdle / 60f);
                v.Energy = Math.Max(0f, v.Energy - Balance.EnergyDecayPerHourAwake / 60f);
                v.Satiety = Math.Max(0f, v.Satiety - Balance.SatietyDecayPerHour / 60f);
                v.Thirst = Math.Max(0f, v.Thirst - Balance.ThirstDecayPerHour / 60f);
                break;
            default:
                v.Energy = Math.Max(0f, v.Energy - Balance.EnergyDecayPerHourAwake / 60f);
                v.Satiety = Math.Max(0f, v.Satiety - Balance.SatietyDecayPerHour / 60f);
                v.Thirst = Math.Max(0f, v.Thirst - Balance.ThirstDecayPerHour / 60f);
                break;
        }

        if (v.Satiety <= 0f)
        {
            v.Health -= Balance.HealthDecayStarvingPerHour / 60f;
            if (v.Health <= 0) Kill(v, DeathCause.Starvation, "在饥饿中倒下了");
        }

        if (v.Thirst <= 0f)
        {
            v.Health -= Balance.HealthDecayDehydratedPerHour / 60f;
            if (v.Health <= 0) Kill(v, DeathCause.Thirst, "因缺水倒下了");
        }

        if (v.Ill)
        {
            float dmg = Balance.HealthDecayIllnessPerHour / 60f;
            v.Health = Math.Max(5f, v.Health - dmg);
        }

        var home = World.Buildings.FirstOrDefault(b => b.Id == v.HomeId && b.State == BuildingState.Complete);
        float warmth = home?.Warmth ?? 0f;
        bool coldSnap = Season == Season.Winter && IsNight && warmth < 0.5f;
        if (coldSnap)
        {
            float coldDmg = Balance.HealthDecayColdPerHour * (1f - warmth) * (v.WinterClothes ? 0.3f : 1f);
            v.Health -= coldDmg / 60f;
            if (v.Health <= 0) Kill(v, DeathCause.Cold, "在寒冬的夜里冻死了");
        }
        if (Season == Season.Winter && !v.WinterClothes && warmth < 0.5f && !v.Ill)
        {
            if (Rng.Chance(0.00005f)) v.AddMood("寒冬里没有御寒的衣物", -5f, 24f);
        }
        if (home != null && home.ComfortBonus > 0 && Rng.Chance(0.001f))
        {
            v.AddMood(home.ComfortBonus >= 0.4f ? "住在宽敞的大宅里" : "有个安稳的家", home.ComfortBonus * 4f, 12f);
        }

        bool fed = v.Satiety > 30f && v.Thirst > 20f && !v.Ill && !coldSnap;
        if (fed && v.Health < 100f)
            v.Health = Math.Min(100f, v.Health + Balance.HealthRegenPerHour / 60f * (v.Age > Balance.ElderAge ? 0.5f : 1f));

        for (int i = v.MoodMods.Count - 1; i >= 0; i--)
        {
            v.MoodMods[i].TicksLeft--;
            if (v.MoodMods[i].TicksLeft <= 0) v.MoodMods.RemoveAt(i);
        }

        v.MentalTodaySum += v.Mood;
        v.MentalTodaySamples++;

        if (v.SpeechTicksLeft > 0 && --v.SpeechTicksLeft == 0) v.Speech = "";
    }
    private void StartDrinking(Villager v)
    {
        if (World.CountItem("water") > 0)
        {
            World.TryTakeItem("water", 1);
            v.Thirst = Math.Min(100f, v.Thirst + Balance.DrinkRestoreWild);
            v.Activity = VillagerActivity.Idle;
            v.DecisionCooldown = 5;
            return;
        }

        var well = World.BuildingsOf("well").FirstOrDefault();
        (int x, int y) spot;
        float restore;
        if (well != null)
        {
            spot = World.DoorOf(well);
            restore = Balance.DrinkRestoreWell;
        }
        else
        {
            ResourceNode? best = null;
            float bestDist = float.MaxValue;
            foreach (var node in World.Resources.Values)
            {
                if (node.Kind != ResKind.WaterSpot || !World.Map.Walkable(node.X, node.Y)) continue;
                float d = Dist2(v.Pos, (node.X, node.Y));
                if (d < bestDist) { bestDist = d; best = node; }
            }
            if (best == null) { v.DecisionCooldown = 30; return; }
            spot = ResourceWorkSpotPublic(best);
            restore = Balance.DrinkRestoreWild;
        }

        v.CurrentJobId = null;
        v.Activity = VillagerActivity.WalkingToJob;
        v.Path = PathFinder.Find(World.Map, v.Pos.x, v.Pos.y, spot.x, spot.y);
        SetSelfTask(v, "drinking", restore.ToString());
        v.DecisionCooldown = 45;
        if (v.Path == null) { v.Activity = VillagerActivity.Idle; SetSelfTask(v, null, null); }
    }

    private void FinishDrinking(Villager v)
    {
        float restore = float.TryParse(v.SelfTaskItem, out var r) ? r : Balance.DrinkRestoreWild;
        v.Thirst = Math.Min(100f, v.Thirst + restore);
        v.Activity = VillagerActivity.Idle;
        SetSelfTask(v, null, null);
        v.DecisionCooldown = 5;
    }

    private void StartEating(Villager v)
    {
        string? chosen = null;
        foreach (var food in FoodPreference)
        {
            if (World.CountItem(food) > 0) { chosen = food; break; }
        }

        if (chosen == null)
        {
            bool found = StartEmergencyForage(v);
            if (!found)
            {
                v.Activity = v.Energy < 40f ? VillagerActivity.Idle : VillagerActivity.Wandering;
                SetSelfTask(v, null, null);
                v.DecisionCooldown = 30;
            }
            return;
        }

        World.TryTakeItem(chosen, 1);
        v.CurrentJobId = null;
        var site = NearestFoodSite(v);
        if (site.HasValue && (site.Value.x != v.Pos.x || site.Value.y != v.Pos.y))
        {
            v.Activity = VillagerActivity.WalkingToJob;
            v.Path = PathFinder.Find(World.Map, v.Pos.x, v.Pos.y, site.Value.x, site.Value.y);
            v.TicksInActivity = 0;
            v.DecisionCooldown = 60;
            SetSelfTask(v, "eating", chosen);
            return;
        }
        BeginEat(v, chosen);
    }

    private void BeginEat(Villager v, string item)
    {
        v.Activity = VillagerActivity.Eating;
        v.TicksInActivity = 0;
        SetSelfTask(v, "eating", item);
        v.DecisionCooldown = 30;
    }

    private void FinishEat(Villager v, string item)
    {
        var def = ItemDefs.All[item];
        float bonus = item == "meal" && HasTech("hotpot") ? 1.15f : 1f;
        v.Satiety = Math.Min(100f, v.Satiety + def.FoodValue * bonus);
        if (item == "meal") v.AddMood("吃了热腾腾的熟食", 5f, 14f);
        else if (item is "fish" or "meat") v.AddMood("吃了生食，有点反胃", -3f, 10f);
        else if (item == "grain") v.AddMood("干啃谷物果腹", -2f, 8f);
        if (v.Satiety < 60f && World.CountItem(item) > 0)
        {
            World.TryTakeItem(item, 1);
            v.TicksInActivity = 0;
            return;
        }
        v.Activity = VillagerActivity.Idle;
        SetSelfTask(v, null, null);
    }

    private void StartSleeping(Villager v)
    {
        var home = World.Buildings.FirstOrDefault(b => b.Id == v.HomeId && b.State == BuildingState.Complete);
        if (home != null)
        {
            var spot = World.DoorOf(home);
            v.Activity = VillagerActivity.WalkingToJob;
            v.Path = PathFinder.Find(World.Map, v.Pos.x, v.Pos.y, spot.x, spot.y);
            SetSelfTask(v, "sleep", "home");
            v.DecisionCooldown = 240;
            return;
        }

        var campfire = FindCampfireSpot(v);
        if (campfire.HasValue && Dist2(v.Pos, campfire.Value) > 4)
        {
            v.Activity = VillagerActivity.WalkingToJob;
            v.Path = PathFinder.Find(World.Map, v.Pos.x, v.Pos.y, campfire.Value.x, campfire.Value.y);
            SetSelfTask(v, "sleep", "campfire");
            v.DecisionCooldown = 120;
        }
        else if (campfire.HasValue)
        {
            v.Pos = campfire.Value;
            v.Activity = VillagerActivity.Sleeping;
            SetSelfTask(v, "sleep", "ground");
            v.DecisionCooldown = 120;
        }
        else
        {
            var safe = FindSafeSleepSpot(v.Pos.x, v.Pos.y);
            if (safe.HasValue) v.Pos = safe.Value;
            v.Activity = VillagerActivity.Sleeping;
            SetSelfTask(v, "sleep", "ground");
            v.DecisionCooldown = 120;
        }
    }

    private (int x, int y)? FindCampfireSpot(Villager v)
    {
        var (cx, cy) = World.SettleCenter;
        for (int r = 2; r <= 6; r++)
        {
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (!World.Map.Walkable(nx, ny) || !World.Map.IsLand(nx, ny)) continue;
                    if (World.BuildingAt(nx, ny) != null) continue;
                    if (Math.Abs(dx) < 2 && Math.Abs(dy) < 2) continue;
                    return (nx, ny);
                }
        }
        return null;
    }

    private (int x, int y)? FindSafeSleepSpot(int x, int y)
    {
        if (World.Map.Walkable(x, y) && World.Map.IsLand(x, y) && World.BuildingAt(x, y) == null)
            return (x, y);
        for (int r = 1; r <= 5; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (World.Map.Walkable(nx, ny) && World.Map.IsLand(nx, ny) && World.BuildingAt(nx, ny) == null)
                        return (nx, ny);
                }
        return null;
    }

    private void WakeUp(Villager v)
    {
        v.Activity = VillagerActivity.Idle;
        v.SleepingIndoor = false;
        if (v.HomeId == 0)
        {
            v.AddMood("露宿荒野", -8f, 20f);
            v.Remember("又睡在了冰冷的地上", 3f);
        }
        SetSelfTask(v, null, null);
    }

    private void StartResting(Villager v)
    {
        v.Activity = VillagerActivity.Resting;
        v.DecisionCooldown = 45;
    }

    private (int x, int y)? NearestFoodSite(Villager v)
    {
        (int x, int y)? best = null;
        float bestDist = float.MaxValue;
        foreach (var b in World.Buildings)
        {
            if (b.State != BuildingState.Complete) continue;
            if (b.Key is not ("granary" or "storehouse" or "house")) continue;
            float d = Dist2(v.Pos, (b.CenterX, b.CenterY));
            if (d < bestDist) { bestDist = d; best = World.DoorOf(b); }
        }
        return best;
    }

    private static float Dist2((int x, int y) a, (int x, int y) b)
    {
        float dx = a.x - b.x, dy = a.y - b.y;
        return dx * dx + dy * dy;
    }
}
