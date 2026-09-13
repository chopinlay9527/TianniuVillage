using System.Text.Json;
using TianniuVillage.Core;

public class OverviewTests
{
    [Fact]
    public void BuildOverview_ProducesSerializablePayload()
    {
        var game = Game.NewGame(424242);
        for (int i = 0; i < 3000; i++) game.Step();

        var overview = game.BuildOverview();
        string json = JsonSerializer.Serialize(overview);

        Assert.False(string.IsNullOrWhiteSpace(json));
        Assert.Contains("\"type\":\"overview\"", json);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(game.Tick, root.GetProperty("tick").GetInt32());
        Assert.NotEmpty(root.GetProperty("villagers").EnumerateArray());
        Assert.Equal("甜牛村", root.GetProperty("village").GetProperty("name").GetString());
        Assert.True(root.GetProperty("items").EnumerateArray().Count() >= 20,
            "物资明细应覆盖全部物品种类");
        Assert.True(root.GetProperty("housing").GetProperty("beds").GetInt32() >= 0);
        Assert.True(root.GetProperty("tech").GetProperty("total").GetInt32() > 0,
            "科技总数应报告完整科技树");
    }
}