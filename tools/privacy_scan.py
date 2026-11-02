#!/usr/bin/env python3
"""Scan files for personal data and account identifiers before they go public.

usage: privacy_scan.py [paths...]          (default: every git-tracked file, or the whole folder outside git)
       privacy_scan.py --deny ~/.shipmygame/privacy-denylist.txt [paths...]
The denylist (one term per line, never committed) holds your own names, emails, game names and IDs; the built-in
patterns catch common identifier shapes. Exit code 1 when anything is found. Placeholders like <AppID> pass.
"""
import argparse
import os
import re
import subprocess
import sys

PATTERNS = {
    "email": r"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}",
    "cn-mobile": r"(?<!\d)1[3-9]\d{9}(?!\d)",
    "cn-id-number": r"(?<!\d)\d{17}[\dXx](?!\d)",
    "wechat-appid": r"\bwx[0-9a-f]{16}\b",
    "google-oauth-client": r"\d{6,}-[a-z0-9]{20,}\.apps\.googleusercontent\.com",
    "cert-fingerprint": r"\b(?:[0-9A-Fa-f]{2}:){15,}[0-9A-Fa-f]{2}\b",
    "long-numeric-id": r"(?<![\d.])\d{10,20}(?![\d.])",
    "home-path": r"/(?:Users|home)/[A-Za-z0-9._-]+/",
    "mounted-volume": r"/Volumes/[A-Za-z0-9._-]+/",
    "windows-user-path": r"[A-Za-z]:\\\\Users\\\\[^\\\\\s]+",
    "token-like": r"\b(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|sk-[A-Za-z0-9_-]{20,}|AKIA[0-9A-Z]{16})\b",
}
ALLOW = [
    r"noreply@anthropic\.com", r"[\w.+-]+@users\.noreply\.github\.com", r"@example\.(com|org)",
    r"git@github\.com", r"41898282\+github-actions\[bot\]",
    r"^3940256099942544$",  # Google's public AdMob test publisher ID (ca-app-pub-3940256099942544)
]
# Lines that quote Google's public AdMob test IDs (ca-app-pub-3940256099942544/<unit>) are not account data.
LINE_ALLOW = [r"ca-app-pub-3940256099942544", r"3940256099942544"]
SECRET_SHAPED = {"wechat-appid", "google-oauth-client", "cert-fingerprint", "token-like"}
SKIP_EXT = {".png", ".jpg", ".jpeg", ".gif", ".webp", ".zip", ".ttf", ".otf", ".mp4", ".pdf", ".docx", ".keystore"}


def files(paths):
    if paths:
        for p in paths:
            if os.path.isdir(p):
                for d, dn, fn in os.walk(p):
                    dn[:] = [x for x in dn if x not in {".git", ".scratch", "node_modules", "__pycache__"}]
                    yield from (os.path.join(d, f) for f in fn)
            else:
                yield p
        return
    try:
        out = subprocess.run(["git", "ls-files", "-co", "--exclude-standard"], capture_output=True, text=True, check=True).stdout
        yield from (f for f in out.splitlines() if f)
    except (subprocess.CalledProcessError, FileNotFoundError):
        yield from files(["."])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("paths", nargs="*")
    ap.add_argument("--deny", action="append", default=[])
    a = ap.parse_args()
    deny = []
    for path in a.deny:
        with open(os.path.expanduser(path), encoding="utf-8") as f:
            deny += [t.strip() for t in f if t.strip() and not t.startswith("#")]
    hits = 0
    for path in files(a.paths):
        if os.path.splitext(path)[1].lower() in SKIP_EXT or not os.path.isfile(path):
            continue
        # copies of official pages: only secret-shaped values matter there (numbers and addresses are the pages' own)
        snapshot = os.path.normpath(path).startswith(os.path.join("tools", "snapshots"))
        text = open(path, encoding="utf-8", errors="ignore").read()
        for n, line in enumerate(text.splitlines(), 1):
            public_test_ids = any(re.search(la, line) for la in LINE_ALLOW)
            for name, pat in PATTERNS.items():
                if name == "long-numeric-id" and public_test_ids:
                    continue
                if snapshot and name not in SECRET_SHAPED:
                    continue
                for m in re.finditer(pat, line):
                    if any(re.search(al, m.group(0)) for al in ALLOW):
                        continue
                    hits += 1
                    print(f"{path}:{n}: {name}: {m.group(0)}")
            low = line.lower()
            for term in deny:
                if term.lower() in low:
                    hits += 1
                    print(f"{path}:{n}: denylist: {term}")
    print(f"{hits} finding(s)", file=sys.stderr)
    sys.exit(1 if hits else 0)


if __name__ == "__main__":
    main()
