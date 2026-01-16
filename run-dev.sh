#!/bin/bash

# Development environment launcher script
# Usage: ./run-dev.sh

SESSION_NAME="dev-session"

# Kill existing session if it exists
tmux kill-session -t $SESSION_NAME 2>/dev/null

# Create new session with frontend (window 0)
tmux new-session -d -s $SESSION_NAME -n "Frontend" -c "$PWD"
sleep 0.2
tmux send-keys -t $SESSION_NAME:Frontend "cd Frontend && pnpm dev" C-m

# Create window for API (window 1)
tmux new-window -t $SESSION_NAME -n "API" -c "$PWD"
sleep 0.2
tmux send-keys -t $SESSION_NAME:API "cd Backend && dotnet run --project API" C-m

# Create window for DocumentWorker (window 2)
tmux new-window -t $SESSION_NAME -n "DocumentWorker" -c "$PWD"
sleep 0.2
tmux send-keys -t $SESSION_NAME:DocumentWorker "cd Backend && dotnet run --project DocumentWorker.Service" C-m

# Create window for MediaWorker (window 3)
tmux new-window -t $SESSION_NAME -n "MediaWorker" -c "$PWD"
sleep 0.2
tmux send-keys -t $SESSION_NAME:MediaWorker "cd Backend && dotnet run --project MediaWorkerService" C-m

# Select the first window (Frontend)
tmux select-window -t $SESSION_NAME:Frontend

# Attach to the session
tmux attach-session -t $SESSION_NAME
