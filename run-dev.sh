#!/bin/bash
set -euo pipefail

# Development environment launcher script
# Usage: ./run-dev.sh [--s3-provider <Garage|RustFS>] [--no-analyzers]
# Example: ./run-dev.sh --s3-provider Garage --no-analyzers
#
# Brings up the docker compose dev stack, builds the backend, then launches
# every service in a tmux session, including a "Docker" window with one log
# tile per compose service.

SESSION_NAME="dev-session"
S3_PROVIDER="Garage"
NO_ANALYZERS=false

# Docker compose environment
COMPOSE_FILE="docker-compose.dev.yml"
COMPOSE_PROJECT="alexandria-dev"
COMPOSE=(docker compose -f "$COMPOSE_FILE" -p "$COMPOSE_PROJECT")

# Services that get their own log tile in the "Docker" window.
# rabbitmq-config is a one-shot init container; remove it from the list if
# you don't want a (static) tile for it.
COMPOSE_LOG_SERVICES=()
while IFS= read -r s; do COMPOSE_LOG_SERVICES+=("$s"); done < <("${COMPOSE[@]}" config --services)
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
command -v docker >/dev/null || die "docker is required but not installed"
docker compose version >/dev/null 2>&1 || die "docker compose (v2) is required but not available"
[[ -f "$COMPOSE_FILE" ]] || die "$COMPOSE_FILE not found (run this script from the repo root)"
# Fail fast on missing required env vars (API_RABBITMQ_USER, etc.) before building
"${COMPOSE[@]}" config >/dev/null || die "$COMPOSE_FILE failed to validate (missing required env vars?)"

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
  Alexandria.Workers.Playlist
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

# ---------------------------------------------------------------------------
# Docker compose environment
# ---------------------------------------------------------------------------
# Runs here (not inside a pane) so that (1) every container already exists
# when the log tiles attach, and (2) the .NET services below only start once
# postgres/garage/rabbitmq are healthy -- nothing in the compose file gates
# the API on postgres readiness. On warm runs --wait returns almost instantly.
section "Starting docker compose ${DIM}(project:${RESET} ${BOLD}${CYAN}$COMPOSE_PROJECT${RESET}${DIM})${RESET}"

if ! "${COMPOSE[@]}" up -d; then
  die "docker compose failed to start (see output above; no sessions were started)"
fi

wait_for_healthy() {
  local container=$1 waited=0
  until [[ "$(docker inspect -f '{{.State.Health.Status}}' "$container" 2>/dev/null || echo starting)" == "healthy" ]]; do
    sleep 2
    waited=$((waited + 2))
    if (( waited > 180 )); then
      die "$container did not become healthy within 180s"
    fi
  done
}

for container in alexandria-postgres-dev alexandria-garage-dev alexandria-rabbitmq-dev; do
  wait_for_healthy "$container"
done
ok "Docker environment ready"

# Launch profile helpers
api_profile="API ($S3_PROVIDER)"
doc_worker_profile="DocumentWorker ($S3_PROVIDER)"
media_worker_profile="MediaWorker ($S3_PROVIDER)"
streaming_worker_profile="Alexandria.Workers.MediaTranspilation ($S3_PROVIDER)"
media_metadata_worker_profile="Alexandria.Workers.MediaMetadata ($S3_PROVIDER)"
lyrics_worker_profile="Alexandria.Workers.Lyrics ($S3_PROVIDER)"
playlist_worker_profile="Alexandria.Workers.Playlist ($S3_PROVIDER)"

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

# --- Docker window: one tile per compose service, each tailing its logs ---
log_cmd() {
  printf "docker compose -f %s -p %s logs -f --tail=50 %s; exec %s" \
    "$COMPOSE_FILE" "$COMPOSE_PROJECT" "$1" "${SHELL:-/bin/bash}"
}

# First service owns the pane the window starts with.
DOCKER_PANE=$(tmux new-window -d -P -F '#{pane_id}' -t "$SESSION_NAME" -n "Docker" -c "$PWD" \
  "$(log_cmd "${COMPOSE_LOG_SERVICES[0]}")")
DOCKER_WINDOW=$(tmux display -p -t "$DOCKER_PANE" '#{window_id}')
tmux resize-window -t "$DOCKER_WINDOW" -x 200 -y 48

LOG_PANES=("$DOCKER_PANE")
for service in "${COMPOSE_LOG_SERVICES[@]:1}"; do
  LOG_PANES+=("$(tmux split-window -d -P -F '#{pane_id}' -t "$DOCKER_PANE" -c "$PWD" \
    "$(log_cmd "$service")")")
  tmux select-layout -t "$DOCKER_WINDOW" tiled
done

for i in "${!COMPOSE_LOG_SERVICES[@]}"; do
  tmux select-pane -t "${LOG_PANES[$i]}" -T "${COMPOSE_LOG_SERVICES[$i]}"
  printf "  %s➜%s %-22s %slog tile%s\n" "$CYAN" "$RESET" "${COMPOSE_LOG_SERVICES[$i]}" "$DIM" "$RESET"
done

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
launch "PlaylistWorker" \
  "cd Backend && dotnet run --project Alexandria.Workers.Playlist --no-build --launch-profile \"$playlist_worker_profile\""

tmux select-window -t "$FRONTEND_PANE"

rule
ok "All services launched in $((SECONDS - SCRIPT_START))s"
printf "  %sAttach:%s tmux attach -t %s   %sDetach:%s Ctrl-b d\n" "$DIM" "$RESET" "$SESSION_NAME" "$DIM" "$RESET"
printf "  %sDocker:%s 'Docker' window, %d log tiles %s(Ctrl-b z zooms a tile, Ctrl-b space cycles layouts)%s\n" \
  "$DIM" "$RESET" "${#COMPOSE_LOG_SERVICES[@]}" "$DIM" "$RESET"
printf "\n"

tmux attach-session -t "$SESSION_NAME"
