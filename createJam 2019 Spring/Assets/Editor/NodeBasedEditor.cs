﻿using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;

public class NodeBasedEditor : EditorWindow
{
    private List<Node> nodes;
    private List<Connection> connections;

    private GUIStyle nodeStyle;
    private GUIStyle selectedNodeStyle;
    private GUIStyle inPointStyle;
    private GUIStyle outPointStyle;
    private GUIStyle buttonStyle;

    private static Texture2D backgroundTex;
    private static Color smallGridColor;
    private static Color bigGridColor;

    private ConnectionPoint selectedInPoint;
    private ConnectionPoint selectedOutPoint;

    private Rect UiPositioning;

    private Vector2 offset;
    private Vector2 drag;

    private string colorBegin;
    private string colorEnd;

    private TowerBlueprint tower;
    //private TowerNode towerNode;
    string[] workingFolders = { "Assets/ScriptableObjects/TowerBluprints" };

    private const float kMinZoom = 0.1f;
    private const float kMaxZoom = 5f;
    private Rect _zoomArea = new Rect(0.0f, 75.0f, 600.0f, 300.0f - 100.0f);
    private float _zoom = 1.0f;
    private Vector2 _zoomCoordsOrigin = Vector2.zero;

    [MenuItem("Window/Node Based Editor")]
    private static void OpenWindow()
    {
        NodeBasedEditor window = GetWindow<NodeBasedEditor>();
        window.titleContent = new GUIContent("Node Based Editor");
        window.minSize = new Vector2(800f, 600f);
        window.wantsMouseMove = true;
        window.Show();
    }

    private Vector2 ConvertScreenCoordsToZoomCoords(Vector2 screenCoords)
    {
        _zoomArea = position;
        _zoomArea.x = 0f; _zoomArea.y = 0f;
        return (screenCoords - _zoomArea.TopLeft()) / _zoom + _zoomCoordsOrigin;
    }
    private void DrawZoomArea()
    {
        // Within the zoom area all coordinates are relative to the top left corner of the zoom area
        // with the width and height being scaled versions of the original/unzoomed area's width and height.
        _zoomArea = position;
        _zoomArea.x = 0f; _zoomArea.y = 0f;
        EditorZoomArea.Begin(_zoom, _zoomArea);

        GUI.DrawTexture(new Rect(0, 0, _zoomArea.width/_zoom, _zoomArea.height/_zoom), backgroundTex, ScaleMode.StretchToFill); //draw background
        

        DrawGrid(20, 0.2f, smallGridColor);
        DrawGrid(100, 0.4f, bigGridColor);

        DrawNodes();
        DrawConnections();

        DrawConnectionLine(Event.current);

        EditorZoomArea.End();
    }
    private void HandleZoomEvents()
    {
        // Allow adjusting the zoom with the mouse wheel as well. In this case, use the mouse coordinates
        // as the zoom center instead of the top left corner of the zoom area. This is achieved by
        // maintaining an origin that is used as offset when drawing any GUI elements in the zoom area.
        if (Event.current.type == EventType.ScrollWheel)
        {
            Vector2 screenCoordsMousePos = Event.current.mousePosition;
            Vector2 delta = Event.current.delta;
            Vector2 zoomCoordsMousePos = ConvertScreenCoordsToZoomCoords(screenCoordsMousePos);
            float zoomDelta = -delta.y / 150.0f; //assume this is scrollwheel zoom speed
            float oldZoom = _zoom;
            _zoom += zoomDelta;
            _zoom = Mathf.Clamp(_zoom, kMinZoom, kMaxZoom);
            _zoomCoordsOrigin += (zoomCoordsMousePos - _zoomCoordsOrigin) - (oldZoom / _zoom) * 
                (zoomCoordsMousePos - _zoomCoordsOrigin);
            //Debug.Log("mouse position: " + _zoomCoordsOrigin);
            //Debug.Log("Scrolling");

            Event.current.Use();
        }

        // Allow moving the zoom area's origin by dragging with the middle mouse button or dragging
        // with the left mouse button with Alt pressed.
        if (Event.current.type == EventType.MouseDrag &&
            (Event.current.button == 0 && Event.current.modifiers == EventModifiers.Alt) ||
            Event.current.button == 2)
        {
            Vector2 delta = Event.current.delta;
            delta /= _zoom;
            _zoomCoordsOrigin += delta;

            Event.current.Use();
        }
    }

