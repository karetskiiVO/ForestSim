using ProceduralVegetation.Utilities;

using System;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace ProceduralVegetation {
    [Serializable]
    public class BakedLandscape : ILandscapeDescriptor {
        // every pixel is a float32 [0...1]
        public Texture2D heightmap;
        public Vector2 texelSize;
        public Vector3 center;
        public float minHeight, maxHeight;
        public Latitude lat;

        public Bounds bbox => new Bounds(
            center,
            new Vector3(
                texelSize.x * heightmap.width,
                maxHeight - minHeight,
                texelSize.y * heightmap.height
            )
        );

        public Latitude latitude => lat;

        public float Height(Vector2 lpos) {
            // TODO check width/height
            if (!bbox.Contains(new Vector3(lpos.x, bbox.center.y, lpos.y))) return float.NaN;

            var t = heightmap.GetRawTextureData<float>()[(lpos / texelSize).CeilToInt().Dot(0, heightmap.width)];

            return Mathf.Lerp(minHeight, maxHeight, t);
        }

        public BakedLandscape Bake(ILandscapeDescriptor.BakeParams bakeParams) {
            return this;
        }
    }
}
