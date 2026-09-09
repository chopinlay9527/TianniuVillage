namespace TianniuVillage.Core;

public sealed partial class Game
{
    public void SetSelfTask(Villager v, string? task, string? item)
    {
        v.SelfTask = task;
        v.SelfTaskItem = item;
    }

    private void UpdateVillagers()
    {
        foreach (var v in Villagers)
        {
            if (!v.Alive) continue;
            if (v.Stage == AgeStage.Infant) { UpdateInfant(v); continue; }

            UpdateNeeds(v);
            if (!v.Alive) continue;

            if (v.Path is { Count: > 0 })
            {
                MoveStep(v);
            }
            else if (v.Activity == VillagerActivity.WalkingToJob)
            {
                OnArrived(v);
            }

            if (!v.Alive) continue;

            switch (v.Activity)
            {
                case VillagerActivity.Working: WorkTick(v); break;
                case VillagerActivity.Eating:
                    if (++v.TicksInActivity >= 10) FinishEat(v, v.SelfTaskItem ?? "berries");
                    break;
                case VillagerActivity.Drinking:
                    if (++v.TicksInActivity >= 3) FinishDrinking(v);
                    break;
                case VillagerActivity.Sleeping:
                    if (ShouldWake(v)) WakeUp(v);
                    else if (v.TicksInActivity == 120) ReflectDuringSleep(v);
                    break;
                case VillagerActivity.Resting:
                    if (v.Stamina > 55f) { v.Activity = VillagerActivity.Idle; SetSelfTask(v, null, null); }
                    break;
                case VillagerActivity.AttendingSchool: SchoolTick(v); break;
                case VillagerActivity.Training: TrainTick(v); break;
                case VillagerActivity.Researching: ResearchTick(v); break;
            }

            if (--v.DecisionCooldown <= 0 && v.Activity is VillagerActivity.Idle or VillagerActivity.Wandering
                or VillagerActivity.Playing or VillagerActivity.Recovering)
                Decide(v);
        }
    }

    private void UpdateInfant(Villager v)
    {
        v.Satiety = Math.Max(0f, v.Satiety - Balance.SatietyDecayPerHour / 120f);
        var mother = Villagers.FirstOrDefault(o => o.Alive && o.Sex == Sex.Female && o.ChildrenIds.Contains(v.Id))
                     ?? Villagers.FirstOrDefault(o => o.Alive && o.Sex == Sex.Female && o.Stage == AgeStage.Adult);
        var home = World.Buildings.FirstOrDefault(b => b.Id == v.HomeId && b.State == BuildingState.Complete);
        v.Pos = home != null ? (home.CenterX, home.CenterY) : mother?.Pos ?? v.Pos;

        if (v.Satiety < 50f && TryFeedInfant())
        {
            v.Satiety = Math.Min(100f, v.Satiety + 50f);
        }
        if (v.Satiety <= 0f)
        {
            v.Health -= Balance.HealthDecayStarvingPerHour / 60f;
            if (v.Health <= 0) Kill(v, DeathCause.Starvation, "还是一个婴儿，没能熬过饥荒");
        }
        v.Age += 1f / 365f;
        if (v.Age >= 7) { /* stage transitions handled by Stage property */ }
    }

    private bool TryFeedInfant()
    {
        foreach (var food in FoodPreference)
            if (World.CountItem(food) > 0 && World.TryTakeItem(food, 1)) return true;
        return false;
    }

