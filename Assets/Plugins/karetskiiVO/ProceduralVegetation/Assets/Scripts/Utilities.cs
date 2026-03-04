using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Sirenix.Utilities;



#if UNITY_EDITOR
using UnityEditor;

using UnityEngine;
#endif

namespace ProceduralVegetation.Utilities {
    [Serializable]
    public struct Latitude {
        [SerializeField, HideInInspector] private float lat;

        public Latitude(float deg) {
            this = FromDegrees(deg);
        }

        public static Latitude FromDegrees(float deg) {
            return new() {
                lat = deg * Mathf.Deg2Rad,
            };
        }
        public static Latitude FromRadians(float rad) {
            return new() {
                lat = rad,
            };
        }

        public static implicit operator float(Latitude latitude) => latitude.lat;
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(Latitude))]
    public class LatitudeDrawer : PropertyDrawer {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);

            var latProp = property.FindPropertyRelative("lat");
            if (latProp != null) {
                EditorGUI.BeginChangeCheck();
                float degrees = EditorGUI.FloatField(position, label, latProp.floatValue * Mathf.Rad2Deg);
                if (EditorGUI.EndChangeCheck()) {
                    latProp.floatValue = Mathf.Clamp(degrees, -90f, 90f) * Mathf.Deg2Rad;
                }
            } else {
                EditorGUI.LabelField(position, label, new GUIContent("?"));
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            return EditorGUIUtility.singleLineHeight;
        }
    }
#endif
    public static class LinqExtents {
        public static IEnumerable<Tout> FilterCast<Tin, Tout>(this IEnumerable<Tin> source) {
            foreach (object item in source) {
                if (item is Tout) {
                    yield return (Tout)item;
                }
            }
        }
    }

    public static class RandomExtents {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Chance(this System.Random random, float probability) {
            return random.NextDouble() < probability;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T RandomElement<T>(this System.Random random, T[] list) {
            if (list.Length == 0) throw new InvalidOperationException("Cannot select a random element from an empty list.");
            return list[random.Next(list.Length)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T RandomElement<T>(this System.Random random, IList<T> list) {
            if (list.Count == 0) throw new InvalidOperationException("Cannot select a random element from an empty list.");
            return list[random.Next(list.Count)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 NextGaussian(this System.Random random, float stddev = 1f, Vector2 mean = default) {
            double u1 = 1.0 - random.NextDouble();
            double u2 = 1.0 - random.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return new Vector2(
                mean.x + stddev * (float)randStdNormal,
                mean.y + stddev * (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2))
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 NextVector2(this System.Random random, Rect rect) {
            return new Vector2(
                (float)(rect.xMin + random.NextDouble() * rect.width),
                (float)(rect.yMin + random.NextDouble() * rect.height)
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 NextVector2(this System.Random random, Bounds bounds) {
            var rect = new Rect(bounds.min.xz(), bounds.size.xz());
            return random.NextVector2(rect);
        }
    }

    public static class GameObjectExtents {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GameObject RemoveChildren(this GameObject parent) {
            if (parent == null) return null;

            for (int i = parent.transform.childCount - 1; i >= 0; i--) {
                GameObject.DestroyImmediate(parent.transform.GetChild(i).gameObject);
            }
            return parent;
        }
    }

    public static class VectorExtents {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Dot(this Vector2Int a, Vector2Int b) => a.x * b.x + a.y * b.y;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Dot(this Vector2Int a, int x, int y) => a.x * x + a.y * y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float[] ToArray(this Vector2 v) => new float[] { v.x, v.y };
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float[] ToArray(this Vector3 v) => new float[] { v.x, v.y, v.z };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 Lerp(Vector2 a, Vector2 b, float xCoeff, float yCoeff) => Lerp(a, b, new(xCoeff, yCoeff));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 Lerp(Vector2 a, Vector2 b, Vector2 coeffs) => a + (b - a) * coeffs.Clamp01();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 Clamp(this Vector2 v, float min, float max) =>
            new(Mathf.Clamp(v.x, min, max), Mathf.Clamp(v.y, min, max));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 Clamp01(this Vector2 v) => v.Clamp(0, 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int CeilToInt(this Vector2 val) => new(Mathf.CeilToInt(val.x), Mathf.CeilToInt(val.y));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Int CeilToInt(this Vector3 val) => new(Mathf.CeilToInt(val.x), Mathf.CeilToInt(val.y), Mathf.CeilToInt(val.z));
    }
}
