using System;

using ProceduralVegetation.Utilities;

using UnityEngine;

namespace ProceduralVegetation {
    [Serializable]
    public class RidgedNoiseLandscapeDescriptor : LandscapeDescriptor {
        /// <summary>Смещение в пространстве шума</summary>
        public Vector2 offset;
        /// <summary>Количество октав</summary>
        [Range(1, 10)]
        public int octaves;
        /// <summary>Множитель частоты между октавами</summary>
        public float lacunarity;
        /// <summary>Множитель амплитуды между октавами</summary>
        [Range(0f, 1f)]
        public float persistence;
        /// <summary>Резкость гребней (усиление веса)</summary>
        public float sharpness;

        public override float Height(Vector2 lpos) {
            if (!bbox.Contains(new Vector3(lpos.x, bbox.center.y, lpos.y))) return float.NaN;

            float s = Mathf.Max(bbox.extents.ToArray());

            float result = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float weight = 1f;
            float maxValue = 0f;

            for (int i = 0; i < Mathf.Max(1, octaves); i++) {
                float ox = offset.x + i * 127.341f;
                float oy = offset.y + i * 311.713f;

                float nx = lpos.x / s * frequency + ox;
                float ny = lpos.y / s * frequency + oy;

                float n = Mathf.PerlinNoise(nx, ny);
                n = 1f - Mathf.Abs(n * 2f - 1f);
                n *= n;
                n *= weight;

                weight = Mathf.Clamp01(n * sharpness);

                result += n * amplitude;
                maxValue += amplitude;

                amplitude *= persistence;
                frequency *= lacunarity;
            }

            float t = maxValue > 0f ? result / maxValue : 0f;
            return Mathf.Lerp(bbox.min.y, bbox.max.y, t);
        }
    }
}
