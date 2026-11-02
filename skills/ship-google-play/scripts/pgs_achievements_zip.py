#!/usr/bin/env python3
"""Build the Play Console achievements import ZIP (Play 游戏服务 → 成就 → 导入成就) from a JSON spec.

usage: pgs_achievements_zip.py spec.json icons_dir out.zip

spec.json:
{
  "achievements": [
    {"key": "first_win", "name": "First Win", "description": "Win your first game.",
     "steps": 0, "points": 5, "state": "Revealed"}
  ],
  "localizations": {
    "zh-CN": [{"name": "首胜", "description": "赢下第一局。"}]
  }
}
- key: icon file name without .png in icons_dir (512x512 PNG); also your stable code-side key.
- steps: 0 = unlock-once achievement, N = incremental with N steps.
- points: multiple of 5, 5..200; all achievements together at most 2000 (Play Console limit, 2026-10).
- icons: 512x512 PNG, each file under 1 MB; at most 400 achievements; incremental steps at most 10000.
- localizations: same order and count as "achievements"; locale codes as Play Console lists them.

The format (CSV without header or quoting, so no ASCII comma anywhere) is the one a Play Console
import accepted in practice; names become the GPGSIds constants after the plugin's setup.
"""
import json
import os
import struct
import sys
import zipfile


def png_size(path):
    with open(path, "rb") as f:
        head = f.read(24)
    if head[:8] != b"\x89PNG\r\n\x1a\n":
        raise SystemExit(f"{path} is not a PNG")
    return struct.unpack(">II", head[16:24])


def check(text, what, limit):
    if "," in text:
        raise SystemExit(f"ASCII comma in {what}: {text!r} (use ， or rephrase)")
    if not text.strip() or len(text) > limit:
        raise SystemExit(f"{what} must be 1..{limit} chars: {text!r}")


def main():
    if len(sys.argv) != 4:
        raise SystemExit(__doc__)
    spec_path, icons, out = sys.argv[1:]
    spec = json.load(open(spec_path, encoding="utf-8"))
    ach = spec["achievements"]
    meta, loc, icon_map = [], [], []
    names = [a["name"] for a in ach]
    if len(set(names)) != len(names):
        raise SystemExit("duplicate achievement names")
    if len(ach) > 400:
        raise SystemExit("at most 400 achievements per game")
    for order, a in enumerate(ach, 1):
        check(a["name"], "name", 100)
        check(a["description"], "description", 500)
        steps, pts = int(a.get("steps", 0)), int(a["points"])
        if steps > 10000:
            raise SystemExit(f"incremental steps must be at most 10000: {a['name']}")
        if pts % 5 or not 5 <= pts <= 200:
            raise SystemExit(f"points must be a multiple of 5 in 5..200: {a['name']}")
        icon = os.path.join(icons, a["key"] + ".png")
        if not os.path.isfile(icon):
            raise SystemExit(f"missing icon {icon}")
        if png_size(icon) != (512, 512) or os.path.getsize(icon) >= 1024 * 1024:
            raise SystemExit(f"{icon}: icons must be 512x512 PNG under 1 MB, got {png_size(icon)} and {os.path.getsize(icon)} bytes")
        meta.append(f"{a['name']},{a['description']},{'True' if steps else 'False'},{steps or ''},"
                    f"{a.get('state', 'Revealed')},{pts},{order}")
        icon_map.append(f"{a['name']},{a['key']}.png")
    if sum(int(a["points"]) for a in ach) > 2000:
        raise SystemExit("total points exceed 2000")
    for code, rows in spec.get("localizations", {}).items():
        if len(rows) != len(ach):
            raise SystemExit(f"{code}: {len(rows)} rows, expected {len(ach)}")
        for a, r in zip(ach, rows):
            check(r["name"], f"{code} name", 100)
            check(r["description"], f"{code} description", 500)
            loc.append(f"{a['name']},{r['name']},{r['description']},{code}")
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("AchievementsMetadata.csv", "\n".join(meta) + "\n")
        z.writestr("AchievementsLocalizations.csv", "\n".join(loc) + ("\n" if loc else ""))
        z.writestr("AchievementsIconsMappings.csv", "\n".join(icon_map) + "\n")
        for a in ach:
            z.write(os.path.join(icons, a["key"] + ".png"), a["key"] + ".png")
    print(f"{out}: {len(meta)} achievements, {len(loc)} translations, {sum(int(a['points']) for a in ach)} points")


if __name__ == "__main__":
    main()
