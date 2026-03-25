using System;

using ProceduralVegetation.Utilities;

using UnityEngine;

namespace ProceduralVegetation {
    public abstract class CompetitiveTreeDescriptor : TreeSpeciesDescriptor {
        private const float SeedStartEnergy = 1f;
        private const float SeedDecayPerYear = 0.16f;
        private const float SeedStartStrength = 0.2f;
        private const float MaxStrength = 3f;

        private const float BaseEnergyGainFactor = 0.62f;
        private const float GrowthEnergyGainScale = 0.35f;
        private const float BaseLightGainFactor = 0.14f;
        private const float GrowthLightGainScale = 0.2f;

        private const float IdealLight = 0.3f;

        private const float MinEnergyToSeed = 0.35f;
        private const float SeedEnergyCost = 0.12f;
        private const float BaseSeedingRate = 0.55f;
        private const float MinSpreadRadius = 4.2f;
        private const float LongDistanceSeedChanceBase = 0.42f;
        private const float LongDistanceSigmaMultiplier = 4.5f;
        private const float LongDistanceRadiusMultiplier = 4.5f;
        private const int SparsePopulationThreshold = 12;

        private readonly float growthFactor;
        private readonly float seedFactor;
        private readonly float spreadSigma;
        private readonly float stressTolerance;
        private readonly float matureAge;
        private readonly float softPopulationCap;

        private int populationCount;

        private readonly PIDRegulator energyPid;
        private readonly PIDRegulator waterPid;
        private readonly PIDRegulator lightPid;

        // Species-specific parameters (can be overridden)
        protected virtual float SaplingBaseUpkeep => 0.008f;
        protected virtual float SaplingStrengthUpkeep => 0.008f;
        protected virtual float SaplingBaseGrowth => 0.025f;
        protected virtual float SaplingGrowthScale => 0.035f;
        protected virtual float SaplingEnergyLoss => 0.11f;

        protected virtual float MatureBaseUpkeep => 0.009f;
        protected virtual float MatureStrengthUpkeep => 0.009f;
        protected virtual float MatureBaseGrowth => 0.012f;
        protected virtual float MatureGrowthScale => 0.018f;
        protected virtual float MatureEnergyLoss => 0.08f;

        protected virtual float WaterStressSensitivity => 0.28f;
        protected virtual float ExcessWaterStressSensitivity => 0.22f;
        protected virtual float LightStressSensitivity => 0.15f;
        protected virtual float StressAccumulationScale => 0.07f;
        protected virtual float WaterStressScaleMultiplier => 1.75f;
        protected virtual float WaterComfortBand => 0.12f;
        protected virtual float MaxWaterStressPerTick => 0.022f;

        protected virtual float IdealWater => 0.4f;

        protected virtual float RecoveryBase => 0.06f;
        protected virtual float RecoveryScale => 0.04f;

        protected virtual float UpkeepScale => 0.82f;
        protected virtual float EnergyLossScale => 0.85f;

        // Age-related death rates
        protected virtual float AgeDeathEnergyRate => 0.0f;    // No hard energy threshold decay (only probabilistic)
        protected virtual float AgeDeathStressRate => 0.0f;    // No hard stress threshold decay (only probabilistic)
        protected CompetitiveTreeDescriptor(
            float growthFactor,
            float seedFactor,
            float spreadSigma,
            float stressTolerance,
            float matureAge,
            float softPopulationCap
        ) {
            this.growthFactor = Mathf.Clamp01(growthFactor);
            this.seedFactor = Mathf.Clamp01(seedFactor);
            this.spreadSigma = Mathf.Max(1f, spreadSigma);
            this.stressTolerance = Mathf.Clamp(stressTolerance, 0.8f, 4f);
            this.matureAge = Mathf.Max(2f, matureAge);
            this.softPopulationCap = Mathf.Max(10f, softPopulationCap);

            energyPid = new PIDRegulator(kp: 0.6f, ki: 0.05f, kd: 0.02f, outputMin: -0.5f, outputMax: 0.5f);
            waterPid = new PIDRegulator(kp: 0.8f, ki: 0.04f, kd: 0.02f, outputMin: -0.2f, outputMax: 0.2f);
            lightPid = new PIDRegulator(kp: 0.7f, ki: 0.03f, kd: 0.015f, outputMin: -0.2f, outputMax: 0.2f);
        }

