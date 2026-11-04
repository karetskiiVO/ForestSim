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

            var local = new Vector2(lpos.x - bbox.min.x, lpos.y - bbox.min.z) / texelSize;
            var pixel = local.CeilToInt();
            int px = Mathf.Clamp(pixel.x, 0, heightmap.width - 1);
            int py = Mathf.Clamp(pixel.y, 0, heightmap.height - 1);
            var t = heightmap.GetRawTextureData<float>()[py * heightmap.width + px];

            return Mathf.Lerp(minHeight, maxHeight, t);
        }

        public override BakedLandscape Bake(BakeParams bakeParams) {
            return this;
        }
    }
}
