#!/usr/bin/env python3
"""Check the official sources the skills depend on and report what changed since the last snapshot.

usage: watch_sources.py [--only ID ...] [--report watch-report.md]
Reads tools/sources.json, keeps one normalized text snapshot per source in tools/snapshots/<id>.txt and writes a
Markdown report (with diffs and the skill files each source affects) only when something changed. A source seen
for the first time becomes the baseline without counting as a change. Standard library only (runs in CI as is).
Source types: page (HTML → text), github-release, github-tags, github-commits, gitee-tags, gitee-commits, npm.
"""
import argparse
import difflib
import gzip
import hashlib
import html
import json
import os
import re
import sys
import urllib.request
import zlib
from html.parser import HTMLParser

ROOT = os.path.dirname(os.path.abspath(__file__))
SNAP = os.path.join(ROOT, "snapshots")
UA = "Mozilla/5.0 (compatible; ShipMyGame-doc-watch; +https://github.com/BaronCyrus/ShipMyGame)"
# Official pages quote IDs that GitHub secret scanning flags (e.g. the WeChat Unity plugin's AppID in the Unity QA
# page). They carry no rule, so snapshots keep a stable placeholder instead of the value.
REDACT = [
    (re.compile(r"\bwx[0-9a-f]{16}\b"), "wx<appid>"),
    (re.compile(r"AIza[0-9A-Za-z_-]{35}"), "<google-api-key>"),
    (re.compile(r"\b(?:ghp|gho|ghu|ghs|github_pat)_[A-Za-z0-9_]{20,}"), "<github-token>"),
    (re.compile(r"\bAKIA[0-9A-Z]{16}\b"), "<aws-key>"),
    (re.compile(r"\bsk-[A-Za-z0-9_-]{20,}"), "<api-key>"),
]
# support.google.com prints a line holding only a 19-20 digit number that differs on every load; without this every
# Google source would show as changed each week.
VOLATILE = re.compile(r"\d{12,}")
BLOCK = {"p", "br", "li", "tr", "h1", "h2", "h3", "h4", "h5", "h6", "div", "section", "article", "pre", "table", "dt", "dd"}


