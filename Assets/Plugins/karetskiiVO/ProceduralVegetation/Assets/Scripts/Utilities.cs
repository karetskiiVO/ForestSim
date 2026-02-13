using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

using Unity.VisualScripting;

using UnityEngine;

using XNode;

namespace ProceduralVegetation.Utilities {
    // DO NOT CREATE MANUALY
    public struct Latitude {
        private float lat;

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

    public static class LinqExtents {
        public static IEnumerable<Tout> FilterCast<Tin, Tout>(this IEnumerable<Tin> source) {
            foreach (object item in source) {
                if (item is Tout) {
                    yield return (Tout)item;
                }
            }
        }
    }
}

