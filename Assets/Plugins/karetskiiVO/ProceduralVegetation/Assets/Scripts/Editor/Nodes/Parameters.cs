using System;

using UnityEngine;

namespace ProceduralVegetation.Editor.Nodes {
    [Serializable]
    [CreateNodeMenu("Parameters/Bake parameters")]
    public class BakeParametersNode : EditorNode {
        [Output] public LandscapeDescriptor.BakeParams bakeParams;
        public Vector2Int resolution = new(512, 512);

        public override void Evaluate() {
            bakeParams = new() {
                resolution = resolution,
            };
        }
    }

}
