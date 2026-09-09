namespace TianniuVillage.Core;

public sealed partial class Game
{
    private int _lastBuildingPlanHour = -1;
    private int _lastFullWarningDay = -1;

    public void RunEconomyPlanner()
    {
        bool full = World.StorageFull;
        if (full && Day != _lastFullWarningDay)
        {
            _lastFullWarningDay = Day;
                Log($"仓储已满（{World.StorageUsed()}/{World.StorageCapacity()}），大家优先做工和建造来腾出空间", LogSeverity.Important);
        }
        int pop = Villagers.Count(v => v.Alive && v.IsWorkingAge);
        int food = CountFood();
        bool hasCook = World.BuildingsOf("cookhouse").Any();
        bool hasWharf = World.BuildingsOf("wharf").Any();
        bool hasLodge = World.BuildingsOf("lodge").Any();

        if (!full) PostFoodJobs(food, pop, hasWharf, hasLodge);
        if (!full) PostMaterialJobs();
        PostPlankJobs();
        PostFarmJobs();
        PostCookJob(pop, hasCook);
        PostWaterJobs(hasCook);
        if (MinuteOfDay / 30 != _lastBuildingPlanHour)
        {
            _lastBuildingPlanHour = MinuteOfDay / 30;
            PlanBuildings(pop, food);
        }
        PostBuildingMaterialJobs();
        PostBuildJobs();
    }

    public int CountFood()
    {
        int total = 0;
        foreach (var id in new[] { "meal", "berries", "fish", "meat", "mushroom", "grain" })
            total += World.CountItem(id);
        return total;
    }

    private void PostFoodJobs(int food, int pop, bool hasWharf, bool hasLodge)
    {
        int target = Season switch
        {
            Season.Autumn => pop * 80,
            Season.Winter => pop * 40,
            _ => pop * 30
        };
        int bufferTarget = target + pop * 20;
        int deficit = target - food;
        if (deficit <= 0 && food >= bufferTarget) return;
        if (deficit < 0) deficit = 0;

        int forageCap = Math.Max(2, pop / 3);
        {
            int cap = Math.Min(Season == Season.Autumn ? 8 : forageCap, forageCap + (Season == Season.Autumn ? 3 : 1));
            int want = Math.Max(deficit / 4, (bufferTarget - food) / 8);
            int forageJobs = Math.Clamp(want, 1, cap) - Jobs.ClaimedCount(JobKind.Forage) - Jobs.OpenCount(JobKind.Forage);
            if (forageJobs > 0)
                PostResourceJobs(JobKind.Forage, ResKind.BerryBush, forageJobs, Season == Season.Autumn ? 95 : Season == Season.Winter ? 96 : 90);
        }
        if (Season == Season.Autumn && World.CountItem("mushroom") < pop * 15)
        {
            int mushJobs = 2 - Jobs.ClaimedCount(JobKind.PickMushroom) - Jobs.OpenCount(JobKind.PickMushroom);
            PostResourceJobs(JobKind.PickMushroom, ResKind.MushroomPatch, mushJobs, 80);
        }
        if (hasWharf)
        {
            int fishJobs = 2 - Jobs.ClaimedCount(JobKind.Fish) - Jobs.OpenCount(JobKind.Fish);
            PostResourceJobs(JobKind.Fish, ResKind.FishSpot, fishJobs, Season == Season.Winter ? 88 : 70);
        }
        if (hasLodge && World.Animals.Any(a => a.Kind == "deer"))
        {
            int huntJobs = 1 - Jobs.ClaimedCount(JobKind.Hunt) - Jobs.OpenCount(JobKind.Hunt);
            PostHuntJobs(huntJobs);
        }
    }

    private (int x, int y) ResourceWorkSpot(ResourceNode n)
    {
        if (n.Kind is not (ResKind.StoneOutcrop or ResKind.FishSpot)) return (n.X, n.Y);
        for (int r = 1; r <= 2; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = n.X + dx, y = n.Y + dy;
                    if (World.Map.Walkable(x, y) && World.Map.IsLand(x, y) && World.BuildingAt(x, y) == null)
                        return (x, y);
                }
        return (n.X, n.Y);
    }

