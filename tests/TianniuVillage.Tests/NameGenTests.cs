using TianniuVillage.Core;

public class NameGenTests
{
    [Fact]
    public void Generate_5000Names_NoDuplicates()
    {
        var used = new HashSet<string>();
        var rng = new Rng(12345);
        for (int i = 0; i < 5000; i++)
        {
            var sex = i % 2 == 0 ? Sex.Male : Sex.Female;
            string n = NameGen.Next(rng, sex, null, used);
            Assert.True(used.Contains(n));
        }
        Assert.Equal(5000, used.Count);
    }

    [Fact]
    public void Generate_MaleAndFemale_NamesVary()
    {
        var rng = new Rng(42);
        var males = new HashSet<string>();
        var females = new HashSet<string>();
        for (int i = 0; i < 200; i++)
        {
            males.Add(NameGen.Next(rng, Sex.Male));
            females.Add(NameGen.Next(rng, Sex.Female));
        }
        Assert.True(males.Count > 150, $"男名多样性不足: {males.Count}/200");
        Assert.True(females.Count > 150, $"女名多样性不足: {females.Count}/200");
    }

    [Fact]
    public void Child_InheritsFatherSurname()
    {
        var used = new HashSet<string> { "赵铁柱" };
        var rng = new Rng(7);
        string child = NameGen.Next(rng, Sex.Male, "赵", used);
        Assert.StartsWith("赵", child);
        Assert.NotEqual("赵铁柱", child); // 不与父亲重名
    }

    [Fact]
    public void RichRandom_FullGameSimulation_NoDuplicateAliveNames()
    {
        var game = Game.NewGame(20260908);
        for (int i = 0; i < 60 * Balance.MinutesPerDay; i++) game.Step(); // 60 天含出生/移民
        var alive = game.Villagers.Where(v => v.Alive).Select(v => v.Name).ToList();
        Assert.Equal(alive.Count, alive.Distinct().Count());
    }
}