    private void MoveStep(Villager v)
    {
        float roadMult = Balance.RoadSpeedMult[World.Map.Roads[World.Map.Index(v.Pos.x, v.Pos.y)]];
        if (HasTech("grassshoes")) roadMult *= 1.15f;
        bool isNight = MinuteOfDay >= Balance.SleepStartMinute || MinuteOfDay < Balance.WakeMinute;
        float nightMult = isNight ? (World.TorchLit ? 1f - Balance.NightSpeedPenalty + Balance.TorchSpeedBonus : Balance.NightSpeedPenalty) : 1f;
        float speed = Balance.VillagerBaseSpeed * v.SpeedFactor * roadMult * nightMult;
        v.MoveProgress += speed;

        while (v.MoveProgress >= 1f && v.Path is { Count: > 0 })
        {
            v.MoveProgress -= 1f;
            var next = v.Path.Dequeue();
            if (!World.Map.Walkable(next.x, next.y)) { v.Path = null; v.MoveProgress = 0; break; }
            v.Facing = next.x > v.Pos.x ? 0 : next.x < v.Pos.x ? 1 : next.y > v.Pos.y ? 2 : 3;
            v.Pos = next;
            int idx = World.Map.Index(next.x, next.y);
            World.Traffic[idx] = World.Traffic.GetValueOrDefault(idx) + 1;
            if (World.Map.Tiles[idx] == Terrain.Water)
            {
                v.Stamina = Math.Max(0f, v.Stamina - 0.5f);
                if (Season == Season.Winter || Weather == Weather.Snow)
                {
                    v.Health = Math.Max(1f, v.Health - 0.3f);
                    if (Rng.Chance(0.02f)) v.AddMood("冬泳真是要命", -3f, 12f);
                }
            }
            if (World.Map.Roads[idx] > 0)
            {
                int wear = World.Map.RoadWear[idx] + 1;
                World.Map.RoadWear[idx] = (ushort)Math.Min(ushort.MaxValue, wear);
            }
        }
        if (v.Path is { Count: 0 }) { v.Path = null; v.MoveProgress = 0; }
    }

    private void OnArrived(Villager v)
    {
        switch (v.SelfTask)
        {
            case "eating":
                if (v.SelfTaskItem != null) BeginEat(v, v.SelfTaskItem);
                else v.Activity = VillagerActivity.Idle;
                break;
            case "sleep":
                v.Activity = VillagerActivity.Sleeping;
                v.SleepingIndoor = v.SelfTaskItem == "home";
                break;
            case "job":
                v.Activity = VillagerActivity.Working;
                v.TicksInActivity = 0;
                break;
            case "school":
                v.Activity = VillagerActivity.AttendingSchool;
                break;
            case "recover":
                v.Activity = VillagerActivity.Recovering;
                v.DecisionCooldown = 30;
                break;
            case "training":
                v.Activity = VillagerActivity.Training;
                v.TicksInActivity = 0;
                break;
            case "research":
                v.Activity = VillagerActivity.Researching;
                v.TicksInActivity = 0;
                break;
            case "drinking":
                v.Activity = VillagerActivity.Drinking;
                break;
            case "store":
                DepositCarrying(v);
                break;
            default:
                v.Activity = v.Stage == AgeStage.Child ? VillagerActivity.Playing : VillagerActivity.Wandering;
                v.DecisionCooldown = 5;
                break;
        }
    }

    private bool ShouldWake(Villager v)
    {
        bool daytime = MinuteOfDay >= Balance.WakeMinute && MinuteOfDay < Balance.SleepStartMinute;
        if (daytime) return true;
        return false;
    }

