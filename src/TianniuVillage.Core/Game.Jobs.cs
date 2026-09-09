namespace TianniuVillage.Core;

public sealed partial class Game
{
    public bool TryClaimBestJob(Villager v)
    {
        Job? best = null;
        float bestScore = 0f;
        foreach (var job in Jobs.All)
        {
            if (job.State != JobState.Open) continue;
            if (job.IsHeavy && (v.Stage == AgeStage.Elder || v.Stamina < 15f)) continue;
            if (job.Kind == JobKind.Build && BuildClaimCount(job.BuildingId) >= 2) continue;
            if (Weather == Weather.Storm && job.Kind is not (JobKind.Cook or JobKind.Saw)) continue;

            float dist = MathF.Sqrt(Dist2(v.Pos, (job.X, job.Y)));
            float distF = 1f / (1f + dist / 40f);
            float skillF = 0.65f + v.SkillOf(job.SkillKey) / 130f;
            float score = job.Priority * skillF * distF * (0.5f + v.Diligence / 200f);
            if (score > bestScore) { bestScore = score; best = job; }
        }

        if (best == null) return false;
        return Claim(v, best);
    }

    private int BuildClaimCount(int buildingId)
    {
        int n = 0;
        foreach (var j in Jobs.All)
            if (j.Kind == JobKind.Build && j.BuildingId == buildingId && j.State == JobState.Claimed)
                n++;
        return n;
    }

    private bool Claim(Villager v, Job job)
    {
        var node = World.Resources.GetValueOrDefault(job.NodeId);
        if (node != null)
        {
            if (!node.Available) { Jobs.Complete(job.Id); return false; }
            node.Reserved = true;
        }

        if (job.Kind == JobKind.Build)
        {
            var b = World.Buildings.FirstOrDefault(x => x.Id == job.BuildingId);
            if (b == null || b.State != BuildingState.Planned) { Jobs.Complete(job.Id); return false; }
        }

        if (job.Kind == JobKind.Cook && !TryConsumeCookIngredients(job))
        {
            Jobs.Complete(job.Id);
            return false;
        }

        if (job.Kind == JobKind.Saw && !World.TryTakeItem("log", 1))
        {
            Jobs.Complete(job.Id);
            return false;
        }

        if (job.Kind == JobKind.Weave && !TryConsumeWeaveMaterials(job))
        {
            Jobs.Complete(job.Id);
            return false;
        }

        if (job.Kind == JobKind.SewClothes && !World.TryTakeItem("cloth", 1))
        {
            Jobs.Complete(job.Id);
            return false;
        }

        if (job.Kind == JobKind.Smelt)
        {
            string ore = job.ItemId == "iron_ore" ? "iron_ore" : "copper_ore";
            if (!World.TryTakeItem(ore, 2) || !World.TryTakeItem("log", 1))
            {
                if (World.CountItem(ore) >= 2) World.AddItem(ore, 2);
                if (World.CountItem("log") >= 1) World.AddItem("log", 1);
                Jobs.Complete(job.Id);
                return false;
            }
        }

        if (job.Kind == JobKind.CraftTool)
        {
            if (job.ItemId == "copper_tool" && (!World.TryTakeItem("copper", 1) || !World.TryTakeItem("plank", 1)))
            {
                Jobs.Complete(job.Id);
                return false;
            }
            if (job.ItemId == "iron_tool" && (!World.TryTakeItem("iron", 2) || !World.TryTakeItem("plank", 1)))
            {
                Jobs.Complete(job.Id);
                return false;
            }
        }

        if (job.Kind == JobKind.HaulStone)
        {
            var sp = GetStoragePos(v);
            job.X = sp.x;
            job.Y = sp.y;
            job.Phase = 0;
        }

        if (job.Kind == JobKind.FetchWater)
        {
            var sp = GetWaterSpotPos(v);
            job.X = sp.x;
            job.Y = sp.y;
        }

        if (job.Kind == JobKind.BuildRoad)
        {
            var seg = World.RoadSegments.GetValueOrDefault(job.SegmentId);
            if (seg == null || !seg.MaterialsReady || seg.NextBuildIndex >= seg.Tiles.Count
                || seg.Tiles[seg.NextBuildIndex] != job.TileIdx)
            {
                Jobs.Complete(job.Id);
                return false;
            }
        }

        if (job.Kind == JobKind.RepairRoad && World.Map.Roads[job.TileIdx] == 0)
        {
            Jobs.Complete(job.Id);
            return false;
        }

        job.State = JobState.Claimed;
        job.ClaimedBy = v.Id;
        v.CurrentJobId = job.Id;
        v.Activity = VillagerActivity.WalkingToJob;
        v.Path = PathFinder.Find(World.Map, v.Pos.x, v.Pos.y, job.X, job.Y);
        SetSelfTask(v, "job", null);
        v.DecisionCooldown = 60;
        if (v.Path == null) { ReleaseJob(v); v.DecisionCooldown = 40; return false; }
        return true;
    }

