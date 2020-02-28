// note, this language is CG

Shader "Custom/Terrain"
{
	Properties{
		testTexture("Texture",2D) = "white"{}
		testScale("Scale",Float) = 1
	}
	SubShader	{
		Tags { "RenderType" = "Opaque" }
		LOD 200

		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		float minHeight;
		float maxHeight;

		const static int maxLayerCount = 8;
		const static float epsilon = 1E-4;

		int layerCount;
		float3 baseColors[maxLayerCount];
		float baseStartHeights[maxLayerCount];
		float baseBlends[maxLayerCount];
		float baseColorStrengths[maxLayerCount];
		float baseTextureScales[maxLayerCount];
		

		sampler2D testTexture;
		float testScale;

		UNITY_DECLARE_TEX2DARRAY(baseTextures);


        struct Input
        {
			float3 worldPos;
			float3 worldNormal;
        };
		//


		float inverseLerp(float a, float b, float value) {
			return saturate((value - a) / (b - a));		// saturate clamps between 0 and 1
		}



		float3 triPlaner(float3 worldPos, float3 scale, float3 blendAxes, int textureIndex){

			//o.Albedo = tex2D(testTexture, IN.worldPos.xz / testScale);	//XZ coordinates look fine on flat surfaces, but stretch when scaling up mountains
			//o.Albedo = tex2D(testTexture, IN.worldPos.xy / testScale);	//XY looks fine going up mountains, but stretches off into distance along y axis

			//Tri-Planer Maping will correct the above problem
			float3 scaledWorldPos = worldPos / scale;

			//float3 xProjection = tex2D(testTexture, scaledWorldPos.yz) * blendAxes.x;
			//float3 yProjection = tex2D(testTexture, scaledWorldPos.xz) * blendAxes.y;
			//float3 zProjection = tex2D(testTexture, scaledWorldPos.xy) * blendAxes.z;
			// these were reworked since we are using a texture ARRAY now...
			float3 xProjection = UNITY_SAMPLE_TEX2DARRAY(baseTextures, float3(scaledWorldPos.y, scaledWorldPos.z, textureIndex)) * blendAxes.x;
			float3 yProjection = UNITY_SAMPLE_TEX2DARRAY(baseTextures, float3(scaledWorldPos.x, scaledWorldPos.z, textureIndex)) * blendAxes.y;
			float3 zProjection = UNITY_SAMPLE_TEX2DARRAY(baseTextures, float3(scaledWorldPos.x, scaledWorldPos.y, textureIndex)) * blendAxes.z;

			
			
			// adding these 3 together can possibly excede color values of 1, making it brighter.  This is corrected by averaging the blendAxes in the surf function before calculating projections
			return xProjection + yProjection + zProjection;
		}



        void surf (Input IN, inout SurfaceOutputStandard o)        {
			float3 blendAxes = abs(IN.worldNormal);
			blendAxes / blendAxes.x + blendAxes.y + blendAxes.z;
			float heightPercent = inverseLerp(minHeight, maxHeight, IN.worldPos.y);

			for (int i = 0; i< layerCount; i++){
				float drawStrength = inverseLerp(-baseBlends[i]/2 - epsilon, baseBlends[i]/2, heightPercent - baseStartHeights[i]);

				float3 baseColor = baseColors[i] * baseColorStrengths[i];
				float3 textureColor = triPlaner(IN.worldPos, baseTextureScales[i], blendAxes, i) * (1- baseColorStrengths[i]);

				o.Albedo = (o.Albedo *(1-drawStrength)) + (baseColor+textureColor) * drawStrength;
			}
        }
        ENDCG
    }
    FallBack "Diffuse"
}
