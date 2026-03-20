using System;

using ProceduralVegetation.Utilities;

using UnityEngine;

namespace ProceduralVegetation {
    public class OakDescriptor : TreeSpeciesDescriptor {
        // Lifecycle tuning parameters.
        public float MatureStressDeathFactor = 0.003f;
        public float MatureAgeResilienceMin = 0.55f;
        public float MatureStrengthResilienceMin = 0.7f;
        public float MatureAgeForMaxResilience = 45f;
        public float MatureMaintenanceMin = 0.72f;
        public float MatureAgeForMaintenanceMin = 50f;
        public float MatureStressRecoveryPerYear = 0.05f;

        public override void AddResources(ref FoliageInstance instance, float energy, float water, float light) {
            if (instance.type == FoliageInstance.FoliageType.Dying || instance.type == FoliageInstance.FoliageType.Seed) return;

            instance.energy += energy * 0.8f + light * 0.2f;
            instance.stress += water < 0.5f ? (0.5f - water) * 0.5f : 0;
        }

        public override bool Alive(in FoliageInstance instance) {
            switch (instance.type) {
                case FoliageInstance.FoliageType.Seed:
                    return instance.energy > 0f;
                case FoliageInstance.FoliageType.Sapling:
                    return instance.energy > 0.0f;
                case FoliageInstance.FoliageType.Mature:
                    if (instance.energy <= 0.0f) {
                        return false;
                    }

                    // Old and strong trees better tolerate drought stress.
                    float ageResilience = Mathf.Lerp(1f, MatureAgeResilienceMin, Mathf.Clamp01(instance.age / MatureAgeForMaxResilience));
                    float strengthResilience = Mathf.Lerp(1f, MatureStrengthResilienceMin, Mathf.Clamp01(instance.strength / 3f));
                    float stressDeathChance = Mathf.Clamp01(instance.stress * MatureStressDeathFactor * ageResilience * strengthResilience);

                    return !Simulation.Random.Chance(stressDeathChance);
                default:
                    return false;
            }
        }

        public override FoliageInstance CreateSeed(Vector2 position) {
            return new FoliageInstance() {
                position = position,
                age = 0,
                energy = 1.2f,
                stress = 0,
                strength = 0,
                type = FoliageInstance.FoliageType.Seed
            };
        }

        public override void Grow(ref FoliageInstance instance) {
            switch (instance.type) {
                case FoliageInstance.FoliageType.Seed:

                    instance.energy -= 0.02f;

                    if (Simulation.Random.Chance(0.68f)) {
                        instance.type = FoliageInstance.FoliageType.Sapling;
                        instance.strength = 0.25f;
                    }

                    break;
                case FoliageInstance.FoliageType.Sapling:
                    instance.age += 1f;
                    instance.energy = Mathf.Max(0f, instance.energy - instance.strength * 0.03f);
                    instance.strength = Mathf.Min(instance.strength + instance.energy * 0.6f * 0.12f, 3f);
                    instance.energy -= instance.energy * 0.45f;

                    if (instance.age > 5f) {
                        instance.type = FoliageInstance.FoliageType.Mature;
                    }
                    break;
                case FoliageInstance.FoliageType.Mature:
                    instance.age += 1f;
                    float matureMaintenance = Mathf.Lerp(1f, MatureMaintenanceMin, Mathf.Clamp01(instance.age / MatureAgeForMaintenanceMin));
                    instance.energy = Mathf.Max(0f, instance.energy - instance.strength * 0.04f * matureMaintenance);
                    instance.strength = Mathf.Min(instance.strength + instance.energy * 0.35f * 0.12f, 3f);
                    instance.energy -= instance.energy * 0.35f * matureMaintenance;
                    instance.stress = Mathf.Max(0f, instance.stress - MatureStressRecoveryPerYear);
                    break;
                case FoliageInstance.FoliageType.Dying:
                    break;
            }
        }

        /// <summary>Maximum seeds one tree can produce per year regardless of energy surplus.</summary>
        private const int MaxSeedsPerYear = 10;

        /// <summary>
        /// Fraction of seeds that disperse far (e.g. carried by animals or wind).
        /// The rest fall locally near the parent.
        /// </summary>
        private const float LongDistanceFraction = 0.08f;
        private const float LocalSigma = 10f;
        private const float LongDistanceSigma = 28f;

        public override FoliageInstance[] Seed(ref FoliageInstance instance) {
            if (instance.type != FoliageInstance.FoliageType.Mature) return Array.Empty<FoliageInstance>();
            if (instance.stress > 0.8f || instance.energy < 0.2f) return Array.Empty<FoliageInstance>();

            int minSeeds = Mathf.FloorToInt(instance.energy / 0.18f);
            int maxSeeds = Mathf.FloorToInt(instance.energy / 0.09f);
            int seedCount = Mathf.Min(Simulation.Random.Next(minSeeds, maxSeeds + 1), MaxSeedsPerYear);
            instance.energy -= seedCount * 0.08f;

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