    private void AbandonJob(Villager v)
    {
        if (v.CurrentJobId == null) return;
        var job = Jobs.Get(v.CurrentJobId.Value);
        v.CurrentJobId = null;
        if (job == null) return;
        job.State = JobState.Open;
        job.ClaimedBy = 0;
        var node = World.Resources.GetValueOrDefault(job.NodeId);
        if (node != null) node.Reserved = false;
        if (job.Kind == JobKind.Cook) RefundCookIngredients(job);
        if (job.Kind == JobKind.Saw) World.AddItem("log", 1);
        if (job.Kind == JobKind.Weave) RefundWeaveMaterials(job);
        if (job.Kind == JobKind.SewClothes) World.AddItem("cloth", 1);
        if (job.Kind == JobKind.Smelt)
        {
            string ore = job.ItemId == "iron_ore" ? "iron_ore" : "copper_ore";
            World.AddItem(ore, 2);
            World.AddItem("log", 1);
        }
        if (job.Kind == JobKind.CraftTool)
        {
            if (job.ItemId == "copper_tool") { World.AddItem("copper", 1); World.AddItem("plank", 1); }
            if (job.ItemId == "iron_tool") { World.AddItem("iron", 2); World.AddItem("plank", 1); }
        }
    }

    private void ReleaseJob(Villager v)
    {
        if (v.CurrentJobId == null) return;
        var job = Jobs.Get(v.CurrentJobId.Value);
        v.CurrentJobId = null;
        if (job == null) return;
        Jobs.Complete(job.Id);
        var node = World.Resources.GetValueOrDefault(job.NodeId);
        if (node != null) node.Reserved = false;
        if (job.Kind == JobKind.Cook) RefundCookIngredients(job);
        if (job.Kind == JobKind.Saw) World.AddItem("log", 1);
        if (job.Kind == JobKind.Weave) RefundWeaveMaterials(job);
        if (job.Kind == JobKind.SewClothes) World.AddItem("cloth", 1);
        if (job.Kind == JobKind.Smelt)
        {
            string ore = job.ItemId == "iron_ore" ? "iron_ore" : "copper_ore";
            World.AddItem(ore, 2);
            World.AddItem("log", 1);
        }
        if (job.Kind == JobKind.CraftTool)
        {
            if (job.ItemId == "copper_tool") { World.AddItem("copper", 1); World.AddItem("plank", 1); }
            if (job.ItemId == "iron_tool") { World.AddItem("iron", 2); World.AddItem("plank", 1); }
        }
    }

    private void WorkTick(Villager v)
    {
        var job = v.CurrentJobId != null ? Jobs.Get(v.CurrentJobId.Value) : null;
        if (job == null) { v.Activity = VillagerActivity.Idle; SetSelfTask(v, null, null); return; }

        if (v.Satiety < 12f || v.Energy < 10f)
        {
            ReleaseJob(v);
            v.Activity = VillagerActivity.Idle;
            v.DecisionCooldown = 1;
            return;
        }

        if (Weather == Weather.Storm && job.Kind is not (JobKind.Cook or JobKind.Saw))
        {
            ReleaseJob(v);
            StartSleeping(v);
            return;
        }

        if (job.Kind == JobKind.Hunt) TrackHuntTarget(v, job);
        if (job.Kind == JobKind.HaulStone) { HaulWorkTick(v, job); return; }
        if (job.Kind == JobKind.FetchWater) { FetchWaterWorkTick(v, job); return; }

        float speed = WorkSpeed(v, job);
        v.WorkAccumulated += 1;
        float cost = job.IsHeavy ? Balance.StaminaCostHeavyPerHour : Balance.StaminaCostLightPerHour;
        v.Stamina = Math.Max(0f, v.Stamina - cost / 60f);

        int required = JobMinutes(job, v);
        if (v.WorkAccumulated >= required * 1f / speed)
        {
            v.WorkAccumulated = 0;
            CompleteJob(v, job);
        }
    }