    private void OnEnable()
    {
        backgroundTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        backgroundTex.SetPixel(0, 0, new Color(0.25f, 0.25f, 0.25f));
        backgroundTex.Apply();

        smallGridColor = Color.grey;
        bigGridColor = new Color(0.7f, 0.7f, 0.7f);

        nodeStyle = new GUIStyle();
        nodeStyle.normal.background = EditorGUIUtility.Load("builtin skins/darkskin/images/node1.png") as Texture2D;
        nodeStyle.border = new RectOffset(12, 12, 12, 12);

        selectedNodeStyle = new GUIStyle();
        selectedNodeStyle.normal.background = EditorGUIUtility.Load("builtin skins/darkskin/images/node1 on.png") as Texture2D;
        selectedNodeStyle.border = new RectOffset(12, 12, 12, 12);

        inPointStyle = new GUIStyle();
        inPointStyle.normal.background = EditorGUIUtility.Load("builtin skins/darkskin/images/btn left.png") as Texture2D;
        inPointStyle.active.background = EditorGUIUtility.Load("builtin skins/darkskin/images/btn left on.png") as Texture2D;
        inPointStyle.border = new RectOffset(4, 4, 12, 12);

        outPointStyle = new GUIStyle();
        outPointStyle.normal.background = EditorGUIUtility.Load("builtin skins/darkskin/images/btn right.png") as Texture2D;
        outPointStyle.active.background = EditorGUIUtility.Load("builtin skins/darkskin/images/btn right on.png") as Texture2D;
        outPointStyle.border = new RectOffset(4, 4, 12, 12);

        buttonStyle = new GUIStyle();
        buttonStyle.alignment = TextAnchor.MiddleCenter;
        //EditorGUIUtility.Load("builtin skins/darkskin/images/btn.png") as Texture2D;
        buttonStyle.normal.background =  EditorGUIUtility.IconContent("PR DropHere@2x").image as Texture2D;
        buttonStyle.active.background = EditorGUIUtility.IconContent("PingBox@2x").image as Texture2D;
        buttonStyle.border = new RectOffset(4, 4, 12, 12);

        colorBegin = "<color=#FF8000><b>";
        colorEnd = "</b></color>";

        UiPositioning = new Rect(10f, 10f, 150f, 50f);
    }

    private void OnGUI()
    {
        HandleZoomEvents();
        DrawZoomArea();        
        
        //Non zoomed stuff
        ProcessNodeEvents(Event.current);
        ProcessEvents(Event.current);

        if (GUI.Button(UiPositioning, colorBegin + "Save Towers" + colorEnd, buttonStyle))
        {
            saveNodes();
        }

        if (GUI.Button(new Rect(UiPositioning.x + UiPositioning.width + 20f, UiPositioning.y, UiPositioning.width, 
            UiPositioning.height), colorBegin + "Load Towers" + colorEnd, buttonStyle))
        {
            LoadTowers();
        }

        GUI.Label(new Rect(UiPositioning.x + UiPositioning.width * 2 + 30f, UiPositioning.y, UiPositioning.width,
            UiPositioning.height), colorBegin+"Current Zoom: " + _zoom.ToString("F2") + colorEnd, buttonStyle);

        if (GUI.changed) Repaint();
    }

