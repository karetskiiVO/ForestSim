using System.Linq;

using ProceduralVegetation.Utilities;

using Sirenix.Utilities;

using UnityEngine;

using static ProceduralVegetation.Simulation;

namespace ProceduralVegetation {
    public abstract class TreeSpeciesCountDescriptor : TreeSpeciesDescriptor {
        public void ResetCount() { count = 0; }

        public void HandleInstance(ref FoliageInstance instance) {
            if (instance.type == FoliageInstance.FoliageType.Dying) return;

            count++;
        }

        int count = 0;
        public int Count => count;

        public class TreeSpeciesCounterEventGenerator : EventGenerator {
            public override ProceduralVegetation.Simulation.Event[] Generate(float currentTime) {
                return new ProceduralVegetation.Simulation.Event[] {
                    new TreeSpeciesCounterEvent(){ time = 0f },
                };
            }

            public class TreeSpeciesCounterEvent : ProceduralVegetation.Simulation.Event {
                public override void Execute(ref SimulationContext context) {
                    for (int i = 0; i < context.points.Count; i++) {
                        var descriptor = context.speciesDescriptors[context.points[i].speciesID];

                        if (descriptor is not TreeSpeciesCountDescriptor) continue;
                        var point = context.points[i];
                        (descriptor as TreeSpeciesCountDescriptor).HandleInstance(ref point.foliageInstance);
                        context.points[i] = point;
                    }
                }
            }
        }
    }

    public class RuntimeSpeciesDescriptor : TreeSpeciesCountDescriptor {
        protected float energyStressWeight = 1f;
        protected float requiredEnergy = 1f;
        protected float waterStressWeight = 1f;
        protected float requiredWater = 1f;
        // protected float lightStressWeight = 1f;
        // protected float requiredLight = 1f;

        protected float acceptableSeedStress = 0.5f;
        protected float acceptableSaplingStress = 0.7f;
        protected float acceptableMatureStress = 0.9f;

        protected float seedEnergy = 1f;

        protected float saplingStartAge = 1f;
        protected float matureStartAge = 5f;

        protected float seedSpreadRadius = 1000f;
        protected int seedSpreadCount = 5;

        protected float seedStrength = 0.2f;
        protected float maxStrength = 2f;
        protected float energyConversation = 0.5f;

        public override void AddResources(ref FoliageInstance instance, float energy, float water, float light) {
            instance.energy += energy;
            instance.stress += energyStressWeight * Mathf.Max(0f, requiredEnergy - energy);
            instance.stress += waterStressWeight * Mathf.Max(0f, requiredWater - water);
            // instance.stress += lightStressWeight * Mathf.Max(0f, requiredLight - light);
        }

        public override bool Alive(in FoliageInstance instance) {
            return instance.energy > 0f &&
                instance.type switch {
                    FoliageInstance.FoliageType.Seed => instance.stress < acceptableSeedStress,
                    FoliageInstance.FoliageType.Sapling => instance.stress < acceptableSaplingStress,
                    FoliageInstance.FoliageType.Mature => instance.stress < acceptableMatureStress,
                    _ => false,
                };
        }

        public override FoliageInstance CreateSeed(Vector2 position) {
            return new() {
                type = FoliageInstance.FoliageType.Seed,
                position = position,
                energy = seedEnergy,
                strength = seedStrength,
                stress = 0f,
            };
        }

        private void UpdateType(ref FoliageInstance instance) {
            if (instance.age < saplingStartAge) instance.type = FoliageInstance.FoliageType.Seed;
            else if (instance.age < matureStartAge) instance.type = FoliageInstance.FoliageType.Sapling;
            else instance.type = FoliageInstance.FoliageType.Mature;
        }

        public override void Grow(ref FoliageInstance instance) {
            instance.age += 1f;
            UpdateType(ref instance);

            instance.strength += energyConversation * instance.energy;
        }

