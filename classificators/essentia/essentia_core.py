"""
essentia_core.py - shared model loading and classification logic, used by
both classify.py (CLI, for local/manual runs) and worker.py (long-lived
RabbitMQ consumer). This is the single place the actual Essentia pipeline
logic lives; both entry points just call into it.
"""

import json
import os
import sys
import time
import traceback

os.environ.setdefault("TF_CPP_MIN_LOG_LEVEL", "3")

import essentia
import essentia.standard as es
import numpy as np

essentia.log.infoActive = False
essentia.log.warningActive = False

MODEL_DIR = os.environ.get("ESSENTIA_MODEL_DIR", "/app/models")

EFFNET_EMBEDDING_MODEL = "discogs-effnet-bs64-1.pb"
EFFNET_EMBEDDING_OUTPUT = "PartitionedCall:1"

EFFNET_GENRE_MODEL = "genre_discogs400-discogs-effnet-1.pb"
EFFNET_GENRE_META = "genre_discogs400-discogs-effnet-1.json"

MAEST_GENRE_MODEL = "discogs-maest-30s-pw-519l-2.pb"
MAEST_GENRE_META = "discogs-maest-30s-pw-519l-2.json"

MOOD_CLASSIFIERS = [
    {"name": "mood_aggressive", "model": "mood_aggressive-discogs-effnet-1.pb",
     "meta": "mood_aggressive-discogs-effnet-1.json", "output": "model/Softmax"},
    {"name": "mood_happy", "model": "mood_happy-discogs-effnet-1.pb",
     "meta": "mood_happy-discogs-effnet-1.json", "output": "model/Softmax"},
    {"name": "mood_party", "model": "mood_party-discogs-effnet-1.pb",
     "meta": "mood_party-discogs-effnet-1.json", "output": "model/Softmax"},
    {"name": "mood_relaxed", "model": "mood_relaxed-discogs-effnet-1.pb",
     "meta": "mood_relaxed-discogs-effnet-1.json", "output": "model/Softmax"},
    {"name": "mood_sad", "model": "mood_sad-discogs-effnet-1.pb",
     "meta": "mood_sad-discogs-effnet-1.json", "output": "model/Softmax"},
    {"name": "mood_acoustic", "model": "mood_acoustic-discogs-effnet-1.pb",
     "meta": "mood_acoustic-discogs-effnet-1.json", "output": "model/Softmax"},
    {"name": "voice_instrumental", "model": "voice_instrumental-discogs-effnet-1.pb",
     "meta": "voice_instrumental-discogs-effnet-1.json", "output": "model/Softmax"},
]


def genre_model_name(backbone):
    """Identifier of the genre classification model used for a backbone.

    This is the exact model filename (minus extension) so downstream systems
    (the .NET orchestrator) can store a reproducible version string without
    guessing or hardcoding it independently on both sides. Model filenames
    are known to drift (e.g. a '-1' -> '-2' suffix bump) between Essentia
    model-zoo releases; this is the single source of truth for that string.
    """
    filename = EFFNET_GENRE_MODEL if backbone == "effnet" else MAEST_GENRE_MODEL
    return os.path.splitext(filename)[0]


def mood_model_name():
    """Identifier of the EffNet embedding model used to compute every mood head."""
    return os.path.splitext(EFFNET_EMBEDDING_MODEL)[0]


def load_meta(json_name):
    with open(os.path.join(MODEL_DIR, json_name)) as f:
        return json.load(f)


def prediction_output_name(meta):
    for out in meta["schema"]["outputs"]:
        if out.get("output_purpose") == "predictions":
            return out["name"]
    raise ValueError("No 'predictions' output found in metadata")


def input_name(meta):
    return meta["schema"]["inputs"][0]["name"]


def class_labels(meta):
    return meta["classes"]


def top_n(predictions, labels, n=5):
    predictions = predictions.squeeze()
    if predictions.ndim == 1:
        predictions = predictions[np.newaxis, :]
    avg = predictions.mean(axis=0)
    idx = avg.argsort()[::-1][:n]
    return [{"label": labels[i], "score": float(avg[i])} for i in idx]


def classify_binary(predictions, labels):
    predictions = predictions.squeeze()
    if predictions.ndim == 1:
        predictions = predictions[np.newaxis, :]
    avg = predictions.mean(axis=0)
    return {labels[i]: float(avg[i]) for i in range(len(labels))}