    private void PostResourceJobs(JobKind kind, ResKind resKind, int count, int priority)
    {
        if (count <= 0) return;
        foreach (var node in World.Resources.Values)
        {
            if (count <= 0) break;
            if (node.Kind != resKind || !node.Available) continue;
            int tileIdx = World.Map.Index(node.X, node.Y);
            bool alreadyPosted = false;
            foreach (var j in Jobs.All)
                if (j.NodeId == tileIdx && j.State != JobState.Done) { alreadyPosted = true; break; }
            if (alreadyPosted) continue;
            var spot = ResourceWorkSpot(node);
            Jobs.Add(World, kind, priority, spot.x, spot.y, nodeId: tileIdx);
            count--;
        }
    }

    private void PostHuntJobs(int count)
    {
        if (count <= 0) return;
        foreach (var animal in World.Animals.Where(a => a.Kind == "deer").Take(count))
        {
            bool alreadyPosted = false;
            foreach (var j in Jobs.All)
                if (j.AnimalId == animal.Id && j.State != JobState.Done) { alreadyPosted = true; break; }
            if (alreadyPosted) continue;
            Jobs.Add(World, JobKind.Hunt, 65, animal.X, animal.Y, animalId: animal.Id);
        }
    }

    private void PostMaterialJobs()
    {
        int logNeed = 60 - World.CountItem("log");
        if (logNeed > 0)
        {
            int jobs = Math.Clamp(logNeed / 4, 1, 6) - Jobs.ClaimedCount(JobKind.Fell) - Jobs.OpenCount(JobKind.Fell);
            PostResourceJobs(JobKind.Fell, ResKind.Tree, jobs, 55);
        }

        int stoneNeed = 55 - World.CountItem("stone");
        if (stoneNeed > 0)
        {
            int jobs = Math.Clamp(stoneNeed / 6, 1, 3) - Jobs.ClaimedCount(JobKind.Mine) - Jobs.OpenCount(JobKind.Mine);
            PostResourceJobs(JobKind.Mine, ResKind.StoneOutcrop, jobs, 45);
        }

        bool someoneSick = Villagers.Any(v => v.Alive && v.Ill);
        int herbNeed = someoneSick ? 8 : 6;
        if (World.CountItem("herb") < herbNeed)
        {
            int jobs = 2 - Jobs.ClaimedCount(JobKind.GatherHerb) - Jobs.OpenCount(JobKind.GatherHerb);
            PostResourceJobs(JobKind.GatherHerb, ResKind.HerbPatch, jobs, 62);
        }

        int clothTarget = Villagers.Count(v => v.Alive) * 3;
        int fiberNeed = World.BuildingsOf("weaver").Any() ? 12 : 4;
        if (World.CountItem("fiber") < fiberNeed && Season is not Season.Winter)
        {
            int jobs = 2 - Jobs.ClaimedCount(JobKind.GatherFiber) - Jobs.OpenCount(JobKind.GatherFiber);
            PostResourceJobs(JobKind.GatherFiber, ResKind.FlaxPatch, jobs, 58);
        }
        if (World.BuildingsOf("weaver").Any() && World.CountItem("cloth") < clothTarget)
        {
            if (World.CountItem("fiber") >= 2 &&
                Jobs.ClaimedCount(JobKind.Weave) + Jobs.OpenCount(JobKind.Weave) < 1)
            {
                var wv = World.BuildingsOf("weaver").First();
                var spot = World.DoorOf(wv);
                Jobs.Add(World, JobKind.Weave, 84, spot.x, spot.y, buildingId: wv.Id);
            }
        }
        if (World.BuildingsOf("weaver").Any() && Season == Season.Autumn &&
            World.CountItem("clothes") < Villagers.Count(v => v.Alive))
        {
            if (World.CountItem("cloth") >= 1 &&
                Jobs.ClaimedCount(JobKind.SewClothes) + Jobs.OpenCount(JobKind.SewClothes) < 1)
            {
                var wv2 = World.BuildingsOf("weaver").First();
                var spot2 = World.DoorOf(wv2);
                Jobs.Add(World, JobKind.SewClothes, 80, spot2.x, spot2.y, buildingId: wv2.Id);
            }
        }
        PostMetalJobs();
        PostLivestockJobs();
    }

