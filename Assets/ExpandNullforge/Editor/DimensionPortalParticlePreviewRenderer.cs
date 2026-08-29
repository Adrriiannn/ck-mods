using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Renders the extracted vanilla GatherEnergy particle system at Core Keeper's native
    /// portal resolution. The completed 48 x 48 image is point-scaled by Portal Studio, so
    /// the 32 x 32 source billboard resolves to its real 1-2 pixel in-game footprint instead
    /// of exposing the source circle at editor zoom. A renderer constructed with
    /// <c>readyBurst</c> drives the one-shot DeathBlink activation burst through the same
    /// pipeline instead of the persistent swirl.
    /// </summary>
    internal sealed class DimensionPortalParticlePreviewRenderer : IDisposable
    {
        private readonly bool readyBurst;

        internal DimensionPortalParticlePreviewRenderer(bool readyBurst = false)
        {
            this.readyBurst = readyBurst;
        }

        private const int NativePixels = DimensionPortalVisualContract.CanonicalFramePixels;
        private const float MaximumIncrementalStep = 0.75f;
        private const float PreviewCameraDistance = 10.0f;
        private const string AdditiveCompositeShaderPath =
            "Assets/ExpandNullforge/Editor/Shaders/PortalStudioAdditiveComposite.shader";
        // GatherEnergy is rendered to a 48 x 48 target centred on the vanilla center
        // SpriteObject pivot. These source-pixel measurements keep its additive heads,
        // trails, and reconstructed halo inside the transparent opening of the 16 x 25
        // PortalCenterEffect ring. The hard edge is intentional: Portal Studio previews
        // native point-sampled pixel art, so a feather would make the aperture look soft.
        private static readonly Vector2 VanillaApertureCenterPixels =
            new Vector2(24.0f, 25.0f);
        private static readonly Vector2 VanillaApertureRadiusPixels =
            new Vector2(5.5f, 7.5f);
        private static readonly int ApertureCenterProperty =
            Shader.PropertyToID("_ApertureCenterPixels");
        private static readonly int ApertureRadiusProperty =
            Shader.PropertyToID("_ApertureRadiusPixels");

        private Scene previewScene;
        private GameObject previewCameraHost;
        private Camera previewCamera;
        private RenderTexture previewTarget;
        private GameObject previewHost;
        private GameObject particleRoot;
        private ParticleSystem rootParticleSystem;
        private DimensionPortalVisualProfileAsset configuredProfile;
        private int configuredFleckHash = -1;
        private bool hasSimulationTime;
        private float lastSimulationTime;
        private float lastRenderedTime = -1.0f;
        private Texture renderedTexture;
        private DimensionPortalVisualProfileAsset failedProfile;
        private int failedFleckHash = -1;
        private string failedError = string.Empty;
        private readonly List<Material> ownedPreviewMaterials = new List<Material>(4);
        private Material additiveCompositeMaterial;
        private CommandBuffer particleRenderCommands;

        /// <summary>One drawable emitter of the preview subtree, with its own baked meshes.</summary>
        private struct BakedEmitter
        {
            public ParticleSystem System;
            public ParticleSystemRenderer Renderer;
            public Mesh ParticleMesh;
            public Mesh TrailMesh;
            public Material ParticleMaterial;
            public Material TrailMaterial;
        }

        private readonly List<BakedEmitter> bakedEmitters = new List<BakedEmitter>(4);

        internal bool TryRender(
            DimensionPortalVisualProfileAsset profile,
            float previewTime,
            out Texture texture,
            out string error)
        {
            texture = null;
            error = string.Empty;
            if (profile == null)
            {
                return false;
            }

            int fleckHash = ComputeFleckConfigurationHash(profile);
            if (failedProfile == profile && failedFleckHash == fleckHash)
            {
                error = failedError;
                return false;
            }

            try
            {
                EnsurePreview(profile, fleckHash);
                EnsurePreviewTarget();
                if (previewCamera == null || previewTarget == null || rootParticleSystem == null)
                {
                    error = "The extracted vanilla GatherEnergy particle system is unavailable.";
                    return false;
                }

                previewTime = Mathf.Max(0.0f, previewTime);
                if (renderedTexture != null &&
                    hasSimulationTime &&
                    Mathf.Abs(previewTime - lastRenderedTime) <= 0.00001f)
                {
                    texture = renderedTexture;
                    return true;
                }

                Simulate(previewTime);
                ConfigureCoreKeeperCamera(previewCamera);
                RenderParticleMeshes();
                texture = previewTarget;

                if (texture.width != NativePixels || texture.height != NativePixels)
                {
                    throw new InvalidOperationException(
                        "Unity produced a " + texture.width + " x " + texture.height +
                        " GatherEnergy target instead of the required physical 48 x 48 pixels.");
                }

                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                renderedTexture = texture;
                lastRenderedTime = previewTime;
                return true;
            }
            catch (Exception exception)
            {
                error = "The exact GatherEnergy preview could not be rendered: " +
                        exception.Message;
                DestroyPreview();
                configuredProfile = null;
                configuredFleckHash = -1;
                hasSimulationTime = false;
                lastSimulationTime = 0.0f;
                failedProfile = profile;
                failedFleckHash = fleckHash;
                failedError = error;
                return false;
            }
        }

        internal void DrawAdditive(Rect destination, Texture texture)
        {
            DrawAdditive(
                destination,
                texture,
                VanillaApertureCenterPixels,
                VanillaApertureRadiusPixels);
        }

        internal void DrawAdditive(
            Rect destination,
            Texture texture,
            Vector2 apertureCenterPixels,
            Vector2 apertureRadiusPixels)
        {
            if (texture == null || destination.width <= 0.0f || destination.height <= 0.0f)
            {
                return;
            }

            EnsureAdditiveCompositeMaterial();
            Color previousColor = GUI.color;
            GUI.color = Color.white;
            try
            {
                if (additiveCompositeMaterial != null)
                {
                    additiveCompositeMaterial.SetVector(
                        ApertureCenterProperty,
                        apertureCenterPixels);
                    additiveCompositeMaterial.SetVector(
                        ApertureRadiusProperty,
                        new Vector2(
                            Mathf.Max(0.5f, apertureRadiusPixels.x),
                            Mathf.Max(0.5f, apertureRadiusPixels.y)));
                    EditorGUI.DrawPreviewTexture(
                        destination,
                        texture,
                        additiveCompositeMaterial,
                        ScaleMode.StretchToFill);
                }
                else
                {
                    // This fallback preserves the native pixel footprint even if the small
                    // editor-only compositor shader is still importing. The normal path uses
                    // additive composition to match ParticleAdd over the center artwork.
                    GUI.DrawTexture(
                        destination,
                        texture,
                        ScaleMode.StretchToFill,
                        true);
                }
            }
            finally
            {
                GUI.color = previousColor;
            }
        }

        internal void Invalidate()
        {
            DestroyPreview();
            configuredProfile = null;
            configuredFleckHash = -1;
            hasSimulationTime = false;
            lastSimulationTime = 0.0f;
            lastRenderedTime = -1.0f;
            renderedTexture = null;
            failedProfile = null;
            failedFleckHash = -1;
            failedError = string.Empty;
        }

        public void Dispose()
        {
            Invalidate();
            if (additiveCompositeMaterial != null)
            {
                Object.DestroyImmediate(additiveCompositeMaterial);
                additiveCompositeMaterial = null;
            }
        }

        private void EnsurePreview(
            DimensionPortalVisualProfileAsset profile,
            int fleckHash)
        {
            if (previewScene.IsValid() &&
                previewScene.isLoaded &&
                EditorSceneManager.IsPreviewScene(previewScene) &&
                previewCamera != null &&
                previewTarget != null &&
                configuredProfile == profile &&
                configuredFleckHash == fleckHash)
            {
                return;
            }

            DestroyPreview();

            previewScene = EditorSceneManager.NewPreviewScene();
            if (!previewScene.IsValid() ||
                !previewScene.isLoaded ||
                !EditorSceneManager.IsPreviewScene(previewScene))
            {
                throw new InvalidOperationException(
                    "Unity could not create the Portal Studio particle preview scene.");
            }

            previewCameraHost = new GameObject("PortalStudioParticlePreviewCamera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            SceneManager.MoveGameObjectToScene(previewCameraHost, previewScene);
            previewCamera = previewCameraHost.AddComponent<Camera>();
            // The camera is only a Core Keeper projection/orientation descriptor for the
            // baked particle meshes. PugRP returns a blank target from Camera.Render() in
            // this editor context, so the preview uses the camera-free command path below.
            previewCamera.enabled = false;
            EnsurePreviewTarget();
            ConfigureCoreKeeperCamera(previewCamera);

            previewHost = new GameObject("PortalStudioParticlePreview")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            SceneManager.MoveGameObjectToScene(previewHost, previewScene);
            particleRoot = readyBurst
                ? DimensionRuntimeConsumerBootstrapUtility.CreatePortalReadyBurstParticlePreview(
                    previewHost.transform,
                    profile)
                : DimensionRuntimeConsumerBootstrapUtility.CreatePortalPersistentParticlePreview(
                    previewHost.transform,
                    profile);
            if (particleRoot == null)
            {
                throw new InvalidOperationException(
                    "The configured GatherEnergy preview root could not be created.");
            }

            particleRoot.hideFlags = HideFlags.HideAndDontSave;
            // The ready-burst root keeps its authored pixel offset; only the persistent swirl
            // is re-anchored to the canvas origin.
            if (!readyBurst)
            {
                particleRoot.transform.localPosition = Vector3.zero;
            }
            particleRoot.SetActive(true);
            DimensionRuntimeConsumerBootstrapUtility.ConfigurePortalPersistentParticlePreviewMaterials(
                particleRoot,
                profile,
                ownedPreviewMaterials,
                readyBurst);
            rootParticleSystem = particleRoot.GetComponent<ParticleSystem>();
            if (rootParticleSystem == null)
            {
                throw new InvalidOperationException(
                    "The extracted GatherEnergy root has no ParticleSystem component.");
            }
            PrepareManualRenderResources();
            if (bakedEmitters.Count == 0)
            {
                throw new InvalidOperationException(
                    "The configured GatherEnergy preview has no drawable particle emitter.");
            }

            configuredProfile = profile;
            configuredFleckHash = fleckHash;
            hasSimulationTime = false;
            lastSimulationTime = 0.0f;
            lastRenderedTime = -1.0f;
            renderedTexture = null;
            failedProfile = null;
            failedFleckHash = -1;
            failedError = string.Empty;
        }

        private void EnsurePreviewTarget()
        {
            if (previewTarget == null)
            {
                RenderTextureFormat format =
                    SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
                        ? RenderTextureFormat.ARGBHalf
                        : RenderTextureFormat.ARGB32;
                previewTarget = new RenderTexture(
                    NativePixels,
                    NativePixels,
                    24,
                    format,
                    RenderTextureReadWrite.Linear)
                {
                    name = "PortalStudioGatherEnergy48",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    antiAliasing = 1,
                    useMipMap = false,
                    autoGenerateMips = false,
                    anisoLevel = 0
                };
            }

            if (!previewTarget.IsCreated())
            {
                if (!previewTarget.Create())
                {
                    throw new InvalidOperationException(
                        "Unity could not create the 48 x 48 Portal Studio particle target.");
                }

                // A RenderTexture can lose its native contents when the graphics device is
                // reset. Never return the stale same-time cache after recreating it.
                renderedTexture = null;
                lastRenderedTime = -1.0f;
            }

            if (previewCamera != null && previewCamera.targetTexture != previewTarget)
            {
                previewCamera.targetTexture = previewTarget;
            }
        }

        private static void ConfigureCoreKeeperCamera(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            // CameraManager.Start() uses a 45-degree X rotation and
            // CameraMatrixScaler multiplies projection m11 by sqrt(2). Together they
            // produce Core Keeper's pixel-aligned screen-Y = world-Y + world-Z
            // projection. An identity preview camera looks equivalent for flat sprites,
            // but it is not equivalent to ParticleSystem trail/billboard rendering,
            // which consumes the real camera direction.
            Quaternion rotation = Quaternion.Euler(45.0f, 0.0f, 0.0f);
            Vector3 forward = rotation * Vector3.forward;
            camera.transform.SetPositionAndRotation(
                -forward * PreviewCameraDistance,
                rotation);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.orthographic = true;
            float coreKeeperProjectionScale = Mathf.Sqrt(2.0f);
            float nativeOrthographicSize =
                NativePixels / (2.0f * DimensionPortalVisualContract.PixelsPerUnit);
            // CameraMatrixScaler multiplies only projection m11 by sqrt(2). Express the
            // same matrix through ordinary Camera properties: reducing orthographic size
            // multiplies m11, while widening the aspect by the same factor cancels that
            // zoom on m00.
            camera.orthographicSize = nativeOrthographicSize / coreKeeperProjectionScale;
            camera.aspect = coreKeeperProjectionScale;
            camera.nearClipPlane = 0.01f;
            // Unity's particle trail renderer projects its screen-corner helper points at
            // depth 20. Keep that depth comfortably inside the frustum; placing the far
            // plane exactly at 20 causes four boundary errors on every Camera.Render.
            camera.farClipPlane = 100.0f;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;

            // Rebuild the standard orthographic matrix from the values above every time,
            // without leaving a custom projection override behind.
            camera.ResetProjectionMatrix();
        }

        private void Simulate(float previewTime)
        {
            previewTime = Mathf.Max(0.0f, previewTime);
            float delta = previewTime - lastSimulationTime;
            bool restart = !hasSimulationTime ||
                           delta < 0.0f ||
                           delta > MaximumIncrementalStep;
            if (restart)
            {
                rootParticleSystem.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
                rootParticleSystem.Simulate(
                    previewTime,
                    true,
                    true,
                    false);
            }
            else if (delta > 0.00001f)
            {
                rootParticleSystem.Simulate(
                    delta,
                    true,
                    false,
                    false);
            }

            hasSimulationTime = true;
            lastSimulationTime = previewTime;
        }

        private void PrepareManualRenderResources()
        {
            if (rootParticleSystem == null)
            {
                return;
            }

            // Every live emitter in the subtree is baked, not just the root. The ready burst
            // lives entirely in the DeathBlink child, and the persistent swirl disables that
            // same child — whichever survives, this walk finds it.
            ParticleSystemRenderer[] renderers =
                particleRoot.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                ParticleSystemRenderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                ParticleSystem system = renderer.GetComponent<ParticleSystem>();
                if (system == null || !system.gameObject.activeInHierarchy)
                {
                    continue;
                }

                bakedEmitters.Add(CreateBakedEmitter(system, renderer, i));
            }

            particleRenderCommands = new CommandBuffer
            {
                name = "Portal Studio GatherEnergy"
            };
        }

        private BakedEmitter CreateBakedEmitter(
            ParticleSystem system,
            ParticleSystemRenderer renderer,
            int index)
        {
            Material[] sourceMaterials = renderer.sharedMaterials;
            Material sourceParticle = sourceMaterials != null && sourceMaterials.Length > 0
                ? sourceMaterials[0]
                : null;
            Material sourceTrail = sourceMaterials != null && sourceMaterials.Length > 1
                ? sourceMaterials[1]
                : sourceParticle;

            ParticleSystem.TextureSheetAnimationModule textureSheet =
                system.textureSheetAnimation;
            Texture particleTexture = null;
            if (textureSheet.enabled && textureSheet.spriteCount > 0)
            {
                Sprite sprite = textureSheet.GetSprite(0);
                if (sprite != null)
                {
                    particleTexture = sprite.texture;
                }
            }
            if (particleTexture == null && sourceParticle != null)
            {
                particleTexture = sourceParticle.mainTexture;
            }

            BakedEmitter emitter = new BakedEmitter
            {
                System = system,
                Renderer = renderer,
                ParticleMaterial = CreateManualRenderMaterial(
                    sourceParticle,
                    particleTexture,
                    "PortalStudioParticles" + index),
                TrailMaterial = CreateManualRenderMaterial(
                    sourceTrail,
                    particleTexture,
                    "PortalStudioTrails" + index),
                ParticleMesh = new Mesh
                {
                    name = "PortalStudioParticleMesh" + index,
                    hideFlags = HideFlags.HideAndDontSave
                },
                TrailMesh = new Mesh
                {
                    name = "PortalStudioTrailMesh" + index,
                    hideFlags = HideFlags.HideAndDontSave
                }
            };
            emitter.ParticleMesh.MarkDynamic();
            emitter.TrailMesh.MarkDynamic();
            return emitter;
        }

        private Material CreateManualRenderMaterial(
            Material source,
            Texture texture,
            string materialName)
        {
            if (source == null)
            {
                return null;
            }

            Material material = new Material(source)
            {
                name = materialName,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (texture != null)
            {
                material.mainTexture = texture;
            }
            ownedPreviewMaterials.Add(material);
            return material;
        }

        private void RenderParticleMeshes()
        {
            if (previewCamera == null ||
                previewTarget == null ||
                rootParticleSystem == null ||
                bakedEmitters.Count == 0 ||
                particleRenderCommands == null)
            {
                throw new InvalidOperationException(
                    "The Portal Studio GatherEnergy mesh renderer is unavailable.");
            }

            particleRenderCommands.Clear();
            particleRenderCommands.SetRenderTarget(previewTarget);
            particleRenderCommands.ClearRenderTarget(true, true, Color.clear);
            particleRenderCommands.SetViewport(
                new Rect(0.0f, 0.0f, NativePixels, NativePixels));
            particleRenderCommands.SetViewProjectionMatrices(
                previewCamera.worldToCameraMatrix,
                GL.GetGPUProjectionMatrix(previewCamera.projectionMatrix, true));

            for (int i = 0; i < bakedEmitters.Count; i++)
            {
                BakedEmitter emitter = bakedEmitters[i];
                if (emitter.System == null ||
                    emitter.Renderer == null ||
                    emitter.ParticleMesh == null)
                {
                    continue;
                }

                emitter.ParticleMesh.Clear(false);
                // Baking a system holding no particles throws rather than producing an empty
                // mesh, so an emitter that has not fired yet is simply skipped this frame.
                if (emitter.System.particleCount > 0)
                {
                    emitter.Renderer.BakeMesh(
                        emitter.ParticleMesh,
                        previewCamera,
                        ParticleSystemBakeMeshOptions.BakeRotationAndScale |
                        ParticleSystemBakeMeshOptions.BakePosition);
                }

                emitter.TrailMesh.Clear(false);
                ParticleSystem.TrailModule trails = emitter.System.trails;
                if (trails.enabled && emitter.System.particleCount > 0)
                {
                    emitter.Renderer.BakeTrailsMesh(
                        emitter.TrailMesh,
                        previewCamera,
                        ParticleSystemBakeMeshOptions.BakeRotationAndScale |
                        ParticleSystemBakeMeshOptions.BakePosition);
                }

                // Additive composition is order-independent, but drawing the tapered trail
                // first leaves the head as the final primitive, like ParticleSystemRenderer.
                DrawBakedMesh(
                    particleRenderCommands,
                    emitter.TrailMesh,
                    emitter.TrailMaterial);
                DrawBakedMesh(
                    particleRenderCommands,
                    emitter.ParticleMesh,
                    emitter.ParticleMaterial);
            }

            Graphics.ExecuteCommandBuffer(particleRenderCommands);
        }

        private static void DrawBakedMesh(
            CommandBuffer commands,
            Mesh mesh,
            Material material)
        {
            if (commands == null ||
                mesh == null ||
                material == null ||
                mesh.vertexCount == 0)
            {
                return;
            }

            int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
            for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
            {
                commands.DrawMesh(
                    mesh,
                    Matrix4x4.identity,
                    material,
                    subMesh,
                    -1);
            }
        }

        private int ComputeFleckConfigurationHash(
            DimensionPortalVisualProfileAsset profile)
        {
            unchecked
            {
                int hash = 17;
                if (!readyBurst)
                {
                    // Vanilla mode deliberately preserves every GatherEnergy module and source
                    // texture. Only the authored swirl tint can change its rendered result.
                    hash = hash * 31 + profile.CenterParticleTint.GetHashCode();
                    return hash;
                }

                // Every authored ready-burst control is baked into the clone at construction
                // time, so any change must rebuild the preview subtree.
                hash = hash * 31 + profile.PlayReadyFlash.GetHashCode();
                hash = hash * 31 + profile.ReadyFlashFollowsCenterPalette.GetHashCode();
                hash = hash * 31 + profile.ReadyFlashTint.GetHashCode();
                hash = hash * 31 + profile.CenterParticleTint.GetHashCode();
                hash = hash * 31 + profile.ReadyFlashOffsetPixels.GetHashCode();
                hash = hash * 31 + profile.ReadyFlashScale.GetHashCode();
                hash = hash * 31 + profile.ReadyFlashRotationDegrees.GetHashCode();
                hash = hash * 31 + profile.ReadyFlashEmissionMultiplier.GetHashCode();
                hash = hash * 31 + profile.ReadyFlashSizeMultiplier.GetHashCode();
                Sprite[] sprites = profile.ReadyFlashSprites;
                hash = hash * 31 + (sprites == null ? 0 : sprites.Length);
                if (sprites != null)
                {
                    for (int i = 0; i < sprites.Length; i++)
                    {
                        hash = hash * 31 +
                               (sprites[i] == null ? 0 : sprites[i].GetInstanceID());
                    }
                }

                hash = hash * 31 + (profile.ReadyFlashTexture == null
                    ? 0
                    : profile.ReadyFlashTexture.GetInstanceID());
                return hash;
            }
        }

        private void EnsureAdditiveCompositeMaterial()
        {
            if (additiveCompositeMaterial != null)
            {
                return;
            }

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(
                AdditiveCompositeShaderPath);
            if (shader == null)
            {
                return;
            }

            additiveCompositeMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private void DestroyPreview()
        {
            rootParticleSystem = null;

            particleRoot = null;
            if (previewCamera != null)
            {
                previewCamera.targetTexture = null;
            }

            if (previewTarget != null)
            {
                if (previewTarget.IsCreated())
                {
                    previewTarget.Release();
                }
                Object.DestroyImmediate(previewTarget);
                previewTarget = null;
            }

            // Destroy the owned hosts even when setup failed halfway through. Closing a
            // preview scene normally destroys them too, but explicit teardown keeps partial
            // construction and domain-reload paths leak-free.
            if (previewCameraHost != null)
            {
                Object.DestroyImmediate(previewCameraHost);
            }
            if (previewHost != null)
            {
                Object.DestroyImmediate(previewHost);
            }
            if (previewScene.IsValid() && EditorSceneManager.IsPreviewScene(previewScene))
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
            previewScene = default(Scene);
            previewCamera = null;
            previewCameraHost = null;
            previewHost = null;

            for (int i = 0; i < ownedPreviewMaterials.Count; i++)
            {
                Material material = ownedPreviewMaterials[i];
                if (material != null)
                {
                    Object.DestroyImmediate(material);
                }
            }
            ownedPreviewMaterials.Clear();
            for (int i = 0; i < bakedEmitters.Count; i++)
            {
                BakedEmitter emitter = bakedEmitters[i];
                if (emitter.ParticleMesh != null)
                {
                    Object.DestroyImmediate(emitter.ParticleMesh);
                }
                if (emitter.TrailMesh != null)
                {
                    Object.DestroyImmediate(emitter.TrailMesh);
                }
            }
            bakedEmitters.Clear();
            if (particleRenderCommands != null)
            {
                particleRenderCommands.Dispose();
                particleRenderCommands = null;
            }
            renderedTexture = null;
            lastRenderedTime = -1.0f;
        }
    }
}
