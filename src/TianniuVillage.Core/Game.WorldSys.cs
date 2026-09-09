namespace TianniuVillage.Core;

public sealed partial class Game
{
    private void RollWeather()
    {
        Weather = Season switch
        {
            Season.Spring => Rng.PickWeighted([
                (Weather.Sunny, 5f), (Weather.Cloudy, 3f), (Weather.Rain, 2f)]),
            Season.Summer => Rng.PickWeighted([
                (Weather.Sunny, 6f), (Weather.Cloudy, 2f), (Weather.Rain, 2f), (Weather.Storm, 1f)]),
            Season.Autumn => Rng.PickWeighted([
                (Weather.Sunny, 5f), (Weather.Cloudy, 3f), (Weather.Rain, 2f)]),
            _ => Rng.PickWeighted([
                (Weather.Sunny, 4f), (Weather.Cloudy, 3f), (Weather.Snow, 3f)])
        };
        WeatherTicksLeft = 360;
        if (Weather == Weather.Storm)
            Log("乌云压顶，暴风雨来了，村民们纷纷躲进屋里", LogSeverity.Important);
    }

    private void UpdateWeather()
    {
        if (WeatherTicksLeft > 0) WeatherTicksLeft--;
        else RollWeather();
    }

    private int _animalMoveTick;

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
    }
}
