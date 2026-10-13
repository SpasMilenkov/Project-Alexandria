#!/bin/sh
set -eu
umask 077

: "${ALEXANDRIA_GENERATION_ID:?ALEXANDRIA_GENERATION_ID is required}"
: "${CORS_ORIGIN:?CORS_ORIGIN is required}"

case "$ALEXANDRIA_GENERATION_ID" in
  *[!a-zA-Z0-9_-]*) echo "ALEXANDRIA_GENERATION_ID contains unsupported characters" >&2; exit 1 ;;
esac

FORCE_CREDENTIAL_EXPORT=${GARAGE_FORCE_CREDENTIAL_EXPORT:-false}
case "$FORCE_CREDENTIAL_EXPORT" in
  true|false) ;;
  *) echo "GARAGE_FORCE_CREDENTIAL_EXPORT must be true or false" >&2; exit 1 ;;
esac

echo "=== Garage Init ==="
echo ""

# ── Helpers
g() { /garage "$@"; }

# ── Wait
attempt=0
until g status >/dev/null 2>&1; do
  attempt=$((attempt + 1))
  if [ "$attempt" -ge 90 ]; then
    echo "Garage RPC did not become available within 180 seconds" >&2
    exit 1
  fi
  sleep 2
done

mkdir -p ~/.aws
cat > ~/.aws/config << 'EOF'
[default]
s3 =
    addressing_style = path
s3api =
    addressing_style = path
EOF

echo "Garage is up"
echo ""

# ── Layout
echo "Step 1: Layout..."
NODE_ID=$(g node id | head -1 | cut -d'@' -f1 | tr -d '\r\n')
echo "  Node ID: $NODE_ID"

CAPACITY_ARGUMENT=${GARAGE_CAPACITY:-10G}
CAPACITY_CHANGE_REQUESTED=${GARAGE_CAPACITY_CHANGE_REQUESTED:-false}
case "$CAPACITY_CHANGE_REQUESTED" in
  true|false) ;;
  *) echo "GARAGE_CAPACITY_CHANGE_REQUESTED must be true or false" >&2; exit 1 ;;
esac

