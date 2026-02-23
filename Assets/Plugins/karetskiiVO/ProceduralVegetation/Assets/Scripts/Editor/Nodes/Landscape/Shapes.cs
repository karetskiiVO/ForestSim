using System;

using XNode;

namespace ProceduralVegetation.Editor.Nodes {
    [Serializable]
    [CreateNodeMenu("Landscape/Shapes/Cone")]
    [NodeWidth(304)]
    class ConeLandscapeNode : CoreLandscapeNode {
        public ConeLandscapeDescriptor descriptor;

        public override LandscapeDescriptor GetLandscapeDescriptor() {
            return descriptor;
        }
    }
}