    private void HaulWorkTick(Villager v, Job job)
    {
        bool toSite = job.BuildingId > 0;
        Building? site = toSite ? World.Buildings.FirstOrDefault(x => x.Id == job.BuildingId) : null;
        RoadSegment? seg = !toSite ? World.RoadSegments.GetValueOrDefault(job.SegmentId) : null;
        if (toSite ? site == null : seg == null)
        {
            Jobs.Complete(job.Id);
            v.CurrentJobId = null;
            v.Activity = VillagerActivity.Idle;
            SetSelfTask(v, null, null);
            v.DecisionCooldown = 2;
            return;
        }

        v.WorkAccumulated++;
        float speed = WorkSpeed(v, job);
        v.Stamina = Math.Max(0f, v.Stamina - Balance.StaminaCostLightPerHour / 60f);
        if (v.WorkAccumulated < Balance.RoadHaulWorkMinutes / speed) return;
        v.WorkAccumulated = 0;

        if (job.Phase == 0)
        {
            job.Phase = 1;
            int tx, ty;
            if (toSite)
            {
                var spot = World.DoorOf(site!);
                tx = spot.x; ty = spot.y;
            }
            else
            {
                int t = seg!.Tiles[Math.Min(seg.NextBuildIndex, seg.Tiles.Count - 1)];
                tx = t % World.Map.W;
                ty = t / World.Map.W;
            }
            job.TileIdx = toSite ? -1 : tx;
            job.X = tx;
            job.Y = ty;
            v.Activity = VillagerActivity.WalkingToJob;
            v.Path = PathFinder.Find(World.Map, v.Pos.x, v.Pos.y, job.X, job.Y);
            if (v.Path == null) ReleaseJob(v);
            return;
        }

        CompleteJob(v, job);
    }

    private float WorkSpeed(Villager v, Job job)
    {
        float skillF = 0.6f + v.SkillOf(job.SkillKey) / 125f;
        float staminaF = v.Stamina < 25f ? 0.6f : 1f;
        float moodF = v.Mood < 30f ? 0.7f : 1f;
        float ageF = v.Stage == AgeStage.Elder ? 0.75f : 1f;
        float weatherF = Weather is Weather.Rain or Weather.Snow or Weather.Fog ? 0.6f : 1f;
        float techF = 1f;
        if (HasTech("knotrecord")) techF *= 1.05f;
        if (HasTech("stoneaxe") && job.Kind == JobKind.Fell) techF *= 1.25f;
        float toolF = 1f;
        if (job.Kind is JobKind.Fell or JobKind.Mine or JobKind.MineOre or JobKind.Plow or JobKind.Harvest)
        {
            if (World.CountItem("iron_tool") > 0) toolF = Balance.IronToolEfficiency;
            else if (World.CountItem("copper_tool") > 0) toolF = Balance.CopperToolEfficiency;
            else toolF = Balance.NoToolEfficiency;
        }
        float injuryF = v.Injury switch
        {
            InjuryType.Fracture => 0.4f,
            InjuryType.Cut => 0.7f,
            _ => 1f
        };
        float mentalF = v.MentalBreaking ? 0.3f : 1f;
        return skillF * staminaF * moodF * ageF * weatherF * techF * toolF * injuryF * mentalF;
    }

