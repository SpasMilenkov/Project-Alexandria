#!/usr/bin/env bash
set -euo pipefail

# Offline cache preparation is the default. Network access requires --download.
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
MODEL_DIR="${1:-${SCRIPT_DIR}/models}"
BACKBONE="${2:-${BACKBONE:-effnet}}"
if [ "$#" -gt 0 ]; then shift; fi
if [ "$#" -gt 0 ]; then shift; fi

exec python3 "${SCRIPT_DIR}/model_assets.py" prepare --model-dir "$MODEL_DIR" --backbone "$BACKBONE" "$@"
