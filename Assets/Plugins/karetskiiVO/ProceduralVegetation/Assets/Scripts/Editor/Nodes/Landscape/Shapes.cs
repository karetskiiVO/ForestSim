using System;

using UnityEngine;

namespace ProceduralVegetation.Editor.Nodes {
    [Serializable]
    [CreateNodeMenu("Landscape/Shapes/Cone")]
    [NodeWidth(304)]
    class ConeLandscapeNode : BaseLandscapeNode {
        [Input(connectionType = ConnectionType.Override, typeConstraint = TypeConstraint.Strict)]
        public Bounds bounds;
        public ConeLandscapeDescriptor descriptor;

        public override LandscapeDescriptor GetLandscapeDescriptor() {
            return descriptor;
        }
    }
}
