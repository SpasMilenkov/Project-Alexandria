import contextlib
import hashlib
import importlib.util
import io
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import types
import unittest
from unittest import mock
import urllib.error

SOURCE_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SOURCE_ROOT))
import model_assets


class ModelAssetsTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="essentia-offline-tests-")
        self.root = Path(self.temporary.name)
        self.source = self.root / "source"
        self.target = self.root / "target"
        self.source.mkdir()
        checksums = []
        requirements = []

        for line in (SOURCE_ROOT / "model-requirements.tsv").read_text().splitlines():
            if not line or line.startswith("#"):
                continue
            group, name, _, download_path = line.split()
            content = ("fixture-" + name).encode()
            (self.source / name).write_bytes(content)
            checksums.append(f"{hashlib.sha256(content).hexdigest()}  {name}\n")
            requirements.append(f"{group} {name} {len(content)} {download_path}\n")

        (self.root / "models.sha256").write_text("".join(checksums))
        (self.root / "model-requirements.tsv").write_text("".join(requirements))

    def tearDown(self):
        self.temporary.cleanup()

    def prepare(self, backbone="effnet", allow_download=False):
        with contextlib.redirect_stdout(io.StringIO()):
            model_assets.prepare_models(self.target, backbone, self.source, allow_download, self.root)

    def test_inference_loaded_files_exactly_match_pinned_selected_requirements(self):
        essentia = types.ModuleType("essentia")
        standard = types.ModuleType("essentia.standard")
        essentia.standard = standard
        essentia.log = types.SimpleNamespace()
        loaded = set()

        def record_graph(**arguments):
            loaded.add(Path(arguments["graphFilename"]).name)

        standard.TensorflowPredictEffnetDiscogs = record_graph
        standard.TensorflowPredict2D = record_graph
        standard.TensorflowPredictMAEST = record_graph
        specification = importlib.util.spec_from_file_location("fixture_core", SOURCE_ROOT / "essentia_core.py")
        core = importlib.util.module_from_spec(specification)

        with mock.patch.dict(sys.modules, {"essentia": essentia, "essentia.standard": standard,
                                          "numpy": types.ModuleType("numpy")}):
            specification.loader.exec_module(core)

        def record_metadata(filename):
            loaded.add(filename)
            return {"classes": [], "schema": {"inputs": [{"name": "input"}], "outputs": [
                {"name": "output", "output_purpose": "predictions"}]}}

        core.load_meta = record_metadata
        for backbone in ("effnet", "maest"):
            with self.subTest(backbone=backbone):
                loaded.clear()
                core.Models(backbone)
                self.assertEqual(loaded, {asset[0] for asset in model_assets.load_assets(backbone)})
                self.assertEqual(len(loaded), 17)

    def test_selected_subset_does_not_require_unrelated_backbone_or_legacy_models(self):
        self.prepare()
        (self.target / "models.sha256").unlink()

        model_assets.verify_models(self.target, "effnet", self.root)

        self.assertFalse((self.target / model_assets.MAEST_GENRE_MODEL).exists())
        self.assertEqual(len([path for path in self.target.iterdir() if not path.name.startswith('.')]), 17)

    def test_maest_keeps_shared_effnet_mood_dependencies(self):
        self.prepare("maest")

        model_assets.verify_models(self.target, "maest", self.root)

        self.assertTrue((self.target / model_assets.EFFNET_EMBEDDING_MODEL).exists())
        self.assertFalse((self.target / model_assets.EFFNET_GENRE_MODEL).exists())

    def test_verified_cache_is_reused_without_source_or_network(self):
        self.prepare()
        shutil.rmtree(self.source)

        with mock.patch.object(model_assets.urllib.request, "urlopen") as fetch:
            self.prepare()

        fetch.assert_not_called()

    def test_missing_source_is_reported_without_network(self):
        (self.source / model_assets.EFFNET_EMBEDDING_MODEL).unlink()

        with mock.patch.object(model_assets.urllib.request, "urlopen") as fetch:
            with self.assertRaisesRegex(FileNotFoundError, "downloads are disabled"):
                self.prepare()

        fetch.assert_not_called()

    def test_corrupt_source_never_promotes_and_removes_partial(self):
        (self.source / model_assets.EFFNET_GENRE_MODEL).write_bytes(b"corrupt")

        with self.assertRaisesRegex(ValueError, "verification failed"):
            self.prepare()

        self.assertTrue((self.target / model_assets.EFFNET_EMBEDDING_MODEL).exists())
        self.assertFalse((self.target / model_assets.EFFNET_GENRE_MODEL).exists())
        self.assertEqual(list(self.target.glob("*.partial")), [])

    def test_mutable_mounted_manifest_cannot_redefine_pinned_hash(self):
        self.prepare()
        name = model_assets.EFFNET_EMBEDDING_MODEL
        model = self.target / name
        model.write_bytes(b"x" * model.stat().st_size)
        (self.target / "models.sha256").write_text(f"{hashlib.sha256(model.read_bytes()).hexdigest()}  {name}\n")

        with self.assertRaisesRegex(ValueError, "Missing/corrupt"):
            model_assets.verify_models(self.target, "effnet", self.root)

    def test_explicit_fetch_uses_fake_source_and_bounded_retries(self):
        path = self.source / model_assets.EFFNET_EMBEDDING_MODEL
        content = path.read_bytes()
        path.unlink()

        with mock.patch.object(model_assets.urllib.request, "urlopen", side_effect=[
                urllib.error.URLError("fixture transient failure"), io.BytesIO(content)]) as fetch:
            with mock.patch.object(model_assets.time, "sleep"):
                self.prepare(allow_download=True)

        self.assertEqual(fetch.call_count, 2)
        model_assets.verify_models(self.target, "effnet", self.root)

    def test_explicit_fetch_repeated_failure_stops_at_three_attempts(self):
        (self.source / model_assets.EFFNET_EMBEDDING_MODEL).unlink()

        with mock.patch.object(model_assets.urllib.request, "urlopen", side_effect=urllib.error.URLError("fixture offline")) as fetch:
            with mock.patch.object(model_assets.time, "sleep"):
                with self.assertRaises(urllib.error.URLError):
                    self.prepare(allow_download=True)

        self.assertEqual(fetch.call_count, 3)
        self.assertEqual(list(self.target.glob("*.partial")), [])

    def test_interrupt_during_copy_cleans_partial(self):
        with mock.patch.object(model_assets.os, "fsync", side_effect=KeyboardInterrupt):
            with self.assertRaises(KeyboardInterrupt):
                self.prepare()

        self.assertEqual(list(self.target.glob("*.partial")), [])
        self.assertFalse((self.target / model_assets.EFFNET_EMBEDDING_MODEL).exists())

    @unittest.skipIf(os.name == "nt", "Unix symlink permissions")
    def test_symlink_model_is_rejected_and_public_cache_is_read_only_for_other_uids(self):
        self.prepare()
        path = self.target / model_assets.EFFNET_EMBEDDING_MODEL
        self.assertEqual(path.stat().st_mode & 0o777, 0o644)
        self.assertEqual(self.target.stat().st_mode & 0o777, 0o755)
        path.unlink()
        path.symlink_to(self.source / model_assets.EFFNET_EMBEDDING_MODEL)

        with self.assertRaisesRegex(ValueError, "symbolic link"):
            model_assets.verify_models(self.target, "effnet", self.root)

    def test_entrypoint_verifies_selected_subset_and_refuses_bypass(self):
        self.prepare()
        for name in ("entrypoint.sh", "model_assets.py"):
            shutil.copyfile(SOURCE_ROOT / name, self.root / name)
        environment = dict(os.environ, BACKBONE="effnet", ESSENTIA_MODEL_DIR=str(self.target), VERIFY_MODELS="false")
        command = ["bash", str(self.root / "entrypoint.sh"), sys.executable, "-c", "print('fixture-command-ran')"]

        good = subprocess.run(command, env=environment, text=True, capture_output=True, check=False)
        self.assertEqual(good.returncode, 0, good.stderr)
        self.assertIn("fixture-command-ran", good.stdout)
        (self.target / model_assets.EFFNET_EMBEDDING_MODEL).write_bytes(b"corrupt")

        bad = subprocess.run(command, env=environment, text=True, capture_output=True, check=False)
        self.assertNotEqual(bad.returncode, 0)
        self.assertIn("Missing/corrupt required effnet model", bad.stderr)
        self.assertNotIn("fixture-command-ran", bad.stdout)

    def test_backbone_cli_and_environment_must_agree(self):
        with mock.patch.dict(os.environ, {"BACKBONE": "effnet"}):
            with self.assertRaisesRegex(ValueError, "conflicts"):
                model_assets.runtime_backbone(["python", "classify.py", "--backbone", "maest"])

        with mock.patch.dict(os.environ, clear=True):
            self.assertEqual(model_assets.runtime_backbone(["python", "classify.py", "--backbone=maest"]), "maest")


if __name__ == "__main__":
    unittest.main()