    private void PostFarmJobs()
    {
        var farms = World.BuildingsOf("farm").ToList();
        foreach (var farm in farms)
        {
            for (int i = 0; i < farm.CropPhase.Length; i++)
            {
                int phase = farm.CropPhase[i];
                int cx = farm.X + i % farm.W, cy = farm.Y + i / farm.W;
                if (phase == 0 && Season != Season.Winter)
                    EnsureJob(JobKind.Plow, farm, i, cx, cy, 92);
                else if (phase == 1 && Season == Season.Spring)
                    EnsureJob(JobKind.Sow, farm, i, cx, cy, 95);
                else if (phase == 3)
                    EnsureJob(JobKind.Harvest, farm, i, cx, cy, 96);
            }
        }
    }

    private void EnsureJob(JobKind kind, Building b, int cellIdx, int x, int y, int priority)
    {
        foreach (var j in Jobs.All)
            if (j.Kind == kind && j.BuildingId == b.Id && j.CellIdx == cellIdx && j.State != JobState.Done)
                return;
        Jobs.Add(World, kind, priority, x, y, buildingId: b.Id, cellIdx: cellIdx);
    }

    private void PostCookJob(int pop, bool hasCook)
    {
        if (!hasCook) return;
        int mealTarget = pop * 3;
        if (World.CountItem("meal") >= mealTarget) return;
        if (World.CountItem("grain") < 1) return;
        if (!new[] { "berries", "fish", "meat", "mushroom" }.Any(p => World.CountItem(p) >= 2)) return;
        if (Jobs.ClaimedCount(JobKind.Cook) + Jobs.OpenCount(JobKind.Cook) >= 1) return;

        var cook = World.BuildingsOf("cookhouse").First();
        var spot = World.DoorOf(cook);
        Jobs.Add(World, JobKind.Cook, 85, spot.x, spot.y, buildingId: cook.Id);
    }

    private void PostBuildingMaterialJobs()
    {
        int totalHaul = Jobs.ClaimedCount(JobKind.HaulStone) + Jobs.OpenCount(JobKind.HaulStone);
        if (totalHaul >= Balance.RoadMaxHaulJobs) return;

        foreach (var b in World.Buildings)
        {
            if (b.State != BuildingState.Planned) continue;
            if (b.MaterialsReady) continue;
            foreach (var (item, count) in b.Def.Cost)
            {
                int missing = count - b.Delivered.GetValueOrDefault(item);
                if (missing <= 0) continue;
                int inFlight = Jobs.All.Count(j =>
                    j.Kind == JobKind.HaulStone && j.BuildingId == b.Id && j.ItemId == item &&
                    j.State is JobState.Open or JobState.Claimed);
                if (inFlight >= Math.Min(missing, 3)) continue;
                if (World.CountItem(item) <= 0) continue;
                if (totalHaul >= Balance.RoadMaxHaulJobs) return;
                var door = World.DoorOf(b);
                var job = Jobs.Add(World, JobKind.HaulStone, 89, door.x, door.y, buildingId: b.Id);
                job.ItemId = item;
                totalHaul++;
            }
        }
    }

    private void PostWaterJobs(bool hasCook)
    {
        if (World.CountItem("water") >= Balance.WaterStockTarget) return;
        if (!hasCook && World.CountItem("water") > 0) return;
        if (Jobs.ClaimedCount(JobKind.FetchWater) + Jobs.OpenCount(JobKind.FetchWater) >= 1) return;
        var v0 = Villagers.FirstOrDefault(v => v.Alive);
        if (v0 == null) return;
        var spot = GetWaterSpotPos(v0);
        if (!World.Map.Walkable(spot.x, spot.y)) return;
        Jobs.Add(World, JobKind.FetchWater, 72, spot.x, spot.y);
    }

