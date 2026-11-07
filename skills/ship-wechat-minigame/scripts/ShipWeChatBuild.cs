// ShipMyGame — WeChat mini-game export (WebGL build + WeChat converter), headless.
// Needs the WeChat Unity SDK in Packages/ (run fetch_wxsdk.sh first). Copy into Assets/Editor/, write
// publish/wechat/build.json (see Config below), then:
//   unity_build.sh <project> WebGL ShipWeChatBuild.Export wechat
// Output: <dst>/minigame — open it in 微信开发者工具, or preview/upload it with miniprogram-ci.
// A 测试号 AppID cannot run Unity games: the SDK's game.js always loads the UnityPlugin, which needs a
// registered mini-game AppID with 快适配 enabled (能力地图 → 研发能力 → 生产提效包 → 快适配).
// Define SHIP_NO_WECHAT (Player Settings → Scripting Define Symbols) on branches without the WeChat SDK.
#if !SHIP_NO_WECHAT
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using WeChatWASM;

public static class ShipWeChatBuild
{
    [Serializable]
    class Config
    {
        public string appid = "";               // 小游戏 AppID; the WX_APPID environment variable overrides it
        public string projectName = "";         // defaults to PlayerSettings.productName
        public string orientation = "Portrait"; // a WXScreenOritation member name
        public string dst = "Builds/wechat";
        public int assetLoadType = 1;           // 1: first-package assets inside the package (verified); 0: load them from cdn
        public string cdn = "";                 // required when assetLoadType is 0; the CDN route is not yet verified
        public string cjkFont = "";             // e.g. Assets/Fonts/CJKSubset.ttf from make_cjk_font.py
        public string cjkFamily = "CJK Subset"; // the family name make_cjk_font.py wrote
        public string[] latinFonts = new string[0]; // uGUI (TrueType) fonts that need the CJK fallback
    }

    const string ConfigPath = "publish/wechat/build.json";

    public static void Export()
    {
        bool ok = false;
        try
        {
            var cfg = File.Exists(ConfigPath) ? JsonUtility.FromJson<Config>(File.ReadAllText(ConfigPath)) : new Config();
            var appid = Environment.GetEnvironmentVariable("WX_APPID") ?? cfg.appid;
            if (string.IsNullOrEmpty(appid)) throw new Exception("No AppID: set appid in " + ConfigPath + " or WX_APPID");
            EnsureCjkFallback(cfg);

            var nbt = NamedBuildTarget.WebGL;
            PlayerSettings.SetManagedStrippingLevel(nbt, ManagedStrippingLevel.Medium);
            PlayerSettings.SetIl2CppCompilerConfiguration(nbt, Il2CppCompilerConfiguration.Release);
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SplashScreen.show = false;
            DisableWasm2023();

            var conf = WXConvertCore.config.ProjectConf;
            conf.projectName = string.IsNullOrEmpty(cfg.projectName) ? PlayerSettings.productName : cfg.projectName;
            conf.Appid = appid;
            conf.Orientation = (WXScreenOritation)Enum.Parse(typeof(WXScreenOritation), cfg.orientation);
            if (cfg.assetLoadType == 0 && string.IsNullOrEmpty(cfg.cdn)) throw new Exception("assetLoadType 0 needs cdn in " + ConfigPath);
            conf.assetLoadType = cfg.assetLoadType;
            if (!string.IsNullOrEmpty(cfg.cdn)) conf.CDN = cfg.cdn;
            conf.compressDataPackage = true; // Brotli
            conf.relativeDST = cfg.dst;
            conf.DST = Path.GetFullPath(cfg.dst);
            // Unity 6 has no WebGL 1: with this off the SDK picks GLES2, asks for a WebGL 1 canvas and every URP
            // shader is "not supported on this GPU" -> black screen.
            WXConvertCore.config.CompileOptions.Webgl2 = true;
            // The SDK's 「优化建议」 launch modal shows in 开发版/体验版 (and in 备案 screenshots); never in release.
            WXConvertCore.config.CompileOptions.showMonitorSuggestModal = false;
            EditorUtility.SetDirty(WXConvertCore.config);
            AssetDatabase.SaveAssets();

            var result = WXConvertCore.DoExport(true);
            ok = result == WXConvertCore.WXExportError.SUCCEED;
#if UNITY_6000_0_OR_NEWER
            if (ok) PatchFramework(conf.DST);
#endif
            Debug.Log($"[Ship] wechat export={result} dst={conf.DST}/minigame appid={appid} version={PlayerSettings.bundleVersion}");
        }
        catch (Exception e)
        {
            ok = false;
            Debug.LogError("[Ship] wechat export failed: " + e);
        }
        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
    }

#if UNITY_6000_0_OR_NEWER
    /// <summary>
    /// Fixes the converted framework JS for Unity 6000.x (all found on 6000.6; B1-B5 verified on phones, B7 in the
    /// devtools simulator with a held-back data package):
    /// - B7: the engine must wait for the plugin's data package (see WaitForDataPackage);
    /// - the SDK swaps Unity's audio/video/keyboard JS for wx versions only when declared as `function _JS_Sound_X(`;
    ///   6000.x also emits `var _JS_Sound_Init=function(){...}`, whose Web Audio path ends in alert();
    /// - mini-games have no alert(), so Unity's fatal-error popups become console errors;
    /// - ConnectToProfiler is exported by a pre-js even when the function is not linked in;
    /// - the converter's Module.dynCall_vi calls getWasmTableEntry, which 6000.x does not emit;
    /// - the SDK's JS calls Module.dynCall_viii (touch) and others that 6000.x keeps local.
    /// </summary>
    static void PatchFramework(string dst)
    {
        var path = Path.Combine(dst, "minigame", "webgl.wasm.framework.unityweb.js");
        var js = File.ReadAllText(path);
        js = WaitForDataPackage(js);
        js = RouteToSdk(js);
        js = "var alert = globalThis.alert || function (m) { console.error(m); };" + js;
        js = js.Replace("Module[\"ConnectToProfiler\"]=ConnectToProfiler;",
            "Module[\"ConnectToProfiler\"]=typeof ConnectToProfiler==\"function\"?ConnectToProfiler:function(){};");
        js = js.Replace("return getWasmTableEntry(cb)(arg1);",
            "return (typeof getWasmTableEntry==\"function\"?getWasmTableEntry(cb):wasmTable.get(cb))(arg1);");
        js = js.Replace("Module.dynCall_vi = Module.dynCall_vi ||",
            "if (typeof dynCalls==\"object\") for (var k in dynCalls) Module[\"dynCall_\"+k]=Module[\"dynCall_\"+k]||dynCalls[k]; Module.dynCall_vi = Module.dynCall_vi ||");
        File.WriteAllText(path, js);
        Debug.Log("[Ship] framework patched: " + path);
    }

