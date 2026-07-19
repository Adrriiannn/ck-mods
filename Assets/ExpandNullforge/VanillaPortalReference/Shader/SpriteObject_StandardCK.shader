Shader "SpriteObject/StandardCK" {
	Properties {
		_SurfaceMode ("Surface Mode", Float) = 0
		_ForwardOnly ("Forward Only", Float) = 0
		_TransparencyMode ("Transparency Mode", Float) = 0
		[ToggleOff(UNLIT_ON)] _LitOn ("Lit", Float) = 1
		[Toggle(ALPHATEST_ON)] _AlphaTestOn ("Alpha Test", Float) = 0
		[Toggle(PIVOT_DEPTH_PROJECTION)] _PivotDepthProjection ("Pivot Projection", Float) = 0
		[Toggle(THICK_OUTLINE)] _ThickOutline ("Thick Outline", Float) = 0
		[Toggle(TALL_SPRITE)] _TallSprite ("Tall Sprite", Float) = 0
		[Toggle(USE_NORMAL)] _UseNormal ("Use Normal (Lambert shading, opaque only)", Float) = 0
		_ZWrite ("Write Depth", Float) = 0
		_ZTest ("Depth Test", Float) = 4
		_Cull ("Cull", Float) = 0
		_UseVolumetricLight ("Use Volumetric Light", Float) = 0
		_FlattenVolumetricLight ("Flatten Volumetric Light", Range(0, 1)) = 0
		_FlatAltitude ("Flatten Altitude", Float) = 3
		_PitFade ("Pit Fade", Float) = 1
		_GroundingGradientHeight ("Grounding Gradient Height", Float) = 1
		_GroundingGradientStrength ("Grounding Gradient Strength", Float) = 0
		_ZSpacePivotProjection ("Z Space Pivot Projection", Float) = 0
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType" = "Opaque" }
		LOD 200
		CGPROGRAM
#pragma surface surf Standard
#pragma target 3.0

		struct Input
		{
			float2 uv_MainTex;
		};

		void surf(Input IN, inout SurfaceOutputStandard o)
		{
			o.Albedo = 1;
		}
		ENDCG
	}
	//CustomEditor "SpriteObjectStandardEditor"
}