Shader "Amplify/Emissive"
{
    Properties
    {
        [HideInInspector] _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [HideInInspector] _EmissionColor ("Emission Color", Vector) = (1,1,1,1)
        [HDR] _EmissiveColor ("EmissiveColor", Vector) = (0,0.4874069,2.270603,1)
        _Emissive ("Emissive", Float) = 1
        _useAdditive ("useAdditive", Float) = 0
        [NoScaleOffset] _ReplacementTex ("ReplacementTex", 2D) = "white" {}
        [NoScaleOffset] _MainTex ("MainTex", 2D) = "white" {}
        _UseReplacementTex ("UseReplacementTex", Float) = 0
        [HideInInspector] _SpriteRect ("Sprite Rect", Vector) = (0,0,0,0)
        [HideInInspector] _SpriteColor ("Sprite Color", Vector) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _ReplacementTex;
            fixed4 _EmissiveColor;
            float _Emissive;
            float _UseReplacementTex;
            float _useAdditive;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 baseColor = tex2D(_MainTex, i.uv);
                fixed4 replacement = tex2D(_ReplacementTex, i.uv);
                fixed useReplacement = step(0.5, _UseReplacementTex);
                fixed4 color = lerp(baseColor, replacement, useReplacement) * i.color;
                color.rgb *= _EmissiveColor.rgb * max(_Emissive, 0.0);
                color.a *= _EmissiveColor.a;
                color.a = lerp(color.a, saturate(color.a * max(_Emissive, 1.0)), step(0.5, _useAdditive));
                return color;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