    private void LoadTowers()
    {
        Rect windoPosition = position;
        Vector2 newNodePosition = new Vector2(windoPosition.x, windoPosition.y);
        string[] result = AssetDatabase.FindAssets("t:TowerBlueprint",workingFolders);

        if (result.Length != 0)
        {
            if (nodes == null)
            {
                nodes = new List<Node>();
            }
            if(result.Length <= nodes.Count)
            {
                //we have already loaded once
                Debug.LogWarning("We have already loaded the Towers once... no need to have duplicate nodes");
                return;
            }
            foreach (String asset in result)
            {
                string path = AssetDatabase.GUIDToAssetPath(asset);
                tower = (TowerBlueprint)AssetDatabase.LoadAssetAtPath(path, typeof(TowerBlueprint));
                Debug.Log("Towers Found = " + tower.name);

                TowerNode towerNode = new TowerNode(tower);
                
                if(tower.getEditorPosition() == null)
                {
                    //Debug.Log("editor position is null");
                    tower.setEditorPosition(newNodePosition);
                }
                //Debug.Log(tower.name + " Editor position to load in = " + tower.getEditorPosition());
                nodes.Add(new Node(tower.getEditorPosition(), 200, 50, nodeStyle, selectedNodeStyle, inPointStyle, 
                    outPointStyle, OnClickInPoint, OnClickOutPoint, OnClickRemoveNode, towerNode));                
            }
            /*for (int i = 0; i < nodes.Count; ++i)
            {
                for (int j = 0; j < nodes[i].skill.skill_Dependencies.Length; ++j)
                {
                    if (skillDictionary.TryGetValue(nodes[i].skill.skill_Dependencies[j], out outSkill))
                    {
                        for (int k = 0; k < nodes.Count; ++k)
                        {
                            if (nodes[k].skill.id_Skill == outSkill.id_Skill)
                            {
                                outNode = nodes[k];
                                OnClickOutPoint(outNode.outPoint);
                                break;
                            }
                        }
                        OnClickInPoint(nodes[i].inPoint);
                    }
                }
            }*/
            for (int i = 0; i < nodes.Count; i++)
            {
                //string curTowerName = nodes[i].myInfo.title;
                string path = AssetDatabase.GUIDToAssetPath(result[i]);
                tower = (TowerBlueprint)AssetDatabase.LoadAssetAtPath(path, typeof(TowerBlueprint));
                for (int j = 0; j < tower.upgradesFrom.Count; j++)
                {
                    string nameToMatch = tower.upgradesFrom[j].name;

                    for (int k = 0; k < nodes.Count; ++k)
                    {
                        Node nodeToMatch;
                        if (nodes[k].myInfo.title == nameToMatch)
                        {
                            nodeToMatch = nodes[k];
                            OnClickOutPoint(nodeToMatch.outPoint);
                            OnClickInPoint(nodes[i].inPoint);
                            break;
                        }
                    }
                }
                for (int j = 0; j < tower.upgradesTo.Count; j++)
                {
                    
                }
            }
        }
        else
        {
            Debug.Log("No TowerBlueprints seems to exist yet");
        }
    }

