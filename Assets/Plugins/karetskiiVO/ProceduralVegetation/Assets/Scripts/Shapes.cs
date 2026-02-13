using System;

using ProceduralVegetation.Utilities;

using UnityEngine;

namespace ProceduralVegetation {
    [Serializable]
    public struct ConeLandscapeDescriptor : ILandscapeDescriptor {
        public Vector2 center;
        public float alpha;
        public Latitude lat;
        public Bounds bounds;

        public Bounds bbox => throw new System.NotImplementedException();
        public Latitude latitude => throw new System.NotImplementedException();

        public float Height(Vector2 pos) {
            float centerHeight = (alpha > 0) ? bounds.max.y : bounds.min.y;

            return centerHeight - alpha * (pos - center).magnitude;
        }
    }
}
