#!/usr/bin/env bash
# Sets up a throwaway test site for the Builder: a source snapshot that looks like a fresh
# clone plus uncommitted work, and an empty settings folder to install into.
#
#   <site>/source    tracked and untracked-but-not-ignored files of this checkout
#   <site>/settings  the installation settings folder to give the wizard
#
# Running it again refreshes <site>/source in place and leaves <site>/settings alone, so
# repair and rerun paths can be tested against newer code. Only files an earlier run copied
# are ever removed, and a source folder this script did not create is never touched.
#
# Usage: scripts/builder-sandbox.sh [--path <dir>] [--models] [--run] [--dry-run]
#   --path <dir>  site location (default: ~/builder-test)
#   --models      also copy the local Essentia model files, so setup can run offline
#   --run         launch the Builder from the snapshot once it is ready
#   --dry-run     print what would happen without touching anything

set -euo pipefail

die() {
  echo "error: $*" >&2
  exit 1
}

usage() {
  sed -n '2,16p' "$0" | sed 's/^# \{0,1\}//'
  exit "${1:-0}"
}

SITE="$HOME/builder-test"
MODELS=0
RUN=0
DRY_RUN=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --path) SITE="${2:?--path needs a directory}"; shift 2 ;;
    --models) MODELS=1; shift ;;
    --run) RUN=1; shift ;;
    --dry-run) DRY_RUN=1; shift ;;
    -h|--help) usage 0 ;;
    *) die "unknown argument $1" ;;
  esac
done

command -v rsync >/dev/null || die "rsync is required"

# Resolved from the script's own location, so the copy in a worktree snapshots that worktree.
REPO="$(git -C "$(dirname "${BASH_SOURCE[0]}")" rev-parse --show-toplevel)"
SITE="$(realpath -m -- "$SITE")"
SOURCE="$SITE/source"
SETTINGS="$SITE/settings"
MARKER="$SITE/.builder-sandbox-files"

# A site inside the checkout would be picked up by the next snapshot of itself.
case "$SITE/" in
  "$REPO"/*) die "$SITE is inside the repository; choose a folder outside it" ;;
esac

if [[ -e "$SOURCE" && ! -f "$MARKER" ]]; then
  die "$SOURCE exists but was not created by this script; refusing to touch it"
fi

WORK="$(mktemp -d)"
trap 'rm -rf -- "$WORK"' EXIT

# Database dumps are untracked on purpose and have no place in a source snapshot.
list_source_files() {
  local rel

  git -C "$REPO" ls-files -z --cached --others --exclude-standard |
    while IFS= read -r -d '' rel; do
      [[ "$rel" == backups/* ]] && continue
      [[ -e "$REPO/$rel" || -L "$REPO/$rel" ]] || continue
      printf '%s\0' "$rel"
    done
}

list_model_files() {
  local models_dir="classificators/essentia/models"

  [[ -d "$REPO/$models_dir" ]] || die "no model files found in $REPO/$models_dir"

  (cd "$REPO" && find -L "$models_dir" -type f -print0)
}

{
  list_source_files

  if [[ $MODELS -eq 1 ]]; then
    list_model_files
  fi
} | LC_ALL=C sort -z -u > "$WORK/current"

: > "$WORK/stale"

if [[ -f "$MARKER" ]]; then
  # Model files stay once copied, so a refresh without --models does not undo an earlier one.
  LC_ALL=C comm -z -23 "$MARKER" "$WORK/current" |
    while IFS= read -r -d '' rel; do
      [[ "$rel" == classificators/essentia/models/* ]] && continue
      printf '%s\0' "$rel"
    done > "$WORK/stale"
fi

COPY_COUNT="$(tr -cd '\0' < "$WORK/current" | wc -c)"
STALE_COUNT="$(tr -cd '\0' < "$WORK/stale" | wc -c)"

echo "Repository: $REPO"
echo "Test site:  $SITE"
echo "Files to sync: $COPY_COUNT, stale files to remove: $STALE_COUNT"

if [[ $DRY_RUN -eq 1 ]]; then
  echo "Dry run; nothing was changed."
  exit 0
fi

mkdir -p "$SOURCE" "$SETTINGS"

# Written before the first copy so an interrupted run can still be resumed. Listing old and
# new files together keeps every copied file removable if this run stops halfway.
if [[ -f "$MARKER" ]]; then
  LC_ALL=C sort -z -u "$MARKER" "$WORK/current" > "$WORK/union"
else
  cp "$WORK/current" "$WORK/union"
fi

cp "$WORK/union" "$MARKER"

while IFS= read -r -d '' rel; do
  rm -f -- "$SOURCE/$rel"
done < "$WORK/stale"

# Updated in place because running containers bind-mount files from this folder.
# --copy-links turns the model symlinks of a worktree into real files.
rsync -a --copy-links --from0 --files-from="$WORK/current" "$REPO/" "$SOURCE/"

cp "$WORK/current" "$MARKER"

LAUNCH=(dotnet run --project "$SOURCE/Builder/Builder.csproj" -- --settings "$SETTINGS")

echo
echo "Ready. Free space on this filesystem: $(df -h --output=avail "$SITE" | tail -n 1 | tr -d ' ')"
echo
echo "Launch the Builder:"
printf ' '
printf ' %q' "${LAUNCH[@]}"
echo
echo
echo "In the wizard, confirm the source is $SOURCE"
echo "and the settings folder is $SETTINGS."
echo "Pick a free HTTPS port such as 8443 if another install already uses 443."
echo
echo "To tear an install down, use the project name and commands in"
echo "$SETTINGS/INSTALLATION.txt with: down -v --rmi local"

if [[ $RUN -eq 1 ]]; then
  command -v dotnet >/dev/null || die "dotnet is required to run the Builder"

  rm -rf -- "$WORK"
  cd "$SITE"
  exec "${LAUNCH[@]}"
fi
