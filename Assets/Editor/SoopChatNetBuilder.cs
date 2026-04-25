using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

public static class SoopChatNetBuilder
{
    private const string AssemblyName = "SoopChatNet";

    // -------------------------------------------------------------------------
    //  Menu items
    // -------------------------------------------------------------------------

    [MenuItem("SoopChatNet/Build DLL")]
    public static void BuildDLL()
    {
        if (!TryGetCompiledDll(out var compiledDll)) return;

        var outputDir = GetOutputDir();
        Directory.CreateDirectory(outputDir);

        File.Copy(compiledDll, Path.Combine(outputDir, $"{AssemblyName}.dll"), overwrite: true);
        CopyDependency(NewtonsoftSrc, Path.Combine(outputDir, "Newtonsoft.Json.dll"));

        var outputPath = Path.Combine(outputDir, $"{AssemblyName}.dll");
        Debug.Log($"[SoopChatNetBuilder] Build succeeded → {outputPath}");
        EditorUtility.RevealInFinder(outputPath);
    }

    [MenuItem("SoopChatNet/Build & Export UnityPackage")]
    public static void BuildAndExportPackage()
    {
        if (!TryGetCompiledDll(out var compiledDll)) return;

        var outputDir = GetOutputDir();
        Directory.CreateDirectory(outputDir);

        var entries = new[]
        {
            new PackageEntry(compiledDll,   "Assets/Plugins/SoopChatNet/SoopChatNet.dll",    EntryType.Dll),
            new PackageEntry(NewtonsoftSrc, "Assets/Plugins/SoopChatNet/Newtonsoft.Json.dll", EntryType.Dll),
            new PackageEntry(ExampleSrc,    "Assets/Scripts/Example.cs",                      EntryType.CSharp),
        };

        foreach (var e in entries)
        {
            if (!File.Exists(e.SourcePath))
            {
                Debug.LogError($"[SoopChatNetBuilder] File not found: {e.SourcePath}");
                return;
            }
        }

        var packagePath = Path.Combine(outputDir, $"{AssemblyName}.unitypackage");
        WriteUnityPackage(packagePath, entries);

        Debug.Log($"[SoopChatNetBuilder] Package exported → {packagePath}");
        EditorUtility.RevealInFinder(packagePath);
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
        var dir = GetOutputDir();
        Directory.CreateDirectory(dir);
        EditorUtility.RevealInFinder(dir);
    }

    // -------------------------------------------------------------------------
    //  Helpers
    // -------------------------------------------------------------------------

    private static string NewtonsoftSrc => Path.Combine(Application.dataPath, "Scripts", "Newtonsoft.Json.dll");
    private static string ExampleSrc    => Path.Combine(Application.dataPath, "Scripts", "Example.cs");

    private static string GetOutputDir() =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Build"));

    private static bool TryGetCompiledDll(out string path)
    {
        if (EditorApplication.isCompiling)
        {
            Debug.LogWarning("[SoopChatNetBuilder] Unity is compiling. Please wait and try again.");
            path = null;
            return false;
        }

        path = Path.Combine(
            Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
            "Library", "ScriptAssemblies", $"{AssemblyName}.dll");

        if (File.Exists(path)) return true;

        Debug.LogError($"[SoopChatNetBuilder] '{AssemblyName}.dll' not found in Library/ScriptAssemblies.");
        path = null;
        return false;
    }

    private static void CopyDependency(string src, string dest)
    {
        if (!File.Exists(src)) return;
        File.Copy(src, dest, overwrite: true);
        Debug.Log($"[SoopChatNetBuilder] Copied {Path.GetFileName(dest)}");
    }

    private static void OnCompilationFinished(object _)
    {
        CompilationPipeline.compilationFinished -= OnCompilationFinished;
        BuildDLL();
    }

    // -------------------------------------------------------------------------
    //  .unitypackage writer
    //  Format: gzip( tar( {guid}/asset, {guid}/asset.meta, {guid}/pathname ) )
    // -------------------------------------------------------------------------

