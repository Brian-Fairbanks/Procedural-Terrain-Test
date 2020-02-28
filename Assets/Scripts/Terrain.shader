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

		const static int maxColorCount = 8;
		const static float epsilon = 1E-4;

		int baseColorCount;
		float3 baseColors[maxColorCount];
		float baseStartHeights[maxColorCount];
		float baseBlends[maxColorCount];

		sampler2D testTexture;
		float testScale;


        struct Input
        {
			float3 worldPos;
			float3 worldNormal;
        };
		//


		float inverseLerp(float a, float b, float value) {
			return saturate((value - a) / (b - a));		// saturate clamps between 0 and 1
		}



        void surf (Input IN, inout SurfaceOutputStandard o)
        {
			float heightPercent = inverseLerp(minHeight, maxHeight, IN.worldPos.y);
			for (int i = 0; i<baseColorCount; i++){
				float drawStrength = inverseLerp(-baseBlends[i]/2 - epsilon, baseBlends[i]/2, heightPercent - baseStartHeights[i]);
				o.Albedo = (o.Albedo *(1-drawStrength)) + (baseColors[i] * drawStrength);
			}

			//o.Albedo = tex2D(testTexture, IN.worldPos.xz / testScale);	//XZ coordinates look fine on flat surfaces, but stretch when scaling up mountains
			//o.Albedo = tex2D(testTexture, IN.worldPos.xy / testScale);	//XY looks fine going up mountains, but stretches off into distance along y axis

			//Tri-Planer Maping will correct the above problem
			float3 scaledWorldPos = IN.worldPos / testScale;
			float3 blendAxes = abs(IN.worldNormal);
			blendAxes / blendAxes.x + blendAxes.y + blendAxes.z;
			float3 xProjection = tex2D(testTexture, scaledWorldPos.yz) * blendAxes.x;
			float3 yProjection = tex2D(testTexture, scaledWorldPos.xz) * blendAxes.y;
			float3 zProjection = tex2D(testTexture, scaledWorldPos.xy) * blendAxes.z;
			// adding these 3 together can possibly excede color values of 1, making it brighter.  This is corrected by averaging the blendAxes before calculating projections
			o.Albedo = xProjection + yProjection + zProjection;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