    private int JobMinutes(Job job, Villager v)
    {
        if (job.Kind == JobKind.Build)
        {
            var b = World.Buildings.FirstOrDefault(x => x.Id == job.BuildingId);
            return b?.Def.WorkMinutes > 0 ? Math.Max(60, b.Def.WorkMinutes / 2) : 60;
        }
        if (job.Kind == JobKind.Plow) return 60;
        if (job.Kind == JobKind.Sow) return 30;
        if (job.Kind == JobKind.Harvest) return 40;
        if (job.Kind == JobKind.Cook) return Balance.CookMinutesPerMeal;
        if (job.Kind == JobKind.Saw) return 60;
        if (job.Kind == JobKind.Weave) return Balance.WeaveMinutes;
        if (job.Kind == JobKind.SewClothes) return Balance.SewMinutes;
        if (job.Kind == JobKind.Hunt) return 60;
        if (job.Kind == JobKind.BuildRoad)
        {
            var seg = World.RoadSegments.GetValueOrDefault(job.SegmentId);
            return seg?.TargetLevel == 3 ? Balance.RoadBuildWorkMinutesStone : Balance.RoadBuildWorkMinutesGravel;
        }
        if (job.Kind == JobKind.RepairRoad)
        {
            byte lvl = World.Map.Roads[job.TileIdx];
            return lvl < Balance.RoadRepairMinutes.Length ? Math.Max(6, Balance.RoadRepairMinutes[lvl]) : 10;
        }
        if (job.Kind == JobKind.HaulStone) return Balance.RoadHaulWorkMinutes;
        if (job.Kind == JobKind.FetchWater) return Balance.WaterFetchWorkMinutes;
        if (job.Kind == JobKind.Smelt) return Balance.SmeltWorkMinutes;
        if (job.Kind == JobKind.CraftTool) return Balance.CraftWorkMinutes;
        if (job.Kind == JobKind.MineOre) return Balance.MineWorkMinutes;
        var node = World.Resources.GetValueOrDefault(job.NodeId);
        return node?.WorkMinutes ?? 60;
    }

    private void TrackHuntTarget(Villager v, Job job)
    {
        var animal = World.Animals.FirstOrDefault(a => a.Id == job.AnimalId);
        if (animal == null) { ReleaseJob(v); return; }
        if (Dist2(v.Pos, (animal.X, animal.Y)) > 9)
        {
            job.X = animal.X;
            job.Y = animal.Y;
            if (Dist2(v.Pos, (job.X, job.Y)) > 16)
            {
                v.Path = PathFinder.Find(World.Map, v.Pos.x, v.Pos.y, animal.X, animal.Y);
            }
        }
    }

    private void CompleteJob(Villager v, Job job)
    {
        v.CurrentJobId = null;
        v.Activity = VillagerActivity.Idle;
        SetSelfTask(v, null, null);
        v.DecisionCooldown = 2;
        v.LearnSkill(job.SkillKey, 0.5f);

        if (job.Kind is JobKind.Fell or JobKind.Mine or JobKind.MineOre or JobKind.Plow or JobKind.Harvest)
            WearTool(v);

        switch (job.Kind)
        {
            case JobKind.Fell: CompleteResourceJob(v, job, Balance.TreeLogYield, "伐得", true, 1f); break;
            case JobKind.Forage: CompleteResourceJob(v, job, Balance.BerryYield, "采得", false, 0.25f); break;
            case JobKind.PickMushroom: CompleteResourceJob(v, job, Balance.MushroomYield, "采得", false, 0.25f); break;
            case JobKind.Mine: CompleteResourceJob(v, job, Balance.StoneYieldPerStage, "采得", false, 0.5f); break;
            case JobKind.GatherHerb: CompleteResourceJob(v, job, Balance.HerbYield, "采得", false, 0.3f); break;
            case JobKind.Fish: CompleteResourceJob(v, job, Balance.FishYield, "捕得", false, 0.3f); break;
            case JobKind.Hunt: CompleteHunt(v, job); break;
            case JobKind.Build: CompleteBuildTick(v, job); break;
            case JobKind.Cook: CompleteCook(v, job); break;
            case JobKind.Saw: CompleteSaw(v); break;
            case JobKind.Weave: CompleteWeave(v, job); break;
            case JobKind.SewClothes: CompleteSew(v); break;
            case JobKind.HaulStone: CompleteHaulStone(v, job); break;
            case JobKind.FetchWater: CompleteFetchWater(v); break;
            case JobKind.Smelt: CompleteSmelt(v, job); break;
            case JobKind.CraftTool: CompleteCraftTool(v, job); break;
            case JobKind.BuildRoad: CompleteRoadBuild(v, job); break;
            case JobKind.RepairRoad: CompleteRoadRepair(v, job); break;
            case JobKind.Plow: FarmCellPhase(v, job, 1, "开垦了一块田"); break;
            case JobKind.Sow: FarmCellPhase(v, job, 2, "播下了种子"); break;
            case JobKind.Harvest: CompleteHarvest(v, job); break;
            case JobKind.TendLivestock: CompleteTendLivestock(v, job); break;
        }

        Jobs.Complete(job.Id);
    }

    public void CompleteTendLivestockForTest(Villager v, Job job) => CompleteTendLivestock(v, job);

