using System;

using ProceduralVegetation.Utilities;

using UnityEngine;

namespace ProceduralVegetation {
    [Serializable]
    public class RandomScatter : Scatter {
        System.Random random;
        public string seed;

        public override Vector2? Next() {
            if (random == null) Reset();
            return VectorExtents.Lerp(
                bbox.min.xz(), bbox.max.xz(), (float)random.NextDouble(), (float)random.NextDouble()
            );
        }

        public override void Reset() {
            random = new System.Random(seed.GetHashCode());
        }
    }
}
