using Unity.VisualScripting;
using UnityEngine;

namespace ProceduralWoods.Utilities {
    // DO NOT CREATE MANUALY
    public struct Latitude {
        private float lat;

        public static Latitude FromDegrees (float deg) {
            return new () {
                lat = deg * Mathf.Deg2Rad,
            };
        }
        public static Latitude FromRadians (float rad) {
            return new () {
                lat = rad,
            };
        }

        public static implicit operator float (Latitude latitude) => latitude.lat;
    }


}