    private void saveNodes()
    {

        if (nodes.Count > 0)
        {
            // We fill with as many skills as nodes we have
            TowerBlueprint[] blueprints = new TowerBlueprint[nodes.Count];
            int[] In_dependencies;
            int[] Out_dependencies;
            List<int> In_dependenciesList = new List<int>();
            List<int> Out_dependenciesList = new List<int>();

            // Iterate over all of the nodes. Populating the skills with the node info
            for (int i = 0; i < nodes.Count; ++i)
            {
                if (connections != null)
                {
                    List<Connection> In_connectionsToRemove = new List<Connection>();
                    List<Connection> Out_connectionsToRemove = new List<Connection>();

                    for (int j = 0; j < connections.Count; j++)
                    {
                        if (connections[j].inPoint == nodes[i].inPoint)
                        {
                            for (int k = 0; k < nodes.Count; ++k)
                            {
                                if (connections[j].outPoint == nodes[k].outPoint)
                                {
                                    //Debug.Log("node k = " + k);
                                    In_dependenciesList.Add(k);
                                }
                            }
                            In_connectionsToRemove.Add(connections[j]);
                        }
                        if (connections[j].outPoint == nodes[i].outPoint)
                        {
                            for (int k = 0; k < nodes.Count; ++k)
                            {
                                if (connections[j].inPoint == nodes[k].inPoint)
                                {
                                    Out_dependenciesList.Add(k);
                                }
                            }
                            Out_connectionsToRemove.Add(connections[j]);
                        }
                    }
                }
                In_dependencies = In_dependenciesList.ToArray();
                In_dependenciesList.Clear();
                Out_dependencies = Out_dependenciesList.ToArray();
                Out_dependenciesList.Clear();

                TowerNode newTowerNode = (TowerNode)nodes[i].myInfo;

                blueprints[i] = newTowerNode.GetTower();
                //Debug.Log(blueprints[i].name +" In dependecies length = " + In_dependencies.Length.ToString());
                //Debug.Log(blueprints[i].name + "Out dependecies length = " + Out_dependencies.Length.ToString());
                for (int k = 0; k < In_dependencies.Length; k++)
                {
                    TowerNode towerNode = (TowerNode)nodes[In_dependencies[k]].myInfo;
                    TowerBlueprint towerToAdd = ScriptableObject.CreateInstance<TowerBlueprint>();
                    towerToAdd = towerNode.GetTower();
                    string _path = workingFolders[0] + "/" + towerToAdd.name + ".asset";
                    TowerBlueprint _towerToAdd = AssetDatabase.LoadAssetAtPath(_path, typeof(TowerBlueprint)) as TowerBlueprint;
                    //Debug.LogWarning("Tower to add type: " + towerToAdd.GetType());
                    blueprints[i].upgradesFrom.Add(_towerToAdd);

                    string tempPath = workingFolders[0] + "/" + blueprints[i].name + ".asset";
                    TowerBlueprint tempBlueprintsI = AssetDatabase.LoadAssetAtPath(tempPath, typeof(TowerBlueprint)) as TowerBlueprint;
                    TowerBlueprint tempTowerToAdd = _towerToAdd;
                    //move this functionallity out of bluprints[i] loop
                    
                }
                
                blueprints[i].setEditorPosition(new Vector2(nodes[i].nodeRect.x, nodes[i].nodeRect.y) + drag);
                //Debug.Log(blueprints[i].name + " Editor Position = " + blueprints[i].getEditorPosition());

                string path = workingFolders[0] + "/" + blueprints[i].name + ".asset";
                TowerBlueprint asset = AssetDatabase.LoadAssetAtPath(path, typeof(TowerBlueprint)) as TowerBlueprint;

                if (asset == null)
                {
                    AssetDatabase.CreateAsset(blueprints[i], workingFolders[0] + "/" + blueprints[i].name + ".asset");
                }
                else
                {
                    EditorUtility.CopySerialized(blueprints[i], asset);
                }
            }
            for (int i = 0; i < blueprints.Length; i++)
            {
                for (int j = 0; j < blueprints[i].upgradesFrom.Count; j++)
                {
                    for (int k = 0; k < blueprints.Length; k++)
                    {
                        if (blueprints[i].upgradesFrom[j].name == blueprints[k].name)
                        {
                            //Debug.Log(blueprints[k].name + " || " + blueprints[i].name);
                            string tempOutPath = workingFolders[0] + "/" + blueprints[k].name + ".asset";
                            string tempInPath = workingFolders[0] + "/" + blueprints[i].name + ".asset";
                            TowerBlueprint tempOutTower = AssetDatabase.LoadAssetAtPath(tempOutPath, typeof(TowerBlueprint)) as TowerBlueprint;
                            TowerBlueprint tempInTower = AssetDatabase.LoadAssetAtPath(tempInPath, typeof(TowerBlueprint)) as TowerBlueprint;
                            blueprints[k].upgradesTo.Add(tempInTower);

                            //tempTowerToAdd.upgradesTo.Add(tempBlueprintsI);
                            EditorUtility.CopySerialized(blueprints[k], tempOutTower);
                        }
                    }
                } 
            }

            Debug.Log("Towers have been saved/updated");
        }
    }

    private void DrawGrid(float gridSpacing, float gridOpacity, Color gridColor)
    {
        int widthDivs = Mathf.CeilToInt((position.width / gridSpacing) /_zoom);
        int heightDivs = Mathf.CeilToInt((position.height / gridSpacing) / _zoom);

        Handles.BeginGUI();
        Handles.color = new Color(gridColor.r, gridColor.g, gridColor.b, gridOpacity);

        offset += drag * 0.5f;
        Vector3 newOffset = new Vector3(offset.x % gridSpacing, offset.y % gridSpacing, 0);

        for (int i = 0; i < widthDivs; i++)
        {
            Handles.DrawLine(new Vector3(gridSpacing * i, -gridSpacing, 0) + newOffset, new Vector3(gridSpacing * i, _zoomArea.height/_zoom, 0f) + newOffset);
        }

        for (int j = 0; j < heightDivs; j++)
        {
            Handles.DrawLine(new Vector3(-gridSpacing, gridSpacing * j, 0) + newOffset, new Vector3(_zoomArea.width/_zoom, gridSpacing * j, 0f) + newOffset);
        }

        Handles.color = Color.white;
        Handles.EndGUI();
    }