    private void CompleteTendLivestock(Villager v, Job job)
    {
        var b = World.Buildings.FirstOrDefault(x => x.Id == job.BuildingId);
        if (b == null) return;
        foreach (var (item, qty) in b.ProdBuffer)
        {
            if (item.StartsWith("_")) continue;
            if (qty > 0) BeginCarrying(v, item, qty);
        }
        b.ProdBuffer.Clear();
        if (b.LivestockCount > 1 && b.Key == "ranch" && Rng.Chance(0.1f))
        {
            int cap = Balance.RanchCapacity;
            if (b.LivestockCount < cap) { b.LivestockCount++; Log($"{b.LivestockType}在畜牧场里繁殖了！现在有{b.LivestockCount}只", LogSeverity.Normal); }
        }
    }

    public void BeginCarryingForTest(Villager v, string item, int qty) => BeginCarrying(v, item, qty);

    private void BeginCarrying(Villager v, string item, int qty)
    {
        v.CarryLoad[item] = v.CarryLoad.GetValueOrDefault(item) + qty;
        if (v.SelfTask == "store") return;

        (int x, int y)? dest = null;
        float bestDist = float.MaxValue;
        foreach (var b in World.Buildings)
        {
            if (b.State != BuildingState.Complete) continue;
            if (b.Key is not ("villagecenter" or "storehouse" or "granary")) continue;
            float d = Dist2(v.Pos, (b.CenterX, b.CenterY));
            if (d < bestDist) { bestDist = d; dest = World.DoorOf(b); }
        }
        if (dest == null)
        {
            foreach (var (k, q) in v.CarryLoad) World.AddItem(k, q);
            v.CarryLoad.Clear();
            return;
        }
        v.Activity = VillagerActivity.WalkingToJob;
        v.Path = PathFinder.Find(World.Map, v.Pos.x, v.Pos.y, dest.Value.x, dest.Value.y);
        SetSelfTask(v, "store", item);
        v.DecisionCooldown = 90;
        if (v.Path == null)
        {
            foreach (var (k, q) in v.CarryLoad) World.AddItem(k, q);
            v.CarryLoad.Clear();
            SetSelfTask(v, null, null);
        }
    }

    private static readonly string[] FoodItems = ["berries", "mushroom", "fish", "meat", "grain", "meal"];

    private void DepositCarrying(Villager v)
    {
        if (!World.StorageFull)
        {
            foreach (var (item, qty) in v.CarryLoad)
                World.AddItem(item, qty);
            v.CarryLoad.Clear();
            v.Activity = VillagerActivity.Idle;
            SetSelfTask(v, null, null);
            v.DecisionCooldown = 2;
            return;
        }

        var remaining = new Dictionary<string, int>();
        foreach (var (item, qty) in v.CarryLoad)
        {
            if (FoodItems.Contains(item) && ItemDefs.All.TryGetValue(item, out var def) && def.FoodValue > 0)
            {
                v.Satiety = Math.Min(100f, v.Satiety + def.FoodValue * MathF.Min(qty, 2));
                if (qty > 2)
                {
                    int shared = 0;
                    foreach (var other in Villagers)
                    {
                        if (shared >= qty - 2) break;
                        if (!other.Alive || other.Id == v.Id || other.Satiety >= 80f) continue;
                        other.Satiety = Math.Min(100f, other.Satiety + def.FoodValue);
                        shared++;
                    }
                }
                if (Rng.Chance(0.1f))
                    Log($"{v.Name}把多出来的{ItemDefs.Name(item)}分给了乡亲们", LogSeverity.Normal);
            }
            else
            {
                remaining[item] = qty;
            }
        }
        v.CarryLoad.Clear();
        foreach (var (item, qty) in remaining) v.CarryLoad[item] = qty;
        v.Activity = VillagerActivity.Idle;
        SetSelfTask(v, null, null);
        v.DecisionCooldown = v.CarryLoad.Count > 0 ? 60 : 5;
    }

