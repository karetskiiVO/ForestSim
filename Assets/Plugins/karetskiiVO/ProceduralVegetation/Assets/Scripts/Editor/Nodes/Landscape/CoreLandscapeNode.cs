using UnityEngine;

namespace ProceduralVegetation.Editor.Nodes {
    public abstract class CoreLandscapeNode : EditorNode {
        [Input] public ILandscapeDescriptor.BakeParams bakeParams;
        [Output] public Descriptor<BakedLandscape> landscape = new();

        public override void Reset() {
            landscape = new();
            base.Reset();
        }

        public override void Evaluate() {
            var bakeParams = GetInputValue(
                "bakeParams",
                new ILandscapeDescriptor.BakeParams() {
                    resolution = new Vector2Int(512, 512),
                }
            );

            landscape.descriptor = GetLandscapeDescriptor().Bake(bakeParams);
        }

        public abstract ILandscapeDescriptor GetLandscapeDescriptor();
    }
}
