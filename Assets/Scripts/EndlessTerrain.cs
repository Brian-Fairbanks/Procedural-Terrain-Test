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
    int chunkSize;
    int chunksVisibleInViewDistance;

    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    static List<TerrainChunk> terrainChunksVisibleLastUpdate = new List<TerrainChunk>();



    private void Start()    {
        maxViewDist = detailLevels[detailLevels.Length - 1].visibleDstthreshold;
        mapGenerator = FindObjectOfType<MapGenerator>();
        chunkSize = mapGenerator.mapChunkSize - 1;
        chunksVisibleInViewDistance = Mathf.RoundToInt(maxViewDist / chunkSize);

        //ensure that there is no movement related problems, and world generates as soon as it starts;
        UpdateVisibleChunks();
    }



    void Update()    {
        // check if the viewer has moved a certain distance before updating the world;
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z)/mapGenerator.terrainData.uniformScale;
        if ((viewerPositionOld - viewerPosition).sqrMagnitude > sqrviewerMoveThresholdForChunkUpdate) {
            UpdateVisibleChunks();
            viewerPositionOld = viewerPosition;
        }
        if (viewerPosition != viewerPositionOld){
            foreach (TerrainChunk chunk in terrainChunksVisibleLastUpdate) {
                chunk.UpdateCollisionChunk();
            }
        }
    }

    void UpdateVisibleChunks()    {
        for (int i = 0; i < terrainChunksVisibleLastUpdate.Count; i++)        {
            terrainChunksVisibleLastUpdate[i].SetVisible(false);
        }
        terrainChunksVisibleLastUpdate.Clear();

        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

        for (int yOffset = -chunksVisibleInViewDistance; yOffset < chunksVisibleInViewDistance; yOffset++)        {
            for (int xOffset = -chunksVisibleInViewDistance; xOffset < chunksVisibleInViewDistance; xOffset++)            {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                if (terrainChunkDictionary.ContainsKey(viewedChunkCoord))                {
                    terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
                 // this bit needs to be moved, since update can now be run from other locations.
                    //if (terrainChunkDictionary[viewedChunkCoord].IsVisible())                    {
                    //    terrainChunksVisibleLastUpdate.Add(terrainChunkDictionary[viewedChunkCoord]);
                    //}
                }
                else                {
                    terrainChunkDictionary.Add(viewedChunkCoord, new TerrainChunk(viewedChunkCoord, chunkSize, detailLevels, transform, mapMaterial, colliderLODIndex));
                }
            }
        }
    }


    // Terrain cunk class.
    // the 239x239 chunks of data that are made surrounding the middle point as needed.
    public class TerrainChunk    {
        GameObject meshObject;
        Vector2 position;
        Bounds bounds;

        MeshRenderer meshRenderer;
        MeshFilter meshFilter;
        MeshCollider meshCollider;

        LODInfo[] detailLevels;
        LODMesh[] lodMeshes;
        int colliderLODIndex;

        MapData mapData;
        bool mapDataReceived;
        int previousLODIndex = -1;

        bool hasSetCollider = false;

        // Mothod to create a new terrain chunk.
        public TerrainChunk(Vector2 coord, int size, LODInfo[] detailLevels, Transform parent, Material material, int colliderLODIndex)        {
            this.detailLevels = detailLevels;
            this.colliderLODIndex = colliderLODIndex;

            position = coord * size;
            bounds = new Bounds(position, Vector2.one * size);
            Vector3 positionV3 = new Vector3(position.x, 0, position.y);

            meshObject = new GameObject("Terrain Chunk "+position);
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshCollider = meshObject.AddComponent<MeshCollider>();
            meshRenderer.material =  material;

            meshObject.transform.position = positionV3 * mapGenerator.terrainData.uniformScale;
            meshObject.transform.parent = parent;
            meshObject.transform.localScale = Vector3.one * mapGenerator.terrainData.uniformScale;
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


            mapGenerator.RequestMapData(position, OnMapDataReceived);
        }

        void OnMapDataReceived(MapData mapData) {
            this.mapData = mapData;
            mapDataReceived = true;

            UpdateTerrainChunk();
        }

        void OnMeshDataReceived(MeshData meshData) {
            meshFilter.mesh = meshData.CreateMesh();
        }

        // Update the terrain chunk.  Set its visibility and detail
        public void UpdateTerrainChunk()        {
            if (mapDataReceived) { 
                float viewerDstFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
                bool visible = viewerDstFromNearestEdge <= maxViewDist;

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
                            lodMesh.RequestMesh(mapData);
                        }
                    }
                    // add to visible last update
                    terrainChunksVisibleLastUpdate.Add(this);
                }
                SetVisible(visible);
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
            if (sqrDstFromViewerToEdge < detailLevels[colliderLODIndex].sqrVisableDistThreshold) {
                if (!lodMeshes[colliderLODIndex].hasRequestedMesh) {
                    lodMeshes[colliderLODIndex].RequestMesh(mapData);
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

        public void RequestMesh(MapData mapData) {
            hasRequestedMesh = true;
            mapGenerator.RequestMeshData(mapData, lod, OnMeshDataReceived);
        }
    }

    [System.Serializable]
    public struct LODInfo {
        public int lod;
        // the distance for each level of detail to switch between higher or lower meshes
        public float visibleDstthreshold;

        public float sqrVisableDistThreshold {
            get{
                return visibleDstthreshold * visibleDstthreshold;
            }
        }
    }
}