    private void Decide(Villager v)
    {
        v.DecisionCooldown = 5;

        if (v.Satiety < Balance.EatWhenSatietyBelow) { StartEating(v); return; }
        if (v.Thirst < Balance.DrinkWhenThirstBelow) { StartDrinking(v); return; }
        if (v.CarryLoad.Count > 0 && v.SelfTask == "store" && v.Activity is VillagerActivity.Idle or VillagerActivity.Wandering)
        {
            if (!World.StorageFull)
            {
                BeginCarryingRetry(v);
                return;
            }
        }
        bool nightHours = MinuteOfDay >= Balance.SleepStartMinute || MinuteOfDay < Balance.WakeMinute;
        if (nightHours)
        {
            StartSleeping(v);
            return;
        }
        if (v.Energy < Balance.SleepWhenEnergyBelow)
        {
            StartSleeping(v);
            return;
        }
        if (v.Stamina < Balance.RestWhenStaminaBelow) { StartResting(v); return; }

        if (v.Stage == AgeStage.Child)
        {
            DecideChild(v);
            return;
        }
        if (v.Stage == AgeStage.Infant) return;

        if (v.Ill)
        {
            var home = World.Buildings.FirstOrDefault(b => b.Id == v.HomeId && b.State == BuildingState.Complete);
            if (home != null && Dist2(v.Pos, (home.CenterX, home.CenterY)) > 2)
            {
                var spot = World.DoorOf(home);
                v.Activity = VillagerActivity.WalkingToJob;
                v.Path = PathFinder.Find(World.Map, v.Pos.x, v.Pos.y, spot.x, spot.y);
                SetSelfTask(v, "recover", null);
                v.DecisionCooldown = 60;
            }
            else
            {
                v.Activity = VillagerActivity.Recovering;
                v.DecisionCooldown = 30;
            }
            return;
        }

        bool workHours = MinuteOfDay >= Balance.WorkStartMinute && MinuteOfDay < Balance.WorkEndMinute;
        bool postpartum = v.PostpartumDays > 0;
        bool pregnantLate = v.PregnantDaysLeft > 0 && v.PregnantDaysLeft < 30;

        if (workHours && Weather != Weather.Storm && !postpartum && !pregnantLate)
        {
            if (TryStartResearch(v)) return;
            if (TryClaimBestJob(v)) return;
            if (MinuteOfDay < Balance.WorkEndMinute - 60 && Rng.Chance(0.4f) &&
                v.Skills.Any(s => s.Value < 95f) &&
                (World.BuildingsOf("hall").Any() || World.BuildingsOf("well").Any()))
            {
                StartTraining(v);
                return;
            }
        }

        WanderNearHome(v);
    }

    private void DecideChild(Villager v)
    {
        var school = World.BuildingsOf("school").FirstOrDefault();
        bool schoolHours = MinuteOfDay >= 480 && MinuteOfDay < 900;
            if (school != null && schoolHours && v.Age >= Balance.SchoolStartAge)
            {
                if (v.Activity != VillagerActivity.AttendingSchool)
                {
                    var spot = World.DoorOf(school);
                    v.Activity = VillagerActivity.WalkingToJob;
                    v.Path = PathFinder.Find(World.Map, v.Pos.x, v.Pos.y, spot.x, spot.y);
                    SetSelfTask(v, "school", null);
                    v.DecisionCooldown = 30;
                }
                return;
            }
        if (Rng.Chance(0.5f)) WanderNearHome(v);
        else v.DecisionCooldown = 30;
    }

    private void SchoolTick(Villager v)
    {
        foreach (var key in v.Skills.Keys.ToList())
            v.Skills[key] = Math.Min(100f, v.Skills[key] + 0.01f);
        if (MinuteOfDay >= 900 || MinuteOfDay < 480)
        {
            v.Activity = VillagerActivity.Idle;
            SetSelfTask(v, null, null);
        }
    }

    private void TrainTick(Villager v)
    {
        foreach (var key in v.Skills.Keys.ToList())
            v.Skills[key] = Math.Min(95f, v.Skills[key] + 0.004f);
        if (++v.TicksInActivity >= 90 || MinuteOfDay >= Balance.WorkEndMinute)
        {
            v.Activity = VillagerActivity.Idle;
            SetSelfTask(v, null, null);
            v.DecisionCooldown = 5;
        }
    }

    private void BeginCarryingRetry(Villager v)
    {
        (int x, int y)? dest = null;
        float bestDist = float.MaxValue;
        foreach (var b in World.Buildings)
        {
            if (b.State != BuildingState.Complete) continue;
            if (b.Key is not ("villagecenter" or "storehouse" or "granary")) continue;
            float d = Dist2(v.Pos, (b.CenterX, b.CenterY));
            if (d < bestDist) { bestDist = d; dest = World.DoorOf(b); }
        }
        if (dest == null) { v.DecisionCooldown = 60; return; }
        v.Activity = VillagerActivity.WalkingToJob;
        v.Path = PathFinder.Find(World.Map, v.Pos.x, v.Pos.y, dest.Value.x, dest.Value.y);
        SetSelfTask(v, "store", null);
        v.DecisionCooldown = 90;
        if (v.Path == null) { v.DecisionCooldown = 60; v.Activity = VillagerActivity.Idle; }
    }

