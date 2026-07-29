// Under-glass circuit floor — the framework's custom ground material for custom tilesets.
//
// Patterned precisely on the SDK's own UGC_Dummy/Opaque (Packages/dev.pugstorm.mod/Assets/
// Shaders/UGCDummy_Opaque.shader): same tags, Cull Off, one unnamed CG pass (renders via the
// pipeline's SRPDefaultUnlit pass), same appdata/v2f contract (vertex color multiplied in) and
// the same clip(alpha - 0.5) edge semantics — so tile edges cut exactly like the normal ground
// material. The UGC family is what the SDK ships for mod content under PugRP, which makes it
// the provably-safe template; the vanilla Amplify_* sources are stripped stubs in the SDK.
//
// On top of the template it adds an emissive circuit glow with two drivers:
//
//  (a) AMBIENT DREAMS — rare deterministic pulses. Per world-space cell (8x8 tiles) per epoch
//      (_AmbientPeriod seconds), a hash of (cell, epoch) gives a chance (_AmbientRarity) that a
//      brief (_PulseDuration) brightness band sweeps across the cell's circuit art. Everything
//      derives from world position + the epoch index — no per-frame randomness, and the sweep
//      coordinate is world-position-driven so it moves coherently across tile boundaries
//      (individual tile UVs never enter the pulse math).
//
//  (b) STABILITY GLOW — the game's real electricity. Every frame the game sets these globals
//      (decompile: ShaderTexturesFinalizeSystem / ShaderTexturesSystem):
//        ElectricityTex     36x24 RGBA32, Point/Clamp. R = live electricity strength (0-255,
//                           per-hop falloff), G = ancient electricity.
//        Origo              float4, the camera's render origin (RenderOrigo).
//        effectsTextureSize float2 = (36, 24).
//      The texture spans world tiles [Origo.x-18, Origo.x+18) x [Origo.z-12, Origo.z+12), so
//        uv = (worldPos.xz - Origo.xz + effectsTextureSize*0.5) / effectsTextureSize.
//      Texel-center caveat: that maps a tile's min corner onto a texel EDGE; with Point
//      filtering a half-texel offset may be needed so a whole tile reads its own texel
//      (+0.5 texel centers it). _TexelNudge (in texels, default 0.5) exposes this for runtime
//      tuning — only an in-game run can confirm the exact alignment.
//
// Glow = _EmissiveTex.rgb * saturate(ambientPulse + electricity * _StabilityStrength)
//        * _EmissiveStrength, added over the regular art. The saturate caps the drive at 1, so
// full electricity (R=1) always reaches the cap and dominates any ambient pulse. Values above 1
// after _EmissiveStrength feed the game's HDR bloom chain.
//
// PLANAR REFLECTION (the "glass" of the under-glass floor) — decompile-verified recipe
// (PugRP PlanarReflectionsRenderFeature.cs + ShaderIDs.cs):
//   - The pipeline renders the scene through a mirrored utility camera BEFORE all geometry
//     (executionStage = BeforeGeometry) into a full camera-res RGB111110Float texture, then
//     cmd.SetGlobalTexture("_PlanarReflection", ...) — so sampling it from an opaque floor is
//     safe. When the feature is disabled (user pref, device profile, no anchor) the pipeline
//     binds Texture2D.blackTexture instead, so the composite MUST stay purely additive and
//     never divide by or gate on the reflection color.
//   - The mirror transform is baked into the reflection camera's matrices
//     (worldToCameraMatrix * reflectionMatrix + oblique clip plane), so the texture is sampled
//     with PLAIN screen-space UV — no manual flip. ComputeScreenPos (UnityCG.cginc) handles
//     the platform y-flip via _ProjectionParams.x; uv = screenPos.xy / screenPos.w.
//   - A small pixel offset sells the surface: uv.y += _ReflectionOffsetPx / _ScreenParams.y
//     (vanilla WaterWell uses -4 px).
//   - FEEDBACK GUARD: while rendering the reflection itself the pipeline enables the global
//     keyword RENDER_PLANAR_REFLECTIONS (cmd.SetKeyword around the mirrored draw). Our floor
//     appears in that mirrored pass too, so the variant compiled with the keyword skips the
//     reflection composite entirely — otherwise last frame's reflection feeds back into itself.
//   - _PlanarReflectionPlane (float4) is also set globally; unused in v1, declared for
//     documentation.
// Composite order: base art -> + reflection (tinted, matching Amplify/PlanarReflectionAdd's
// additive semantics) -> + emissive circuit glow. The circuits fictionally sit beneath the
// glass, but glowing on top of the reflection is what reads correctly.
//
// _MainTex needs no manual binding: the tile renderer (PugMapLayer2.MeshInit) instantiates the
// override material and sets mainTexture (the layer's REGULAR sheet) plus _EmissiveTex (the
// layer's EMISSIVE sheet) on it every init.
Shader "Dimensions API/NullforgeCircuitFloor"
{
	Properties
	{
		[PerRendererData] _MainTex ("Texture", 2D) = "white" {}
		_EmissiveTex ("Circuit Emissive", 2D) = "black" {}
		_EmissiveStrength ("Emissive Strength", Float) = 3
		_AmbientRarity ("Ambient Pulse Chance", Range(0, 1)) = 0.015
		_AmbientPeriod ("Ambient Epoch (seconds)", Float) = 8
		_PulseDuration ("Pulse Duration (seconds)", Float) = 1.5
		_PulseSpeed ("Pulse Sweep Speed (tiles/sec)", Float) = 10
		_StabilityStrength ("Electricity Glow Strength", Float) = 1
		_TexelNudge ("Electricity Texel Nudge (texels)", Float) = 0.5
		_ReflectionTint ("Reflection Tint", Color) = (0.35, 0.35, 0.35, 1)
		_ReflectionOffsetPx ("Reflection Y Offset (pixels)", Float) = -4
		_ReflectionStrength ("Reflection Strength", Range(0, 1)) = 1
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" "Queue"="Geometry" }

		Cull Off

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// The pipeline toggles this global keyword around the mirrored reflection draw;
			// the keyword-on variant must not sample the reflection it is being drawn into.
			#pragma multi_compile _ RENDER_PLANAR_REFLECTIONS

			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _MainTex_ST;
			sampler2D _EmissiveTex;
			float _EmissiveStrength;
			float _AmbientRarity;
			float _AmbientPeriod;
			float _PulseDuration;
			float _PulseSpeed;
			float _StabilityStrength;
			float _TexelNudge;
			float4 _ReflectionTint;
			float _ReflectionOffsetPx;
			float _ReflectionStrength;

			// Game-set globals (bound by name every frame; never declared in Properties).
			sampler2D ElectricityTex;
			float4 Origo;
			float4 effectsTextureSize;

			// Pipeline-set globals (cmd.SetGlobalTexture/Vector; never declared in Properties).
			// _PlanarReflection is blackTexture whenever the feature is off — additive-only use.
			sampler2D _PlanarReflection;
			float4 _PlanarReflectionPlane; // set by the pipeline; unused in v1

			// One ambient cell is 8x8 world tiles.
			#define CIRCUIT_CELL_SIZE 8.0

			struct appdata
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float4 color : COLOR;
				float2 uv : TEXCOORD0;
				float3 worldPos : TEXCOORD1;
				float4 screenPos : TEXCOORD2;
			};

			// Cheap deterministic hash of (cell.x, cell.y, epoch) -> [0,1). Fraction-based
			// (no sin), stable for the moderate magnitudes cells and epochs reach.
			float Hash31(float3 p3)
			{
				p3 = frac(p3 * 0.1031);
				p3 += dot(p3, p3.zyx + 31.32);
				return frac((p3.x + p3.y) * p3.z);
			}

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.color = v.color;
				o.uv = v.uv;
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				// ComputeScreenPos (UnityCG.cginc) bakes the platform y-flip in via
				// _ProjectionParams.x; the perspective divide happens in frag.
				o.screenPos = ComputeScreenPos(o.vertex);
				return o;
			}

			// (a) AMBIENT DREAMS: 0 when this cell drew a blank this epoch; otherwise a band
			// sweeping the cell, world-position-driven so it crosses tile seams coherently.
			float AmbientPulse(float2 worldXZ)
			{
				float period = max(_AmbientPeriod, 0.0001);
				float duration = max(_PulseDuration, 0.0001);
				float2 cell = floor(worldXZ / CIRCUIT_CELL_SIZE);
				float epoch = floor(_Time.y / period);

				float h = Hash31(float3(cell, epoch));
				if (h >= _AmbientRarity)
				{
					return 0.0;
				}

				// h is uniform on [0,_AmbientRarity): reuse it as this pulse's start offset
				// inside the epoch (still fully deterministic, one hash total).
				float start = (h / max(_AmbientRarity, 0.0001)) * max(period - duration, 0.0);
				float tLocal = _Time.y - epoch * period - start;
				if (tLocal < 0.0 || tLocal > duration)
				{
					return 0.0;
				}

				float progress = tLocal / duration;

				// Sweep direction from a second hash so pulses don't all travel the same way.
				float angle = Hash31(float3(cell, epoch + 73.0)) * 6.2831853;
				float2 dir = float2(cos(angle), sin(angle));

				// Signed sweep coordinate in tiles, measured from the cell center — world
				// position driven, so the band front is continuous across every tile boundary.
				float2 cellCenter = (cell + 0.5) * CIRCUIT_CELL_SIZE;
				float sweep = dot(worldXZ - cellCenter, dir);

				// The band enters at -reach and travels at _PulseSpeed tiles/sec; the sine
				// envelope fades the whole pulse in and out so it never pops.
				const float bandHalfWidth = 1.5;
				float reach = CIRCUIT_CELL_SIZE * 0.75 + bandHalfWidth;
				float center = -reach + tLocal * _PulseSpeed;
				float band = saturate(1.0 - abs(sweep - center) / bandHalfWidth);
				band *= band;
				return band * sin(3.1415927 * progress);
			}

			// (b) STABILITY GLOW: live electricity (R) at this world position.
			float Electricity(float2 worldXZ)
			{
				// effectsTextureSize is (36,24); guard the div for editor views where the
				// global was never set.
				float2 texSize = effectsTextureSize.x > 0.0
					? effectsTextureSize.xy
					: float2(36.0, 24.0);
				float2 uv = (worldXZ - Origo.xz + texSize * 0.5 + _TexelNudge.xx) / texSize;
				// Clamp wrap mode handles out-of-range; Point filter means no bleed.
				// R = live electricity, G = ancient electricity (unused here).
				return tex2D(ElectricityTex, uv).r;
			}

			float4 frag(v2f i) : SV_Target
			{
				float4 color = tex2D(_MainTex, i.uv) * i.color;
				clip(color.a - 0.5);

#ifndef RENDER_PLANAR_REFLECTIONS
				// The glass: the pipeline's planar reflection, composited additively (matching
				// Amplify/PlanarReflectionAdd semantics). Plain screen-space UV — the mirror is
				// baked into the reflection camera's matrices — plus a small pixel drop like
				// vanilla WaterWell. Skipped entirely inside the mirrored pass (see the
				// multi_compile keyword) so the reflection never feeds back into itself.
				float2 reflUV = i.screenPos.xy / i.screenPos.w;
				reflUV.y += _ReflectionOffsetPx / _ScreenParams.y;
				color.rgb += tex2D(_PlanarReflection, reflUV).rgb *
					_ReflectionTint.rgb * _ReflectionStrength;
#endif

				float drive = saturate(
					AmbientPulse(i.worldPos.xz) +
					Electricity(i.worldPos.xz) * _StabilityStrength);
				float3 glow = tex2D(_EmissiveTex, i.uv).rgb * drive * _EmissiveStrength;

				color.rgb += glow;
				return color;
			}
			ENDCG
		}
	}
}
