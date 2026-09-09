using TianniuVillage.Core;

namespace TianniuVillage.App;

public sealed class GameManager
{
    public Game Game { get; private set; } = Game.NewGame(20260908);
    public float Speed { get; set; } = 1f;
    public int LastLogSeq;
    public int PendingSaveDays;
    public event Action<string>? OnStatus;

    public static string SaveDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TianniuVillage", "saves");

    public string AutosavePath => Path.Combine(SaveDir, "autosave.json");

    public void NewGame(int seed)
    {
        Game = Game.NewGame(seed);
        LastLogSeq = 0;
        OnStatus?.Invoke($"新世界已创造（种子 {seed}）");
    }

    public void Save(string? name = null)
    {
        try
        {
            string path = string.IsNullOrEmpty(name)
                ? AutosavePath
                : Path.Combine(SaveDir, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            SaveService.Save(Game, path);
            OnStatus?.Invoke($"已存档：{Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            OnStatus?.Invoke($"存档失败：{ex.Message}");
        }
    }

    public string? LoadLatest()
    {
        try
        {
            string? latest = null;
            DateTime latestTime = DateTime.MinValue;
            if (Directory.Exists(SaveDir))
            {
                foreach (var f in Directory.EnumerateFiles(SaveDir, "*.json"))
                {
                    var t = File.GetLastWriteTime(f);
                    if (t > latestTime) { latestTime = t; latest = f; }
                }
            }
            if (latest == null)
            {
                OnStatus?.Invoke("没有找到任何存档");
                return null;
            }
            Game = SaveService.Load(latest);
            LastLogSeq = 0;
            OnStatus?.Invoke($"已读取存档：{Path.GetFileName(latest)}");
            return latest;
        }
        catch (Exception ex)
        {
            OnStatus?.Invoke($"读档失败：{ex.Message}");
            return null;
        }
    }

    public bool ShouldAutosave()
    {
        if (Game.Day - PendingSaveDays >= Balance.AutosaveIntervalGameDays)
        {
            PendingSaveDays = Game.Day;
            return true;
        }
        return false;
    }

    public object BuildInit() => Game.BuildInit();

    public object BuildUpdate()
    {
        var update = Game.BuildUpdate(LastLogSeq);
        if (update is not null && Game.Logs.Count > 0)
            LastLogSeq = Math.Max(LastLogSeq, Game.Logs[^1].Seq);
        return update;
    }
}
