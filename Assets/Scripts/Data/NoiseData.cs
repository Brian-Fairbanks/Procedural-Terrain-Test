using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu()]
public class NoiseData : UpdatableData {
    public Noise.NormalizeMode normalizeMode;

    public float noiseScale;

    public int octaves;
    
    // apply range slider to persistance from 0 to 1
    [Range(0, 1)]
    public float persistance;
    public float lacunarity;

    public int seed;
    public Vector2 offset;

    //================================================================================
    //  Functions
    //================================================================================

    //setting max/min values
    protected override void OnValidate() {
        if (lacunarity < 1) { lacunarity = 1; }
        if (octaves < 1) { octaves = 1; }

        base.OnValidate(); // call back parent in updatableData
    }

}