        public override void AddResources(ref FoliageInstance instance, float energy, float water, float light) {
            if (instance.type == FoliageInstance.FoliageType.Dying || instance.type == FoliageInstance.FoliageType.Seed) {
                return;
            }

            float energyGain = BaseEnergyGainFactor + growthFactor * GrowthEnergyGainScale;
            float lightGain = BaseLightGainFactor + growthFactor * GrowthLightGainScale;

            // Жёсткая конкуренция за ресурсы в старте и при плотной популяции
            float densityRatio = Mathf.Clamp01(populationCount / softPopulationCap);
            float competitionMultiplier = Mathf.Lerp(1f, 0.2f, Mathf.Pow(densityRatio, 3f));
            float crowdStress = densityRatio * 0.30f;

            // PID-регуляция для мягкого подстроения к текущим условиям
            float energyPidAdjustment = energyPid.Update(0.5f, instance.energy);
            float waterPidAdjustment = waterPid.Update(IdealWater, water);
            float lightPidAdjustment = lightPid.Update(IdealLight, light);

            float baseEnergyGain = energy * energyGain + light * lightGain;
            instance.energy += baseEnergyGain * competitionMultiplier + 0.25f * (energyPidAdjustment + waterPidAdjustment + lightPidAdjustment);

            instance.stress += crowdStress;

            float waterDelta = water - IdealWater;
            float waterDeviation = Mathf.Abs(waterDelta);
            if (waterDeviation > WaterComfortBand) {
                float effectiveDeviation = waterDeviation - WaterComfortBand;
                float sensitivity = waterDelta < 0f ? WaterStressSensitivity : ExcessWaterStressSensitivity;
                float waterStress = effectiveDeviation * sensitivity * StressAccumulationScale * WaterStressScaleMultiplier;
                instance.stress += Mathf.Min(MaxWaterStressPerTick, waterStress);
            }

            if (light < IdealLight) {
                instance.stress += (IdealLight - light) * LightStressSensitivity * StressAccumulationScale;
            }
        }

        public override bool Alive(in FoliageInstance instance) {
            switch (instance.type) {
                case FoliageInstance.FoliageType.Seed:
                    return instance.energy > 0f;
                case FoliageInstance.FoliageType.Sapling:
                    float saplingEnergyThreshold = -0.2f;
                    float saplingStressThreshold = stressTolerance * 2.2f;
                    return instance.energy > saplingEnergyThreshold && instance.stress < saplingStressThreshold;
                case FoliageInstance.FoliageType.Mature:
                    // Hard limits independent of age - only basic survival thresholds
                    if (instance.energy <= -0.2f || instance.stress >= stressTolerance * 1.7f) {
                        return false;
                    }

                    // Probabilistic death from old age
                    // Gradual increase: ~0.04% at 100 years, ~0.38% at 300 years, ~0.75% at 600+ years
                    float ageDeathChance = Mathf.Clamp01(
                        Mathf.Pow(instance.age / 600f, 3f) * 0.0075f
                    );
                    return !Simulation.Random.Chance(ageDeathChance);
                default:
                    return false;
            }
        }

        public override FoliageInstance CreateSeed(Vector2 position) {
            return new FoliageInstance {
                position = position,
                age = 0f,
                stress = 0f,
                energy = SeedStartEnergy,
                strength = 0f,
                type = FoliageInstance.FoliageType.Seed,
            };
        }

