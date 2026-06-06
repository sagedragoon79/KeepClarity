# Keep Clarity — "Crisp Mode" (native ReShade-clone) plan

**Status:** design + shader written; KC C# not yet wired. Awaiting user-built AssetBundle.
**Goal:** replicate the user's 3-filter ReShade preset *natively* in KC, in **one** fullscreen
pass, world-camera only (UI stays untouched).
**Why native (not perf):** ReShade's 3 sharpen/color shaders are already sub-ms on GPU — this
won't raise FPS. Wins are: 3 passes → 1, no injector/overlay, and **HUD text not crunched**
(world camera only). Real motivation = bundled into KC, one in-game toggle, no external app.

## User's ReShade stack being cloned
| ReShade filter | What it does | Native equivalent |
| --- | --- | --- |
| `cCAS.fx` (CShade CAS) | contrast-adaptive sharpen | CAS pass (in `Crisp.shader`) |
| `AdaptiveSharpen.fx` | edge-aware sharpen | folded into CAS (CAS alone ≈ 95%); skip the 2nd sharpener |
| `Chromaticity.fx` | color/vibrance pop | vibrance term (in `Crisp.shader`) |

## Environment (verified)
- Unity **2022.3.62f3**, **built-in** render pipeline, `hdr-display-enabled=0` (gamma/sRGB out).
- World camera: `GameManager.cameraManager.mainCamera` (`CameraManager`, decompile L59113/L59296).
- UI camera: `cameraManager.widgetCamera` (L59151) — **separate**, so sharpen never hits UI.
- Caveat: world cam sometimes renders to `targetTexture` (minimap/screenshots, L59836) — guard
  `targetTexture == null` in the hook so only the real screen pass is processed.

## Artifacts in this folder
- `Crisp.shader` — the one-pass CAS + vibrance image effect (ShaderLab, built-in pipeline).
- `BuildAssetBundles.cs` — Unity editor menu item to bake the bundle.

## Phase 1 — Unity Editor 2022.3.62f3 (user, one-time)
1. Install **Unity Hub** (unity.com/download).
2. Hub → Installs → Install Editor → "Archive" tab → find **2022.3.62f3** (Unity 6/LTS archive),
   or use `unityhub://2022.3.62f3/<hash>`. No extra platform modules needed (StandaloneWindows64
   ships with the Windows editor). ~5–7 GB, one-time.

## Phase 2 — Build the bundle (user, ~5 min)
1. New project (3D Core template, any name).
2. Copy `Crisp.shader` → `Assets/`.
3. Copy `BuildAssetBundles.cs` → `Assets/Editor/` (create the `Editor` folder).
4. Select `Crisp.shader` → in the Inspector's bottom bar, set **AssetBundle = `crisp`** (New…).
5. Menu **Assets → Build Crisp Bundle**.
6. Grab `<project>/AssetBundles/crisp`. That single file is the deliverable.

## Phase 3 — KC wiring (claude, after bundle exists)
Ship `crisp` bundle to `UserData/KeepClarity/crisp.bundle` (or beside the DLL). Feature file
`src/Features/CrispMode.cs` (or wherever KC keeps features):

```csharp
// 1. Load bundle once → Material:
var bundle = AssetBundle.LoadFromFile(pathToCrisp);
var shader = bundle.LoadAsset<Shader>("Hidden/KeepClarity/Crisp"); // or LoadAllAssets
_mat = new Material(shader);

// 2. Attach to the world camera:
var cam = UnitySingleton<GameManager>.Instance.cameraManager.mainCamera;
var fx  = cam.gameObject.AddComponent<CrispBlit>(); // tiny MonoBehaviour
fx.mat  = _mat;

// 3. CrispBlit:
class CrispBlit : MonoBehaviour {
    public Material mat;
    void OnRenderImage(RenderTexture src, RenderTexture dst) {
        if (mat == null || !Config.EnableCrispMode.Value || GetComponent<Camera>().targetTexture != null)
            { Graphics.Blit(src, dst); return; }     // passthrough when off / rendering to RT
        mat.SetFloat("_Sharpness", Config.CrispSharpness.Value);
        mat.SetFloat("_Vibrance",  Config.CrispVibrance.Value);
        Graphics.Blit(src, dst, mat);
    }
}
```

### Prefs (KC, live — RestartRequired:false, ReloadRequired:false)
- `EnableCrispMode`  (bool,  default false)
- `CrispSharpness`   (float, default 0.6, range 0–1)
- `CrispVibrance`    (float, default 0.15, range 0–1)

Read live in `OnRenderImage`, so all changes apply instantly (no reload). Register in KC panel
under a "Visuals"/"Display" group.

### Edge cases to handle in wiring
- Re-attach `CrispBlit` on scene/camera rebuild (camera is recreated on map load); hook
  `OnSceneWasInitialized("Frontier")` to (re)find `cameraManager.mainCamera` and add the component
  if missing. Reset on scene unload.
- If `cameraManager` or `mainCamera` is null early, defer until game-ready.
- Pink output = bundle built on wrong Unity version → rebuild in 2022.3.62f3.
- Foreign-mod guard optional (no known conflicting visual mod); ReShade can run on top but will
  double-sharpen — document "disable your ReShade sharpen if you enable Crisp Mode."

## Tuning to match the user's current look
Start `CrispSharpness ≈ 0.6`, `CrispVibrance ≈ 0.15`. Their cCAS "Sharpening Contrast" slider was
at 0.000 (default), and they stack a 2nd sharpener — so a single CAS at ~0.6–0.7 should land close.
A/B against a ReShade-on screenshot at the same spot and nudge.
