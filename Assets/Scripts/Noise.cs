using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Noise{

    public enum NormalizeMode {Local, Global}

    // octaves = number of passes of noise generation.
    // Persistance = how much each octave modifies the original (in range 0-1, so always decreasing per pass) (detail height)
    // lacunarity = increasing detail. (detail length/width)
    // Consider octave 1  = mountain, octave 2 = boulders, octave 3 = pebbles/pits
    public static float[,] GenerateNoiseMap(int mapWidth, int mapHeight, float scale, int seed, int octaves, float persistance, float lacunarity, Vector2 offset, NormalizeMode normalizeMode)    {
        float[,] noiseMap = new float[mapWidth, mapHeight];

        // normalize all data between 0-1 at the end.  need to keep track of highest and lowest value.
        float amplitude = 1;
        float frequency = 1;

        float maxPosHeight = 0;

        float maxLocalHeight = float.MinValue;
        float minLocalHeight = float.MaxValue;

        // seeting the seed to allow many different map types
        System.Random prng = new System.Random(seed);
        Vector2[] octaveOffsets = new Vector2[octaves];
        for (int i = 0; i<octaves; i++)        {
            float offsetX = prng.Next(-100000, 100000) + offset.x;
            float offsetY = prng.Next(-100000, 100000) - offset.y;
            octaveOffsets[i] = new Vector2(offsetX, offsetY);

            maxPosHeight += amplitude;
            amplitude *= persistance;
        }

        // confirm scale is int range
        if (scale <= 0){
            scale = 0.0001f;
        }


        float halfWidth = mapWidth / 2f;
        float halfHeight = mapHeight / 2f;

        for (int y = 0; y < mapHeight; y++)        {
            for (int x = 0; x < mapWidth; x++)            {
                amplitude = 1;
                frequency = 1;
                float noiseHeight = 0;

                for (int i=0; i < octaves; i++) {
                    float sampleX = (x-halfWidth + octaveOffsets[i].x) /scale*frequency;
                    float sampleY = (y-halfHeight + octaveOffsets[i].y)/ scale*frequency;
                    // originally set for just in the range of 0-1.
                    //float perlinValue = Mathf.PerlinNoise(sampleX, sampleY);

                    // changed to range -1 - 1, so it can increase or decrease
                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) *2 - 1;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= persistance;
                    frequency *= lacunarity;
                }

                // monitor max and min values for normalizing, then set value in map
                if (noiseHeight > maxLocalHeight) { maxLocalHeight = noiseHeight; }
                else if (noiseHeight < minLocalHeight) { minLocalHeight = noiseHeight; }

                noiseMap[x, y] = noiseHeight;
            }
        }

        // normalize the map
        for (int y = 0; y < mapHeight; y++) {
            for (int x = 0; x < mapWidth; x++) {
                if (normalizeMode == NormalizeMode.Local) {
                    noiseMap[x, y] = Mathf.InverseLerp(minLocalHeight, maxLocalHeight, noiseMap[x, y]);
                }
                else if (normalizeMode == NormalizeMode.Global) {
                    // global normalization looks pretty crappy...  be sure to come back to this part and find a better way.
                    float normalizedHeight = (noiseMap[x, y] + 1) / (2*maxPosHeight/1.4f );
                    noiseMap[x, y] = Mathf.Clamp(normalizedHeight, 0, int.MaxValue);
                }
            }
        }

        return noiseMap;
    }
}