        public override void Grow(ref FoliageInstance instance) {
            switch (instance.type) {
                case FoliageInstance.FoliageType.Seed:
                    instance.energy -= SeedDecayPerYear;
                    if (instance.energy > 0f && Simulation.Random.Chance(0.35f + 0.35f * growthFactor)) {
                        instance.type = FoliageInstance.FoliageType.Sapling;
                        instance.strength = SeedStartStrength;
                        instance.energy = Mathf.Max(instance.energy, 0.5f);
                    }
                    break;
                case FoliageInstance.FoliageType.Sapling:
                    instance.age += 1f;
                    instance.energy = Mathf.Max(-0.15f, instance.energy - (SaplingBaseUpkeep + instance.strength * SaplingStrengthUpkeep) * UpkeepScale);
                    float saplingPositiveEnergy = Mathf.Max(0f, instance.energy);
                    float saplingGrowth = SaplingBaseGrowth + growthFactor * SaplingGrowthScale;
                    instance.strength = Mathf.Min(MaxStrength, instance.strength + saplingPositiveEnergy * saplingGrowth);
                    instance.energy -= instance.energy * (SaplingEnergyLoss * EnergyLossScale);
                    instance.stress = Mathf.Max(0f, instance.stress - (RecoveryBase + growthFactor * RecoveryScale));

                    if (instance.age >= matureAge) {
                        instance.type = FoliageInstance.FoliageType.Mature;
                        instance.energy = Mathf.Max(instance.energy, MinEnergyToSeed);
                    }
                    break;
                case FoliageInstance.FoliageType.Mature:
                    instance.age += 1f;
                    instance.energy = Mathf.Max(-0.1f, instance.energy - (MatureBaseUpkeep + instance.strength * MatureStrengthUpkeep) * UpkeepScale);
                    float matureGrowth = MatureBaseGrowth + growthFactor * MatureGrowthScale;
                    instance.strength = Mathf.Min(MaxStrength, instance.strength + instance.energy * matureGrowth);
                    instance.energy -= instance.energy * (MatureEnergyLoss * EnergyLossScale);
                    instance.stress = Mathf.Max(0f, instance.stress - (RecoveryBase + growthFactor * RecoveryScale));
                    break;
                case FoliageInstance.FoliageType.Dying:
                    break;
            }
        }

        public override FoliageInstance[] Seed(ref FoliageInstance instance) {
            if (instance.type != FoliageInstance.FoliageType.Mature) {
                return Array.Empty<FoliageInstance>();
            }

            if (instance.energy < MinEnergyToSeed || instance.stress > stressTolerance * 1.15f) {
                return Array.Empty<FoliageInstance>();
            }

            float energyReadiness = Mathf.Clamp01((instance.energy - MinEnergyToSeed) / 1.5f);
            float stressReadiness = Mathf.Clamp01(1f - instance.stress / Mathf.Max(0.01f, stressTolerance));
            float crowding = Mathf.Clamp01(1f - populationCount / softPopulationCap);
            float sparseBoost = populationCount <= SparsePopulationThreshold
                ? Mathf.Lerp(1.8f, 1f, populationCount / (float)SparsePopulationThreshold)
                : 1f;

            float expectedSeeds = (BaseSeedingRate + seedFactor * 0.65f)
                * (0.5f + energyReadiness)
                * (0.5f + stressReadiness)
                * (0.55f + crowding * 0.45f)
                * sparseBoost
                * 2.4f;

            // Жёсткая регуляция дублирования в плотной популяции
            if (populationCount > softPopulationCap * 0.5f) {
                expectedSeeds *= Mathf.Lerp(0.6f, 0.2f, (populationCount - softPopulationCap * 0.5f) / (softPopulationCap * 0.5f));
            }

            int seedCount = Mathf.FloorToInt(expectedSeeds);
            float remainder = expectedSeeds - seedCount;
            if (Simulation.Random.Chance(remainder)) {
                seedCount++;
            }

            if (seedCount <= 0 && populationCount <= SparsePopulationThreshold && energyReadiness > 0.2f && stressReadiness > 0.2f) {
                seedCount = 1;
            }

            seedCount = Mathf.Clamp(seedCount, 0, populationCount <= SparsePopulationThreshold ? 6 : 4);

            float totalSeedCost = SeedEnergyCost * seedCount;
            if (seedCount <= 0 || instance.energy < totalSeedCost) {
                return Array.Empty<FoliageInstance>();
            }

            instance.energy = Mathf.Max(0f, instance.energy - totalSeedCost);

            var seeds = new FoliageInstance[seedCount];
            for (int i = 0; i < seedCount; i++) {
                float longDistanceChance = LongDistanceSeedChanceBase + seedFactor * 0.22f;
                bool isLongDistance = Simulation.Random.Chance(longDistanceChance);
                float sigma = isLongDistance ? spreadSigma * LongDistanceSigmaMultiplier : spreadSigma;
                float minRadius = isLongDistance ? MinSpreadRadius * LongDistanceRadiusMultiplier : MinSpreadRadius;
                Vector2 seedPos = SampleDispersedPosition(instance.position, sigma, minRadius);
                seeds[i] = CreateSeed(seedPos);
            }

            return seeds;
        }