    private void DrawNodes()
    {
        if (nodes != null)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i].Draw();
            }
        }
    }

    private void DrawConnections()
    {
        if (connections != null)
        {
            for (int i = 0; i < connections.Count; i++)
            {
                connections[i].Draw();
            }
        }
    }

    private void ProcessEvents(Event e)
    {
        drag = Vector2.zero;

        switch (e.type)
        {
            case EventType.MouseDown:
                if (e.button == 0)
                {
                    ClearConnectionSelection();
                }

                if (e.button == 1)
                {
                    ProcessContextMenu(e.mousePosition);
                }
                break;

            case EventType.MouseDrag:
                if (e.button == 0)
                {
                    OnDrag(e.delta/_zoom);
                }
                break;
        }
    }

    private void ProcessNodeEvents(Event e)
    {
        if (nodes != null)
        {
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                bool guiChanged = nodes[i].ProcessEvents(e);

                if (guiChanged)
                {
                    GUI.changed = true;
                }
            }
        }
    }

    private void DrawConnectionLine(Event e)
    {
        if (selectedInPoint != null && selectedOutPoint == null)
        {
            Handles.DrawBezier(
                selectedInPoint.rect.center,
                e.mousePosition,
                selectedInPoint.rect.center + Vector2.left * 50f,
                e.mousePosition - Vector2.left * 50f,
                Color.white,
                null,
                2f
            );

            GUI.changed = true;
        }

        if (selectedOutPoint != null && selectedInPoint == null)
        {
            Handles.DrawBezier(
                selectedOutPoint.rect.center,
                e.mousePosition,
                selectedOutPoint.rect.center - Vector2.left * 50f,
                e.mousePosition + Vector2.left * 50f,
                Color.white,
                null,
                2f
            );

            GUI.changed = true;
        }
    }

    private void ProcessContextMenu(Vector2 mousePosition)
    {
        GenericMenu genericMenu = new GenericMenu();
        genericMenu.AddItem(new GUIContent("Add new Tower"), false, () => OnClickAddNode(mousePosition, new TowerNode()));
        genericMenu.ShowAsContext();
    }

    private void OnDrag(Vector2 delta)
    {
        drag = delta;

        if (nodes != null)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i].Drag(delta);
            }
        }

        GUI.changed = true;
    }

    private void OnClickAddNode(Vector2 mousePosition, DrawableInfo info)
    {
        if (nodes == null)
        {
            nodes = new List<Node>();
        }

        nodes.Add(new Node(mousePosition, 200, 50, nodeStyle, selectedNodeStyle, inPointStyle, outPointStyle, OnClickInPoint, OnClickOutPoint, OnClickRemoveNode, info));
    }

    private void OnClickInPoint(ConnectionPoint inPoint)
    {
        selectedInPoint = inPoint;

        if (selectedOutPoint != null)
        {
            if (selectedOutPoint.node != selectedInPoint.node)
            {
                CreateConnection();
                //TowerNode tower = (TowerNode)inPoint.node.myInfo;
                //Debug.Log("I clicked an inPoint on " + inPoint.node.title);
                ClearConnectionSelection();
            }
            else
            {
                ClearConnectionSelection();
            }
        }
    }

    private void OnClickOutPoint(ConnectionPoint outPoint)
    {
        selectedOutPoint = outPoint;

        if (selectedInPoint != null)
        {
            if (selectedOutPoint.node != selectedInPoint.node)
            {
                CreateConnection();
                Debug.Log("I clicked an outPoint on " + outPoint.node.title);
                ClearConnectionSelection();
            }
            else
            {
                ClearConnectionSelection();
            }
        }
    }

    private void OnClickRemoveNode(Node node)
    {
        if (connections != null)
        {
            List<Connection> connectionsToRemove = new List<Connection>();

            for (int i = 0; i < connections.Count; i++)
            {
                if (connections[i].inPoint == node.inPoint || connections[i].outPoint == node.outPoint)
                {
                    connectionsToRemove.Add(connections[i]);
                }
            }

            for (int i = 0; i < connectionsToRemove.Count; i++)
            {
                connections.Remove(connectionsToRemove[i]);
            }

            connectionsToRemove = null;
        }

        nodes.Remove(node);
    }

    private void OnClickRemoveConnection(Connection connection)
    {
        connections.Remove(connection);
    }

    private void CreateConnection()
    {
        if (connections == null)
        {
            connections = new List<Connection>();
        }

        connections.Add(new Connection(selectedInPoint, selectedOutPoint, OnClickRemoveConnection));
    }

    private void ClearConnectionSelection()
    {
        selectedInPoint = null;
        selectedOutPoint = null;
    }
}