    private void PostMetalJobs()
    {
        var copperMine = World.BuildingsOf("coppermine").FirstOrDefault();
        if (copperMine != null && World.CountItem("copper_ore") < 30)
        {
            var vein = World.Resources.Values.FirstOrDefault(n => n.Kind == ResKind.CopperVein && n.Available);
            if (vein != null && !Jobs.All.Any(j => j.NodeId == World.Map.Index(vein.X, vein.Y) && j.State != JobState.Done))
            {
                var spot = ResourceWorkSpot(vein);
                Jobs.Add(World, JobKind.MineOre, 68, spot.x, spot.y, nodeId: World.Map.Index(vein.X, vein.Y));
            }
        }

        var ironMine = World.BuildingsOf("ironmine").FirstOrDefault();
        if (ironMine != null && World.CountItem("iron_ore") < 20)
        {
            var vein = World.Resources.Values.FirstOrDefault(n => n.Kind == ResKind.IronVein && n.Available);
            if (vein != null && !Jobs.All.Any(j => j.NodeId == World.Map.Index(vein.X, vein.Y) && j.State != JobState.Done))
            {
                var spot = ResourceWorkSpot(vein);
                Jobs.Add(World, JobKind.MineOre, 65, spot.x, spot.y, nodeId: World.Map.Index(vein.X, vein.Y));
            }
        }

        var smelter = World.BuildingsOf("smelter").FirstOrDefault();
        if (smelter != null)
        {
            if (World.CountItem("copper_ore") >= 2 && World.CountItem("copper") < 20 &&
                Jobs.ClaimedCount(JobKind.Smelt) + Jobs.OpenCount(JobKind.Smelt) < 1)
            {
                var spot = World.DoorOf(smelter);
                var j = Jobs.Add(World, JobKind.Smelt, 78, spot.x, spot.y, buildingId: smelter.Id);
                j.ItemId = "copper_ore";
            }
            if (World.CountItem("iron_ore") >= 2 && World.CountItem("iron") < 10 &&
                Jobs.ClaimedCount(JobKind.Smelt) + Jobs.OpenCount(JobKind.Smelt) < 2)
            {
                var spot = World.DoorOf(smelter);
                var j = Jobs.Add(World, JobKind.Smelt, 76, spot.x, spot.y, buildingId: smelter.Id);
                j.ItemId = "iron_ore";
            }
        }

        var workshop = World.BuildingsOf("workshop").FirstOrDefault();
        if (workshop != null)
        {
            bool needCopperTool = World.CountItem("copper_tool") < Villagers.Count(v => v.Alive && v.IsWorkingAge);
            bool needIronTool = HasTech("prospecting") && World.CountItem("iron") >= 2 && World.CountItem("iron_tool") < 3;
            if (needCopperTool && World.CountItem("copper") >= 1 && World.CountItem("plank") >= 1 &&
                Jobs.ClaimedCount(JobKind.CraftTool) + Jobs.OpenCount(JobKind.CraftTool) < 1)
            {
                var spot = World.DoorOf(workshop);
                var j = Jobs.Add(World, JobKind.CraftTool, 82, spot.x, spot.y, buildingId: workshop.Id);
                j.ItemId = "copper_tool";
            }
            if (needIronTool && World.CountItem("iron") >= 2 && World.CountItem("plank") >= 1 &&
                Jobs.ClaimedCount(JobKind.CraftTool) + Jobs.OpenCount(JobKind.CraftTool) < 2)
            {
                var spot = World.DoorOf(workshop);
                var j = Jobs.Add(World, JobKind.CraftTool, 84, spot.x, spot.y, buildingId: workshop.Id);
                j.ItemId = "iron_tool";
            }
        }
    }

    private void PostLivestockJobs()
    {
        foreach (var b in World.Buildings)
        {
            if (b.State != BuildingState.Complete) continue;
            if (b.Key is not ("pen" or "ranch")) continue;
            if (b.LivestockCount <= 0) continue;

            string feed = "grain";
            if (World.CountItem(feed) >= Balance.LivestockFeedPerDay * b.LivestockCount)
                World.TryTakeItem(feed, Balance.LivestockFeedPerDay * b.LivestockCount);
            else if (b.ProdBuffer.Count > 0)
            {
                foreach (var k in b.ProdBuffer.Keys.ToList()) b.ProdBuffer[k] = Math.Max(1, b.ProdBuffer[k] / 2);
            }

            bool hasProd = b.ProdBuffer.Values.Any(v => v > 0);
            if (hasProd && Jobs.ClaimedCount(JobKind.TendLivestock) + Jobs.OpenCount(JobKind.TendLivestock) < 1)
            {
                var spot = World.DoorOf(b);
                Jobs.Add(World, JobKind.TendLivestock, 78, spot.x, spot.y, buildingId: b.Id);
            }
        }
    }

