using UnityEngine;

namespace ProceduralVegetation.Editor.Nodes {
    public abstract class CoreLandscapeNode : EditorNode {
        [Input(connectionType = ConnectionType.Override, typeConstraint = TypeConstraint.Strict)]
        public LandscapeDescriptor.BakeParams bakeParams;
        [Output]
        public Descriptor<BakedLandscape> landscape = new();

        public override void Reset() {
            landscape = new();
            base.Reset();
        }

        public override void Evaluate() {
            var bakeParams = GetInputValue(
                "bakeParams",
                new LandscapeDescriptor.BakeParams() {
                    resolution = new Vector2Int(512, 512),
                }
            );

            landscape.descriptor = GetLandscapeDescriptor().Bake(bakeParams);
        }

        public abstract LandscapeDescriptor GetLandscapeDescriptor();
    }
}
