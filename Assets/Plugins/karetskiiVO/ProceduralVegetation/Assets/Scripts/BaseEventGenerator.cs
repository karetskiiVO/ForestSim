namespace ProceduralVegetation {
    public class BaseEventGenerator : Simulation.EventGenerator {
        public override Simulation.Event[] Generate(float currentTime) {
            return new Simulation.Event[] {
                new DeathEvent()                { time = currentTime + 0.01f },  // Remove dying trees first
                new GrowthEvent()               { time = currentTime + 0.50f },
                new ResourceDistributionEvent() { time = currentTime + 0.55f },
                new SeedingEvent()              { time = currentTime + 0.60f },
            };
        }
    }
}
