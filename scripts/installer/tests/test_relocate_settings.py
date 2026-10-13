import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("relocate", Path(__file__).parents[1] / "relocate-settings.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class RelocationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="alexandria migration ")
        self.root = Path(self.temp.name)
        self.old = self.root / "old settings"
        self.old.mkdir()
        self.source = self.root / "old source"
        self.source.mkdir()
        (self.source / "asset").write_text("original source asset")
        self.new_source = self.root / "new source"
        shutil.copytree(self.source, self.new_source)
        self.destination = self.root / "new settings"
        config = {"SchemaVersion": 2, "Stage": "complete", "GenerationId": "same-generation",
                  "ProjectName": "alexandria-existing", "InstallPath": str(self.old),
                  "SourceRoot": str(self.source), "ModelSourceDirectory": "/external/cache"}
        (self.old / ".env").write_text("DB_PASSWORD=synthetic-existing-secret\n")
        (self.old / ".env").chmod(0o600)
        (self.old / "docker-compose.yml").write_text(f"source: {self.source}\nsettings: {self.old}\n")
        (self.old / "private.key").write_bytes(b"synthetic-key-unchanged")
        (self.old / "private.key").chmod(0o600)
        (self.old / "alexandria-config.json").write_text(json.dumps(config))
        checkpoint = {"SchemaVersion": 1, "Stage": "complete", "GenerationId": "same-generation",
                      "Configuration": config, "ArtifactHashes": {
                          name: module.sha256(self.old / name) for name in (".env", "docker-compose.yml", "private.key")}}
        (self.old / ".alexandria-generation.json").write_text(json.dumps(checkpoint))

    def tearDown(self):
        self.temp.cleanup()

    def run_relocation(self):
        return module.relocate(self.old, self.destination, self.new_source)

    def test_paths_change_while_identity_secrets_keys_and_modes_survive(self):
        result = self.run_relocation()
        metadata = json.loads((self.destination / "alexandria-config.json").read_text())
        marker = json.loads((self.destination / ".alexandria-generation.json").read_text())
        self.assertEqual(metadata, marker["Configuration"])
        self.assertEqual(metadata["InstallPath"], str(self.destination))
        self.assertEqual(metadata["SourceRoot"], str(self.new_source))
        self.assertEqual(metadata["ModelSourceDirectory"], str(self.destination / "models"))
        self.assertEqual(result["project"], "alexandria-existing")
        self.assertEqual(result["generation"], "same-generation")
        for name in (".env", "private.key"):
            self.assertEqual((self.old / name).read_bytes(), (self.destination / name).read_bytes())
            self.assertEqual((self.destination / name).stat().st_mode & 0o777, 0o600)
        compose = (self.destination / "docker-compose.yml").read_text()
        self.assertNotIn(str(self.old), compose)
        self.assertNotIn(str(self.source), compose)
        for name, digest in marker["ArtifactHashes"].items():
            self.assertEqual(hashlib.sha256((self.destination / name).read_bytes()).hexdigest().upper(), digest)

    def test_modified_artifact_stops_without_writing_destination(self):
        (self.old / ".env").write_text("changed")
        with self.assertRaisesRegex(ValueError, "checkpoint"):
            self.run_relocation()
        self.assertFalse(self.destination.exists())

    def test_existing_destination_is_preserved(self):
        self.destination.mkdir()
        marker = self.destination / "keep"
        marker.write_text("existing")
        with self.assertRaisesRegex(ValueError, "already exists"):
            self.run_relocation()
        self.assertEqual(marker.read_text(), "existing")

    def test_changed_source_stops_before_copy(self):
        (self.new_source / "asset").write_text("different")
        with self.assertRaisesRegex(ValueError, "unchanged copy"):
            self.run_relocation()
        self.assertFalse(self.destination.exists())

    def test_symlink_stops_before_copy(self):
        (self.old / "linked-secret").symlink_to(self.old / ".env")
        with self.assertRaisesRegex(ValueError, "Symbolic links"):
            self.run_relocation()
        self.assertFalse(self.destination.exists())

    def test_checkpoint_traversal_is_rejected(self):
        path = self.old / ".alexandria-generation.json"
        checkpoint = json.loads(path.read_text())
        checkpoint["ArtifactHashes"]["../outside"] = "0" * 64
        path.write_text(json.dumps(checkpoint))
        with self.assertRaisesRegex(ValueError, "Unsafe artifact"):
            self.run_relocation()
        self.assertFalse(self.destination.exists())


if __name__ == "__main__":
    unittest.main()
