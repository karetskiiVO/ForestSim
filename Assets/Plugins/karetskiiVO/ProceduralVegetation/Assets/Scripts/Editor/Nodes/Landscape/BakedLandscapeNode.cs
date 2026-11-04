using System;

namespace ProceduralVegetation.Editor.Nodes {
    [Serializable]
    [CreateNodeMenu("Landscape/Baked landscape")]
    class BakedLandscapeNode : BaseLandscapeNode {
        // TODO: Remove input port
        public BakedLandscape descriptor;

        public override LandscapeDescriptor GetLandscapeDescriptor() {
            return descriptor;
        }
    }
}