    public void LivestockDailyTick()
    {
        foreach (var b in World.Buildings)
        {
            if (b.State != BuildingState.Complete) continue;
            if (b.LivestockCount <= 0) continue;
            int days = b.Key switch
            {
                "pen" => Balance.SlaughterSeasons * Balance.DaysPerSeason,
                "ranch" => Balance.SlaughterSeasons * Balance.DaysPerSeason / 2,
                _ => 999
            };
            if (b.LivestockType == "chicken")
            {
                b.ProdBuffer["egg"] = b.ProdBuffer.GetValueOrDefault("egg") + Balance.EggsPerChickenPerDay * b.LivestockCount;
            }
            else if (b.LivestockType == "sheep")
            {
                b.ProdBuffer["milk"] = b.ProdBuffer.GetValueOrDefault("milk") + Balance.MilkPerSheepPerDay * b.LivestockCount;
                b.ProdBuffer["wool"] = b.ProdBuffer.GetValueOrDefault("wool") + b.LivestockCount / 3;
            }
            else if (b.LivestockType == "pig")
            {
                b.ProdBuffer["_slaughter"] = b.ProdBuffer.GetValueOrDefault("_slaughter") + 1;
                if (b.ProdBuffer.GetValueOrDefault("_slaughter") >= days)
                {
                    b.ProdBuffer["_slaughter"] = 0;
                    b.ProdBuffer["meat"] = b.ProdBuffer.GetValueOrDefault("meat") + 4 * b.LivestockCount;
                    b.ProdBuffer["hide"] = b.ProdBuffer.GetValueOrDefault("hide") + 2 * b.LivestockCount;
                }
            }
        }
    }

    private void PostBuildJobs()
    {
        foreach (var b in World.Buildings)
        {
            if (b.State != BuildingState.Planned) continue;
            if (!b.MaterialsReady) continue;
            int existing = Jobs.All.Count(j =>
                j.Kind == JobKind.Build && j.BuildingId == b.Id &&
                j.State is JobState.Open or JobState.Claimed);
            for (int i = existing; i < 2; i++)
            {
                var spot = World.DoorOf(b);
                Jobs.Add(World, JobKind.Build, 88, spot.x, spot.y, buildingId: b.Id);
            }
        }
    }