class Text(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.out, self.skip = [], 0

    def handle_starttag(self, tag, attrs):
        if tag in ("script", "style", "noscript", "svg", "nav", "footer", "header"):
            self.skip += 1
        elif tag in BLOCK:
            self.out.append("\n")

    def handle_endtag(self, tag):
        if tag in ("script", "style", "noscript", "svg", "nav", "footer", "header"):
            self.skip = max(0, self.skip - 1)
        elif tag in BLOCK:
            self.out.append("\n")

    def handle_data(self, data):
        if not self.skip:
            self.out.append(data)


def get(url, headers=None):
    req = urllib.request.Request(url, headers={"User-Agent": UA, "Accept-Language": "zh-CN,zh;q=0.9,en;q=0.8",
                                               "Accept-Encoding": "gzip, deflate", **(headers or {})})
    with urllib.request.urlopen(req, timeout=45) as r:
        raw = r.read()
        charset = r.headers.get_content_charset() or "utf-8"
        encoding = (r.headers.get("Content-Encoding") or "").lower()
    # developers.weixin.qq.com sends gzip even when not asked; urllib does not decompress
    if encoding == "gzip" or raw[:2] == b"\x1f\x8b":
        raw = gzip.decompress(raw)
    elif encoding == "deflate":
        raw = zlib.decompress(raw, -zlib.MAX_WBITS)
    return raw.decode(charset, errors="replace")


def normalize(text, ignore):
    lines = []
    for line in text.splitlines():
        line = re.sub(r"\s+", " ", html.unescape(line)).strip()
        for pattern, placeholder in REDACT:
            line = pattern.sub(placeholder, line)
        if line and not VOLATILE.fullmatch(line) and not any(re.search(p, line) for p in ignore):
            lines.append(line)
    return "\n".join(lines) + "\n"


def fetch(src):
    kind = src.get("type", "page")
    gh = {"Authorization": "Bearer " + os.environ["GITHUB_TOKEN"]} if os.environ.get("GITHUB_TOKEN") else {}
    if kind == "page":
        p = Text()
        p.feed(get(src["url"]))
        return "".join(p.out)
    if kind == "github-release":
        rel = json.loads(get(f"https://api.github.com/repos/{src['repo']}/releases/latest", gh))
        return f"{rel['tag_name']}\n{rel.get('published_at', '')}\n{rel.get('body') or ''}"
    if kind == "github-tags":
        tags = json.loads(get(f"https://api.github.com/repos/{src['repo']}/tags?per_page=10", gh))
        return "\n".join(t["name"] for t in tags)
    if kind == "gitee-tags":
        tags = json.loads(get(f"https://gitee.com/api/v5/repos/{src['repo']}/tags"))
        return "\n".join(sorted((t["name"] for t in tags), reverse=True)[:10])
    if kind == "gitee-commits":
        commits = json.loads(get(f"https://gitee.com/api/v5/repos/{src['repo']}/commits?per_page=5"))
        return "\n".join(f"{c['sha'][:7]} {c['commit']['author']['date'][:10]} {c['commit']['message'].splitlines()[0]}" for c in commits)
    if kind == "github-commits":
        commits = json.loads(get(f"https://api.github.com/repos/{src['repo']}/commits?per_page=5", gh))
        return "\n".join(f"{c['sha'][:7]} {c['commit']['author']['date'][:10]} {c['commit']['message'].splitlines()[0]}" for c in commits)
    if kind == "npm":
        meta = json.loads(get(f"https://registry.npmjs.org/{src['package']}/latest"))
        return f"{meta['version']}\n{meta.get('description', '')}"
    raise ValueError("unknown type " + kind)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", nargs="*")
    ap.add_argument("--report", default="watch-report.md")
    a = ap.parse_args()
    sources = json.load(open(os.path.join(ROOT, "sources.json"), encoding="utf-8"))
    os.makedirs(SNAP, exist_ok=True)
    changed, failed, baseline = [], [], []
    for src in sources:
        if a.only and src["id"] not in a.only:
            continue
        path = os.path.join(SNAP, src["id"] + ".txt")
        try:
            new = normalize(fetch(src), src.get("ignore", []))
        except Exception as e:  # network hiccups must not fail the whole run
            failed.append((src, f"{type(e).__name__}: {e}"))
            continue
        if len(new) < src.get("min_chars", 40 if src.get("type", "page") == "page" else 1):
            failed.append((src, f"only {len(new)} chars of text (page now rendered by JavaScript?)"))
            continue
        old = open(path, encoding="utf-8").read() if os.path.exists(path) else None
        if old == new:
            continue
        with open(path, "w", encoding="utf-8") as f:
            f.write(new)
        if old is None:
            baseline.append(src)
        else:
            diff = list(difflib.unified_diff(old.splitlines(), new.splitlines(), "before", "after", lineterm="", n=1))
            changed.append((src, diff))
        print(("CHANGED " if old else "BASELINE ") + src["id"], hashlib.sha256(new.encode()).hexdigest()[:12])

    print(f"changed={len(changed)} baseline={len(baseline)} failed={len(failed)}")
    if not changed:
        if os.path.exists(a.report):
            os.remove(a.report)
        for src, err in failed:
            print(f"fetch failed {src['id']}: {err}", file=sys.stderr)
        return
    out = ["以下官方来源和上次快照相比有变化。请按 MAINTAINING.md 的「处理巡检 Issue」核对并更新受影响的 skill 文件。", ""]
    for src, diff in changed:
        out += [f"## {src['id']}（{src.get('channel', '')}）", "",
                f"- 来源：{src.get('url') or src.get('repo') or src.get('package')}",
                f"- 决定了：{src.get('note', '')}",
                f"- 受影响：{', '.join('`' + x + '`' for x in src.get('affects', []))}", "",
                "<details><summary>差异（节选）</summary>", "", "```diff"]
        out += diff[:120] + (["… (更多差异见快照文件)"] if len(diff) > 120 else []) + ["```", "", "</details>", ""]
    if failed:
        out += ["## 本次抓取失败（下次重试）", ""] + [f"- {s['id']}: {e}" for s, e in failed]
    with open(a.report, "w", encoding="utf-8") as f:
        f.write("\n".join(out) + "\n")


if __name__ == "__main__":
    main()
