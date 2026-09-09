namespace TianniuVillage.Core;

public sealed partial class Game
{
    private void RollWeather()
    {
        Weather = Season switch
        {
            Season.Spring => Rng.PickWeighted([
                (Weather.Sunny, 5f), (Weather.Cloudy, 3f), (Weather.Rain, 2f), (Weather.Fog, 1f)]),
            Season.Summer => Rng.PickWeighted([
                (Weather.Sunny, 6f), (Weather.Cloudy, 2f), (Weather.Rain, 2f), (Weather.Storm, 1f)]),
            Season.Autumn => Rng.PickWeighted([
                (Weather.Sunny, 5f), (Weather.Cloudy, 3f), (Weather.Rain, 2f), (Weather.Fog, 1f)]),
            _ => Rng.PickWeighted([
                (Weather.Sunny, 4f), (Weather.Cloudy, 3f), (Weather.Snow, 3f), (Weather.Fog, 1f)])
        };
        WeatherTicksLeft = 360;
        if (Weather == Weather.Storm)
            Log("乌云压顶，暴风雨来了，村民们纷纷躲进屋里", LogSeverity.Important);
        if (Weather == Weather.Fog)
            Log("晨雾弥漫，能见度很低", LogSeverity.Normal);
    }

    private void UpdateWeather()
    {
        if (WeatherTicksLeft > 0) WeatherTicksLeft--;
        else RollWeather();
    }

    private int _animalMoveTick;
    private int _wolfTick;

    private void UpdateAnimals()
    {
        _animalMoveTick++;
        if (_animalMoveTick % 3 != 0) return;
        foreach (var animal in World.Animals)
        {
            var threat = Villagers.FirstOrDefault(v => v.Alive && Dist2(v.Pos, (animal.X, animal.Y)) < 16);
            int dx = 0, dy = 0;
            if (threat != null)
            {
                dx = Math.Sign(animal.X - threat.Pos.x);
                dy = Math.Sign(animal.Y - threat.Pos.y);
            }
            else if (Rng.Chance(0.3f))
            {
                dx = Rng.Next(-1, 2);
                dy = Rng.Next(-1, 2);
            }
            int nx = animal.X + dx, ny = animal.Y + dy;
            var terrain = World.Map.InBounds(nx, ny) ? World.Map.Get(nx, ny) : Terrain.DeepWater;
            if (terrain is Terrain.Grass or Terrain.Forest or Terrain.Sand or Terrain.Highland)
                (animal.X, animal.Y) = (nx, ny);
        }
        UpdateWolves();
    }

    private void UpdateWolves()
    {
        _wolfTick++;
        if (_wolfTick % 10 != 0) return;

        if (Season == Season.Winter && IsNight && Rng.Chance(0.005f))
        {
            var target = Villagers.Where(v => v.Alive && v.Stage == AgeStage.Adult && v.Injury == InjuryType.None).ToList();
            if (target.Count > 0)
            {
                var victim = target[Rng.Next(target.Count)];
                var huntersNearby = Villagers.Count(v => v.Alive && v.SkillOf("hunting") > 30 && Dist2(v.Pos, victim.Pos) < 36);
                bool hasArmor = World.CountItem("armor") > 0;

                if (hasArmor || huntersNearby >= 2)
                {
                    World.TryTakeItem("armor", 1);
                    Log($"🐺 狼群在夜色中窥视，但被村民的防御击退了", LogSeverity.Normal);
                }
                else
                {
                    victim.Injury = InjuryType.Cut;
                    victim.InjuryDaysLeft = Rng.NextFloat(5, 12);
                    victim.Health -= 20;
                    victim.AddMood("被狼袭击", -12f, 96f);
                    Log($"🐺 狼群袭击了落单的{victim.Name}！", LogSeverity.Important);
                    if (victim.Health <= 0) Kill(victim, DeathCause.Accident, "惨死于狼牙之下");
                }
            }
        }
    }
}
