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

    [Serializable]
    [CreateNodeMenu("Parameters/BBox parameters")]
    [NodeWidth(304)]
    public class BBOXParametersNode : EditorNode {
        [Output] public Bounds bbox;
        public Vector3 center = new(0, 0, 0);
        public Vector3 extents = new(100, 10, 100);

        public override void Evaluate() {
            bbox = new() {
                center = center,
                extents = extents
            };
        }
    }
}