    /// <summary>
    /// B7. The UnityPlugin unpacks the first data package (boot.config, scenes, Resources) into the file system and
    /// then calls removeRunDependency("wasm-preloadAssets"). The SDK adds that dependency, and calls
    /// Module.wasmInstantiated, by rewriting addRunDependency("wasm-instantiate"), but the emscripten in Unity 6000.6
    /// no longer has that dependency: it does `wasmExports=await (createWasm());run();`, and the export log lists the
    /// SDK's rules as "UnMatched WXReplaceRules". So run() never waited for the data. With the data cached (every launch
    /// after the first) it is unpacked before the wasm compiles and nothing shows; on a first launch with a slow
    /// download main() runs on an empty file system ("no boot config - using default values") and the screen stays
    /// black. Review opens the game on a fresh device, so it rejected a build that always worked on the developer's
    /// phone. It is also why the plugin logged "still waiting runDependencies": its count ended at -1.
    /// The monitor call also gets the dependency id back, as the SDK's rule gives it, so the plugin's logs name it.
    /// Where the SDK's rule did match (an emscripten that still has "wasm-instantiate") the framework is left alone;
    /// a framework with neither stops the export instead of shipping a package that is black on a first launch.
    /// </summary>
    static string WaitForDataPackage(string js)
    {
        if (js.Contains("addRunDependency(\"wasm-preloadAssets\")"))
        {
            Debug.Log("[Ship] B7 not needed: the SDK already makes the engine wait for the data package");
            return js;
        }
        js = ReplaceExactly(js, "wasmExports=await (createWasm());run();",
            "addRunDependency(\"wasm-preloadAssets\");" +
            "if(GameGlobal.manager&&GameGlobal.manager.TimeLogger)GameGlobal.manager.TimeLogger.timeStart(\"wasm编译耗时\");" +
            "wasmExports=await (createWasm());" +
            "try{if(Module.wasmInstantiated)Module.wasmInstantiated()}catch(e){console.error(\"wasmInstantiated\",e)}" +
            "run();", 1);
        // only the plugin's logs use the id, so a framework without these calls still exports
        js = js.Replace("Module[\"monitorRunDependencies\"]?.(runDependencies)",
            "Module[\"monitorRunDependencies\"]?.(runDependencies,id)");
        Debug.Log("[Ship] B7: the engine now waits for the data package");
        return js;
    }

    /// <summary>Replaces <paramref name="old"/>, failing the export unless it occurs exactly <paramref name="count"/> times.</summary>
    static string ReplaceExactly(string js, string old, string replacement, int count)
    {
        int found = 0;
        for (int i = js.IndexOf(old, StringComparison.Ordinal); i >= 0; i = js.IndexOf(old, i + old.Length, StringComparison.Ordinal)) found++;
        if (found != count)
            throw new Exception($"[Ship] framework patch expected {count}x but found {found}x: {old}");
        return js.Replace(old, replacement);
    }

    static readonly Regex SdkOwned = new Regex(@"var (_JS_(?:Sound|Video|MobileKeyboard|MobileKeybard)_\w+)=(?:function\s*\(|\()");

    static string RouteToSdk(string js)
    {
        var sb = new StringBuilder();
        int pos = 0;
        foreach (Match m in SdkOwned.Matches(js))
        {
            if (m.Index < pos) continue;
            int i = js.IndexOf('{', m.Index), depth = 0;
            do
            {
                if (js[i] == '{') depth++;
                else if (js[i] == '}') depth--;
                i++;
            } while (depth > 0 && i < js.Length);
            if (i < js.Length && js[i] == ';') i++;
            string name = m.Groups[1].Value;
            sb.Append(js, pos, m.Index - pos);
            sb.Append($"var {name} = window.WXWASMSDK.{name} ? window.WXWASMSDK.{name} : () => console.warn(\"unimplemented:{name}\", jsStackTrace());");
            pos = i;
        }
        sb.Append(js, pos, js.Length - pos);
        return sb.ToString();
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

    /// <summary>
    /// WebGL has no OS font fallback, so Chinese glyphs come from the bundled subset, listed in each Latin font's
    /// Font Names and fallback references. In 6000.x the importer's fontNames/fontReferences setters do not reach
    /// the .meta, so the serialized fields are written directly. TextMeshPro projects add a dynamic TMP_FontAsset
    /// made from the subset to their fallback list instead and leave latinFonts empty.
    /// </summary>
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
