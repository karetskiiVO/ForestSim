using System;

namespace ProceduralVegetation.Editor.Nodes {
    [Serializable]
    [CreateNodeMenu("Scatters/Random")]
    class RandomScatterNode : BaseScatterNode {
        public string seed;

        public override Scatter GetScatter() {
            return new RandomScatter() {
                seed = seed,
            };
        }
    }
}
