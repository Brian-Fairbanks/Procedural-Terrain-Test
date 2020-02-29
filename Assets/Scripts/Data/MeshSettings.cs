using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class MeshSettings : UpdatableData {

    public const int numSupportedLODs = 5;
    public const int numSupportedFlatChunkSizes = 3;
    public const int numSupportedChunkSizes = 9;
    public static readonly int[] supportedChunkSizes = { 48, 72, 96, 120, 144, 168, 192, 216, 240 };
    
    public float meshScale = 2f;
    public bool useFlatShading;

    [Range(0, numSupportedChunkSizes - 1)]
    public int chunkSizeIndex;
    [Range(0, numSupportedFlatChunkSizes - 1)]
    public int chunkFlatSizeIndex;




    // Functions
    //==================================================================

    // number of verticies per line of mesh rendered at level of detail = 0
    // this number includes the 2 extra vertices when calculating normals, but excluded from final mesh.
    public int numVertsPerLine {
        get {
            return supportedChunkSizes[ (useFlatShading) ? chunkFlatSizeIndex : chunkSizeIndex ] +1;
            }
        }

    public float meshWorldSize {
        get {
            return (numVertsPerLine - 3) * meshScale;
        }
    }
}
