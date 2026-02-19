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

        public Bounds bbox => bounds;
        public Latitude latitude => lat;

        public float Height(Vector2 lpos) {
            if (!bbox.Contains(new Vector3(lpos.x, bbox.center.y, lpos.y))) return float.NaN;

            float centerHeight = (alpha > 0) ? bounds.max.y : bounds.min.y;

            return centerHeight - alpha * (lpos - center).magnitude;
        }
    }
}
