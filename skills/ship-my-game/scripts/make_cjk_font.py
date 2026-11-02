#!/usr/bin/env python3
"""Chinese fallback font subset for builds without OS font fallback (WebGL: WeChat / TapTap mini-games).

usage: make_cjk_font.py --font NotoSansSC[wght].ttf --out Assets/Fonts/CJKSubset.ttf \
           [--family "CJK Subset"] [--weight 700] [--scan Assets] [--scan publish] [--text "额外的字"] \
           [--license OFL.txt] [--ascii] [--all]

Collects every non-ASCII character used by the game, keeps only those glyphs, and renames the font family
so Unity can reference it by name (uGUI Text: the Latin font's "Font Names" + fallback list; TextMeshPro:
make a dynamic TMP_FontAsset from it and add it to the fallback list). The result is typically 300-900 KB.
- --scan: files or folders to read text from (.cs .txt .json .csv .xml .asset .prefab .unity .md .yaml); Unity
  YAML stores non-ASCII strings as \\uXXXX escapes, which are decoded. Default: Assets. Point it at the Chinese
  string table when the game has many languages; only CJK characters and full-width punctuation are kept
  (accented Latin comes from the Latin font), unless --all.
- Text that only exists at runtime (dates from CultureInfo, numbers spelled in Chinese, names typed by the
  player) is not in any file: pass it with --text. Example: month names "一二三四五六七八九十月年".
- --ascii also keeps printable ASCII (use when this font is the only font for some text).
Needs: pip install fonttools. Get Noto Sans SC (SIL OFL) from Google Fonts; ship its OFL.txt with the game.
"""
import argparse
import os
import re
import shutil
import sys

from fontTools import subset
from fontTools.ttLib import TTFont

TEXT_EXT = {".cs", ".txt", ".json", ".csv", ".xml", ".asset", ".prefab", ".unity", ".md", ".yaml", ".yml", ".uxml", ".uss"}
PUNCT = "，。！？：；、“”‘’（）《》【】…—·～％＋－×÷"
ESCAPE = re.compile(r"\\u([0-9a-fA-F]{4})")


def is_cjk(c):
    o = ord(c)
    return (0x2E80 <= o <= 0x9FFF or 0xF900 <= o <= 0xFAFF or 0xFF00 <= o <= 0xFFEF
            or 0x2000 <= o <= 0x206F or c in "·×÷")


def files_under(paths):
    for root in paths:
        if os.path.isfile(root):
            yield root
            continue
        for dirpath, dirnames, files in os.walk(root):
            dirnames[:] = [d for d in dirnames if d not in {"Library", "Temp", "Logs", "Builds", "node_modules", ".git"}]
            for f in files:
                if os.path.splitext(f)[1].lower() in TEXT_EXT:
                    yield os.path.join(dirpath, f)


def scan(paths, keep_all):
    chars = set()
    for path in files_under(paths):
        try:
            text = open(path, encoding="utf-8", errors="ignore").read()
        except OSError:
            continue
        text += "".join(chr(int(h, 16)) for h in ESCAPE.findall(text))
        chars.update(c for c in text if ord(c) > 0x7F and c.isprintable() and (keep_all or is_cjk(c)))
    return chars


def main():
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("--font", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--family", default="CJK Subset")
    p.add_argument("--weight", type=float, default=700, help="for variable fonts")
    p.add_argument("--scan", action="append")
    p.add_argument("--text", default="")
    p.add_argument("--license")
    p.add_argument("--ascii", action="store_true")
    p.add_argument("--all", action="store_true", help="keep every non-ASCII character found, not only CJK")
    a = p.parse_args()

    chars = scan(a.scan or ["Assets"], a.all) | set(PUNCT) | set(a.text)
    if a.ascii:
        chars |= {chr(c) for c in range(0x20, 0x7F)}
    font = TTFont(a.font)
    if "fvar" in font:
        from fontTools.varLib import instancer
        font = instancer.instantiateVariableFont(font, {"wght": a.weight})
    have = font.getBestCmap()
    opts = subset.Options()
    opts.layout_features = ["*"]
    opts.name_IDs = ["*"]
    opts.hinting = False
    sub = subset.Subsetter(opts)
    sub.populate(unicodes=sorted(ord(c) for c in chars if ord(c) in have))
    sub.subset(font)

    names = font["name"]
    for name_id in (16, 17):
        names.removeNames(nameID=name_id)
    for name_id, value in ((1, a.family), (2, "Regular"), (4, a.family), (6, a.family.replace(" ", "") + "-Regular")):
        names.setName(value, name_id, 3, 1, 0x409)
        names.setName(value, name_id, 1, 0, 0)
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    font.save(a.out)
    if a.license:
        shutil.copyfile(a.license, os.path.splitext(a.out)[0] + "-OFL.txt")

    missing = "".join(sorted(c for c in chars if ord(c) not in have and not c.isspace()))
    print(f"{a.out}: {len(chars)} chars, {os.path.getsize(a.out) // 1024} KB, family '{a.family}'")
    if missing:
        print(f"not in the source font (will show as boxes): {missing}", file=sys.stderr)


if __name__ == "__main__":
    main()
