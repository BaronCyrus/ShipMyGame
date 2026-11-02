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
    /// Fixes the converted framework JS for Unity 6000.x (each verified on a phone):
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
