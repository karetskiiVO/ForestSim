using System;

using XNode;

namespace ProceduralVegetation.Editor.Nodes {
    [Serializable]
    [CreateNodeMenu("Landscape/Noises/RidgedNoise")]
    [NodeWidth(304)]
    class RidgedNoiseLandscapeNode : CoreLandscapeNode {
        public RidgedNoiseLandscapeDescriptor descriptor;

        public override ILandscapeDescriptor GetLandscapeDescriptor() {
            return descriptor;
        }
    }
}
