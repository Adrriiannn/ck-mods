Shader "ExpandNullforge/Fallback/FloorShadow" {
	Properties {
		[HideInInspector] _AlphaCutoff ("Alpha Cutoff ", Range(0, 1)) = 0.5
		[HideInInspector] _EmissionColor ("Emission Color", Vector) = (1,1,1,1)
		[ASEBegin] _Color ("Color", Vector) = (1,1,1,1)
		[Enum(Object,0,Tall,1,Short,2,Soft,3)] _Type ("Type", Float) = 0
		[NoScaleOffset] _ReplacementTex ("ReplacementTex", 2D) = "white" {}
		[NoScaleOffset] _MainTex ("MainTex", 2D) = "white" {}
		[ASEEnd] _UseReplacementTex ("UseReplacementTex", Float) = 0
		[HideInInspector] _texcoord ("", 2D) = "white" {}
		[HideInInspector] _SpriteRect ("Sprite Rect", Vector) = (0,0,0,0)
		[HideInInspector] _SpriteColor ("Sprite Color", Vector) = (1,1,1,1)
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType"="Opaque" }
		LOD 200
		CGPROGRAM
#pragma surface surf Standard
#pragma target 3.0

		sampler2D _MainTex;
		fixed4 _Color;
		struct Input
		{
			float2 uv_MainTex;
		};
		
		void surf(Input IN, inout SurfaceOutputStandard o)
		{
			fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
			o.Albedo = c.rgb;
			o.Alpha = c.a;
		}
		ENDCG
	}
	Fallback "Hidden/InternalErrorShader"
	//CustomEditor "UnityEditor.ShaderGraph.PBRMasterGUI"
}
