using System;

using ProceduralVegetation.Utilities;

using UnityEngine;

namespace ProceduralVegetation {
    public abstract class CompetitiveTreeDescriptor : TreeSpeciesDescriptor {
        private const int LowPopulationThreshold = 42;
        private const int HighPopulationThreshold = 92;
        private const float MaxSeedBoost = 3.1f;
        private const float MinSeedScale = 0.12f;
        private const int RareSpeciesEmergencyMinSeeds = 1;
        private const int RareSpeciesEmergencyMaxSeeds = 3;
        private const float RareSpeciesEmergencyJitterSigma = 16f;
        private const float BaseMinSpreadRadius = 3f;
        private const float BoostedMinSpreadRadius = 7f;
        private const float EmergencyMinSpreadRadius = 10f;
        private const float BoostedLongDistanceFraction = 0.35f;
        private const float EmergencyLongDistanceFraction = 0.55f;

        private const float StressAccumulationScale = 0.18f;
        private const float MatureUpkeepScale = 0.68f;
        private const float SaplingUpkeepScale = 0.62f;
        private const float MatureStressDeathScale = 0.28f;

        private readonly float seedEnergy;
        private readonly float seedDecay;
        private readonly float seedToSaplingChance;
        private readonly float saplingInitialStrength;
        private readonly float saplingMaintenance;
        private readonly float saplingGrowth;
        private readonly float saplingEnergyLoss;
        private readonly float saplingToMatureAge;
        private readonly float matureMaintenance;
        private readonly float matureMaintenanceAgeSoftening;
        private readonly float matureGrowth;
        private readonly float matureEnergyLoss;
        private readonly float maxStrength;
        private readonly float matureStressRecovery;
        private readonly float matureStressDeathFactor;
        private readonly float matureAgeResilienceMin;
        private readonly float matureStrengthResilienceMin;
        private readonly float matureAgeForResilience;
        private readonly float matureMaxAge;
        private readonly float senescenceChance;
        private readonly float energyGainFactor;
        private readonly float lightGainFactor;
        private readonly float idealWater;
        private readonly float waterStressSensitivity;
        private readonly float idealLight;
        private readonly float lightStressSensitivity;
        private readonly float maxSeedsPerYear;
        private readonly float minEnergyToSeed;
        private readonly float seedEnergyMinCoeff;
        private readonly float seedEnergyMaxCoeff;
        private readonly float seedEnergyCost;
        private readonly float seedingStressCutoff;
        private readonly float longDistanceFraction;
        private readonly float localSigma;
        private readonly float longDistanceSigma;

        private int populationCount;

        protected CompetitiveTreeDescriptor(
            float seedEnergy,
            float seedDecay,
            float seedToSaplingChance,
            float saplingInitialStrength,
            float saplingMaintenance,
            float saplingGrowth,
            float saplingEnergyLoss,
            float saplingToMatureAge,
            float matureMaintenance,
            float matureMaintenanceAgeSoftening,
            float matureGrowth,
            float matureEnergyLoss,
            float maxStrength,
            float matureStressRecovery,
            float matureStressDeathFactor,
            float matureAgeResilienceMin,
            float matureStrengthResilienceMin,
            float matureAgeForResilience,
            float matureMaxAge,
            float senescenceChance,
            float energyGainFactor,
            float lightGainFactor,
            float idealWater,
            float waterStressSensitivity,
            float idealLight,
            float lightStressSensitivity,
            float maxSeedsPerYear,
            float minEnergyToSeed,
            float seedEnergyMinCoeff,
            float seedEnergyMaxCoeff,
            float seedEnergyCost,
            float seedingStressCutoff,
            float longDistanceFraction,
            float localSigma,
            float longDistanceSigma
        ) {
            this.seedEnergy = seedEnergy;
            this.seedDecay = seedDecay;
            this.seedToSaplingChance = seedToSaplingChance;
            this.saplingInitialStrength = saplingInitialStrength;
            this.saplingMaintenance = saplingMaintenance;
            this.saplingGrowth = saplingGrowth;
            this.saplingEnergyLoss = saplingEnergyLoss;
            this.saplingToMatureAge = saplingToMatureAge;
            this.matureMaintenance = matureMaintenance;
            this.matureMaintenanceAgeSoftening = matureMaintenanceAgeSoftening;
            this.matureGrowth = matureGrowth;
            this.matureEnergyLoss = matureEnergyLoss;
            this.maxStrength = maxStrength;
            this.matureStressRecovery = matureStressRecovery;
            this.matureStressDeathFactor = matureStressDeathFactor;
            this.matureAgeResilienceMin = matureAgeResilienceMin;
            this.matureStrengthResilienceMin = matureStrengthResilienceMin;
            this.matureAgeForResilience = matureAgeForResilience;
            this.matureMaxAge = matureMaxAge;
            this.senescenceChance = senescenceChance;
            this.energyGainFactor = energyGainFactor;
            this.lightGainFactor = lightGainFactor;
            this.idealWater = idealWater;
            this.waterStressSensitivity = waterStressSensitivity;
            this.idealLight = idealLight;
            this.lightStressSensitivity = lightStressSensitivity;
            this.maxSeedsPerYear = maxSeedsPerYear;
            this.minEnergyToSeed = minEnergyToSeed;
            this.seedEnergyMinCoeff = seedEnergyMinCoeff;
            this.seedEnergyMaxCoeff = seedEnergyMaxCoeff;
            this.seedEnergyCost = seedEnergyCost;
            this.seedingStressCutoff = seedingStressCutoff;
            this.longDistanceFraction = longDistanceFraction;
            this.localSigma = localSigma;
            this.longDistanceSigma = longDistanceSigma;
        }

