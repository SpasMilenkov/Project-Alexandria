"""
batch_outputs.py - the per-file output JSON contract, kept free of Essentia
and TensorFlow imports so the lightweight worker parent can write failure
outputs without loading either.
"""

import json
import os

from model_assets import genre_model_name, mood_model_name


def output_path(input_path, output_dir):
    """Where the JSON result for one input file lands."""
    stem = os.path.splitext(os.path.basename(input_path))[0]
    return os.path.join(output_dir, f"{stem}.json")


def base_payload(input_path, backbone):
    return {
        "input_file": os.path.basename(input_path),
        "backbone": backbone,
        "success": False,
        "error": None,
        "processing_seconds": None,
        "genre": None,
        "mood": None,
        "model_versions": {
            "genre": genre_model_name(backbone),
            "mood": mood_model_name(),
        },
    }


def write_payload(input_path, output_dir, payload):
    """Writes through a temporary file so readers never see a partial JSON."""
    final_path = output_path(input_path, output_dir)
    tmp_path = final_path + ".tmp"
    with open(tmp_path, "w") as f:
        json.dump(payload, f, indent=2)
    os.rename(tmp_path, final_path)  # atomic on the same filesystem


def write_fatal_failure(input_path, output_dir, backbone, error_message):
    """Used when a file could not be attempted at all, e.g. the models failed
    to load or the classifier process died before reaching it."""
    payload = base_payload(input_path, backbone)
    payload["error"] = error_message
    write_payload(input_path, output_dir, payload)
