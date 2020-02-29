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

    public MeshSettings meshSettings;
    public HeightMapSettings heightMapSettings;
    public TextureData textureData;

    public Material terrainMaterial;

    [Range(0, MeshSettings.numSupportedLODs-1)]
    public int previewLevelOfDetail;

    // automatically regenerate when changing values - passing to MapGeneraterEditor
    public bool autoUpdate;

    Queue<MapThreadInfo<HeightMap>> heightMapThreadInfoQueue = new Queue<MapThreadInfo<HeightMap>>();
    Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

    float[,] falloffMap;



    ///===================================================================
    /// Functions
    ///===================================================================

    void Start() {
        textureData.ApplyToMaterial(terrainMaterial);  // this is not accounting for falloff map at the moment
        textureData.UpdateMeshHeights(terrainMaterial, heightMapSettings.minHeight, heightMapSettings.maxHeight);
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


    // check the draw mode, and draw a chunk as a preview in the editor.
    public void DrawMapInEditor()    {
        
        textureData.ApplyToMaterial(terrainMaterial);  // this is not accounting for falloff map at the moment
        textureData.UpdateMeshHeights(terrainMaterial, heightMapSettings.minHeight, heightMapSettings.maxHeight);

        HeightMap heightMap = HeightMapGenerator.GenerateHeightMap(meshSettings.numVertsPerLine, meshSettings.numVertsPerLine, heightMapSettings, Vector2.zero);

        //locate whatever object has MapDisplay attached to it
        MapDisplay display = FindObjectOfType<MapDisplay>();

        //draw the noise texture on the object found above

        if (drawMode == DrawMode.NoiseMap)        {
            display.drawTexture(TextureGenerator.TextureFromHeightMap(heightMap.values));
        }
        else if (drawMode == DrawMode.MeshMap) {
            display.drawMesh(MeshGenerator.GenerateTerrainMesh(heightMap.values, meshSettings, previewLevelOfDetail));
        }
        else if (drawMode == DrawMode.FalloffMap) {
            display.drawTexture(TextureGenerator.TextureFromHeightMap(FalloffGenerator.GenerateFalloffMap(meshSettings.numVertsPerLine)));
        }
    }




    // implementing the ability to pass chunk generation to another thread
    public void RequestHeightMap(Vector2 center, Action<HeightMap> callback) {
        ThreadStart threadStart = delegate {
            HeightMapThread(center, callback);
        };

        new Thread(threadStart).Start();
    }




    // The actual map data generation, in the thread requested from above function
    void HeightMapThread(Vector2 center, Action<HeightMap> callback) {
        HeightMap heightMap = HeightMapGenerator.GenerateHeightMap(meshSettings.numVertsPerLine, meshSettings.numVertsPerLine, heightMapSettings, center);
        // make sure you dont have a race case
        lock (heightMapThreadInfoQueue) {
            heightMapThreadInfoQueue.Enqueue(new MapThreadInfo<HeightMap>(callback, heightMap));
        }
    }




    // Request a new chunk of Mesh.  Creates a new thread to handle this request.
    public void RequestMeshData(HeightMap heightMap, int lod,  Action<MeshData> callback) {
        ThreadStart threadStart = delegate {
            MeshDataThread(heightMap, lod, callback);
        };

        new Thread(threadStart).Start();
    }




    // The actual map data generation, in the thread requested from above function
    void MeshDataThread(HeightMap heightMap, int lod, Action<MeshData> callback) {
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(heightMap.values, meshSettings, lod);
        // make sure you dont have a race case
        lock (meshDataThreadInfoQueue) {
            meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }
    }



    void Update() {
        if (heightMapThreadInfoQueue.Count > 0) {
            for (int i = 0; i < heightMapThreadInfoQueue.Count; i++) {
                MapThreadInfo<HeightMap> threadInfo = heightMapThreadInfoQueue.Dequeue();
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



    private void OnValidate() {

        //if a new terrain data or noise data is AppDomainSetup...
        if (heightMapSettings != null) {
            meshSettings.OnValuesUpdated -= OnValuesUpdated;
            // this will otherwise create a loop of updating, and thus needing to update again, and again, and again...
            heightMapSettings.OnValuesUpdated += OnValuesUpdated;
        }

        if (meshSettings != null) {
            meshSettings.OnValuesUpdated -= OnValuesUpdated;
            // could also check and update invocation list, but this will do the same thing.
            meshSettings.OnValuesUpdated += OnValuesUpdated;
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
