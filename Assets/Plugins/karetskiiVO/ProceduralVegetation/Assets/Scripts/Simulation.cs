using System;
using System.Collections.Generic;

using ProceduralVegetation.Core;

using UnityEngine;

namespace ProceduralVegetation {
    // Описывает основные стратегии развития вида
    public abstract class TreeSpeciesDescriptor {
        public abstract void Grow(ref FoliageInstance instance, float deltaTime);
    }

    public struct FoliageInstance {
        public Vector2 position;
        public float age;
        public float stress;
        public float energy;
        public float growth;
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
                float growth = 0
            ) {
                this.foliageInstance = new FoliageInstance() {
                    position = position,
                    age = age,
                    stress = stress,
                    energy = energy,
                    growth = growth
                };
                this.speciesID = speciesID;
            }
        }
        public struct SimulationContext {
            public List<TreeSpeciesDescriptor> speciesDescriptors;
            public List<SimulationPoint> points;
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

        public void Run(float simTime) {
            float currentTime = 0;
            for (float wholeYear = 0; wholeYear < simTime; wholeYear++) {
                Debug.Log($"Simulating year {wholeYear}");
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

        private PriorityQueue<Event, float> events = new();
        private List<EventGenerator> eventGenerators = new();
        private SimulationContext simulationContext = new() {
            speciesDescriptors = new(),
            points = new(),
        };
    }
}
