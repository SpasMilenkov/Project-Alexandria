"""
essentia_core.py - shared model loading and classification logic, used by
both classify.py (CLI, for local/manual runs) and model_child.py (the model
process the RabbitMQ worker starts on demand). This is the single place the
actual Essentia pipeline logic lives; both entry points just call into it.

Importing this module loads Essentia and TensorFlow, so the worker parent
must never import it.
"""

import json
import os
import sys
import time
import traceback

from batch_outputs import base_payload, write_payload
from model_assets import (
    EFFNET_EMBEDDING_MODEL, EFFNET_EMBEDDING_OUTPUT,
    EFFNET_GENRE_MODEL, EFFNET_GENRE_META,
    MAEST_GENRE_MODEL, MAEST_GENRE_META, MOOD_CLASSIFIERS,
)

os.environ.setdefault("TF_CPP_MIN_LOG_LEVEL", "3")

import essentia
import essentia.standard as es
import numpy as np

essentia.log.infoActive = False
essentia.log.warningActive = False

MODEL_DIR = os.environ.get("ESSENTIA_MODEL_DIR", "/app/models")


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
    In the worker, the model child (model_child.py) builds one instance when
    it starts and reuses it across every batch until the worker parent stops
    it after a period of inactivity.
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
    payload = base_payload(input_path, models.backbone)

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
        write_payload(input_path, output_dir, payload)

    return payload["success"]
