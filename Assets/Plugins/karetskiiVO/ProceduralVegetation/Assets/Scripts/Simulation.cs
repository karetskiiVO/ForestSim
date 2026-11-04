using System;
using System.Collections.Generic;
using System.Linq;

using ProceduralVegetation.Core;

using UnityEngine;

namespace ProceduralVegetation {
    // Описывает основные стратегии развития вида
    public abstract class TreeSpeciesDescriptor {
        public abstract void Grow(ref FoliageInstance instance);
        // TODO: remove allocations
        public abstract FoliageInstance[] Seed(ref FoliageInstance instance /* TODO: landscape */ );
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

            public TreeSpeciesDescriptor descriptor => speciesDescriptors[point.speciesID];

            private SimulationPoint point;
            private List<TreeSpeciesDescriptor> speciesDescriptors;

            public SimulationPointView(SimulationPoint point, Simulation simulation) {
                this.point = point;
                speciesDescriptors = simulation.simulationContext.speciesDescriptors;
            }

            public SimulationPointView(SimulationPoint point, List<TreeSpeciesDescriptor> speciesDescriptors) {
                this.point = point;
                this.speciesDescriptors = speciesDescriptors;
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

            public readonly IEnumerable<SimulationPointView> pointsView {
                get {
                    var speciesDescriptors = this.speciesDescriptors;
                    return points.Select(point => new SimulationPointView(point, speciesDescriptors));
                }
            }
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

        // TODO: make it possible to use multiple random generators with different seeds
        private static System.Random random = new(42);
        public static System.Random Random => random;

        // Set the global random seed used by simulation for deterministic runs.
        public static void SetRandomSeed(int seed) {
            random = new System.Random(seed);
        }
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