    private void PlanBuildings(int pop, int food)
    {
        int adults = Villagers.Count(v => v.Alive && v.Stage == AgeStage.Adult);
        int popTotal = Villagers.Count(v => v.Alive);
        bool anySingleAdult = Villagers.Any(v => v.Alive && v.Stage == AgeStage.Adult && v.SpouseId == 0);
        int totalBeds = World.Buildings.Where(b => b.State == BuildingState.Complete)
            .Sum(b => b.Key switch { "hut" => 2, "house" => 4, "manor" => 8, _ => 0 });
        int housesPlanned = World.Buildings.Count(b => b.Key is "hut" or "house" or "manor" && b.State == BuildingState.Planned);
        bool houseWanted = totalBeds < popTotal + 2 && housesPlanned == 0;
        int plannedBuilds = World.Buildings.Count(b => b.State == BuildingState.Planned);

        if (plannedBuilds >= 3 || (plannedBuilds >= 2 && !houseWanted)) return;

        bool hasPlank = World.CountItem("plank") >= 8;
        bool hasLog = World.CountItem("log") >= 6;
        string houseKey = popTotal >= 10 && World.CountItem("cloth") >= 4 && World.CountItem("plank") >= 20 ? "manor"
            : hasPlank ? "house"
            : hasLog ? "hut"
            : "house";

        var candidates = new List<(string key, bool wanted, string reason)>
        {
            (houseKey, houseWanted, houseKey switch
            {
                "hut" => "大家需要一处挡风遮雨的窝，先搭个茅舍吧",
                "manor" => "村子兴旺了，建一座气派的大宅",
                _ => "一位村民提议：我们需要遮风挡雨的住宅"
            }),
            ("storehouse", !World.Buildings.Any(b => b.Key == "storehouse"),
                "材料到处乱放，大家决定建一间仓库"),
            ("sawpit", !World.Buildings.Any(b => b.Key == "sawpit") && World.CountItem("log") > 15,
                "原木越堆越多，建一座锯木坊来加工木板"),
            ("granary", !World.Buildings.Any(b => b.Key == "granary"),
                "粮食堆在露天地里会被鸟兽偷吃，该建一座粮仓了"),
            ("farm", World.Buildings.Count(b => b.Key == "farm") < adults / 4 + 1
                     && Season is Season.Spring or Season.Summer && food < pop * 30,
                "光靠采集不够安稳，开一片农田吧"),
            ("cookhouse", !World.Buildings.Any(b => b.Key == "cookhouse") && World.CountItem("grain") > 15,
                "有了粮食，该建一间烹饪屋让大家吃上热饭"),
            ("well", !World.Buildings.Any(b => b.Key == "well"),
                "打一口水井，取水就不用走远路了"),
            ("weaver", !World.Buildings.Any(b => b.Key == "weaver") && pop >= 6,
                "纤维和皮毛需要织造成布，建一座织布坊"),
            ("study", !World.Buildings.Any(b => b.Key == "study") && pop >= 5,
                "长老们商议着建一间书斋，把经验传下去"),
            ("smelter", !World.Buildings.Any(b => b.Key == "smelter") && HasTech("smelting") && World.CountItem("copper_ore") + World.CountItem("iron_ore") > 0,
                "发现了矿石，建一座熔炉来冶炼金属"),
            ("coppermine", !World.Buildings.Any(b => b.Key == "coppermine") && World.Resources.Values.Any(n => n.Kind == ResKind.CopperVein && n.Amount > 0),
                "山里发现了铜矿脉，建一座矿坑来开采"),
            ("workshop", !World.Buildings.Any(b => b.Key == "workshop") && HasTech("crafting") && World.CountItem("copper") > 0,
                "有了铜锭，建一座手工坊来打造工具"),
            ("ironmine", !World.Buildings.Any(b => b.Key == "ironmine") && HasTech("prospecting") && World.Resources.Values.Any(n => n.Kind == ResKind.IronVein && n.Amount > 0),
                "探明了铁矿脉，建一座铁矿坑来开采"),
            ("pen", !World.Buildings.Any(b => b.Key == "pen") && HasTech("domestication"),
                "猎人们带回了幼崽，建一座畜栏来饲养"),
            ("ranch", !World.Buildings.Any(b => b.Key == "ranch") && HasTech("domestication") && pop >= 8 && World.Buildings.Any(b => b.Key == "pen" && b.LivestockCount > 0),
                "畜栏里的牲畜越来越多，建一座畜牧场来扩大规模"),
            ("herbgarden", !World.Buildings.Any(b => b.Key == "herbgarden") && pop >= 5,
                "采来的草药需要地方培育，建一座药圃"),
            ("clinic", !World.Buildings.Any(b => b.Key == "clinic") && pop >= 10,
                "村里人越来越多了，需要一间诊所"),
            ("hall", !World.Buildings.Any(b => b.Key == "hall") && pop >= 12,
                "村民提议建一座集会所，让大家有个聚会的地方"),
            ("school", !World.Buildings.Any(b => b.Key == "school") && pop >= 12,
                "孩子们到了读书的年纪，建一所学校吧"),
            ("wharf", !World.Buildings.Any(b => b.Key == "wharf") && food < pop * 4,
                "水边鱼群肥美，搭一间渔屋捕鱼"),
            ("lodge", !World.Buildings.Any(b => b.Key == "lodge") && pop >= 8,
                "建一间猎屋，猎人们可以去追猎鹿群了")
        };

        foreach (var (key, wanted, reason) in candidates)
        {
            if (!wanted) continue;
            if (TryPlanBuilding(key, reason))
            {
                plannedBuilds++;
                if (plannedBuilds >= 3 || (plannedBuilds >= 2 && !houseWanted)) return;
            }
        }
    }

    private void PostPlankJobs()
    {
        if (!World.BuildingsOf("sawpit").Any()) return;
        if (World.CountItem("plank") >= 30) return;
        if (World.CountItem("log") < 1) return;
        if (Jobs.ClaimedCount(JobKind.Saw) + Jobs.OpenCount(JobKind.Saw) >= 2) return;
        var sawpit = World.BuildingsOf("sawpit").First();
        var sawSpot = World.DoorOf(sawpit);
        Jobs.Add(World, JobKind.Saw, 90, sawSpot.x, sawSpot.y, buildingId: sawpit.Id);
    }

    public string LastBuildFail = "";

    private bool TryPlanBuilding(string key, string reason)
    {
        var def = BuildingDefs.All[key];
        var site = FindBuildSite(def);
        if (site == null) { LastBuildFail = $"{key}:no-site"; return false; }

        if (def.Cost.Length > 0)
        {
            foreach (var (item, count) in def.Cost)
                if (World.CountItem(item) < count) { LastBuildFail = $"{key}:lack-{item}"; return false; }
        }

        var b = new Building
        {
            Id = World.NextBuildingId++,
            Key = key,
            X = site.Value.x,
            Y = site.Value.y,
            State = BuildingState.Planned
        };
        if (key == "farm")
        {
            b.CropPhase = new int[def.W * def.H];
            b.CropGrowth = new float[def.W * def.H];
        }
        World.Buildings.Add(b);
        Log($"{reason}。村民们在村边选好了新{def.NameZh}的位置，需要的材料会陆续搬过去。", LogSeverity.Important);
        _pendingBuildSites.Add((b.X, b.Y));
        LastBuildFail = "";
        return true;
    }

