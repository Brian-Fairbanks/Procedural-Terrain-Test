using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;


public class ThreadedDataRequester : MonoBehaviour {

    static ThreadedDataRequester instance;
    Queue<ThreadInfo> dataQueue = new Queue<ThreadInfo>();



    // class set up as static, so this is now required to have it's instance set
    private void Awake() {
        instance = FindObjectOfType<ThreadedDataRequester>();
    }


    // implementing the ability to pass chunk generation to another thread
    public static void RequestData(Func<object> generateData, Action<object> callback) {
        ThreadStart threadStart = delegate {
            instance.DataThread(generateData, callback);
        };

        new Thread(threadStart).Start();
    }




    // The actual map data generation, in the thread requested from above function
    void DataThread(Func<object> generateData, Action<object> callback) {
        // doing away with this thread creating specific data sets.
        // no you are passing in a function to be called that will retrieve said data

        // HeightMap heightMap = HeightMapGenerator.GenerateHeightMap(meshSettings.numVertsPerLine, meshSettings.numVertsPerLine, heightMapSettings, center);
        object data = generateData();
        // make sure you dont have a race case
        lock (dataQueue) {
            dataQueue.Enqueue(new ThreadInfo(callback, data));
        }
    }



    void Update() {
        if (dataQueue.Count > 0) {
            for (int i = 0; i < dataQueue.Count; i++) {
                ThreadInfo threadInfo = dataQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }

    }


    //Generic struct.  this can be done for either noisemap, or for the mesh map
    struct ThreadInfo {
        public readonly Action<object> callback;
        public readonly object parameter;

        public ThreadInfo(Action<object> callback, object parameter) {
            this.callback = callback;
            this.parameter = parameter;
        }

    }
}