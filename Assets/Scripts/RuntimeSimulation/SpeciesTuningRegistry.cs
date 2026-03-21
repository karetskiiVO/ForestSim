using System;
using System.Collections.Generic;

using UnityEngine;

namespace ProceduralVegetation {
    [Serializable]
    public struct SpeciesTuning {
        public string speciesName;
        public float growthMultiplier;
        public float mortalityMultiplier;
        public float seedingMultiplier;

        public static SpeciesTuning DefaultFor(string speciesName) {
            return new SpeciesTuning {
                speciesName = speciesName,
                growthMultiplier = 1f,
                mortalityMultiplier = 1f,
                seedingMultiplier = 1f,
            };
        }

        public SpeciesTuning Clamped() {
            return new SpeciesTuning {
                speciesName = speciesName,
                growthMultiplier = Mathf.Clamp(growthMultiplier, 0.5f, 1.8f),
                mortalityMultiplier = Mathf.Clamp(mortalityMultiplier, 0.5f, 1.8f),
                seedingMultiplier = Mathf.Clamp(seedingMultiplier, 0.5f, 1.8f),
            };
        }
    }

    public static class SpeciesTuningRegistry {
        private static readonly Dictionary<string, SpeciesTuning> tuningBySpecies = new();

        public static SpeciesTuning Get(string speciesName) {
            if (string.IsNullOrEmpty(speciesName)) {
                return SpeciesTuning.DefaultFor("UnknownSpecies");
            }

            if (tuningBySpecies.TryGetValue(speciesName, out var tuning)) {
                return tuning;
            }

            var defaultTuning = SpeciesTuning.DefaultFor(speciesName);
            tuningBySpecies[speciesName] = defaultTuning;
            return defaultTuning;
        }

        public static void Set(SpeciesTuning tuning) {
            if (string.IsNullOrEmpty(tuning.speciesName)) {
                return;
            }

            tuningBySpecies[tuning.speciesName] = tuning.Clamped();
        }

        public static void Apply(IEnumerable<SpeciesTuning> tunings) {
            if (tunings == null) {
                return;
            }

            foreach (var tuning in tunings) {
                Set(tuning);
            }
        }

        public static SpeciesTuning[] Snapshot() {
            var snapshot = new SpeciesTuning[tuningBySpecies.Count];
            int index = 0;
            foreach (var item in tuningBySpecies) {
                snapshot[index++] = item.Value;
            }

            return snapshot;
        }
    }
}