        public override void AddResources(ref FoliageInstance instance, float energy, float water, float light) {
            if (instance.type == FoliageInstance.FoliageType.Dying || instance.type == FoliageInstance.FoliageType.Seed) {
                return;
            }

            var tuning = SpeciesTuningRegistry.Get(GetType().Name);

            instance.energy += (energy * energyGainFactor + light * lightGainFactor) * tuning.growthMultiplier;

            if (water < idealWater) {
                instance.stress += (idealWater - water) * waterStressSensitivity * StressAccumulationScale;
            }

            if (light < idealLight) {
                instance.stress += (idealLight - light) * lightStressSensitivity * StressAccumulationScale;
            }
        }

        public override bool Alive(in FoliageInstance instance) {
            var tuning = SpeciesTuningRegistry.Get(GetType().Name);
            float mortalityScale = Mathf.Max(0.1f, tuning.mortalityMultiplier);

            switch (instance.type) {
                case FoliageInstance.FoliageType.Seed:
                    return instance.energy > 0f;
                case FoliageInstance.FoliageType.Sapling:
                    return instance.energy > -0.2f && instance.stress < (5.6f / Mathf.Sqrt(mortalityScale));
                case FoliageInstance.FoliageType.Mature:
                    if (instance.energy <= 0f) {
                        // Mature trees can survive short starvation periods if stress remains moderate.
                        return instance.stress < 1.55f && !Simulation.Random.Chance(0.2f * mortalityScale);
                    }

                    float ageResilience = Mathf.Lerp(1f, matureAgeResilienceMin, Mathf.Clamp01(instance.age / matureAgeForResilience));
                    float strengthResilience = Mathf.Lerp(1f, matureStrengthResilienceMin, Mathf.Clamp01(instance.strength / Mathf.Max(0.01f, maxStrength)));
                    float stressDeathChance = Mathf.Clamp01(
                        Mathf.Sqrt(Mathf.Max(0f, instance.stress))
                        * matureStressDeathFactor
                        * ageResilience
                        * strengthResilience
                        * MatureStressDeathScale
                        * mortalityScale
                    );

                    if (instance.age > matureMaxAge) {
                        stressDeathChance = Mathf.Clamp01(stressDeathChance + senescenceChance * 0.4f * (instance.age - matureMaxAge) * mortalityScale);
                    }

                    return !Simulation.Random.Chance(stressDeathChance);
                default:
                    return false;
            }
        }

        public override FoliageInstance CreateSeed(Vector2 position) {
            return new FoliageInstance {
                position = position,
                age = 0f,
                stress = 0f,
                energy = seedEnergy,
                strength = 0f,
                type = FoliageInstance.FoliageType.Seed,
            };
        }

