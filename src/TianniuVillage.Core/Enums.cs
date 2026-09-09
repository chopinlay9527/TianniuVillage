namespace TianniuVillage.Core;

public enum Terrain : byte
{
    DeepWater = 0,
    Water = 1,
    Sand = 2,
    Grass = 3,
    Forest = 4,
    Highland = 5,
    Mountain = 6
}

public enum ResKind
{
    Tree,
    BerryBush,
    MushroomPatch,
    StoneOutcrop,
    HerbPatch,
    FishSpot,
    WaterSpot,
    FlaxPatch,
    CopperVein,
    IronVein
}

public enum Sex { Male, Female }

public enum AgeStage { Infant, Child, Adult, Elder }

public enum Season { Spring, Summer, Autumn, Winter }

public enum Weather { Sunny, Cloudy, Rain, Storm, Snow, Fog }

public enum DiseaseType
{
    None,
    Cold,
    Dysentery,
    Pneumonia,
    HeatStroke,
    Plague
}

public enum InjuryType
{
    None,
    Bruise,
    Cut,
    Fracture
}

public enum FestivalType
{
    None,
    NewYear,
    Harvest,
    WinterSolstice,
    Wedding,
    Funeral
}

public enum VillageRole
{
    None,
    Elder,
    HuntChief,
    Healer
}

public enum VillagerActivity
{
    Idle,
    Wandering,
    WalkingToJob,
    Working,
    Eating,
    Sleeping,
    Resting,
    Socializing,
    AttendingSchool,
    Playing,
    Recovering,
    Training,
    Drinking,
    Researching
}

public enum JobKind
{
    Fell,
    Forage,
    PickMushroom,
    Mine,
    GatherHerb,
    Fish,
    Hunt,
    Plow,
    Sow,
    Harvest,
    Build,
    Cook,
    Saw,
    HaulStone,
    BuildRoad,
    RepairRoad,
    FetchWater,
    Weave,
    SewClothes,
    GatherFiber,
    MineOre,
    Smelt,
    CraftTool,
    TendLivestock
}

public enum JobState { Open, Claimed, Done, Cancelled }

public enum BuildingState { Planned, UnderConstruction, Complete, Ruined }

public enum LogSeverity { Important, Normal, Debug }

public enum DeathCause { Starvation, Illness, OldAge, Cold, Accident, Fire, Thirst }