        public override FoliageInstance[] Seed(ref FoliageInstance instance) {
            var seeds = new FoliageInstance[instance.type == FoliageInstance.FoliageType.Mature ? seedSpreadCount : 0];

            for (int i = 0; i < seeds.Length; i++) {
                seeds[i] = CreateSeed(
                    Simulation.Random.NextGaussian(seedSpreadRadius, instance.position)
                );
            }

            return seeds;
        }
    }

    public class OakDescriptor : RuntimeSpeciesDescriptor {
        public OakDescriptor() {
            energyStressWeight = 1.2f;
            requiredEnergy = 1.5f;
            waterStressWeight = 1.1f;
            requiredWater = 1.2f;

            acceptableSeedStress = 0.45f;
            acceptableSaplingStress = 0.65f;
            acceptableMatureStress = 0.85f;

            seedEnergy = 1.4f;
            saplingStartAge = 2f;
            matureStartAge = 10f;
            seedSpreadRadius = 450f;
            seedSpreadCount = 3;
        }
    }

    public class PineDescriptor : RuntimeSpeciesDescriptor {
        public PineDescriptor() {
            energyStressWeight = 1.0f;
            requiredEnergy = 1.2f;
            waterStressWeight = 0.9f;
            requiredWater = 1.0f;

            acceptableSeedStress = 0.55f;
            acceptableSaplingStress = 0.75f;
            acceptableMatureStress = 0.9f;

            seedEnergy = 1.1f;
            saplingStartAge = 1.5f;
            matureStartAge = 8f;
            seedSpreadRadius = 600f;
            seedSpreadCount = 6;
        }
    }

    public class BirchDescriptor : RuntimeSpeciesDescriptor {
        public BirchDescriptor() {
            energyStressWeight = 1.1f;
            requiredEnergy = 1.1f;
            waterStressWeight = 1.3f;
            requiredWater = 1.3f;

            acceptableSeedStress = 0.5f;
            acceptableSaplingStress = 0.7f;
            acceptableMatureStress = 0.9f;

            seedEnergy = 1.15f;
            saplingStartAge = 1f;
            matureStartAge = 6f;
            seedSpreadRadius = 550f;
            seedSpreadCount = 5;
        }
    }

    public class SpruceDescriptor : RuntimeSpeciesDescriptor {
        public SpruceDescriptor() {
            energyStressWeight = 1.1f;
            requiredEnergy = 1.3f;
            waterStressWeight = 1.0f;
            requiredWater = 1.1f;

            acceptableSeedStress = 0.5f;
            acceptableSaplingStress = 0.7f;
            acceptableMatureStress = 0.88f;

            seedEnergy = 1.2f;
            saplingStartAge = 1.8f;
            matureStartAge = 9f;
            seedSpreadRadius = 620f;
            seedSpreadCount = 5;
        }
    }

    public class LindenDescriptor : RuntimeSpeciesDescriptor {
        public LindenDescriptor() {
            energyStressWeight = 1.3f;
            requiredEnergy = 1.4f;
            waterStressWeight = 1.2f;
            requiredWater = 1.4f;

            acceptableSeedStress = 0.48f;
            acceptableSaplingStress = 0.68f;
            acceptableMatureStress = 0.88f;

            seedEnergy = 1.25f;
            saplingStartAge = 2.5f;
            matureStartAge = 10f;
            seedSpreadRadius = 520f;
            seedSpreadCount = 4;
        }
    }

    public class BushDescriptor : RuntimeSpeciesDescriptor {
        public BushDescriptor() {
            energyStressWeight = 0.9f;
            requiredEnergy = 0.9f;
            waterStressWeight = 1.0f;
            requiredWater = 0.95f;

            acceptableSeedStress = 0.6f;
            acceptableSaplingStress = 0.8f;
            acceptableMatureStress = 0.95f;

            seedEnergy = 1.0f;
            saplingStartAge = 0.8f;
            matureStartAge = 4f;
            seedSpreadRadius = 400f;
            seedSpreadCount = 8;
        }
    }
}