    private bool TryStartResearch(Villager v)
    {
        var study = World.BuildingsOf("study").FirstOrDefault();
        if (study == null || World.NextTech == null) return false;
        if (World.Researchers.Count >= 2 && !World.Researchers.Contains(v.Id)) return false;
        var top = Villagers.Where(x => x.Alive && x.IsWorkingAge)
            .OrderByDescending(x => x.SkillOf("learning")).Take(2).Select(x => x.Id).ToHashSet();
        if (!top.Contains(v.Id)) return false;

        var spot = World.DoorOf(study);
        v.Activity = VillagerActivity.WalkingToJob;
        v.Path = PathFinder.Find(World.Map, v.Pos.x, v.Pos.y, spot.x, spot.y);
        SetSelfTask(v, "research", null);
        v.DecisionCooldown = 120;
        if (v.Path == null) { v.Activity = VillagerActivity.Idle; SetSelfTask(v, null, null); return false; }
        World.Researchers.Add(v.Id);
        return true;
    }

    private void ResearchTick(Villager v)
    {
        var tech = World.NextTech;
        if (tech == null)
        {
            v.Activity = VillagerActivity.Idle;
            SetSelfTask(v, null, null);
            return;
        }
        World.CurrentTech = tech.Id;
        World.ResearchProgress += 0.04f * (1f + v.SkillOf("learning") / 100f);
        v.LearnSkill("learning", 0.01f);
        if (World.ResearchProgress >= tech.Cost)
        {
            World.Researched.Add(tech.Id);
            World.ResearchProgress = 0f;
            Log($"{VillageName}领悟了【{tech.NameZh}】：{tech.EffectZh}", LogSeverity.Important);
        }
        if (++v.TicksInActivity >= 90 || MinuteOfDay >= Balance.WorkEndMinute)
        {
            v.Activity = VillagerActivity.Idle;
            SetSelfTask(v, null, null);
            v.DecisionCooldown = 5;
        }
    }

    private void StartTraining(Villager v)
    {
        var hall = World.BuildingsOf("hall").FirstOrDefault()
                   ?? World.BuildingsOf("well").FirstOrDefault();
        if (hall == null) { WanderNearHome(v); return; }
        var spot = World.DoorOf(hall);
        v.Activity = VillagerActivity.WalkingToJob;
        v.Path = PathFinder.Find(World.Map, v.Pos.x, v.Pos.y, spot.x, spot.y);
        SetSelfTask(v, "training", null);
        v.DecisionCooldown = 90;
        if (v.Path == null) { v.Activity = VillagerActivity.Idle; SetSelfTask(v, null, null); }
    }

    private void WanderNearHome(Villager v)
    {
        var hall = World.BuildingsOf("hall").FirstOrDefault();
        var well = World.BuildingsOf("well").FirstOrDefault();
        bool eveningGather = MinuteOfDay >= Balance.WorkEndMinute && MinuteOfDay < Balance.SleepStartMinute;
        var anchor = eveningGather && (hall != null || well != null)
            ? (x: hall?.CenterX ?? well!.CenterX, y: hall?.CenterY ?? well!.CenterY)
            : v.HomeId != 0
                ? World.Buildings.Where(b => b.Id == v.HomeId).Select(b => (x: b.CenterX, y: b.CenterY)).FirstOrDefault()
                : (x: World.SettleCenter.x, y: World.SettleCenter.y);
        int radius = eveningGather && (hall != null || well != null) ? 4 : 8;
        for (int i = 0; i < 8; i++)
        {
            int tx = anchor.x + Rng.Next(-radius, radius + 1), ty = anchor.y + Rng.Next(-radius, radius + 1);
            if (!World.Map.Walkable(tx, ty)) continue;
            var path = PathFinder.Find(World.Map, v.Pos.x, v.Pos.y, tx, ty);
            if (path == null) continue;
            v.Activity = VillagerActivity.WalkingToJob;
            v.Path = path;
            SetSelfTask(v, null, null);
            v.DecisionCooldown = 12;
            return;
        }
        v.Activity = VillagerActivity.Idle;
        v.DecisionCooldown = 30;
    }
}
