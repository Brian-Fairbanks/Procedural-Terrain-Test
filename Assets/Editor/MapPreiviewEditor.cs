using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor (typeof (MapPreview))]
public class MapPreiviewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        MapPreview mapGen = (MapPreview)target;

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
