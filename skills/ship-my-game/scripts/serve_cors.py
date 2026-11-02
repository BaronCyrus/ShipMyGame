#!/usr/bin/env python3
"""Serve local files to a web console page so page JavaScript can put them into an <input type=file>.

Why: browser-automation tools often cap uploads (Claude in Chrome: 10 MB per call) or have no upload tool at all
(built-in browser panes). A page can fetch from 127.0.0.1 instead, once the server answers CORS and Chrome's
Private Network Access preflight. Verified on mp.weixin.qq.com (filing screenshots inside a shadow root) and
developer.taptap.cn (a >10 MB game package).

Safety: binds 127.0.0.1 only, serves only files under ROOT (no listing, no ..), answers only allowed origins,
and exits by itself after --minutes.

usage: serve_cors.py ROOT [--port 8765] [--origin https://mp.weixin.qq.com ...] [--minutes 30]
then in the page: fetch("http://127.0.0.1:8765/<relative path>") — see page_upload.js
"""
import argparse
import mimetypes
import os
import sys
import threading
import urllib.parse
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

DEFAULT_ORIGINS = [
    "https://play.google.com",
    "https://console.cloud.google.com",
    "https://apps.admob.com",
    "https://mp.weixin.qq.com",
    "https://developer.taptap.cn",
    "https://developer.taptap.io",
    "https://partner.steamgames.com",
    "https://appstoreconnect.apple.com",
    "https://developer.open-douyin.com",
]


def make_handler(root, origins):
    class Handler(BaseHTTPRequestHandler):
        def _cors(self):
            origin = self.headers.get("Origin", "")
            if origin in origins:
                self.send_header("Access-Control-Allow-Origin", origin)
                self.send_header("Vary", "Origin")
                self.send_header("Access-Control-Allow-Methods", "GET, OPTIONS")
                self.send_header("Access-Control-Allow-Headers", "*")
                # Chrome asks public pages for this before they may reach a loopback address
                self.send_header("Access-Control-Allow-Private-Network", "true")
            return origin in origins or not origin

        def _path(self):
            rel = urllib.parse.unquote(urllib.parse.urlparse(self.path).path).lstrip("/")
            full = os.path.realpath(os.path.join(root, rel))
            if full != root and not full.startswith(root + os.sep):
                return None
            return full if os.path.isfile(full) else None

        def do_OPTIONS(self):
            self.send_response(204)
            self._cors()
            self.end_headers()

        def do_GET(self):
            full = self._path()
            if full is None:
                self.send_response(404)
                self._cors()
                self.end_headers()
                return
            size = os.path.getsize(full)
            self.send_response(200)
            if not self._cors():
                self.log_message("refused origin %s", self.headers.get("Origin"))
            self.send_header("Content-Type", mimetypes.guess_type(full)[0] or "application/octet-stream")
            self.send_header("Content-Length", str(size))
            self.end_headers()
            with open(full, "rb") as f:
                while chunk := f.read(1 << 20):
                    self.wfile.write(chunk)

        def log_message(self, fmt, *args):
            sys.stderr.write("[serve_cors] " + (fmt % args) + "\n")

    return Handler


def main():
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("root", help="folder (or single file) to serve")
    p.add_argument("--port", type=int, default=8765)
    p.add_argument("--origin", action="append", help="allowed page origin; repeatable (default: known consoles)")
    p.add_argument("--minutes", type=float, default=30, help="stop after this many minutes")
    a = p.parse_args()

    root = os.path.realpath(a.root)
    if os.path.isfile(root):
        root = os.path.dirname(root)
    origins = set(a.origin or DEFAULT_ORIGINS)
    server = ThreadingHTTPServer(("127.0.0.1", a.port), make_handler(root, origins))
    threading.Timer(a.minutes * 60, server.shutdown).start()
    print(f"serving {root} at http://127.0.0.1:{a.port}/ for {a.minutes:g} min; origins: {', '.join(sorted(origins))}", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
