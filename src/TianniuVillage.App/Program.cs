namespace TianniuVillage.App;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--smoke"))
        {
            RunSmoke();
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    private static void RunSmoke()
    {
        var manager = new GameManager();
        var init = System.Text.Json.JsonSerializer.Serialize(manager.BuildInit());
        Console.WriteLine($"init: {init.Length} bytes, head={init[..Math.Min(220, init.Length)]}...");
        for (int i = 0; i < 2000; i++) manager.Game.Step();
        var update = System.Text.Json.JsonSerializer.Serialize(manager.BuildUpdate());
        Console.WriteLine($"update after 2000 ticks: {update.Length} bytes");
        Console.WriteLine(update[..Math.Min(600, update.Length)]);
        Console.WriteLine("SMOKE OK");
    }
}