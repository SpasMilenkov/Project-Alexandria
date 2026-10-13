"""Opt-in inference/UID acceptance using existing model assets and cached images."""

import contextlib
import io
import json
import math
import os
from pathlib import Path
import shutil
import struct
import subprocess
import sys
import tempfile
import unittest
import uuid
import wave

SOURCE_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SOURCE_ROOT))
import model_assets


@unittest.skipUnless(os.environ.get("ALEXANDRIA_BUILDER_MODEL_RUNTIME_TESTS") == "1",
                     "Enable cached model runtime acceptance explicitly; no images/models are fetched")
class CachedModelRuntimeTests(unittest.TestCase):
    def docker(self, *arguments, timeout=60):
        if arguments[:1] == ("run",):
            arguments = ("run", "--label", "alexandria.fixture.model-runtime=" + self.identity, *arguments[1:])
        result = subprocess.run(["docker", *arguments], text=True, capture_output=True, timeout=timeout, check=False)
        self.assertEqual(result.returncode, 0, result.stderr[-6000:])
        return result

    def test_both_backbones_infer_with_shared_uid_and_pinned_selected_cache(self):
        source = os.environ.get("ALEXANDRIA_EXISTING_MODEL_SOURCE") or os.environ.get("ALEXANDRIA_EXISTING_MODELS")
        self.assertTrue(source and Path(source).is_dir(), "Set ALEXANDRIA_EXISTING_MODEL_SOURCE to an existing cached model directory")
        self.identity = os.environ.get("ALEXANDRIA_MODEL_RUNTIME_IDENTITY", uuid.uuid4().hex)
        classifier = os.environ.get("ALEXANDRIA_ESSENTIA_IMAGE", "alexandria-local-essentia-effnet-worker:latest")
        coordinator = os.environ.get("ALEXANDRIA_METADATA_IMAGE", "alexandria-local-media-metadata-worker:latest")
        self.docker("image", "inspect", classifier, coordinator)
        target = os.environ.get("ALEXANDRIA_MODEL_RUNTIME_TARGET")
        if target:
            temporary = Path(target)
            temporary.mkdir(parents=True, exist_ok=False)
        else:
            temporary = Path(tempfile.mkdtemp(prefix="alexandria-cached-model-runtime-"))
        volume = "builder-model-runtime-" + self.identity
        self.docker("volume", "create", "--label", "alexandria.fixture=model-runtime", volume)

        try:
            audio = temporary / "fixture.wav"
            with wave.open(str(audio), "wb") as output:
                output.setnchannels(1)
                output.setsampwidth(2)
                output.setframerate(16000)
                samples = (int(6000 * math.sin(2 * math.pi * 220 * index / 16000)
                               + 3000 * math.sin(2 * math.pi * 440 * index / 16000)) for index in range(31 * 16000))
                output.writeframes(b"".join(struct.pack("<h", sample) for sample in samples))
            audio.chmod(0o644)
            mount = "type=volume,src=" + volume + ",dst=/data"
            setup = self.docker("run", "--rm", "--pull", "never", "--network", "none", "--read-only",
                                "--mount", mount, "--mount", f"type=bind,src={audio},dst=/fixture.wav,readonly",
                                "--entrypoint", "/bin/sh", coordinator, "-c",
                                "test \"$(id -u)\" = 1001; test \"$(id -g)\" = 1001; "
                                "mkdir -p /data/staged /data/output; cp /fixture.wav /data/staged/fixture.wav; "
                                "printf coordinator-fixture > /data/staged/shared.txt; echo uid1001-staging-ready")
            self.assertIn("uid1001-staging-ready", setup.stdout)

            for backbone in ("effnet", "maest"):
                with self.subTest(backbone=backbone):
                    cache = temporary / backbone
                    with contextlib.redirect_stdout(io.StringIO()):
                        model_assets.prepare_models(cache, backbone, Path(source))
                    self.assertEqual(len(list(cache.glob("*.pb"))) + len(list(cache.glob("*.json"))), 17)
                    arguments = ["run", "--rm", "--pull", "never", "--network", "none", "--read-only",
                                 "--cpus", "4", "--memory", "2g", "--tmpfs", "/tmp:rw,nosuid,size=64m",
                                 "--mount", mount, "--mount", f"type=bind,src={cache},dst=/app/models,readonly",
                                 "--env", "BACKBONE=" + backbone]
                    for name in ("entrypoint.sh", "model_assets.py", "essentia_core.py", "classify.py", "model-requirements.tsv"):
                        arguments += ["--mount", f"type=bind,src={SOURCE_ROOT / name},dst=/app/{name},readonly"]
                    arguments += ["--mount", f"type=bind,src={SOURCE_ROOT / 'models/models.sha256'},dst=/app/models.sha256,readonly",
                                  "--entrypoint", "/bin/bash", classifier, "/app/entrypoint.sh", "/bin/sh", "-c",
                                  "test \"$(id -u)\" = 1001; test \"$(id -g)\" = 1001; "
                                  "test \"$(cat /data/staged/shared.txt)\" = coordinator-fixture; "
                                  f"python /app/classify.py --backbone {backbone} --output-dir /data/output /data/staged/fixture.wav; "
                                  "result=$?; printf MEMORY_PEAK_BYTES= >&2; cat /sys/fs/cgroup/memory.peak >&2; exit $result"]
                    inference = self.docker(*arguments, timeout=600)
                    peak = next((line for line in inference.stderr.splitlines() if line.startswith("MEMORY_PEAK_BYTES=")), None)
                    print(f"{backbone}: current inference succeeded with 2 GiB memory cap; {peak}", flush=True)
                    readback = self.docker("run", "--rm", "--pull", "never", "--network", "none", "--read-only",
                                          "--mount", mount, "--entrypoint", "/bin/sh", coordinator, "-c",
                                          "cat /data/output/fixture.json; rm /data/output/fixture.json")
                    payload = json.loads(readback.stdout)
                    self.assertTrue(payload["success"], payload.get("error"))
                    self.assertEqual(payload["backbone"], backbone)
                    self.assertEqual(payload["genre"]["backbone"], backbone)
                    self.assertEqual(len(payload["mood"]), 7)
                    self.assertEqual(len(payload["genre"]["predictions"]), 5)

            self.docker("run", "--rm", "--pull", "never", "--network", "none", "--read-only", "--mount", mount,
                        "--entrypoint", "/bin/sh", coordinator, "-c", "rm /data/staged/fixture.wav /data/staged/shared.txt")
        finally:
            self.docker("volume", "rm", volume)
            shutil.rmtree(temporary)


if __name__ == "__main__":
    unittest.main()
