#!/usr/bin/env bash
# Create the Android upload key for one game, outside the project, with a random password.
# usage: make_upload_keystore.sh <applicationId, e.g. com.example.mygame> ["CN=Studio name"]
# Writes ~/.shipmygame/keys/<applicationId>/{upload.keystore,keystore.env} (mode 600) and refuses to overwrite:
# Google Play links your app to this key, so losing or replacing it means a key-reset request to Google.
# Back the folder up somewhere safe (password manager, encrypted drive). Never commit it.
# The CN ends up in the certificate; keep it to a studio name or the default, not a legal name.
set -euo pipefail
ID="${1:?applicationId}"
DNAME="${2:-CN=Developer}"
DIR="$HOME/.shipmygame/keys/$ID"
KS="$DIR/upload.keystore"
[ -e "$KS" ] && { echo "exists: $KS (not touched)"; exit 1; }

# macOS ships a /usr/bin/keytool stub that fails without a JDK; Unity's Android module bundles one.
KT="${KEYTOOL:-keytool}"
if ! "$KT" -help >/dev/null 2>&1; then
  KT=""
  for k in /Applications/Unity/Hub/Editor/*/PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool \
           "$HOME"/Unity/Hub/Editor/*/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool \
           /Volumes/*/Applications/Unity/Hub/Editor/*/PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool; do
    [ -x "$k" ] && "$k" -help >/dev/null 2>&1 && { KT="$k"; break; }
  done
  [ -n "$KT" ] || { echo "no working keytool: install a JDK or the Unity Android module, or set KEYTOOL=<path>" >&2; exit 2; }
fi

NEW_DIR=0; [ -d "$DIR" ] || NEW_DIR=1
mkdir -p "$DIR"; chmod 700 "$DIR"
# On failure remove only what this run created; the folder may already hold other keys (e.g. play-api.json).
trap '[ -s "$KS" ] || { rm -f "$KS" "$DIR/keystore.env"; [ "$NEW_DIR" = 1 ] && rmdir "$DIR" 2>/dev/null; true; }' EXIT
PASS="$(LC_ALL=C tr -dc 'A-Za-z0-9' </dev/urandom | head -c 32 || true)"
"$KT" -genkeypair -keystore "$KS" -storetype PKCS12 -alias upload -keyalg RSA -keysize 4096 \
  -validity 10000 -storepass "$PASS" -keypass "$PASS" -dname "$DNAME" >/dev/null
cat > "$DIR/keystore.env" <<EOF
KEYSTORE_PATH=$KS
KEYSTORE_PASS=$PASS
KEY_ALIAS=upload
KEY_PASS=$PASS
EOF
chmod 600 "$KS" "$DIR/keystore.env"
echo "created $KS"
"$KT" -list -v -keystore "$KS" -storepass "$PASS" | grep -E "SHA1:|SHA256:" | sed 's/^[[:space:]]*/upload key /'
echo "Back up $DIR now. The SHA-1 above is the upload key's; Play Games Services needs it for one OAuth client."
