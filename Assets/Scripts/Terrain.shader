﻿// note, this language is CG

Shader "Custom/Terrain"
{
	Properties{
	//
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

		const static int maxColorCount = 8;

		int baseColorCount;
		float3 baseColors[maxColorCount];
		float baseStartHeights[maxColorCount];



        struct Input
        {
			float3 worldPos;
        };
		//


		float inverseLerp(float a, float b, float value) {
			return saturate((value - a) / (b - a));		// saturate clamps between 0 and 1
		}



        void surf (Input IN, inout SurfaceOutputStandard o)
        {
			float heightPercent = inverseLerp(minHeight, maxHeight, IN.worldPos.y);
			for (int i = 0; i<baseColorCount; i++){
				float drawStrength = saturate(sign(heightPercent - baseStartHeights[i]));
				//o.Albedo = heightPercent;
				o.Albedo = (o.Albedo *(1-drawStrength)) + (baseColors[i] * drawStrength);
			}
        }
        ENDCG
    }
    FallBack "Diffuse"
}