using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

public class MapGenerator : MonoBehaviour
{
    public enum DrawMode    {
        NoiseMap, ColorMap, MeshMap, FalloffMap
    };

    public DrawMode drawMode;

    public TerrainData terrainData;
    public NoiseData noiseData;
    public TextureData textureData;

    public Material terrainMaterial;

    [Range(0, MeshGenerator.numSupportedChunkSizes - 1)]
    public int chunkSizeIndex;
    [Range(0, MeshGenerator.numSupportedFlatChunkSizes - 1)]
    public int chunkFlatSizeIndex;

    [Range(0, MeshGenerator.numSupportedLODs-1)]
    public int previewLevelOfDetail;

    // automatically regenerate when changing values - passing to MapGeneraterEditor
    public bool autoUpdate;

    Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
    Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

    float[,] falloffMap;



    ///===================================================================
    /// Functions
    ///===================================================================

    void Awake() {
        textureData.ApplyToMaterial(terrainMaterial);  // this is not accounting for falloff map at the moment
        textureData.UpdateMeshHeights(terrainMaterial, terrainData.minHeight, terrainData.maxHeight);
    }


    // listens to editor, this will run when specific data points are changed, and the map needs to be updated.
    void OnValuesUpdated() {
        if (!Application.isPlaying) {
            DrawMapInEditor();
        }
    }



    //
    public void OmTextureValiuesUpdated() {
        textureData.ApplyToMaterial(terrainMaterial);
    }




    // had no idea you could switch a variable get like this.
    public int mapChunkSize {
        get {
            if (terrainData.useFlatShading) {
                return MeshGenerator.supportedFlatChunkSizes[chunkFlatSizeIndex]-1;
            }
            else {
                return MeshGenerator.supportedChunkSizes[chunkSizeIndex] - 1;
            }
        }
    }




    // check the draw mode, and draw a chunk as a preview in the editor.
    public void DrawMapInEditor()    {
        MapData mapData = generateMapData(Vector2.zero);

        textureData.ApplyToMaterial(terrainMaterial);  // this is not accounting for falloff map at the moment
        textureData.UpdateMeshHeights(terrainMaterial, terrainData.minHeight, terrainData.maxHeight);

        //locate whatever object has MapDisplay attached to it
        MapDisplay display = FindObjectOfType<MapDisplay>();

        //draw the noise texture on the object found above

        if (drawMode == DrawMode.NoiseMap)        {
            display.drawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
        }
        else if (drawMode == DrawMode.MeshMap) {
            display.drawMesh(MeshGenerator.GenerateTerrainMesh(mapData.heightMap, terrainData.meshHeightMultiplier, terrainData.meshHeightCurve, previewLevelOfDetail, terrainData.useFlatShading));
        }
        else if (drawMode == DrawMode.FalloffMap) {
            display.drawTexture(TextureGenerator.TextureFromHeightMap(FalloffGenerator.GenerateFalloffMap(mapChunkSize)));
        }
    }




    // implementing the ability to pass chunk generation to another thread
    public void RequestMapData(Vector2 center, Action<MapData> callback) {
        ThreadStart threadStart = delegate {
            MapDataThread(center, callback);
        };

        new Thread(threadStart).Start();
    }




    // The actual map data generation, in the thread requested from above function
    void MapDataThread(Vector2 center, Action<MapData> callback) {
        MapData mapData = generateMapData(center);
        // make sure you dont have a race case
        lock (mapDataThreadInfoQueue) {
            mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
        }
    }




    // Request a new chunk of Mesh.  Creates a new thread to handle this request.
    public void RequestMeshData(MapData mapData, int lod,  Action<MeshData> callback) {
        ThreadStart threadStart = delegate {
            MeshDataThread(mapData, lod, callback);
        };

        new Thread(threadStart).Start();
    }




    // The actual map data generation, in the thread requested from above function
    void MeshDataThread(MapData mapData, int lod, Action<MeshData> callback) {
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(mapData.heightMap, terrainData.meshHeightMultiplier, terrainData.meshHeightCurve, lod, terrainData.useFlatShading);
        // make sure you dont have a race case
        lock (meshDataThreadInfoQueue) {
            meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }
    }



    void Update() {
        if (mapDataThreadInfoQueue.Count > 0) {
            for (int i = 0; i < mapDataThreadInfoQueue.Count; i++) {
                MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }

        if (meshDataThreadInfoQueue.Count > 0) {
            for (int i = 0; i < meshDataThreadInfoQueue.Count; i++) {
                MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }
    }




    MapData generateMapData(Vector2 center)
    {
        // generate a noise map using the Noise class
        float[,] noiseMap = Noise.GenerateNoiseMap(mapChunkSize+2, mapChunkSize+2, noiseData.noiseScale, noiseData.seed, noiseData.octaves, noiseData.persistance, noiseData.lacunarity, center+ noiseData.offset, noiseData.normalizeMode);

        if (terrainData.enableFalloff) {
            if (falloffMap == null) {
                falloffMap = FalloffGenerator.GenerateFalloffMap(mapChunkSize + 2);
            }

            // set color of map based on regions
            for (int y = 0; y < mapChunkSize + 2; y++) {
                for (int x = 0; x < mapChunkSize + 2; x++) {
                    noiseMap[x, y] = Mathf.Clamp01(noiseMap[x, y] - falloffMap[x, y]);
                }
            }
        }

        return new MapData(noiseMap);
    }




    private void OnValidate() {

        //if a new terrain data or noise data is AppDomainSetup...
        if (noiseData != null) {
            terrainData.OnValuesUpdated -= OnValuesUpdated;
            // this will otherwise create a loop of updating, and thus needing to update again, and again, and again...
            noiseData.OnValuesUpdated += OnValuesUpdated;
        }

        if (terrainData != null) {
            terrainData.OnValuesUpdated -= OnValuesUpdated;
            // could also check and update invocation list, but this will do the same thing.
            terrainData.OnValuesUpdated += OnValuesUpdated;
        }

        if (textureData != null) {
            textureData.OnValuesUpdated -= OnValuesUpdated;
            textureData.OnValuesUpdated += OnValuesUpdated;
        }
    }




    //Generic struct.  this can be done for either noisemap, or for the mesh map
    struct MapThreadInfo<T> {
        public readonly Action<T> callback;
        public readonly T parameter;

        public MapThreadInfo(Action<T> callback, T parameter) {
            this.callback = callback;
            this.parameter = parameter;
        }

    }

}

//setting up color based on height map

public struct MapData{
    public readonly float[,] heightMap;

    public MapData(float[,] heightMap)    {
        this.heightMap = heightMap;
    }
}