        public override void Grow(ref FoliageInstance instance) {
            var tuning = SpeciesTuningRegistry.Get(GetType().Name);
            float growthScale = Mathf.Max(0.1f, tuning.growthMultiplier);
            float mortalityScale = Mathf.Max(0.1f, tuning.mortalityMultiplier);

            switch (instance.type) {
                case FoliageInstance.FoliageType.Seed:
                    instance.energy -= seedDecay * 0.9f;
                    if (instance.energy > 0f && Simulation.Random.Chance(seedToSaplingChance)) {
                        instance.type = FoliageInstance.FoliageType.Sapling;
                        instance.strength = saplingInitialStrength;
                        instance.energy = Mathf.Max(instance.energy, seedEnergy * 0.55f);
                    }
                    break;
                case FoliageInstance.FoliageType.Sapling:
                    instance.age += 1f;
                    instance.energy = Mathf.Max(-0.08f, instance.energy - instance.strength * saplingMaintenance * SaplingUpkeepScale);
                    float positiveEnergy = Mathf.Max(0f, instance.energy);
                    instance.strength = Mathf.Min(maxStrength, instance.strength + positiveEnergy * saplingGrowth * growthScale * 1.12f);
                    instance.energy -= instance.energy * saplingEnergyLoss * SaplingUpkeepScale;
                    instance.stress = Mathf.Max(0f, instance.stress - matureStressRecovery * 0.85f / mortalityScale);

                    if (instance.age >= saplingToMatureAge) {
                        instance.type = FoliageInstance.FoliageType.Mature;
                        instance.energy = Mathf.Max(instance.energy, minEnergyToSeed * 0.7f);
                    }
                    break;
                case FoliageInstance.FoliageType.Mature:
                    instance.age += 1f;
                    float matureUpkeep = Mathf.Lerp(1f, matureMaintenanceAgeSoftening, Mathf.Clamp01(instance.age / Mathf.Max(1f, matureMaxAge)));
                    instance.energy = Mathf.Max(0f, instance.energy - instance.strength * matureMaintenance * matureUpkeep * MatureUpkeepScale);
                    instance.strength = Mathf.Min(maxStrength, instance.strength + instance.energy * matureGrowth * growthScale);
                    instance.energy -= instance.energy * matureEnergyLoss * matureUpkeep * MatureUpkeepScale;
                    instance.stress = Mathf.Max(0f, instance.stress - (matureStressRecovery + instance.energy * 0.015f) / mortalityScale);
                    break;
                case FoliageInstance.FoliageType.Dying:
                    break;
            }
        }

        public override FoliageInstance[] Seed(ref FoliageInstance instance) {
            var tuning = SpeciesTuningRegistry.Get(GetType().Name);
            float seedingScale = Mathf.Max(0.1f, tuning.seedingMultiplier);

            if (instance.type != FoliageInstance.FoliageType.Mature) return Array.Empty<FoliageInstance>();
            bool rareSpecies = populationCount <= LowPopulationThreshold;
            bool canRegularSeed = instance.energy >= minEnergyToSeed && instance.stress <= seedingStressCutoff;
            if (!canRegularSeed && !rareSpecies) {
                return Array.Empty<FoliageInstance>();
            }

            if (!canRegularSeed && rareSpecies) {
                int emergencyCount = Simulation.Random.Next(RareSpeciesEmergencyMinSeeds, RareSpeciesEmergencyMaxSeeds + 1);
                var emergencySeeds = new FoliageInstance[emergencyCount];
                for (int i = 0; i < emergencyCount; i++) {
                    bool longDistance = Simulation.Random.Chance(EmergencyLongDistanceFraction);
                    float sigma = longDistance ? longDistanceSigma * 1.35f : RareSpeciesEmergencyJitterSigma;
                    var pos = SampleDispersedPosition(instance.position, sigma, EmergencyMinSpreadRadius);

                    emergencySeeds[i] = CreateSeed(pos);
                }

                return emergencySeeds;
            }

            int minSeeds = Mathf.Max(0, Mathf.FloorToInt(instance.energy * seedEnergyMinCoeff));
            int maxSeeds = Mathf.Max(minSeeds, Mathf.CeilToInt(instance.energy * seedEnergyMaxCoeff));
            int sampledSeeds = Simulation.Random.Next(minSeeds, maxSeeds + 1);
            int scaledSeeds = Mathf.RoundToInt(sampledSeeds * seedingScale);
            int seedCount = Mathf.Min(Mathf.Max(0, scaledSeeds), Mathf.CeilToInt(maxSeedsPerYear * seedingScale));

            if (seedCount <= 0 && rareSpecies) {
                seedCount = 1;
            }

            if (seedCount <= 0) {
                return Array.Empty<FoliageInstance>();
            }

            instance.energy = Mathf.Max(0f, instance.energy - seedCount * seedEnergyCost);

            var seeds = new FoliageInstance[seedCount];
            for (int i = 0; i < seedCount; i++) {
                float sigma = Simulation.Random.Chance(longDistanceFraction) ? longDistanceSigma : localSigma;
                var pos = SampleDispersedPosition(instance.position, sigma, BaseMinSpreadRadius);
                seeds[i] = CreateSeed(pos);
            }

            return seeds;
        }

