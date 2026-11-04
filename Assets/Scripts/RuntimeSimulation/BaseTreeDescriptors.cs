using System;
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
                    new TreeSpeciesCounterEvent(){ time = 0.999f },
                };
            }

            public class TreeSpeciesCounterEvent : ProceduralVegetation.Simulation.Event {
                public override void Execute(ref SimulationContext context) {
                    for (int i = 0; i < context.points.Count; i++) {
                        var descriptor = context.speciesDescriptors[context.points[i].speciesID];

                        if (descriptor is not TreeSpeciesCountDescriptor) continue;
                        var point = context.points[i];
                        (descriptor as TreeSpeciesCountDescriptor).ResetCount();
                    }

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
        protected float SeedToSaplingAge = 1f;
        protected float SaplingToMatureAge = 4f;

        // Mortality (annual) base rates by stage (lowered to reduce excessive mortality)
        protected float BaseSeedMortality = 0.3f;
        protected float BaseSaplingMortality = 0.06f;
        protected float BaseMatureMortality = 0.005f;

        // If species count is below or equal to this threshold, disable stochastic death (deterministic survival)
        protected int DeterministicSurvivalThreshold = 2;

        // Growth / fecundity / dispersal tuning
        // Lowered defaults to avoid explosive yearly increases.
        protected float GrowthCoefficient = 0.7f; // converts available energy -> strength
        // Size-dependent slowdown: 1/(1 + beta*strength)
        protected float SizeModifierBeta = 0.4f;
        // Cap annual strength increase to avoid large jumps
        protected float MaxAnnualStrengthIncrease = 0.5f;
        // Energy cost factor per unit strength increase
        protected float EnergyCostFactor = 0.6f;
        protected float MaxFecundityPerStrength = 2.0f; // seeds per unit strength (increased)
        protected float DispersalScale = 50f; // characteristic dispersal distance (m)
        // Population threshold for reproduction (above this, trees stop producing seeds)
        protected int ReproductionPopulationThreshold = 250;
        // Sharpness of penalty: larger values make suppression stronger around threshold
        protected float ReproductionPenaltySharpness = 3f;
        // Minimum density factor to avoid completely zero fecundity
        protected float ReproductionMinDensityFactor = 0.01f;
        // Maximum number of seeds a single tree can produce per year (species can override)
        protected int MaxSeedsPerTree = 50;

        public override void Grow(ref FoliageInstance instance) {
            // Advance age by one year (assumes Grow is called once per year).
            instance.age += 1f;

            // Convert stored energy into strength (biomass proxy).
            // Use a saturating log-like conversion and apply a size modifier to slow growth for large trees.
            float energy = Mathf.Max(0f, instance.energy);
            float rawDelta = GrowthCoefficient * Mathf.Log(1f + 0.5f * energy);
            float sizeMod = 1f / (1f + SizeModifierBeta * instance.strength);
            float deltaStrength = rawDelta * sizeMod;
            // cap annual increase
            deltaStrength = Mathf.Min(deltaStrength, MaxAnnualStrengthIncrease);
            instance.strength = Mathf.Max(0f, instance.strength + deltaStrength);

            // Consume part of energy for growth/maintenance.
            instance.energy = Mathf.Max(0f, instance.energy - deltaStrength * EnergyCostFactor);

            // Gentle natural decay of stress over time
            instance.stress = Mathf.Max(0f, instance.stress - 0.05f);

            // Stage transitions
            if (instance.type == FoliageInstance.FoliageType.Seed) {
                // Lowered threshold to make seed->sapling transition easier when seeds have modest energy.
                if (instance.age >= SeedToSaplingAge && instance.strength > 0.005f) {
                    instance.type = FoliageInstance.FoliageType.Sapling;
                }
            } else if (instance.type == FoliageInstance.FoliageType.Sapling) {
                if (instance.age >= SaplingToMatureAge && instance.strength >= 0.5f) {
                    instance.type = FoliageInstance.FoliageType.Mature;
                }
            } else if (instance.type == FoliageInstance.FoliageType.Mature) {
                // cap strength to keep numbers bounded
                instance.strength = Mathf.Min(instance.strength, 1000f);
            }
        }

        public override bool Alive(in FoliageInstance instance) {
            // Protect small populations: if species population is below threshold, prevent stochastic death.
            if (this.Count <= DeterministicSurvivalThreshold) {
                return true;
            }
            // Compute base survival probability by life stage
            float baseSurvival = instance.type switch {
                FoliageInstance.FoliageType.Seed => 1f - BaseSeedMortality,
                FoliageInstance.FoliageType.Sapling => 1f - BaseSaplingMortality,
                FoliageInstance.FoliageType.Mature => 1f - BaseMatureMortality,
                FoliageInstance.FoliageType.Dying => 0f,
                _ => 1f - BaseMatureMortality,
            };

            // Resource modifiers: more energy/strength increases survival.
            float energyFactor = Mathf.Clamp01(instance.energy / (1f + instance.age * 0.1f));
            float strengthFactor = Mathf.Clamp01(instance.strength / (1f + instance.age * 0.2f));

            float resourceSurvival = Mathf.Clamp01(0.5f * energyFactor + 0.5f * strengthFactor);

            // Combine base and resource-driven survival (weighted towards base survival)
            float finalSurvival = Mathf.Clamp01(0.6f * baseSurvival + 0.4f * resourceSurvival);

            // Ensure a minimum floor for survival by stage to avoid near-certain wipeout
            float minSurvival = instance.type switch {
                FoliageInstance.FoliageType.Seed => 0.55f,
                FoliageInstance.FoliageType.Sapling => 0.8f,
                FoliageInstance.FoliageType.Mature => 0.98f,
                _ => 0.3f,
            };

            finalSurvival = Mathf.Clamp01(Mathf.Max(finalSurvival, minSurvival));

            // Stochastic outcome for a single-year survival
            return Simulation.Random.Chance(finalSurvival);
        }

        public override void AddResources(ref FoliageInstance instance, float energy, float water, float light) {
            // Simple bookkeeping: baseline yearly energy gain plus incoming resource contribution.
            // Baseline yearly energy supply (reduced slightly to slow runaway growth)
            float baseline = instance.type switch {
                FoliageInstance.FoliageType.Seed => 0.04f,
                FoliageInstance.FoliageType.Sapling => 0.08f,
                FoliageInstance.FoliageType.Mature => 0.03f,
                _ => 0.015f,
            };

            instance.energy += baseline + energy;
            instance.energy += light * 0.01f;
            // water currently ignored in this simple descriptor but could modulate conversion.
        }

        public override FoliageInstance[] Seed(ref FoliageInstance instance) {
            if (instance.type != FoliageInstance.FoliageType.Mature) return Array.Empty<FoliageInstance>();

            // Density-dependent fecundity using a smooth penalty function (exponential decay)
            int currentCount = this.Count;
            float x = (float)currentCount / Mathf.Max(1f, (float)ReproductionPopulationThreshold);
            float densityFactor = Mathf.Exp(-ReproductionPenaltySharpness * x);
            densityFactor = Mathf.Clamp(densityFactor, ReproductionMinDensityFactor, 1f);

            float fecundityFloat = instance.strength * MaxFecundityPerStrength * densityFactor;
            int expected = Mathf.FloorToInt(Mathf.Clamp(fecundityFloat, 0f, (float)MaxSeedsPerTree));

            // If population is still low (below threshold) ensure at least one seed from mature trees
            if (instance.type == FoliageInstance.FoliageType.Mature && expected == 0 && currentCount < ReproductionPopulationThreshold) expected = 1;
            if (expected <= 0) return Array.Empty<FoliageInstance>();

            var seeds = new FoliageInstance[expected];
            for (int i = 0; i < expected; i++) {
                // Exponential dispersal: r = -scale * ln(u)
                double u = Simulation.Random.NextDouble();
                float r = (float)(-DispersalScale * Math.Log(Math.Max(1e-6, u)));
                double theta = Simulation.Random.NextDouble() * Math.PI * 2.0;
                var pos = instance.position + new Vector2(Mathf.Cos((float)theta), Mathf.Sin((float)theta)) * r;
                seeds[i] = CreateSeed(pos);
            }

            return seeds;
        }

        public override FoliageInstance CreateSeed(Vector2 position) {
            return new FoliageInstance() {
                position = position,
                age = 0f,
                stress = 0f,
                // Initial energy and small starting strength for seeds (improves establishment)
                energy = 0.5f + (float)(Simulation.Random.NextDouble() * 0.1f),
                strength = 0.05f,
                type = FoliageInstance.FoliageType.Seed,
            };
        }
    }

    public class OakDescriptor : RuntimeSpeciesDescriptor {
        public OakDescriptor() {
            SaplingToMatureAge = 6f;
            BaseSeedMortality = 0.25f;
            BaseSaplingMortality = 0.05f;
            BaseMatureMortality = 0.005f;
            GrowthCoefficient = 1.3f;
            // Reduce oak fecundity and dispersal to slow aggressive spread
            MaxFecundityPerStrength = 0.25f;
            DispersalScale = 20f;
            // Make density-dependent suppression stronger and act earlier
            ReproductionPopulationThreshold = 150;
            ReproductionPenaltySharpness = 4f;
            // Strong per-tree cap on seeds for oak
            MaxSeedsPerTree = 8;
        }
    }

    public class PineDescriptor : RuntimeSpeciesDescriptor {
        public PineDescriptor() {
            SeedToSaplingAge = 0.5f;
            SaplingToMatureAge = 4f;
            BaseSeedMortality = 0.25f;
            BaseSaplingMortality = 0.04f;
            BaseMatureMortality = 0.004f;
            GrowthCoefficient = 1.3f;
            MaxFecundityPerStrength = 1.2f;
            DispersalScale = 50f;
        }
    }

    public class BirchDescriptor : RuntimeSpeciesDescriptor {
        public BirchDescriptor() {
            SeedToSaplingAge = 0.5f;
            SaplingToMatureAge = 4f;
            BaseSeedMortality = 0.3f;
            BaseSaplingMortality = 0.055f;
            BaseMatureMortality = 0.006f;
            GrowthCoefficient = 1.2f;
            MaxFecundityPerStrength = 0.8f;
            DispersalScale = 50f;
        }
    }

    public class SpruceDescriptor : RuntimeSpeciesDescriptor {
        public SpruceDescriptor() {
            SeedToSaplingAge = 0.5f;
            SaplingToMatureAge = 5f;
            BaseSeedMortality = 0.25f;
            BaseSaplingMortality = 0.04f;
            BaseMatureMortality = 0.004f;
            GrowthCoefficient = 1.3f;
            MaxFecundityPerStrength = 1.0f;
            DispersalScale = 50f;
        }
    }

    public class LindenDescriptor : RuntimeSpeciesDescriptor {
        public LindenDescriptor() {
            SeedToSaplingAge = 0.5f;
            SaplingToMatureAge = 4f;
            BaseSeedMortality = 0.25f;
            BaseSaplingMortality = 0.04f;
            BaseMatureMortality = 0.005f;
            GrowthCoefficient = 1.3f;
            MaxFecundityPerStrength = 0.7f;
            DispersalScale = 50f;
        }
    }

    public class BushDescriptor : RuntimeSpeciesDescriptor {
        public BushDescriptor() {
            SeedToSaplingAge = 0.2f;
            SaplingToMatureAge = 2f;
            BaseSeedMortality = 0.2f;
            BaseSaplingMortality = 0.05f;
            BaseMatureMortality = 0.01f;
            GrowthCoefficient = 1.5f;
            MaxFecundityPerStrength = 1.5f;
            DispersalScale = 50f;
        }
    }
}
