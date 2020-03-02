using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Terrain cunk class.
// the 239x239 chunks of data that are made surrounding the middle point as needed.
public class TerrainChunk{
    public event System.Action<TerrainChunk, bool> onVisibilityChanged;
    public Vector2 coord;

    const float colliderGenerationDstThreshold = 5;

    GameObject meshObject;
    Vector2 sampleCenter;
    Bounds bounds;

    MeshRenderer meshRenderer;
    MeshFilter meshFilter;
    MeshCollider meshCollider;

    LODInfo[] detailLevels;
    LODMesh[] lodMeshes;
    int colliderLODIndex;

    HeightMap heightMap;
    bool heightMapReceived;
    int previousLODIndex = -1;

    bool hasSetCollider = false;
    float maxViewDst;

    HeightMapSettings heightMapSettings;
    MeshSettings meshSettings;
    Transform viewer;

    // Mothod to create a new terrain chunk.
    public TerrainChunk(Vector2 coord, HeightMapSettings heightMapSettings, MeshSettings meshSettings, LODInfo[] detailLevels, Transform parent, Material material, int colliderLODIndex, Transform viewer) {
        this.coord = coord;
        this.detailLevels = detailLevels;
        this.colliderLODIndex = colliderLODIndex;
        this.heightMapSettings = heightMapSettings;
        this.meshSettings = meshSettings;
        this.viewer = viewer;

        sampleCenter = coord * meshSettings.meshWorldSize / meshSettings.meshScale;
        //Vector3 positionV3 = new Vector3(sampleCenter.x, 0, sampleCenter.y);
        Vector2 position = coord * meshSettings.meshWorldSize;
        bounds = new Bounds(position, Vector2.one * meshSettings.meshWorldSize);
        maxViewDst = detailLevels[detailLevels.Length - 1].visibleDstthreshold;


        meshObject = new GameObject("Terrain Chunk " + sampleCenter);
        meshRenderer = meshObject.AddComponent<MeshRenderer>();
        meshFilter = meshObject.AddComponent<MeshFilter>();
        meshCollider = meshObject.AddComponent<MeshCollider>();
        meshRenderer.material = material;

        meshObject.transform.position = new Vector3(position.x, 0, position.y);      //previously positionV3 * mapGenerator.meshSettings.meshScale;
        meshObject.transform.parent = parent;
        //meshObject.transform.localScale = Vector3.one * mapGenerator.meshSettings.meshScale;  this is now backed into the mesh settings.
        // start with every chunk not visible, untill it loads in for the first time.
        SetVisible(false);

        // set up an array of meshes to be used at different detail.
        lodMeshes = new LODMesh[detailLevels.Length];
        for (int i = 0; i < detailLevels.Length; i++) {
            lodMeshes[i] = new LODMesh(detailLevels[i].lod, UpdateTerrainChunk);
            lodMeshes[i].updateCallback += UpdateTerrainChunk;
            if (i == colliderLODIndex) {
                lodMeshes[i].updateCallback += UpdateCollisionChunk;
            }
        }
       
    }



    public void Load() {
        //mapGenerator.RequestHeightMap(sampleCenter, OnHeightMapReceived); - deprecated after creation of threadedDataRequester
        ThreadedDataRequester.RequestData(() => HeightMapGenerator.GenerateHeightMap(meshSettings.numVertsPerLine, meshSettings.numVertsPerLine, heightMapSettings, sampleCenter), OnHeightMapReceived);
    }




    void OnHeightMapReceived(object heightMap) {
        this.heightMap = (HeightMap)heightMap;
        heightMapReceived = true;

        UpdateTerrainChunk();
    }



    Vector2 viewerPosition {
        get {
            return new Vector2(viewer.position.x, viewer.position.z);
        }
    }


    void OnMeshDataReceived(MeshData meshData) {
        meshFilter.mesh = meshData.CreateMesh();
    }

    // Update the terrain chunk.  Set its visibility and detail
    public void UpdateTerrainChunk() {
        if (heightMapReceived) {
            float viewerDstFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
            bool wasVisable = IsVisible();
            bool visible = viewerDstFromNearestEdge <= maxViewDst;

            if (visible) {

                int lodIndex = 0;

                for (int i = 0; i <= detailLevels.Length - 1; i++) {
                    if (viewerDstFromNearestEdge > detailLevels[i].visibleDstthreshold) {
                        lodIndex += 1;
                    }
                    else {
                        break;
                    }
                }

                if (lodIndex != previousLODIndex) {
                    LODMesh lodMesh = lodMeshes[lodIndex];
                    if (lodMesh.hasMesh) {
                        previousLODIndex = lodIndex;
                        meshFilter.mesh = lodMesh.mesh;
                    }
                    else if (!lodMesh.hasRequestedMesh) {
                        lodMesh.RequestMesh(heightMap, meshSettings);
                    }
                }
            }

            if (wasVisable != visible) {
                SetVisible(visible);
                if (onVisibilityChanged != null) {
                    onVisibilityChanged(this, visible);
                }
            }
        }
    }

    public void SetVisible(bool visible) {
        meshObject.SetActive(visible);
    }

    public bool IsVisible() {
        return meshObject.activeSelf;
    }



    // creation of collider will need to be checked more often than detail, hence in a seperate thread.
    // this will allow it to run later, and not have to worry about accidentally stepping off of collision mesh.
    public void UpdateCollisionChunk() {
        if (!hasSetCollider) {
            float sqrDstFromViewerToEdge = bounds.SqrDistance(viewerPosition);

            // since this is much closer than the update terrain chunk, the collision data is going to be needed much more urgently.  CALL IT NOW!
            if (sqrDstFromViewerToEdge < detailLevels[colliderLODIndex].sqrVisableDistThreshold) {
                if (!lodMeshes[colliderLODIndex].hasRequestedMesh) {
                    lodMeshes[colliderLODIndex].RequestMesh(heightMap, meshSettings);
                }
            }

            if (sqrDstFromViewerToEdge < colliderGenerationDstThreshold * colliderGenerationDstThreshold) {
                if (lodMeshes[colliderLODIndex].hasMesh) {
                    meshCollider.sharedMesh = lodMeshes[colliderLODIndex].mesh;
                    hasSetCollider = true;
                }
            }
        }
    }
}


//Level of Detail Mesh
class LODMesh {
    public Mesh mesh;
    public bool hasRequestedMesh;
    public bool hasMesh;
    int lod;

    // fixing problem.  By reducing update to run on movement, Update is not called after the mesh loads. Had to bee added to LOD mesh too.
    //System.Action updateCallback;
    // fixing problem again, this now needs to run 2 callbacks
    public event System.Action updateCallback;


    public LODMesh(int lod, System.Action updateCallback) {
        this.lod = lod;
        //this.updateCallback = updateCallback; - moved to terrain chunk creation
    }


    public void RequestMesh(HeightMap heightMap, MeshSettings meshSettings) {
        hasRequestedMesh = true;
        //mapGenerator.RequestMeshData(heightMap, lod, OnMeshDataReceived);
        ThreadedDataRequester.RequestData(() => MeshGenerator.GenerateTerrainMesh(heightMap.values, meshSettings, lod), OnMeshDataReceived);
    }


    void OnMeshDataReceived(object meshData) {
        mesh = ((MeshData)meshData).CreateMesh();
        hasMesh = true;

        updateCallback();
    }
}
