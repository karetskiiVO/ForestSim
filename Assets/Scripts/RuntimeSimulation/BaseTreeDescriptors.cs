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
        // Basic, configurable defaults for a runtime species.
        // Subclasses can override behavior by overriding methods or changing these constants.
        protected virtual float SeedToSaplingAge => 1f;
        protected virtual float SaplingToMatureAge => 5f;

        // Mortality (annual) base rates by stage (lowered to reduce excessive mortality)
        protected virtual float BaseSeedMortality => 0.3f;
        protected virtual float BaseSaplingMortality => 0.06f;
        protected virtual float BaseMatureMortality => 0.005f;

        // Growth / fecundity / dispersal tuning
        // Lowered defaults to avoid explosive yearly increases.
        protected virtual float GrowthCoefficient => 0.7f; // converts available energy -> strength
        // Size-dependent slowdown: 1/(1 + beta*strength)
        protected virtual float SizeModifierBeta => 0.4f;
        // Cap annual strength increase to avoid large jumps
        protected virtual float MaxAnnualStrengthIncrease => 0.5f;
        // Energy cost factor per unit strength increase
        protected virtual float EnergyCostFactor => 0.6f;
        protected virtual float MaxFecundityPerStrength => 0.5f; // seeds per unit strength
        protected virtual float DispersalScale => 50f; // characteristic dispersal distance (m)

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

            // Number of seeds scales with strength (rounded down). Keep integer count modest.
            int expected = Mathf.FloorToInt(Mathf.Clamp(instance.strength * MaxFecundityPerStrength, 0f, 50f));
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
                // Initial energy for seeds (moderated)
                energy = 0.12f + (float)(Simulation.Random.NextDouble() * 0.03f),
                strength = 0f,
                type = FoliageInstance.FoliageType.Seed,
            };
        }
    }

    public class OakDescriptor : RuntimeSpeciesDescriptor {
        protected override float SaplingToMatureAge => 6f;
        protected override float BaseSeedMortality => 0.25f;
        protected override float BaseSaplingMortality => 0.05f;
        protected override float BaseMatureMortality => 0.005f;
        protected override float GrowthCoefficient => 1.3f;
        protected override float MaxFecundityPerStrength => 0.6f;
        protected override float DispersalScale => 50f;
    }

    public class PineDescriptor : RuntimeSpeciesDescriptor {
        protected override float SeedToSaplingAge => 0.5f;
        protected override float SaplingToMatureAge => 4f;
        protected override float BaseSeedMortality => 0.25f;
        protected override float BaseSaplingMortality => 0.04f;
        protected override float BaseMatureMortality => 0.004f;
        protected override float GrowthCoefficient => 1.3f;
        protected override float MaxFecundityPerStrength => 1.2f;
        protected override float DispersalScale => 50f;
    }

    public class BirchDescriptor : RuntimeSpeciesDescriptor {
        protected override float SeedToSaplingAge => 0.5f;
        protected override float SaplingToMatureAge => 4f;
        protected override float BaseSeedMortality => 0.3f;
        protected override float BaseSaplingMortality => 0.055f;
        protected override float BaseMatureMortality => 0.006f;
        protected override float GrowthCoefficient => 1.2f;
        protected override float MaxFecundityPerStrength => 0.8f;
        protected override float DispersalScale => 50f;
    }

    public class SpruceDescriptor : RuntimeSpeciesDescriptor {
        protected override float SeedToSaplingAge => 1f;
        protected override float SaplingToMatureAge => 7f;
        protected override float BaseSeedMortality => 0.25f;
        protected override float BaseSaplingMortality => 0.04f;
        protected override float BaseMatureMortality => 0.004f;
        protected override float GrowthCoefficient => 1.3f;
        protected override float MaxFecundityPerStrength => 0.5f;
        protected override float DispersalScale => 50f;
    }

    public class LindenDescriptor : RuntimeSpeciesDescriptor {
        protected override float SeedToSaplingAge => 1f;
        protected override float SaplingToMatureAge => 6f;
        protected override float BaseSeedMortality => 0.25f;
        protected override float BaseSaplingMortality => 0.04f;
        protected override float BaseMatureMortality => 0.003f;
        protected override float GrowthCoefficient => 1.3f;
        protected override float MaxFecundityPerStrength => 0.45f;
        protected override float DispersalScale => 50f;
    }

    public class BushDescriptor : RuntimeSpeciesDescriptor {
        protected override float SeedToSaplingAge => 0.2f;
        protected override float SaplingToMatureAge => 2f;
        protected override float BaseSeedMortality => 0.2f;
        protected override float BaseSaplingMortality => 0.05f;
        protected override float BaseMatureMortality => 0.01f;
        protected override float GrowthCoefficient => 1.5f;
        protected override float MaxFecundityPerStrength => 1.5f;
        protected override float DispersalScale => 50f;
    }
}
