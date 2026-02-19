using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;


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
            if (latProp == null) {
                EditorGUI.LabelField(position, label, "Field lat not found");
                EditorGUI.EndProperty();
                return;
            }

            Rect contentRect = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            EditorGUI.BeginChangeCheck();
            float degrees = EditorGUI.FloatField(contentRect, latProp.floatValue * Mathf.Rad2Deg);
            if (EditorGUI.EndChangeCheck()) {
                latProp.floatValue = Mathf.Clamp(degrees, -90f, 90f) * Mathf.Deg2Rad;
                property.serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            return EditorGUIUtility.singleLineHeight;
        }
    }
#endif


    // #if UNITY_EDITOR
    //     [CustomPropertyDrawer(typeof(Latitude))]
    //     public class LatitudeDrawer : PropertyDrawer {
    //         public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
    //             EditorGUI.BeginProperty(position, label, property);

    //             var latProp = property.FindPropertyRelative("lat");
    //             position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

    //             Rect contentRect = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

    //             EditorGUI.BeginChangeCheck();
    //             float degrees = EditorGUI.FloatField(position, latProp.floatValue * Mathf.Rad2Deg);

    //             if (EditorGUI.EndChangeCheck()) {
    //                 latProp.floatValue = Mathf.Clamp(degrees, -90f, 90f) * Mathf.Deg2Rad;
    //                 property.serializedObject.ApplyModifiedProperties();
    //             }

    //             EditorGUI.EndProperty();
    //         }

    //         public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
    //             return EditorGUIUtility.singleLineHeight;
    //         }
    //     }
    // #endif

    public static class LinqExtents {
        public static IEnumerable<Tout> FilterCast<Tin, Tout>(this IEnumerable<Tin> source) {
            foreach (object item in source) {
                if (item is Tout) {
                    yield return (Tout)item;
                }
            }
        }
    }

    public static class VectorExtents {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Dot(this Vector2Int a, Vector2Int b) => a.x * b.x + a.y * b.y;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Dot(this Vector2Int a, int x, int y) => a.x * x + a.y * y;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int CeilToInt(this Vector2 val) => new(Mathf.CeilToInt(val.x), Mathf.CeilToInt(val.y));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Int CeilToInt(this Vector3 val) => new(Mathf.CeilToInt(val.x), Mathf.CeilToInt(val.y), Mathf.CeilToInt(val.z));
    }
}