        public override void ResetPopulationCounters() {
            populationCount = 0;
        }

        public override void RegisterInstance(in FoliageInstance instance) {
            if (instance.type != FoliageInstance.FoliageType.Dying) {
                populationCount++;
            }
        }

        public override int PopulationCount => populationCount;

        public override FoliageInstance[] ScaleSeedsByPopulation(
            FoliageInstance[] seeds,
            in FoliageInstance parentInstance
        ) {
            return seeds ?? Array.Empty<FoliageInstance>();
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
        // Oak: hardwood, stress-resistant, nearly unlimited lifespan
        // Adjusted to be thinned: slower proliferation, lower density ceiling.
        public OakDescriptor() : base(growthFactor: 0.48f, seedFactor: 0.40f, spreadSigma: 7f, stressTolerance: 2.2f, matureAge: 6f, softPopulationCap: 120f) { }
        protected override float SaplingBaseUpkeep => 0.009f;
        protected override float SaplingStrengthUpkeep => 0.008f;
        protected override float SaplingEnergyLoss => 0.11f;
        protected override float MatureBaseUpkeep => 0.010f;
        protected override float MatureStrengthUpkeep => 0.009f;
        protected override float MatureEnergyLoss => 0.08f;
        protected override float IdealWater => 0.56f;
        protected override float WaterStressSensitivity => 0.33f;
        protected override float ExcessWaterStressSensitivity => 0.2f;
        protected override float LightStressSensitivity => 0.15f;
        protected override float AgeDeathEnergyRate => 0.00001f;  // ~unlimited lifespan
        protected override float AgeDeathStressRate => 0.00001f;
    }

    public class SpruceDescriptor : CompetitiveTreeDescriptor {
        public SpruceDescriptor() : base(growthFactor: 0.54f, seedFactor: 0.67f, spreadSigma: 8f, stressTolerance: 2.5f, matureAge: 6f, softPopulationCap: 250f) { }

        // Spruce: conifer, moderate upkeep, ~250 year lifespan
        protected override float SaplingBaseUpkeep => 0.009f;
        protected override float SaplingStrengthUpkeep => 0.008f;
        protected override float SaplingEnergyLoss => 0.11f;
        protected override float MatureBaseUpkeep => 0.010f;
        protected override float MatureStrengthUpkeep => 0.009f;
        protected override float MatureEnergyLoss => 0.08f;
        protected override float IdealWater => 0.2f;
        protected override float WaterStressSensitivity => 0.24f;
        protected override float ExcessWaterStressSensitivity => 0.55f;
        protected override float LightStressSensitivity => 0.16f;
        protected override float AgeDeathEnergyRate => 0.008f;   // Dies around 250 years
        protected override float AgeDeathStressRate => 0.005f;
    }

