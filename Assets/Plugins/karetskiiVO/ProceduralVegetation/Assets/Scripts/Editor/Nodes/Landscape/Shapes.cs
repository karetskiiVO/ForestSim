using System;

using XNode;

namespace ProceduralVegetation.Editor.Nodes {
    [Serializable]
    [CreateNodeMenu("landscape/cone")]
    class ConeLandscapeNode : CoreLandscapeNode {
        public ConeLandscapeDescriptor descriptor;

        public override ILandscapeDescriptor GetLandscapeDescriptor() {
            return descriptor;
        }
    }
}
