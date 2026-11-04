using System;

using UnityEngine;

using XNode;

namespace ProceduralVegetation.Editor.Nodes {
    [Serializable]
    [CreateNodeMenu("Landscape/Noises/Ridged")]
    [NodeWidth(304)]
    class RidgedNoiseLandscapeNode : BaseLandscapeNode {
        [Input(connectionType = ConnectionType.Override, typeConstraint = TypeConstraint.Strict)]
        public Bounds bounds;

        public Vector2 offset;
        [Range(1, 10)]
        public int octaves;
        public float lacunarity;
        [Range(0f, 1f)]
        public float persistence;
        public float sharpness;

        public override LandscapeDescriptor GetLandscapeDescriptor() {
            return new RidgedNoiseLandscapeDescriptor() {
                bbox = GetInputValue<Bounds>("bounds"),
                offset = offset,
                octaves = octaves,
                lacunarity = lacunarity,
                persistence = persistence,
                sharpness = sharpness
            };
        }
    }
}
