using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class ForestEditorWindow : EditorWindow
{
    private ForestEditorGraphView graphView;

    [MenuItem("Window/Custom/Forest generator")]
    public static void Open()
    {
        var window = GetWindow<ForestEditorWindow>();
        window.titleContent = new GUIContent("Forest generator");
    }

    private void OnEnable()
    {
        CreateGraph();
        CreateTollbar();
    }

    private void OnDisable()
    { 
        rootVisualElement.Remove(graphView);
    }

    private GridBackground gridBackground;
    private void CreateGraph()
    {
        graphView = new ForestEditorGraphView();
        graphView.StretchToParentSize();

        gridBackground = new GridBackground();
        graphView.Add(gridBackground);
        gridBackground.SendToBack();
        gridBackground.StretchToParentSize();

        graphView.SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        graphView.AddManipulator(new ContentDragger());
        graphView.AddManipulator(new SelectionDragger());
        graphView.AddManipulator(new RectangleSelector());

        rootVisualElement.Add(graphView);
    }

    private void CreateTollbar()
    {
        var toolbar = new Toolbar();

        var addButton = new Button(() => graphView.CreateNode(new Vector2(200, 200)))
        {
            text = "add"
        };
        toolbar.Add(addButton);

        rootVisualElement.Add(toolbar);
    }
}