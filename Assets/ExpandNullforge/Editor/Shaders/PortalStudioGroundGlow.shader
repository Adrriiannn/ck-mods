// The Portal Studio's floor pool compositor. The game adds every light's contribution over
// zero ambient, so the only honest way to preview the pool is genuinely additive blending —
// IMGUI's own DrawTexture can only alpha-blend, which would grey the floor instead of
// lighting it. The texture arrives fully baked (colour, intensity and the shipped falloff
// curve are all CPU-side, next to the profile fields), so this shader is a pure passthrough.
Shader "Hidden/ExpandNullforge/PortalStudioGroundGlow"
{
    Properties
    {
        _MainTex ("Baked Pool", 2D) = "black" {}
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend One One
        ColorMask RGB

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(tex2D(_MainTex, i.uv).rgb, 1.0);
            }
            ENDCG
        }
    }
}
