using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessTerrain : MonoBehaviour{

    //limit chunk updates to not happen every frame
    const float viewerMoveThresholdForChunkUpdate = 25f;
    const float sqrviewerMoveThresholdForChunkUpdate = viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;
    const float colliderGenerationDstThreshold = 5;

    public int colliderLODIndex;
    public LODInfo[] detailLevels;
    public static float maxViewDist;

    public Transform viewer;
    public Material mapMaterial;

    public static Vector2 viewerPosition;
    Vector2 viewerPositionOld;
    static MapGenerator mapGenerator;
    float meshWorldSize;
    int chunksVisibleInViewDistance;

    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    static List<TerrainChunk> visibleTerrainChunks = new List<TerrainChunk>();



    private void Start()    {
        maxViewDist = detailLevels[detailLevels.Length - 1].visibleDstthreshold;
        mapGenerator = FindObjectOfType<MapGenerator>();
        meshWorldSize = mapGenerator.meshSettings.meshWorldSize;
        chunksVisibleInViewDistance = Mathf.RoundToInt(maxViewDist / meshWorldSize);

        //ensure that there is no movement related problems, and world generates as soon as it starts;
        UpdateVisibleChunks();
    }



    void Update()    {
        // check if the viewer has moved a certain distance before updating the world;
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z);
        if ((viewerPositionOld - viewerPosition).sqrMagnitude > sqrviewerMoveThresholdForChunkUpdate) {
            UpdateVisibleChunks();
            viewerPositionOld = viewerPosition;
        }
        if (viewerPosition != viewerPositionOld){
            foreach (TerrainChunk chunk in visibleTerrainChunks) {
                chunk.UpdateCollisionChunk();
            }
        }
    }

    void UpdateVisibleChunks()    {
        HashSet<Vector2> alreadyUpdatedChunkCoords = new HashSet<Vector2>();
        for (int i = visibleTerrainChunks.Count-1; i >= 0; i--)        {   // starting at end of list and counting backwards, so removing sections of the list wont cause problems
            alreadyUpdatedChunkCoords.Add(visibleTerrainChunks[i].coord);
            visibleTerrainChunks[i].UpdateTerrainChunk();
        }

        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / meshWorldSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / meshWorldSize);

        for (int yOffset = -chunksVisibleInViewDistance; yOffset <= chunksVisibleInViewDistance; yOffset++)        {
            for (int xOffset = -chunksVisibleInViewDistance; xOffset <= chunksVisibleInViewDistance; xOffset++)            {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                if (!alreadyUpdatedChunkCoords.Contains(viewedChunkCoord)){
                    if (terrainChunkDictionary.ContainsKey(viewedChunkCoord)) {
                        terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
                        // this bit has been moved, since update can now be run from other locations.
                        //if (terrainChunkDictionary[viewedChunkCoord].IsVisible())                    {
                        //    terrainChunksVisibleLastUpdate.Add(terrainChunkDictionary[viewedChunkCoord]);
                        //}
                    }
                    else {
                        terrainChunkDictionary.Add(viewedChunkCoord, new TerrainChunk(viewedChunkCoord, meshWorldSize, detailLevels, transform, mapMaterial, colliderLODIndex));
                    }
                }
            }
        }
    }


    // Terrain cunk class.
    // the 239x239 chunks of data that are made surrounding the middle point as needed.
    public class TerrainChunk    {

        public Vector2 coord;

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

        // Mothod to create a new terrain chunk.
        public TerrainChunk(Vector2 coord, float meshWorldSize, LODInfo[] detailLevels, Transform parent, Material material, int colliderLODIndex)        {
            this.coord = coord;
            this.detailLevels = detailLevels;
            this.colliderLODIndex = colliderLODIndex;

            sampleCenter = coord * meshWorldSize / mapGenerator.meshSettings.meshScale;
            //Vector3 positionV3 = new Vector3(sampleCenter.x, 0, sampleCenter.y);
            Vector2 position = coord * meshWorldSize;
            bounds = new Bounds(position, Vector2.one * meshWorldSize);


            meshObject = new GameObject("Terrain Chunk "+sampleCenter);
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshCollider = meshObject.AddComponent<MeshCollider>();
            meshRenderer.material =  material;

            meshObject.transform.position = new Vector3(position.x, 0, position.y);      //previously positionV3 * mapGenerator.meshSettings.meshScale;
            meshObject.transform.parent = parent;
            //meshObject.transform.localScale = Vector3.one * mapGenerator.meshSettings.meshScale;  this is now backed into the mesh settings.
            // start with every chunk not visible, untill it loads in for the first time.
            SetVisible(false);

            // set up an array of meshes to be used at different detail.
            lodMeshes = new LODMesh[detailLevels.Length];
            for (int i = 0; i<detailLevels.Length; i++) {
                lodMeshes[i] = new LODMesh(detailLevels[i].lod, UpdateTerrainChunk);
                lodMeshes[i].updateCallback += UpdateTerrainChunk;
                if(i == colliderLODIndex) {
                    lodMeshes[i].updateCallback += UpdateCollisionChunk;
                }
            }


            mapGenerator.RequestHeightMap(sampleCenter, OnHeightMapReceived);
        }

        void OnHeightMapReceived(HeightMap heightMap) {
            this.heightMap = heightMap;
            heightMapReceived = true;

            UpdateTerrainChunk();
        }

        void OnMeshDataReceived(MeshData meshData) {
            meshFilter.mesh = meshData.CreateMesh();
        }

        // Update the terrain chunk.  Set its visibility and detail
        public void UpdateTerrainChunk() {
            if (heightMapReceived) {
                float viewerDstFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));

                bool wasVisible = IsVisible();
                bool visible = viewerDstFromNearestEdge <= maxViewDist;

                if (visible) {
                    int lodIndex = 0;

                    for (int i = 0; i < detailLevels.Length - 1; i++) {
                        if (viewerDstFromNearestEdge > detailLevels[i].visibleDstthreshold) {
                            lodIndex = i + 1;
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
                            lodMesh.RequestMesh(heightMap);
                        }
                    }


                }

                if (wasVisible != visible) {
                    if (visible) {
                        visibleTerrainChunks.Add(this);
                    }
                    else {
                        visibleTerrainChunks.Remove(this);
                    }
                    SetVisible(visible);
                }
            }
        }

        public void SetVisible(bool visible)        {
            meshObject.SetActive(visible);
        }

        public bool IsVisible()        {
            return meshObject.activeSelf;
        }



        // creation of collider will need to be checked more often than detail, hence in a seperate thread.
        // this will allow it to run later, and not have to worry about accidentally stepping off of collision mesh.
        public void UpdateCollisionChunk() {
            if (!hasSetCollider) { }
            float sqrDstFromViewerToEdge = bounds.SqrDistance(viewerPosition);

            // since this is much closer than the update terrain chunk, the collision data is going to be needed much more urgently.  CALL IT NOW!
            if (sqrDstFromViewerToEdge < detailLevels[colliderLODIndex].sqrVisibleDistThreshold) {
                if (!lodMeshes[colliderLODIndex].hasRequestedMesh) {
                    lodMeshes[colliderLODIndex].RequestMesh(heightMap);
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


        void OnMeshDataReceived(MeshData meshData) {
            mesh = meshData.CreateMesh();
            hasMesh = true;

            updateCallback();
        }

        public void RequestMesh(HeightMap heightMap) {
            hasRequestedMesh = true;
            mapGenerator.RequestMeshData(heightMap, lod, OnMeshDataReceived);
        }
    }

    [System.Serializable]
    public struct LODInfo {
        [Range(0, MeshSettings.numSupportedLODs-1)]
        public int lod;
        // the distance for each level of detail to switch between higher or lower meshes
        public float visibleDstthreshold;

        public float sqrVisibleDistThreshold {
            get{
                return visibleDstthreshold * visibleDstthreshold;
            }
        }
    }
}
