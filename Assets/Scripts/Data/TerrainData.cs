﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class TerrainData : UpdatableData {
    public float uniformScale = 2f;

    public bool useFlatShading;
    public bool enableFalloff;

    //mesh exclusive
    public float meshHeightMultiplier;
    public AnimationCurve meshHeightCurve;
}
