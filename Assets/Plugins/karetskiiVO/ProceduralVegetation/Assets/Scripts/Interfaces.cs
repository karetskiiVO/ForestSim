using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace ProceduralVegetation {
    // Описывает террайн
    public abstract class LandscapeDescriptor {
        [Serializable]
        public class BakeParams {
            public Vector2Int resolution;
        }

        public virtual BakedLandscape Bake(BakeParams bakeParams) {
            var heightmap = new Texture2D(
                bakeParams.resolution.x,
                bakeParams.resolution.y,
                TextureFormat.RFloat,
                false
            ) {
                filterMode = FilterMode.Point
            };

            var texelSize = new Vector2(
                bbox.size.x / bakeParams.resolution.x,
                bbox.size.z / bakeParams.resolution.y
            );

            float[] heights = new float[bakeParams.resolution.x * bakeParams.resolution.y];
            float minH = float.MaxValue;
            float maxH = float.MinValue;

            for (int y = 0; y < bakeParams.resolution.y; y++) {
                for (int x = 0; x < bakeParams.resolution.x; x++) {
                    var lpos = new Vector2(
                        bbox.min.x + (x + 0.5f) * texelSize.x,
                        bbox.min.z + (y + 0.5f) * texelSize.y
                    );

                    float h = Height(lpos);
                    if (float.IsNaN(h)) h = NANStrategy(lpos);

                    heights[y * bakeParams.resolution.x + x] = h;

                    if (h < minH) minH = h;
                    if (h > maxH) maxH = h;
                }
            }

            float range = maxH - minH;
            if (range > 0) {
                for (int i = 0; i < heights.Length; i++) {
                    heights[i] = (heights[i] - minH) / range;
                }
            }

            heightmap.SetPixelData(heights, 0);
            heightmap.Apply();

            return new BakedLandscape {
                heightmap = heightmap,
                texelSize = texelSize,
                bbox = bbox,
                minHeight = minH,
                maxHeight = maxH,
            };
        }

        // Границы местности
        public Bounds bbox;

        public abstract float Height(Vector2 lpos);

        public virtual float NANStrategy(Vector2 lpos) => 0;
    }

    public class LanscapeFruitfillness {
        public Texture2D fruitfulnessMap;
        public float fruitfulnessScale = 1f;
    }

    public class LanscapeWater {
        public Texture2D waterMap;
        public float waterScale = 1f;
    }

    public class LanscapeLighting {
        public Texture2D lightMap;
        public float lightScale = 1f;
    }

    public abstract class Scatter : IEnumerable<Vector2> {
        public abstract Vector2? Next();
        public abstract void Reset();

        public Bounds bbox;

        // Rust-style hint относительно количества точек
        public virtual long? countHint { get => null; }

        public IEnumerator<Vector2> GetEnumerator() {
            Vector2? value;
            while ((value = Next()).HasValue) {
                yield return value.Value;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }

    public interface ISimulated {
        void Simulate();
    }

    public interface IResetable {
        void Reset();
    }
}
