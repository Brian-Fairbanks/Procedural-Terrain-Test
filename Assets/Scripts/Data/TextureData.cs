using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[CreateAssetMenu()]
public class TextureData : UpdatableData
{

    public Layer[] layers;

    float savedMinHeight;
    float savedMaxHeight;



    public void ApplyToMaterial(Material material) {

        material.SetInt("layerCount", layers.Length);
        material.SetColorArray("baseColors", layers.Select(x => x.tint).ToArray());
        material.SetFloatArray("baseStartHeights", layers.Select(x => x.startHeight).ToArray());
        material.SetFloatArray("baseBlends", layers.Select(x => x.blendStrength).ToArray());
        material.SetFloatArray("baseColorStrengths", layers.Select(x => x.tintStrength).ToArray());
        material.SetFloatArray("baseTextureScales", layers.Select(x => x.textureScale).ToArray());

        // adding this, along with the saved details to maintain this information after changes are made to the shader.
        //UpdateMeshHeights(material, savedMinHeight, savedMaxHeight);
    }


    public void UpdateMeshHeights(Material material, float minHeight, float maxHeight) {
        savedMaxHeight = maxHeight;
        savedMinHeight = minHeight;

        material.SetFloat("minHeight", minHeight);
        material.SetFloat("maxHeight", maxHeight);
    }

    [System.Serializable]
    public class Layer {
        public Texture texture;
        public Color tint;
        [Range(0,1)]
        public float tintStrength;
        [Range(0, 1)]
        public float startHeight;
        [Range(0, 1)]
        public float blendStrength;
        public float textureScale;
    }
}