        public override void ResetPopulationCounters() {
            populationCount = 0;
        }

        public override void RegisterInstance(in FoliageInstance instance) {
            if (instance.type == FoliageInstance.FoliageType.Dying) {
                return;
            }

            populationCount++;
        }

        public override int PopulationCount => populationCount;

        public override FoliageInstance[] ScaleSeedsByPopulation(
            FoliageInstance[] seeds,
            in FoliageInstance parentInstance
        ) {
            if (seeds == null || seeds.Length == 0) {
                return seeds;
            }

            float scale = ComputeSeedScale(populationCount);
            if (Mathf.Approximately(scale, 1f)) {
                return seeds;
            }

            if (scale < 1f) {
                int keepCount = Mathf.Clamp(Mathf.RoundToInt(seeds.Length * scale), 0, seeds.Length);
                if (keepCount == 0) {
                    return Array.Empty<FoliageInstance>();
                }

                var trimmed = new FoliageInstance[keepCount];
                for (int i = 0; i < keepCount; i++) {
                    int pick = Simulation.Random.Next(seeds.Length);
                    trimmed[i] = seeds[pick];
                }

                return trimmed;
            }

            int targetCount = Mathf.RoundToInt(seeds.Length * scale);
            targetCount = Mathf.Max(targetCount, seeds.Length);

            var boosted = new FoliageInstance[targetCount];
            int baseCount = seeds.Length;
            Array.Copy(seeds, boosted, baseCount);

            for (int i = baseCount; i < targetCount; i++) {
                int pick = Simulation.Random.Next(baseCount);
                Vector2 origin = seeds[pick].position;
                bool longDistance = Simulation.Random.Chance(BoostedLongDistanceFraction);
                float sigma = longDistance ? longDistanceSigma : localSigma;
                Vector2 jittered = SampleDispersedPosition(origin, sigma, BoostedMinSpreadRadius);

                boosted[i] = CreateSeed(jittered);
            }

            return boosted;
        }

        private static float ComputeSeedScale(int population) {
            if (population <= LowPopulationThreshold) {
                float t = Mathf.Clamp01(population / (float)LowPopulationThreshold);
                return Mathf.Lerp(MaxSeedBoost, 1f, t);
            }

            if (population >= HighPopulationThreshold) {
                float t = Mathf.Clamp01((population - HighPopulationThreshold) / (float)(HighPopulationThreshold * 0.75f));
                return Mathf.Lerp(1f, MinSeedScale, t);
            }

            return 1f;
        }

        private static Vector2 SampleDispersedPosition(Vector2 origin, float sigma, float minRadius) {
            Vector2 candidate = Simulation.Random.NextGaussian(sigma, origin);
            if (float.IsNaN(candidate.x) || float.IsNaN(candidate.y)) {
                candidate = origin;
            }

            Vector2 offset = candidate - origin;
            float sqrMag = offset.sqrMagnitude;

            if (sqrMag < minRadius * minRadius) {
                if (sqrMag < 1e-6f) {
                    float angle = (float)(Simulation.Random.NextDouble() * Math.PI * 2.0);
                    offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                } else {
                    offset /= Mathf.Sqrt(sqrMag);
                }

                offset *= minRadius;
                candidate = origin + offset;
            }

            return candidate;
        }
    }

    public class OakDescriptor : CompetitiveTreeDescriptor {
        public OakDescriptor() : base(
            seedEnergy: 1.18f,
            seedDecay: 0.02f,
            seedToSaplingChance: 0.59f,
            saplingInitialStrength: 0.24f,
            saplingMaintenance: 0.031f,
            saplingGrowth: 0.068f,
            saplingEnergyLoss: 0.36f,
            saplingToMatureAge: 6.4f,
            matureMaintenance: 0.032f,
            matureMaintenanceAgeSoftening: 0.74f,
            matureGrowth: 0.048f,
            matureEnergyLoss: 0.28f,
            maxStrength: 4.8f,
            matureStressRecovery: 0.085f,
            matureStressDeathFactor: 0.0016f,
            matureAgeResilienceMin: 0.62f,
            matureStrengthResilienceMin: 0.61f,
            matureAgeForResilience: 95f,
            matureMaxAge: 135f,
            senescenceChance: 0.0036f,
            energyGainFactor: 0.82f,
            lightGainFactor: 0.16f,
            idealWater: 0.42f,
            waterStressSensitivity: 0.27f,
            idealLight: 0.24f,
            lightStressSensitivity: 0.11f,
            maxSeedsPerYear: 7f,
            minEnergyToSeed: 0.34f,
            seedEnergyMinCoeff: 2.4f,
            seedEnergyMaxCoeff: 4.2f,
            seedEnergyCost: 0.098f,
            seedingStressCutoff: 0.92f,
            longDistanceFraction: 0.05f,
            localSigma: 7f,
            longDistanceSigma: 16f
        ) { }
    }

