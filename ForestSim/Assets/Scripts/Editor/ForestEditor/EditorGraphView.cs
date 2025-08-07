using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class ForestEditorGraphView : GraphView
{
    public void CreateNode(Vector2 position)
    { 
        var node = new Node
        {
            title = "node"
        };

        node.style.backgroundColor = new Color
        {
            r = 1,
            g = 0,
            b = 0,
            a = 1,
        };

        node.RefreshExpandedState();
        node.RefreshPorts();

        node.SetPosition(new Rect(position, new Vector2(150, 150)));

        AddElement(node);
    }
}