Shader "ExpandNullforge/PortalFallback"
{
    Properties
    {
        [HideInInspector] _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [HideInInspector] _EmissionColor ("Emission Color", Vector) = (1,1,1,1)
        _MinUV ("MinUV", Vector) = (0,0,0,0)
        _MaxUV ("MaxUV", Vector) = (1,1,0,0)
        _OutlineColor ("OutlineColor", Vector) = (1,1,1,1)
        _UseOuter ("UseOuter", Float) = 0
        _loadingMul ("loadingMul", Float) = 1
        _emissiveStrengthMul ("emissiveStrengthMul", Float) = 0
        _activeStrength ("activeStrength", Float) = 5
        _activeHeight ("activeHeight", Float) = 0.9
        _loadingSpeed ("loadingSpeed", Float) = 1
        _emissiveColor ("emissiveColor", Vector) = (0,0.6256573,1,1)
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _GradientTex ("GradientTex", 2D) = "white" {}
        _EmissiveTex ("EmissiveTex", 2D) = "white" {}
        _MainColor ("MainColor", Vector) = (1,1,1,1)
        _ShowOutline ("ShowOutline", Float) = 0
        _OutlineExpand ("OutlineExpand", Float) = 0.035
        [HideInInspector] _texcoord ("", 2D) = "white" {}
        [HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
        [HideInInspector] _QueueControl ("_QueueControl", Float) = -1
        [HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
        [ToggleOff] [HideInInspector] _SpecularHighlights ("Specular Highlights", Float) = 1
        [ToggleOff] [HideInInspector] _EnvironmentReflections ("Environment Reflections", Float) = 1
        [ToggleOff] [HideInInspector] _ReceiveShadows ("Receive Shadows", Float) = 1
        [HideInInspector] _SpriteRect ("Sprite Rect", Vector) = (0,0,0,0)
        [HideInInspector] _SpriteColor ("Sprite Color", Vector) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off

        HLSLINCLUDE
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define OUTLINE_ALPHA_THRESHOLD 0.5

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            struct PortalSurface
            {
                float4 colorAlpha;
                float3 emission;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_EmissiveTex);
            SAMPLER(sampler_EmissiveTex);
            TEXTURE2D(_GradientTex);
            SAMPLER(sampler_GradientTex);

            float4 _MainTex_TexelSize;
            float4 _MinUV;
            float4 _MaxUV;
            float4 _OutlineColor;
            float4 _emissiveColor;
            float4 _MainColor;
            float _ShowOutline;
            float _UseOuter;
            float _loadingMul;
            float _emissiveStrengthMul;
            float _activeStrength;
            float _activeHeight;
            float _loadingSpeed;
            float _AlphaCutoff;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            float HasValidSpriteRect()
            {
                return step(0.0001, _MaxUV.x - _MinUV.x) *
                    step(0.0001, _MaxUV.y - _MinUV.y);
            }

            float IsInsideSpriteRect(float2 uv)
            {
                float rectMask = step(_MinUV.x, uv.x) *
                    step(_MinUV.y, uv.y) *
                    step(uv.x, _MaxUV.x) *
                    step(uv.y, _MaxUV.y);
                return lerp(1.0, rectMask, HasValidSpriteRect());
            }

            float4 SampleMain(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
            }

            float4 SampleEmissive(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_EmissiveTex, sampler_EmissiveTex, uv);
            }

            float SampleLoadingGradient(float distanceToWave)
            {
                float normalizedDistance = saturate(distanceToWave);
                float mathFalloff = 1.0 - smoothstep(0.0, 1.0, normalizedDistance);
                float startAlpha = SAMPLE_TEXTURE2D(
                    _GradientTex,
                    sampler_GradientTex,
                    float2(0.5, 0.0)).a;
                float endAlpha = SAMPLE_TEXTURE2D(
                    _GradientTex,
                    sampler_GradientTex,
                    float2(0.5, 1.0)).a;
                float gradientUv = startAlpha >= endAlpha
                    ? normalizedDistance
                    : 1.0 - normalizedDistance;
                float sampleAlpha = SAMPLE_TEXTURE2D(
                    _GradientTex,
                    sampler_GradientTex,
                    float2(0.5, gradientUv)).a;
                float minAlpha = min(startAlpha, endAlpha);
                float maxAlpha = max(startAlpha, endAlpha);
                float textureFalloff =
                    saturate((sampleAlpha - minAlpha) / max(maxAlpha - minAlpha, 0.0001));
                float hasUsableGradient = step(0.01, maxAlpha - minAlpha);
                return lerp(mathFalloff, textureFalloff, hasUsableGradient);
            }

            float LoadingWaveStrength(float2 uv)
            {
                float loading = saturate(_loadingMul);
                if (loading <= 0.001)
                {
                    return 0.0;
                }

                float activeHeight = max(_activeHeight, 0.01);
                float normalizedY = saturate(uv.y / activeHeight);
                float waveY = frac(_Time.y * 0.65 * max(_loadingSpeed, 0.01));
                float bandWidth = 0.18;
                float distanceToWave = abs(normalizedY - waveY) / bandWidth;
                float wave = SampleLoadingGradient(distanceToWave);
                return loading * wave;
            }

            float SampleSpriteAlpha(float2 uv)
            {
                return SampleMain(uv).a * IsInsideSpriteRect(uv);
            }

            float SolidSpriteAlpha(float2 uv)
            {
                return step(OUTLINE_ALPHA_THRESHOLD, SampleSpriteAlpha(uv));
            }

            float NeighbourSolidAlpha(float2 uv)
            {
                float2 px = _MainTex_TexelSize.xy;
                float alpha = 0.0;
                alpha = max(alpha, SolidSpriteAlpha(uv + float2( px.x, 0.0)));
                alpha = max(alpha, SolidSpriteAlpha(uv + float2(-px.x, 0.0)));
                alpha = max(alpha, SolidSpriteAlpha(uv + float2(0.0,  px.y)));
                alpha = max(alpha, SolidSpriteAlpha(uv + float2(0.0, -px.y)));
                alpha = max(alpha, SolidSpriteAlpha(uv + float2( px.x,  px.y)));
                alpha = max(alpha, SolidSpriteAlpha(uv + float2(-px.x,  px.y)));
                alpha = max(alpha, SolidSpriteAlpha(uv + float2( px.x, -px.y)));
                alpha = max(alpha, SolidSpriteAlpha(uv + float2(-px.x, -px.y)));
                return alpha;
            }

            float MinCardinalNeighbourSolidAlpha(float2 uv)
            {
                float2 px = _MainTex_TexelSize.xy;
                float alpha = 1.0;
                alpha = min(alpha, SolidSpriteAlpha(uv + float2( px.x, 0.0)));
                alpha = min(alpha, SolidSpriteAlpha(uv + float2(-px.x, 0.0)));
                alpha = min(alpha, SolidSpriteAlpha(uv + float2(0.0,  px.y)));
                alpha = min(alpha, SolidSpriteAlpha(uv + float2(0.0, -px.y)));
                return alpha;
            }

            PortalSurface GetPortalSurface(Varyings input)
            {
                PortalSurface surface = (PortalSurface)0;
                surface.colorAlpha = SampleMain(input.uv);
                surface.colorAlpha.rgb *= _MainColor.rgb;
                surface.colorAlpha.a *= _MainColor.a;
                surface.colorAlpha.a *= IsInsideSpriteRect(input.uv);
                surface.emission = 0.0;

                float outlineApplied = 0.0;
                if (_ShowOutline > 0.5)
                {
                    float centerSolid =
                        step(OUTLINE_ALPHA_THRESHOLD, surface.colorAlpha.a);
                    if (_UseOuter > 0.5)
                    {
                        float neighbourAlpha = NeighbourSolidAlpha(input.uv);
                        if (centerSolid <= 0.0 && neighbourAlpha > 0.0)
                        {
                            surface.colorAlpha = _OutlineColor;
                            surface.emission = _OutlineColor.rgb * _OutlineColor.a;
                            outlineApplied = 1.0;
                        }
                    }
                    else if (centerSolid > 0.0 &&
                        MinCardinalNeighbourSolidAlpha(input.uv) <= 0.0)
                    {
                        float outlineAlpha = _OutlineColor.a * surface.colorAlpha.a;
                        surface.colorAlpha = float4(_OutlineColor.rgb, outlineAlpha);
                        surface.emission = _OutlineColor.rgb * outlineAlpha;
                        outlineApplied = 1.0;
                    }
                }

                if (outlineApplied < 0.5 && surface.colorAlpha.a > 0.001)
                {
                    float4 emissive = SampleEmissive(input.uv) * _emissiveColor;
                    float activeEmission = saturate(_emissiveStrengthMul);
                    float loadingEmission = LoadingWaveStrength(input.uv) * 0.7;
                    float emissiveStrength = max(activeEmission, loadingEmission);
                    surface.emission += emissive.rgb *
                        emissive.a *
                        emissiveStrength *
                        max(_activeStrength, 0.0);
                }

                return surface;
            }
        ENDHLSL

        Pass
        {
            Name "GBuffer"
            Tags { "LightMode" = "UniversalGBuffer" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragGBuffer

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"

            FragmentOutput FragGBuffer(Varyings input)
            {
                PortalSurface surface = GetPortalSurface(input);
                clip(surface.colorAlpha.a - max(_AlphaCutoff, 0.001));

                FragmentOutput output = (FragmentOutput)0;
                float3 normalWS = input.normalWS;
                normalWS = dot(normalWS, normalWS) > 0.0001
                    ? normalize(normalWS)
                    : float3(0.0, 0.0, 1.0);

                output.GBuffer0.xyz = surface.colorAlpha.rgb;
                output.GBuffer1.xyz = normalWS * 0.5 + 0.5;
                output.GBuffer1.w = 1.0 / 3.0;
                output.GBuffer2.xyz = surface.emission;
                return output;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragShadowCaster

            float4 FragShadowCaster(Varyings input) : SV_TARGET
            {
                PortalSurface surface = GetPortalSurface(input);
                clip(surface.colorAlpha.a - max(_AlphaCutoff, 0.001));
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/InternalErrorShader"
}
