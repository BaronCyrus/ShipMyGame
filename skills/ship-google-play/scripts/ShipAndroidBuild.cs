// ShipMyGame — Android builds for Google Play (and the TapTap APK route), headless.
// Copy into Assets/Editor/ of the game, then:
//   unity_build.sh <project> Android ShipAndroidBuild.Aab release -shipVersion 1.0.1 -shipVersionCode 2
//   unity_build.sh <project> Android ShipAndroidBuild.Apk dev            (debug APK for an emulator or phone)
// Optional args: -shipOut <file>  -shipKeys <keystore.env>  (default ~/.shipmygame/keys/<applicationId>/keystore.env)
// keystore.env lives outside the project and is never committed:
//   KEYSTORE_PATH=/abs/path/upload.keystore
//   KEYSTORE_PASS=...
//   KEY_ALIAS=upload
//   KEY_PASS=...
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ShipAndroidBuild
{
    // Google Play's target API floor rises every August; keep this at or above the current requirement.
    const int MinTargetSdk = 36;

    public static void Aab() => Run(aab: true, release: true);
    public static void Apk() => Run(aab: false, release: false);
    public static void ReleaseApk() => Run(aab: false, release: true);

    static void Run(bool aab, bool release)
    {
        try
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            var nbt = NamedBuildTarget.Android;

            var version = Arg("-shipVersion", PlayerSettings.bundleVersion);
            var code = int.Parse(Arg("-shipVersionCode", PlayerSettings.Android.bundleVersionCode.ToString()));
            PlayerSettings.bundleVersion = version;
            PlayerSettings.Android.bundleVersionCode = code;

            PlayerSettings.SetScriptingBackend(nbt, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(nbt, release ? Il2CppCompilerConfiguration.Release : Il2CppCompilerConfiguration.Debug);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            // "Automatic" (0) means the highest platform installed, which can be older than Play's floor.
            if ((int)PlayerSettings.Android.targetSdkVersion < MinTargetSdk)
                PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)MinTargetSdk;
            PlayerSettings.SetManagedStrippingLevel(nbt, release ? ManagedStrippingLevel.Medium : ManagedStrippingLevel.Low);
#if UNITY_6000_0_OR_NEWER
            // Symbol table inside the .aab (BUNDLE-METADATA) so Play symbolicates native crashes.
            UnityEditor.Android.UserBuildSettings.DebugSymbols.level = release
                ? Unity.Android.Types.DebugSymbolLevel.SymbolTable : Unity.Android.Types.DebugSymbolLevel.None;
            UnityEditor.Android.UserBuildSettings.DebugSymbols.format =
                Unity.Android.Types.DebugSymbolFormat.Zip | Unity.Android.Types.DebugSymbolFormat.IncludeInBundle;
#else
            EditorUserBuildSettings.androidCreateSymbols = release ? AndroidCreateSymbols.Public : AndroidCreateSymbols.Disabled;
#endif
            if (release) ApplySigning();
            else PlayerSettings.Android.useCustomKeystore = false;

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) throw new Exception("No enabled scenes in Build Settings");
            CheckAudioListeners(scenes);

            EditorUserBuildSettings.buildAppBundle = aab;
            var ext = aab ? "aab" : "apk";
            var outPath = Arg("-shipOut", Path.Combine("Builds", $"{Safe(PlayerSettings.productName)}-{version}-{code}{(release ? "" : "-dev")}.{ext}"));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = release ? BuildOptions.None : BuildOptions.Development,
            });
            var s = report.summary;
            long bytes = File.Exists(outPath) ? new FileInfo(outPath).Length : 0;
            Debug.Log($"[Ship] result={s.result} out={Path.GetFullPath(outPath)} size={bytes / (1024 * 1024)}MB " +
                      $"id={PlayerSettings.GetApplicationIdentifier(nbt)} version={version} ({code}) targetSdk={(int)PlayerSettings.Android.targetSdkVersion}");
            if (s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
        catch (Exception e)
        {
            Debug.LogError("[Ship] build failed: " + e);
            EditorApplication.Exit(1);
        }
    }

    static void ApplySigning()
    {
        var id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var envPath = Arg("-shipKeys", Path.Combine(home, ".shipmygame", "keys", id, "keystore.env"));
        var env = ReadEnv(envPath);
        if (!env.TryGetValue("KEYSTORE_PATH", out var ks) || !File.Exists(ks))
            throw new Exception("Keystore not found via " + envPath + " (run make_upload_keystore.sh first)");
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = ks;
        PlayerSettings.Android.keystorePass = env["KEYSTORE_PASS"];
        PlayerSettings.Android.keyaliasName = env["KEY_ALIAS"];
        PlayerSettings.Android.keyaliasPass = env["KEY_PASS"];
        Debug.Log("[Ship] signing with " + ks + " alias " + env["KEY_ALIAS"]);
    }

    /// <summary>Both shipped test games were silent for days because the scene had no AudioListener.</summary>
    static void CheckAudioListeners(string[] scenes)
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (var path in scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int n = scene.GetRootGameObjects().Sum(go => go.GetComponentsInChildren<AudioListener>(true).Length);
                if (n != 1)
                    Debug.LogWarning($"[Ship] WARNING: {path} has {n} AudioListener(s); the game is silent with 0 unless one is added at runtime");
            }
        }
        finally
        {
            if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }

    static Dictionary<string, string> ReadEnv(string path)
    {
        var d = new Dictionary<string, string>();
        if (!File.Exists(path)) return d;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            int i = line.IndexOf('=');
            if (line.Length == 0 || line.StartsWith("#") || i <= 0) continue;
            d[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
        }
        return d;
    }

    static string Arg(string name, string fallback)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return fallback;
    }

    static string Safe(string s) => new string(s.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
}
