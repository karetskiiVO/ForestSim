namespace ProceduralVegetation {
    public class BaseEventGenerator : Simulation.EventGenerator {
        public override Simulation.Event[] Generate(float currentTime) {
            return new Simulation.Event[] {
                new GrowthEvent()               { time = currentTime + 0.50f },
                new ResourceDistributionEvent() { time = currentTime + 0.55f },
                new SeedingEvent()              { time = currentTime + 0.60f },
                new DeathEvent()                { time = currentTime + 0.99f },
            };
        }
    }
}
