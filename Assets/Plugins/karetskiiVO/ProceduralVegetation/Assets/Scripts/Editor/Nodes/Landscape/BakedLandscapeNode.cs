using System;

namespace ProceduralVegetation.Editor.Nodes {
    [Serializable]
    [CreateNodeMenu("landscape/baked")]
    class BakedLandscapeNode : CoreLandscapeNode {
        // TODO: Remove input port
        public BakedLandscape descriptor;

        public override LandscapeDescriptor GetLandscapeDescriptor() {
            return descriptor;
        }
    }
}
