using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using ProceduralVegetation.Core;

using UnityEngine;

namespace ProceduralVegetation {
    // Описывает основные стратегии развития вида
    public abstract class TreeSpeciesDescriptor {
        public abstract void Grow(ref FoliageInstance instance);
        // TODO: remove allocations
        public abstract FoliageInstance[] Seed(ref FoliageInstance instance /* TODO: landscape */ );
        public virtual FoliageInstance[] ScaleSeedsByPopulation(
            FoliageInstance[] seeds,
            in FoliageInstance parentInstance
        ) {
            return seeds;
        }
        public abstract bool Alive(in FoliageInstance instance);
        public abstract void AddResources(ref FoliageInstance instance, float energy, float water, float light);
        public abstract FoliageInstance CreateSeed(Vector2 position);
    }

    public struct FoliageInstance {
        public enum FoliageType {
            Seed,
            Sapling,
            Mature,
            Dying,
        }

        public Vector2 position;
        public float age;
        public float stress;
        public float energy;
        public float strength;
        public FoliageType type;
    }

    public class Simulation {
        private struct SpeciesStats {
            public int total;
            public int seed;
            public int sapling;
            public int mature;
            public int dying;
        }

        public struct SimulationPoint {
            public FoliageInstance foliageInstance;

            public int speciesID;

            public SimulationPoint(
                Vector2 position,
                int speciesID,
                float age = 0,
                float stress = 0,
                float energy = 0,
                float strength = 0,
                FoliageInstance.FoliageType type = FoliageInstance.FoliageType.Seed
            ) {
                this.foliageInstance = new FoliageInstance() {
                    position = position,
                    age = age,
                    stress = stress,
                    energy = energy,
                    strength = strength,
                    type = type
                };
                this.speciesID = speciesID;
            }
        }

        public struct SimulationPointView {
            public Vector2 position => point.foliageInstance.position;
            public float age => point.foliageInstance.age;
            public float stress => point.foliageInstance.stress;
            public float energy => point.foliageInstance.energy;
            public float strength => point.foliageInstance.strength;
            public FoliageInstance.FoliageType type => point.foliageInstance.type;

            public TreeSpeciesDescriptor descriptor => simulation.simulationContext.speciesDescriptors[point.speciesID];

            private SimulationPoint point;
            private Simulation simulation;

            public SimulationPointView(SimulationPoint point, Simulation simulation) {
                this.point = point;
                this.simulation = simulation;
            }
        }

        public struct SimulationContext {
            public List<TreeSpeciesDescriptor> speciesDescriptors;
            public List<SimulationPoint> points;
            public Queue<SimulationPoint> deadPoints;
            public int deadPointsMaxSize;

            public BakedLandscape landscape;
            public LanscapeFruitfillness fruitfulness;
            public LanscapeWater water;
            public LanscapeLighting lighting;
        }

        public abstract class Event {
            public float time;

            public abstract void Execute(ref SimulationContext simulation);
        }

        public abstract class EventGenerator {
            public abstract Event[] Generate(float currentTime);
        }

        public Simulation AddSpecies(TreeSpeciesDescriptor descriptor, params Vector2[] position) {
            simulationContext.speciesDescriptors.Add(descriptor);
            simulationContext.points.Capacity += position.Length;
            var speciesID = simulationContext.speciesDescriptors.Count - 1;

            for (int i = 0; i < position.Length; i++) {
                var seed = descriptor.CreateSeed(position[i]);
                simulationContext.points.Add(new SimulationPoint(
                    seed.position,
                    speciesID,
                    seed.age,
                    seed.stress,
                    seed.energy,
                    seed.strength,
                    seed.type
                ));
            }

            return this;
        }

        public Simulation AddEventGenerator(EventGenerator generator) {
            eventGenerators.Add(generator);
            return this;
        }

        public Simulation GenerateWaterAuto(int iterations = 100, float dt = 0.1f, float waterScale = 1f, float rainRate = 0.002f) {
            simulationContext.water = new() {
                waterMap = LandscapeWaterGenerator.GenerateWaterMap(
                    simulationContext.landscape,
                    iterations,
                    dt,
                    rainRate
                ),
                waterScale = waterScale,
            };

            return this;
        }

        public Simulation SetLandscape(BakedLandscape landscape) {
            simulationContext.landscape = landscape;
            return this;
        }

        public Simulation SetFruitfulness(LanscapeFruitfillness fruitfulness) {
            simulationContext.fruitfulness = fruitfulness;
            return this;
        }

