using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Noise{

    public enum NormalizeMode {Local, Global}

    // octaves = number of passes of noise generation.
    // Persistance = how much each octave modifies the original (in range 0-1, so always decreasing per pass) (detail height)
    // lacunarity = increasing detail. (detail length/width)
    // Consider octave 1  = mountain, octave 2 = boulders, octave 3 = pebbles/pits
    public static float[,] GenerateNoiseMap(int mapWidth, int mapHeight, NoiseSettings settings, Vector2 sampleCenter)    {
        float[,] noiseMap = new float[mapWidth, mapHeight];

        // normalize all data between 0-1 at the end.  need to keep track of highest and lowest value.
        float amplitude = 1;
        float frequency = 1;

        float maxPosHeight = 0;

        float maxLocalHeight = float.MinValue;
        float minLocalHeight = float.MaxValue;

        // seeting the seed to allow many different map types
        System.Random prng = new System.Random(settings.seed);
        Vector2[] octaveOffsets = new Vector2[settings.octaves];
        for (int i = 0; i< settings.octaves; i++)        {
            float offsetX = prng.Next(-100000, 100000) + settings.offset.x + sampleCenter.x;
            float offsetY = prng.Next(-100000, 100000) - settings.offset.y + sampleCenter.y;
            octaveOffsets[i] = new Vector2(offsetX, offsetY);

            maxPosHeight += amplitude;
            amplitude *= settings.persistance;
        }

        float halfWidth = mapWidth / 2f;
        float halfHeight = mapHeight / 2f;

        for (int y = 0; y < mapHeight; y++)        {
            for (int x = 0; x < mapWidth; x++)            {
                amplitude = 1;
                frequency = 1;
                float noiseHeight = 0;

                for (int i=0; i < settings.octaves; i++) {
                    float sampleX = (x-halfWidth + octaveOffsets[i].x) / settings.scale *frequency;
                    float sampleY = (y-halfHeight + octaveOffsets[i].y)/ settings.scale *frequency;
                    // originally set for just in the range of 0-1.
                    //float perlinValue = Mathf.PerlinNoise(sampleX, sampleY);

                    // changed to range -1 - 1, so it can increase or decrease
                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) *2 - 1;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= settings.persistance;
                    frequency *= settings.lacunarity;
                }

                // monitor max and min values for normalizing, then set value in map
                if (noiseHeight > maxLocalHeight) { maxLocalHeight = noiseHeight; }
                if (noiseHeight < minLocalHeight) { minLocalHeight = noiseHeight; }

                noiseMap[x, y] = noiseHeight;

                if (settings.normalizeMode == NormalizeMode.Global) {
                    // global normalization looks pretty crappy...  be sure to come back to this part and find a better way.
                    float normalizedHeight = (noiseMap[x, y] + 1) / (maxPosHeight / 0.9f);
                    noiseMap[x, y] = Mathf.Clamp(normalizedHeight, 0, int.MaxValue);
                }
            }
        }

        // normalize the map
        if (settings.normalizeMode == NormalizeMode.Local) {
            for (int y = 0; y < mapHeight; y++) {
                for (int x = 0; x < mapWidth; x++) {
                    if (settings.normalizeMode == NormalizeMode.Local) {
                        noiseMap[x, y] = Mathf.InverseLerp(minLocalHeight, maxLocalHeight, noiseMap[x, y]);
                    }
                }
            }
        }

        return noiseMap;
    }
}


[System.Serializable]
public class NoiseSettings {
    public Noise.NormalizeMode normalizeMode;

    public float scale = 50;

    public int octaves = 6;

    // apply range slider to persistance from 0 to 1
    [Range(0, 1)]
    public float persistance = .6f;
    public float lacunarity = 2;

    public int seed;
    public Vector2 offset;


    // validate that all the values remain within specific ranges.
    public void ValidateValues() {
        scale = Mathf.Max(scale, 0.01f);
        octaves = Mathf.Max(octaves, 1);
        lacunarity = Mathf.Max(lacunarity, 1);
        persistance = Mathf.Clamp01(persistance);

    }

}