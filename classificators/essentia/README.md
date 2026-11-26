# Essentia Classificators

Python side of Alexandria's audio analysis pipeline. Two long-lived RabbitMQ
consumers classify audio batches with [Essentia](https://essentia.upf.edu/)
TensorFlow models and report results back to the .NET media-metadata worker.

## Layout

| File | Purpose |
|------|---------|
| `worker.py` | Long-lived RabbitMQ consumer. Loads models once at startup, processes batch messages, publishes completion results to `content-exchange` (`media-metadata.completed`) |
| `classify.py` | CLI entry point for one-shot manual runs |
| `essentia_core.py` | Shared model loading + classification logic (single source of pipeline truth) |
| `download-models.sh` | Fetches model files into `models/` |
| `entrypoint.sh` | Verifies `models.sha256` checksums before every run (skip with `VERIFY_MODELS=false`) |
| `models/` | EffNet/MAEST genre models, mood classifiers, voice/instrumental — checksummed |

## Runtime contract

Run one container per backbone:

```
BACKBONE=effnet QUEUE_NAME=classify.effnet  python worker.py
BACKBONE=maest  QUEUE_NAME=classify.maest   python worker.py
```

**Input** (one message per batch):

```json
{
  "batch_id": "b47f...",
  "output_dir": "/data/output/b47f...",
  "files": ["/data/input/song1.mp3"]
}
```

**Completion result** (published after the batch):

```json
{
  "batch_id": "b47f...",
  "backbone": "effnet",
  "ok_count": 9,
  "fail_count": 1,
  "output_dir": "/data/output/b47f...",
  "seconds": 38.2,
  "model_versions": { "genre": "...", "mood": "..." }
}
```

Messages are acked once every file is attempted (per-file failures count as
attempted). Only pre-processing failures (malformed body, unwritable output
dir) nack without requeue.

I/O happens through a shared volume mounted at `/data`; `.NET` owns dispatch
and cleanup. Model version identifiers come from the Python side
(`model_versions`) and are never hardcoded in .NET.

See `plans/essentia-enrichment-plan.md` for the full orchestration design.
