using System;

using ProceduralVegetation.Utilities;

using UnityEngine;

namespace ProceduralVegetation {
    [Serializable]
    public class ConeLandscapeDescriptor : LandscapeDescriptor {
        public Vector2 center;
        public float alpha;
        public Bounds bounds;

        public override float Height(Vector2 lpos) {
            if (!bbox.Contains(new Vector3(lpos.x, bbox.center.y, lpos.y))) return float.NaN;

            float centerHeight = (alpha > 0) ? bounds.max.y : bounds.min.y;

            return centerHeight - alpha * (lpos - center).magnitude;
        }
    }
}
