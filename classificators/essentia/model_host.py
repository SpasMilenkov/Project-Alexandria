"""
model_host.py - owns the model process for the worker parent.

The models live in a child process (model_child.py) that is started when a
batch arrives and nothing is loaded, reused while batches keep coming, and
stopped once it has been idle for `idle_unload_seconds`. Ending the process
is what returns TensorFlow's memory to the OS.

This module must stay free of Essentia and TensorFlow imports.
"""

import multiprocessing
import time

import model_child
from batch_outputs import write_fatal_failure


class ModelHost:
    def __init__(self, backbone, idle_unload_seconds, log, target=model_child.serve,
                 poll_seconds=1.0, stop_grace_seconds=10.0, clock=time.monotonic):
        """
        idle_unload_seconds: how long the models may sit unused before the
            process is stopped; 0 keeps them loaded for the worker's lifetime.
        target: the child entry point, replaceable in tests so no models load.
        """
        self._backbone = backbone
        self._idle_unload_seconds = idle_unload_seconds
        self._log = log
        self._target = target
        self._poll_seconds = poll_seconds
        self._stop_grace_seconds = stop_grace_seconds
        self._clock = clock
        # spawn, not fork: the child gets a clean interpreter and never
        # inherits the parent's open RabbitMQ socket.
        self._context = multiprocessing.get_context("spawn")
        self._process = None
        self._connection = None
        self._last_used = None

    @property
    def is_loaded(self):
        return self._process is not None

    def load(self, keep_alive):
        """Starts the model process if it is not running. Returns None once it
        is ready, otherwise a description of why loading failed."""
        if self._process is not None and not self._process.is_alive():
            self._log(f"Model process exited on its own (code {self._process.exitcode}); starting a new one.")
            self._discard()

        if self._process is not None:
            return None

        self._log(f"Loading models for backbone={self._backbone}...")
        start = time.perf_counter()
        parent_end, child_end = self._context.Pipe(duplex=True)
        process = self._context.Process(
            target=self._target, args=(child_end, self._backbone),
            name=f"essentia-{self._backbone}-models", daemon=True)
        process.start()
        # Without closing our copy of the child's end, a dead child would
        # never show up as EOF on our end.
        child_end.close()
        self._process = process
        self._connection = parent_end
        self._last_used = self._clock()

        message = self._receive(keep_alive)
        if message is not None and message["type"] == "ready":
            self._log(f"Models loaded in {time.perf_counter() - start:.1f}s (process start included).")
            return None

        if message is None:
            error = f"model process exited with code {process.exitcode} while loading"
        elif message["type"] == "load_failed":
            error = message["error"]
        else:
            error = f"model process sent {message['type']!r} before it was ready"
        self._log(f"Model load failed: {error}")
        self._discard()
        return error

    def run_batch(self, files, output_dir, keep_alive):
        """
        Classifies `files` into `output_dir`, loading the models first when
        needed. `keep_alive` is called roughly every `poll_seconds` while the
        child works, so the caller can service its broker connection.

        Returns (ok_count, fail_count). Classifier failures never raise: any
        file the child could not attempt gets a fatal-failure JSON instead.
        If `keep_alive` raises, the child is stopped (it may be mid-batch and
        its late replies must not reach the next batch) and the error
        propagates.
        """
        try:
            return self._run_batch(files, output_dir, keep_alive)
        except BaseException as e:
            self._log(f"Batch interrupted ({type(e).__name__}); stopping the model process.")
            self._discard()
            raise
        finally:
            self._last_used = self._clock()

    def unload_if_idle(self):
        """Stops the model process once it has been unused long enough.
        Returns True when it did."""
        if self._process is None or self._idle_unload_seconds <= 0:
            return False

        idle = self._clock() - self._last_used
        if idle < self._idle_unload_seconds:
            return False

        self._stop()
        self._log(f"Models unloaded after {idle:.0f}s idle.")
        return True

    def close(self):
        if self._process is None:
            return
        self._stop()
        self._log("Models unloaded for shutdown.")

    def _run_batch(self, files, output_dir, keep_alive):
        error = self.load(keep_alive)
        if error is not None:
            self._fail_files(files, output_dir, f"model load failed: {error}")
            return 0, len(files)

        try:
            self._connection.send({"type": "batch", "files": files, "output_dir": output_dir})
        except OSError as e:
            self._log(f"Could not hand the batch to the model process: {e}")
            self._discard()
            self._fail_files(files, output_dir, f"could not reach the model process: {e}")
            return 0, len(files)

        ok_count = 0
        fail_count = 0
        while True:
            message = self._receive(keep_alive)
            if message is None:
                exit_code = self._process.exitcode
                remaining = files[ok_count + fail_count:]
                self._log(f"Model process exited with code {exit_code} mid-batch; "
                          f"{len(remaining)} file(s) not attempted.")
                self._discard()
                self._fail_files(remaining, output_dir,
                                 f"classifier process exited with code {exit_code} before finishing this file")
                return ok_count, fail_count + len(remaining)

            if message["type"] == "file_done":
                if message["success"]:
                    ok_count += 1
                else:
                    fail_count += 1
            elif message["type"] == "batch_done":
                return ok_count, fail_count
            else:
                raise RuntimeError(f"Model process sent an unexpected message type: {message['type']!r}")

    def _receive(self, keep_alive):
        """Next message from the child, or None once it has died."""
        while True:
            if self._connection.poll(self._poll_seconds):
                try:
                    return self._connection.recv()
                except EOFError:
                    self._process.join(self._stop_grace_seconds)
                    return None
            if not self._process.is_alive():
                return None
            keep_alive()

    def _stop(self):
        """Asks the idle child to exit, escalating if it does not."""
        process = self._process
        try:
            self._connection.send({"type": "stop"})
        except OSError as e:
            self._log(f"Could not ask the model process to stop ({e}); terminating it.")
        process.join(self._stop_grace_seconds)
        if process.is_alive():
            self._log(f"Model process still running {self._stop_grace_seconds:.0f}s after stop; terminating it.")
        self._discard()

    def _discard(self):
        """Ends the child without waiting for it to finish what it is doing."""
        process, connection = self._process, self._connection
        if process is None:
            return
        self._process = None
        self._connection = None
        if process.is_alive():
            process.terminate()
            process.join(5)
        if process.is_alive():
            self._log("Model process ignored SIGTERM; killing it.")
            process.kill()
            process.join()
        connection.close()

    def _fail_files(self, files, output_dir, error_message):
        for input_path in files:
            try:
                write_fatal_failure(input_path, output_dir, self._backbone, error_message)
            except OSError as e:
                self._log(f"Could not write the failure output for {input_path}: {e}")
