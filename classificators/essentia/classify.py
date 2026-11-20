#!/usr/bin/env python3
"""
classify.py - one-shot CLI entry point, useful for local testing/manual runs.
For the production two-worker setup, see worker.py, which keeps models
loaded across many batches instead of paying load cost per invocation.

The output JSON contract lives in batch_outputs.py.

Usage:
    python classify.py --backbone {effnet,maest} --output-dir DIR file1.mp3 [file2.mp3 ...]

Exit codes: 0 every file succeeded, 2 some failed, 1 all failed or the
models could not load.
"""

import argparse
import os
import sys
import traceback

from batch_outputs import write_fatal_failure
from essentia_core import Models, process_file


def main():
    parser = argparse.ArgumentParser(description="Classify audio files with Essentia.")
    parser.add_argument("--backbone", choices=["effnet", "maest"], required=True)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("files", nargs="+")
    args = parser.parse_args()

    os.makedirs(args.output_dir, exist_ok=True)

    try:
        models = Models(args.backbone)
    except Exception as e:
        print(f"FATAL: model load failed: {e}", file=sys.stderr)
        traceback.print_exc(file=sys.stderr)
        for input_path in args.files:
            write_fatal_failure(input_path, args.output_dir, args.backbone, f"model load failed: {e}")
        sys.exit(1)

    ok_count = 0
    fail_count = 0
    for input_path in args.files:
        if process_file(models, input_path, args.output_dir):
            ok_count += 1
        else:
            fail_count += 1

    if fail_count == 0:
        sys.exit(0)
    elif ok_count > 0:
        sys.exit(2)
    else:
        sys.exit(1)


if __name__ == "__main__":
    main()
