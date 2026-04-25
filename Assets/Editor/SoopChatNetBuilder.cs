using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

public static class SoopChatNetBuilder
{
    private const string AssemblyName = "SoopChatNet";

    [MenuItem("SoopChatNet/Build DLL")]
    public static void BuildDLL()
    {
        if (EditorApplication.isCompiling)
        {
            Debug.LogWarning("[SoopChatNetBuilder] Unity is compiling. Please wait and try again.");
            return;
        }

        var projectDir = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var compiledDll = Path.Combine(projectDir, "Library", "ScriptAssemblies", $"{AssemblyName}.dll");

        if (!File.Exists(compiledDll))
        {
            Debug.LogError($"[SoopChatNetBuilder] '{AssemblyName}.dll' not found in Library/ScriptAssemblies. Make sure the project has no compile errors.");
            return;
        }

        var outputDir = Path.Combine(projectDir, "Build");
        Directory.CreateDirectory(outputDir);

        var outputPath = Path.Combine(outputDir, $"{AssemblyName}.dll");
        File.Copy(compiledDll, outputPath, overwrite: true);

        CopyDependency(
            Path.Combine(Application.dataPath, "Scripts", "Newtonsoft.Json.dll"),
            Path.Combine(outputDir, "Newtonsoft.Json.dll"));

        Debug.Log($"[SoopChatNetBuilder] Build succeeded → {outputPath}");
        EditorUtility.RevealInFinder(outputPath);
    }

    [MenuItem("SoopChatNet/Force Recompile + Build DLL")]
    public static void ForceRecompileAndBuild()
    {
        if (EditorApplication.isCompiling)
        {
            Debug.LogWarning("[SoopChatNetBuilder] Already compiling. Please wait.");
            return;
        }

        Debug.Log("[SoopChatNetBuilder] Requesting script recompilation...");
        CompilationPipeline.compilationFinished += OnCompilationFinished;
        CompilationPipeline.RequestScriptCompilation();
    }

    [MenuItem("SoopChatNet/Open Output Folder")]
    public static void OpenOutputFolder()
    {
        var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Build"));
        Directory.CreateDirectory(dir);
        EditorUtility.RevealInFinder(dir);
    }

    private static void OnCompilationFinished(object _)
    {
        CompilationPipeline.compilationFinished -= OnCompilationFinished;
        BuildDLL();
    }

    private static void CopyDependency(string src, string dest)
    {
        if (!File.Exists(src)) return;
        File.Copy(src, dest, overwrite: true);
        Debug.Log($"[SoopChatNetBuilder] Copied {Path.GetFileName(dest)}");
    }
}
