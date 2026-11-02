#!/usr/bin/env python3
"""Upload an AAB to a Google Play track through the Play Developer API (no browser, no 10 MB limit).

Not yet verified end to end on a real account: written from the official API reference (androidpublisher v3).
Google requires the FIRST bundle of a new app to be uploaded in the web console; use this from the second upload
on. The browser route (the user drags the AAB into 「创建新版本」) is the verified fallback.

usage:
  play_upload.py --key KEY.json --package <applicationId> --aab Builds/x.aab --track alpha \
      [--notes en-US=notes/en-US.txt --notes zh-CN=notes/zh-CN.txt] [--name "2 (1.0.1)"] \
      [--status draft|completed] [--send-for-review] [--dry-run]
  play_upload.py --key KEY.json --package <applicationId> --list-tracks

- KEY.json: service-account key, kept outside the project (~/.shipmygame/keys/<applicationId>/play-api.json),
  for an account invited in Play Console 「用户和权限」 with release permissions for this app.
- --track: internal | alpha (the default closed-testing track) | beta | production | a custom track's name;
  --list-tracks prints the names this app really has.
- --status draft (default) creates a draft release you can inspect and send from the console; apps that have never
  been published accept only drafts. completed rolls the release out once it passes review.
- Without --send-for-review the edit is committed with changesNotSentForReview=true: nothing goes to review until
  someone sends it from 「发布概览」. Pass it only after the user said 「提交」 (提审闸门).
- The commit always sets changesInReviewBehavior=ERROR_IF_IN_REVIEW: by default a commit would cancel changes that
  are already in review; this makes it fail instead, so wait for that review to finish.
- Some apps refuse changesNotSentForReview ("...must not be set": every commit goes to review) and some require it
  ("...cannot be sent for review automatically"). The script stops on either message and explains; it never
  retries on its own, because a retry could send changes for review without the user's 「提交」.
Needs: pip install google-api-python-client google-auth
"""
import argparse
import os
import sys

UPLOAD_TIMEOUT = 300  # seconds; Google asks for at least 2 minutes on bundle uploads


def parse_notes(items):
    notes = []
    for item in items or []:
        lang, _, path = item.partition("=")
        if not lang or not path:
            raise SystemExit(f"--notes expects LANG=FILE, got {item!r}")
        text = open(os.path.expanduser(path), encoding="utf-8").read().strip()
        if len(text) > 500:
            raise SystemExit(f"release notes for {lang} have {len(text)} chars; Play allows 500")
        notes.append({"language": lang, "text": text})
    return notes


def main():
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("--key", required=True)
    p.add_argument("--package", required=True)
    p.add_argument("--aab")
    p.add_argument("--list-tracks", action="store_true")
    p.add_argument("--track", default="internal")
    p.add_argument("--notes", action="append")
    p.add_argument("--name")
    p.add_argument("--status", choices=["draft", "completed"], default="draft")
    p.add_argument("--send-for-review", action="store_true")
    p.add_argument("--dry-run", action="store_true")
    a = p.parse_args()

    key = os.path.expanduser(a.key)
    if not os.path.isfile(key):
        raise SystemExit(f"not found: {key}")
    if a.list_tracks:
        edits = connect(key)
        edit_id = edits.insert(packageName=a.package, body={}).execute()["id"]
        try:
            for t in edits.tracks().list(packageName=a.package, editId=edit_id).execute().get("tracks", []):
                print(t["track"], [(r.get("name"), r.get("status"), r.get("versionCodes")) for r in t.get("releases", [])])
        finally:
            edits.delete(packageName=a.package, editId=edit_id).execute()
        return
    if not a.aab:
        raise SystemExit("--aab is required unless --list-tracks")
    aab = os.path.expanduser(a.aab)
    if not os.path.isfile(aab):
        raise SystemExit(f"not found: {aab}")
    notes = parse_notes(a.notes)
    print(f"package={a.package} track={a.track} status={a.status} aab={os.path.basename(aab)} "
          f"({os.path.getsize(aab) // (1024 * 1024)} MB) notes={[n['language'] for n in notes]} "
          f"send_for_review={a.send_for_review}")
    if a.dry_run:
        return

    from googleapiclient.errors import HttpError
    from googleapiclient.http import MediaFileUpload

    edits = connect(key)
    edit_id = edits.insert(packageName=a.package, body={}).execute()["id"]
    try:
        bundle = edits.bundles().upload(
            packageName=a.package, editId=edit_id,
            media_body=MediaFileUpload(aab, mimetype="application/octet-stream", resumable=True)).execute()
        code = str(bundle["versionCode"])
        print(f"uploaded versionCode {code}")
        release = {"versionCodes": [code], "status": a.status}
        if a.name:
            release["name"] = a.name
        if notes:
            release["releaseNotes"] = notes
        edits.tracks().update(packageName=a.package, editId=edit_id, track=a.track,
                              body={"track": a.track, "releases": [release]}).execute()
        commit = {"changesInReviewBehavior": "ERROR_IF_IN_REVIEW"}
        if not a.send_for_review:
            commit["changesNotSentForReview"] = True
        try:
            result = edits.commit(packageName=a.package, editId=edit_id, **commit).execute()
        except HttpError as e:
            text = str(e)
            if "must not be set" in text:
                raise SystemExit("This app sends every committed change for review. Nothing was committed. Get the "
                                 "user's 「提交」 first (提审闸门), then rerun with --send-for-review.") from e
            if "cannot be sent for review automatically" in text:
                raise SystemExit("This app cannot send changes for review through the API. Rerun without "
                                 "--send-for-review, then send them from 发布概览 after the user says 「提交」.") from e
            raise
        print(f"committed edit {result.get('id')} to track {a.track}"
              + ("" if a.send_for_review else "; send it for review from 发布概览 when the user says so"))
    except BaseException:
        try:
            edits.delete(packageName=a.package, editId=edit_id).execute()
        except Exception as cleanup:  # keep the original error visible
            print(f"(could not delete edit {edit_id}: {cleanup})", file=sys.stderr)
        raise


def connect(key):
    import google_auth_httplib2
    import httplib2
    from google.oauth2 import service_account
    from googleapiclient.discovery import build

    creds = service_account.Credentials.from_service_account_file(
        key, scopes=["https://www.googleapis.com/auth/androidpublisher"])
    http = google_auth_httplib2.AuthorizedHttp(creds, http=httplib2.Http(timeout=UPLOAD_TIMEOUT))
    return build("androidpublisher", "v3", http=http, cache_discovery=False).edits()


if __name__ == "__main__":
    sys.exit(main())
