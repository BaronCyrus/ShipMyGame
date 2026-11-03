// ShipMyGame — Windows 64-bit player for Steam, headless.
// Copy into Assets/Editor/ of the game, then:
//   unity_build.sh <project> Win64 ShipSteamBuild.Win64 steam-win64 [-shipVersion 1.0.0] [-shipOut <path/Game.exe>] [-shipIl2cpp]
// Scenes come from the enabled entries of the Build Settings / build profile scene list; an empty list fails.
// Mono by default: a macOS editor can't build Windows IL2CPP. Pass -shipIl2cpp only on a Windows editor
// that has Visual Studio's C++ tools and the Windows Build Support (IL2CPP) module.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class ShipSteamBuild
{
    public static void Win64()
    {
        try
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
                throw new Exception("no enabled scenes in the Build Settings scene list; add the game's scenes first");

            var il2cpp = Environment.GetCommandLineArgs().Contains("-shipIl2cpp");
            if (il2cpp && Application.platform != RuntimePlatform.WindowsEditor)
                throw new Exception("Windows IL2CPP players can only be built on Windows; drop -shipIl2cpp to build with Mono");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,
                il2cpp ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x);

            var version = Arg("-shipVersion", PlayerSettings.bundleVersion);
            PlayerSettings.bundleVersion = version;

            var output = Arg("-shipOut", Path.Combine("Builds", "Steam", "Windows", PlayerSettings.productName + ".exe"));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            Debug.Log($"[ShipSteam] {summary.result}: {output} ({summary.totalSize / (1024 * 1024)} MB), " +
                      $"backend {PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone)}, " +
                      $"version {version}, {scenes.Length} scene(s)");
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
        catch (Exception e)
        {
            Debug.LogError("[ShipSteam] failed: " + e.Message);
            EditorApplication.Exit(1);
        }
    }

    static string Arg(string name, string fallback)
    {
        var args = Environment.GetCommandLineArgs();
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    }
}
