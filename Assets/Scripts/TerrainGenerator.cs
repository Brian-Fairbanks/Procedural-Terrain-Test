using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainGenerator : MonoBehaviour {

    //limit chunk updates to not happen every frame
    const float viewerMoveThresholdForChunkUpdate = 25f;
    const float sqrviewerMoveThresholdForChunkUpdate = viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;

    public int colliderLODIndex;
    public LODInfo[] detailLevels;

    public HeightMapSettings heightMapSettings;
    public MeshSettings meshSettings;
    public TextureData textureSettings;

    public Transform viewer;
    public Material mapMaterial;

    Vector2 viewerPosition;
    Vector2 viewerPositionOld;
    float meshWorldSize;
    int chunksVisibleInViewDistance;

    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    static List<TerrainChunk> visableTerrainChunks = new List<TerrainChunk>();



    private void Start() {
        
        textureSettings.ApplyToMaterial(mapMaterial);  // this is not accounting for falloff map at the moment
        textureSettings.UpdateMeshHeights(mapMaterial, heightMapSettings.minHeight, heightMapSettings.maxHeight);

        float maxViewDist = detailLevels[detailLevels.Length - 1].visibleDstthreshold;
        meshWorldSize = meshSettings.meshWorldSize;
        chunksVisibleInViewDistance = Mathf.RoundToInt(maxViewDist / meshWorldSize);

        //ensure that there is no movement related problems, and world generates as soon as it starts;
        UpdateVisibleChunks();
    }



    void Update() {
        // check if the viewer has moved a certain distance before updating the world;
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z);
        if ((viewerPositionOld - viewerPosition).sqrMagnitude > sqrviewerMoveThresholdForChunkUpdate) {
            UpdateVisibleChunks();
            viewerPositionOld = viewerPosition;
        }
        if (viewerPosition != viewerPositionOld) {
            foreach (TerrainChunk chunk in visableTerrainChunks) {
                chunk.UpdateCollisionChunk();
            }
        }
    }

    void UpdateVisibleChunks() {
        HashSet<Vector2> alreadyUpdatedChunkCoords = new HashSet<Vector2>();
        for (int i = visableTerrainChunks.Count - 1; i >= 0; i--) {   // starting at end of list and counting backwards, so removing sections of the list wont cause problems
            alreadyUpdatedChunkCoords.Add(visableTerrainChunks[i].coord);
            visableTerrainChunks[i].UpdateTerrainChunk();
        }

        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / meshWorldSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / meshWorldSize);

        for (int yOffset = -chunksVisibleInViewDistance; yOffset <= chunksVisibleInViewDistance; yOffset++) {
            for (int xOffset = -chunksVisibleInViewDistance; xOffset <= chunksVisibleInViewDistance; xOffset++) {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                if (!alreadyUpdatedChunkCoords.Contains(viewedChunkCoord)) {
                    if (terrainChunkDictionary.ContainsKey(viewedChunkCoord)) {
                        terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
                        // this bit has been moved, since update can now be run from other locations.
                        //if (terrainChunkDictionary[viewedChunkCoord].IsVisible())                    {
                        //    terrainChunksVisibleLastUpdate.Add(terrainChunkDictionary[viewedChunkCoord]);
                        //}
                    }
                    else {
                        TerrainChunk newChunk = new TerrainChunk(viewedChunkCoord, heightMapSettings, meshSettings, detailLevels, transform, mapMaterial, colliderLODIndex, viewer);
                        terrainChunkDictionary.Add(viewedChunkCoord, newChunk);
                        newChunk.onVisibilityChanged += OnTCVisibilityChanged;
                        newChunk.Load();
                    }
                }
            }
        }
    }



    void OnTCVisibilityChanged(TerrainChunk chunk, bool isVisible) {
        if (isVisible) {
            visableTerrainChunks.Add(chunk);
        }
        else {
            visableTerrainChunks.Remove(chunk);
        }
    }

}


[System.Serializable]
public struct LODInfo {
    [Range(0, MeshSettings.numSupportedLODs - 1)]
    public int lod;
    // the distance for each level of detail to switch between higher or lower meshes
    public float visibleDstthreshold;

    public float sqrVisableDistThreshold {
        get {
            return visibleDstthreshold * visibleDstthreshold;
        }
    }
}