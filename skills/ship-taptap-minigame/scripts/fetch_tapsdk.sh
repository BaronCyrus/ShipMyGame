#!/usr/bin/env bash
# Embed the TapTap mini-game Unity SDK (com.taptap.minigame) in <project>/Packages/ and, on Unity 6, patch it.
# usage: fetch_tapsdk.sh <Unity project dir> [tag]
# The SDK is a fork of the WeChat one and ships WeChat's UnityPlugin, so it cannot share a project with the WeChat
# SDK (both define WeChatWASM types): keep each channel on its own git branch or worktree.
# Unity 6000.x fixes (verified with Unity 6000.6 + SDK 2.0.21):
#  1. The debug tools' config helper uses BuildProfile.miniGameSettings (Tuanjie 1.5+ only) behind
#     UNITY_6000_0_OR_NEWER, which breaks every Unity 6 compile -> guard it with TUANJIE_1_5_OR_NEWER.
#  2. Drop writeStackCookie/checkStackCookie from EXPORTED_RUNTIME_METHODS.
#  3. Export FS and the HEAP views on Module.
#  4. Export _main, _malloc and _free. The SDK appends ",_main" (2021.2.5 only) after
#     "-s ERROR_ON_UNDEFINED_SYMBOLS=0", where it lands in the wrong flag, so they go in front of _sbrk.
# Git-ignore Packages/com.taptap.minigame/ and rerun this after a fresh checkout. Set https_proxy if GitHub is slow.
set -euo pipefail
PROJECT="$(cd "${1:?Unity project dir}" && pwd)"
REPO="${TAPSDK_REPO:-https://github.com/taptap/minigame-sdk-unity.git}"
TAG="${2:-2.0.21}"
DST="$PROJECT/Packages/com.taptap.minigame"
MAJOR="$(sed -n 's/^m_EditorVersion: \([0-9]*\).*/\1/p' "$PROJECT/ProjectSettings/ProjectVersion.txt")"

[ -d "$PROJECT/Packages/com.qq.weixin.minigame" ] && { echo "remove Packages/com.qq.weixin.minigame first (type clash)" >&2; exit 1; }
rm -rf "$DST"
git -c advice.detachedHead=false clone --quiet --depth 1 --branch "$TAG" "$REPO" "$DST"
rm -rf "$DST/.git"

if [ "${MAJOR:-0}" -ge 6000 ]; then
python3 -I - "$DST" <<'PY'
import sys, pathlib
root = pathlib.Path(sys.argv[1])
Q = '\\"'
patches = [
    ("TapMiniGameTools.Debug/Editor/TapMiniGameConfigHelper.cs", "#if UNITY_6000_0_OR_NEWER|| TUANJIE_1_0_OR_NEWER", "#if TUANJIE_1_5_OR_NEWER"),
    ("Editor/Scripts/TJExtDef.cs", f",{Q}writeStackCookie{Q},{Q}checkStackCookie{Q}", ""),
    ("Editor/Scripts/TJExtDef.cs", f",{Q}stringToUTF8{Q}\";",
     f",{Q}stringToUTF8{Q},{Q}FS{Q},{Q}HEAP8{Q},{Q}HEAPU8{Q},{Q}HEAP16{Q},{Q}HEAPU16{Q},{Q}HEAP32{Q},{Q}HEAPF32{Q}\";"),
    ("Editor/Scripts/TJExtDef.cs", "EXPORTED_FUNCTIONS=_sbrk,", "EXPORTED_FUNCTIONS=_main,_malloc,_free,_sbrk,"),
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
echo "TapTap mini-game SDK $TAG embedded at $DST (Unity major $MAJOR)"