REQUESTED_BYTES=$(python3 -c '
import re, sys
value = sys.argv[1]
if not re.fullmatch(r"[1-9][0-9]*G", value):
    raise SystemExit("Garage capacity must be a whole number of GB, at least 1 GB")
size = int(value[:-1]) * 1000000000
if size > 9223372036854775807:
    raise SystemExit("Garage capacity exceeds the supported byte range")
print(size)
' "$CAPACITY_ARGUMENT")

read_layout() {
  g json-api GetClusterLayout | python3 -c '
import json, sys
layout = json.load(sys.stdin)
roles = layout["roles"]
version = layout["version"]
if type(version) is not int or version < 0 or not isinstance(roles, list):
    raise SystemExit("Garage returned an unsupported cluster layout")
if layout.get("stagedRoleChanges") or layout.get("stagedParameters") is not None:
    raise SystemExit("Garage has unreviewed staged layout changes; existing layout was preserved")
if not roles:
    print(version, 0, 0)
else:
    if len(roles) != 1 or roles[0].get("id") != sys.argv[1]:
        raise SystemExit("This installer supports one local Garage storage node; existing layout was preserved")
    capacity = roles[0].get("capacity")
    if type(capacity) is not int or capacity <= 0 or capacity > 9223372036854775807:
        raise SystemExit("Garage did not return a valid actual storage capacity")
    print(version, 1, capacity)
' "$NODE_ID"
}

LAYOUT_STATE=$(read_layout)
set -- $LAYOUT_STATE
LAYOUT_VERSION=$1
ROLE_COUNT=$2
ACTUAL_CAPACITY_BYTES=$3

if [ "$ROLE_COUNT" -eq 0 ]; then
  g layout assign -z dc1 -c "$CAPACITY_ARGUMENT" "$NODE_ID"
  NEXT_VERSION=$((LAYOUT_VERSION + 1))
  g layout apply --version "$NEXT_VERSION"
  echo "  Layout applied (version $NEXT_VERSION)"
  LAYOUT_STATE=$(read_layout)
  set -- $LAYOUT_STATE
  ACTUAL_CAPACITY_BYTES=$3
  if [ "$ACTUAL_CAPACITY_BYTES" != "$REQUESTED_BYTES" ]; then
    echo "Garage applied an unexpected capacity; inspect the actual layout before continuing" >&2
    exit 1
  fi
else
  if [ "$CAPACITY_CHANGE_REQUESTED" = true ] && [ "$ACTUAL_CAPACITY_BYTES" != "$REQUESTED_BYTES" ]; then
    echo "Existing Garage layout capacity is $ACTUAL_CAPACITY_BYTES bytes; requested $REQUESTED_BYTES bytes." >&2
    echo "Changing an established storage layout is a separate operation. Existing capacity and data were preserved." >&2
    exit 1
  fi
  echo "  Existing layout capacity preserved: $ACTUAL_CAPACITY_BYTES bytes"
fi

echo "  Actual Garage layout capacity: $ACTUAL_CAPACITY_BYTES bytes"
mkdir -p /output
capacity_receipt=$(mktemp /output/.garage-capacity.XXXXXX)
trap 'rm -f "$capacity_receipt"' EXIT HUP INT TERM
printf '%s\n' "$ACTUAL_CAPACITY_BYTES" > "$capacity_receipt"
chmod 0644 "$capacity_receipt"
chown "$(stat -c '%u:%g' /etc/garage.toml)" "$capacity_receipt"
mv -f "$capacity_receipt" /output/garage-capacity.bytes
trap - EXIT HUP INT TERM

# ── Buckets
echo ""
echo "Step 2: Buckets..."
for bucket in alexandria-files alexandria-previews alexandria-temp alexandria-images alexandria-streaming; do
  if g bucket info "$bucket" >/dev/null 2>&1; then
    echo "  Already exists: $bucket"
  else
    g bucket create "$bucket"
    echo "  Created: $bucket"
  fi
done

# Enable web endpoint for streaming + previews buckets so nginx can proxy to
# the website port without S3 auth. Neither bucket is externally reachable —
# nginx enforces auth_request (stream-check / preview-check) before any
# request reaches Garage. Write access is unaffected: uploads still require
# a valid Garage key (see alexandria-preview-key / alexandria-streaming-key
# below) — website mode only changes unauthenticated GET behavior.
echo ""
echo "Step 2b: Web endpoints..."
g bucket website --allow alexandria-streaming >/dev/null
echo "  Web endpoint enabled: alexandria-streaming"
g bucket website --allow alexandria-previews >/dev/null
echo "  Web endpoint enabled: alexandria-previews"

# ── Keys
echo ""
echo "Step 3: Keys..."

if ! g key info alexandria-master-key >/dev/null 2>&1; then
  g key create alexandria-master-key >/dev/null
  echo "  Created: alexandria-master-key"
else
  echo "  Already exists: alexandria-master-key"
fi

if ! g key info alexandria-preview-key >/dev/null 2>&1; then
  g key create alexandria-preview-key >/dev/null
  echo "  Created: alexandria-preview-key"
else
  echo "  Already exists: alexandria-preview-key"
fi

if ! g key info alexandria-streaming-key >/dev/null 2>&1; then
  g key create alexandria-streaming-key >/dev/null
  echo "  Created: alexandria-streaming-key"
else
  echo "  Already exists: alexandria-streaming-key"
fi

MASTER_KEY_ID=$(g key info alexandria-master-key | grep "Key ID"     | awk '{print $NF}' | tr -d '[:space:]')
MASTER_SECRET=$(g key info --show-secret alexandria-master-key  | grep "Secret key" | awk '{print $NF}' | tr -d '[:space:]')
PREVIEW_KEY_ID=$(g key info alexandria-preview-key | grep "Key ID"     | awk '{print $NF}' | tr -d '[:space:]')
PREVIEW_SECRET=$(g key info --show-secret alexandria-preview-key | grep "Secret key" | awk '{print $NF}' | tr -d '[:space:]')
STREAMING_KEY_ID=$(g key info alexandria-streaming-key | grep "Key ID" | awk '{print $NF}' | tr -d '[:space:]')
STREAMING_SECRET=$(g key info --show-secret alexandria-streaming-key | grep "Secret key" | awk '{print $NF}' | tr -d '[:space:]')

# ── Permissions
echo ""
echo "Step 4: Permissions..."
for bucket in alexandria-files alexandria-previews alexandria-temp alexandria-images alexandria-streaming; do
  g bucket allow --read --write --owner "$bucket" --key alexandria-master-key >/dev/null
  echo "  Master key -> full access -> $bucket"
done

# Preview key still needs read/write via the authenticated S3 API — website
# mode only changes how reads happen for unauthenticated nginx-proxied
# requests, it does not replace the worker's own upload path.
g bucket allow --read --write alexandria-previews --key alexandria-preview-key >/dev/null
g bucket allow --read         alexandria-files    --key alexandria-preview-key >/dev/null
echo "  Preview key -> read/write -> alexandria-previews"
echo "  Preview key -> read       -> alexandria-files"

g bucket allow --read         alexandria-files     --key alexandria-streaming-key >/dev/null
g bucket allow --read --write alexandria-streaming --key alexandria-streaming-key >/dev/null
echo "  Streaming key -> read       -> alexandria-files"
echo "  Streaming key -> read/write -> alexandria-streaming"

# ── CORS
echo ""
echo "Step 5: CORS..."
case "$CORS_ORIGIN" in
  *[!a-zA-Z0-9.:/\[\]-]*) echo "CORS origin contains unsupported characters" >&2; exit 1 ;;
