import json
import os
from pathlib import Path
import shlex
import stat
import subprocess
import tempfile
import unittest


SCRIPT = Path(__file__).resolve().parents[1] / "init.sh"


class CredentialHandoffTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="garage-handoff-offline-")
        self.root = Path(self.temporary.name)
        self.output = self.root / "output"
        self.output.mkdir()
        self.handoff = self.output / "garage-credentials.env"
        self.receipt = self.output / "garage-credentials.persisted"
        self.calls = self.root / "calls.jsonl"
        self.aws_calls = self.root / "aws-calls.jsonl"
        config = self.root / "garage.toml"
        config.write_text("# fixture config\n")
        cors = self.root / "cors.json"
        cors.write_text('{"CORSRules":[{"AllowedOrigins":["CORS_ORIGIN_PLACEHOLDER"]}]}')
        fake = self.root / "garage.py"
        fake.write_text('''import json, os, sys
from pathlib import Path
arguments = sys.argv[1:]
with Path(os.environ["FIXTURE_CALLS"]).open("a") as log:
    log.write(json.dumps(arguments) + "\\n")
if arguments == ["status"]:
    pass
elif arguments == ["node", "id"]:
    print("fixture-node@127.0.0.1:3901")
elif arguments == ["json-api", "GetClusterLayout"]:
    print(json.dumps({"version": 1, "roles": [{"id": "fixture-node", "capacity": 25000000000}],
                      "stagedRoleChanges": [], "stagedParameters": None}))
elif arguments[:2] == ["bucket", "info"] or arguments[:2] == ["bucket", "website"] or arguments[:2] == ["bucket", "allow"]:
    pass
elif arguments[:2] == ["key", "info"]:
    keys = {"alexandria-master-key": ("GKmaster", "masterSecret0123"),
            "alexandria-preview-key": ("GKpreview", "previewSecret4567"),
            "alexandria-streaming-key": ("GKstreaming", "streamingSecret8901")}
    key, secret = keys[arguments[-1]]
    if "--show-secret" in arguments:
        print("Secret key: " + secret)
    else:
        print("Key ID: " + key)
else:
    raise SystemExit("Unexpected Garage mutation or fixture CLI request: " + repr(arguments))
''')
        fake_aws = self.root / "aws.py"
        fake_aws.write_text('''import json, os, sys
from pathlib import Path
with Path(os.environ["FIXTURE_AWS_CALLS"]).open("a") as log:
    log.write(json.dumps(sys.argv[1:]) + "\\n")
''')
        source = SCRIPT.read_text()
        source = source.replace('g() { /garage "$@"; }',
                                'g() { python3 ' + shlex.quote(str(fake)) + ' "$@"; }\n' +
                                'aws() { python3 ' + shlex.quote(str(fake_aws)) + ' "$@"; }')
        source = source.replace("/output", str(self.output))
        source = source.replace("/etc/garage.toml", str(config))
        source = source.replace("/config/cors.json", str(cors))
        source = source.replace("/tmp/cors.json", str(self.root / "rendered-cors.json"))
        source = source.replace("~/.aws", str(self.root / "aws"))
        self.script = self.root / "init.sh"
        self.script.write_text(source)

    def tearDown(self):
        self.temporary.cleanup()

    def run_init(self, generation="fixture-generation", force="false"):
        return subprocess.run(["/bin/sh", str(self.script)], check=False, text=True, capture_output=True,
                              env=dict(os.environ, ALEXANDRIA_GENERATION_ID=generation,
                                       CORS_ORIGIN="https://192.0.2.10", GARAGE_CAPACITY="25G",
                                       GARAGE_CAPACITY_CHANGE_REQUESTED="false", GARAGE_FORCE_CREDENTIAL_EXPORT=force,
                                       FIXTURE_CALLS=str(self.calls), FIXTURE_AWS_CALLS=str(self.aws_calls)))

    def credential_values(self):
        return dict(line.split("=", 1) for line in self.handoff.read_text().splitlines()
                    if line and not line.startswith("#"))

    def assert_no_rotation(self):
        calls = [json.loads(line) for line in self.calls.read_text().splitlines()]
        self.assertFalse(any(call[:2] == ["key", "create"] or call[:1] == ["layout"] for call in calls))

    def test_initial_export_is_atomic_protected_and_contains_established_keys(self):
        result = self.run_init()

        self.assertEqual(result.returncode, 0, result.stderr)
        values = self.credential_values()
        self.assertEqual(values["ALEXANDRIA_GENERATION_ID"], "fixture-generation")
        self.assertEqual(values["GARAGE_S3_SECRET_KEY"], "masterSecret0123")
        self.assertEqual(values["GARAGE_PREVIEW_SECRET"], "previewSecret4567")
        self.assertEqual(values["GARAGE_STREAMING_SECRET"], "streamingSecret8901")
        self.assertEqual(stat.S_IMODE(self.handoff.stat().st_mode), 0o600)
        self.assertEqual(list(self.output.glob(".garage-credentials.*")), [])
        self.assertNotIn("masterSecret0123", result.stdout + result.stderr)
        self.assert_no_rotation()

    def test_normal_repeat_validates_garage_and_cors_without_reexporting_consumed_handoff(self):
        self.assertEqual(self.run_init().returncode, 0)
        self.handoff.unlink()
        self.receipt.write_text("fixture-generation\n")
        self.receipt.chmod(0o600)
        self.calls.unlink()
        self.aws_calls.unlink()

        result = self.run_init()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertFalse(self.handoff.exists())
        self.assertIn("no credential handoff exported", result.stdout)
        calls = [json.loads(line) for line in self.calls.read_text().splitlines()]
        self.assertIn(["json-api", "GetClusterLayout"], calls)
        self.assertIn(["key", "info", "--show-secret", "alexandria-master-key"], calls)
        aws_calls = [json.loads(line) for line in self.aws_calls.read_text().splitlines()]
        self.assertEqual(sum("put-bucket-cors" in call for call in aws_calls), 5)
        self.assert_no_rotation()

    def test_explicit_recovery_reexports_the_same_established_keys(self):
        self.assertEqual(self.run_init().returncode, 0)
        established = self.credential_values()
        self.handoff.unlink()
        self.receipt.write_text("fixture-generation\n")

        result = self.run_init(force="true")

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(self.credential_values(), established)
        self.assert_no_rotation()

    def test_old_generation_receipt_never_suppresses_new_generation_export(self):
        self.receipt.write_text("previous-generation\n")

        result = self.run_init(generation="next-generation")

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(self.credential_values()["ALEXANDRIA_GENERATION_ID"], "next-generation")
        self.assert_no_rotation()

    def test_receipt_requires_exact_single_line_generation(self):
        self.receipt.write_text("fixture-generation\n\n")

        result = self.run_init()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertTrue(self.handoff.exists())

    def test_symlink_receipt_never_suppresses_export_or_modifies_target(self):
        target = self.root / "unrelated-receipt"
        target.write_text("fixture-generation\n")
        self.receipt.symlink_to(target)

        result = self.run_init()

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertTrue(self.handoff.exists())
        self.assertEqual(target.read_text(), "fixture-generation\n")

    def test_invalid_force_flag_fails_before_garage_changes(self):
        result = self.run_init(force="yes")

        self.assertNotEqual(result.returncode, 0)
        self.assertIn("must be true or false", result.stderr)
        self.assertFalse(self.calls.exists())
        self.assertFalse(self.handoff.exists())

    def test_interrupted_handoff_publication_preserves_previous_recovery_copy(self):
        self.assertEqual(self.run_init().returncode, 0)
        original = self.handoff.read_bytes()
        source = self.script.read_text()
        source = source.replace('handoff=$(mktemp ', '''mv() {
  for destination in "$@"; do :; done
  case "$destination" in
    */garage-credentials.env) return 1 ;;
    *) command mv "$@" ;;
  esac
}
handoff=$(mktemp ''')
        self.script.write_text(source)

        result = self.run_init(force="true")

        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(self.handoff.read_bytes(), original)
        self.assertEqual(list(self.output.glob(".garage-credentials.*")), [])
        self.assert_no_rotation()


if __name__ == "__main__":
    unittest.main()
