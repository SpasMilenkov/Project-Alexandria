#!/usr/bin/env bash
set -euo pipefail

# Downloads every model/metadata file classify.py needs, then verifies them
# all against models.sha256 (generated locally via: sha256sum *.pb *.json > models.sha256).
# Fails the build immediately if any file doesn't match, before anything
# downloaded gets baked into the image.

MODEL_DIR="${1:-/app/models}"
mkdir -p "$MODEL_DIR"
cd "$MODEL_DIR"

BASE="https://essentia.upf.edu/models"

FILES=(
  "feature-extractors/discogs-effnet/discogs-effnet-bs64-1.pb"
  "feature-extractors/discogs-effnet/discogs-effnet-bs64-1.json"
  "classification-heads/genre_discogs400/genre_discogs400-discogs-effnet-1.pb"
  "classification-heads/genre_discogs400/genre_discogs400-discogs-effnet-1.json"
  "feature-extractors/maest/discogs-maest-30s-pw-519l-2.pb"
  "feature-extractors/maest/discogs-maest-30s-pw-519l-2.json"
  "classification-heads/mood_aggressive/mood_aggressive-discogs-effnet-1.pb"
  "classification-heads/mood_aggressive/mood_aggressive-discogs-effnet-1.json"
  "classification-heads/mood_happy/mood_happy-discogs-effnet-1.pb"
  "classification-heads/mood_happy/mood_happy-discogs-effnet-1.json"
  "classification-heads/mood_party/mood_party-discogs-effnet-1.pb"
  "classification-heads/mood_party/mood_party-discogs-effnet-1.json"
  "classification-heads/mood_relaxed/mood_relaxed-discogs-effnet-1.pb"
  "classification-heads/mood_relaxed/mood_relaxed-discogs-effnet-1.json"
  "classification-heads/mood_sad/mood_sad-discogs-effnet-1.pb"
  "classification-heads/mood_sad/mood_sad-discogs-effnet-1.json"
  "classification-heads/mood_acoustic/mood_acoustic-discogs-effnet-1.pb"
  "classification-heads/mood_acoustic/mood_acoustic-discogs-effnet-1.json"
  "classification-heads/voice_instrumental/voice_instrumental-discogs-effnet-1.pb"
  "classification-heads/voice_instrumental/voice_instrumental-discogs-effnet-1.json"
)

for path in "${FILES[@]}"; do
  filename=$(basename "$path")
  echo "Downloading ${filename}..."
  wget -q "${BASE}/${path}" -O "${filename}"
done

echo "Verifying checksums against models.sha256..."
sha256sum -c models.sha256
echo "All model files verified."
