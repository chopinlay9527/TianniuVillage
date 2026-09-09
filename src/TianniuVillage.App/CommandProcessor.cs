using System.Text.Json;
using TianniuVillage.Core;

namespace TianniuVillage.App;

public sealed class CommandProcessor
{
    private readonly GameManager _manager;
    private readonly Action<string> _postInit;
    private readonly Action<string> _postLog;

    public CommandProcessor(GameManager manager, Action<string> postInit, Action<string> postLog)
    {
        _manager = manager;
        _postInit = postInit;
        _postLog = postLog;
    }

    public void Handle(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string type = root.GetProperty("type").GetString() ?? "";

            switch (type)
            {
                case "jserror":
                    _manager.Game.Log("[系统] " + (root.TryGetProperty("msg", out var m) ? m.GetString() : "?"), LogSeverity.Debug);
                    break;
                case "ready":
                    _postInit(JsonSerializer.Serialize(_manager.BuildInit()));
                    break;
                case "speed":
                    _manager.Speed = root.GetProperty("value").GetSingle();
                    break;
                case "newgame":
                {
                    int seed = root.TryGetProperty("seed", out var s) ? s.GetInt32() : Random.Shared.Next(1, int.MaxValue);
                    _manager.NewGame(seed);
                    _postInit(JsonSerializer.Serialize(_manager.BuildInit()));
                    break;
                }
                case "save":
                    _manager.Save(root.TryGetProperty("name", out var n) ? n.GetString() : null);
                    _manager.Game.Log("村庄已存档", LogSeverity.Normal);
                    break;
                case "load":
                    if (_manager.LoadLatest() != null)
                        _postInit(JsonSerializer.Serialize(_manager.BuildInit()));
                    break;
                case "god":
                    HandleGod(root.GetProperty("action").GetString() ?? "");
                    break;
            }
        }
        catch (Exception ex)
        {
            _manager.Game?.Log($"系统异常：{ex.Message}", LogSeverity.Debug);
        }
    }

    private void HandleGod(string action)
    {
        switch (action)
        {
            case "food": _manager.Game.GodSpawnResource(); break;
            case "heal": _manager.Game.GodHealAll(); break;
            case "harvest": _manager.Game.GodBlessHarvest(); break;
            case "fire": _manager.Game.GodFire(); break;
            case "plague": _manager.Game.GodPlague(); break;
            case "storm": _manager.Game.GodStorm(); break;
            case "drought": _manager.Game.GodDrought(); break;
            case "wanderer": _manager.Game.GodSpawnVillager(); break;
        }
    }
}
