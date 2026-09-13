namespace TianniuVillage.Core;

public static class Balance
{
    public const int MapW = 256;
    public const int MapH = 256;
    public const int InitialVillagers = 6;
    public const int MaxVillagers = 100;

    public const int TicksPerSecond = 5;
    public const int MinutesPerDay = 1440;
    public const int DaysPerSeason = 30;
    public const int SeasonsPerYear = 4;

    public const int WorkStartMinute = 7 * 60;
    public const int WorkEndMinute = 19 * 60;
    public const int SleepStartMinute = 21 * 60;
    public const int WakeMinute = 6 * 60;

    public const float VillagerBaseSpeed = 1f;

    public const float SatietyDecayPerHour = 3f;
    public const float EnergyDecayPerHourAwake = 5f;
    public const float EnergyRecoverPerHourBed = 15f;
    public const float EnergyRecoverPerHourGround = 12f;
    public const float StaminaRegenPerHourIdle = 420f;
    public const float StaminaRegenPerHourSleep = 2400f;
    public const float StaminaCostHeavyPerHour = 120f;
    public const float StaminaCostLightPerHour = 60f;
    public const float HealthRegenPerHour = 1.8f;
    public const float HealthDecayStarvingPerHour = 2.2f;
    public const float HealthDecayColdPerHour = 1.2f;
    public const float HealthDecayIllnessPerHour = 0.2f;

    public const float EatWhenSatietyBelow = 35f;
    public const float SleepWhenEnergyBelow = 22f;
    public const float RestWhenStaminaBelow = 18f;

    public const float MoodBase = 68f;
    public const int MemoryCapacity = 50;

    public const float FarmCropGrowDays = 26f;
    public const int FarmYieldPerCell = 6;
    public const int TreeLogYield = 4;
    public const int TreeRegrowDays = 90;
    public const int BerryYield = 4;
    public const int BerryRegrowDays = 10;
    public const int MushroomYield = 3;
    public const int MushroomRegrowDays = 25;
    public const int StoneYieldPerStage = 6;
    public const int HerbYield = 2;
    public const int HerbRegrowDays = 15;
    public const int FishYield = 3;

    public const int CookMinutesPerMeal = 60;

    public const float PregnancyChancePerDay = 0.03f;
    public const int PregnancyDays = 135;
    public const int PostpartumRestDays = 20;
    public const int AdultAge = 15;
    public const int SchoolStartAge = 7;
    public const int ElderAge = 60;

    public const int AutosaveIntervalGameDays = 5;

    public static readonly float[] RoadSpeedMult = [1f, 1.25f, 1.5f, 1.8f];
    public const int RoadTrafficDirt = 150;

    public const int RoadDirtBridgeTraffic = 90;
    public const int RoadTrafficGravel = 900;
    public const int RoadTrafficStone = 2600;
    public const int RoadMaxFormPerHour = 3;
    public const int RoadMaxSegmentTiles = 14;
    public const int RoadMaxActiveSegments = 2;
    public const int RoadMaxHaulJobs = 6;
    public const int RoadHaulPerSegment = 3;
    public const int RoadMaxRepairJobs = 3;
    public const int RoadBuildWorkMinutesGravel = 30;
    public const int RoadBuildWorkMinutesStone = 45;
    public const int RoadHaulWorkMinutes = 3;
    public static readonly int[] RoadDegradeWear = [0, 400, 700, 1400];
    public static readonly int[] RoadRepairMinutes = [0, 0, 14, 22];
    public const float RoadRepairWearFraction = 0.75f;

    public const int StorageCapVillageCenter = 200;
    public const int StorageCapStorehouse = 300;
    public const int StorageCapGranary = 150;
    public const float FoodSpoilBasePerDay = 0.03f;

    public const float ThirstDecayPerHour = 6f;
    public const float DrinkWhenThirstBelow = 30f;
    public const float DrinkRestoreWell = 60f;
    public const float DrinkRestoreWild = 40f;
    public const float HealthDecayDehydratedPerHour = 3f;
    public const int WaterPerFetch = 5;
    public const int WaterFetchWorkMinutes = 3;
    public const int WaterStockTarget = 12;

    public const int FlaxYield = 3;
    public const int FlaxRegrowDays = 20;
    public const int WeaveMinutes = 45;
    public const int SewMinutes = 30;

    public const int MineWorkMinutes = 300;
    public const int SmeltWorkMinutes = 60;
    public const int CraftWorkMinutes = 45;
    public const float NoToolEfficiency = 1.0f;

    public const float ToolWearChance = 0.03f;
    public const float CopperToolEfficiency = 1.25f;
    public const float IronToolEfficiency = 1.5f;

    public const float NightSpeedPenalty = 0.5f;
    public const float TorchSpeedBonus = 0.3f;
    public const int TorchWoodPerNight = 2;

    public const int PenCapacity = 6;
    public const int RanchCapacity = 8;
    public const float HuntCaptureChance = 0.25f;
    public const float HuntCaptureBonus = 0.15f;
    public const int LivestockFeedPerDay = 1;
    public const int EggsPerChickenPerDay = 2;
    public const int MilkPerSheepPerDay = 2;
    public const int SlaughterSeasons = 2;

    public const float AccidentChanceFell = 0.02f;
    public const float AccidentChanceMine = 0.015f;
    public const float AccidentChanceHunt = 0.03f;
    public const int QuarryDailyStone = 2;
    public const float HerbGardenBoost = 1.5f;
    public const float ClinicRecoveryBoost = 2.0f;
    public const float MentalBreakThreshold = 15f;
    public const int MentalBreakTrigger = 2;
    public const int FestivalFoodCost = 20;
    public const float FestivalHappinessBoost = 12f;
    public const float WoolToClothEfficiency = 1.5f;
    public const int MerchantVisitInterval = 30;
    public const int MerchantStayTicks = 1440;
}
