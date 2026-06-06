using System;
using System.Reflection;
using UnityEngine;

namespace FFUIOverhaul.UI
{
    /// <summary>
    /// "Crisp Mode" — a native, single-pass post-process that replicates a
    /// ReShade CAS + vibrance stack, applied to the WORLD camera only (UI/widget
    /// camera is separate, so the HUD never gets sharpened).
    ///
    /// The shader ships as an AssetBundle embedded in this DLL (built in Unity
    /// 2022.3.62f3 to match the game). We load it once, build a Material, and
    /// attach a tiny image-effect component (CrispBlit) to CameraManager.mainCamera.
    ///
    /// Lifecycle: Tick() is called every frame from OnUpdate. When the toggle is
    /// off we never attach (or disable an existing component → zero render cost).
    /// When on, we (re)find the world camera each map load and attach.
    /// </summary>
    internal static class CrispMode
    {
        private const string EmbeddedBundleName = "FFUIOverhaul.crisp.bundle";
        private static readonly int SharpId = Shader.PropertyToID("_Sharpness");
        private static readonly int VibId   = Shader.PropertyToID("_Vibrance");

        private static Shader? _shader;
        private static Material? _mat;
        private static bool _bundleTried;
        private static CrispBlit? _blit;

        internal static int SharpProp => SharpId;
        internal static int VibProp   => VibId;
        internal static Material? Material => _mat;

        /// <summary>Called every frame from OnUpdate (in-map only).</summary>
        public static void Tick()
        {
            bool on = FFUIOverhaulMod.EnableCrispMode.Value;

            if (!on)
            {
                // Disable (not destroy) so flipping back on is instant and free.
                if (_blit != null) _blit.enabled = false;
                return;
            }

            EnsureAttached();
            if (_blit != null) _blit.enabled = true;
        }

        /// <summary>Drop our camera reference on scene unload; the camera GO is
        /// destroyed on map exit, so the component dies with it.</summary>
        public static void OnSceneChanged() => _blit = null;

        private static void EnsureAttached()
        {
            if (_blit != null) return;                 // Unity fake-null clears this on camera destroy
            if (!GameManager.gameReadyToPlay) return;

            var mat = GetMaterial();
            if (mat == null) return;

            var cam = FindWorldCamera();
            if (cam == null) return;

            _blit = cam.gameObject.GetComponent<CrispBlit>() ?? cam.gameObject.AddComponent<CrispBlit>();
            FFUIOverhaulMod.Log.Msg("[CrispMode] attached to world camera: " + cam.name);
        }

        private static Camera? FindWorldCamera()
        {
            // CameraManager.mainCamera is the world/game camera (widgetCamera is
            // the separate UI camera we deliberately leave untouched).
            var cm = UnityEngine.Object.FindObjectOfType<CameraManager>();
            if (cm != null && cm.mainCamera != null) return cm.mainCamera;
            return Camera.main;
        }

        private static Material? GetMaterial()
        {
            if (_mat != null) return _mat;
            var shader = LoadShader();
            if (shader == null) return null;
            _mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return _mat;
        }

        private static Shader? LoadShader()
        {
            if (_shader != null) return _shader;
            if (_bundleTried) return _shader;          // don't retry a failed load every frame
            _bundleTried = true;

            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using (var s = asm.GetManifestResourceStream(EmbeddedBundleName))
                {
                    if (s == null)
                    {
                        FFUIOverhaulMod.Log.Error("[CrispMode] embedded bundle '" + EmbeddedBundleName + "' not found.");
                        return null;
                    }
                    var bytes = new byte[s.Length];
                    int read = 0, off = 0;
                    while (off < bytes.Length && (read = s.Read(bytes, off, bytes.Length - off)) > 0) off += read;

                    var bundle = AssetBundle.LoadFromMemory(bytes);
                    if (bundle == null)
                    {
                        FFUIOverhaulMod.Log.Error("[CrispMode] AssetBundle.LoadFromMemory returned null (Unity version mismatch?).");
                        return null;
                    }

                    var shaders = bundle.LoadAllAssets<Shader>();
                    _shader = (shaders != null && shaders.Length > 0) ? shaders[0] : null;
                    bundle.Unload(false); // keep loaded Shader asset, free the bundle container

                    if (_shader == null)
                        FFUIOverhaulMod.Log.Error("[CrispMode] bundle loaded but contained no Shader.");
                    else
                        FFUIOverhaulMod.Log.Msg("[CrispMode] loaded shader: " + _shader.name);
                }
            }
            catch (Exception e)
            {
                FFUIOverhaulMod.Log.Error("[CrispMode] shader load failed: " + e);
                _shader = null;
            }
            return _shader;
        }
    }

    /// <summary>
    /// The actual image effect. Built-in pipeline calls OnRenderImage after the
    /// camera (and any PPv2 layer) has rendered, so we sharpen the final graded
    /// frame. Disabled component = Unity skips this entirely (no cost when off).
    /// </summary>
    internal sealed class CrispBlit : MonoBehaviour
    {
        private Camera? _cam;

        private void Awake() => _cam = GetComponent<Camera>();

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            var mat = CrispMode.Material;
            // Pass through untouched when no material, or when the camera is
            // rendering to an offscreen target (minimap / screenshot capture).
            if (mat == null || (_cam != null && _cam.targetTexture != null))
            {
                Graphics.Blit(src, dst);
                return;
            }

            mat.SetFloat(CrispMode.SharpProp, FFUIOverhaulMod.CrispSharpness.Value);
            mat.SetFloat(CrispMode.VibProp,   FFUIOverhaulMod.CrispVibrance.Value);
            Graphics.Blit(src, dst, mat);
        }
    }
}