esac

export AWS_ACCESS_KEY_ID="$MASTER_KEY_ID"
export AWS_SECRET_ACCESS_KEY="$MASTER_SECRET"
export AWS_DEFAULT_REGION="garage"


aws configure set default.s3.addressing_style path
sed "s|CORS_ORIGIN_PLACEHOLDER|${CORS_ORIGIN}|g" /config/cors.json > /tmp/cors.json

for bucket in alexandria-files alexandria-previews alexandria-temp alexandria-images alexandria-streaming; do
  aws --endpoint-url http://garage:3900 \
    s3api put-bucket-cors \
    --bucket "$bucket" \
    --cors-configuration file:///tmp/cors.json
done

# ── Credentials
echo ""
echo "Step 7: Storage credential handoff..."
mkdir -p /output

for value in "$MASTER_KEY_ID" "$MASTER_SECRET" "$PREVIEW_KEY_ID" "$PREVIEW_SECRET" "$STREAMING_KEY_ID" "$STREAMING_SECRET"; do
  case "$value" in
    ''|*[!a-zA-Z0-9]*) echo "Garage returned an incomplete or unsupported credential value" >&2; exit 1 ;;
  esac
done

if [ "$FORCE_CREDENTIAL_EXPORT" = false ] && python3 -c '
import os, stat, sys
path = "/output/garage-credentials.persisted"
try:
    with open(path, "rb", opener=lambda name, flags: os.open(name, flags | os.O_NOFOLLOW | os.O_NONBLOCK)) as receipt:
        regular = stat.S_ISREG(os.fstat(receipt.fileno()).st_mode)
        matches = regular and receipt.read(len(sys.argv[1]) + 2) == (sys.argv[1] + "\n").encode("ascii")
except OSError:
    raise SystemExit(1)
raise SystemExit(0 if regular and matches else 1)
' "$ALEXANDRIA_GENERATION_ID"; then
  echo "  Storage keys already persisted for this generation; no credential handoff exported"
  echo ""
  echo "=== Done ==="
  exit 0
fi

handoff=$(mktemp /output/.garage-credentials.XXXXXX)
trap 'rm -f "$handoff"' EXIT HUP INT TERM
cat > "$handoff" <<EOF
ALEXANDRIA_GENERATION_ID=$ALEXANDRIA_GENERATION_ID
# Generated by garage-init on $(date -u +"%Y-%m-%dT%H:%M:%SZ")
# Installer consumes this file after successful initialization

GARAGE_S3_ACCESS_KEY=$MASTER_KEY_ID
GARAGE_S3_SECRET_KEY=$MASTER_SECRET

# Preview key (media worker)
GARAGE_PREVIEW_KEY_ID=$PREVIEW_KEY_ID
GARAGE_PREVIEW_SECRET=$PREVIEW_SECRET

# Streaming key (transpilation worker)
GARAGE_STREAMING_KEY_ID=$STREAMING_KEY_ID
GARAGE_STREAMING_SECRET=$STREAMING_SECRET
EOF

chmod 0600 "$handoff"
chown "$(stat -c '%u:%g' /etc/garage.toml)" "$handoff"
mv -f "$handoff" /output/garage-credentials.env
trap - EXIT HUP INT TERM
echo "  Storage key handoff published for the installer"

echo ""
echo "=== Done ==="
