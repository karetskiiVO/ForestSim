using System;

using UnityEngine;

namespace ProceduralVegetation {
    public class AdvancedMountainLandscapeDescriptor : LandscapeDescriptor {
        [Serializable]
        public class AdvancedMountainParams {
            public float baseHeight = 0f;
            public float peakHeight = 500f;
            public float noiseScale = 0.25f;
            public int octaves = 6;
            public float persistence = 0.5f;
            public float lacunarity = 2f;
            public Vector2 offset = Vector2.zero;
            public AnimationCurve heightCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

            [Header("Voronoi настройки")]
            public float voronoiScale = 4f;
            public float voronoiInfluence = 0.3f;
            public float voronoiEdgePower = 0.05f;

            [Header("Ridged настройки")]
            public float ridgeSharpness = 1.5f;
            public float valleyDepth = 0.2f;
        }

        public AdvancedMountainParams advancedParams = new AdvancedMountainParams();

        public override float Height(Vector2 lpos) {
            Vector2 normalizedPos = new Vector2(
                (lpos.x - bbox.min.x) / bbox.size.x,
                (lpos.y - bbox.min.z) / bbox.size.z
            );

            Vector2 samplePos = normalizedPos * advancedParams.noiseScale + advancedParams.offset;

            // Получаем Ridged-Multifractal шум
            float ridgedNoise = GetRidgedMultifractal(samplePos, advancedParams.octaves,
                advancedParams.persistence, advancedParams.lacunarity, advancedParams.ridgeSharpness);

            // Получаем Voronoi шум
            Vector2 voronoiSamplePos = normalizedPos * advancedParams.voronoiScale + advancedParams.offset;
            float voronoiNoise = GetVoronoiNoise(voronoiSamplePos, advancedParams.voronoiEdgePower);

            // Комбинируем шумы
            float combinedNoise = Mathf.Lerp(ridgedNoise, voronoiNoise, advancedParams.voronoiInfluence);

            // Применяем глубину долин
            combinedNoise = Mathf.Max(combinedNoise - advancedParams.valleyDepth, 0f);
            combinedNoise = Mathf.Clamp01(combinedNoise);

            // Применяем кривую высоты
            combinedNoise = advancedParams.heightCurve.Evaluate(combinedNoise);

            float height = Mathf.Lerp(
                advancedParams.baseHeight,
                advancedParams.peakHeight,
                combinedNoise
            );

            return height;
        }

        /// <summary>
        /// Ridged-Multifractal Noise
        /// </summary>
        private float GetRidgedMultifractal(Vector2 position, int octaves, float persistence, float lacunarity, float sharpness) {
            float amplitude = 1f;
            float frequency = 1f;
            float maxValue = 0f;
            float noiseValue = 0f;
            float weight = 1f;

            for (int i = 0; i < octaves; i++) {
                float sampleX = position.x * frequency;
                float sampleY = position.y * frequency;

                float perlinValue = Mathf.PerlinNoise(sampleX, sampleY);
                perlinValue = perlinValue * 2f - 1f;

                // Создаём хребты с контролем остроты
                float ridged = 1f - Mathf.Abs(perlinValue);
                ridged = Mathf.Pow(ridged, sharpness);

                ridged *= weight;
                weight = Mathf.Clamp01(ridged * 2f);

                noiseValue += ridged * amplitude;
                maxValue += amplitude;

                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return noiseValue / maxValue;
        }

        /// <summary>
        /// Voronoi Noise - создаёт структуру ячеек, похожую на тектонические плиты
        /// </summary>
        private float GetVoronoiNoise(Vector2 position, float edgePower) {
            // Находим ячейку, в которой находимся
            Vector2Int cellCoord = new Vector2Int(
                Mathf.FloorToInt(position.x),
                Mathf.FloorToInt(position.y)
            );

            Vector2 localPos = new Vector2(
                position.x - cellCoord.x,
                position.y - cellCoord.y
            );

            float minDistance = float.MaxValue;
            float secondMinDistance = float.MaxValue;

            // Проверяем соседние ячейки
            for (int y = -1; y <= 1; y++) {
                for (int x = -1; x <= 1; x++) {
                    Vector2Int neighborCoord = cellCoord + new Vector2Int(x, y);

                    // Получаем случайную точку в соседней ячейке
                    Vector2 pointInCell = GetVoronoiPoint(neighborCoord);
                    Vector2 cellLocalPos = new Vector2(
                        neighborCoord.x + pointInCell.x,
                        neighborCoord.y + pointInCell.y
                    );

                    float distance = Vector2.Distance(position, cellLocalPos);

                    if (distance < minDistance) {
                        secondMinDistance = minDistance;
                        minDistance = distance;
                    } else if (distance < secondMinDistance) {
                        secondMinDistance = distance;
                    }
                }
            }

            // Возвращаем расстояние до края ячейки (разница между первым и вторым ближайшим)
            float edgeDistance = secondMinDistance - minDistance;
            edgeDistance = Mathf.Pow(edgeDistance, 1f / edgePower);

            return Mathf.Clamp01(edgeDistance);
        }

        /// <summary>
        /// Генерирует псевдо-случайную точку в ячейке на основе её координат
        /// </summary>
        private Vector2 GetVoronoiPoint(Vector2Int cellCoord) {
            // Используем простую функцию хеша для генерации координат
            float hash1 = Mathf.Sin(cellCoord.x * 73.156f + cellCoord.y * 94.673f) * 43758.5453f;
            float hash2 = Mathf.Sin(cellCoord.x * 45.164f + cellCoord.y * 94.673f) * 43758.5453f;

            return new Vector2(
                hash1 - Mathf.Floor(hash1),
                hash2 - Mathf.Floor(hash2)
            );
        }

        public override float NANStrategy(Vector2 lpos) {
            return (advancedParams.baseHeight + advancedParams.peakHeight) / 2f;
        }
    }
}
