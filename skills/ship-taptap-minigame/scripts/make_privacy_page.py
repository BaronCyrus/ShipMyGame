#!/usr/bin/env python3
"""Turn the in-game privacy policy (ShipCompliance text file) into a static HTML page for the store's 隐私政策链接.

usage: make_privacy_page.py <Assets/Resources/ShipCompliance/zh-Hans.txt> --out <publish/taptap/privacy/index.html>
                            [--key privacy] [--title-key privacy_title] [--allow-placeholders]

The page shows the same text the game shows (the `privacy` key, \\n = line break), so the store link and the
in-game policy never drift apart: TapTap asks for a link whose content matches the policy inside the game.
Lines like "一、…" become headings. It refuses to write a page while {{placeholders}} are left, unless
--allow-placeholders. Host the result on any public HTTPS URL (ask the user before publishing it).
Tested: generated from templates/compliance.zh-Hans.txt on 2026-11-02 (placeholder check, output parsed back as HTML);
not yet tested on a live host. Standard library only.
"""
import argparse
import html
import os
import re
import sys

HEADING = re.compile(r"^[一二三四五六七八九十]+、")


def read_texts(path):
    texts = {}
    with open(path, encoding="utf-8-sig") as f:
        for raw in f:
            line = raw.rstrip("\r\n")
            if line.startswith("#") or "=" not in line:
                continue
            key, value = line.split("=", 1)
            texts[key.strip()] = value.replace("\\n", "\n")
    return texts


def render(title, body):
    lines = [l.strip() for l in body.split("\n")]
    out, first = [], True
    for line in lines:
        if not line:
            continue
        esc = html.escape(line)
        if first:  # the policy's own first line is its title, e.g. "<游戏名> 隐私政策"
            out.append(f"<h1>{esc}</h1>")
            first = False
        elif line.startswith(("更新日期", "生效日期")):
            out.append(f'<p class="muted">{esc}</p>')
        elif HEADING.match(line):
            out.append(f"<h2>{esc}</h2>")
        else:
            out.append(f"<p>{esc}</p>")
    return f"""<!doctype html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{html.escape(title)}</title>
<style>
  :root {{ --bg:#ffffff; --fg:#1d2433; --muted:#5a6475; }}
  @media (prefers-color-scheme: dark) {{ :root {{ --bg:#12151c; --fg:#e6e9ef; --muted:#9aa3b2; }} }}
  body {{ background:var(--bg); color:var(--fg); margin:0;
         font:16px/1.75 -apple-system, BlinkMacSystemFont, "PingFang SC", "Noto Sans SC", "Microsoft YaHei", sans-serif; }}
  main {{ max-width:760px; margin:0 auto; padding:32px 16px 64px; }}
  h1 {{ font-size:26px; margin:0 0 8px; }} h2 {{ font-size:19px; margin:28px 0 8px; }}
  p {{ margin:8px 0; }} .muted {{ color:var(--muted); margin:2px 0; }}
</style>
</head>
<body>
<main>
{chr(10).join(out)}
</main>
</body>
</html>
"""


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("texts")
    ap.add_argument("--out", required=True)
    ap.add_argument("--key", default="privacy")
    ap.add_argument("--title-key", default="privacy_title")
    ap.add_argument("--allow-placeholders", action="store_true")
    a = ap.parse_args()

    texts = read_texts(a.texts)
    if a.key not in texts:
        sys.exit(f"no '{a.key}=' line in {a.texts}")
    body = texts[a.key]
    title = texts.get(a.title_key, "隐私政策")
    left = sorted(set(re.findall(r"\{\{[^}]*\}\}", title + body)))
    if left and not a.allow_placeholders:
        sys.exit("placeholders left, fill them in first: " + ", ".join(left))

    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    with open(a.out, "w", encoding="utf-8") as f:
        f.write(render(title, body))
    print(f"wrote {a.out} ({len(body)} characters of policy text)")


if __name__ == "__main__":
    main()
