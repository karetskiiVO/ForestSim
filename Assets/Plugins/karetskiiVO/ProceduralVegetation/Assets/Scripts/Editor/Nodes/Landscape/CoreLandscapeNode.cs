using UnityEngine;

using XNode;

namespace ProceduralVegetation.Editor.Nodes {
    public abstract class CoreLandscapeNode : EditorNode {
        [Output] public Landscape landscape = new();
        private Landscape result = new();

        public override void Reset() {
            result = new();

            base.Reset();
        }

        public override void Evaluate() {
            result.landscapeDescriptor = GetLandscapeDescriptor();
        }

        public override object GetValue(NodePort port) {
            switch (port.fieldName) {
                case "landscape":
                    landscape = result;
                    return landscape;
                default:
                    return null;
            }
        }

        public abstract ILandscapeDescriptor GetLandscapeDescriptor();
    }
}