    public class SpruceDescriptor : CompetitiveTreeDescriptor {
        public SpruceDescriptor() : base(
            seedEnergy: 1.24f,
            seedDecay: 0.02f,
            seedToSaplingChance: 0.67f,
            saplingInitialStrength: 0.22f,
            saplingMaintenance: 0.028f,
            saplingGrowth: 0.058f,
            saplingEnergyLoss: 0.33f,
            saplingToMatureAge: 7f,
            matureMaintenance: 0.033f,
            matureMaintenanceAgeSoftening: 0.76f,
            matureGrowth: 0.041f,
            matureEnergyLoss: 0.25f,
            maxStrength: 4.1f,
            matureStressRecovery: 0.09f,
            matureStressDeathFactor: 0.0013f,
            matureAgeResilienceMin: 0.58f,
            matureStrengthResilienceMin: 0.64f,
            matureAgeForResilience: 90f,
            matureMaxAge: 150f,
            senescenceChance: 0.0022f,
            energyGainFactor: 0.76f,
            lightGainFactor: 0.24f,
            idealWater: 0.43f,
            waterStressSensitivity: 0.22f,
            idealLight: 0.2f,
            lightStressSensitivity: 0.07f,
            maxSeedsPerYear: 11f,
            minEnergyToSeed: 0.22f,
            seedEnergyMinCoeff: 4.2f,
            seedEnergyMaxCoeff: 7.3f,
            seedEnergyCost: 0.072f,
            seedingStressCutoff: 1.15f,
            longDistanceFraction: 0.06f,
            localSigma: 8f,
            longDistanceSigma: 21f
        ) { }
    }

    public class PineDescriptor : CompetitiveTreeDescriptor {
        public PineDescriptor() : base(
            seedEnergy: 1f,
            seedDecay: 0.027f,
            seedToSaplingChance: 0.58f,
            saplingInitialStrength: 0.23f,
            saplingMaintenance: 0.034f,
            saplingGrowth: 0.062f,
            saplingEnergyLoss: 0.46f,
            saplingToMatureAge: 5f,
            matureMaintenance: 0.04f,
            matureMaintenanceAgeSoftening: 0.8f,
            matureGrowth: 0.041f,
            matureEnergyLoss: 0.38f,
            maxStrength: 3.25f,
            matureStressRecovery: 0.038f,
            matureStressDeathFactor: 0.0048f,
            matureAgeResilienceMin: 0.72f,
            matureStrengthResilienceMin: 0.8f,
            matureAgeForResilience: 55f,
            matureMaxAge: 85f,
            senescenceChance: 0.0052f,
            energyGainFactor: 0.8f,
            lightGainFactor: 0.12f,
            idealWater: 0.38f,
            waterStressSensitivity: 0.5f,
            idealLight: 0.36f,
            lightStressSensitivity: 0.24f,
            maxSeedsPerYear: 10f,
            minEnergyToSeed: 0.32f,
            seedEnergyMinCoeff: 3.9f,
            seedEnergyMaxCoeff: 6.8f,
            seedEnergyCost: 0.082f,
            seedingStressCutoff: 0.72f,
            longDistanceFraction: 0.06f,
            localSigma: 8f,
            longDistanceSigma: 20f
        ) { }
    }

