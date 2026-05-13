#!/bin/bash
set -e

# Development environment launcher script
# Usage: ./run-dev.sh [--s3-provider <Garage|RustFS>]
# Example: ./run-dev.sh --s3-provider Garage

SESSION_NAME="dev-session"
S3_PROVIDER="Garage"


while [[ $# -gt 0 ]]; do
  case $1 in
    --s3-provider)
      S3_PROVIDER="$2"
      shift 2
      ;;
    *)
      echo "Unknown option: $1"
      echo "Usage: ./run-dev.sh [--s3-provider <Garage|RustFS>]"
      exit 1
      ;;
  esac
done

case "$S3_PROVIDER" in
  Garage|MinIO|RustFS)
    ;;
  *)
    echo "Invalid S3 provider: $S3_PROVIDER"
    echo "Valid values: Garage, RustFS"
    exit 1
    ;;
esac

echo "Starting dev environment with S3 provider: $S3_PROVIDER"

# ----------------------------
# Launch profile helpers
# ----------------------------
api_profile="API ($S3_PROVIDER)"
doc_worker_profile="DocumentWorker ($S3_PROVIDER)"
media_worker_profile="MediaWorker ($S3_PROVIDER)"
streaming_worker_profile="Alexandria.Workers.MediaTranspilation ($S3_PROVIDER)"
# ----------------------------
# tmux setup
# ----------------------------
tmux kill-session -t "$SESSION_NAME" 2>/dev/null || true

tmux new-session -d -s "$SESSION_NAME" -n "Frontend" -c "$PWD"
sleep 0.2
tmux send-keys -t "$SESSION_NAME:Frontend" \
  "cd Frontend && pnpm dev" C-m

tmux new-window -t "$SESSION_NAME" -n "API" -c "$PWD"
sleep 0.2
tmux send-keys -t "$SESSION_NAME:API" \
  "cd Backend && dotnet run --project Alexandria.Api --launch-profile \"$api_profile\"" C-m

tmux new-window -t "$SESSION_NAME" -n "DocumentWorker" -c "$PWD"
sleep 5.0
tmux send-keys -t "$SESSION_NAME:DocumentWorker" \
  "cd Backend && dotnet run --project Alexandria.Workers.Document --launch-profile \"$doc_worker_profile\"" C-m

tmux new-window -t "$SESSION_NAME" -n "MediaWorker" -c "$PWD"
sleep 5.0
tmux send-keys -t "$SESSION_NAME:MediaWorker" \
  "cd Backend && dotnet run --project Alexandria.Workers.Media --launch-profile \"$media_worker_profile\"" C-m

tmux new-window -t "$SESSION_NAME" -n "TranspilationWorker" -c "$PWD"
sleep 5.0
tmux send-keys -t "$SESSION_NAME:TranspilationWorker" \
  "cd Backend && dotnet run --project Alexandria.Workers.MediaTranspilation --launch-profile \"$streaming_worker_profile\"" C-m

tmux select-window -t "$SESSION_NAME:Frontend"
tmux attach-session -t "$SESSION_NAME"
