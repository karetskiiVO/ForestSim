using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ProceduralVegetation.Core;

using UnityEngine;

namespace ProceduralVegetation {
    // Описывает основные стратегии развития вида
    public abstract class TreeSpeciesDescriptor {
        public abstract void Grow(ref FoliageInstance instance);
        // TODO: remove allocations
        public abstract FoliageInstance[] Seed(ref FoliageInstance instance /* TODO: landscape */ );
        public abstract bool Alive(in FoliageInstance instance);
        public abstract void AddResources(ref FoliageInstance instance, float energy, float water);
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
                float strength = 0
            ) {
                this.foliageInstance = new FoliageInstance() {
                    position = position,
                    age = age,
                    stress = stress,
                    energy = energy,
                    strength = strength,
                    type = FoliageInstance.FoliageType.Seed
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

            public BakedLandscape landscape;
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
                simulationContext.points.Add(new(position[i], speciesID));
            }

            return this;
        }

        public Simulation AddEventGenerator(EventGenerator generator) {
            eventGenerators.Add(generator);
            return this;
        }

        public Simulation SetLandscape(BakedLandscape landscape) {
            simulationContext.landscape = landscape;
            return this;
        }

        public void Run(float simTime) {
            float currentTime = 0;
            for (float wholeYear = 0; wholeYear < simTime; wholeYear++) {
                Debug.Log($"Simulating year {wholeYear}: Tree num: {simulationContext.points.Count}");
                foreach (var generator in eventGenerators) {
                    var newEvents = generator.Generate(wholeYear);
                    foreach (var e in newEvents) {
                        events.Enqueue(e, e.time);
                    }
                }
                currentTime = wholeYear;

                while (events.Count > 0 && events.Peek().time <= wholeYear + 1f) {
                    var simEvent = events.Dequeue();
                    if (simEvent.time < currentTime) {
                        Debug.LogWarning($"Event `{simEvent.GetType()}` was is in the past");
                    }
                    simEvent.Execute(ref simulationContext);
                }
            }
        }

        public IEnumerable<SimulationPointView> GetPointsView() {
            foreach (var point in simulationContext.points) {
                yield return new SimulationPointView(point, this);
            }
        }

        // TODO: make it possible to use multiple random generators with different seeds
        private static System.Random random = new(42);
        public static System.Random Random => random;

        private PriorityQueue<Event, float> events = new();
        private List<EventGenerator> eventGenerators = new();
        private SimulationContext simulationContext = new() {
            speciesDescriptors = new(),
            points = new(),
        };
    }
}