    public class LindenDescriptor : CompetitiveTreeDescriptor {
        public LindenDescriptor() : base(
            seedEnergy: 1.24f,
            seedDecay: 0.018f,
            seedToSaplingChance: 0.66f,
            saplingInitialStrength: 0.22f,
            saplingMaintenance: 0.029f,
            saplingGrowth: 0.063f,
            saplingEnergyLoss: 0.33f,
            saplingToMatureAge: 6f,
            matureMaintenance: 0.034f,
            matureMaintenanceAgeSoftening: 0.77f,
            matureGrowth: 0.042f,
            matureEnergyLoss: 0.26f,
            maxStrength: 4f,
            matureStressRecovery: 0.085f,
            matureStressDeathFactor: 0.0017f,
            matureAgeResilienceMin: 0.64f,
            matureStrengthResilienceMin: 0.68f,
            matureAgeForResilience: 75f,
            matureMaxAge: 120f,
            senescenceChance: 0.0028f,
            energyGainFactor: 0.8f,
            lightGainFactor: 0.22f,
            idealWater: 0.42f,
            waterStressSensitivity: 0.24f,
            idealLight: 0.26f,
            lightStressSensitivity: 0.08f,
            maxSeedsPerYear: 10f,
            minEnergyToSeed: 0.23f,
            seedEnergyMinCoeff: 3.8f,
            seedEnergyMaxCoeff: 6.8f,
            seedEnergyCost: 0.078f,
            seedingStressCutoff: 1.08f,
            longDistanceFraction: 0.1f,
            localSigma: 8.5f,
            longDistanceSigma: 23f
        ) { }
    }

    public class BirchDescriptor : CompetitiveTreeDescriptor {
        public BirchDescriptor() : base(
            seedEnergy: 1f,
            seedDecay: 0.03f,
            seedToSaplingChance: 0.56f,
            saplingInitialStrength: 0.2f,
            saplingMaintenance: 0.034f,
            saplingGrowth: 0.062f,
            saplingEnergyLoss: 0.43f,
            saplingToMatureAge: 5.3f,
            matureMaintenance: 0.039f,
            matureMaintenanceAgeSoftening: 0.82f,
            matureGrowth: 0.037f,
            matureEnergyLoss: 0.36f,
            maxStrength: 2.9f,
            matureStressRecovery: 0.05f,
            matureStressDeathFactor: 0.0036f,
            matureAgeResilienceMin: 0.78f,
            matureStrengthResilienceMin: 0.82f,
            matureAgeForResilience: 45f,
            matureMaxAge: 70f,
            senescenceChance: 0.005f,
            energyGainFactor: 0.78f,
            lightGainFactor: 0.08f,
            idealWater: 0.39f,
            waterStressSensitivity: 0.42f,
            idealLight: 0.34f,
            lightStressSensitivity: 0.2f,
            maxSeedsPerYear: 10f,
            minEnergyToSeed: 0.3f,
            seedEnergyMinCoeff: 3.5f,
            seedEnergyMaxCoeff: 6.2f,
            seedEnergyCost: 0.083f,
            seedingStressCutoff: 0.72f,
            longDistanceFraction: 0.08f,
            localSigma: 8f,
            longDistanceSigma: 20f
        ) { }
    }

    public class BushDescriptor : CompetitiveTreeDescriptor {
        public BushDescriptor() : base(
            seedEnergy: 0.92f,
            seedDecay: 0.016f,
            seedToSaplingChance: 0.72f,
            saplingInitialStrength: 0.15f,
            saplingMaintenance: 0.024f,
            saplingGrowth: 0.084f,
            saplingEnergyLoss: 0.33f,
            saplingToMatureAge: 3.2f,
            matureMaintenance: 0.03f,
            matureMaintenanceAgeSoftening: 0.86f,
            matureGrowth: 0.052f,
            matureEnergyLoss: 0.31f,
            maxStrength: 2.2f,
            matureStressRecovery: 0.08f,
            matureStressDeathFactor: 0.0032f,
            matureAgeResilienceMin: 0.8f,
            matureStrengthResilienceMin: 0.86f,
            matureAgeForResilience: 30f,
            matureMaxAge: 42f,
            senescenceChance: 0.007f,
            energyGainFactor: 0.72f,
            lightGainFactor: 0.3f,
            idealWater: 0.41f,
            waterStressSensitivity: 0.36f,
            idealLight: 0.25f,
            lightStressSensitivity: 0.08f,
            maxSeedsPerYear: 24f,
            minEnergyToSeed: 0.14f,
            seedEnergyMinCoeff: 7.2f,
            seedEnergyMaxCoeff: 13.6f,
            seedEnergyCost: 0.04f,
            seedingStressCutoff: 1.1f,
            longDistanceFraction: 0.14f,
            localSigma: 6f,
            longDistanceSigma: 18f
        ) { }
    }
}
