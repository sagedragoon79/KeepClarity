// Drop this in your Unity project under  Assets/Editor/BuildAssetBundles.cs
// (the "Editor" folder name matters). Then use the menu:  Assets > Build Crisp Bundle.
// Output lands in  <project>/AssetBundles/crisp  — that single file is what KC ships.
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildAssetBundles
{
    [MenuItem("Assets/Build Crisp Bundle")]
    public static void Build()
    {
        string outDir = "AssetBundles";
        Directory.CreateDirectory(outDir);
        BuildPipeline.BuildAssetBundles(
            outDir,
            BuildAssetBundleOptions.ChunkBasedCompression,
            BuildTarget.StandaloneWindows64);
        Debug.Log("Crisp bundle built -> " + Path.GetFullPath(outDir));
    }
}
