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

    public static class XNodeExtend {
        public struct NodeIterator : IEnumerable<Node> {
            public IEnumerator<Node> GetEnumerator() {
                throw new System.NotImplementedException();
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }

        public static IEnumerable<Node> GetInputNeighbours(this NodePort port) {
            if (port == null) return null;
            return port.GetConnections().Select(target => target.node);
        }

        public static IEnumerable<Node> GetInputNeighbours(this Node node) {
            if (node == null) return null;
            return node.Inputs.SelectMany(port => node.GetInputNeighbours());
        }

        public static NodeT GetInputNeighbour<NodeT>(this NodePort port) where NodeT : Node {
            if (port == null) return null;
            return port.GetInputNeighbours()?.OfType<NodeT>().FirstOrDefault();
        }

        // public static NodeT GetInputNeighbour<NodeT>(this Node node) where NodeT : Node {
        //     // select
        // }

        public static NodePort GetInputPort<NodeType>(this Node node) {
            foreach (var port in node.Ports) {
                if (port.node is NodeType) return port;
            }

            return null;
        }

        public static NodePort GetInputPort<NodeType>(this Node node, string name) {
            var port = node.GetInputPort(name);

            if (port == null && port.node is not NodeType) return null;
            return port;
        }
    }
}
