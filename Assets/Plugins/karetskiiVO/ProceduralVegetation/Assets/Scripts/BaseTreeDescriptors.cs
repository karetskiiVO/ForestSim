using System;

using ProceduralVegetation.Utilities;

using UnityEngine;

namespace ProceduralVegetation {
    public class OakDescriptor : TreeSpeciesDescriptor {
        public override void AddResources(ref FoliageInstance instance, float energy, float water) {
            if (instance.type == FoliageInstance.FoliageType.Dying || instance.type == FoliageInstance.FoliageType.Seed) return;

            instance.energy += energy * 0.8f;
            instance.stress += water < 0.5f ? (0.5f - water) * 0.5f : 0;
        }

        public override bool Alive(in FoliageInstance instance) {
            return instance.type switch {
                FoliageInstance.FoliageType.Seed => instance.energy > 0f,
                FoliageInstance.FoliageType.Sapling => instance.energy > 0.0f,
                FoliageInstance.FoliageType.Mature => instance.energy > 0.0f && !Simulation.Random.Chance(instance.stress * 0.01f),
                _ => false,
            };
        }

        public override FoliageInstance CreateSeed(Vector2 position) {
            return new FoliageInstance() {
                position = position,
                age = 0,
                energy = 1f,
                stress = 0,
                strength = 0,
                type = FoliageInstance.FoliageType.Seed
            };
        }

        public override void Grow(ref FoliageInstance instance) {
            switch (instance.type) {
                case FoliageInstance.FoliageType.Seed:

                    instance.energy -= 0.1f;

                    if (Simulation.Random.Chance(0.5f)) {
                        instance.type = FoliageInstance.FoliageType.Sapling;
                        instance.strength = 0.3f;
                    }

                    break;
                case FoliageInstance.FoliageType.Sapling:
                    instance.age += 1f;
                    instance.energy = Mathf.Max(0f, instance.energy - instance.strength * 0.05f);
                    instance.strength = Mathf.Min(instance.strength + instance.energy * 0.6f * 0.15f, 3f);
                    instance.energy -= instance.energy * 0.6f;

                    if (instance.age > 5f) {
                        instance.type = FoliageInstance.FoliageType.Mature;
                    }
                    break;
                case FoliageInstance.FoliageType.Mature:
                    instance.age += 1f;
                    instance.energy = Mathf.Max(0f, instance.energy - instance.strength * 0.05f);
                    instance.strength = Mathf.Min(instance.strength + instance.energy * 0.4f * 0.15f, 3f);
                    instance.energy -= instance.energy * 0.4f;
                    break;
                case FoliageInstance.FoliageType.Dying:
                    break;
            }
        }

        /// <summary>Maximum seeds one tree can produce per year regardless of energy surplus.</summary>
        private const int MaxSeedsPerYear = 3;

        /// <summary>
        /// Fraction of seeds that disperse far (e.g. carried by animals or wind).
        /// The rest fall locally near the parent.
        /// </summary>
        private const float LongDistanceFraction = 0.15f;
        private const float LocalSigma = 50f;
        private const float LongDistanceSigma = 250f;

        public override FoliageInstance[] Seed(ref FoliageInstance instance) {
            if (instance.type != FoliageInstance.FoliageType.Mature) return Array.Empty<FoliageInstance>();
            if (instance.stress > 0.7f || instance.energy < 0.3f) return Array.Empty<FoliageInstance>();

            int minSeeds = Mathf.FloorToInt(instance.energy / 0.2f);
            int maxSeeds = Mathf.FloorToInt(instance.energy / 0.1f);
            int seedCount = Mathf.Min(Simulation.Random.Next(minSeeds, maxSeeds + 1), MaxSeedsPerYear);
            instance.energy -= seedCount * 0.1f;

            var seeds = new FoliageInstance[seedCount];
            for (var i = 0; i < seedCount; i++) {
                float sigma = Simulation.Random.Chance(LongDistanceFraction)
                    ? LongDistanceSigma
                    : LocalSigma;
                seeds[i] = CreateSeed(Simulation.Random.NextGaussian(sigma, instance.position));
            }
            return seeds;
        }
    }
}