    private void CompleteResourceJob(Villager v, Job job, int baseYield, string verb, bool removeTree, float logChance)
    {
        var node = World.Resources.GetValueOrDefault(job.NodeId);
        if (node == null) return;
        node.Reserved = false;

        int yield = Math.Max(1, (int)(baseYield * (1f + v.SkillOf(job.SkillKey) / 200f)));
        if (Rng.Chance(logChance))
        {
            string itemZh = ItemDefs.Name(node.ItemId);
            Log($"{v.Name}{verb}{itemZh}×{yield}", LogSeverity.Normal);
        }

        switch (node.Kind)
        {
            case ResKind.Tree:
                node.Amount = 0;
                node.RegenDaysLeft = Balance.TreeRegrowDays;
                ResRemoved.Add(node.Id);
                break;
            case ResKind.BerryBush:
                node.HasBerries = false;
                node.RegenDaysLeft = Balance.BerryRegrowDays;
                ResChanged.Add(node.Id);
                break;
            case ResKind.MushroomPatch:
                node.Amount = 0;
                node.RegenDaysLeft = Balance.MushroomRegrowDays;
                ResChanged.Add(node.Id);
                break;
            case ResKind.HerbPatch:
                node.Amount = 0;
                node.RegenDaysLeft = Balance.HerbRegrowDays;
                ResChanged.Add(node.Id);
                break;
            case ResKind.StoneOutcrop:
                node.Amount -= yield;
                ResChanged.Add(node.Id);
                if (node.Amount <= 0)
                {
                    ResRemoved.Add(node.Id);
                    World.Resources.Remove(job.NodeId);
                    World.RebuildBlocked();
                }
                break;
            case ResKind.FishSpot:
                ResChanged.Add(node.Id);
                break;
            case ResKind.CopperVein:
            case ResKind.IronVein:
                node.Amount -= yield;
                ResChanged.Add(node.Id);
                if (node.Amount <= 0)
                {
                    ResRemoved.Add(node.Id);
                    World.Resources.Remove(job.NodeId);
                }
                break;
        }

        BeginCarrying(v, node.ItemId, yield);
    }

    private void CompleteHunt(Villager v, Job job)
    {
        var animal = World.Animals.FirstOrDefault(a => a.Id == job.AnimalId);
        if (animal != null) World.Animals.Remove(animal);

        bool isDeer = animal == null || animal.Kind == "deer";
        int yield = isDeer
            ? Math.Max(2, (int)(5 * (1f + v.SkillOf("hunting") / 200f)))
            : Math.Max(1, (int)(2 * (1f + v.SkillOf("hunting") / 200f)));
        int hides = isDeer ? 1 + (v.SkillOf("hunting") > 50 ? 1 : 0) : (v.SkillOf("hunting") > 50 ? 1 : 0);
        string animalZh = isDeer ? "鹿" : "野兔";

        float captureChance = Balance.HuntCaptureChance + (World.BuildingsOf("lodge").Any() ? Balance.HuntCaptureBonus : 0f);
        var pen = World.Buildings.FirstOrDefault(x => x.Key is "pen" or "ranch" && x.State == BuildingState.Complete);
        if (pen != null && isDeer && Rng.Chance(captureChance))
        {
            int cap = pen.Key == "pen" ? Balance.PenCapacity : Balance.RanchCapacity;
            if (pen.LivestockCount < cap)
            {
                string kind = Rng.Pick(new[] { "chicken", "pig", "sheep" });
                if (pen.LivestockType == "" || pen.LivestockType == kind)
                {
                    pen.LivestockType = kind;
                    pen.LivestockCount++;
                    Log($"{v.Name}捕获了一只活的{kind}，带回了{(pen.Key == "pen" ? "畜栏" : "畜牧场")}", LogSeverity.Normal);
                    BeginCarrying(v, "meat", yield);
                    BeginCarrying(v, "hide", hides);
                    return;
                }
            }
        }

        Log($"{v.Name}猎到了一只{animalZh}，获得生肉×{yield}、皮毛×{hides}", LogSeverity.Normal);
        BeginCarrying(v, "meat", yield);
        BeginCarrying(v, "hide", hides);
    }

    private void CompleteBuildTick(Villager v, Job job)
    {
        var b = World.Buildings.FirstOrDefault(x => x.Id == job.BuildingId);
        if (b == null) return;
        b.WorkDone += Math.Max(60, b.Def.WorkMinutes / 2);
        if (b.WorkDone >= b.Def.WorkMinutes && b.State == BuildingState.Planned)
        {
            b.State = BuildingState.Complete;
            World.RebuildBlocked();
            Log($"{BuildingDefs.All[b.Key].NameZh}落成了", LogSeverity.Important);
            AssignHomes();
            if (_pendingBuildSites.Count > 0) _pendingBuildSites.Clear();
        }
    }

