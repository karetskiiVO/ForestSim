using UnityEngine;

using XNode;

namespace ProceduralVegetation.Nodes {
    public abstract class CoreLandscapeNode : Node {
        [Output] public Landscape landscape;

        public abstract ILandscapeDescriptor descriptor{ get; }
    }
}
