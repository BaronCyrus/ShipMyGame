#!/usr/bin/env bash
# Run a static C# method of a Unity project in batch mode (no editor window) and summarize the log.
# usage: unity_build.sh <project dir> <Android|WebGL|...> <Class.Method> [log name] [extra Unity args...]
# The editor is found from ProjectSettings/ProjectVersion.txt; set UNITY_PATH to override.
# Close the editor on this project first: Unity refuses to open a project that is already open.
set -uo pipefail

PROJECT="$(cd "${1:?project dir}" && pwd)"
TARGET="${2:?build target, e.g. Android or WebGL}"
METHOD="${3:?Class.Method}"
NAME="${4:-build}"
shift $(( $# >= 4 ? 4 : $# ))

VERSION="$(sed -n 's/^m_EditorVersion: //p' "$PROJECT/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"

find_unity() {
  [ -n "${UNITY_PATH:-}" ] && { echo "$UNITY_PATH"; return; }
  # Unity Hub lists every editor it knows (including ones on other disks) in editors-v2.json
  local cfg
  for cfg in "$HOME/Library/Application Support/UnityHub/editors-v2.json" "$HOME/.config/UnityHub/editors-v2.json" "${APPDATA:-/nonexistent}/UnityHub/editors-v2.json"; do
    [ -f "$cfg" ] && command -v python3 >/dev/null || continue
    local loc
    loc="$(python3 -I -c 'import json,sys
for e in json.load(open(sys.argv[1])).get("data", []):
    if e.get("version") == sys.argv[2]:
        print((e.get("location") or [""])[0]); break' "$cfg" "$VERSION" 2>/dev/null)"
    for exe in "$loc/Contents/MacOS/Unity" "$loc"; do
      [ -n "$loc" ] && [ -f "$exe" ] && [ -x "$exe" ] && { echo "$exe"; return; }
    done
  done
  local roots=("/Applications/Unity/Hub/Editor" "$HOME/Unity/Hub/Editor" "/c/Program Files/Unity/Hub/Editor")
  for r in "${roots[@]}"; do
    for exe in "$r/$VERSION/Unity.app/Contents/MacOS/Unity" "$r/$VERSION/Editor/Unity" "$r/$VERSION/Editor/Unity.exe"; do
      [ -x "$exe" ] && { echo "$exe"; return; }
    done
  done
}

UNITY="$(find_unity)"
if [ -z "$UNITY" ]; then
  echo "Unity $VERSION not found. Install it with Unity Hub (plus the $TARGET module) or set UNITY_PATH." >&2
  exit 2
fi

mkdir -p "$PROJECT/Logs/ship"
LOG="$PROJECT/Logs/ship/$NAME.log"
echo "Unity $VERSION -> $METHOD ($TARGET), log: $LOG"
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" -buildTarget "$TARGET" \
  -executeMethod "$METHOD" -logFile "$LOG" "$@"
code=$?

echo "exit=$code"
grep -E "^\[Ship" "$LOG" | sort -u | head -40
grep -E "error CS[0-9]+|Build Failed|BuildFailedException|Scripts have compiler errors" "$LOG" | sort -u | head -20
grep -E "What went wrong" -A6 "$LOG" | head -16
exit $code