    private static void WriteUnityPackage(string outputPath, PackageEntry[] entries)
    {
        using var fs = File.Create(outputPath);
        using var gz = new GZipStream(fs, System.IO.Compression.CompressionLevel.Optimal);

        foreach (var entry in entries)
        {
            string guid = Guid.NewGuid().ToString("N");
            byte[] asset    = File.ReadAllBytes(entry.SourcePath);
            byte[] meta     = Encoding.UTF8.GetBytes(BuildMeta(guid, entry.Type));
            byte[] pathname = Encoding.UTF8.GetBytes(entry.AssetPath);

            WriteTarEntry(gz, $"{guid}/asset",      asset);
            WriteTarEntry(gz, $"{guid}/asset.meta", meta);
            WriteTarEntry(gz, $"{guid}/pathname",   pathname);
        }

        gz.Write(new byte[1024], 0, 1024); // end-of-archive: two zero blocks
    }

    private static void WriteTarEntry(Stream stream, string name, byte[] data)
    {
        byte[] header = new byte[512];

        // name (≤99 chars + null)
        byte[] nameBytes = Encoding.ASCII.GetBytes(name);
        Array.Copy(nameBytes, header, Math.Min(nameBytes.Length, 99));

        WriteOctal(header, 100,  8, 0b110_100_100);                         // mode 0644
        WriteOctal(header, 108,  8, 0);                                      // uid
        WriteOctal(header, 116,  8, 0);                                      // gid
        WriteOctal(header, 124, 12, data.Length);                            // size
        WriteOctal(header, 136, 12, DateTimeOffset.UtcNow.ToUnixTimeSeconds()); // mtime

        header[156] = (byte)'0'; // type: regular file

        // ustar magic
        Encoding.ASCII.GetBytes("ustar").CopyTo(header, 257);
        header[263] = (byte)'0';
        header[264] = (byte)'0';

        // checksum — treat field as spaces, then write
        for (int i = 148; i < 156; i++) header[i] = (byte)' ';
        int checksum = 0;
        foreach (byte b in header) checksum += b;
        WriteOctal(header, 148, 7, checksum);
        header[155] = (byte)' ';

        stream.Write(header, 0, 512);
        stream.Write(data, 0, data.Length);

        int pad = data.Length % 512;
        if (pad > 0) stream.Write(new byte[512 - pad], 0, 512 - pad);
    }

    private static void WriteOctal(byte[] buf, int offset, int len, long value)
    {
        string s = Convert.ToString(value, 8).PadLeft(len - 1, '0');
        Encoding.ASCII.GetBytes(s).CopyTo(buf, offset);
        buf[offset + len - 1] = 0;
    }

    // -------------------------------------------------------------------------
    //  Meta file templates
    // -------------------------------------------------------------------------

    private enum EntryType { Dll, CSharp }

    private static string BuildMeta(string guid, EntryType type) => type switch
    {
        EntryType.Dll    => DllMeta(guid),
        EntryType.CSharp => CSharpMeta(guid),
        _                => throw new ArgumentOutOfRangeException()
    };

    private static string DllMeta(string guid) =>
$@"fileFormatVersion: 2
guid: {guid}
PluginImporter:
  externalObjects: {{}}
  serializedVersion: 2
  iconMap: {{}}
  executionOrder: {{}}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Any:
    second:
      enabled: 1
      settings: {{}}
  - first:
      Editor: Editor
    second:
      enabled: 0
      settings:
        DefaultValueInitialized: true
  userData:
  assetBundleName:
  assetBundleVariant:
";

    private static string CSharpMeta(string guid) =>
$@"fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData:
  assetBundleName:
  assetBundleVariant:
";

    // -------------------------------------------------------------------------
    //  PackageEntry
    // -------------------------------------------------------------------------

    private readonly struct PackageEntry
    {
        public readonly string    SourcePath;
        public readonly string    AssetPath;
        public readonly EntryType Type;

        public PackageEntry(string sourcePath, string assetPath, EntryType type)
        {
            SourcePath = sourcePath;
            AssetPath  = assetPath;
            Type       = type;
        }
    }
}
