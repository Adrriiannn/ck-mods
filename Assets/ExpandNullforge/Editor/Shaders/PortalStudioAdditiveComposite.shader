Shader "Hidden/ExpandNullforge/PortalStudioAdditiveComposite"
{
    Properties
    {
        _MainTex ("Particle Preview", 2D) = "black" {}
        // These editor-only defaults are measured against the supplied vanilla capture.
        // They preserve the source particle footprint; only the missing PugRP display
        // response is reconstructed here.
        _CoreGain ("Core gain", Float) = 1.22
        _HighlightMix ("White highlight mix", Range(0, 1)) = 0.28
        _HighlightThreshold ("White highlight threshold", Range(0, 1)) = 0.60
        _BloomThreshold ("Isolated bloom threshold", Range(0, 1)) = 0.60
        _OnePixelHalo ("One-pixel halo", Range(0, 1)) = 0.32
        _TwoPixelHalo ("Two-pixel halo", Range(0, 1)) = 0.06
        _ApertureCenterPixels ("Aperture center (native pixels)", Vector) = (24, 25, 0, 0)
        _ApertureRadiusPixels ("Aperture radius (native pixels)", Vector) = (5.5, 7.5, 0, 0)
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
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            half _CoreGain;
            half _HighlightMix;
            half _HighlightThreshold;
            half _BloomThreshold;
            half _OnePixelHalo;
            half _TwoPixelHalo;
            float2 _ApertureCenterPixels;
            float2 _ApertureRadiusPixels;

            half Peak(half3 color)
            {
                return max(color.r, max(color.g, color.b));
            }

            half3 ExtractBloom(float2 uv)
            {
                half3 color = tex2D(_MainTex, uv).rgb;
                half peak = Peak(color);
                // Core Keeper's in-game camera extracts bloom above 1.9 from the combined
                // HDR scene. Portal Studio supplies this pass only the isolated particle
                // layer, so the capture-calibrated 0.60 threshold reconstructs the same
                // one-pixel response without enlarging the source geometry. Retain the
                // source hue and carry only the over-threshold energy into the halo.
                half bloomWeight = saturate((peak - _BloomThreshold) / max(peak, 0.0001h));
                return color * bloomWeight;
            }

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(v2f input) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy;
                // Quantize before clipping so the aperture edge stays aligned to the
                // source 48 x 48 pixel grid at every integer Portal Studio zoom. Apply
                // the clip after the halo samples are calculated conceptually, but before
                // returning them, so reconstructed bloom cannot spill across the ring.
                float2 nativePixel = floor(input.uv * _MainTex_TexelSize.zw) + 0.5;
                float2 apertureDelta =
                    (nativePixel - _ApertureCenterPixels) /
                    max(_ApertureRadiusPixels, float2(0.5, 0.5));
                clip(1.0 - dot(apertureDelta, apertureDelta));
                half3 source = tex2D(_MainTex, input.uv).rgb;
                half peak = Peak(source);
                half highlight = smoothstep(
                    _HighlightThreshold,
                    1.0h,
                    saturate(peak));
                // PugRP's final resolve makes the hottest additive pixels approach white.
                // Mixing toward the existing peak retains intensity and the authored hue.
                half3 core = lerp(
                    source,
                    peak.xxx,
                    highlight * _HighlightMix) * _CoreGain;

                half3 crossOne =
                    ExtractBloom(input.uv + float2(texel.x, 0.0)) +
                    ExtractBloom(input.uv - float2(texel.x, 0.0)) +
                    ExtractBloom(input.uv + float2(0.0, texel.y)) +
                    ExtractBloom(input.uv - float2(0.0, texel.y));
                half3 diagonalOne =
                    ExtractBloom(input.uv + texel) +
                    ExtractBloom(input.uv - texel) +
                    ExtractBloom(input.uv + float2(texel.x, -texel.y)) +
                    ExtractBloom(input.uv + float2(-texel.x, texel.y));
                half3 onePixelHalo = crossOne * 0.1875h + diagonalOne * 0.0625h;

                float2 twoTexels = texel * 2.0;
                half3 twoPixelHalo =
                    (ExtractBloom(input.uv + float2(twoTexels.x, 0.0)) +
                     ExtractBloom(input.uv - float2(twoTexels.x, 0.0)) +
                     ExtractBloom(input.uv + float2(0.0, twoTexels.y)) +
                     ExtractBloom(input.uv - float2(0.0, twoTexels.y))) * 0.25h;

                // Vanilla footage shows a strong one-native-pixel halo and only a weak
                // second-pixel tail. Sampling no farther than this keeps the 1.6-pixel
                // source heads and tapered trails visibly pixel-art, never smooth arcs.
                half3 contribution = core +
                    onePixelHalo * _OnePixelHalo +
                    twoPixelHalo * _TwoPixelHalo;
                return half4(contribution, 0.0);
            }
            ENDCG
        }
    }
}
