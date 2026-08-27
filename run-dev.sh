#!/bin/bash
set -euo pipefail

# Development environment launcher script
# Usage: ./run-dev.sh [--s3-provider <Garage|RustFS>] [--no-analyzers]
# Example: ./run-dev.sh --s3-provider Garage --no-analyzers

SESSION_NAME="dev-session"
S3_PROVIDER="Garage"
NO_ANALYZERS=false

SCRIPT_START=$SECONDS

# Output helpers (honors NO_COLOR)
if [[ -t 1 && -z "${NO_COLOR:-}" ]]; then
  BOLD=$'\033[1m'; DIM=$'\033[2m'; GREEN=$'\033[32m'; RED=$'\033[31m'
  YELLOW=$'\033[33m'; CYAN=$'\033[36m'; MAGENTA=$'\033[35m'; RESET=$'\033[0m'
else
  BOLD=""; DIM=""; GREEN=""; RED=""; YELLOW=""; CYAN=""; MAGENTA=""; RESET=""
fi

rule() { printf "%s%s%s\n" "$DIM" "$(printf '%.0s.' {1..60})" "$RESET"; }
banner() {
  printf "\n%s%s  Alexandria Dev Environment%s\n" "$BOLD" "$MAGENTA" "$RESET"
  rule
}
section() { printf "\n%s%s==>%s %s%s%s\n" "$BOLD" "$CYAN" "$RESET" "$BOLD" "$1" "$RESET"; }
ok()      { printf "  %s✓%s %s\n" "$GREEN" "$RESET" "$1"; }
warn()    { printf "  %s!%s %s\n" "$YELLOW" "$RESET" "$1"; }
die()     { printf "  %s✗%s %s\n" "$RED" "$RESET" "$1" >&2; exit 1; }

while [[ $# -gt 0 ]]; do
  case $1 in
    --s3-provider)
      S3_PROVIDER="$2"
      shift 2
      ;;
    --no-analyzers)
      NO_ANALYZERS=true
      shift
      ;;
    *)
      die "Unknown option: $1 (usage: ./run-dev.sh [--s3-provider <Garage|RustFS>] [--no-analyzers])"
      ;;
  esac
done

case "$S3_PROVIDER" in
  Garage|MinIO|RustFS)
    ;;
  *)
    die "Invalid S3 provider: $S3_PROVIDER (valid values: Garage, MinIO, RustFS)"
    ;;
esac

command -v tmux >/dev/null || die "tmux is required but not installed"

banner

# Pre-build backend once
section "Building backend ${DIM}(S3 provider:${RESET} ${BOLD}${CYAN}$S3_PROVIDER${RESET}${DIM})${RESET}"

BUILD_ARGS=(-m:2 -v quiet --nologo)
if [[ "$NO_ANALYZERS" == true ]]; then
  warn "Skipping analyzers during build"
  BUILD_ARGS+=(-p:RunAnalyzersDuringBuild=false)
fi

BACKEND_PROJECTS=(
  Alexandria.Api
  Alexandria.Workers.Document
  Alexandria.Workers.Media
  Alexandria.Workers.MediaTranspilation
  Alexandria.Workers.MediaMetadata
  Alexandria.Workers.Lyrics
)

total=${#BACKEND_PROJECTS[@]}
index=1
for project in "${BACKEND_PROJECTS[@]}"; do
  printf "  %s[%d/%d]%s %-38s " "$DIM" "$index" "$total" "$RESET" "$project"
  start=$SECONDS
  if ! dotnet build "Backend/$project" "${BUILD_ARGS[@]}"; then
    printf "\n"
    die "Backend build failed on $project, see errors above (no sessions were started)"
  fi
  printf "%s✓%s %ss\n" "$GREEN" "$RESET" "$((SECONDS - start))"
  index=$((index + 1))
done

ok "Backend ready in $((SECONDS - SCRIPT_START))s"

# Launch profile helpers
api_profile="API ($S3_PROVIDER)"
doc_worker_profile="DocumentWorker ($S3_PROVIDER)"
media_worker_profile="MediaWorker ($S3_PROVIDER)"
streaming_worker_profile="Alexandria.Workers.MediaTranspilation ($S3_PROVIDER)"
media_metadata_worker_profile="Alexandria.Workers.MediaMetadata ($S3_PROVIDER)"
lyrics_worker_profile="Alexandria.Workers.Lyrics ($S3_PROVIDER)"

# tmux setup
section "Launching dev environment"

# Creates a window and returns its pane id, which stays valid even if
# tmux renames the window after the command starts.
launch() {
  local name="$1" command="$2" pane
  pane=$(tmux new-window -d -P -F '#{pane_id}' -t "$SESSION_NAME" -n "$name" -c "$PWD")
  tmux send-keys -t "$pane" "$command" C-m
  printf "  %s➜%s %-22s %sstarted%s\n" "$CYAN" "$RESET" "$name" "$DIM" "$RESET"
}

tmux kill-session -t "$SESSION_NAME" 2>/dev/null || true

FRONTEND_PANE=$(tmux new-session -d -P -F '#{pane_id}' -s "$SESSION_NAME" -n "Frontend" -c "$PWD/Frontend")
tmux send-keys -t "$FRONTEND_PANE" "pnpm dev" C-m
printf "  %s➜%s %-22s %sstarted%s\n" "$CYAN" "$RESET" "Frontend" "$DIM" "$RESET"

launch "API" \
  "cd Backend && dotnet run --project Alexandria.Api --no-build --launch-profile \"$api_profile\""
launch "DocumentWorker" \
  "cd Backend && dotnet run --project Alexandria.Workers.Document --no-build --launch-profile \"$doc_worker_profile\""
launch "MediaWorker" \
  "cd Backend && dotnet run --project Alexandria.Workers.Media --no-build --launch-profile \"$media_worker_profile\""
launch "TranspilationWorker" \
  "cd Backend && dotnet run --project Alexandria.Workers.MediaTranspilation --no-build --launch-profile \"$streaming_worker_profile\""
launch "MediaMetadataWorker" \
  "cd Backend && dotnet run --project Alexandria.Workers.MediaMetadata --no-build --launch-profile \"$media_metadata_worker_profile\""
launch "LyricsWorker" \
  "cd Backend && dotnet run --project Alexandria.Workers.Lyrics --no-build --launch-profile \"$lyrics_worker_profile\""

tmux select-window -t "$FRONTEND_PANE"

rule
ok "All services launched in $((SECONDS - SCRIPT_START))s"
printf "  %sAttach:%s tmux attach -t %s   %sDetach:%s Ctrl-b d\n" "$DIM" "$RESET" "$SESSION_NAME" "$DIM" "$RESET"
printf "\n"

tmux attach-session -t "$SESSION_NAME"
