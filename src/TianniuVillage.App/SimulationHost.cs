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
        double acc = 0;
        const double secondsPerTick = 1.0 / Balance.TicksPerSecond;

        while (_running)
        {
            Thread.Sleep(2);
            long now = stopwatch.ElapsedTicks;
            double elapsed = (now - lastTicks) / (double)Stopwatch.Frequency;
            lastTicks = now;
            double speed = _manager.Speed;
            if (speed <= 0.01f)
            {
                _wakeup.WaitOne(100);
                lastTicks = stopwatch.ElapsedTicks;
                carry = 0;
                continue;
            }
            carry += elapsed * speed;
            int steps = (int)(carry / secondsPerTick);
            if (steps <= 0) continue;
            carry -= steps * secondsPerTick;
            steps = Math.Min(steps, Balance.TicksPerSecond * 20);

            bool doUpdate = false;
            for (int i = 0; i < steps; i++)
            {
                _manager.Game.Step();
                if (_manager.ShouldAutosave())
                {
                    _manager.Save();
                }
                acc += secondsPerTick;
                if (acc >= 0.1)
                {
                    acc = 0;
                    doUpdate = true;
                }
            }

            if (doUpdate)
            {
                string json = JsonSerializer.Serialize(_manager.BuildUpdate());
                UpdateReady?.Invoke(json);
            }
        }
    }

    public void Wake() => _wakeup.Set();

    public void Dispose()
    {
        Stop();
        _wakeup.Dispose();
    }
}
