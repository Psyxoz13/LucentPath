using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class ShaderCacheToolsWindow : EditorWindow
{
    private Vector2 _scroll;
    private string _lastReport = string.Empty;
    private string _customRoot = "Assets";

    private static readonly string[] _shaderExtensions = { ".shader", ".compute", ".hlsl", ".cginc", ".raytrace" };

    [MenuItem("Tools/Shader Cache Tools")]
    private static void Open()
    {
        var window = GetWindow<ShaderCacheToolsWindow>("Shader Cache Tools");

        window.minSize = new Vector2(360, 280);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Shader Cache Tools", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Reimport Root", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        _customRoot = EditorGUILayout.TextField(_customRoot);

        if (GUILayout.Button("Browse", GUILayout.Width(70)))
        {
            var picked = EditorUtility.OpenFolderPanel("Select Folder", _customRoot, string.Empty);

            if (string.IsNullOrEmpty(picked) == false)
            {
                int idx = picked.Replace('\\', '/').IndexOf("Assets/");

                _customRoot = idx >= 0 ? picked.Replace('\\', '/').Substring(idx) : picked;
            }
        }

        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Reimport Shaders Under Root", GUILayout.Height(28)))
        {
            ReimportShaders(_customRoot);
        }

        if (GUILayout.Button("Reimport All Project Shaders", GUILayout.Height(28)))
        {
            ReimportShaders("Assets");
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Clear GlobalShaderCache (Library/ShaderCache)", GUILayout.Height(28)))
        {
            ClearLibraryShaderCache();
        }

        if (GUILayout.Button("Clear ShaderCompilerCache (Library/ShaderCompilerCache)", GUILayout.Height(28)))
        {
            ClearLibraryShaderCompilerCache();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Recompile All Project Shaders", GUILayout.Height(28)))
        {
            RecompileAllShaders();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Full Refresh: Clear Cache + Reimport Root + Recompile", GUILayout.Height(36)))
        {
            FullRefresh();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Last Report:", EditorStyles.boldLabel);

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(80));

        EditorGUILayout.SelectableLabel(_lastReport, EditorStyles.textArea, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void ReimportShaders(string root)
    {
        var paths = CollectShaderAssetPaths(root);

        ReimportPaths(paths, $"Reimported {paths.Count} files under {root}");
    }

    private void ReimportPaths(List<string> paths, string message)
    {
        AssetDatabase.StartAssetEditing();

        try
        {
            for (var i = 0; i < paths.Count; i++)
            {
                var path = paths[i];

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        _lastReport = message;

        Debug.Log(message);
    }

    private static List<string> CollectShaderAssetPaths(string root)
    {
        var result = new List<string>();

        if (string.IsNullOrEmpty(root) || Directory.Exists(root) == false)
        {
            return result;
        }

        var files = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories);

        for (var i = 0; i < files.Length; i++)
        {
            var file = files[i];
            var ext = Path.GetExtension(file).ToLowerInvariant();
            bool match = false;

            for (var j = 0; j < _shaderExtensions.Length; j++)
            {
                var allowed = _shaderExtensions[j];

                if (ext == allowed)
                {
                    match = true;
                    break;
                }
            }

            if (match == false)
            {
                continue;
            }

            var assetPath = file.Replace('\\', '/');
            int idx = assetPath.IndexOf("Assets/");

            if (idx >= 0)
            {
                assetPath = assetPath.Substring(idx);
            }

            result.Add(assetPath);
        }

        return result;
    }

    private void ClearLibraryShaderCache()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "Library", "ShaderCache");

        DeleteDirectoryContents(path);

        _lastReport = $"Cleared {path}";

        Debug.Log(_lastReport);
    }

    private void ClearLibraryShaderCompilerCache()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "Library", "ShaderCompilerCache");

        DeleteDirectoryContents(path);

        _lastReport = $"Cleared {path}";

        Debug.Log(_lastReport);
    }

    private static void DeleteDirectoryContents(string path)
    {
        if (Directory.Exists(path) == false)
        {
            return;
        }

        var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);

        for (var i = 0; i < files.Length; i++)
        {
            var file = files[i];

            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            catch
            { }
        }

        var dirs = Directory.GetDirectories(path);

        for (var j = 0; j < dirs.Length; j++)
        {
            var dir = dirs[j];

            try
            {
                Directory.Delete(dir, true);
            }
            catch
            { }
        }
    }

    private void RecompileAllShaders()
    {
        var allShaders = AssetDatabase.FindAssets("t:Shader t:ComputeShader");
        int reimported = 0;

        AssetDatabase.StartAssetEditing();

        try
        {
            for (var i = 0; i < allShaders.Length; i++)
            {
                var guid = allShaders[i];
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (path.StartsWith("Packages/") == false)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    reimported++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        _lastReport = $"Recompiled {reimported} project shaders";

        Debug.Log(_lastReport);
    }

    private void FullRefresh()
    {
        ClearLibraryShaderCache();
        ClearLibraryShaderCompilerCache();

        var paths = CollectShaderAssetPaths(_customRoot);

        AssetDatabase.StartAssetEditing();

        try
        {
            for (var i = 0; i < paths.Count; i++)
            {
                var p = paths[i];

                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        _lastReport = $"Full refresh: cleared shader caches and reimported {paths.Count} files in {_customRoot}";

        Debug.Log(_lastReport);
    }
}
