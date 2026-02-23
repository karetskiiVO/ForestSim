using ProceduralVegetation.Utilities;

using System;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace ProceduralVegetation {
    [Serializable]
    public class BakedLandscape : LandscapeDescriptor {
        // every pixel is a float32 [0...1]
        public Texture2D heightmap;
        public Vector2 texelSize;
        public float minHeight, maxHeight;

        public override float Height(Vector2 lpos) {
            // TODO check width/height
            if (!bbox.Contains(new Vector3(lpos.x, bbox.center.y, lpos.y))) return float.NaN;

            var t = heightmap.GetRawTextureData<float>()[(lpos / texelSize).CeilToInt().Dot(0, heightmap.width)];

            return Mathf.Lerp(minHeight, maxHeight, t);
        }

        public override BakedLandscape Bake(BakeParams bakeParams) {
            return this;
        }
    }
}