    public class PineDescriptor : CompetitiveTreeDescriptor {
        public PineDescriptor() : base(growthFactor: 0.5f, seedFactor: 0.58f, spreadSigma: 8f, stressTolerance: 2.0f, matureAge: 5f, softPopulationCap: 210f) { }

        // Pine: hardy, lower stress sensitivity
        protected override float SaplingBaseUpkeep => 0.008f;
        protected override float SaplingStrengthUpkeep => 0.008f;
        protected override float SaplingEnergyLoss => 0.11f;
        protected override float MatureBaseUpkeep => 0.010f;
        protected override float MatureStrengthUpkeep => 0.009f;
        protected override float MatureEnergyLoss => 0.08f;
        protected override float IdealWater => 0.20f;
        protected override float WaterStressSensitivity => 0.28f;
        protected override float ExcessWaterStressSensitivity => 0.46f;
        protected override float LightStressSensitivity => 0.16f;
    }

    public class LindenDescriptor : CompetitiveTreeDescriptor {
        public LindenDescriptor() : base(growthFactor: 0.54f, seedFactor: 0.6f, spreadSigma: 8.5f, stressTolerance: 2.2f, matureAge: 6f, softPopulationCap: 230f) { }

        // Linden: shade-tolerant, higher stress tolerance
        protected override float SaplingBaseUpkeep => 0.009f;
        protected override float SaplingStrengthUpkeep => 0.008f;
        protected override float SaplingEnergyLoss => 0.11f;
        protected override float MatureBaseUpkeep => 0.010f;
        protected override float MatureStrengthUpkeep => 0.009f;
        protected override float MatureEnergyLoss => 0.08f;
        protected override float IdealWater => 0.60f;
        protected override float WaterStressSensitivity => 0.34f;
        protected override float ExcessWaterStressSensitivity => 0.22f;
        protected override float LightStressSensitivity => 0.18f;
    }

    public class BirchDescriptor : CompetitiveTreeDescriptor {
        public BirchDescriptor() : base(growthFactor: 0.55f, seedFactor: 0.62f, spreadSigma: 8f, stressTolerance: 1.9f, matureAge: 6f, softPopulationCap: 225f) { }

        // Birch: pioneer, higher upkeep but resilient, ~125 year lifespan
        protected override float SaplingBaseUpkeep => 0.010f;
        protected override float SaplingStrengthUpkeep => 0.009f;
        protected override float SaplingEnergyLoss => 0.12f;
        protected override float MatureBaseUpkeep => 0.011f;
        protected override float MatureStrengthUpkeep => 0.010f;
        protected override float MatureEnergyLoss => 0.09f;
        protected override float IdealWater => 0.62f;
        protected override float WaterStressSensitivity => 0.46f;
        protected override float ExcessWaterStressSensitivity => 0.17f;
        protected override float LightStressSensitivity => 0.18f;
        protected override float AgeDeathEnergyRate => 0.016f;   // Dies around 125 years
        protected override float AgeDeathStressRate => 0.01f;
    }

    public class BushDescriptor : CompetitiveTreeDescriptor {
        public BushDescriptor() : base(growthFactor: 0.62f, seedFactor: 0.75f, spreadSigma: 6f, stressTolerance: 2.0f, matureAge: 4f, softPopulationCap: 300f) { }

        // Bush: early succession, low upkeep, fragile
        protected override float SaplingBaseUpkeep => 0.008f;
        protected override float SaplingStrengthUpkeep => 0.007f;
        protected override float SaplingEnergyLoss => 0.11f;
        protected override float MatureBaseUpkeep => 0.011f;
        protected override float MatureStrengthUpkeep => 0.010f;
        protected override float MatureEnergyLoss => 0.09f;
        protected override float IdealWater => 0.34f;
        protected override float WaterStressSensitivity => 0.37f;
        protected override float ExcessWaterStressSensitivity => 0.3f;
        protected override float LightStressSensitivity => 0.19f;
    }
}
