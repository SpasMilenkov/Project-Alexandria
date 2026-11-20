"""
model_child.py - entry point of the short-lived model process.

The worker parent (worker.py, through model_host.py) starts this in a fresh
interpreter when a batch arrives and no models are loaded. It is the only
process that imports Essentia and TensorFlow, so when the parent stops it
after a period of inactivity, every byte those libraries held goes back to
the OS. Freeing the models inside a long-lived process does not achieve
that: TensorFlow's allocator and glibc keep freed memory mapped.

Pipe protocol (dicts sent over a multiprocessing Connection):
    child -> parent  {"type": "ready"}                       models loaded
                     {"type": "load_failed", "error": str}   then exits
                     {"type": "file_done", "success": bool}  once per file, in order
                     {"type": "batch_done"}                  after the last file
    parent -> child  {"type": "batch", "files": [...], "output_dir": str}
                     {"type": "stop"}                        exit cleanly
"""

import sys
import traceback


def serve(connection, backbone):
    try:
        # Imported here, not at module level: the parent imports this module
        # to hand the function to multiprocessing and must stay TF-free.
        from essentia_core import Models, process_file
        models = Models(backbone)
    except Exception as e:
        traceback.print_exc(file=sys.stderr)
        connection.send({"type": "load_failed", "error": f"{type(e).__name__}: {e}"})
        return

    connection.send({"type": "ready"})

    while True:
        try:
            message = connection.recv()
        except EOFError:
            print("Model process: worker closed the pipe, exiting.", file=sys.stderr, flush=True)
            return

        if message["type"] == "stop":
            return
        if message["type"] != "batch":
            raise ValueError(f"Model process received an unknown message type: {message['type']!r}")

        for input_path in message["files"]:
            succeeded = process_file(models, input_path, message["output_dir"])
            connection.send({"type": "file_done", "success": succeeded})
        connection.send({"type": "batch_done"})
