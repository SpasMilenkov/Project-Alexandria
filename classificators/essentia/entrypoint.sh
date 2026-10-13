#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# The image owns the pinned checksums; a modified bind mount cannot redefine trust.
python3 "${SCRIPT_DIR}/model_assets.py" runtime-verify "$@"

exec "$@"
