Shader "ExpandNullforge/Fallback/ShadowCastingSprite"
{
    Properties
    {
        [HideInInspector] _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [ASEEnd] [ASEBegin] [NoScaleOffset] _MainTex ("MainTex", 2D) = "white" {}
        [HideInInspector] _SpriteColor ("Sprite Color", Vector) = (1,1,1,1)
        [HideInInspector] _Color ("Color", Color) = (1,1,1,1)
        [HideInInspector] _Cutoff ("Cutoff", Range(0, 1)) = 0.5
        [HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
        [HideInInspector] _QueueControl ("_QueueControl", Float) = -1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100
        Cull Off
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _AlphaCutoff;
            float _Cutoff;
            float4 _Color;
            float4 _SpriteColor;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
                output.color = input.color * _Color * _SpriteColor;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half alpha = tex2D(_MainTex, input.uv).a * input.color.a;
                clip(alpha - max(_AlphaCutoff, _Cutoff));
                return 0;
            }
            ENDCG
        }

        Pass
        {
            Name "ShadowCasterCube"
            Tags { "LightMode" = "ShadowCasterCube" }
            ColorMask 0

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _AlphaCutoff;
            float _Cutoff;
            float4 _Color;
            float4 _SpriteColor;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
                output.color = input.color * _Color * _SpriteColor;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half alpha = tex2D(_MainTex, input.uv).a * input.color.a;
                clip(alpha - max(_AlphaCutoff, _Cutoff));
                return 0;
            }
            ENDCG
        }
    }

    Fallback Off
}
