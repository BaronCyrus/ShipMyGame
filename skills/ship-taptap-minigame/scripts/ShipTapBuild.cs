// ShipMyGame — TapTap mini-game export (WebGL build + TapTap converter, com.taptap.minigame), headless.
// Needs the SDK in Packages/ (run fetch_tapsdk.sh first). Copy into Assets/Editor/, write publish/taptap/build.json
// (see Config below), then:
//   unity_build.sh <project> WebGL ShipTapBuild.Export taptap
// Output: <dst>/game.zip (upload it in 商店 → 游戏包管理 → 小游戏管理 → 包体管理 → 上传包体, or with taptap-cli)
// and game_wasm_split.zip.
// Every upload needs a higher version than the last one, and the version you type in 包体管理 must match it.
// Define SHIP_NO_TAPTAP (Player Settings → Scripting Define Symbols) on branches without the TapTap SDK.
#if !SHIP_NO_TAPTAP
using System;
using System.IO;
using minihost.editor;
using TapTapMiniGame;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class ShipTapBuild
{
    [Serializable]
    class Config
    {
        public string appid = "";               // MiniApp ID from 小游戏基本信息; the TAP_APPID environment variable overrides it
        public string version = "";             // TapTap's own version numbering, e.g. 1.0.0; required
        public string orientation = "Portrait"; // a TJScreenOritation member name
        public string dst = "Builds/taptap";
        public string cjkFont = "";             // e.g. Assets/Fonts/CJKSubset.ttf from make_cjk_font.py
        public string cjkFamily = "CJK Subset";
        public string[] latinFonts = new string[0];
    }

    const string ConfigPath = "publish/taptap/build.json";

    public static void Export()
    {
        bool ok = false;
        try
        {
            var cfg = File.Exists(ConfigPath) ? JsonUtility.FromJson<Config>(File.ReadAllText(ConfigPath)) : new Config();
            var appid = Environment.GetEnvironmentVariable("TAP_APPID") ?? cfg.appid;
            if (string.IsNullOrEmpty(appid)) throw new Exception("No MiniApp ID: set appid in " + ConfigPath + " or TAP_APPID");
            if (string.IsNullOrEmpty(cfg.version)) throw new Exception("Set version in " + ConfigPath + " (higher than the last upload)");
            EnsureCjkFallback(cfg);

            var nbt = NamedBuildTarget.WebGL;
            PlayerSettings.bundleVersion = cfg.version;
            PlayerSettings.SetManagedStrippingLevel(nbt, ManagedStrippingLevel.Medium);
            PlayerSettings.SetIl2CppCompilerConfiguration(nbt, Il2CppCompilerConfiguration.Release);
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SplashScreen.show = false;

            var config = TapTapUtil.GetEditorConf();
            var conf = config.ProjectConf;
            conf.Appid = appid;
            conf.Orientation = (TJScreenOritation)Enum.Parse(typeof(TJScreenOritation), cfg.orientation);
            conf.assetLoadType = 1;          // assets inside the package: TapTap's CDN option is for partner games only
            conf.compressDataPackage = true; // Brotli
            conf.DST = Path.GetFullPath(cfg.dst);
            config.CompileOptions.Webgl2 = true; // Unity 6 has no WebGL 1 (see ShipWeChatBuild)
            config.CompileOptions.showMonitorSuggestModal = false;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            foreach (var zip in new[] { "game.zip", "game_wasm_split.zip" })
                File.Delete(Path.Combine(conf.DST, zip));
            bool started = TapTapBuildWindowHelper.BuildTapTapMiniGame(false);
            ok = started && File.Exists(Path.Combine(conf.DST, "game.zip"));
#if UNITY_6000_0_OR_NEWER
            if (ok)
            {
                PatchFramework(conf.DST);
                PatchNamespace(conf.DST);
                TapTapConvertCore.PostProcess(); // zips both packages again, now with the patched files
            }
#endif
            var zipPath = Path.Combine(conf.DST, "game.zip");
            Debug.Log($"[Ship] taptap export={(ok ? "SUCCEED" : "FAILED")} zip={zipPath} " +
                      $"size={(File.Exists(zipPath) ? new FileInfo(zipPath).Length / (1024 * 1024) : 0)}MB version={cfg.version} appid={appid}");
        }
        catch (Exception e)
        {
            ok = false;
            Debug.LogError("[Ship] taptap export failed: " + e);
        }
        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
    }

#if UNITY_6000_0_OR_NEWER
    /// <summary>
    /// The SDK's JS calls Module.dynCall_vi/vii/viii/viiiii (audio end, video, touch, sockets); 6000.x keeps them local.
    /// A game whose framework has a dynCalls table gets them from it (verified). A framework without one (seen in a
    /// near-empty project) routes them through getWasmTableEntry instead; that path is not yet verified on a device.
    /// </summary>
    static void PatchFramework(string dst)
    {
        var path = Path.Combine(dst, "minigame", "webgl.wasm.framework.unityweb.js");
        var js = File.ReadAllText(path);
        const string anchor = "dynCalls={};";
        const string table = "getWasmTableEntry=function getWasmTableEntry(funcPtr){";
        if (js.IndexOf(anchor, StringComparison.Ordinal) >= 0)
        {
            js = js.Replace(anchor, anchor + "[\"vi\",\"vii\",\"viii\",\"viiiii\"].forEach(function(s){Module[\"dynCall_\"+s]=Module[\"dynCall_\"+s]||function(){return dynCalls[s].apply(null,arguments)}});");
        }
        else if (js.IndexOf(table, StringComparison.Ordinal) >= 0)
        {
            int i = js.IndexOf('{', js.IndexOf(table, StringComparison.Ordinal)), depth = 0;
            do
            {
                if (js[i] == '{') depth++;
                else if (js[i] == '}') depth--;
                i++;
            } while (depth > 0 && i < js.Length);
            if (i < js.Length && js[i] == ';') i++;
            js = js.Insert(i, "[\"vi\",\"vii\",\"viii\",\"viiiii\"].forEach(function(s){Module[\"dynCall_\"+s]=Module[\"dynCall_\"+s]||function(p){return getWasmTableEntry(p).apply(null,Array.prototype.slice.call(arguments,1))}});");
            Debug.LogWarning("[Ship] no dynCalls table: Module.dynCall_* routed through getWasmTableEntry (check sound and touch on a device)");
        }
        else throw new Exception("Neither \"" + anchor + "\" nor getWasmTableEntry in " + path);
        File.WriteAllText(path, js);
        Debug.Log("[Ship] framework patched: " + path);
    }

    /// <summary>
    /// TapTap's Android runtime removes fetch (game.js sets GameGlobal.fetch=void 0) and its WebAssembly.instantiate
    /// takes a package path. 6000.x's createWasm falls back to fetch unless Module.instantiateWasm is set, which ends
    /// in a black/white screen on every Android device of the 包体准入测试 ("fetch is not a function"). The plugin copies
    /// unityNamespace.instantiateWasm into Module, so Android gets one here; iOS (WKWebView) loads as it is.
    /// </summary>
    static void PatchNamespace(string dst)
    {
        var path = Path.Combine(dst, "minigame", "unity-namespace.js");
        var js = File.ReadAllText(path);
        const string anchor = "GameGlobal.unityNamespace=GameGlobal.unityNamespace||unityNamespace;";
        if (js.IndexOf(anchor, StringComparison.Ordinal) < 0) throw new Exception("No \"" + anchor + "\" in " + path);
        const string loader =
            "(function(){var sys=tj.getSystemInfoSync()||{};if(sys.platform!==\"android\")return;" +
            "GameGlobal.unityNamespace.instantiateWasm=function(imports,receiveInstance){" +
            "var mod=this,ns=GameGlobal.unityNamespace,cfg=GameGlobal.managerConfig||{};" +
            "var wasm=typeof WXWebAssembly!==\"undefined\"?WXWebAssembly:WebAssembly;" +
            "var file=mod.wasmPath||\"wasmcode/\"+(ns.CODE_FILE_MD5||cfg.CODE_FILE_MD5)+\".\"+(ns.GAME_NAME||cfg.GAME_NAME)+\".wasm.code.unityweb.wasm.br\";" +
            "wasm.instantiate(file,imports).then(function(r){receiveInstance(r.instance,r.module);" +
            "try{if(typeof mod.wasmInstantiated===\"function\")mod.wasmInstantiated()}catch(e){console.warn(e)}}," +
            "function(e){console.error(\"[Ship] wasm instantiate failed: \"+file,e)});return{}}})();";
        js = js.Replace(anchor, anchor + loader);
        File.WriteAllText(path, js);
        Debug.Log("[Ship] Android wasm loader added: " + path);
    }
#endif

    /// <summary>WebGL has no OS font fallback; see ShipWeChatBuild.EnsureCjkFallback, which this mirrors.</summary>
    static void EnsureCjkFallback(Config cfg)
    {
        if (string.IsNullOrEmpty(cfg.cjkFont) || cfg.latinFonts.Length == 0) return;
        var cjk = AssetDatabase.LoadAssetAtPath<Font>(cfg.cjkFont);
        if (cjk == null) throw new Exception("Missing " + cfg.cjkFont + " (run make_cjk_font.py)");
        foreach (var path in cfg.latinFonts)
        {
            var importer = AssetImporter.GetAtPath(path);
            if (importer == null) throw new Exception("Font not found: " + path);
            var so = new SerializedObject(importer);
            var names = so.FindProperty("m_FontNames") ?? so.FindProperty("fontNames");
            var refs = so.FindProperty("m_FallbackFontReferences") ?? so.FindProperty("fallbackFontReferences");
            if (names == null || refs == null) throw new Exception("TrueTypeFontImporter font name fields not found on " + path);
            if (refs.arraySize == 1 && refs.GetArrayElementAtIndex(0).objectReferenceValue == cjk) continue;
            var own = ((Font)AssetDatabase.LoadMainAssetAtPath(path)).fontNames;
            names.arraySize = 2;
            names.GetArrayElementAtIndex(0).stringValue = own != null && own.Length > 0 ? own[0] : Path.GetFileNameWithoutExtension(path);
            names.GetArrayElementAtIndex(1).stringValue = cfg.cjkFamily;
            refs.arraySize = 1;
            refs.GetArrayElementAtIndex(0).objectReferenceValue = cjk;
            so.ApplyModifiedPropertiesWithoutUndo();
            importer.SaveAndReimport();
            Debug.Log("[Ship] CJK fallback set on " + path);
        }
    }
}
#endif