    private void CompleteCook(Villager v, Job job)
    {
        if (job.ItemId == "cheese")
        {
            Log($"{v.Name}把鲜奶做成了奶酪×2", LogSeverity.Normal);
            BeginCarrying(v, "cheese", 2);
            return;
        }
        if (job.ItemId == "jerky")
        {
            Log($"{v.Name}熏制了肉干×3，能存放很久", LogSeverity.Normal);
            BeginCarrying(v, "jerky", 3);
            return;
        }
        Log($"{v.Name}烹好了熟食×2，香气四溢", LogSeverity.Normal);
        BeginCarrying(v, "meal", 2);
    }

    private void CompleteSaw(Villager v)
    {
        BeginCarrying(v, "plank", 2);
    }

    private void CompleteWeave(Villager v, Job job)
    {
        if (job.ItemId == "hide_coat")
        {
            Log($"{v.Name}缝制了一件厚实的毛皮大衣", LogSeverity.Normal);
            BeginCarrying(v, "hide_coat", 1);
            return;
        }
        BeginCarrying(v, "cloth", 1);
    }

    private void CompleteSew(Villager v)
    {
        BeginCarrying(v, "clothes", 1);
        if (Rng.Chance(0.2f)) Log($"{v.Name}缝好了过冬的衣物", LogSeverity.Normal);
    }

    private void CompleteHaulStone(Villager v, Job job)
    {
        string item = string.IsNullOrEmpty(job.ItemId) ? "stone" : job.ItemId;
        if (job.BuildingId > 0)
        {
            var b = World.Buildings.FirstOrDefault(x => x.Id == job.BuildingId);
            if (b != null && World.TryTakeItem(item, 1))
                b.Delivered[item] = b.Delivered.GetValueOrDefault(item) + 1;
            return;
        }
        var seg = World.RoadSegments.GetValueOrDefault(job.SegmentId);
        if (seg != null && World.TryTakeItem(item, 1))
            seg.MaterialsDelivered++;
    }

    private void CompleteRoadBuild(Villager v, Job job)
    {
        var seg = World.RoadSegments.GetValueOrDefault(job.SegmentId);
        if (seg == null) return;
        if (seg.Tiles[seg.NextBuildIndex] == job.TileIdx) seg.NextBuildIndex++;
        World.Map.Roads[job.TileIdx] = seg.TargetLevel;
        World.Map.RoadWear[job.TileIdx] = 0;
        RoadsChanged.Add(job.TileIdx);
        if (seg.NextBuildIndex >= seg.Tiles.Count)
        {
            World.RoadSegments.Remove(seg.Id);
            string zh = seg.TargetLevel == 2 ? "碎石路" : "石板路";
            Log($"{zh}竣工了，路面平整，行走如风", LogSeverity.Normal);
        }
    }

    private void CompleteRoadRepair(Villager v, Job job)
    {
        if (World.Map.Roads[job.TileIdx] > 0)
        {
            World.Map.RoadWear[job.TileIdx] = 0;
        }
    }

    private void FetchWaterWorkTick(Villager v, Job job)
    {
        v.WorkAccumulated++;
        float speed = WorkSpeed(v, job);
        v.Stamina = Math.Max(0f, v.Stamina - Balance.StaminaCostLightPerHour / 60f);
        if (v.WorkAccumulated < Balance.WaterFetchWorkMinutes / speed) return;
        v.WorkAccumulated = 0;
        CompleteJob(v, job);
    }

    private void CompleteFetchWater(Villager v)
    {
        Log($"{v.Name}打了一担清水", LogSeverity.Debug);
        BeginCarrying(v, "water", Balance.WaterPerFetch);
    }

    private void CompleteSmelt(Villager v, Job job)
    {
        if (job.ItemId == "iron_ore")
        {
            World.AddItem("iron", 1);
            if (Rng.Chance(0.3f)) Log($"{v.Name}炼出了一块铁锭", LogSeverity.Normal);
        }
        else
        {
            World.AddItem("copper", 1);
            if (Rng.Chance(0.3f)) Log($"{v.Name}炼出了一块铜锭", LogSeverity.Normal);
        }
    }

    private void CompleteCraftTool(Villager v, Job job)
    {
        if (job.ItemId == "copper_tool")
        {
            World.AddItem("copper_tool", 2);
            Log($"{v.Name}打造了铜工具×2", LogSeverity.Normal);
        }
        else if (job.ItemId == "iron_tool")
        {
            World.AddItem("iron_tool", 2);
            Log($"{v.Name}打造了铁工具×2", LogSeverity.Important);
        }
    }

