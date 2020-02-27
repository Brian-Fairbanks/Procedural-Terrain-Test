using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UpdatableData: ScriptableObject{

    public event System.Action OnValuesUpdated;
    public bool autoUpdate;

    protected virtual void OnValidate() {       // must be set up as protected virtual void, since noiseData also has an onValidate.  that way this one will be called in addition to the noise update
        if (autoUpdate) {
            NotifyOfUpdatedValues();
        }
    }

    // autoupdate when values are changed
    public void NotifyOfUpdatedValues() {
        if (OnValuesUpdated != null) {
            OnValuesUpdated();
        }
    }

}
