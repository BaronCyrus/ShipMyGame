#!/usr/bin/env bash
# Embed the WeChat mini-game Unity SDK in <project>/Packages/com.qq.weixin.minigame and, on Unity 6, patch it.
# usage: fetch_wxsdk.sh <Unity project dir> [git revision]
# The SDK officially targets Unity 2018–2022 and Tuanjie; on Unity 6000.x it needs these fixes (each one was
# found by a failing build or a black screen on Unity 6000.6 with SDK v0.1.34, then verified on a phone):
#  1. Object.GetInstanceID() is a compile error ("Use GetEntityId instead") -> GetHashCode().
#  2. Newer Emscripten rejects writeStackCookie/checkStackCookie in EXPORTED_RUNTIME_METHODS (nothing calls them).
#  3. FS and the HEAP views are no longer on Module unless exported; the plugin loads the data package through
#     Module.FS and the SDK's audio reads Module.HEAPU8/HEAPF32.
#  4. _main is exported only for 2021.2.5, so the linker drops the engine (a 170 KB wasm) -> export it on 6000 too.
#  5. _malloc/_free are not exported; the SDK's JS (touch, video, sockets) calls them on Module.
#  6. The project.config.json template spells condition.game "currentL" -> devtools: "app.json 或自定义编译条件错误".
# Use a local copy (not a git dependency): Unity's own git often cannot reach the repo through a proxy.
# Git-ignore Packages/com.qq.weixin.minigame/ and rerun this after a fresh checkout.
# Override the source with WXSDK_REPO (GitHub mirror: https://github.com/wechat-miniprogram/minigame-tuanjie-transform-sdk.git).
set -euo pipefail
PROJECT="$(cd "${1:?Unity project dir}" && pwd)"
REPO="${WXSDK_REPO:-https://gitee.com/wechat-minigame/minigame-tuanjie-transform-sdk.git}"
REV="${2:-915d4bf4f60ef0cd1c29316a8d67257185d609a7}"   # v0.1.34, the version the patches were verified on
BRANCH="${WXSDK_BRANCH:-v0.1.34}"
DST="$PROJECT/Packages/com.qq.weixin.minigame"
MAJOR="$(sed -n 's/^m_EditorVersion: \([0-9]*\).*/\1/p' "$PROJECT/ProjectSettings/ProjectVersion.txt")"

rm -rf "$DST"
# The repo history carries large binaries: clone only the release branch tip, then pin the exact commit.
git -c advice.detachedHead=false clone --quiet --depth 1 --branch "$BRANCH" "$REPO" "$DST"
if [ "$(git -C "$DST" rev-parse HEAD)" != "$REV" ]; then
  if ! { git -C "$DST" fetch --quiet --depth 1 origin "$REV" && git -C "$DST" -c advice.detachedHead=false checkout --quiet FETCH_HEAD; }; then
    echo "branch $BRANCH moved past $REV and that commit cannot be fetched shallowly; pass a revision or WXSDK_BRANCH" >&2
    exit 1
  fi
fi
rm -rf "$DST/.git"

if [ "${MAJOR:-0}" -ge 6000 ]; then
python3 -I - "$DST" <<'PY'
import sys, pathlib
root = pathlib.Path(sys.argv[1])
Q = '\\"'   # the export lists live inside C# string literals, so quotes appear as \"
patches = [
    ("Runtime/WXRuntimeExtDef.cs", "return unityObject.GetInstanceID();", "return unityObject.GetHashCode();"),
    ("Editor/WXConvertCore.cs", f",{Q}writeStackCookie{Q},{Q}checkStackCookie{Q}", ""),
    ("Editor/WXConvertCore.cs", f",{Q}stringToUTF8{Q}\";",
     f",{Q}stringToUTF8{Q},{Q}FS{Q},{Q}HEAP8{Q},{Q}HEAPU8{Q},{Q}HEAP16{Q},{Q}HEAPU16{Q},{Q}HEAP32{Q},{Q}HEAPF32{Q}\";"),
    ("Editor/WXConvertCore.cs", "\n#if UNITY_2021_2_5\n", "\n#if UNITY_2021_2_5 || UNITY_6000_0_OR_NEWER\n"),
    ("Editor/WXConvertCore.cs", 'PlayerSettings.WebGL.emscriptenArgs += ",_main";', 'PlayerSettings.WebGL.emscriptenArgs += ",_main,_malloc,_free";'),
    ("Runtime/wechat-default/project.config.json", '"currentL": -1', '"current": -1'),
]
failed = False
for rel, old, new in patches:
    f = root / rel
    s = f.read_text(encoding="utf-8")
    if old in s:
        f.write_text(s.replace(old, new), encoding="utf-8")
        print(f"patched {rel}: {old[:60]!r}")
    elif new and new in s:
        print(f"already fixed upstream {rel}: {new[:60]!r}")
    else:
        failed = True
        print(f"PATCH NOT APPLIED {rel}: {old[:60]!r} not found; the SDK changed, check whether this fix is still needed", file=sys.stderr)
sys.exit(1 if failed else 0)
PY
fi
echo "WeChat SDK $REV embedded at $DST (Unity major $MAJOR)"
