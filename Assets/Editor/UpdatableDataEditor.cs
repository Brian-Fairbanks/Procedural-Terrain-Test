﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(UpdatableData),true)]      // the true enables this to work for all derived classes, like NoiseData and TerrainData
public class UpdatableDataEditor : Editor{

    public override void OnInspectorGUI() {
        base.OnInspectorGUI();

        UpdatableData data = (UpdatableData)target;

        if (GUILayout.Button("Update")) {
            data.NotifyOfUpdatedValues ();
        }
    }
}
