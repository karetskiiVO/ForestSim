using System;

using UnityEngine;

namespace ProceduralVegetation.Editor.Nodes {
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
