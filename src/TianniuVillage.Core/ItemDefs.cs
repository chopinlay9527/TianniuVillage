namespace TianniuVillage.Core;

public sealed record ItemDef(string Id, string NameZh, ItemCategory Category, float FoodValue);

public enum ItemCategory { Food, Material, Medicine }

public static class ItemDefs
{
    public static readonly ItemDef Berries = new("berries", "浆果", ItemCategory.Food, 30f);
    public static readonly ItemDef Mushroom = new("mushroom", "蘑菇", ItemCategory.Food, 35f);
    public static readonly ItemDef Fish = new("fish", "鲜鱼", ItemCategory.Food, 40f);
    public static readonly ItemDef Meat = new("meat", "生肉", ItemCategory.Food, 45f);
    public static readonly ItemDef Grain = new("grain", "谷物", ItemCategory.Food, 25f);
    public static readonly ItemDef Meal = new("meal", "熟食", ItemCategory.Food, 65f);
    public static readonly ItemDef Water = new("water", "淡水", ItemCategory.Food, 0f);
    public static readonly ItemDef Hide = new("hide", "皮毛", ItemCategory.Material, 0f);
    public static readonly ItemDef Fiber = new("fiber", "纤维", ItemCategory.Material, 0f);
    public static readonly ItemDef Cloth = new("cloth", "织物", ItemCategory.Material, 0f);
    public static readonly ItemDef Clothes = new("clothes", "衣物", ItemCategory.Material, 0f);
    public static readonly ItemDef CopperOre = new("copper_ore", "铜矿石", ItemCategory.Material, 0f);
    public static readonly ItemDef IronOre = new("iron_ore", "铁矿石", ItemCategory.Material, 0f);
    public static readonly ItemDef Copper = new("copper", "铜锭", ItemCategory.Material, 0f);
    public static readonly ItemDef Iron = new("iron", "铁锭", ItemCategory.Material, 0f);
    public static readonly ItemDef CopperTool = new("copper_tool", "铜工具", ItemCategory.Material, 0f);
    public static readonly ItemDef IronTool = new("iron_tool", "铁工具", ItemCategory.Material, 0f);
    public static readonly ItemDef Basket = new("basket", "箩筐", ItemCategory.Material, 0f);
    public static readonly ItemDef Wheelbarrow = new("wheelbarrow", "独轮车", ItemCategory.Material, 0f);
    public static readonly ItemDef Armor = new("armor", "护具", ItemCategory.Material, 0f);
    public static readonly ItemDef Milk = new("milk", "羊奶", ItemCategory.Food, 35f);
    public static readonly ItemDef Egg = new("egg", "鸡蛋", ItemCategory.Food, 25f);
    public static readonly ItemDef Wool = new("wool", "羊毛", ItemCategory.Material, 0f);
    public static readonly ItemDef Log = new("log", "原木", ItemCategory.Material, 0f);
    public static readonly ItemDef Plank = new("plank", "木板", ItemCategory.Material, 0f);
    public static readonly ItemDef Stone = new("stone", "石料", ItemCategory.Material, 0f);
    public static readonly ItemDef Herb = new("herb", "草药", ItemCategory.Medicine, 0f);

    public static readonly Dictionary<string, ItemDef> All = new()
    {
        [Berries.Id] = Berries,
        [Mushroom.Id] = Mushroom,
        [Fish.Id] = Fish,
        [Meat.Id] = Meat,
        [Grain.Id] = Grain,
        [Meal.Id] = Meal,
        [Water.Id] = Water,
        [Hide.Id] = Hide,
        [Fiber.Id] = Fiber,
        [Cloth.Id] = Cloth,
        [Clothes.Id] = Clothes,
        [CopperOre.Id] = CopperOre,
        [IronOre.Id] = IronOre,
        [Copper.Id] = Copper,
        [Iron.Id] = Iron,
        [CopperTool.Id] = CopperTool,
        [IronTool.Id] = IronTool,
        [Basket.Id] = Basket,
        [Wheelbarrow.Id] = Wheelbarrow,
        [Armor.Id] = Armor,
        [Milk.Id] = Milk,
        [Egg.Id] = Egg,
        [Wool.Id] = Wool,
        [Log.Id] = Log,
        [Plank.Id] = Plank,
        [Stone.Id] = Stone,
        [Herb.Id] = Herb
    };

    public static string Name(string id) => All.TryGetValue(id, out var def) ? def.NameZh : id;
}
