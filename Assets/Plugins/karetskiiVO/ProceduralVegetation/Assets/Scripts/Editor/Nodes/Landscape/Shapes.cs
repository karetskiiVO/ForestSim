using System;

using XNode;

namespace ProceduralVegetation.Editor.Nodes {
    [Serializable]
    [CreateNodeMenu("landscape/cone")]
    [NodeWidth(304)]
    class ConeLandscapeNode : CoreLandscapeNode {
        public ConeLandscapeDescriptor descriptor;

        public override ILandscapeDescriptor GetLandscapeDescriptor() {
            return descriptor;
        }
    }
}