class Models:
    """
    Loads every model exactly once. In the CLI (classify.py), one instance
    is built per process invocation, shared across the files in that batch.
    In the worker (worker.py), one instance is built ONCE at process
    startup and reused across every batch consumed for the worker's entire
    lifetime, this is what actually amortizes model load time.
    """

    def __init__(self, backbone):
        self.backbone = backbone

        self.effnet_embedder = es.TensorflowPredictEffnetDiscogs(
            graphFilename=os.path.join(MODEL_DIR, EFFNET_EMBEDDING_MODEL),
            output=EFFNET_EMBEDDING_OUTPUT,
        )

        self.mood_classifiers = []
        for cfg in MOOD_CLASSIFIERS:
            meta = load_meta(cfg["meta"])
            clf = es.TensorflowPredict2D(
                graphFilename=os.path.join(MODEL_DIR, cfg["model"]),
                output=cfg["output"],
            )
            self.mood_classifiers.append({"name": cfg["name"], "clf": clf, "labels": class_labels(meta)})

        if backbone == "effnet":
            clf_meta = load_meta(EFFNET_GENRE_META)
            self.genre_clf = es.TensorflowPredict2D(
                graphFilename=os.path.join(MODEL_DIR, EFFNET_GENRE_MODEL),
                input=input_name(clf_meta),
                output=prediction_output_name(clf_meta),
            )
            self.genre_labels = class_labels(clf_meta)
        elif backbone == "maest":
            meta = load_meta(MAEST_GENRE_META)
            self.maest_model = es.TensorflowPredictMAEST(
                graphFilename=os.path.join(MODEL_DIR, MAEST_GENRE_MODEL),
                output=prediction_output_name(meta),
            )
            self.genre_labels = class_labels(meta)
        else:
            raise ValueError(f"Unknown backbone: {backbone}")

    def classify_file(self, audio):
        result = {"genre": None, "mood": {}}

        effnet_embeddings = self.effnet_embedder(audio)

        if self.backbone == "effnet":
            genre_predictions = self.genre_clf(effnet_embeddings)
            result["genre"] = {"backbone": "effnet", "predictions": top_n(genre_predictions, self.genre_labels)}
        else:
            genre_predictions = self.maest_model(audio)
            result["genre"] = {"backbone": "maest", "predictions": top_n(genre_predictions, self.genre_labels)}

        for m in self.mood_classifiers:
            predictions = m["clf"](effnet_embeddings)
            result["mood"][m["name"]] = classify_binary(predictions, m["labels"])

        return result


def process_file(models, input_path, output_dir):
    """
    Always writes exactly one output JSON for this input, regardless of
    success or failure. The try/except/finally shape guarantees this: no
    code path here can skip the write, so the caller (CLI or worker) can
    trust that "no output file" only ever means "the process died before
    reaching this function at all", never a silently-swallowed per-file
    error.
    """
    stem = os.path.splitext(os.path.basename(input_path))[0]
    final_path = os.path.join(output_dir, f"{stem}.json")
    tmp_path = final_path + ".tmp"

    payload = {
        "input_file": os.path.basename(input_path),
        "backbone": models.backbone,
        "success": False,
        "error": None,
        "processing_seconds": None,
        "genre": None,
        "mood": None,
        "model_versions": {
            "genre": genre_model_name(models.backbone),
            "mood": mood_model_name(),
        },
    }

    start = time.perf_counter()
    try:
        audio = es.MonoLoader(filename=input_path, sampleRate=16000, resampleQuality=4)()
        result = models.classify_file(audio)
        payload["genre"] = result["genre"]
        payload["mood"] = result["mood"]
        payload["success"] = True
    except Exception as e:
        payload["success"] = False
        payload["error"] = str(e)
        traceback.print_exc(file=sys.stderr)  # human-debug only, callers don't read this
    finally:
        payload["processing_seconds"] = time.perf_counter() - start
        with open(tmp_path, "w") as f:
            json.dump(payload, f, indent=2)
        os.rename(tmp_path, final_path)  # atomic on the same filesystem

    return payload["success"]


def write_fatal_failure(input_path, output_dir, backbone, error_message):
    """Used when Models() itself fails to load, before any file can be attempted."""
    stem = os.path.splitext(os.path.basename(input_path))[0]
    final_path = os.path.join(output_dir, f"{stem}.json")
    tmp_path = final_path + ".tmp"
    payload = {
        "input_file": os.path.basename(input_path),
        "backbone": backbone,
        "success": False,
        "error": error_message,
        "processing_seconds": None,
        "genre": None,
        "mood": None,
        "model_versions": {
            "genre": genre_model_name(backbone),
            "mood": mood_model_name(),
        },
    }
    with open(tmp_path, "w") as f:
        json.dump(payload, f, indent=2)
    os.rename(tmp_path, final_path)
