#!/usr/bin/env python3
"""ShipMyGame — reproduce a first launch (no cached data package, slow download) in 微信开发者工具's simulator.

Review opens the game on a fresh device. A Unity 6 build whose engine does not wait for the data package (build.md,
patch B7) is black there, yet fine on every later launch, so the developer's own phone never shows it.

  python3 wx_cold_start.py clear <AppID>                 # delete the simulator's cached data package
  python3 wx_cold_start.py hold <dst>/minigame [secs]     # game.js makes data-package arrive secs late (default 12)
  # devtools: menu 工具 → 编译. Expect the loading page for secs, then the home screen; black means B7 is missing
  python3 wx_cold_start.py log <AppID>                    # ~40 s after launch: the plugin's lines and a verdict
  python3 wx_cold_start.py release <dst>/minigame         # ALWAYS before 预览 or 上传 (re-exporting also restores it)

`log` alone also works on any launch: with B7 in place the plugin logs "no dependencies, invoke run"; without it,
"still waiting runDependencies" 30 s after start. It reads the last launch in the log, so check that it started after
you compiled (a logged-out devtools never starts one). Paths are those of devtools on macOS; pass --root elsewhere.
"""
import argparse
import glob
import os
import re
import sys
import time

ROOT = os.path.expanduser("~/Library/Application Support/微信开发者工具")
ANCHOR = "// 版本检查"  # the SDK's game.js template, just before it loads the UnityPlugin (SDK v0.1.34)
BEGIN = "// [ship-hold] begin: test only, remove with wx_cold_start.py release\n"
END = "// [ship-hold] end\n"
HOLD_JS = """(function () {
    const log = (...a) => { console.log('[ship-hold]', ...a); try { wx.getLogManager({ level: 0 }).log('[ship-hold]', ...a); } catch (e) {} };
    const orig = wx.loadSubpackage;
    const wrapped = function (opts) {
        if (opts && opts.name === 'data-package') {
            log('holding data-package %(secs)ds');
            const ok = opts.success;
            opts = Object.assign({}, opts, { success(res) { setTimeout(() => { log('data-package released'); ok && ok(res); }, %(ms)d); } });
        }
        return orig.call(wx, opts);
    };
    try { wx.loadSubpackage = wrapped; } catch (e) { log('assign failed', e && e.message); }
    log('wrapper installed:', wx.loadSubpackage === wrapped);
})();
"""
KEY_LINES = re.compile(r"game starting|\[ship-hold\]|no dependencies|still waiting|PLUGIN ERROR|no boot config|游戏可交互|callMain耗时")


def game_js(minigame):
    path = os.path.join(minigame, "game.js")
    if not os.path.isfile(path):
        sys.exit(f"no game.js in {minigame} (pass the exported <dst>/minigame folder)")
    with open(path, encoding="utf-8", newline="") as f:
        return path, f.read()


def write(path, text):
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)


def hold(a):
    path, s = game_js(a.minigame)
    if BEGIN in s:
        sys.exit("game.js is already held; run release first")
    if s.count(ANCHOR) != 1:
        sys.exit(f"expected one '{ANCHOR}' in game.js (the SDK template changed?); found {s.count(ANCHOR)}")
    block = BEGIN + HOLD_JS % {"secs": a.secs, "ms": a.secs * 1000} + END
    write(path, s.replace(ANCHOR, block + ANCHOR, 1))
    print(f"held: data-package arrives {a.secs}s late. Run release before 预览 or 上传.")


def release(a):
    path, s = game_js(a.minigame)
    i = s.find(BEGIN)
    if i < 0:
        print("nothing to release: game.js is clean")
        return
    j = s.find(END, i)
    if j < 0:
        sys.exit("found the begin marker but not the end marker; re-export to get a clean game.js")
    s = s[:i] + s[j + len(END):]
    if "[ship-hold]" in s:
        sys.exit("game.js still mentions [ship-hold]; re-export to get a clean game.js")
    write(path, s)
    print("released: game.js is back to the exported file")


def app_dirs(root, appid):
    # devtools 2.02.2608 kept a folder per openid above the AppID; 2.02.2609 puts the AppID right under WeappFileSystem
    base = os.path.join(glob.escape(root), "*", "WeappSimulator", "WeappFileSystem")
    return glob.glob(os.path.join(base, "*", appid)) + glob.glob(os.path.join(base, appid))


def clear(a):
    dirs = app_dirs(a.root, a.appid)
    if not dirs:
        sys.exit(f"no simulator data for {a.appid} under {a.root}: run the game in the simulator once, or pass --root")
    files = [f for d in dirs for f in glob.glob(os.path.join(glob.escape(d), "usr", "__GAME_FILE_CACHE", "*.webgl.data.unityweb.bin"))]
    for f in files:
        if not a.dry_run:
            os.remove(f)
        print(("would delete " if a.dry_run else "deleted ") + os.path.basename(f))
    if not files:
        print("no cached data package: the next launch is already a first launch")


def log(a):
    logs = [f for d in app_dirs(a.root, a.appid) for f in glob.glob(os.path.join(glob.escape(d), "usr", "miniprogramLog", "log*"))]
    if not logs:
        sys.exit(f"no simulator log for {a.appid} under {a.root}")
    newest = max(logs, key=os.path.getmtime)
    with open(newest, "rb") as f:
        text = f.read().replace(b"\0", b"").decode("utf-8", "replace")
    lines = text.splitlines()
    starts = [i for i, line in enumerate(lines) if "game starting" in line]
    launch = lines[starts[-1]:] if starts else lines
    # a launch that never started (devtools logged out, compile not run) leaves the previous one here
    print("log file last written " + time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(os.path.getmtime(newest))))
    keys = [line for line in launch if KEY_LINES.search(line)]
    for line in keys[:60]:
        print(line[:200])
    body = "\n".join(launch)
    if "no dependencies" in body:
        print("\nOK: the engine waited for the data package (B7 in place)")
    elif "still waiting runDependencies" in body:
        print("\nBROKEN: the engine did not wait for the data package (B7 missing) or a dependency never finished")
        sys.exit(1)
    else:
        print("\nNOT YET: no verdict in this launch; wait about 40 s after it starts and run log again")
        sys.exit(2)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("hold", help="make data-package arrive late")
    p.add_argument("minigame")
    p.add_argument("secs", nargs="?", type=int, default=12)
    p.set_defaults(func=hold)
    p = sub.add_parser("release", help="undo hold")
    p.add_argument("minigame")
    p.set_defaults(func=release)
    for name, func, text in (("clear", clear, "delete the cached data package"), ("log", log, "show the last launch")):
        p = sub.add_parser(name, help=text)
        p.add_argument("appid")
        p.add_argument("--root", default=ROOT, help="devtools data folder (default: the macOS one)")
        if name == "clear":
            p.add_argument("--dry-run", action="store_true")
        p.set_defaults(func=func)
    a = ap.parse_args()
    a.func(a)


if __name__ == "__main__":
    main()
