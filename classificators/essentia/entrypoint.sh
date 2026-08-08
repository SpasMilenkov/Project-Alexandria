#!/usr/bin/env bash
set -euo pipefail

# Verifies the mounted models directory against models.sha256 before every
# run, since these files now live on a host-mounted volume rather than being
# baked into the image. A rebuild of this image doesn't protect you from the
# host-side models folder being modified, replaced, or corrupted between
# runs, this check does. Set VERIFY_MODELS=false to skip it (e.g. for a
# faster inner dev loop), but leave it on for anything resembling production.

MODEL_DIR="${ESSENTIA_MODEL_DIR:-/app/models}"

if [ "${VERIFY_MODELS:-true}" = "true" ]; then
  if [ ! -f "${MODEL_DIR}/models.sha256" ]; then
    echo "FATAL: ${MODEL_DIR}/models.sha256 not found. Mount a models directory" >&2
    echo "       containing a models.sha256 checksum file, or set VERIFY_MODELS=false" >&2
    echo "       to skip verification (not recommended)." >&2
    exit 1
  fi

  echo "Verifying model checksums in ${MODEL_DIR}..." >&2
  if ! (cd "${MODEL_DIR}" && sha256sum -c models.sha256 --quiet); then
    echo "FATAL: model checksum verification failed. The mounted models" >&2
    echo "       directory does not match models.sha256. Refusing to run." >&2
    exit 1
  fi
  echo "Model checksums OK." >&2
fi

# CMD (default: worker.py) or a docker run override (e.g. classify.py ... for
# a one-shot manual run) gets exec'd here, replacing this shell as PID 1.
exec "$@"
