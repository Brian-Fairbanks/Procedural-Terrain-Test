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
    public Noise.NormalizeMode normalizeMode;

    public float scale = 1f;

    public const int mapChunkSize = 239;
    [Range(0, 6)]
    public int previewLevelOfDetail;
    public bool enableFalloff;
    public float noiseScale;
    public int octaves;
    // apply range slider to persistance from 0 to 1
    [Range(0,1)]
    public float persistance;
    public float lacunarity;
    public int seed;
    public Vector2 offset;

    //mesh exclusive
    public float meshHeightMultiplier;
    public AnimationCurve meshHeightCurve;

    // automatically regenerate when changing values - passing to MapGeneraterEditor
    public bool autoUpdate;

    public TerrainType[] regions;

    Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
    Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

    float[,] falloffMap;

    ///
    /// Functions
    /// 

    void Awake() {
        falloffMap = FalloffGenerator.GenerateFalloffMap(mapChunkSize);
    }

    public void DrawMapInEditor()    {
        MapData mapData = generateMapData(Vector2.zero);

        //locate whatever object has MapDisplay attached to it
        MapDisplay display = FindObjectOfType<MapDisplay>();

        //draw the noise texture on the object found above

        if (drawMode == DrawMode.NoiseMap)        {
            display.drawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
        }
        else if (drawMode == DrawMode.ColorMap)        {
            display.drawTexture(TextureGenerator.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize));
        }
        else if (drawMode == DrawMode.MeshMap) {
            display.drawMesh(MeshGenerator.GenerateTerrainMesh(mapData.heightMap, meshHeightMultiplier, meshHeightCurve, previewLevelOfDetail), TextureGenerator.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize));
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
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(mapData.heightMap, meshHeightMultiplier, meshHeightCurve, lod);
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
        float[,] noiseMap = Noise.GenerateNoiseMap(mapChunkSize+2, mapChunkSize+2, noiseScale, seed, octaves, persistance, lacunarity, center+offset, normalizeMode);

        // set color of map based on regions
        Color[] colorMap = new Color[mapChunkSize * mapChunkSize];
        for(int y=0; y < mapChunkSize; y++)        {
            for (int x=0; x<mapChunkSize; x++)            {
                if (enableFalloff) {
                    noiseMap[x, y] = Mathf.Clamp01(noiseMap[x, y] - falloffMap[x, y]);
                }
                float currentHeight = noiseMap[x, y];
                for (int i=0; i < regions.Length; i++)                {
                    if (currentHeight >= regions[i].height){
                        colorMap[y * mapChunkSize + x] = regions[i].color;
                    }
                    else {
                        break;
                    }
                }
            }
        }
        return new MapData(noiseMap, colorMap);
    }


    //setting max/min values
    private void OnValidate()    {
        if (lacunarity < 1) { lacunarity = 1; }
        if (octaves < 1) { octaves = 1; }

        falloffMap = FalloffGenerator.GenerateFalloffMap(mapChunkSize);
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
[System.Serializable]
public struct TerrainType{
    public float height;
    public Color color;
    public string name;
}


public struct MapData{
    public readonly float[,] heightMap;
    public readonly Color[] colorMap;

    public MapData(float[,] heightMap, Color[] colorMap)    {
        this.heightMap = heightMap;
        this.colorMap = colorMap;
    }
}