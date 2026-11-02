// ShipMyGame — apply Play Games Services resources headlessly (the plugin's own setup window needs a click).
// Copy into Assets/Editor/, save the XML from Play Console (Play 游戏服务 → 设置和管理 → 配置 → 获取资源) as
// publish/google-play/games-ids.xml, then:
//   unity_build.sh <project> Android ShipPgsSetup.Run pgs
// It writes GPGSIds.cs (constants for every achievement/leaderboard), the APP_ID manifest library and
// Assets/GooglePlayGames/Resources/PlayGamesSettings.asset. Rerun it whenever you add achievements or boards.
// The plugin is called through reflection so this file compiles before the plugin is imported and on non-Android
// targets. Game code that calls the plugin itself still needs #if UNITY_ANDROID: with another active target
// (e.g. WebGL) the plugin's runtime assemblies are not compiled for the player.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ShipPgsSetup
{
    const string Xml = "publish/google-play/games-ids.xml";   // relative to the Unity project root
    const string IdsFolder = "Assets/Scripts";
    const string IdsClass = "GPGSIds";

    [MenuItem("Tools/ShipMyGame/Apply games-ids.xml")]
    public static void Run()
    {
        bool ok = false;
        try
        {
            if (!File.Exists(Xml)) throw new Exception("missing " + Path.GetFullPath(Xml));
            var upgrader = FindType("GooglePlayGames.Editor.GPGSUpgrader");
            var setup = FindType("GooglePlayGames.Editor.GPGSAndroidSetupUI");
            if (upgrader == null || setup == null)
                throw new Exception("Google Play Games plugin not found: import it first (Assets/GooglePlayGames)");
            const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            // In batch mode the upgrader's delayCall never runs, so record the plugin version the manifest needs here.
            upgrader.GetMethod("EnsureMigrated", any, null, Type.EmptyTypes, null)?.Invoke(null, null);
            Directory.CreateDirectory(IdsFolder);
            var perform = setup.GetMethods(any).First(m => m.Name == "PerformSetup" && m.GetParameters().Length == 5);
            ok = (bool)perform.Invoke(null, new object[] { null, IdsFolder, IdsClass, File.ReadAllText(Xml), null });
            Debug.Log("[Ship] PGS setup " + (ok ? "done" : "FAILED"));
        }
        catch (Exception e)
        {
            Debug.LogError("[Ship] PGS setup failed: " + e);
        }
        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
    }

    static Type FindType(string fullName) =>
        AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName, false)).FirstOrDefault(t => t != null);
}
