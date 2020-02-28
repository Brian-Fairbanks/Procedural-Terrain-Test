using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UpdatableData: ScriptableObject{

    public event System.Action OnValuesUpdated;
    public bool autoUpdate;


    // preprocessor directive --
#if UNITY_EDITOR
    protected virtual void OnValidate() {       // must be set up as protected virtual void, since noiseData also has an onValidate.  that way this one will be called in addition to the noise update
        if (autoUpdate) {
            UnityEditor.EditorApplication.update += NotifyOfUpdatedValues;

        }
    }

    // autoupdate when values are changed
    public void NotifyOfUpdatedValues() {
        UnityEditor.EditorApplication.update -= NotifyOfUpdatedValues;
        if (OnValuesUpdated != null) {
            OnValuesUpdated();
        }
    }
#endif

}