        public Simulation SetWater(LanscapeWater water) {
            simulationContext.water = water;
            return this;
        }

        public Simulation SetLighting(LanscapeLighting lighting) {
            simulationContext.lighting = lighting;
            return this;
        }

        public Simulation SetDeadPointsBufferCapacity(int maxSize) {
            if (maxSize <= 0) {
                throw new ArgumentOutOfRangeException(nameof(maxSize), "Dead points buffer capacity should be positive.");
            }

            simulationContext.deadPointsMaxSize = maxSize;
            return this;
        }

        public void Run(float simTime) {
            if (simulationContext.fruitfulness == null) {
                simulationContext.fruitfulness = new LanscapeFruitfillness() {
                    fruitfulnessMap = new Texture2D(1, 1, TextureFormat.RFloat, false) {
                        filterMode = FilterMode.Point,
                    },
                    fruitfulnessScale = 20f,
                };

                simulationContext.fruitfulness.fruitfulnessMap.SetPixelData(new float[] { 1f }, 0);
                simulationContext.fruitfulness.fruitfulnessMap.Apply();
            }

            for (float wholeYear = 0; wholeYear < simTime; wholeYear++) {
                var yearStartTime = currentTime + wholeYear;
                var yearEndTime = Mathf.Min(yearStartTime + 1f, currentTime + simTime);

                foreach (var generator in eventGenerators) {
                    var newEvents = generator.Generate(yearStartTime);
                    foreach (var e in newEvents) {
                        events.Enqueue(e, e.time + yearStartTime);
                    }
                }

                while (events.Count > 0 && events.Peek().time <= yearEndTime) {
                    if (!events.TryDequeue(out var simEvent, out var scheduledTime)) {
                        break;
                    }

                    if (scheduledTime < currentTime) {
                        Debug.LogWarning($"Event `{simEvent.GetType()}` was is in the past");
                    }

                    simEvent.time = scheduledTime;
                    simEvent.Execute(ref simulationContext);
                }

                LogSpeciesStats(yearStartTime);
            }

            currentTime += simTime;
        }

        public IEnumerable<SimulationPointView> GetPointsView() {
            foreach (var point in simulationContext.points) {
                yield return new SimulationPointView(point, this);
            }
        }

        public IEnumerable<SimulationPointView> GetDeadPointsView() {
            foreach (var point in simulationContext.deadPoints) {
                yield return new SimulationPointView(point, this);
            }
        }

        private void LogSpeciesStats(float year) {
            int speciesCount = simulationContext.speciesDescriptors.Count;
            var stats = new SpeciesStats[speciesCount];

            for (int i = 0; i < simulationContext.points.Count; i++) {
                var point = simulationContext.points[i];
                if (point.speciesID < 0 || point.speciesID >= speciesCount) {
                    continue;
                }

                stats[point.speciesID].total++;
                switch (point.foliageInstance.type) {
                    case FoliageInstance.FoliageType.Seed:
                        stats[point.speciesID].seed++;
                        break;
                    case FoliageInstance.FoliageType.Sapling:
                        stats[point.speciesID].sapling++;
                        break;
                    case FoliageInstance.FoliageType.Mature:
                        stats[point.speciesID].mature++;
                        break;
                    case FoliageInstance.FoliageType.Dying:
                        stats[point.speciesID].dying++;
                        break;
                }
            }

            var message = new StringBuilder();
            message.Append($"Year {year:0}: total={simulationContext.points.Count}");

            for (int i = 0; i < speciesCount; i++) {
                var descriptor = simulationContext.speciesDescriptors[i];
                var speciesName = descriptor != null ? descriptor.GetType().Name : "UnknownSpecies";
                var species = stats[i];
                message.Append($" | [{i}:{speciesName}] total={species.total}, seed={species.seed}, sapling={species.sapling}, mature={species.mature}, dying={species.dying}");
            }

            Debug.Log(message.ToString());
        }

        // TODO: make it possible to use multiple random generators with different seeds
        private static System.Random random = new(42);
        public static System.Random Random => random;
        private float currentTime;

        private PriorityQueue<Event, float> events = new();
        private List<EventGenerator> eventGenerators = new();
        // TODO: make private after debugging
        public SimulationContext simulationContext = new() {
            speciesDescriptors = new(),
            points = new(),
            deadPoints = new Queue<SimulationPoint>(),
            deadPointsMaxSize = 4096,
        };
    }
}