    private bool TryConsumeCookIngredients(Job job)
    {
        if (job.ItemId == "cheese")
        {
            if (World.CountItem("milk") < 4) return false;
            World.TryTakeItem("milk", 4);
            return true;
        }
        if (job.ItemId == "jerky")
        {
            if (World.CountItem("meat") < 3 || World.CountItem("log") < 1) return false;
            World.TryTakeItem("meat", 3);
            World.TryTakeItem("log", 1);
            return true;
        }
        if (World.CountItem("water") < 1) return false;
        string? protein = null;
        foreach (var p in new[] { "berries", "fish", "meat", "mushroom" })
            if (World.CountItem(p) >= 2) { protein = p; break; }
        if (protein == null || World.CountItem("grain") < 1) return false;
        World.TryTakeItem(protein, 2);
        World.TryTakeItem("grain", 1);
        World.TryTakeItem("water", 1);
        return true;
    }

    private void RefundCookIngredients(Job job)
    {
        if (job.ItemId == "cheese") { World.AddItem("milk", 4); return; }
        if (job.ItemId == "jerky") { World.AddItem("meat", 3); World.AddItem("log", 1); return; }
        World.AddItem("grain", 1);
        World.AddItem("berries", 2);
        World.AddItem("water", 1);
    }

    private bool TryConsumeWeaveMaterials(Job job)
    {
        if (job.ItemId == "hide_coat")
        {
            if (World.CountItem("hide") < 3) return false;
            World.TryTakeItem("hide", 3);
            return true;
        }
        return World.TryTakeItem("fiber", 2);
    }

    private void RefundWeaveMaterials(Job job)
    {
        if (job.ItemId == "hide_coat") { World.AddItem("hide", 3); return; }
        World.AddItem("fiber", 2);
    }

    private void FarmCellPhase(Villager v, Job job, int phase, string logText)
    {
        var b = World.Buildings.FirstOrDefault(x => x.Id == job.BuildingId);
        if (b == null || job.CellIdx >= b.CropPhase.Length) return;
        b.CropPhase[job.CellIdx] = phase;
        v.LearnSkill("farming", 0.4f);
        if (Rng.Chance(0.15f)) Log($"{v.Name}{logText}", LogSeverity.Normal);
    }

    private void CompleteHarvest(Villager v, Job job)
    {
        var b = World.Buildings.FirstOrDefault(x => x.Id == job.BuildingId);
        if (b == null || job.CellIdx >= b.CropPhase.Length) return;
        b.CropPhase[job.CellIdx] = 0;
        int yield = Math.Max(2, (int)(Balance.FarmYieldPerCell * (1f + v.SkillOf("farming") / 200f) * (HasTech("croprotation") ? 1.3f : 1f)));
        Log($"{v.Name}收割庄稼，收获谷物×{yield}", LogSeverity.Normal);
        if (Season == Season.Autumn && Rng.Chance(0.08f))
            Log("田野上一片金黄，又是一个丰收的季节", LogSeverity.Important);
        BeginCarrying(v, "grain", yield);
    }

    private bool StartEmergencyForage(Villager v)
    {
        ResourceNode? best = null;
        float bestDist = float.MaxValue;
        foreach (var node in World.Resources.Values)
        {
            if (!node.Available) continue;
            if (node.ItemId is not ("berries" or "fish" or "mushroom")) continue;
            float d = Dist2(v.Pos, (node.X, node.Y));
            if (d < bestDist) { bestDist = d; best = node; }
        }
        if (best == null) return false;
        var jobKind = best.Kind switch
        {
            ResKind.BerryBush => JobKind.Forage,
            ResKind.MushroomPatch => JobKind.PickMushroom,
            ResKind.FishSpot => JobKind.Fish,
            _ => JobKind.Hunt
        };
        var job = Jobs.Add(World, jobKind, 200, best.X, best.Y, nodeId: World.Map.Index(best.X, best.Y));
        best.Reserved = true;
        job.State = JobState.Claimed;
        job.ClaimedBy = v.Id;
        v.CurrentJobId = job.Id;
        v.Activity = VillagerActivity.WalkingToJob;
        v.Path = PathFinder.Find(World.Map, v.Pos.x, v.Pos.y, best.X, best.Y);
        SetSelfTask(v, "job", null);
        v.DecisionCooldown = 60;
        if (v.Path == null) { ReleaseJob(v); return false; }
        return true;
    }
}
