using System.Text.Json;
using TianniuVillage.Core;

namespace TianniuVillage.App;

public sealed class CommandProcessor
{
    private readonly GameManager _manager;
    private readonly Action<string> _postInit;
    private readonly Action<string> _postMessage;
    private long _lastJsErrorLogTicks;

    public CommandProcessor(GameManager manager, Action<string> postInit, Action<string> postMessage)
    {
        _manager = manager;
        _postInit = postInit;
        _postMessage = postMessage;
    }

    public void Handle(string json)
    {
        string? jsError = null;
        lock (_manager.SyncRoot)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                string type = root.GetProperty("type").GetString() ?? "";

                switch (type)
                {
                    case "jserror":
                        jsError = root.TryGetProperty("msg", out var m) ? m.GetString() : "?";
                        _manager.Game.Log("[系统] " + jsError, LogSeverity.Debug);
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
                    case "overview":
                        _postMessage(JsonSerializer.Serialize(_manager.BuildOverview()));
                        break;
                }
            }
            catch (Exception ex)
            {
                _manager.Game?.Log($"系统异常：{ex.Message}", LogSeverity.Debug);
            }
        }

        if (jsError != null)
        {
            long now = DateTime.UtcNow.Ticks;
            if (now - _lastJsErrorLogTicks >= TimeSpan.TicksPerSecond)
            {
                _lastJsErrorLogTicks = now;
                try
                {
                    var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TianniuVillage");
                    Directory.CreateDirectory(dir);
                    File.AppendAllText(Path.Combine(dir, "jserrors.log"),
                        $"{DateTime.Now:HH:mm:ss} " + jsError + Environment.NewLine);
                }
                catch { }
            }
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
