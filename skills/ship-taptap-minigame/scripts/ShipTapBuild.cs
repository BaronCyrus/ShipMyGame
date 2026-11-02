// ShipMyGame — TapTap mini-game export (WebGL build + TapTap converter, com.taptap.minigame), headless.
// Needs the SDK in Packages/ (run fetch_tapsdk.sh first). Copy into Assets/Editor/, write publish/taptap/build.json
// (see Config below), then:
//   unity_build.sh <project> WebGL ShipTapBuild.Export taptap
//   node tap_boot_test.js <dst>/minigame        (boot check in node before every upload)
// Output: <dst>/game.zip (upload it in 商店 → 游戏包管理 → 小游戏管理 → 包体管理 → 上传包体, or with taptap-cli)
// and game_wasm_split.zip.
// Every upload needs a higher version than the last one, and the version you type in 包体管理 must match it.
// Unity 6000.x: the framework patches below are the set a Unity 6000.6 game (SDK 2.0.21) shipped with when it passed
// TapTap's 包体准入测试 on Android (version 1.0.7, 2026-11-03), after 1.0.3–1.0.6 failed one problem at a time.
// That game also had to keep every WeChatWASM.WX call out of its TapTap build (see the TapTap skill's build notes).
// Define SHIP_NO_TAPTAP (Player Settings → Scripting Define Symbols) on branches without the TapTap SDK.
#if !SHIP_NO_TAPTAP
using System;
using System.IO;
using System.Text.RegularExpressions;
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
            DisableWasm2023();

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
    /// Fixes the converted framework for Unity 6000.x, which the TapTap converter only partly rewrites (the WeChat
    /// converter does these, so the shared UnityPlugin expects them). Each line below was a device failure:
    /// - the SDK's JS calls Module.dynCall_vi/vii/viii/viiiii (audio end, video, touch, sockets); 6000.x keeps them local;
    /// - the plugin writes the data package through unityNamespace.FS and mounts /idbfs with unityNamespace.IDBFS
    ///   ("reading 'createDataFile'" of undefined), and learns that main() ran from Module.calledMainCb;
    /// - every JS_Sound/JS_MobileKeyboard import goes to TJWASMSDK, which lacks the ones 6000.x added, and an import
    ///   that is not a function fails the wasm link ("function import requires a callable");
    /// - the plugin removes run dependency "wasm-preloadAssets" once the data package is in FS but nothing adds it, so
    ///   main() ran before the data arrived and Unity never started ("still waiting runDependencies");
    /// - Module.SystemInfo comes from Unity's web loader, which a mini-game never runs (main() threw in HasWebGPU);
    /// - the canvas has no id (Unity's "#"+id selector throws), its context fails the Safari WebGL check, and the
    ///   runtime has no crypto or alert().
    /// With WebAssembly 2023 on, the framework has no dynCalls table (DisableWasm2023 turns it off first; the
    /// getWasmTableEntry route is only a fallback, unverified on devices). Anchors marked optional exist only when the
    /// feature is linked; every other anchor must match exactly once, or the build stops instead of shipping a package
    /// that cannot start.
    /// </summary>
    static void PatchFramework(string dst)
    {
        var path = Path.Combine(dst, "minigame", "webgl.wasm.framework.unityweb.js");
        var js = File.ReadAllText(path);

        const string table = "getWasmTableEntry=function getWasmTableEntry(funcPtr){";
        if (js.Contains("dynCalls={};"))
            js = Once(js, path, "dynCalls={};",
                "dynCalls={};[\"vi\",\"vii\",\"viii\",\"viiiii\"].forEach(function(s){Module[\"dynCall_\"+s]=Module[\"dynCall_\"+s]||function(){return dynCalls[s].apply(null,arguments)}});");
        else if (js.Contains(table))
        {
            js = AfterStatement(js, table, "[\"vi\",\"vii\",\"viii\",\"viiiii\"].forEach(function(s){Module[\"dynCall_\"+s]=Module[\"dynCall_\"+s]||function(p){return getWasmTableEntry(p).apply(null,Array.prototype.slice.call(arguments,1))}});");
            Debug.Log("[Ship] no dynCalls table (WebAssembly 2023 was on?): Module.dynCall_* routed through getWasmTableEntry");
        }
        else throw new Exception("Neither a dynCalls table nor getWasmTableEntry in " + path);

        js = Once(js, path, "FS={root:null,", "FS=GameGlobal.unityNamespace.FS={root:null,");
        js = Once(js, path, "IDBFS={dbs:{},", "IDBFS=GameGlobal.unityNamespace.IDBFS={dbs:{},");
        js = Once(js, path, "if(buffer.buffer===HEAP8.buffer){canOwn=!1}",
            "if(typeof HEAP8!=\"undefined\"&&HEAP8&&buffer.buffer===HEAP8.buffer){canOwn=!1}");
        js = Once(js, path, "return createWasm();", "addRunDependency(\"wasm-preloadAssets\");return createWasm();");

        int infos = 0;
        js = Regex.Replace(js, @"Module\.SystemInfo(?![\w$])", m =>
        {
            infos++;
            return "(Module.SystemInfo||GameGlobal.unityNamespace.UnityLoader.SystemInfo)";
        });
        if (infos == 0) throw new Exception("No Module.SystemInfo in " + path);

        js = Once(js, path,
            "try{var ret=entryFunction(argc,argv);exitJS(ret,!0);return ret}catch(e){return handleException(e)}};",
            "try{var ret=entryFunction(argc,argv);exitJS(ret,!0);return ret}catch(e){return handleException(e)}" +
            "finally{if(!(typeof ABORT!=\"undefined\"&&ABORT)&&typeof Module.calledMainCb==\"function\")try{Module.calledMainCb()}catch(e){console.error(e)}}};");

        int guarded = 0;
        js = Regex.Replace(js, @"window\.TJWASMSDK\.(_\w+):", m =>
        {
            guarded++;
            return $"(window.TJWASMSDK.{m.Groups[1].Value}||tapMissingImport(\"{m.Groups[1].Value}\")):";
        });
        if (guarded == 0) throw new Exception("No TJWASMSDK imports in " + path);

        js = Optional(js, path, "_JS_Init_CopyPaste=function _JS_Init_CopyPaste2(){",
            "_JS_Init_CopyPaste=function _JS_Init_CopyPaste2(){if(Module.IsTjHostGame)return;");
        js = Once(js, path, "var canvasId=Module[\"canvas\"]?Module[\"canvas\"].id:\"unity-canvas\";",
            "var canvasId=Module[\"canvas\"]?Module[\"canvas\"].id:\"unity-canvas\";if(Module.IsTjHostGame&&typeof canvasId!=\"string\")return\"canvas\";");
        js = Once(js, path, "var gl=canvas.getContextSafariWebGL2Fixed(ver,attrs);",
            "var gl=canvas.getContextSafariWebGL2Fixed(ver,attrs);if(Module.IsTjHostGame)return gl;");
        js = Optional(js, path,
            "initRandomFill=function initRandomFill(){return function(view){return crypto.getRandomValues(view)}};",
            "initRandomFill=function initRandomFill(){if(typeof crypto==\"object\"&&crypto&&typeof crypto.getRandomValues==\"function\")return function(view){return crypto.getRandomValues(view)};" +
            "return function(view){for(var i=0;i<view.length;i++)view[i]=Math.random()*256|0;return view}};");

        const string strict = "\"use strict\";";
        const string helpers = "var alert=typeof alert==\"function\"?alert:function(m){console.error(m)};" +
            "function tapMissingImport(n){return function(){if(!tapMissingImport[n]){tapMissingImport[n]=1;console.warn(\"[Ship] TJWASMSDK lacks \"+n)}return 0}}";
        js = js.StartsWith(strict, StringComparison.Ordinal) ? strict + helpers + js.Substring(strict.Length) : helpers + js;
        File.WriteAllText(path, js);
        Debug.Log($"[Ship] framework patched, {guarded} TJWASMSDK imports guarded, {infos} SystemInfo reads: " + path);
    }

    static string Once(string js, string path, string anchor, string replacement)
    {
        int i = js.IndexOf(anchor, StringComparison.Ordinal);
        if (i < 0 || js.IndexOf(anchor, i + 1, StringComparison.Ordinal) >= 0)
            throw new Exception("Expected exactly one \"" + anchor + "\" in " + path);
        return js.Substring(0, i) + replacement + js.Substring(i + anchor.Length);
    }

    static string Optional(string js, string path, string anchor, string replacement)
    {
        if (js.Contains(anchor)) return Once(js, path, anchor, replacement);
        Debug.Log("[Ship] not linked in this build, skipped: " + anchor.Substring(0, Math.Min(40, anchor.Length)));
        return js;
    }

    /// <summary>Inserts <paramref name="code"/> after the statement that starts at <paramref name="anchor"/> and ends with its block.</summary>
    static string AfterStatement(string js, string anchor, string code)
    {
        int i = js.IndexOf('{', js.IndexOf(anchor, StringComparison.Ordinal)), depth = 0;
        do
        {
            if (js[i] == '{') depth++;
            else if (js[i] == '}') depth--;
            i++;
        } while (depth > 0 && i < js.Length);
        if (i < js.Length && js[i] == ';') i++;
        return js.Insert(i, code);
    }

    /// <summary>
    /// Gives TapTap's Android runtime a way to load the wasm. Its WebAssembly.instantiate takes a package path and its
    /// game.js removes fetch; 6000.x's createWasm otherwise fetches build.wasm and fails ("fetch is not a function":
    /// a black/white screen on every Android device of the 包体准入测试). createWasm takes Module.instantiateWasm first,
    /// which the plugin fills from unityNamespace.instantiateWasm, so Android gets one here; iOS (WKWebView) loads as it is.
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
            "wasm.instantiate(file,imports).then(function(r){" +
            "try{if(typeof mod.wasmInstantiated===\"function\")mod.wasmInstantiated()}catch(e){console.warn(e)}" +
            "receiveInstance(r.instance,r.module)}," +
            "function(e){console.error(\"[Ship] wasm instantiate failed: \"+file,e)});return{}}})();";
        js = js.Replace(anchor, anchor + loader);
        File.WriteAllText(path, js);
        Debug.Log("[Ship] Android wasm loader added: " + path);
    }
#endif

    /// <summary>
    /// Every build that ran on WeChat and TapTap devices had WebAssembly 2023 and BigInt off; Unity 6000.x turns
    /// WebAssembly 2023 on for new projects, which passes 64-bit integers as BigInt to glue code the mini-game SDKs do
    /// not expect (main() threw "Cannot convert a BigInt value to a number" in fd_seek). Older Unity versions have
    /// neither property, so they are set through reflection.
    /// </summary>
    static void DisableWasm2023()
    {
        foreach (var name in new[] { "wasm2023", "webAssemblyBigInt" })
        {
            var p = typeof(PlayerSettings.WebGL).GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (p == null || !p.CanWrite) continue;
            if ((bool)p.GetValue(null)) Debug.Log("[Ship] turned off PlayerSettings.WebGL." + name);
            p.SetValue(null, false);
        }
    }

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
