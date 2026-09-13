using System.Diagnostics;
using System.Text.Json;
using TianniuVillage.Core;

namespace TianniuVillage.App;

public sealed class SimulationHost : IDisposable
{
    private readonly GameManager _manager;
    private Thread? _thread;
    private volatile bool _running;
    private readonly AutoResetEvent _wakeup = new(false);

    public event Action<string>? UpdateReady;

    public SimulationHost(GameManager manager)
    {
        _manager = manager;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _thread = new Thread(SimLoop) { IsBackground = true, Name = "SimLoop" };
        _thread.Start();
    }

    public void Stop() => _running = false;

    private void SimLoop()
    {
        var stopwatch = Stopwatch.StartNew();
        long lastTicks = stopwatch.ElapsedTicks;
        double carry = 0;
        long nextUpdateTicks = stopwatch.ElapsedTicks;
        const double secondsPerTick = 1.0 / Balance.TicksPerSecond;
        long updateIntervalTicks = (long)(0.1 * Stopwatch.Frequency);

        while (_running)
        {
            Thread.Sleep(2);
            long now = stopwatch.ElapsedTicks;
            double elapsed = (now - lastTicks) / (double)Stopwatch.Frequency;
            lastTicks = now;
            double speed = _manager.Speed;
            if (speed <= 0.01f)
            {
                try { _wakeup.WaitOne(100); }
                catch (ObjectDisposedException) { break; }
                lastTicks = stopwatch.ElapsedTicks;
                carry = 0;
                continue;
            }
            carry += elapsed * speed;
            int steps = (int)(carry / secondsPerTick);
            if (steps <= 0) continue;
            carry -= steps * secondsPerTick;
            steps = Math.Min(steps, Balance.TicksPerSecond * 20);

            string? updateJson = null;
            string? saveJson = null;
            lock (_manager.SyncRoot)
            {
                for (int i = 0; i < steps; i++)
                {
                    _manager.Game.Step();
                    if (_manager.ShouldAutosave())
                        saveJson = SaveService.Serialize(_manager.Game);
                }

                if (now >= nextUpdateTicks)
                {
                    nextUpdateTicks = now + updateIntervalTicks;
                    updateJson = JsonSerializer.Serialize(_manager.BuildUpdate());
                }
            }

            if (saveJson != null)
                File.WriteAllText(_manager.AutosavePath, saveJson);
            if (updateJson != null) UpdateReady?.Invoke(updateJson);
        }
    }

    public void Wake() => _wakeup.Set();

    public void Dispose()
    {
        Stop();
        if (_thread != null && _thread.IsAlive)
            _thread.Join(1000);
        _wakeup.Dispose();
    }
}