    private (int x, int y)? FindBuildSite(BuildingDef def)
    {
        var center = World.SettleCenter;
        for (int attempt = 0; attempt < 200; attempt++)
        {
            int radius = attempt < 100 ? 4 + attempt / 20 : 20 + attempt - 100;
            int angleOff = Rng.Next(360);
            double angle = (angleOff + attempt * 7) * Math.PI / 180;
            int x = center.x + (int)(Math.Cos(angle) * radius);
            int y = center.y + (int)(Math.Sin(angle) * radius);
            if (CanPlace(def, x, y)) return (x, y);
        }
        return null;
    }

    private bool CanPlace(BuildingDef def, int x, int y)
    {
        var map = World.Map;
        if (!map.InBounds(x, y) || !map.InBounds(x + def.W - 1, y + def.H - 1)) return false;
        for (int dy = 0; dy < def.H; dy++)
            for (int dx = 0; dx < def.W; dx++)
            {
                int tx = x + dx, ty = y + dy;
                var t = map.Get(tx, ty);
                if (t is not (Terrain.Grass or Terrain.Sand or Terrain.Forest)) return false;
                if (World.BuildingAt(tx, ty) != null) return false;
                if (World.Resources.ContainsKey(map.Index(tx, ty))) return false;
            }
        if (def.NeedsWaterAdjacent && !HasAdjacent(x, y, def.W, def.H, t => t is Terrain.Water or Terrain.DeepWater)) return false;
        if (def.NeedsStoneAdjacent && !HasAdjacent(x, y, def.W, def.H, t => t is Terrain.Mountain or Terrain.Highland)) return false;
        return true;
    }

    private bool HasAdjacent(int x, int y, int w, int h, Func<Terrain, bool> pred)
    {
        for (int dx = -1; dx <= w; dx++)
        {
            if (World.Map.InBounds(x + dx, y - 1) && pred(World.Map.Get(x + dx, y - 1))) return true;
            if (World.Map.InBounds(x + dx, y + h) && pred(World.Map.Get(x + dx, y + h))) return true;
        }
        for (int dy = 0; dy < h; dy++)
        {
            if (World.Map.InBounds(x - 1, y + dy) && pred(World.Map.Get(x - 1, y + dy))) return true;
            if (World.Map.InBounds(x + w, y + dy) && pred(World.Map.Get(x + w, y + dy))) return true;
        }
        return false;
    }

    private void GrowCrops()
    {
        if (Season is not (Season.Spring or Season.Summer)) return;
        float growthPerHour = 100f / (Balance.FarmCropGrowDays * 24f);
        if (Weather is Weather.Rain or Weather.Storm) growthPerHour *= 1.3f;
        foreach (var b in World.Buildings)
        {
            if (b.Key != "farm" || b.State != BuildingState.Complete) continue;
            if (b.CropGrowth.Length != b.CropPhase.Length)
                b.CropGrowth = new float[b.CropPhase.Length];
            for (int i = 0; i < b.CropPhase.Length; i++)
            {
                if (b.CropPhase[i] != 2) continue;
                b.CropGrowth[i] += growthPerHour;
                if (b.CropGrowth[i] >= 100f)
                {
                    b.CropPhase[i] = 3;
                    b.CropGrowth[i] = 0;
                }
            }
        }
    }

    private void TreatSick()
    {
        foreach (var v in Villagers)
        {
            if (!v.Alive || !v.Ill) continue;
            v.IllnessDaysLeft -= 1f / 24f;
            if (v.IllnessDaysLeft <= 0)
            {
                v.Ill = false;
                v.AddMood("大病初愈", -4f, 24f);
                Log($"{v.Name}的病终于好了", LogSeverity.Normal);
                continue;
            }
            if (World.CountItem("herb") >= 1 && Tick % 4 == 0)
            {
                World.TryTakeItem("herb", 1);
                v.IllnessDaysLeft = Math.Max(0.5f, v.IllnessDaysLeft * (HasTech("herbalism") ? 0.5f : 0.75f));
            }
        }
    }
}
