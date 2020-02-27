using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor (typeof (MapGenerator))]
public class MapGeneraterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        MapGenerator mapGen = (MapGenerator)target;

        // changing this to autogenerate
        //        DrawDefaultInspector();

        if (DrawDefaultInspector()) // if anything has changed
        {
            if (mapGen.autoUpdate) // and auto update enabled
            {
                mapGen.DrawMapInEditor(); // generate map
            }
        }

        // generate the noise map whenever the generate button is clicked.
        if (GUILayout.Button("Generate"))
        {
            mapGen.DrawMapInEditor();
        }
    }
}
