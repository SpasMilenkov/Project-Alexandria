"""Model process lifecycle: on-demand load, idle unload, crash and load failures.

The child targets below stand in for model_child.serve so no Essentia or
TensorFlow is needed; they run in real spawned processes like the worker's.
"""

import json
import os
from pathlib import Path
import sys
import tempfile
import time
import unittest

SOURCE_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SOURCE_ROOT))
from model_host import ModelHost


def serve_successfully(connection, backbone):
    connection.send({"type": "ready"})
    while True:
        message = connection.recv()
        if message["type"] == "stop":
            return
        for _ in message["files"]:
            connection.send({"type": "file_done", "success": True})
        connection.send({"type": "batch_done"})


def serve_slowly(connection, backbone):
    connection.send({"type": "ready"})
    while True:
        message = connection.recv()
        if message["type"] == "stop":
            return
        time.sleep(0.5)
        connection.send({"type": "batch_done"})


def crash_after_first_file(connection, backbone):
    connection.send({"type": "ready"})
    connection.recv()
    connection.send({"type": "file_done", "success": True})
    os._exit(9)


def report_load_failure(connection, backbone):
    connection.send({"type": "load_failed", "error": "OSError: model graph missing"})


def die_while_loading(connection, backbone):
    os._exit(3)


class ModelHostTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="essentia-model-host-tests-")
        self.output_dir = self.temporary.name
        self.now = 1000.0
        self.messages = []
        self.hosts = []

    def tearDown(self):
        for host in self.hosts:
            host.close()
        self.temporary.cleanup()

    def host(self, target, idle_unload_seconds=300):
        host = ModelHost(backbone="effnet", idle_unload_seconds=idle_unload_seconds, log=self.messages.append,
                         target=target, poll_seconds=0.05, stop_grace_seconds=2.0, clock=lambda: self.now)
        self.hosts.append(host)
        return host

    def files(self, count):
        return [f"/data/staged/track{index}.mp3" for index in range(count)]

    def output(self, index):
        with open(os.path.join(self.output_dir, f"track{index}.json")) as f:
            return json.load(f)

    def test_first_batch_starts_the_model_process(self):
        host = self.host(serve_successfully)
        self.assertFalse(host.is_loaded)

        counts = host.run_batch(files=self.files(3), output_dir=self.output_dir, keep_alive=lambda: None)

        self.assertEqual(counts, (3, 0))
        self.assertTrue(host.is_loaded)

    def test_models_unload_only_after_the_idle_period(self):
        host = self.host(serve_successfully, idle_unload_seconds=300)
        host.run_batch(files=self.files(1), output_dir=self.output_dir, keep_alive=lambda: None)
        process = host._process

        self.now += 299
        self.assertFalse(host.unload_if_idle())
        self.assertTrue(host.is_loaded)

        self.now += 1
        self.assertTrue(host.unload_if_idle())
        self.assertFalse(host.is_loaded)
        self.assertEqual(process.exitcode, 0, "an idle child should exit on the stop message, not be killed")

    def test_next_batch_after_unload_starts_a_fresh_process(self):
        host = self.host(serve_successfully)
        host.run_batch(files=self.files(1), output_dir=self.output_dir, keep_alive=lambda: None)
        first = host._process.pid
        self.now += 300
        host.unload_if_idle()

        counts = host.run_batch(files=self.files(2), output_dir=self.output_dir, keep_alive=lambda: None)

        self.assertEqual(counts, (2, 0))
        self.assertNotEqual(host._process.pid, first)

    def test_zero_keeps_models_loaded(self):
        host = self.host(serve_successfully, idle_unload_seconds=0)
        host.run_batch(files=self.files(1), output_dir=self.output_dir, keep_alive=lambda: None)

        self.now += 365 * 24 * 3600
        self.assertFalse(host.unload_if_idle())
        self.assertTrue(host.is_loaded)

    def test_idle_clock_restarts_when_a_batch_finishes(self):
        host = self.host(serve_successfully, idle_unload_seconds=300)
        host.run_batch(files=self.files(1), output_dir=self.output_dir, keep_alive=lambda: None)
        self.now += 200
        host.run_batch(files=self.files(1), output_dir=self.output_dir, keep_alive=lambda: None)

        self.now += 200
        self.assertFalse(host.unload_if_idle())

    def test_keep_alive_runs_while_the_child_works(self):
        host = self.host(serve_slowly)
        calls = []

        host.run_batch(files=self.files(1), output_dir=self.output_dir, keep_alive=lambda: calls.append(1))

        self.assertGreater(len(calls), 0)

    def test_crash_mid_batch_fails_the_unattempted_files(self):
        host = self.host(crash_after_first_file)

        counts = host.run_batch(files=self.files(3), output_dir=self.output_dir, keep_alive=lambda: None)

        self.assertEqual(counts, (1, 2))
        self.assertFalse(host.is_loaded)
        self.assertFalse(os.path.exists(os.path.join(self.output_dir, "track0.json")),
                         "the file the child reported must keep the child's own output")
        for index in (1, 2):
            payload = self.output(index)
            self.assertFalse(payload["success"])
            self.assertIn("exited with code 9", payload["error"])
            self.assertEqual(payload["backbone"], "effnet")

    def test_load_failure_fails_every_file_and_retries_next_batch(self):
        host = self.host(report_load_failure)

        counts = host.run_batch(files=self.files(2), output_dir=self.output_dir, keep_alive=lambda: None)

        self.assertEqual(counts, (0, 2))
        self.assertFalse(host.is_loaded)
        self.assertIn("model graph missing", self.output(0)["error"])
        self.assertIn("model graph missing", self.output(1)["error"])

        host._target = serve_successfully
        self.assertEqual(host.run_batch(files=self.files(1), output_dir=self.output_dir, keep_alive=lambda: None),
                         (1, 0))

    def test_death_while_loading_fails_every_file(self):
        host = self.host(die_while_loading)

        counts = host.run_batch(files=self.files(2), output_dir=self.output_dir, keep_alive=lambda: None)

        self.assertEqual(counts, (0, 2))
        self.assertIn("exited with code 3 while loading", self.output(0)["error"])

    def test_interrupted_batch_stops_the_child_and_propagates(self):
        host = self.host(serve_slowly)
        host.load(keep_alive=lambda: None)
        process = host._process

        def broker_lost():
            raise ConnectionError("broker went away")

        with self.assertRaises(ConnectionError):
            host.run_batch(files=self.files(1), output_dir=self.output_dir, keep_alive=broker_lost)

        self.assertFalse(host.is_loaded)
        self.assertFalse(process.is_alive())

    def test_child_that_died_while_idle_is_replaced(self):
        host = self.host(serve_successfully)
        host.load(keep_alive=lambda: None)
        host._process.kill()
        host._process.join()

        counts = host.run_batch(files=self.files(1), output_dir=self.output_dir, keep_alive=lambda: None)

        self.assertEqual(counts, (1, 0))


if __name__ == "__main__":
    unittest.main()
