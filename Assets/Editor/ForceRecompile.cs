using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace FPS.EditorTools
{
    /// <summary>
    /// Forces an AssetDatabase refresh plus a script recompile. Useful when the Editor
    /// window is not focused, because Unity defers auto-refresh until the window regains
    /// focus - which stalls automated workflows driven from outside the Editor.
    /// </summary>
    public static class ForceRecompile
    {
        [MenuItem("FPS/Force Refresh + Recompile")]
        public static void Run()
        {
            Debug.Log("[FPS] Forcing AssetDatabase.Refresh and script compilation...");
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            CompilationPipeline.RequestScriptCompilation();
        }

        [MenuItem("FPS/Force Refresh + Recompile", true)]
        public static bool Validate() => !EditorApplication.isCompiling;
    }
}
